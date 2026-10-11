using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Attributes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.Runtime;

namespace Hexalith.Timesheets.Projections.TimeEntries;

/// <summary>Materializes validated, aggregate-owned Time Entry evidence.</summary>
/// <param name="readModelStore">The SDK read-model store used for owned evidence.</param>
[EventStoreDomain(TimesheetsEventStoreIntegration.DomainName)]
public sealed class TimeEntryEvidenceProjectionHandler(IReadModelStore readModelStore) :
    IAsyncDomainProjectionRebuildHandler,
    IDeclaresProjectionReadModelSlots
{
    /// <summary>Gets the canonical aggregate-owned evidence slot.</summary>
    public static IReadOnlyList<ProjectionReadModelSlotDeclaration> ProjectionReadModelSlots { get; } =
    [
        new(
            TimesheetsEventStoreIntegration.DomainName,
            TimeEntryEvidenceProjection.ProjectionName,
            TimeEntryEvidenceReadModelAddress.SlotName,
            ProjectionReadModelSlotKind.AggregateOwned,
            declaresCanonicalWriter: true)
    ];

    /// <inheritdoc />
    public string Domain => TimesheetsEventStoreIntegration.DomainName;

    /// <inheritdoc />
    public string ProjectionType => TimeEntryEvidenceProjection.ProjectionName;

    /// <inheritdoc />
    public DomainProjectionRebuildSemantics RebuildSemantics => DomainProjectionRebuildSemantics.FullReplay;

    /// <inheritdoc />
    public async Task<DomainProjectionHandlerResult> ProjectAsync(
        ProjectionRequest request,
        string dispatchId,
        CancellationToken cancellationToken)
    {
        Validate(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(dispatchId);
        cancellationToken.ThrowIfCancellationRequested();

        TimeEntryEvidenceReadModel? incoming;
        try
        {
            incoming = Fold(request);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or JsonException)
        {
            return DomainProjectionHandlerResult.Failed(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }

        if (incoming is null)
        {
            return DomainProjectionHandlerResult.AlreadyCompleted();
        }

        try
        {
            _ = await ReadModelWritePolicy.UpdateAsync<TimeEntryEvidenceReadModel>(
                readModelStore,
                TimeEntryEvidenceReadModelAddress.StateStoreName,
                TimeEntryEvidenceReadModelAddress.StateKey(request.TenantId, incoming.TimeEntryId),
                current => GuardMerge(current, incoming),
                new ReadModelWriteContext(TimeEntryEvidenceReadModelAddress.SlotName, ProjectionType)
                    .WithEventDiagnostics(request.Events),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return DomainProjectionHandlerResult.Completed();
        }
        catch (ProjectionFoldException)
        {
            return DomainProjectionHandlerResult.Failed(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return DomainProjectionHandlerResult.Retryable(ProjectionDispatchReasonCodes.DeliveryStateUnavailable);
        }
    }

    /// <inheritdoc />
    public async Task<DomainProjectionRebuildPlan> PrepareRebuildAsync(
        ProjectionRequest request,
        string operationId,
        CancellationToken cancellationToken)
    {
        Validate(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        cancellationToken.ThrowIfCancellationRequested();

        TimeEntryEvidenceReadModel? rebuilt;
        try
        {
            rebuilt = Fold(request);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or JsonException)
        {
            throw new DomainProjectionRebuildRejectedException(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }
        string key = TimeEntryEvidenceReadModelAddress.StateKey(request.TenantId, new TimeEntryId(request.AggregateId));
        ReadModelEntry<TimeEntryEvidenceReadModel> current = await readModelStore
            .GetAsync<TimeEntryEvidenceReadModel>(
                TimeEntryEvidenceReadModelAddress.StateStoreName,
                key,
                cancellationToken).ConfigureAwait(false);
        ReadModelBatchConcurrency concurrency = current.ETag is { Length: > 0 }
            ? ReadModelBatchConcurrency.Match(current.ETag)
            : rebuilt is null
                ? ReadModelBatchConcurrency.IdempotentAbsent
                : ReadModelBatchConcurrency.CreateOnly;
        ReadModelBatchOperation operation = rebuilt is null
            ? ReadModelBatchOperation.Delete(
                key,
                concurrency)
            : ReadModelBatchOperation.Write(
                key,
                rebuilt,
                concurrency);
        return new DomainProjectionRebuildPlan(TimeEntryEvidenceReadModelAddress.StateStoreName, [operation]);
    }

    private static TimeEntryEvidenceReadModel? Fold(ProjectionRequest request)
    {
        IReadOnlyList<ProjectionEventDto> normalized = ProjectionEventReader.Normalize(request.Events);
        var evidence = new List<TimeEntryProjectionEvent>();
        List<string> payloadLineage = normalized.Select(static item => Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(item.EventTypeName + "\0")
                .Concat(item.Payload).ToArray()))).ToList();
        var seenRecording = false;
        TimeEntryCorrectionValues? precedingValues = null;
        ContributorCategory contributorCategory = ContributorCategory.Unknown;
        var entryId = new TimeEntryId(request.AggregateId);
        foreach (ProjectionEventDto item in normalized)
        {
            object? payload = Decode(item);
            if (payload is null)
            {
                continue;
            }

            ValidateEvidence(payload);
            TimeEntryId owner = OwnerOf(payload);
            if (!string.Equals(owner.Value, request.AggregateId, StringComparison.Ordinal)
                || !TimeEntryEvidenceProjection.MatchesTenant(payload, request.TenantId))
            {
                throw new InvalidOperationException("Time Entry evidence does not match its owner.");
            }

            if (payload is TimeEntryRecorded)
            {
                if (seenRecording)
                {
                    throw new InvalidOperationException("Time Entry evidence has a second recording.");
                }

                seenRecording = true;
                var recorded = (TimeEntryRecorded)payload;
                contributorCategory = recorded.ContributorCategory;
                precedingValues = new TimeEntryCorrectionValues(
                    recorded.Target, recorded.Contributor, recorded.ActivityTypeId,
                    recorded.ServiceDate, recorded.DurationMinutes, recorded.BillableState,
                    recorded.ContributorCategory, recorded.AiMetrics)
                {
                    ActivityTypeScope = recorded.ActivityTypeScope,
                    Comment = recorded.Comment
                };
            }
            else if (!seenRecording)
            {
                throw new InvalidOperationException("Time Entry evidence precedes recording.");
            }

            if (payload is TimeEntryContributorConfirmed confirmation
                && (contributorCategory != ContributorCategory.ExternalContributor
                    || confirmation.Contributor != precedingValues?.Contributor)
                || payload is TimeEntryAdjustedThroughMagicLink adjustment
                && (contributorCategory != ContributorCategory.ExternalContributor
                    || adjustment.Contributor != precedingValues?.Contributor
                    || !Equals(adjustment.PreviousValues, precedingValues)
                    || adjustment.AdjustedValues.Target != precedingValues?.Target
                    || adjustment.AdjustedValues.Contributor != precedingValues.Contributor)
                || payload is TimeEntryCorrected correction
                && !Equals(correction.PreviousValues, precedingValues)
                || payload is TimeEntryApprovedCorrected approvedCorrection
                && !Equals(approvedCorrection.PreviousValues, precedingValues))
            {
                throw new InvalidOperationException("Time Entry transition contradicts its preceding state.");
            }

            precedingValues = payload switch
            {
                TimeEntryAdjustedThroughMagicLink adjusted => adjusted.AdjustedValues,
                TimeEntryCorrected corrected => corrected.CorrectedValues,
                TimeEntryApprovedCorrected corrected => corrected.CorrectedValues,
                _ => precedingValues
            };

            evidence.Add(new TimeEntryProjectionEvent(
                string.IsNullOrWhiteSpace(item.MessageId)
                    ? item.SequenceNumber.ToString(CultureInfo.InvariantCulture)
                    : item.MessageId,
                item.SequenceNumber,
                payload,
                request.TenantId));
        }

        if (evidence.Count == 0)
        {
            return null;
        }

        long cursor = normalized.Max(static item => item.GlobalPosition > 0 ? item.GlobalPosition : item.SequenceNumber);
        TimeEntryEvidenceReadModel? model = new TimeEntryEvidenceProjection().Project(
            request.TenantId,
            entryId,
            evidence,
            new TimesheetsProjectionCheckpoint(
                request.TenantId,
                TimeEntryEvidenceProjection.ProjectionName,
                cursor,
                ProjectionFreshness.Fresh));
        if (model is null || model.EventLineage.Count != evidence.Count)
        {
            throw new InvalidOperationException("Time Entry evidence contains an invalid transition.");
        }

        return model with { EventPayloadLineage = payloadLineage };
    }

    private static object? Decode(ProjectionEventDto item)
    {
        StoredTimeEntryRecorded? recorded = ProjectionEventReader.Deserialize<StoredTimeEntryRecorded>(item);
        if (recorded is not null) return recorded.Event ?? throw new InvalidOperationException("Recorded evidence is missing.");
        StoredTimeEntrySubmitted? submitted = ProjectionEventReader.Deserialize<StoredTimeEntrySubmitted>(item);
        if (submitted is not null) return submitted.Event ?? throw new InvalidOperationException("Submitted evidence is missing.");
        StoredTimeEntryConfirmed? confirmed = ProjectionEventReader.Deserialize<StoredTimeEntryConfirmed>(item);
        if (confirmed is not null) return confirmed.Event ?? throw new InvalidOperationException("Confirmed evidence is missing.");
        StoredTimeEntryAdjusted? adjusted = ProjectionEventReader.Deserialize<StoredTimeEntryAdjusted>(item);
        if (adjusted is not null) return adjusted.Event ?? throw new InvalidOperationException("Adjusted evidence is missing.");
        return (object?)ProjectionEventReader.Deserialize<TimeEntryRecorded>(item)
            ?? ProjectionEventReader.Deserialize<TimeEntrySubmitted>(item)
            ?? ProjectionEventReader.Deserialize<TimeEntryContributorConfirmed>(item)
            ?? ProjectionEventReader.Deserialize<TimeEntryAdjustedThroughMagicLink>(item)
            ?? ProjectionEventReader.Deserialize<TimeEntryApproved>(item)
            ?? ProjectionEventReader.Deserialize<TimeEntryRejected>(item)
            ?? (object?)ProjectionEventReader.Deserialize<TimeEntryCorrected>(item)
            ?? ProjectionEventReader.Deserialize<TimeEntryApprovedCorrected>(item);
    }

    private static TimeEntryId OwnerOf(object payload) => payload switch
    {
        TimeEntryRecorded item => item.TimeEntryId,
        TimeEntrySubmitted item => item.TimeEntryId,
        TimeEntryContributorConfirmed item => item.TimeEntryId,
        TimeEntryAdjustedThroughMagicLink item => item.TimeEntryId,
        TimeEntryApproved item => item.TimeEntryId,
        TimeEntryRejected item => item.TimeEntryId,
        TimeEntryCorrected item => item.TimeEntryId,
        TimeEntryApprovedCorrected item => item.TimeEntryId,
        _ => throw new InvalidOperationException("Unrecognized Time Entry evidence.")
    };

    private static void ValidateEvidence(object payload)
    {
        bool valid = payload switch
        {
            TimeEntryRecorded recorded => recorded.TimeEntryId is not null
                && !string.IsNullOrWhiteSpace(recorded.TimeEntryId.Value)
                && recorded.Target is not null
                && !string.IsNullOrWhiteSpace(recorded.Target.TargetId)
                && recorded.Target.TargetKind is TimeEntryTargetKind.Project or TimeEntryTargetKind.Work
                && !string.IsNullOrWhiteSpace(recorded.Contributor?.PartyId)
                && !string.IsNullOrWhiteSpace(recorded.ActivityTypeId?.Value)
                && recorded.ActivityTypeScope is ActivityTypeScope.Tenant or ActivityTypeScope.Project
                && recorded.ServiceDate != default
                && recorded.DurationMinutes > 0
                && recorded.BillableState is BillableState.Billable or BillableState.NonBillable
                && recorded.ApprovalState == TimeEntryApprovalState.Draft
                && recorded.ContributorCategory != ContributorCategory.Unknown
                && ValidAiMetrics(recorded.AiMetrics, recorded.ContributorCategory)
                && ValidComment(recorded.Comment),
            TimeEntrySubmitted submitted => ValidOwner(submitted.TimeEntryId)
                && ValidParty(submitted.Submitter) && ValidTenant(submitted.Tenant)
                && submitted.SubmittedAtUtc != default
                && ValidId(submitted.TimeEntrySubmissionId?.Value)
                && submitted.SubmissionScope is TimeEntrySubmissionScope.SelectedEntries or TimeEntrySubmissionScope.TimesheetPeriod
                && submitted.ApprovalState == TimeEntryApprovalState.Submitted,
            TimeEntryContributorConfirmed confirmed => ValidOwner(confirmed.TimeEntryId)
                && ValidParty(confirmed.Contributor) && ValidTenant(confirmed.Tenant)
                && confirmed.ConfirmedAtUtc != default && ValidSource(confirmed.Source),
            TimeEntryAdjustedThroughMagicLink adjusted => ValidOwner(adjusted.TimeEntryId)
                && ValidParty(adjusted.Contributor) && ValidTenant(adjusted.Tenant)
                && adjusted.AdjustedAtUtc != default
                && adjusted.ActivityTypeScope is ActivityTypeScope.Tenant or ActivityTypeScope.Project
                && ValidValues(adjusted.PreviousValues) && ValidValues(adjusted.AdjustedValues)
                && ValidSource(adjusted.Source),
            TimeEntryApproved approved => ValidOwner(approved.TimeEntryId)
                && ValidParty(approved.Approver) && ValidTenant(approved.Tenant)
                && approved.DecidedAtUtc != default
                && ValidId(approved.TimeEntryApprovalDecisionId?.Value)
                && approved.ApprovalScope is TimeEntryApprovalScope.IndividualEntry or TimeEntryApprovalScope.TimesheetPeriod
                && ValidAuthority(approved.AuthoritySource)
                && ValidApprovalAction(approved.AuthoritySource, approved.ApprovalScope, approved.ApprovalState)
                && approved.ApprovalState == TimeEntryApprovalState.Approved,
            TimeEntryRejected rejected => ValidOwner(rejected.TimeEntryId)
                && ValidParty(rejected.Approver) && ValidTenant(rejected.Tenant)
                && rejected.DecidedAtUtc != default
                && ValidId(rejected.TimeEntryApprovalDecisionId?.Value)
                && rejected.ApprovalScope is TimeEntryApprovalScope.IndividualEntry or TimeEntryApprovalScope.TimesheetPeriod
                && ValidAuthority(rejected.AuthoritySource)
                && ValidApprovalAction(rejected.AuthoritySource, rejected.ApprovalScope, rejected.ApprovalState)
                && ValidId(rejected.Reason?.Value)
                && rejected.ApprovalState == TimeEntryApprovalState.Rejected,
            TimeEntryCorrected corrected => ValidOwner(corrected.TimeEntryId)
                && ValidId(corrected.TimeEntryCorrectionId?.Value)
                && ValidTenant(corrected.Tenant) && ValidParty(corrected.CorrectedBy)
                && corrected.CorrectedAtUtc != default
                && ValidValues(corrected.PreviousValues) && ValidValues(corrected.CorrectedValues)
                && ValidId(corrected.RejectionReason?.Value)
                && ValidId(corrected.RejectionDecisionId?.Value)
                && corrected.ApprovalState == TimeEntryApprovalState.Draft
                && corrected.CorrectionState != TimeEntryCorrectionState.Unknown,
            TimeEntryApprovedCorrected corrected => ValidOwner(corrected.TimeEntryId)
                && ValidId(corrected.TimeEntryCorrectionId?.Value)
                && ValidTenant(corrected.Tenant) && ValidParty(corrected.CorrectedBy)
                && corrected.CorrectedAtUtc != default
                && ValidValues(corrected.PreviousValues) && ValidValues(corrected.CorrectedValues)
                && ValidId(corrected.Reason?.Value)
                && ValidId(corrected.SourceApprovalDecisionId?.Value)
                && corrected.SourceApprovalScope is TimeEntryApprovalScope.IndividualEntry or TimeEntryApprovalScope.TimesheetPeriod
                && corrected.ApprovalState == TimeEntryApprovalState.Approved
                && corrected.CorrectionState != TimeEntryCorrectionState.Unknown,
            _ => true
        };
        if (!valid)
        {
            throw new InvalidOperationException("Time Entry evidence contains incomplete or contradictory state.");
        }
    }

    private static bool ValidId(string? value) => !string.IsNullOrWhiteSpace(value);

    private static bool ValidOwner(TimeEntryId? value) => ValidId(value?.Value);

    private static bool ValidTenant(TenantReference? value) => ValidId(value?.TenantId);

    private static bool ValidParty(PartyReference? value) => ValidId(value?.PartyId);

    private static bool ValidSource(ExternalContributionSource? value)
        => ValidId(value?.SourceSystem) && ValidId(value?.ExternalRequestId);

    private static bool ValidValues(TimeEntryCorrectionValues? value)
        => value is not null
            && value.Target is not null && ValidId(value.Target.TargetId)
            && value.Target.TargetKind is TimeEntryTargetKind.Project or TimeEntryTargetKind.Work
            && ValidParty(value.Contributor) && ValidId(value.ActivityTypeId?.Value)
            && value.ServiceDate != default && value.DurationMinutes > 0
            && value.BillableState is BillableState.Billable or BillableState.NonBillable
            && value.ContributorCategory != ContributorCategory.Unknown
            && (value.ActivityTypeScope is null or ActivityTypeScope.Tenant or ActivityTypeScope.Project)
            && ValidAiMetrics(value.AiMetrics, value.ContributorCategory)
            && ValidComment(value.Comment);

    private static bool ValidApprovalAction(ApprovalAuthoritySourceAttribution authority,
        TimeEntryApprovalScope scope, TimeEntryApprovalState decision)
        => authority.Action == ((scope, decision) switch
        {
            (TimeEntryApprovalScope.IndividualEntry, TimeEntryApprovalState.Approved) => ApprovalAuthorityAction.EntryApproval,
            (TimeEntryApprovalScope.IndividualEntry, TimeEntryApprovalState.Rejected) => ApprovalAuthorityAction.EntryRejection,
            (TimeEntryApprovalScope.TimesheetPeriod, TimeEntryApprovalState.Approved) => ApprovalAuthorityAction.PeriodApproval,
            (TimeEntryApprovalScope.TimesheetPeriod, TimeEntryApprovalState.Rejected) => ApprovalAuthorityAction.PeriodRejection,
            _ => ApprovalAuthorityAction.Unknown
        });

    private static bool ValidAiMetrics(AiEffortMetrics? metrics, ContributorCategory category)
    {
        if (metrics is null) return true;
        if (metrics.Availability == AiMetricAvailability.Unknown
            || metrics.TokenAvailability == AiTokenMetricAvailability.Unknown
            || metrics.Source is null || metrics.Source.SourceCategory == AiEffortMetricSourceCategory.Unknown
            || metrics.WallClockDurationMilliseconds < 0 || metrics.ModelRuntimeMilliseconds < 0
            || metrics.BillableEffortMinutes < 0 || metrics.ProviderInputTokenCount < 0
            || metrics.ProviderOutputTokenCount < 0 || metrics.ProviderTotalTokenCount < 0)
            return false;

        AiEffortMetricSourceMetadata source = metrics.Source;
        if (source.SourceCategory == AiEffortMetricSourceCategory.Provider && !ValidId(source.ProviderName)
            || source.SourceCategory == AiEffortMetricSourceCategory.Tool && !ValidId(source.ToolName)
            || source.SourceCategory == AiEffortMetricSourceCategory.WorkExecution && !ValidId(source.WorkExecutionId)
            || metrics.Availability is AiMetricAvailability.ProviderReported or AiMetricAvailability.Estimated
                && source.SourceCategory == AiEffortMetricSourceCategory.Unavailable)
            return false;

        bool anyToken = metrics.ProviderInputTokenCount is not null
            || metrics.ProviderOutputTokenCount is not null || metrics.ProviderTotalTokenCount is not null;
        if (metrics.TokenAvailability is AiTokenMetricAvailability.NotReported or AiTokenMetricAvailability.Unavailable
                && anyToken
            || metrics.TokenAvailability == AiTokenMetricAvailability.ProviderReported
                && (metrics.ProviderInputTokenCount is null || metrics.ProviderOutputTokenCount is null
                    || metrics.ProviderTotalTokenCount is null)
            || metrics.Availability == AiMetricAvailability.Unavailable
                && (anyToken || metrics.WallClockDurationMilliseconds is not null
                    || metrics.ModelRuntimeMilliseconds is not null || metrics.BillableEffortMinutes is not null)
            || category != ContributorCategory.AutomatedAgent
                && (metrics.Availability is AiMetricAvailability.ProviderReported or AiMetricAvailability.Estimated
                    || metrics.TokenAvailability == AiTokenMetricAvailability.ProviderReported))
            return false;
        return true;
    }

    private static bool ValidComment(TimeEntryComment? comment)
        => comment is null || !string.IsNullOrWhiteSpace(comment.Text)
            && comment.Text.Length <= TimeEntryComment.MaxLength
            && comment.Policy is { } policy
            && policy.InternalDisplay != TimesheetsCommentPolicyDecision.Unknown
            && policy.ExternalConfirmationDisplay != TimesheetsCommentPolicyDecision.Unknown
            && policy.ProjectionInclusion != TimesheetsCommentPolicyDecision.Unknown
            && policy.ExportInclusion != TimesheetsCommentPolicyDecision.Unknown
            && policy.SupportDiagnostics != TimesheetsCommentPolicyDecision.Unknown
            && policy.RedactionRequirement != TimesheetsCommentRedactionRequirement.Unknown
            && policy.RetentionCategory != TimesheetsEvidenceRetentionCategory.Unknown;

    private static bool ValidAuthority(ApprovalAuthoritySourceAttribution? value)
        => value is not null
            && value.Action != ApprovalAuthorityAction.Unknown
            && value.Source != ApprovalAuthoritySource.Unknown
            && value.DecisionState == ApprovalAuthorityDecisionState.Allowed
            && ValidId(value.PolicyKey) && ValidId(value.PolicyVersion)
            && value.Freshness is { State: ProjectionFreshnessState.Fresh };

    private static TimeEntryEvidenceReadModel GuardMerge(
        TimeEntryEvidenceReadModel? current,
        TimeEntryEvidenceReadModel incoming)
    {
        try
        {
            if (current is null) return incoming;
            if (current.TimeEntryId != incoming.TimeEntryId
                || current.EventPayloadLineage is null
                || current.EventPayloadLineage.Count < current.EventLineage.Count)
            {
                throw new InvalidOperationException("Stored Time Entry lineage is invalid.");
            }

            int common = Math.Min(current.EventPayloadLineage.Count, incoming.EventPayloadLineage.Count);
            if (!current.EventPayloadLineage.Take(common).SequenceEqual(incoming.EventPayloadLineage.Take(common), StringComparer.Ordinal)
                || !current.EventLineage.Where(item => item.Ordinal <= common)
                    .SequenceEqual(incoming.EventLineage.Where(item => item.Ordinal <= common)))
            {
                throw new InvalidOperationException("Time Entry history does not extend stored evidence.");
            }

            return incoming.EventPayloadLineage.Count > current.EventPayloadLineage.Count ? incoming : current;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or NullReferenceException)
        {
            throw new ProjectionFoldException(exception);
        }
    }

    private static void Validate(ProjectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(request.Domain, TimesheetsEventStoreIntegration.DomainName, StringComparison.Ordinal))
        {
            throw new ArgumentException("The projection request targets another domain.", nameof(request));
        }

        _ = new AggregateIdentity(request.TenantId, request.Domain, request.AggregateId);
    }
}
