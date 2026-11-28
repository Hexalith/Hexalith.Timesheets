using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.Timesheets.Contracts;
using Hexalith.Timesheets.Contracts.Commands.TimeEntries;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.TimeEntries.Commands;

namespace Hexalith.Timesheets.Server.TimeEntries;

/// <summary>Submits one authenticated Project lifecycle intent and verifies EventStore completion.</summary>
public sealed class ProjectTimeEntryWorkflowSubmission(
    ITimesheetsAccessGuard accessGuard, IApprovedTimeLedgerProjectionReader ledgerReader,
    MagicLinkDurableSubmissionService durableSubmission)
{
    /// <summary>Submits a single-owner workflow command keyed by the authenticated caller and exact intent.</summary>
    public async Task<ProjectTimeEntrySubmissionOutcome> SubmitAsync(
        TimesheetsRequestContext context, TimeEntryId owner, object command, string requestKey, CancellationToken cancellationToken)
    {
        if (!context.IsProjectWorkflow || context.Tenant is null || context.Actor is null || owner is null
            || string.IsNullOrWhiteSpace(requestKey) || requestKey.Length > 128)
        {
            return ProjectTimeEntrySubmissionOutcome.Refused;
        }

        object? intent = command switch
        {
            RecordTimeEntry record when record.TimeEntryId == owner => new CommitProjectTimeEntryRecord(record, context.Tenant, context.Actor),
            SubmitTimeEntriesForApproval { TimeEntryIds: [var submitted] } submit when submitted == owner =>
                new CommitProjectTimeEntrySubmit(submit, context.Tenant, context.Actor),
            ApproveTimeEntry approve when approve.TimeEntryId == owner => new CommitProjectTimeEntryApprove(approve, context.Tenant, context.Actor),
            CorrectApprovedTimeEntry correct when correct.TimeEntryId == owner => new CommitProjectTimeEntryCorrect(correct, context.Tenant, context.Actor),
            _ => null
        };
        if (intent is null)
        {
            return ProjectTimeEntrySubmissionOutcome.Refused;
        }

        try
        {
            var admission = new TimesheetsAuthorizationRequest(context, TimesheetsOperation.Command);
            if (!(await accessGuard.AuthorizeAsync(admission, cancellationToken).ConfigureAwait(false)).IsAuthorized
                || !await ledgerReader.IsDeliveryAvailableAsync(context, cancellationToken).ConfigureAwait(false))
            {
                return ProjectTimeEntrySubmissionOutcome.Refused;
            }
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return ProjectTimeEntrySubmissionOutcome.Refused;
        }

        Type commandType = intent.GetType();
        string identity = JsonSerializer.Serialize(new { requestKey, owner, commandType.FullName, intent }, JsonSerializerOptions.Web);
        string messageId = MagicLinkDurableSubmissionService.CreateMessageId(identity);
        string tenant = context.Tenant.TenantId;
        try
        {
            CommandStatusQueryResponse? status = await durableSubmission.SubmitAndReadStatusAsync(
                messageId, tenant, owner.Value, intent, commandType, cancellationToken).ConfigureAwait(false);
            if (MagicLinkDurableSubmissionService.HasCommittedStatus(
                status, messageId, tenant, owner.Value, 1, allowMissingSequence: true))
            {
                return ProjectTimeEntrySubmissionOutcome.Committed;
            }

            if (MagicLinkDurableSubmissionService.HasCommittedStatus(
                status, messageId, tenant, owner.Value, 0, allowMissingSequence: true)
                || status is { IsRejected: true } && status.MessageId == messageId && status.TenantId == tenant
                    && status.Domain == TimesheetsDomain.Name && status.AggregateId == owner.Value)
            {
                return ProjectTimeEntrySubmissionOutcome.Refused;
            }

            return ProjectTimeEntrySubmissionOutcome.Indeterminate;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return ProjectTimeEntrySubmissionOutcome.Indeterminate;
        }
    }
}
