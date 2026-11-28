using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.DomainService;
using Hexalith.Timesheets.Contracts.Commands.TimeEntries;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.ReadModels.TimeEntries;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.TimeEntries.Commands;

namespace Hexalith.Timesheets.Server.TimeEntries;

/// <summary>Authorizes and folds single-owner Project lifecycle changes for EventStore persistence.</summary>
public sealed class ProjectTimeEntryEventStoreReceiver(
    ITimesheetsAccessGuard accessGuard,
    EventStoreMagicLinkConfirmationCapabilityStateLoader catalogReader,
    TimeEntryCommandService capture,
    TimeEntrySubmissionCommandService submission,
    TimeEntryApprovalCommandService approval,
    TimeEntryCorrectionCommandService correction,
    IApprovedTimeLedgerProjectionReader ledgerReader,
    TimeProvider clock)
{
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web)
    {
        AllowDuplicateProperties = false,
        RespectRequiredConstructorParameters = true
    };

    /// <summary>Gets the exact Project lifecycle owner payload types.</summary>
    internal static IReadOnlyList<Type> PayloadTypes { get; } =
        [typeof(CommitProjectTimeEntryRecord), typeof(CommitProjectTimeEntrySubmit),
            typeof(CommitProjectTimeEntryApprove), typeof(CommitProjectTimeEntryCorrect)];

    /// <summary>Checks whether the owner payload belongs to the Project lifecycle receiver.</summary>
    internal static bool Accepts(string type) => PayloadTypes.Any(payload => payload.FullName == type);

    /// <summary>Rechecks exact owner, authenticated actor, and current provider authority before emitting a transition.</summary>
    internal async Task<DomainResult> ProcessAsync(CommandEnvelope envelope, object? currentState, CancellationToken cancellationToken)
    {
        try
        {
            Type payloadType = PayloadTypes.Single(payload => payload.FullName == envelope.CommandType);
            var (command, tenant, actor, owner) = JsonSerializer.Deserialize(envelope.Payload, payloadType, s_json) switch
            {
                CommitProjectTimeEntryRecord { Command: { } item } intent =>
                    ((object)item, intent.Tenant, intent.Actor, item.TimeEntryId),
                CommitProjectTimeEntrySubmit { Command: { TimeEntryIds: [var entry] } item } intent =>
                    (item, intent.Tenant, intent.Actor, entry),
                CommitProjectTimeEntryApprove { Command: { } item } intent =>
                    (item, intent.Tenant, intent.Actor, item.TimeEntryId),
                CommitProjectTimeEntryCorrect { Command: { } item } intent =>
                    (item, intent.Tenant, intent.Actor, item.TimeEntryId),
                _ => throw new InvalidOperationException("The Project workflow command is malformed.")
            };

            if (tenant is null || actor is null || owner is null || tenant.TenantId != envelope.TenantId
                || owner.Value != envelope.AggregateId || !MagicLinkEventStoreDomainProcessor.HasVerifiedActor(envelope, actor.PartyId))
            {
                return DomainResult.NoOp();
            }

            TimesheetsRequestContext context = TimesheetsRequestContext.ForProjectWorkflow(tenant, actor, envelope.CorrelationId);
            var coarseGate = new TimesheetsAuthorizationRequest(context, TimesheetsOperation.Command);
            if (!(await accessGuard.AuthorizeAsync(coarseGate, cancellationToken).ConfigureAwait(false)).IsAuthorized
                || !await ledgerReader.IsDeliveryAvailableAsync(context, cancellationToken).ConfigureAwait(false))
            {
                return DomainResult.NoOp();
            }

            TimeEntryState? state = DomainStateReplay.Rehydrate<TimeEntryState>(currentState);
            if (state is { IsRecorded: true } && (!state.MatchesConfirmationTenant(tenant) || state.TimeEntryId != owner)
                || state?.IsRecorded != true && DomainStateReplay.SnapshotAware(currentState) is { CurrentSequence: > 0 }
                || command is not RecordTimeEntry && state?.Target?.TargetKind != TimeEntryTargetKind.Project)
            {
                return DomainResult.NoOp();
            }

            (TimeEntryTargetReference? target, PartyReference? contributor, ActivityTypeId? activity) = command switch
            {
                RecordTimeEntry item => (item.Target, item.Contributor, item.ActivityTypeId),
                CorrectApprovedTimeEntry item => (item.Target, item.Contributor, item.ActivityTypeId),
                _ => (state?.Target, state?.Contributor, state?.ActivityTypeId)
            };
            if (target?.TargetKind != TimeEntryTargetKind.Project || contributor is null || activity is null
                || command is RecordTimeEntry { ContributorCategory: not ContributorCategory.Employee } or RecordTimeEntry { ExternalSource: not null }
                || command is RecordTimeEntry or SubmitTimeEntriesForApproval && actor != contributor
                || command is SubmitTimeEntriesForApproval { SubmissionScope: not TimeEntrySubmissionScope.SelectedEntries })
            {
                return DomainResult.NoOp();
            }

            if (!(await accessGuard.AuthorizeAsync(coarseGate with
                {
                    Project = new ProjectReference(target.TargetId),
                    Contributor = contributor
                }, cancellationToken).ConfigureAwait(false)).IsAuthorized)
            {
                return DomainResult.NoOp();
            }

            ActivityTypeCatalogReadModel catalog = await catalogReader.LoadCatalogForTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
            if (catalog.ProjectionFreshness.State != ProjectionFreshnessState.Fresh
                || !catalog.Items.Any(item => item.ActivityTypeId == activity && item.IsActive && item.IsAvailableForCapture)
                || !MagicLinkConfirmationCapabilityCommandService.IsAllowedByProjectPolicy(tenant, target, activity, catalog, out _))
            {
                return DomainResult.NoOp();
            }

            DateTimeOffset now = clock.GetUtcNow();
            TimesheetsDomainResult? result = command switch
            {
                RecordTimeEntry item => (await capture.RecordAsync(context, item, state, catalog, cancellationToken).ConfigureAwait(false)).DomainResult,
                SubmitTimeEntriesForApproval item => (await submission.SubmitAsync(context, item,
                    new Dictionary<TimeEntryId, TimeEntryState?> { [owner] = state }, catalog, now,
                    cancellationToken).ConfigureAwait(false)).Entries.Single().DomainResult,
                ApproveTimeEntry item => (await approval.ApproveAsync(context, item, state, now, cancellationToken).ConfigureAwait(false)).DomainResult,
                CorrectApprovedTimeEntry item => (await correction.CorrectAsync(context, item, state, catalog, now, cancellationToken).ConfigureAwait(false)).DomainResult,
                _ => null
            };
            return result?.Events switch
            {
                [TimeEntryRecorded item] => DomainResult.Success([new StoredTimeEntryRecorded(item)]),
                [TimeEntrySubmitted item] => DomainResult.Success([new StoredTimeEntrySubmitted(item)]),
                [TimeEntryApproved item] => DomainResult.Success([new StoredTimeEntryApproved(item)]),
                [TimeEntryApprovedCorrected item] => DomainResult.Success([new StoredTimeEntryApprovedCorrected(item)]),
                _ => DomainResult.NoOp()
            };
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested
            && exception is JsonException or ArgumentException or InvalidOperationException or NullReferenceException
                or TargetInvocationException { InnerException: InvalidOperationException })
        {
            return DomainResult.NoOp();
        }
    }
}
