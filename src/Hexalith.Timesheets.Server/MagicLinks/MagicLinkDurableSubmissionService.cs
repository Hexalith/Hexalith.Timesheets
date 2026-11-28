using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Timesheets.Contracts;
using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
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
    TimeEntryCommentPublicationIssuer? commentIssuer = null,
    TimeProvider? clock = null)
{
    private const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly TimeEntryCommentPublicationIssuer _commentIssuer = commentIssuer
        ?? new TimeEntryCommentPublicationIssuer();
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly MagicLinkSubmissionPollingOptions _polling = SnapshotPollingOptions(pollingOptions);

    /// <summary>Creates a durable submission service with the system clock.</summary>
    public MagicLinkDurableSubmissionService(
        IEventStoreGatewayClient gateway,
        IOptions<MagicLinkSubmissionPollingOptions> pollingOptions,
        TimeEntryCommentPublicationIssuer? commentIssuer)
        : this(gateway, pollingOptions, commentIssuer, null)
    {
    }

    /// <summary>Gets the validated polling limits for consumers using the same submission coordinator.</summary>
    internal MagicLinkSubmissionPollingOptions PollingOptions => _polling;

    /// <summary>Gets the clock used to enforce submission and readback deadlines.</summary>
    internal TimeProvider Clock => _clock;

    /// <summary>Copies and validates polling limits so operation deadlines remain stable.</summary>
    internal static MagicLinkSubmissionPollingOptions SnapshotPollingOptions(IOptions<MagicLinkSubmissionPollingOptions>? options)
    {
        if (options?.Value is { } configured)
        {
            var snapshot = new MagicLinkSubmissionPollingOptions
            {
                Timeout = configured.Timeout,
                PollInterval = configured.PollInterval,
                ReadbackTimeout = configured.ReadbackTimeout
            };
            if (snapshot.IsValid())
            {
                return snapshot;
            }
        }

        throw new OptionsValidationException(
            nameof(MagicLinkSubmissionPollingOptions),
            typeof(MagicLinkSubmissionPollingOptions),
            ["Timeout and ReadbackTimeout must be positive and at most 30 seconds; PollInterval must be at least 10 ms and no greater than Timeout."]);
    }

    /// <summary>
    /// Builds an internal commit intent from the server's resolved capability state.
    /// Adjustment success requires the complete trusted prior snapshot captured before submission.
    /// </summary>
    public Task<bool> SubmitResolvedUseAsync(
        MagicLinkCapabilityState capability,
        MagicLinkUseAction action,
        AdjustTimeThroughMagicLink? adjustment,
        DateTimeOffset usedAtUtc,
        string requestId,
        CancellationToken cancellationToken)
        => SubmitResolvedUseAsync(capability, action, adjustment, usedAtUtc, requestId, cancellationToken, null);

    /// <summary>Submits resolved intent with the trusted prior snapshot needed to verify an adjustment.</summary>
    public Task<bool> SubmitResolvedUseAsync(
        MagicLinkCapabilityState capability,
        MagicLinkUseAction action,
        AdjustTimeThroughMagicLink? adjustment,
        DateTimeOffset usedAtUtc,
        string requestId,
        CancellationToken cancellationToken,
        TimeEntryCorrectionValues? trustedPreviousValues)
        => capability is { CapabilityId: { } capabilityId, Tenant: { } tenant, TimeEntryId: { } timeEntryId, TokenHash: { } hash }
            ? SubmitUseAsync(new CommitMagicLinkUse(
                capabilityId, tenant, timeEntryId, hash, action, adjustment, usedAtUtc),
                requestId, cancellationToken, capability, trustedPreviousValues)
            : Task.FromResult(false);

    /// <summary>Uses a client retry identity only after exact authoritative receipt verification.</summary>
    public Task<bool> SubmitKeyedResolvedUseAsync(
        MagicLinkCapabilityState capability, MagicLinkUseAction action,
        AdjustTimeThroughMagicLink? adjustment, DateTimeOffset usedAtUtc, string idempotencyKey,
        bool submitIfMissing, CancellationToken cancellationToken,
        TimeEntryCorrectionValues? trustedPreviousValues = null)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 256
            || idempotencyKey.Any(char.IsControl)
            || capability is not { CapabilityId: { } id, Tenant: { } tenant,
                TimeEntryId: { } entry, TokenHash: { } hash }) return Task.FromResult(false);
        var intent = new CommitMagicLinkUse(id, tenant, entry, hash, action, adjustment, usedAtUtc);
        // Hash the whole intent binding. Neither a caller key nor a raw token is persisted.
        string retryIdentity = "client-retry-v1:" + CreateMessageId(idempotencyKey + "\n" + hash.Value);
        return SubmitUseAsync(intent, retryIdentity, cancellationToken, capability,
            trustedPreviousValues, true, submitIfMissing);
    }

    /// <summary>Returns true only after an issued capability event is committed in its own stream.</summary>
    public async Task<bool> SubmitIssueAsync(CommitMagicLinkIssue intent, string requestId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        string messageId = CreateMessageId(
            $"issue\n{intent.Tenant.TenantId}\n{intent.Command.CapabilityId.Value}\n{requestId}");
        try
        {
            using CancellationTokenSource timeout = new(_polling.Timeout, _clock);
            using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
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
                        page = await ReadStoredEventsAsync(
                            new StreamReadRequest(
                                intent.Tenant.TenantId, TimesheetsEventStoreIntegration.DomainName,
                                intent.Command.CapabilityId.Value,
                                FromSequence: status!.CommittedEventSequence!.Value - 1,
                                PageSize: 1),
                            deadline.Token).ConfigureAwait(false);
                        if (!IsStaleReadback(page, status.CommittedEventSequence.Value, 1))
                        {
                            break;
                        }
                    }
                    catch (Exception exception) when (IsRetryableGatewayFailure(exception, cancellationToken))
                    {
                        // A completed command can become visible in the stream after its status.
                    }

                    await Task.Delay(_polling.PollInterval, _clock, deadline.Token).ConfigureAwait(false);
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
            using CancellationTokenSource timeout = new(_polling.Timeout, _clock);
            using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            CommandStatusQueryResponse? status = await SubmitAndReadStatusAsync(
                messageId, intent.Tenant.TenantId, intent.TimeEntryId.Value, intent,
                typeof(CommitMagicLinkTransition), deadline.Token).ConfigureAwait(false);
            if (!HasCommittedStatus(status, messageId, intent.Tenant.TenantId, intent.TimeEntryId.Value, 1))
            {
                return false;
            }

            StreamReadPage page = await ReadStoredEventsAsync(
                new StreamReadRequest(
                    intent.Tenant.TenantId, TimesheetsEventStoreIntegration.DomainName,
                    intent.TimeEntryId.Value,
                    FromSequence: status!.CommittedEventSequence!.Value - 1,
                    PageSize: 1),
                deadline.Token).ConfigureAwait(false);
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

    /// <summary>
    /// Returns true only for a verified two-event committed use.
    /// Adjustments require the complete trusted prior snapshot captured before submission.
    /// </summary>
    public Task<bool> SubmitUseAsync(CommitMagicLinkUse intent, string requestId, CancellationToken cancellationToken)
        => SubmitUseAsync(intent, requestId, cancellationToken, null, null);

    /// <summary>Submits use intent with the trusted prior snapshot needed to verify an adjustment.</summary>
    public Task<bool> SubmitUseAsync(CommitMagicLinkUse intent, string requestId, CancellationToken cancellationToken,
        TimeEntryCorrectionValues? trustedPreviousValues)
        => SubmitUseAsync(intent, requestId, cancellationToken, null, trustedPreviousValues);

    private async Task<bool> SubmitUseAsync(
        CommitMagicLinkUse intent,
        string requestId,
        CancellationToken cancellationToken,
        MagicLinkCapabilityState? capability,
        TimeEntryCorrectionValues? trustedPreviousValues,
        bool originalEvidence = false, bool submitIfMissing = true)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        cancellationToken.ThrowIfCancellationRequested();
        if (intent.Action == MagicLinkUseAction.Adjust
            && (!originalEvidence && trustedPreviousValues is null
                || intent.Adjustment?.Comment is not null && capability is null))
        {
            return false;
        }

        string messageId = CreateMessageId(intent, requestId);
        try
        {
            TimeEntryComment? expectedComment = !originalEvidence && intent.Action == MagicLinkUseAction.Adjust
                && capability is not null
                ? _commentIssuer.Issue(
                    MagicLinkExternalRequestContext.FromResolvedCapability(capability, messageId),
                    intent.Adjustment?.Comment)
                : null;
            using CancellationTokenSource timeout = new(_polling.Timeout, _clock);
            using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            try
            {
                CommandStatusQueryResponse? status = await SubmitAndObserveStatusAsync(
                    messageId, intent.Tenant.TenantId, intent.TimeEntryId.Value, intent,
                    typeof(CommitMagicLinkUse), cancellationToken, deadline.Token, submitIfMissing).ConfigureAwait(false);
                if (status is null)
                {
                    return false;
                }

                while (true)
                {
                    try
                    {
                        bool? verified = await VerifyCommittedBatchAsync(intent, messageId, status, capability,
                            trustedPreviousValues, expectedComment, deadline.Token, originalEvidence).ConfigureAwait(false);
                        if (verified is not null)
                        {
                            return verified.Value;
                        }
                    }
                    catch (Exception exception) when (IsRetryableGatewayFailure(exception, cancellationToken))
                    {
                        // Retry transient reads within the same bounded command deadline.
                    }

                    await Task.Delay(_polling.PollInterval, _clock, deadline.Token).ConfigureAwait(false);
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
        CancellationToken deadlineToken, bool submitIfMissing = true)
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

        if (status is null && !submitIfMissing) return null;

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

            await Task.Delay(_polling.PollInterval, _clock, deadlineToken).ConfigureAwait(false);
        }
    }

    private async Task<CommandStatusQueryResponse?> ProbeStatusAsync(
        string tenant, string messageId, CancellationToken deadlineToken)
    {
        using CancellationTokenSource timeout = new(
            TimeSpan.FromMilliseconds(Math.Min(2_000, _polling.Timeout.TotalMilliseconds / 3)), _clock);
        using CancellationTokenSource probe = CancellationTokenSource.CreateLinkedTokenSource(deadlineToken, timeout.Token);
        return await gateway.GetWorkloadCommandStatusAsync(tenant, messageId, probe.Token)
            .WaitAsync(probe.Token).ConfigureAwait(false);
    }

    internal Task<StreamReadPage> ReadStoredEventsAsync(StreamReadRequest request, CancellationToken cancellationToken)
        => ReadStoredEventsAsync(gateway, request, _polling.ReadbackTimeout, _clock, cancellationToken);

    /// <summary>Bounds a stream read by both the per-read timeout and the enclosing deadline.</summary>
    internal static async Task<StreamReadPage> ReadStoredEventsAsync(
        IEventStoreGatewayClient gateway, StreamReadRequest request, TimeSpan readbackTimeout,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = new(readbackTimeout, clock);
        using CancellationTokenSource readback = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        return await gateway.ReadWorkloadStreamAsync(request, readback.Token)
            .WaitAsync(readback.Token).ConfigureAwait(false);
    }

    /// <summary>Identifies recoverable transport failures while preserving caller cancellation.</summary>
    internal static bool IsRetryableGatewayFailure(
        Exception exception, CancellationToken callerCancellationToken, bool includeAllServerErrors = false)
        => !callerCancellationToken.IsCancellationRequested
            && (exception is OperationCanceledException or TimeoutException or HttpRequestException
                || exception is EventStoreGatewayException { StatusCode: 408 or 429 or 500 or 502 or 503 or 504 }
                || (includeAllServerErrors && exception is EventStoreGatewayException { StatusCode: >= 500 and <= 599 }));

    private async Task<bool?> VerifyCommittedBatchAsync(
        CommitMagicLinkUse intent,
        string messageId,
        CommandStatusQueryResponse status,
        MagicLinkCapabilityState? capability,
        TimeEntryCorrectionValues? trustedPreviousValues,
        TimeEntryComment? expectedComment,
        CancellationToken cancellationToken, bool originalEvidence = false)
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
        StreamReadPage page = await ReadStoredEventsAsync(
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
        if (originalEvidence && used is not null)
        {
            // These are the original immutable command effects, never the retry's clock or
            // the now-adjusted aggregate snapshot. The full pair still passes every check below.
            intent = intent with { UsedAtUtc = used.UsedAtUtc };
            trustedPreviousValues = intent.Action == MagicLinkUseAction.Adjust
                ? Deserialize<TimeEntryAdjustedThroughMagicLink>(page.Events[1])?.PreviousValues
                : null;
        }
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
                && trustedPreviousValues is not null
                && adjusted.PreviousValues is { } previous
                && previous == trustedPreviousValues
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
                && (originalEvidence
                    ? CommentsMatchOriginalIntent(adjusted.AdjustedValues.Comment, intent.Adjustment.Comment)
                    : CommentsMatchExpected(adjusted.AdjustedValues.Comment, expectedComment));
    }

    private static bool CommentsMatchOriginalIntent(TimeEntryComment? stored, TimeEntryComment? caller)
    {
        if (stored is null || caller is null) return stored is null && caller is null;
        if (stored.Policy is not { } issued || caller.Policy is not { } requested
            || issued.RedactionRequirement != (issued.ServerExternalConfirmationDisplay == TimesheetsCommentPolicyDecision.Allowed
                ? TimesheetsCommentRedactionRequirement.NotRequired
                : TimesheetsCommentRedactionRequirement.RequiredBeforeExternalDisclosure)) return false;
        // Only server-issued publication/disclosure decisions may differ from the original caller intent.
        return CommentsMatchExpected(stored, new TimeEntryComment(caller.Text, requested with
        {
            ProjectionInclusion = issued.ProjectionInclusion,
            ExternalConfirmationDisplay = issued.ExternalConfirmationDisplay,
            RedactionRequirement = issued.RedactionRequirement,
            ServerPublicationVersion = issued.ServerPublicationVersion,
            ServerProjectionInclusion = issued.ServerProjectionInclusion,
            ServerExternalConfirmationDisplay = issued.ServerExternalConfirmationDisplay,
            ServerExportInclusion = issued.ServerExportInclusion
        }));
    }

    /// <summary>Verifies stored issuance and compares the exact expected comment.</summary>
    internal static bool CommentsMatchExpected(TimeEntryComment? stored, TimeEntryComment? expected)
    {
        if (stored is null || expected is null) return stored is null && expected is null;
        if (stored.Policy is null || expected.Policy is null) return false;
        if (stored.Policy.ServerPublicationVersion is not (TimesheetsDomain.ExternalCommentPublicationVersion
                or TimesheetsDomain.CommentPublicationVersion)
            || stored.Policy.ServerProjectionInclusion is not (TimesheetsCommentPolicyDecision.Allowed
                or TimesheetsCommentPolicyDecision.PolicyRequired)
            || stored.Policy.ProjectionInclusion != stored.Policy.ServerProjectionInclusion
            || stored.Policy.ServerExternalConfirmationDisplay is not (TimesheetsCommentPolicyDecision.Allowed
                or TimesheetsCommentPolicyDecision.Excluded)
            || stored.Policy.ExternalConfirmationDisplay != stored.Policy.ServerExternalConfirmationDisplay
            || !stored.Policy.HasValidServerExportDecision())
            return false;
        return stored == expected;
    }

    /// <summary>Checks that the stored event names the command as both cause and correlation.</summary>
    internal static bool HasStoredEventIdentity(StreamReadEvent stored, string commandMessageId)
        => !string.IsNullOrWhiteSpace(stored.MessageId)
            && StringComparer.Ordinal.Equals(stored.CorrelationId, commandMessageId)
            && StringComparer.Ordinal.Equals(stored.CausationId, commandMessageId);

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
        long started = _clock.GetTimestamp();
        using CancellationTokenSource timeout = new(_polling.Timeout, _clock);
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var observed = await SubmitAndReadStatusWithPreflightAsync(
            messageId, tenant, aggregateId, intent, commandType,
            cancellationToken, deadline, _polling, _clock, started).ConfigureAwait(false);
        return (observed.Status, observed.WasPresentAtPreflight);
    }

    /// <summary>Submits at most once and observes status using the caller's existing operation deadline.</summary>
    internal async Task<(CommandStatusQueryResponse? Status, bool WasPresentAtPreflight, bool Recovered)> SubmitAndReadStatusWithPreflightAsync(
        string messageId,
        string tenant,
        string aggregateId,
        object intent,
        Type commandType,
        CancellationToken cancellationToken,
        CancellationTokenSource deadline,
        MagicLinkSubmissionPollingOptions polling,
        TimeProvider clock,
        long started,
        Func<CancellationToken, Task<bool>>? tryRecoverCommit = null)
    {
        CancellationToken deadlineToken = deadline.Token;
        CommandStatusQueryResponse? status;
        while (true)
        {
            try
            {
                status = await gateway.GetWorkloadCommandStatusAsync(tenant, messageId, deadlineToken)
                    .WaitAsync(deadlineToken).ConfigureAwait(false);
                break;
            }
            catch (Exception exception) when (tryRecoverCommit is not null
                && IsRetryableGatewayFailure(exception, deadlineToken, includeAllServerErrors: true))
            {
                // Establish whether the command already exists before dispatching it.
                await Task.Delay(polling.PollInterval, clock, deadlineToken).ConfigureAwait(false);
            }
        }

        bool wasPresentAtPreflight = status is not null;
        bool canRecover = false;
        if (status is null)
        {
            TimeSpan submitRemaining = polling.Timeout - clock.GetElapsedTime(started);
            if (submitRemaining <= TimeSpan.Zero)
            {
                deadline.Cancel();
                return (null, false, false);
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
                    return (null, false, false);
                }

                canRecover = tryRecoverCommit is not null && accepted.CorrelationId == messageId;
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
            TimeSpan remaining = polling.Timeout - clock.GetElapsedTime(started);
            if (remaining <= TimeSpan.Zero)
            {
                deadline.Cancel();
                return (null, wasPresentAtPreflight, false);
            }

            try
            {
                status ??= await gateway.GetWorkloadCommandStatusAsync(tenant, messageId, deadlineToken)
                    .WaitAsync(deadlineToken).ConfigureAwait(false);
                if (canRecover)
                {
                    canRecover = status is null;
                    if (canRecover && await tryRecoverCommit!(deadlineToken).ConfigureAwait(false))
                    {
                        // An observed terminal state takes precedence over the alternate proof.
                        status = await gateway.GetWorkloadCommandStatusAsync(tenant, messageId, deadlineToken)
                            .WaitAsync(deadlineToken).ConfigureAwait(false);
                        canRecover = status is null;
                        if (canRecover)
                        {
                            return (status, false, true);
                        }
                    }
                }
            }
            catch (Exception exception) when (IsRetryableGatewayFailure(exception, cancellationToken, tryRecoverCommit is not null))
            {
                // A transient observation fault does not justify another submission.
                status = null;
            }

            if (status?.Status is nameof(CommandStatus.Completed) or nameof(CommandStatus.Rejected)
                or nameof(CommandStatus.PublishFailed) or nameof(CommandStatus.TimedOut))
            {
                return (status, wasPresentAtPreflight, false);
            }

            remaining = polling.Timeout - clock.GetElapsedTime(started);
            if (remaining <= TimeSpan.Zero)
            {
                deadline.Cancel();
                return (null, wasPresentAtPreflight, false);
            }

            status = null;
            await Task.Delay(
                remaining < polling.PollInterval ? remaining : polling.PollInterval,
                clock, deadlineToken).ConfigureAwait(false);
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

    internal static T? Deserialize<T>(StreamReadEvent streamEvent, JsonSerializerOptions? options = null)
        where T : class
    {
        if (!string.Equals(streamEvent.SerializationFormat, "json", StringComparison.OrdinalIgnoreCase)
            || streamEvent.Payload is not { Length: > 0 })
        {
            return default;
        }

        options ??= _jsonOptions;
        string eventType = streamEvent.EventTypeName.Split(',', 2)[0];
        if (StringComparer.Ordinal.Equals(eventType, typeof(T).FullName))
        {
            return JsonSerializer.Deserialize<T>(streamEvent.Payload, options);
        }

        object? stored = eventType switch
        {
            var name when name == typeof(StoredMagicLinkIssued).FullName =>
                JsonSerializer.Deserialize<StoredMagicLinkIssued>(streamEvent.Payload, options)?.Event,
            var name when name == typeof(StoredMagicLinkUsed).FullName =>
                JsonSerializer.Deserialize<StoredMagicLinkUsed>(streamEvent.Payload, options)?.Event,
            var name when name == typeof(StoredMagicLinkRevoked).FullName =>
                JsonSerializer.Deserialize<StoredMagicLinkRevoked>(streamEvent.Payload, options)?.Event,
            var name when name == typeof(StoredMagicLinkExpired).FullName =>
                JsonSerializer.Deserialize<StoredMagicLinkExpired>(streamEvent.Payload, options)?.Event,
            var name when name == typeof(StoredTimeEntryConfirmed).FullName =>
                JsonSerializer.Deserialize<StoredTimeEntryConfirmed>(streamEvent.Payload, options)?.Event,
            var name when name == typeof(StoredTimeEntryAdjusted).FullName =>
                JsonSerializer.Deserialize<StoredTimeEntryAdjusted>(streamEvent.Payload, options)?.Event,
            var name when name == typeof(StoredTimeEntryRecorded).FullName =>
                JsonSerializer.Deserialize<StoredTimeEntryRecorded>(streamEvent.Payload, options)?.Event,
            var name when name == typeof(StoredTimeEntrySubmitted).FullName =>
                JsonSerializer.Deserialize<StoredTimeEntrySubmitted>(streamEvent.Payload, options)?.Event,
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
