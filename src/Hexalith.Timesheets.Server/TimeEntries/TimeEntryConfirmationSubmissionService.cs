using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Timesheets.Contracts.Commands.ExternalContributions;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.Runtime;
using Hexalith.Timesheets.Server.TimeEntries.Commands;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.Timesheets.Server.TimeEntries;

/// <summary>Submits an external contributor confirmation to its EventStore owner and verifies exact readback.</summary>
/// <param name="durableSubmission">The bounded EventStore submission and stream readback coordinator.</param>
/// <param name="accessGuard">The server-side authorization gate.</param>
/// <param name="externalContributionService">The domain validation and resource-level authorization service.</param>
/// <param name="pollingOptions">The bounded polling and readback configuration.</param>
/// <param name="logger">Optional low-cardinality outcome logger.</param>
/// <param name="clock">Optional monotonic clock for deterministic deadline verification.</param>
public sealed class TimeEntryConfirmationSubmissionService(
    MagicLinkDurableSubmissionService durableSubmission,
    ITimesheetsAccessGuard accessGuard,
    ExternalContributionCommandService externalContributionService,
    IOptions<MagicLinkSubmissionPollingOptions> pollingOptions,
    ILogger<TimeEntryConfirmationSubmissionService>? logger = null,
    TimeProvider? clock = null)
{
    private const int StreamPageSize = 500;
    private const int MaxStreamPages = 64;
    private const int MaxStreamEvents = 10_000;
    private const long MaxStreamPayloadBytes = 16 * 1024 * 1024;

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly EventStorePayloadProtectionMetadata _unprotected = EventStorePayloadProtectionMetadata.Unprotected();
    private static readonly EventStorePayloadProtectionMetadata _legacyUnprotected = EventStorePayloadProtectionMetadataCarrier.Legacy();

    private static readonly Type[] _ownerEventTypes =
    [
        typeof(TimeEntryRecorded),
        typeof(TimeEntrySubmitted),
        typeof(TimeEntryContributorConfirmed),
        typeof(TimeEntryAdjustedThroughMagicLink),
        typeof(TimeEntryApproved),
        typeof(TimeEntryRejected),
        typeof(TimeEntryCorrected),
        typeof(TimeEntryApprovedCorrected),
        typeof(MagicLinkConfirmationCapabilityUsed),
        typeof(MagicLinkConfirmationCapabilityRevoked),
        typeof(MagicLinkConfirmationCapabilityExpired),
        typeof(MagicLinkCommitRejected)
    ];

    private readonly MagicLinkDurableSubmissionService _durableSubmission =
        durableSubmission ?? throw new ArgumentNullException(nameof(durableSubmission));

    private readonly ITimesheetsAccessGuard _accessGuard =
        accessGuard ?? throw new ArgumentNullException(nameof(accessGuard));

    private readonly ExternalContributionCommandService _externalContributionService =
        externalContributionService ?? throw new ArgumentNullException(nameof(externalContributionService));

    private readonly MagicLinkSubmissionPollingOptions _polling =
        SnapshotPollingOptions(pollingOptions);

    private readonly ILogger? _logger = logger;
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>
    /// Verifies authority and recorded owner state, persists confirmation once via EventStore,
    /// and returns <see langword="true"/> only after bounded exact-event readback.
    /// </summary>
    /// <param name="context">The server-resolved tenant, actor, and correlation context.</param>
    /// <param name="command">The external confirmation command.</param>
    /// <param name="confirmedAtUtc">The UTC confirmation instant.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns><see langword="true"/> when a matching confirmation is verified on the owner stream; otherwise <see langword="false"/>.</returns>
    public async Task<bool> ConfirmAsync(
        TimesheetsRequestContext context,
        ConfirmExternalTimeEntry command,
        DateTimeOffset confirmedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Tenant is null
            || string.IsNullOrWhiteSpace(context.Tenant.TenantId)
            || context.Actor is null
            || string.IsNullOrWhiteSpace(context.Actor.PartyId)
            || command.TimeEntryId is null
            || string.IsNullOrWhiteSpace(command.TimeEntryId.Value)
            || command.Contributor is null
            || string.IsNullOrWhiteSpace(command.Contributor.PartyId)
            || command.Source is null
            || string.IsNullOrWhiteSpace(command.Source.SourceSystem)
            || string.IsNullOrWhiteSpace(command.Source.ExternalRequestId)
            || confirmedAtUtc == default
            || confirmedAtUtc.Offset != TimeSpan.Zero)
        {
            return RecordAndReturn(false, ExternalConfirmationLog.PolicyRefusal);
        }

        string messageId = CreateMessageId(context.Tenant, context.Actor, command);

        using CancellationTokenSource timeout = new(_polling.Timeout, _clock);
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try
        {
            TimesheetsAuthorizationDecision coarseDecision = await _accessGuard.AuthorizeAsync(
                new TimesheetsAuthorizationRequest(context, TimesheetsOperation.Confirmation)
                {
                    Contributor = command.Contributor
                },
                deadline.Token).AsTask().WaitAsync(deadline.Token).ConfigureAwait(false);
            deadline.Token.ThrowIfCancellationRequested();
            if (!coarseDecision.IsAuthorized)
            {
                return RecordAndReturn(false, ExternalConfirmationLog.PolicyRefusal);
            }

            (bool isMissing, TimeEntryState state, StreamReadEvent[] events) = await LoadOwnerStreamAsync(
                context.Tenant,
                command.TimeEntryId,
                deadline.Token).ConfigureAwait(false);
            if (isMissing)
            {
                return RecordAndReturn(false, ExternalConfirmationLog.PolicyRefusal);
            }

            if (state.HasAmbiguousMagicLinkTerminalHistory)
            {
                return RecordAndReturn(false, ExternalConfirmationLog.IndeterminateCommit);
            }

            TimeEntryConfirmationCommandResult decision = await _externalContributionService.ConfirmAsync(
                context,
                command,
                state,
                confirmedAtUtc,
                deadline.Token).AsTask().WaitAsync(deadline.Token).ConfigureAwait(false);
            deadline.Token.ThrowIfCancellationRequested();
            if (!decision.WasDispatched || decision.DomainResult is null || decision.DomainResult.IsRejection)
            {
                return RecordAndReturn(false, ExternalConfirmationLog.PolicyRefusal);
            }

            if (decision.DomainResult.IsNoOp)
            {
                return HasVerifiedStoredConfirmation(events, context, command, messageId)
                    ? RecordAccepted(ExternalConfirmationLog.AlreadyConfirmed, deadline.Token)
                    : RecordAndReturn(false, ExternalConfirmationLog.PolicyRefusal);
            }

            CommitTimeEntryConfirmation intent = new(command, context.Tenant, context.Actor, confirmedAtUtc);
            (CommandStatusQueryResponse? status, bool wasPresentAtPreflight) =
                await _durableSubmission.SubmitAndReadStatusWithPreflightAsync(
                    messageId,
                    context.Tenant.TenantId,
                    command.TimeEntryId.Value,
                    intent,
                    typeof(CommitTimeEntryConfirmation),
                    deadline.Token).ConfigureAwait(false);

            if (status is null
                || status.Status is nameof(CommandStatus.PublishFailed) or nameof(CommandStatus.TimedOut))
            {
                return RecordAndReturn(false, ExternalConfirmationLog.IndeterminateCommit);
            }

            if (IsRejectedStatus(status, messageId, context.Tenant.TenantId, command.TimeEntryId.Value))
            {
                return RecordAndReturn(false, ExternalConfirmationLog.PolicyRefusal);
            }

            if (IsCompletedNoOpStatus(status, messageId, context.Tenant.TenantId, command.TimeEntryId.Value))
            {
                (bool refreshedMissing, TimeEntryState refreshedState, StreamReadEvent[] refreshedEvents) =
                    await LoadOwnerStreamAsync(
                        context.Tenant,
                        command.TimeEntryId,
                        deadline.Token).ConfigureAwait(false);
                if (refreshedMissing)
                {
                    return RecordAndReturn(false, ExternalConfirmationLog.PolicyRefusal);
                }

                if (refreshedState.HasAmbiguousMagicLinkTerminalHistory)
                {
                    return RecordAndReturn(false, ExternalConfirmationLog.IndeterminateCommit);
                }

                TimeEntryConfirmationCommandResult refreshedDecision = await _externalContributionService.ConfirmAsync(
                    context,
                    command,
                    refreshedState,
                    confirmedAtUtc,
                    deadline.Token).AsTask().WaitAsync(deadline.Token).ConfigureAwait(false);
                deadline.Token.ThrowIfCancellationRequested();
                return refreshedDecision.WasDispatched
                    && refreshedDecision.DomainResult?.IsNoOp == true
                    && HasVerifiedStoredConfirmation(refreshedEvents, context, command, messageId)
                        ? RecordAccepted(ExternalConfirmationLog.AlreadyConfirmed, deadline.Token)
                        : RecordAndReturn(false, ExternalConfirmationLog.PolicyRefusal);
            }

            if (!MagicLinkDurableSubmissionService.HasCommittedStatus(
                status,
                messageId,
                context.Tenant.TenantId,
                command.TimeEntryId.Value,
                1))
            {
                return RecordAndReturn(false, ExternalConfirmationLog.IndeterminateCommit);
            }

            long committedSequence = status.CommittedEventSequence!.Value;
            if (committedSequence < 2 || committedSequence <= events.Length)
            {
                return RecordAndReturn(false, ExternalConfirmationLog.IndeterminateCommit);
            }

            StreamReadPage page = await ReadCommittedEventPageAsync(
                context.Tenant.TenantId,
                command.TimeEntryId.Value,
                committedSequence,
                deadline.Token).ConfigureAwait(false);

            bool metadataValid = page.Tenant == context.Tenant.TenantId
                && page.Domain == TimesheetsEventStoreIntegration.DomainName
                && page.AggregateId == command.TimeEntryId.Value
                && page.Metadata.FromSequence == committedSequence - 1
                && page.Metadata.ToSequence is null
                && page.Metadata.EventCount == 1
                && page.Metadata.LastSequenceReturned == committedSequence
                && page.Metadata.LatestSequence >= committedSequence
                && page.Events.Count == 1
                && page.Events[0].SequenceNumber == committedSequence;

            TimeEntryContributorConfirmed? verified = metadataValid
                ? MatchStoredConfirmation(page.Events[0], context, command, messageId)
                : null;

            if (verified is null)
            {
                return RecordAndReturn(false, ExternalConfirmationLog.IndeterminateCommit);
            }

            return RecordAccepted(
                !wasPresentAtPreflight && verified.ConfirmedAtUtc == confirmedAtUtc
                    ? ExternalConfirmationLog.Committed
                    : ExternalConfirmationLog.AlreadyConfirmed,
                deadline.Token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return RecordAndReturn(false, ExternalConfirmationLog.IndeterminateCommit);
        }
    }

    internal static string CreateMessageId(
        TenantReference tenant,
        PartyReference actor,
        ConfirmExternalTimeEntry command)
        => MagicLinkDurableSubmissionService.CreateMessageId(
            JsonSerializer.Serialize(new[]
            {
                "confirm",
                tenant.TenantId,
                actor.PartyId,
                command.TimeEntryId.Value,
                command.Contributor.PartyId,
                command.Source.SourceSystem,
                command.Source.ExternalRequestId
            }));

    private static MagicLinkSubmissionPollingOptions SnapshotPollingOptions(
        IOptions<MagicLinkSubmissionPollingOptions> pollingOptions)
    {
        ArgumentNullException.ThrowIfNull(pollingOptions);
        if (pollingOptions.Value is { } configured)
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

    private bool RecordAndReturn(bool accepted, string outcome)
    {
        ExternalConfirmationLog.RecordOutcome(_logger, outcome);
        return accepted;
    }

    private bool RecordAccepted(string outcome, CancellationToken deadlineToken)
    {
        deadlineToken.ThrowIfCancellationRequested();
        return RecordAndReturn(true, outcome);
    }

    private async Task<StreamReadPage> ReadCommittedEventPageAsync(
        string tenantId,
        string aggregateId,
        long committedSequence,
        CancellationToken deadlineToken)
    {
        while (true)
        {
            deadlineToken.ThrowIfCancellationRequested();
            try
            {
                StreamReadPage page = await _durableSubmission.ReadStoredEventsAsync(
                    new StreamReadRequest(
                        tenantId,
                        TimesheetsEventStoreIntegration.DomainName,
                        aggregateId,
                        FromSequence: committedSequence - 1,
                        PageSize: 1),
                    deadlineToken).ConfigureAwait(false);
                if (page.Events.Count >= 1 && page.Events[^1].SequenceNumber >= committedSequence)
                {
                    return page;
                }
            }
            catch (Exception exception) when (IsRetryableGatewayFailure(exception, deadlineToken))
            {
                // A completed command can become visible in the stream after its status.
            }

            await Task.Delay(_polling.PollInterval, _clock, deadlineToken).ConfigureAwait(false);
        }
    }

    private async Task<(bool IsMissing, TimeEntryState State, StreamReadEvent[] Events)> LoadOwnerStreamAsync(
        TenantReference tenant,
        TimeEntryId timeEntryId,
        CancellationToken deadlineToken)
    {
        List<StreamReadEvent> rawEvents = [];
        long fromSequence = 0;
        long latestSequence = 0;
        int pages = 0;
        int totalEvents = 0;
        long totalPayloadBytes = 0;
        bool hasMore;

        do
        {
            deadlineToken.ThrowIfCancellationRequested();
            if (++pages > MaxStreamPages)
            {
                throw new InvalidOperationException("The owner stream page allowance was exhausted.");
            }

            StreamReadPage page;
            try
            {
                page = await ReadOwnerPageAsync(
                    new StreamReadRequest(
                        tenant.TenantId,
                        TimesheetsEventStoreIntegration.DomainName,
                        timeEntryId.Value,
                        FromSequence: fromSequence,
                        PageSize: StreamPageSize),
                    deadlineToken).ConfigureAwait(false);
            }
            catch (EventStoreGatewayException exception) when (
                fromSequence == 0
                && exception.StatusCode == 404
                && string.Equals(exception.ReasonCode, StreamReplayReasonCodes.MissingStream, StringComparison.Ordinal))
            {
                return (true, new TimeEntryState(), []);
            }

            if (!string.Equals(page.Tenant, tenant.TenantId, StringComparison.Ordinal)
                || !string.Equals(page.Domain, TimesheetsEventStoreIntegration.DomainName, StringComparison.Ordinal)
                || !string.Equals(page.AggregateId, timeEntryId.Value, StringComparison.Ordinal)
                || page.Metadata.FromSequence != fromSequence
                || page.Metadata.ToSequence is not null
                || page.Events.Count > StreamPageSize
                || page.Metadata.EventCount != page.Events.Count
                || page.Events.Any(item => item.SequenceNumber <= fromSequence)
                || (page.Events.Count > 0 && page.Metadata.LastSequenceReturned is null)
                || (page.Metadata.LastSequenceReturned is { } reportedLast
                    && (page.Events.Count == 0
                        || reportedLast != page.Events.Max(static item => item.SequenceNumber)))
                || page.Metadata.LatestSequence < latestSequence
                || page.Metadata.LatestSequence < (page.Metadata.LastSequenceReturned ?? 0))
            {
                throw new InvalidOperationException("The EventStore stream response did not match the requested scope.");
            }

            foreach (StreamReadEvent item in page.Events)
            {
                totalEvents = checked(totalEvents + 1);
                totalPayloadBytes = checked(totalPayloadBytes + (item.Payload?.LongLength ?? 0));
                if (totalEvents > MaxStreamEvents || totalPayloadBytes > MaxStreamPayloadBytes)
                {
                    throw new InvalidOperationException("The owner stream event allowance was exhausted.");
                }
            }

            rawEvents.AddRange(page.Events);
            latestSequence = Math.Max(latestSequence, page.Metadata.LatestSequence);
            long? lastReturned = page.Metadata.LastSequenceReturned;
            if (lastReturned is null || lastReturned <= fromSequence)
            {
                if (page.Metadata.IsTruncated || latestSequence > fromSequence)
                {
                    throw new InvalidOperationException("The EventStore stream page did not advance its exclusive sequence cursor.");
                }

                if (fromSequence == 0)
                {
                    return (true, new TimeEntryState(), []);
                }

                break;
            }

            fromSequence = lastReturned.Value;
            hasMore = page.Metadata.IsTruncated || fromSequence < latestSequence;
        }
        while (hasMore);

        StreamReadEvent[] normalized = NormalizeEvents(rawEvents);
        TimeEntryState state = FoldOwnerState(normalized, tenant, timeEntryId);
        return (false, state, normalized);
    }

    private async Task<StreamReadPage> ReadOwnerPageAsync(StreamReadRequest request, CancellationToken deadlineToken)
    {
        while (true)
        {
            deadlineToken.ThrowIfCancellationRequested();
            try
            {
                return await _durableSubmission.ReadStoredEventsAsync(request, deadlineToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsRetryableGatewayFailure(exception, deadlineToken))
            {
                await Task.Delay(_polling.PollInterval, _clock, deadlineToken).ConfigureAwait(false);
            }
        }
    }

    private static StreamReadEvent[] NormalizeEvents(List<StreamReadEvent> events)
    {
        long expectedSequence = 1;
        HashSet<string> messageIds = new(StringComparer.Ordinal);
        List<StreamReadEvent> normalized = [];
        foreach (IGrouping<long, StreamReadEvent> group in events
            .GroupBy(static item => item.SequenceNumber)
            .OrderBy(static group => group.Key))
        {
            StreamReadEvent streamEvent = group.First();
            if (streamEvent.SequenceNumber != expectedSequence++)
            {
                throw new InvalidOperationException("The EventStore stream history is incomplete.");
            }

            if (group.Any(item => !Equivalent(item, streamEvent))
                || string.IsNullOrWhiteSpace(streamEvent.MessageId)
                || !messageIds.Add(streamEvent.MessageId))
            {
                throw new InvalidOperationException("The EventStore stream history is ambiguous.");
            }

            normalized.Add(streamEvent);
        }

        return [.. normalized];
    }

    private static TimeEntryState FoldOwnerState(
        StreamReadEvent[] events,
        TenantReference tenant,
        TimeEntryId timeEntryId)
    {
        TimeEntryState state = new();
        foreach (StreamReadEvent streamEvent in events)
        {
            object payload = DeserializeOwnerEvent(streamEvent);
            if (payload is MagicLinkCommitRejected)
            {
                continue;
            }

            if (!MatchesTimeEntryIdentity(payload, tenant, timeEntryId))
            {
                throw new InvalidOperationException("A Time Entry event does not match the requested stream identity.");
            }

            switch (payload)
            {
                case TimeEntryRecorded recorded:
                    state.Apply(recorded);
                    break;
                case TimeEntrySubmitted submitted:
                    state.Apply(submitted);
                    break;
                case TimeEntryContributorConfirmed confirmed:
                    state.Apply(confirmed);
                    break;
                case TimeEntryAdjustedThroughMagicLink adjusted:
                    if ((adjusted.PreviousValues.ActivityTypeScope is { } previousScope
                            && previousScope != state.ActivityTypeScope)
                        || (adjusted.AdjustedValues.ActivityTypeScope is { } adjustedScope
                            && adjustedScope != adjusted.ActivityTypeScope))
                    {
                        throw new InvalidOperationException(
                            "A Time Entry adjustment's explicit nested scopes do not match its authoritative scope lineage.");
                    }

                    state.Apply(adjusted);
                    break;
                case TimeEntryApproved approved:
                    state.Apply(approved);
                    break;
                case TimeEntryRejected rejected:
                    state.Apply(rejected);
                    break;
                case TimeEntryCorrected corrected:
                    state.Apply(corrected);
                    break;
                case TimeEntryApprovedCorrected corrected:
                    state.Apply(corrected);
                    break;
                case MagicLinkConfirmationCapabilityUsed used:
                    state.Apply(used);
                    break;
                case MagicLinkConfirmationCapabilityRevoked revoked:
                    state.Apply(revoked);
                    break;
                case MagicLinkConfirmationCapabilityExpired expired:
                    state.Apply(expired);
                    break;
            }
        }

        return state;
    }

    private static object DeserializeOwnerEvent(StreamReadEvent streamEvent)
    {
        string unqualifiedEventTypeName = streamEvent.EventTypeName.Split(',', 2)[0];
        Type? eventType = unqualifiedEventTypeName switch
        {
            var name when name == typeof(StoredMagicLinkUsed).FullName || name == nameof(StoredMagicLinkUsed) =>
                typeof(StoredMagicLinkUsed),
            var name when name == typeof(StoredMagicLinkRevoked).FullName || name == nameof(StoredMagicLinkRevoked) =>
                typeof(StoredMagicLinkRevoked),
            var name when name == typeof(StoredMagicLinkExpired).FullName || name == nameof(StoredMagicLinkExpired) =>
                typeof(StoredMagicLinkExpired),
            var name when name == typeof(StoredTimeEntryConfirmed).FullName || name == nameof(StoredTimeEntryConfirmed) =>
                typeof(StoredTimeEntryConfirmed),
            var name when name == typeof(StoredTimeEntryAdjusted).FullName || name == nameof(StoredTimeEntryAdjusted) =>
                typeof(StoredTimeEntryAdjusted),
            var name when name == typeof(StoredTimeEntryRecorded).FullName || name == nameof(StoredTimeEntryRecorded) =>
                typeof(StoredTimeEntryRecorded),
            var name when name == typeof(StoredTimeEntrySubmitted).FullName || name == nameof(StoredTimeEntrySubmitted) =>
                typeof(StoredTimeEntrySubmitted),
            _ => _ownerEventTypes.FirstOrDefault(type =>
                string.Equals(unqualifiedEventTypeName, type.Name, StringComparison.Ordinal)
                || string.Equals(unqualifiedEventTypeName, type.FullName, StringComparison.Ordinal))
        };

        if (eventType is null)
        {
            throw new InvalidOperationException("The authoritative stream contains an unknown event type.");
        }

        if (!HasSupportedEventMetadata(streamEvent)
            || !string.Equals(streamEvent.SerializationFormat, "json", StringComparison.OrdinalIgnoreCase)
            || streamEvent.Payload is not { Length: > 0 })
        {
            throw new InvalidOperationException("A recognized EventStore event payload is invalid.");
        }

        using JsonDocument document = JsonDocument.Parse(streamEvent.Payload);
        object payload = JsonSerializer.Deserialize(document.RootElement, eventType, _jsonOptions)
            ?? throw new InvalidOperationException("A recognized EventStore event has a null payload.");

        return payload switch
        {
            StoredMagicLinkUsed item => item.Event ?? throw new InvalidOperationException("An EventStore payload has no domain event."),
            StoredMagicLinkRevoked item => item.Event ?? throw new InvalidOperationException("An EventStore payload has no domain event."),
            StoredMagicLinkExpired item => item.Event ?? throw new InvalidOperationException("An EventStore payload has no domain event."),
            StoredTimeEntryConfirmed item => item.Event ?? throw new InvalidOperationException("An EventStore payload has no domain event."),
            StoredTimeEntryAdjusted item => item.Event ?? throw new InvalidOperationException("An EventStore payload has no domain event."),
            StoredTimeEntryRecorded item => item.Event ?? throw new InvalidOperationException("An EventStore payload has no domain event."),
            StoredTimeEntrySubmitted item => item.Event ?? throw new InvalidOperationException("An EventStore payload has no domain event."),
            _ => payload
        };
    }

    private static bool MatchesTimeEntryIdentity(
        object payload,
        TenantReference tenant,
        TimeEntryId timeEntryId)
        => payload switch
        {
            TimeEntryRecorded item => item.TimeEntryId == timeEntryId,
            TimeEntrySubmitted item => item.Tenant == tenant && item.TimeEntryId == timeEntryId,
            TimeEntryContributorConfirmed item => item.Tenant == tenant && item.TimeEntryId == timeEntryId,
            TimeEntryAdjustedThroughMagicLink item => item.Tenant == tenant && item.TimeEntryId == timeEntryId,
            TimeEntryApproved item => item.Tenant == tenant && item.TimeEntryId == timeEntryId,
            TimeEntryRejected item => item.Tenant == tenant && item.TimeEntryId == timeEntryId,
            TimeEntryCorrected item => item.Tenant == tenant && item.TimeEntryId == timeEntryId,
            TimeEntryApprovedCorrected item => item.Tenant == tenant && item.TimeEntryId == timeEntryId,
            MagicLinkConfirmationCapabilityUsed item => item.Tenant == tenant && item.TimeEntryId == timeEntryId,
            MagicLinkConfirmationCapabilityRevoked item => item.Tenant == tenant && item.TimeEntryId == timeEntryId,
            MagicLinkConfirmationCapabilityExpired item => item.Tenant == tenant && item.TimeEntryId == timeEntryId,
            _ => false
        };

    private static bool HasVerifiedStoredConfirmation(
        StreamReadEvent[] events,
        TimesheetsRequestContext context,
        ConfirmExternalTimeEntry command,
        string messageId)
    {
        int matchingCount = 0;
        foreach (StreamReadEvent streamEvent in events)
        {
            string eventType = streamEvent.EventTypeName.Split(',', 2)[0];
            if (StringComparer.Ordinal.Equals(eventType, typeof(StoredTimeEntryConfirmed).FullName))
            {
                if (MatchStoredConfirmation(streamEvent, context, command, messageId) is null)
                {
                    return false;
                }

                matchingCount++;
            }
            else if (StringComparer.Ordinal.Equals(eventType, nameof(StoredTimeEntryConfirmed))
                || StringComparer.Ordinal.Equals(eventType, typeof(TimeEntryContributorConfirmed).FullName)
                || StringComparer.Ordinal.Equals(eventType, nameof(TimeEntryContributorConfirmed)))
            {
                return false;
            }
        }

        return matchingCount == 1;
    }

    private static TimeEntryContributorConfirmed? MatchStoredConfirmation(
        StreamReadEvent streamEvent,
        TimesheetsRequestContext context,
        ConfirmExternalTimeEntry command,
        string messageId)
    {
        string eventType = streamEvent.EventTypeName.Split(',', 2)[0];
        if (!StringComparer.Ordinal.Equals(eventType, typeof(StoredTimeEntryConfirmed).FullName)
            || !HasSupportedEventMetadata(streamEvent)
            || string.IsNullOrWhiteSpace(streamEvent.MessageId)
            || !StringComparer.Ordinal.Equals(streamEvent.CorrelationId, messageId)
            || !StringComparer.Ordinal.Equals(streamEvent.CausationId, messageId)
            || !StringComparer.Ordinal.Equals(streamEvent.UserId, context.Actor!.PartyId))
        {
            return null;
        }

        TimeEntryContributorConfirmed? confirmed =
            MagicLinkDurableSubmissionService.Deserialize<TimeEntryContributorConfirmed>(streamEvent);
        return confirmed is not null
            && confirmed.TimeEntryId == command.TimeEntryId
            && confirmed.Contributor == command.Contributor
            && confirmed.Tenant == context.Tenant
            && confirmed.Source == command.Source
            && confirmed.ConfirmedAtUtc != default
            && confirmed.ConfirmedAtUtc.Offset == TimeSpan.Zero
                ? confirmed
                : null;
    }

    private static bool IsRejectedStatus(
        CommandStatusQueryResponse status,
        string messageId,
        string tenantId,
        string aggregateId)
        => status.MessageId == messageId
            && status.IsRejected
            && status.TenantId == tenantId
            && status.Domain == TimesheetsEventStoreIntegration.DomainName
            && status.AggregateId == aggregateId;

    private static bool IsCompletedNoOpStatus(
        CommandStatusQueryResponse status,
        string messageId,
        string tenantId,
        string aggregateId)
        => status.MessageId == messageId
            && status.Status == nameof(CommandStatus.Completed)
            && status.StatusCode == (int)CommandStatus.Completed
            && status.EventCount == 0
            && status.CommittedEventSequence is null
            && status.TenantId == tenantId
            && status.Domain == TimesheetsEventStoreIntegration.DomainName
            && status.AggregateId == aggregateId
            && status.RejectionEventType is null
            && status.FailureReason is null
            && status.Retryable is not true;

    private static bool HasSupportedEventMetadata(StreamReadEvent streamEvent)
        => streamEvent.MetadataVersion == 1
            && (streamEvent.PayloadVersion is null or 1)
            && (streamEvent.ProtectionMetadata is null
                || _unprotected.Equals(streamEvent.ProtectionMetadata)
                || _legacyUnprotected.Equals(streamEvent.ProtectionMetadata));

    private static bool IsRetryableGatewayFailure(Exception exception, CancellationToken deadlineToken)
        => !deadlineToken.IsCancellationRequested
            && (exception is OperationCanceledException or TimeoutException or HttpRequestException
                || exception is EventStoreGatewayException { StatusCode: 408 or 429 or 500 or 502 or 503 or 504 });

    private static bool Equivalent(StreamReadEvent left, StreamReadEvent right)
        => left.SequenceNumber == right.SequenceNumber
            && string.Equals(left.EventTypeName, right.EventTypeName, StringComparison.Ordinal)
            && PayloadsEqual(left.Payload, right.Payload)
            && string.Equals(left.SerializationFormat, right.SerializationFormat, StringComparison.OrdinalIgnoreCase)
            && left.MetadataVersion == right.MetadataVersion
            && left.PayloadVersion == right.PayloadVersion
            && left.GlobalPosition == right.GlobalPosition
            && string.Equals(left.MessageId, right.MessageId, StringComparison.Ordinal)
            && string.Equals(left.CorrelationId, right.CorrelationId, StringComparison.Ordinal)
            && string.Equals(left.CausationId, right.CausationId, StringComparison.Ordinal)
            && left.Timestamp == right.Timestamp
            && string.Equals(left.UserId, right.UserId, StringComparison.Ordinal)
            && Equals(left.ProtectionMetadata, right.ProtectionMetadata);

    private static bool PayloadsEqual(byte[]? left, byte[]? right)
        => left is null
            ? right is null
            : right is not null && left.AsSpan().SequenceEqual(right);
}
