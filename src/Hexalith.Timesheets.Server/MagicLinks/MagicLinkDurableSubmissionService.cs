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
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks.Commands;
using Hexalith.Timesheets.Server.Runtime;

namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>Submits one Time Entry command and verifies its terminal status and event batch.</summary>
public sealed class MagicLinkDurableSubmissionService(IEventStoreGatewayClient gateway)
{
    private const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

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
            CommandStatusQueryResponse? status = await SubmitAndReadStatusAsync(
                messageId, intent.Tenant.TenantId, intent.Command.CapabilityId.Value, intent,
                typeof(CommitMagicLinkIssue), cancellationToken).ConfigureAwait(false);
            if (!HasCommittedStatus(status, messageId, intent.Tenant.TenantId, intent.Command.CapabilityId.Value, 1))
            {
                return false;
            }

            StreamReadPage page = await gateway.ReadWorkloadStreamAsync(
                new StreamReadRequest(
                    intent.Tenant.TenantId, TimesheetsEventStoreIntegration.DomainName,
                    intent.Command.CapabilityId.Value,
                    FromSequence: status!.CommittedEventSequence!.Value - 1,
                    PageSize: 1),
                cancellationToken).ConfigureAwait(false);
            MagicLinkConfirmationCapabilityIssued? issued = page.Events.Count == 1
                && page.Events[0].SequenceNumber == status.CommittedEventSequence
                    ? Deserialize<MagicLinkConfirmationCapabilityIssued>(page.Events[0])
                    : null;
            return page.Tenant == intent.Tenant.TenantId
                && page.Domain == TimesheetsEventStoreIntegration.DomainName
                && page.AggregateId == intent.Command.CapabilityId.Value
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
                || page.Events[0].SequenceNumber != status.CommittedEventSequence)
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
            CommandStatusQueryResponse? status = await SubmitAndReadStatusAsync(
                messageId, intent.Tenant.TenantId, intent.TimeEntryId.Value, intent,
                typeof(CommitMagicLinkUse), cancellationToken).ConfigureAwait(false);
            return status is not null
                && await VerifyCommittedBatchAsync(intent, messageId, status, capability, cancellationToken)
                    .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Unknown commit outcomes remain opaque. A retry retains the same command identity.
        }

        return false;
    }

    private async Task<bool> VerifyCommittedBatchAsync(
        CommitMagicLinkUse intent,
        string messageId,
        CommandStatusQueryResponse status,
        MagicLinkCapabilityState? capability,
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
        if (page.Tenant != intent.Tenant.TenantId
            || page.Domain != TimesheetsEventStoreIntegration.DomainName
            || page.AggregateId != intent.TimeEntryId.Value
            || page.Events.Count != 2
            || page.Events[0].SequenceNumber != lastSequence - 1
            || page.Events[1].SequenceNumber != lastSequence)
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
                && adjusted.AdjustedValues.Comment == intent.Adjustment.Comment;
    }

    private async Task<CommandStatusQueryResponse?> SubmitAndReadStatusAsync(
        string messageId,
        string tenant,
        string aggregateId,
        object intent,
        Type commandType,
        CancellationToken cancellationToken)
    {
        CommandStatusQueryResponse? status = await gateway.GetWorkloadCommandStatusAsync(
            tenant, messageId, cancellationToken).ConfigureAwait(false);
        if (status is null)
        {
            SubmitCommandResponse accepted = await gateway.SubmitWorkloadCommandAsync(
                new SubmitCommandRequest(
                    messageId, tenant, TimesheetsEventStoreIntegration.DomainName, aggregateId,
                    commandType.FullName!, JsonSerializer.SerializeToElement(intent, commandType, _jsonOptions),
                    CorrelationId: messageId, IdempotencyKey: messageId),
                cancellationToken).ConfigureAwait(false);
            if (accepted.MessageId != messageId)
            {
                return null;
            }
        }

        for (int attempt = 0; attempt < 5; attempt++)
        {
            status ??= await gateway.GetWorkloadCommandStatusAsync(tenant, messageId, cancellationToken).ConfigureAwait(false);
            if (status?.Status is nameof(CommandStatus.Completed) or nameof(CommandStatus.Rejected)
                or nameof(CommandStatus.PublishFailed) or nameof(CommandStatus.TimedOut))
            {
                return status;
            }

            status = null;
            if (attempt < 4)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }

        return null;
    }

    private static bool HasCommittedStatus(
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
            && status.FailureReason is null
            && status.Retryable is not true;

    private static T? Deserialize<T>(StreamReadEvent streamEvent)
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

    private static string CreateMessageId(string stableInput)
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
