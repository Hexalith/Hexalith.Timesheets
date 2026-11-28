using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Timesheets.Contracts;
using Hexalith.Timesheets.Contracts.Commands.ExternalContributions;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.Policies;
using Hexalith.Timesheets.Server.Runtime;
using Hexalith.Timesheets.Server.TimeEntries.Commands;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.Timesheets.Server.TimeEntries;

/// <summary>Submits an external recording to its EventStore owner and verifies the stored batch.</summary>
/// <param name="gateway">The workload gateway used for recording commands and readback.</param>
/// <param name="durableSubmission">The bounded submission and readback service.</param>
/// <param name="policy">The external recording policy.</param>
/// <param name="commentIssuer">Optional issuer of server-owned publication decisions.</param>
/// <param name="pollingOptions">Optional polling and readback limits.</param>
/// <param name="logger">Optional outcome logger.</param>
/// <param name="clock">Optional clock for deadlines.</param>
public sealed class TimeEntryRecordSubmissionService(
    IEventStoreGatewayClient gateway,
    MagicLinkDurableSubmissionService durableSubmission,
    ExternalContributionPolicyOptions policy,
    TimeEntryCommentPublicationIssuer? commentIssuer = null,
    IOptions<MagicLinkSubmissionPollingOptions>? pollingOptions = null,
    ILogger<TimeEntryRecordSubmissionService>? logger = null,
    TimeProvider? clock = null)
{
    private const int RecoveryPageSize = 100;

    private static readonly JsonSerializerOptions _strictJsonOptions = new(JsonSerializerDefaults.Web)
    {
        AllowDuplicateProperties = false,
        RespectRequiredConstructorParameters = true
    };

    private readonly MagicLinkSubmissionPollingOptions _polling = pollingOptions is null
        ? durableSubmission.PollingOptions
        : MagicLinkDurableSubmissionService.SnapshotPollingOptions(pollingOptions);
    private readonly TimeProvider _clock = clock ?? durableSubmission.Clock;
    private readonly TimeEntryCommentPublicationIssuer _commentIssuer = commentIssuer
        ?? new TimeEntryCommentPublicationIssuer();

    /// <summary>Creates a recording service with the existing submission coordinator's polling limits and clock.</summary>
    public TimeEntryRecordSubmissionService(
        IEventStoreGatewayClient gateway,
        MagicLinkDurableSubmissionService durableSubmission,
        ExternalContributionPolicyOptions policy,
        TimeEntryCommentPublicationIssuer? commentIssuer)
        : this(gateway, durableSubmission, policy, commentIssuer, null, null, null)
    {
    }

    /// <summary>Returns true only after verifying the exact recording batch on the owner stream.</summary>
    public async Task<bool> SubmitAsync(
        TimesheetsRequestContext context,
        SubmitExternalTimeEntry? command,
        DateTimeOffset submittedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Tenant is null
            || string.IsNullOrWhiteSpace(context.Tenant.TenantId)
            || context.Actor is null
            || string.IsNullOrWhiteSpace(context.Actor.PartyId)
            || command?.TimeEntryId is null
            || string.IsNullOrWhiteSpace(command.TimeEntryId.Value)
            || command.Target is null
            || string.IsNullOrWhiteSpace(command.Target.TargetId)
            || command.Contributor is null
            || string.IsNullOrWhiteSpace(command.Contributor.PartyId)
            || command.ActivityTypeId is null
            || string.IsNullOrWhiteSpace(command.ActivityTypeId.Value)
            || command.Source is null
            || string.IsNullOrWhiteSpace(command.Source.SourceSystem)
            || string.IsNullOrWhiteSpace(command.Source.ExternalRequestId)
            || submittedAtUtc == default
            || submittedAtUtc.Offset != TimeSpan.Zero)
        {
            return RecordAndReturn(false, ExternalRecordingLog.PolicyRefusal);
        }

        long started = _clock.GetTimestamp();
        using CancellationTokenSource timeout = new(_polling.Timeout, _clock);
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        CommitTimeEntryRecord intent = new(command, context.Tenant, context.Actor, submittedAtUtc);
        string tenant = context.Tenant.TenantId;
        string aggregateId = command.TimeEntryId.Value;
        string messageId = MagicLinkDurableSubmissionService.CreateMessageId(
            JsonSerializer.Serialize(new[]
            {
                "record", tenant, context.Actor.PartyId, aggregateId,
                command.Source.SourceSystem, command.Source.ExternalRequestId
            }));
        int expectedCount = policy.InitialApprovalState == TimeEntryApprovalState.Submitted ? 2 : 1;
        try
        {
            TimeEntryComment? expectedComment = _commentIssuer.Issue(context, command.Comment, evaluateFinance: false);
            deadline.Token.ThrowIfCancellationRequested();
            (CommandStatusQueryResponse? status, bool wasPresentAtPreflight) =
                await durableSubmission.SubmitAndReadStatusWithPreflightAsync(
                    messageId, tenant, aggregateId, intent, typeof(CommitTimeEntryRecord), cancellationToken,
                    deadline, _polling, _clock, started, enforceRecordingStatus: true)
                    .ConfigureAwait(false);
            deadline.Token.ThrowIfCancellationRequested();

            if (IsRejectedStatus(status, messageId, tenant, aggregateId))
            {
                return RecordAndReturn(false, ExternalRecordingLog.PolicyRefusal);
            }

            if (status?.CorrelationId != messageId
                || !MagicLinkDurableSubmissionService.HasCommittedStatus(
                    status, messageId, tenant, aggregateId, expectedCount, allowMissingSequence: true))
            {
                return RecordAndReturn(false, ExternalRecordingLog.IndeterminateCommit);
            }

            bool verified;
            if (status!.CommittedEventSequence is long lastSequence)
            {
                StreamReadPage page = await ReadCommittedRecordPageAsync(
                    new StreamReadRequest(tenant, TimesheetsEventStoreIntegration.DomainName, aggregateId,
                        FromSequence: lastSequence - expectedCount, PageSize: expectedCount),
                    lastSequence, deadline.Token).ConfigureAwait(false);
                verified = MatchesCommittedBatch(page.Events, intent, messageId, lastSequence, expectedComment, wasPresentAtPreflight);
            }
            else
            {
                verified = await ReadRecoveredBatchAsync(intent, messageId, expectedCount, expectedComment,
                    wasPresentAtPreflight, deadline.Token).ConfigureAwait(false);
            }
            deadline.Token.ThrowIfCancellationRequested();
            if (!verified)
            {
                return RecordAndReturn(false, ExternalRecordingLog.IndeterminateCommit);
            }

            return RecordAndReturn(true, wasPresentAtPreflight ? ExternalRecordingLog.AlreadyRecorded : ExternalRecordingLog.Committed);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return RecordAndReturn(false, ExternalRecordingLog.IndeterminateCommit);
        }
    }

    private async Task<bool> ReadRecoveredBatchAsync(
        CommitTimeEntryRecord intent, string messageId, int expectedCount, TimeEntryComment? expectedComment,
        bool wasPresentAtPreflight, CancellationToken deadlineToken)
    {
        while (true)
        {
            deadlineToken.ThrowIfCancellationRequested();
            try
            {
                bool? verified = await TryRecoverCommittedBatchAsync(intent, messageId, expectedCount, expectedComment,
                    wasPresentAtPreflight, deadlineToken).ConfigureAwait(false);
                if (verified.HasValue)
                {
                    return verified.Value;
                }
            }
            catch (Exception exception) when (IsMissingStream(exception)
                || MagicLinkDurableSubmissionService.IsRetryableGatewayFailure(exception, deadlineToken, includeAllServerErrors: true))
            {
                // Retry the owner read within the same submission deadline.
            }

            await Task.Delay(_polling.PollInterval, _clock, deadlineToken).ConfigureAwait(false);
        }
    }

    private async Task<bool?> TryRecoverCommittedBatchAsync(
        CommitTimeEntryRecord intent, string messageId, int expectedCount, TimeEntryComment? expectedComment,
        bool wasPresentAtPreflight, CancellationToken deadlineToken)
    {
        var request = new StreamReadRequest(intent.Tenant.TenantId, TimesheetsEventStoreIntegration.DomainName,
            intent.Command.TimeEntryId.Value, PageSize: RecoveryPageSize);
        var batch = new List<StreamReadEvent>(expectedCount);
        long sequence = 0;
        while (true)
        {
            deadlineToken.ThrowIfCancellationRequested();
            StreamReadPage page = await MagicLinkDurableSubmissionService.ReadStoredEventsAsync(
                gateway, request, _polling.ReadbackTimeout, _clock, deadlineToken).ConfigureAwait(false);
            ValidatePage(request, page);
            foreach (StreamReadEvent stored in page.Events)
            {
                if (stored.SequenceNumber != ++sequence)
                {
                    return false;
                }

                bool sharesLineage = stored.CorrelationId == messageId || stored.CausationId == messageId;
                if (batch.Count == expectedCount)
                {
                    return !sharesLineage;
                }

                if (batch.Count == 0 && !sharesLineage)
                {
                    continue;
                }

                if (stored.UserId != intent.Actor.PartyId
                    || !MagicLinkDurableSubmissionService.HasStoredEventIdentity(stored, messageId))
                {
                    return false;
                }

                batch.Add(stored);
                if (batch.Count == expectedCount
                    && !MatchesCommittedBatch(batch, intent, messageId, sequence, expectedComment, wasPresentAtPreflight))
                {
                    return false;
                }
            }

            if (sequence == page.Metadata.LatestSequence)
            {
                return batch.Count == expectedCount ? true : null;
            }

            if (page.Events.Count == 0)
            {
                return null;
            }

            request = request with { FromSequence = sequence };
        }
    }

    private async Task<StreamReadPage> ReadCommittedRecordPageAsync(
        StreamReadRequest request, long lastSequence, CancellationToken deadlineToken)
    {
        while (true)
        {
            deadlineToken.ThrowIfCancellationRequested();
            try
            {
                StreamReadPage page = await MagicLinkDurableSubmissionService.ReadStoredEventsAsync(
                    gateway, request, _polling.ReadbackTimeout, _clock, deadlineToken).ConfigureAwait(false);
                long lastRead = ValidatePage(request, page);
                if (page.Events.Count == request.PageSize && lastRead >= lastSequence)
                {
                    return page;
                }
            }
            catch (Exception exception) when (IsMissingStream(exception)
                || MagicLinkDurableSubmissionService.IsRetryableGatewayFailure(exception, deadlineToken, includeAllServerErrors: true))
            {
                // A transient read failure does not justify submitting again.
            }

            await Task.Delay(_polling.PollInterval, _clock, deadlineToken).ConfigureAwait(false);
        }
    }

    private static long ValidatePage(StreamReadRequest request, StreamReadPage page)
    {
        long lastRead = StreamReadPageValidator.ValidateAndGetNextSequence(request, page);
        if (!page.Metadata.IsTruncated && page.Metadata.NextContinuationToken is not null)
        {
            throw new InvalidOperationException("A complete stream page cannot have a continuation.");
        }

        return lastRead;
    }

    private static bool IsMissingStream(Exception exception)
        => exception is EventStoreGatewayException { StatusCode: 404, ReasonCode: StreamReplayReasonCodes.MissingStream };

    private static bool MatchesCommittedBatch(
        IReadOnlyList<StreamReadEvent> events, CommitTimeEntryRecord intent, string messageId, long lastSequence,
        TimeEntryComment? expectedComment, bool wasPresentAtPreflight)
    {
        SubmitExternalTimeEntry command = intent.Command;
        if (!MatchesStoredEvent(events[0], messageId, lastSequence - events.Count + 1)
            || MagicLinkDurableSubmissionService.Deserialize<TimeEntryRecorded>(events[0], _strictJsonOptions) is not { } recorded
            || recorded.TimeEntryId != command.TimeEntryId
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
            || !MatchesRecordedComment(recorded.Comment, expectedComment, command.Comment, wasPresentAtPreflight)
            || recorded.ExternalSource != command.Source)
        {
            return false;
        }

        if (events.Count == 1)
        {
            return true;
        }

        return MatchesStoredEvent(events[1], messageId, lastSequence)
            && !StringComparer.Ordinal.Equals(events[0].MessageId, events[1].MessageId)
            && MagicLinkDurableSubmissionService.Deserialize<TimeEntrySubmitted>(events[1], _strictJsonOptions) is { } submitted
            && submitted.TimeEntryId == command.TimeEntryId
            && submitted.Tenant == intent.Tenant
            && submitted.Submitter == intent.Actor
            && submitted.TimeEntrySubmissionId?.Value == command.Source.ExternalRequestId
            && submitted.SubmissionScope == TimeEntrySubmissionScope.SelectedEntries
            && submitted.ApprovalState == TimeEntryApprovalState.Submitted
            && submitted.SubmittedAtUtc != default
            && submitted.SubmittedAtUtc.Offset == TimeSpan.Zero
            && submitted.SubmittedAtUtc <= intent.SubmittedAtUtc;
    }

    private static bool MatchesStoredEvent(StreamReadEvent stored, string messageId, long sequence)
        => stored.SequenceNumber == sequence
            && MagicLinkDurableSubmissionService.HasStoredEventIdentity(stored, messageId)
            && TimeEntryConfirmationSubmissionService.HasSupportedEventMetadata(stored);

    private static bool IsRejectedStatus(CommandStatusQueryResponse? status, string messageId, string tenant, string aggregateId)
        => status?.IsRejected == true
            && status.MessageId == messageId
            && status.CorrelationId == messageId
            && status.TenantId == tenant
            && status.Domain == TimesheetsEventStoreIntegration.DomainName
            && status.AggregateId == aggregateId;

    private bool RecordAndReturn(bool accepted, string outcome)
    {
        ExternalRecordingLog.RecordOutcome(logger, outcome);
        return accepted;
    }

    private static bool MatchesRecordedComment(
        TimeEntryComment? stored, TimeEntryComment? expected, TimeEntryComment? requested, bool existingCommand)
    {
        if (stored?.Policy is not { } issued || expected?.Policy is not { } publication)
        {
            return stored is null && expected is null;
        }

        if ((!existingCommand && issued.ServerPublicationVersion != TimesheetsDomain.CommentPublicationVersion)
            || (issued.ServerExportInclusion == TimesheetsCommentPolicyDecision.Allowed
                && requested?.Policy.RedactionRequirement is not (TimesheetsCommentRedactionRequirement.NotRequired
                    or TimesheetsCommentRedactionRequirement.RequiredBeforeExternalDisclosure)))
        {
            return false;
        }

        // Finance authority belongs to the authorized owner that emitted the event.
        return MagicLinkDurableSubmissionService.CommentsMatchExpected(stored, new TimeEntryComment(expected.Text, publication with
        {
            ServerPublicationVersion = issued.ServerPublicationVersion,
            ServerExportInclusion = issued.ServerExportInclusion
        }));
    }
}
