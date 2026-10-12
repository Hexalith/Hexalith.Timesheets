using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Timesheets.Contracts.Commands.ExternalContributions;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.Policies;
using Hexalith.Timesheets.Server.Runtime;
using Hexalith.Timesheets.Server.TimeEntries.Commands;

namespace Hexalith.Timesheets.Server.TimeEntries;

/// <summary>Submits an external recording to its EventStore owner and verifies the stored batch.</summary>
public sealed class TimeEntryRecordSubmissionService(
    IEventStoreGatewayClient gateway,
    MagicLinkDurableSubmissionService durableSubmission,
    ExternalContributionPolicyOptions policy,
    TimeEntryCommentPublicationIssuer? commentIssuer = null)
{
    private readonly TimeEntryCommentPublicationIssuer _commentIssuer = commentIssuer
        ?? new TimeEntryCommentPublicationIssuer();

    /// <summary>Returns true only after a Completed status and an exact owner-stream readback.</summary>
    public async Task<bool> SubmitAsync(
        TimesheetsRequestContext context,
        SubmitExternalTimeEntry command,
        DateTimeOffset submittedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);
        if (context.Tenant is null
            || context.Actor is null
            || command.TimeEntryId is null
            || command.Target is null
            || command.Contributor is null
            || command.ActivityTypeId is null
            || command.Source is null
            || submittedAtUtc.Offset != TimeSpan.Zero)
        {
            return false;
        }

        CommitTimeEntryRecord intent = new(command, context.Tenant, context.Actor, submittedAtUtc);
        string messageId = MagicLinkDurableSubmissionService.CreateMessageId(
            JsonSerializer.Serialize(new[]
            {
                "record", context.Tenant.TenantId, context.Actor.PartyId, command.TimeEntryId.Value,
                command.Source.SourceSystem, command.Source.ExternalRequestId
            }));
        int expectedCount = policy.InitialApprovalState == TimeEntryApprovalState.Submitted ? 2 : 1;
        try
        {
            TimeEntryComment? expectedComment = _commentIssuer.Issue(context, command.Comment);
            var submission = await durableSubmission.SubmitAndReadStatusWithPreflightAsync(
                messageId, context.Tenant.TenantId, command.TimeEntryId.Value, intent,
                typeof(CommitTimeEntryRecord), cancellationToken).ConfigureAwait(false);
            CommandStatusQueryResponse? status = submission.Status;
            if (!MagicLinkDurableSubmissionService.HasCommittedStatus(
                status, messageId, context.Tenant.TenantId, command.TimeEntryId.Value, expectedCount))
            {
                return false;
            }

            long lastSequence = status!.CommittedEventSequence!.Value;
            StreamReadPage page = await gateway.ReadWorkloadStreamAsync(
                new StreamReadRequest(
                    context.Tenant.TenantId,
                    TimesheetsEventStoreIntegration.DomainName,
                    command.TimeEntryId.Value,
                    FromSequence: lastSequence - expectedCount,
                    PageSize: expectedCount),
                cancellationToken).ConfigureAwait(false);
            if (page.Tenant != context.Tenant.TenantId
                || page.Domain != TimesheetsEventStoreIntegration.DomainName
                || page.AggregateId != command.TimeEntryId.Value
                || page.Events.Count != expectedCount
                || page.Events[0].SequenceNumber != lastSequence - expectedCount + 1
                || string.IsNullOrWhiteSpace(page.Events[0].MessageId)
                || page.Events[0].CorrelationId != messageId)
            {
                return false;
            }

            TimeEntryRecorded? recorded = MagicLinkDurableSubmissionService.Deserialize<TimeEntryRecorded>(page.Events[0]);
            if (recorded?.TimeEntryId != command.TimeEntryId
                || recorded.Target != command.Target
                || recorded.Contributor != command.Contributor
                || recorded.ActivityTypeId != command.ActivityTypeId
                || recorded.ActivityTypeScope != ActivityTypeScope.Tenant
                || recorded.ServiceDate != command.ServiceDate
                || recorded.DurationMinutes != command.DurationMinutes
                || recorded.BillableState != command.BillableState
                || recorded.ApprovalState != TimeEntryApprovalState.Draft
                || recorded.ContributorCategory != ContributorCategory.ExternalContributor
                || recorded.AiMetrics is not null
                || !MagicLinkDurableSubmissionService.CommentsMatchExpected(
                    recorded.Comment, expectedComment)
                || recorded.ExternalSource != command.Source)
            {
                return false;
            }

            if (expectedCount == 1)
            {
                return true;
            }

            if (page.Events[1].SequenceNumber != lastSequence
                || string.IsNullOrWhiteSpace(page.Events[1].MessageId)
                || StringComparer.Ordinal.Equals(page.Events[0].MessageId, page.Events[1].MessageId)
                || page.Events[1].CorrelationId != messageId)
            {
                return false;
            }

            TimeEntrySubmitted? submitted = MagicLinkDurableSubmissionService.Deserialize<TimeEntrySubmitted>(page.Events[1]);
            return submitted?.TimeEntryId == command.TimeEntryId
                && submitted.Tenant == context.Tenant
                && submitted.Submitter == context.Actor
                && submitted.TimeEntrySubmissionId.Value == command.Source.ExternalRequestId
                && submitted.SubmissionScope == TimeEntrySubmissionScope.SelectedEntries
                && submitted.ApprovalState == TimeEntryApprovalState.Submitted
                && submitted.SubmittedAtUtc.Offset == TimeSpan.Zero
                && (submission.WasPresentAtPreflight || submitted.SubmittedAtUtc == submittedAtUtc);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
