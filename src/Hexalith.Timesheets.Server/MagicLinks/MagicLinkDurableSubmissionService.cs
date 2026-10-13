using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks.Commands;
using Hexalith.Timesheets.Server.Policies;
using Hexalith.Timesheets.Server.Runtime;
using Microsoft.Extensions.Options;

namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>Submits one Time Entry command and verifies its terminal status and event batch.</summary>
public sealed class MagicLinkDurableSubmissionService(
    IEventStoreGatewayClient gateway,
    IOptions<MagicLinkSubmissionPollingOptions> pollingOptions,
    TimeEntryCommentPublicationIssuer? commentIssuer = null)
{
    private const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly TimeEntryCommentPublicationIssuer _commentIssuer = commentIssuer
        ?? new TimeEntryCommentPublicationIssuer();
    private readonly MagicLinkSubmissionPollingOptions _polling = pollingOptions.Value.IsValid()
        ? pollingOptions.Value
        : throw new OptionsValidationException(
            nameof(MagicLinkSubmissionPollingOptions),
            typeof(MagicLinkSubmissionPollingOptions),
            ["Timeout must be positive and at most 30 seconds; PollInterval must be at least 10 ms and no greater than Timeout."]);

    /// <summary>Builds an internal commit intent from the server's resolved capability state.</summary>
    public Task<bool> SubmitResolvedUseAsync(
        MagicLinkCapabilityState capability,
        MagicLinkUseAction action,
        AdjustTimeThroughMagicLink? adjustment,
        DateTimeOffset usedAtUtc,
        string requestId,
        CancellationToken cancellationToken)
        => capability is { CapabilityId: { } capabilityId, Tenant: { } tenant, TimeEntryId: { } timeEntryId, TokenHash: { } hash }
            ? SubmitUseAsync(new CommitMagicLinkUse(
                capabilityId, tenant, timeEntryId, hash, action, adjustment, usedAtUtc),
                requestId, cancellationToken, capability)
            : Task.FromResult(false);

    /// <summary>Returns true only after an issued capability event is committed in its own stream.</summary>
    public async Task<bool> SubmitIssueAsync(CommitMagicLinkIssue intent, string requestId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        string messageId = CreateMessageId(
            $"issue\n{intent.Tenant.TenantId}\n{intent.Command.CapabilityId.Value}\n{requestId}");
        try
        {
            using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(_polling.Timeout);
            try
            {
                CommandStatusQueryResponse? status = await SubmitAndObserveStatusAsync(
                    messageId, intent.Tenant.TenantId, intent.Command.CapabilityId.Value, intent,
                    typeof(CommitMagicLinkIssue), cancellationToken, deadline.Token).ConfigureAwait(false);
                if (!HasCommittedStatus(status, messageId, intent.Tenant.TenantId, intent.Command.CapabilityId.Value, 1))
                {
                    return false;
                }

                StreamReadPage page;
                while (true)
                {
                    try
                    {
                        using CancellationTokenSource probe = CreateProbeDeadline(deadline.Token);
                        page = await gateway.ReadWorkloadStreamAsync(
                            new StreamReadRequest(
                                intent.Tenant.TenantId, TimesheetsEventStoreIntegration.DomainName,
                                intent.Command.CapabilityId.Value,
                                FromSequence: status!.CommittedEventSequence!.Value - 1,
                                PageSize: 1),
                            probe.Token).WaitAsync(probe.Token).ConfigureAwait(false);
                        if (!IsStaleReadback(page, status.CommittedEventSequence.Value, 1))
                        {
                            break;
                        }
                    }
                    catch (Exception exception) when (IsRetryableGatewayFailure(exception, cancellationToken))
                    {
                        // A completed command can become visible in the stream after its status.
                    }

                    await Task.Delay(_polling.PollInterval, deadline.Token).ConfigureAwait(false);
                }
                MagicLinkConfirmationCapabilityIssued? issued = page.Events.Count == 1
                    && page.Events[0].SequenceNumber == status.CommittedEventSequence
                        ? Deserialize<MagicLinkConfirmationCapabilityIssued>(page.Events[0])
                        : null;
                return page.Tenant == intent.Tenant.TenantId
                    && page.Domain == TimesheetsEventStoreIntegration.DomainName
                    && page.AggregateId == intent.Command.CapabilityId.Value
                    && page.Events.Count == 1
                    && HasStoredEventIdentity(page.Events[0], messageId)
                    && issued?.CapabilityId == intent.Command.CapabilityId
                    && issued.Tenant == intent.Tenant
                    && issued.TokenHash == intent.TokenHash
                    && issued.Issuer == intent.Issuer
                    && issued.Contributor == intent.Command.Scope.Contributor
                    && issued.Target == intent.Command.Scope.Target
                    && issued.ActivityTypeId == intent.Command.Scope.ActivityTypeId
                    && issued.TimeEntryId == intent.Command.Scope.TimeEntryId
                    && issued.TargetKind == intent.Command.Scope.TargetKind
                    && issued.AllowedAction == intent.Command.AllowedAction
                    && issued.ExpiresAtUtc == intent.Command.ExpiresAtUtc
                    && issued.Source == intent.Command.Source
                    && issued.IsSingleUse
                    && issued.IssuedAtUtc == intent.IssuedAtUtc;
            }
            finally
            {
                deadline.Cancel();
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>Returns true only after one terminal transition is committed in the Time Entry stream.</summary>
    public async Task<bool> SubmitTransitionAsync(CommitMagicLinkTransition intent, string requestId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        string messageId = CreateMessageId(
            $"transition\n{intent.Tenant.TenantId}\n{intent.CapabilityId.Value}\n{intent.Action}\n{requestId}");
        try
        {
            CommandStatusQueryResponse? status = await SubmitAndReadStatusAsync(
                messageId, intent.Tenant.TenantId, intent.TimeEntryId.Value, intent,
                typeof(CommitMagicLinkTransition), cancellationToken).ConfigureAwait(false);
            if (!HasCommittedStatus(status, messageId, intent.Tenant.TenantId, intent.TimeEntryId.Value, 1))
            {
                return false;
            }

            StreamReadPage page = await gateway.ReadWorkloadStreamAsync(
                new StreamReadRequest(
                    intent.Tenant.TenantId, TimesheetsEventStoreIntegration.DomainName,
                    intent.TimeEntryId.Value,
                    FromSequence: status!.CommittedEventSequence!.Value - 1,
                    PageSize: 1),
                cancellationToken).ConfigureAwait(false);
            if (page.Tenant != intent.Tenant.TenantId
                || page.Domain != TimesheetsEventStoreIntegration.DomainName
                || page.AggregateId != intent.TimeEntryId.Value
                || page.Events.Count != 1
                || page.Events[0].SequenceNumber != status.CommittedEventSequence
                || !HasStoredEventIdentity(page.Events[0], messageId))
            {
                return false;
            }

            return intent.Action == MagicLinkTransitionAction.Revoke
                ? Deserialize<MagicLinkConfirmationCapabilityRevoked>(page.Events[0]) is { } revoked
                    && revoked.CapabilityId == intent.CapabilityId
                    && revoked.Tenant == intent.Tenant
                    && revoked.TimeEntryId == intent.TimeEntryId
                    && revoked.RevokedBy == intent.Actor
                    && revoked.RevokedAtUtc == intent.AtUtc
                    && revoked.Source == intent.Source
                : Deserialize<MagicLinkConfirmationCapabilityExpired>(page.Events[0]) is { } expired
                    && expired.CapabilityId == intent.CapabilityId
                    && expired.Tenant == intent.Tenant
                    && expired.TimeEntryId == intent.TimeEntryId
                    && expired.ExpiredAtUtc == intent.AtUtc
                    && expired.Source == intent.Source;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>Returns true only for a verified two-event committed use.</summary>
    public Task<bool> SubmitUseAsync(CommitMagicLinkUse intent, string requestId, CancellationToken cancellationToken)
        => SubmitUseAsync(intent, requestId, cancellationToken, null);

    private async Task<bool> SubmitUseAsync(
        CommitMagicLinkUse intent,
        string requestId,
        CancellationToken cancellationToken,
        MagicLinkCapabilityState? capability)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        string messageId = CreateMessageId(intent, requestId);
        try
        {
            TimeEntryComment? expectedComment = intent.Action == MagicLinkUseAction.Adjust
                && capability is not null
                ? _commentIssuer.Issue(
                    MagicLinkExternalRequestContext.FromResolvedCapability(capability, messageId),
                    intent.Adjustment?.Comment)
                : null;
            using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(_polling.Timeout);
            try
            {
                CommandStatusQueryResponse? status = await SubmitAndObserveStatusAsync(
                    messageId, intent.Tenant.TenantId, intent.TimeEntryId.Value, intent,
                    typeof(CommitMagicLinkUse), cancellationToken, deadline.Token).ConfigureAwait(false);
                if (status is null)
                {
                    return false;
                }

                while (true)
                {
                    try
                    {
                        using CancellationTokenSource probe = CreateProbeDeadline(deadline.Token);
                        bool? verified = await VerifyCommittedBatchAsync(intent, messageId, status, capability,
                            expectedComment, probe.Token)
                            .WaitAsync(probe.Token).ConfigureAwait(false);
                        if (verified is not null)
                        {
                            return verified.Value;
                        }
                    }
                    catch (Exception exception) when (IsRetryableGatewayFailure(exception, cancellationToken))
                    {
                        // Retry transient reads within the same bounded command deadline.
                    }

                    await Task.Delay(_polling.PollInterval, deadline.Token).ConfigureAwait(false);
                }
            }
            finally
            {
                // Stop an in-flight submit response after terminal status and exact stored
                // evidence have already settled the caller's outcome.
                deadline.Cancel();
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Unknown commit outcomes remain opaque. A retry retains the same command identity.
        }

        return false;
    }

    private async Task<CommandStatusQueryResponse?> SubmitAndObserveStatusAsync(
        string messageId,
        string tenant,
        string aggregateId,
        object intent,
        Type commandType,
        CancellationToken callerCancellationToken,
        CancellationToken deadlineToken)
    {
        CommandStatusQueryResponse? status = null;
        try
        {
            status = await ProbeStatusAsync(tenant, messageId, deadlineToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsRetryableGatewayFailure(exception, callerCancellationToken))
        {
            // A transient status outage cannot prevent the first submission. Keep the
            // same message identity and require a terminal status plus exact readback.
        }

        Task<SubmitCommandResponse>? submission = null;
        if (status is null)
        {
            submission = gateway.SubmitWorkloadCommandAsync(new SubmitCommandRequest(
                messageId, tenant, TimesheetsEventStoreIntegration.DomainName, aggregateId,
                commandType.FullName!, JsonSerializer.SerializeToElement(intent, commandType, _jsonOptions),
                CorrelationId: messageId), deadlineToken);
            // EventStore may commit and expose a terminal status before the submit HTTP response
            // survives projection delivery. The status and exact stored batch, never this pending
            // transport response, authorize a successful external response.
            _ = submission.ContinueWith(static task => _ = task.Exception,
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        while (true)
        {
            deadlineToken.ThrowIfCancellationRequested();
            if (submission is { IsCompleted: true })
            {
                try
                {
                    SubmitCommandResponse accepted = await submission.ConfigureAwait(false);
                    if (accepted.MessageId != messageId)
                    {
                        return null;
                    }
                }
                catch (Exception) when (!callerCancellationToken.IsCancellationRequested)
                {
                    // Any failed submit response has an unknown commit outcome, including an HTTP
                    // resilience timeout. Only terminal status plus exact readback can prove success.
                }

                submission = null;
            }

            if (status?.Status is nameof(CommandStatus.Completed) or nameof(CommandStatus.Rejected)
                or nameof(CommandStatus.PublishFailed) or nameof(CommandStatus.TimedOut))
            {
                return status;
            }

            try
            {
                status = await ProbeStatusAsync(tenant, messageId, deadlineToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsRetryableGatewayFailure(exception, callerCancellationToken))
            {
                status = null;
            }

            if (status?.Status is nameof(CommandStatus.Completed) or nameof(CommandStatus.Rejected)
                or nameof(CommandStatus.PublishFailed) or nameof(CommandStatus.TimedOut))
            {
                return status;
            }

            await Task.Delay(_polling.PollInterval, deadlineToken).ConfigureAwait(false);
        }
    }

    private async Task<CommandStatusQueryResponse?> ProbeStatusAsync(
        string tenant, string messageId, CancellationToken deadlineToken)
    {
        using CancellationTokenSource probe = CreateProbeDeadline(deadlineToken);
        return await gateway.GetWorkloadCommandStatusAsync(tenant, messageId, probe.Token)
            .WaitAsync(probe.Token).ConfigureAwait(false);
    }

    private CancellationTokenSource CreateProbeDeadline(CancellationToken deadlineToken)
    {
        CancellationTokenSource probe = CancellationTokenSource.CreateLinkedTokenSource(deadlineToken);
        probe.CancelAfter(TimeSpan.FromMilliseconds(Math.Min(2_000, _polling.Timeout.TotalMilliseconds / 3)));
        return probe;
    }

    private static bool IsRetryableGatewayFailure(Exception exception, CancellationToken callerCancellationToken)
        => !callerCancellationToken.IsCancellationRequested
            && (exception is OperationCanceledException or TimeoutException or HttpRequestException
                || exception is EventStoreGatewayException { StatusCode: 408 or 429 or 500 or 502 or 503 or 504 });

    private async Task<bool?> VerifyCommittedBatchAsync(
        CommitMagicLinkUse intent,
        string messageId,
        CommandStatusQueryResponse status,
        MagicLinkCapabilityState? capability,
        TimeEntryComment? expectedComment,
        CancellationToken cancellationToken)
    {
        if (!StringComparer.Ordinal.Equals(status.MessageId, messageId)
            || status.Status != nameof(CommandStatus.Completed)
            || status.StatusCode != (int)CommandStatus.Completed
            || status.EventCount != 2
            || status.CommittedEventSequence is not >= 2
            || status.TenantId != intent.Tenant.TenantId
            || status.Domain != TimesheetsEventStoreIntegration.DomainName
            || status.AggregateId != intent.TimeEntryId.Value
            || status.RejectionEventType is not null
            || status.FailureReason is not null
            || status.Retryable is true)
        {
            return false;
        }

        long lastSequence = status.CommittedEventSequence.Value;
        StreamReadPage page = await gateway.ReadWorkloadStreamAsync(
            new StreamReadRequest(
                intent.Tenant.TenantId,
                TimesheetsEventStoreIntegration.DomainName,
                intent.TimeEntryId.Value,
                FromSequence: lastSequence - 2,
                PageSize: 2),
            cancellationToken).ConfigureAwait(false);
        if (IsStaleReadback(page, lastSequence, 2))
        {
            return null;
        }

        if (page.Tenant != intent.Tenant.TenantId
            || page.Domain != TimesheetsEventStoreIntegration.DomainName
            || page.AggregateId != intent.TimeEntryId.Value
            || page.Events.Count != 2
            || page.Events[0].SequenceNumber != lastSequence - 1
            || page.Events[1].SequenceNumber != lastSequence
            || !HasStoredEventIdentity(page.Events[0], messageId)
            || !HasStoredEventIdentity(page.Events[1], messageId)
            || StringComparer.Ordinal.Equals(page.Events[0].MessageId, page.Events[1].MessageId))
        {
            return false;
        }

        MagicLinkConfirmationCapabilityUsed? used = Deserialize<MagicLinkConfirmationCapabilityUsed>(page.Events[0]);
        if (used?.CapabilityId != intent.CapabilityId
            || used.Tenant != intent.Tenant
            || used.TimeEntryId != intent.TimeEntryId
            || (capability is not null && used.Contributor != capability.Contributor)
            || used.UsedAtUtc != intent.UsedAtUtc
            || used.Source.SourceSystem != "magic-link"
            || used.Source.SourceReference != intent.CapabilityId.Value
            || used.OutcomeCategory != (intent.Action == MagicLinkUseAction.Confirm ? "confirmed" : "adjusted"))
        {
            return false;
        }

        return intent.Action == MagicLinkUseAction.Confirm
            ? Deserialize<TimeEntryContributorConfirmed>(page.Events[1]) is { } confirmed
                && confirmed.TimeEntryId == intent.TimeEntryId
                && confirmed.Tenant == intent.Tenant
                && confirmed.Contributor == used.Contributor
                && confirmed.ConfirmedAtUtc == intent.UsedAtUtc
                && confirmed.Source.SourceSystem == "magic-link"
                && confirmed.Source.ExternalRequestId == intent.CapabilityId.Value
            : Deserialize<TimeEntryAdjustedThroughMagicLink>(page.Events[1]) is { } adjusted
                && adjusted.TimeEntryId == intent.TimeEntryId
                && adjusted.Tenant == intent.Tenant
                && adjusted.Contributor == used.Contributor
                && adjusted.AdjustedAtUtc == intent.UsedAtUtc
                && adjusted.Source.SourceSystem == "magic-link"
                && adjusted.Source.ExternalRequestId == intent.CapabilityId.Value
                && adjusted.ActivityTypeScope == ActivityTypeScope.Tenant
                && adjusted.PreviousValues is { } previous
                && adjusted.AdjustedValues is { } next
                && previous.Target == next.Target
                && (capability is null || next.Target == capability.Target)
                && previous.Contributor == next.Contributor
                && next.Contributor == used.Contributor
                && previous.ActivityTypeId == (capability?.ActivityTypeId ?? previous.ActivityTypeId)
                && previous.ActivityTypeScope == adjusted.ActivityTypeScope
                && next.ActivityTypeScope == adjusted.ActivityTypeScope
                && previous.ContributorCategory == ContributorCategory.ExternalContributor
                && next.ContributorCategory == previous.ContributorCategory
                && Equals(previous.AiMetrics, next.AiMetrics)
                && adjusted.AdjustedValues.ServiceDate == intent.Adjustment!.ServiceDate
                && adjusted.AdjustedValues.DurationMinutes == intent.Adjustment.DurationMinutes
                && adjusted.AdjustedValues.ActivityTypeId == intent.Adjustment.ActivityTypeId
                && adjusted.AdjustedValues.BillableState == intent.Adjustment.BillableState
                && (intent.Adjustment.Comment is null || capability is not null)
                && CommentsMatchExpected(adjusted.AdjustedValues.Comment,
                    expectedComment);
    }

    internal static bool CommentsMatchExpected(TimeEntryComment? stored, TimeEntryComment? expected)
    {
        if (stored is null || expected is null) return stored is null && expected is null;
        if (stored.Policy is null || expected.Policy is null) return false;
        if (stored.Policy.ServerPublicationVersion != TimeEntryCommentPublicationIssuer.CurrentVersion
            || stored.Policy.ServerProjectionInclusion is not (TimesheetsCommentPolicyDecision.Allowed
                or TimesheetsCommentPolicyDecision.PolicyRequired)
            || stored.Policy.ProjectionInclusion != stored.Policy.ServerProjectionInclusion
            || stored.Policy.ServerExternalConfirmationDisplay is not (TimesheetsCommentPolicyDecision.Allowed
                or TimesheetsCommentPolicyDecision.Excluded)
            || stored.Policy.ExternalConfirmationDisplay != stored.Policy.ServerExternalConfirmationDisplay)
            return false;
        return stored == expected;
    }

    private static bool HasStoredEventIdentity(StreamReadEvent stored, string commandMessageId)
        => !string.IsNullOrWhiteSpace(stored.MessageId)
            && StringComparer.Ordinal.Equals(stored.CorrelationId, commandMessageId);

    private static bool IsStaleReadback(StreamReadPage page, long lastSequence, int eventCount)
        => page.Events.Count < eventCount
            || page.Events[^1].SequenceNumber < lastSequence;

    internal async Task<CommandStatusQueryResponse?> SubmitAndReadStatusAsync(
        string messageId,
        string tenant,
        string aggregateId,
        object intent,
        Type commandType,
        CancellationToken cancellationToken)
        => (await SubmitAndReadStatusWithPreflightAsync(
            messageId, tenant, aggregateId, intent, commandType, cancellationToken)
            .ConfigureAwait(false)).Status;

    internal async Task<(CommandStatusQueryResponse? Status, bool WasPresentAtPreflight)> SubmitAndReadStatusWithPreflightAsync(
        string messageId,
        string tenant,
        string aggregateId,
        object intent,
        Type commandType,
        CancellationToken cancellationToken)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_polling.Timeout);
        CancellationToken deadlineToken = deadline.Token;
        CommandStatusQueryResponse? status = await gateway.GetWorkloadCommandStatusAsync(
            tenant, messageId, deadlineToken)
            .WaitAsync(deadlineToken).ConfigureAwait(false);
        bool wasPresentAtPreflight = status is not null;
        if (status is null)
        {
            TimeSpan submitRemaining = _polling.Timeout - elapsed.Elapsed;
            if (submitRemaining <= TimeSpan.Zero)
            {
                deadline.Cancel();
                return (null, false);
            }

            try
            {
                SubmitCommandResponse accepted = await gateway.SubmitWorkloadCommandAsync(
                    new SubmitCommandRequest(
                        messageId, tenant, TimesheetsEventStoreIntegration.DomainName, aggregateId,
                        commandType.FullName!, JsonSerializer.SerializeToElement(intent, commandType, _jsonOptions),
                        CorrelationId: messageId),
                    deadlineToken).WaitAsync(deadlineToken).ConfigureAwait(false);
                if (accepted.MessageId != messageId)
                {
                    return (null, false);
                }
            }
            catch (Exception) when (!deadlineToken.IsCancellationRequested)
            {
                // A failed transport response cannot settle whether EventStore committed.
                // Observe the same command within the original deadline, without resubmitting;
                // callers still require Completed status and exact stored-event evidence.
            }
        }

        while (true)
        {
            TimeSpan remaining = _polling.Timeout - elapsed.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                deadline.Cancel();
                return (null, wasPresentAtPreflight);
            }

            try
            {
                status ??= await gateway.GetWorkloadCommandStatusAsync(tenant, messageId, deadlineToken)
                    .WaitAsync(deadlineToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsRetryableGatewayFailure(exception, cancellationToken))
            {
                // A transient observation fault does not justify another submission.
                status = null;
            }

            if (status?.Status is nameof(CommandStatus.Completed) or nameof(CommandStatus.Rejected)
                or nameof(CommandStatus.PublishFailed) or nameof(CommandStatus.TimedOut))
            {
                return (status, wasPresentAtPreflight);
            }

            remaining = _polling.Timeout - elapsed.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                deadline.Cancel();
                return (null, wasPresentAtPreflight);
            }

            status = null;
            await Task.Delay(
                remaining < _polling.PollInterval ? remaining : _polling.PollInterval,
                deadlineToken).ConfigureAwait(false);
        }
    }

    internal static bool HasCommittedStatus(
        CommandStatusQueryResponse? status,
        string messageId,
        string tenant,
        string aggregateId,
        int eventCount)
        => status?.MessageId == messageId
            && status.Status == nameof(CommandStatus.Completed)
            && status.StatusCode == (int)CommandStatus.Completed
            && status.EventCount == eventCount
            && status.CommittedEventSequence >= eventCount
            && status.TenantId == tenant
            && status.Domain == TimesheetsEventStoreIntegration.DomainName
            && status.AggregateId == aggregateId
            && status.RejectionEventType is null
            && status.FailureReason is null
            && status.Retryable is not true;

    internal static T? Deserialize<T>(StreamReadEvent streamEvent)
        where T : class
    {
        if (!string.Equals(streamEvent.SerializationFormat, "json", StringComparison.OrdinalIgnoreCase)
            || streamEvent.Payload is not { Length: > 0 })
        {
            return default;
        }

        string eventType = streamEvent.EventTypeName.Split(',', 2)[0];
        if (StringComparer.Ordinal.Equals(eventType, typeof(T).FullName))
        {
            return JsonSerializer.Deserialize<T>(streamEvent.Payload, _jsonOptions);
        }

        object? stored = eventType switch
        {
            var name when name == typeof(StoredMagicLinkIssued).FullName =>
                JsonSerializer.Deserialize<StoredMagicLinkIssued>(streamEvent.Payload, _jsonOptions)?.Event,
            var name when name == typeof(StoredMagicLinkUsed).FullName =>
                JsonSerializer.Deserialize<StoredMagicLinkUsed>(streamEvent.Payload, _jsonOptions)?.Event,
            var name when name == typeof(StoredMagicLinkRevoked).FullName =>
                JsonSerializer.Deserialize<StoredMagicLinkRevoked>(streamEvent.Payload, _jsonOptions)?.Event,
            var name when name == typeof(StoredMagicLinkExpired).FullName =>
                JsonSerializer.Deserialize<StoredMagicLinkExpired>(streamEvent.Payload, _jsonOptions)?.Event,
            var name when name == typeof(StoredTimeEntryConfirmed).FullName =>
                JsonSerializer.Deserialize<StoredTimeEntryConfirmed>(streamEvent.Payload, _jsonOptions)?.Event,
            var name when name == typeof(StoredTimeEntryAdjusted).FullName =>
                JsonSerializer.Deserialize<StoredTimeEntryAdjusted>(streamEvent.Payload, _jsonOptions)?.Event,
            var name when name == typeof(StoredTimeEntryRecorded).FullName =>
                JsonSerializer.Deserialize<StoredTimeEntryRecorded>(streamEvent.Payload, _jsonOptions)?.Event,
            var name when name == typeof(StoredTimeEntrySubmitted).FullName =>
                JsonSerializer.Deserialize<StoredTimeEntrySubmitted>(streamEvent.Payload, _jsonOptions)?.Event,
            _ => null
        };
        return stored as T;
    }

    private static string CreateMessageId(CommitMagicLinkUse intent, string requestId)
    {
        // One host request keeps its command identity through gateway submission and status retries.
        // Independent requests have different identities, so the Time Entry actor rejects the loser.
        // No raw token or token hash participates in the identifier.
        return CreateMessageId(
            $"{intent.Tenant.TenantId}\n{intent.CapabilityId.Value}\n{intent.TimeEntryId.Value}\n{intent.Action}\n{requestId}\n"
            + JsonSerializer.Serialize(intent.Adjustment, _jsonOptions));
    }

    internal static string CreateMessageId(string stableInput)
    {
        byte[] input = Encoding.UTF8.GetBytes(stableInput);
        byte[] digest = SHA256.HashData(input);
        BigInteger number = new(digest.AsSpan(0, 16), isUnsigned: true, isBigEndian: true);
        char[] encoded = new char[26];
        for (int index = encoded.Length - 1; index >= 0; index--)
        {
            encoded[index] = CrockfordAlphabet[(int)(number & 31)];
            number >>= 5;
        }

        return new string(encoded);
    }
}
