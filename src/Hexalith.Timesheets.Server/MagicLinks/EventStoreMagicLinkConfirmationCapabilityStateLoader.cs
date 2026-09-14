using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Runtime;
using Hexalith.Timesheets.Server.TimeEntries;

namespace Hexalith.Timesheets.Server.MagicLinks;

public sealed class EventStoreMagicLinkConfirmationCapabilityStateLoader(
    IEventStoreGatewayClient eventStore,
    IReadModelStore readModelStore,
    IMagicLinkTokenGenerator tokenGenerator,
    ITimesheetsTrustedContextAccessor contextAccessor) : IMagicLinkConfirmationCapabilityStateLoader
{
    private const int StreamPageSize = 500;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IEventStoreGatewayClient _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
    private readonly IReadModelStore _readModelStore = readModelStore ?? throw new ArgumentNullException(nameof(readModelStore));
    private readonly IMagicLinkTokenGenerator _tokenGenerator = tokenGenerator ?? throw new ArgumentNullException(nameof(tokenGenerator));
    private readonly ITimesheetsTrustedContextAccessor _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));

    public async ValueTask<ActivityTypeCatalogReadModel> LoadActivityTypeCatalogAsync(CancellationToken cancellationToken)
    {
        TenantReference? tenant = _contextAccessor.CurrentTenant;
        if (tenant is null)
        {
            return UnavailableCatalog();
        }

        return await LoadActivityTypeCatalogAsync(tenant, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<MagicLinkCapabilityState?> LoadCapabilityAsync(
        MagicLinkCapabilityId capabilityId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capabilityId);

        TenantReference? tenant = _contextAccessor.CurrentTenant;
        if (tenant is null)
        {
            return null;
        }

        return await LoadCapabilityAsync(tenant, capabilityId, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<MagicLinkEndpointTokenState> LoadTokenStateAsync(
        string oneTimeToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(oneTimeToken))
        {
            return UnavailableTokenState();
        }

        MagicLinkTokenHash tokenHash;
        try
        {
            tokenHash = _tokenGenerator.DeriveHash(oneTimeToken);
        }
        catch (ArgumentException)
        {
            return UnavailableTokenState();
        }

        MagicLinkTokenHashCapabilityIndexEntry? candidate = await ResolveCandidateAsync(
            tokenHash,
            cancellationToken).ConfigureAwait(false);
        if (candidate is null)
        {
            return UnavailableTokenState();
        }

        MagicLinkCapabilityState? capability = await LoadCapabilityAsync(
            candidate.Tenant,
            candidate.CapabilityId,
            cancellationToken).ConfigureAwait(false);
        if (capability is null
            || capability.Tenant != candidate.Tenant
            || capability.CapabilityId != candidate.CapabilityId
            || capability.TokenHash != tokenHash
            || capability.TimeEntryId is null)
        {
            return UnavailableTokenState();
        }

        TimeEntryState? timeEntry = await LoadTimeEntryAsync(
            candidate.Tenant,
            capability.TimeEntryId,
            cancellationToken).ConfigureAwait(false);
        if (timeEntry is null || !timeEntry.IsRecorded)
        {
            return UnavailableTokenState();
        }

        ActivityTypeCatalogReadModel catalog = await LoadActivityTypeCatalogAsync(
            candidate.Tenant,
            cancellationToken).ConfigureAwait(false);
        if (catalog.ProjectionFreshness.State != ProjectionFreshnessState.Fresh)
        {
            return UnavailableTokenState();
        }

        return new MagicLinkEndpointTokenState(capability, timeEntry, catalog);
    }

    private async Task<MagicLinkTokenHashCapabilityIndexEntry?> ResolveCandidateAsync(
        MagicLinkTokenHash tokenHash,
        CancellationToken cancellationToken)
    {
        try
        {
            ReadModelEntry<MagicLinkTokenHashCapabilityIndexReadModel> index = await _readModelStore
                .GetAsync<MagicLinkTokenHashCapabilityIndexReadModel>(
                    MagicLinkTokenHashCapabilityIndexProjection.StateStoreName,
                    MagicLinkTokenHashCapabilityIndexProjection.StateKey,
                    cancellationToken)
                .ConfigureAwait(false);

            if (index.Value?.Entries is null
                || !index.Value.Entries.TryGetValue(tokenHash.Value, out MagicLinkTokenHashCapabilityIndexEntry? candidate)
                || candidate is null
                || string.IsNullOrWhiteSpace(candidate.Tenant.TenantId)
                || string.IsNullOrWhiteSpace(candidate.CapabilityId.Value))
            {
                return null;
            }

            return candidate;
        }
        catch (Exception ex) when (IsFailClosedReadException(ex, cancellationToken))
        {
            return null;
        }
    }

    private async Task<MagicLinkCapabilityState?> LoadCapabilityAsync(
        TenantReference tenant,
        MagicLinkCapabilityId capabilityId,
        CancellationToken cancellationToken)
    {
        try
        {
            StreamReadEvent[] events = await ReadAllEventsAsync(
                tenant,
                capabilityId.Value,
                cancellationToken).ConfigureAwait(false);

            MagicLinkCapabilityState state = new();
            HashSet<string> appliedMessageIds = new(StringComparer.Ordinal);

            foreach (StreamReadEvent streamEvent in OrderedDistinct(events, appliedMessageIds))
            {
                object? payload = Deserialize(
                    streamEvent,
                    typeof(MagicLinkConfirmationCapabilityIssued),
                    typeof(MagicLinkConfirmationCapabilityRevoked),
                    typeof(MagicLinkConfirmationCapabilityExpired),
                    typeof(MagicLinkConfirmationCapabilityUsed));
                if (payload is not null && !MatchesCapabilityIdentity(payload, tenant, capabilityId))
                {
                    throw new InvalidOperationException("A capability event does not match the requested stream identity.");
                }

                switch (payload)
                {
                    case MagicLinkConfirmationCapabilityIssued issued:
                        state.Apply(issued);
                        break;
                    case MagicLinkConfirmationCapabilityRevoked revoked:
                        state.Apply(revoked);
                        break;
                    case MagicLinkConfirmationCapabilityExpired expired:
                        state.Apply(expired);
                        break;
                    case MagicLinkConfirmationCapabilityUsed used:
                        state.Apply(used);
                        break;
                }
            }

            return state.Exists ? state : null;
        }
        catch (Exception ex) when (IsFailClosedReadException(ex, cancellationToken))
        {
            return null;
        }
    }

    private async Task<TimeEntryState?> LoadTimeEntryAsync(
        TenantReference tenant,
        TimeEntryId timeEntryId,
        CancellationToken cancellationToken)
    {
        try
        {
            StreamReadEvent[] events = await ReadAllEventsAsync(
                tenant,
                timeEntryId.Value,
                cancellationToken).ConfigureAwait(false);

            TimeEntryState state = new();
            HashSet<string> appliedMessageIds = new(StringComparer.Ordinal);

            foreach (StreamReadEvent streamEvent in OrderedDistinct(events, appliedMessageIds))
            {
                object? payload = Deserialize(
                    streamEvent,
                    typeof(TimeEntryRecorded),
                    typeof(TimeEntrySubmitted),
                    typeof(TimeEntryContributorConfirmed),
                    typeof(TimeEntryAdjustedThroughMagicLink),
                    typeof(TimeEntryApproved),
                    typeof(TimeEntryRejected),
                    typeof(TimeEntryCorrected),
                    typeof(TimeEntryApprovedCorrected));
                if (payload is not null && !MatchesTimeEntryIdentity(payload, tenant, timeEntryId))
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
                }
            }

            return state.IsRecorded ? state : null;
        }
        catch (Exception ex) when (IsFailClosedReadException(ex, cancellationToken))
        {
            return null;
        }
    }

    private async Task<ActivityTypeCatalogReadModel> LoadActivityTypeCatalogAsync(
        TenantReference tenant,
        CancellationToken cancellationToken)
    {
        try
        {
            ReadModelEntry<ActivityTypeCatalogReadModel> entry = await _readModelStore
                .GetAsync<ActivityTypeCatalogReadModel>(
                    MagicLinkActivityTypeCatalogReadModelAddress.StateStoreName,
                    MagicLinkActivityTypeCatalogReadModelAddress.StateKey(tenant),
                    cancellationToken)
                .ConfigureAwait(false);

            ActivityTypeCatalogReadModel? catalog = entry.Value;
            if (catalog?.ProjectionFreshness.State != ProjectionFreshnessState.Fresh
                || catalog.Items is null
                || catalog.Items.Any(static item =>
                    item.Scope != ActivityTypeScope.Tenant
                    || item.Project is not null
                    || string.IsNullOrWhiteSpace(item.ActivityTypeId.Value)
                    || string.IsNullOrWhiteSpace(item.Label))
                || catalog.Items
                    .GroupBy(static item => item.ActivityTypeId.Value, StringComparer.Ordinal)
                    .Any(static group => group.Count() != 1))
            {
                return UnavailableCatalog();
            }

            return catalog;
        }
        catch (Exception ex) when (IsFailClosedReadException(ex, cancellationToken))
        {
            return UnavailableCatalog();
        }
    }

    private async Task<StreamReadEvent[]> ReadAllEventsAsync(
        TenantReference tenant,
        string? aggregateId,
        CancellationToken cancellationToken)
    {
        List<StreamReadEvent> events = [];
        long fromSequence = 0;
        long latestSequence = 0;
        bool hasMore;

        do
        {
            StreamReadPage page = await _eventStore
                .ReadStreamAsync(
                    new StreamReadRequest(
                        tenant.TenantId,
                        TimesheetsEventStoreIntegration.DomainName,
                        aggregateId,
                        FromSequence: fromSequence,
                        PageSize: StreamPageSize),
                    cancellationToken)
                .ConfigureAwait(false);

            if (!string.Equals(page.Tenant, tenant.TenantId, StringComparison.Ordinal)
                || !string.Equals(page.Domain, TimesheetsEventStoreIntegration.DomainName, StringComparison.Ordinal)
                || !string.Equals(page.AggregateId, aggregateId, StringComparison.Ordinal)
                || page.Metadata.FromSequence != fromSequence
                || page.Metadata.ToSequence is not null
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

            events.AddRange(page.Events);
            latestSequence = Math.Max(latestSequence, page.Metadata.LatestSequence);
            hasMore = page.Metadata.IsTruncated || fromSequence < latestSequence;
            long? lastReturned = page.Metadata.LastSequenceReturned;
            if (lastReturned is null || lastReturned <= fromSequence)
            {
                if (page.Metadata.IsTruncated || latestSequence > fromSequence)
                {
                    throw new InvalidOperationException("The EventStore stream page did not advance its exclusive sequence cursor.");
                }

                break;
            }

            fromSequence = lastReturned.Value;
            hasMore = page.Metadata.IsTruncated || fromSequence < latestSequence;
        }
        while (hasMore);

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

            if (group.Any(item =>
                    !string.Equals(item.EventTypeName, streamEvent.EventTypeName, StringComparison.Ordinal)
                    || !string.Equals(item.SerializationFormat, streamEvent.SerializationFormat, StringComparison.OrdinalIgnoreCase)
                    || !item.Payload.AsSpan().SequenceEqual(streamEvent.Payload))
                || (!string.IsNullOrWhiteSpace(streamEvent.MessageId) && !messageIds.Add(streamEvent.MessageId)))
            {
                throw new InvalidOperationException("The EventStore stream history is ambiguous.");
            }

            normalized.Add(streamEvent);
        }

        return normalized.ToArray();
    }

    private static IEnumerable<StreamReadEvent> OrderedDistinct(
        IEnumerable<StreamReadEvent> events,
        HashSet<string> appliedMessageIds)
    {
        foreach (StreamReadEvent streamEvent in events.OrderBy(static @event => @event.SequenceNumber))
        {
            if (!string.IsNullOrWhiteSpace(streamEvent.MessageId)
                && !appliedMessageIds.Add(streamEvent.MessageId))
            {
                continue;
            }

            yield return streamEvent;
        }
    }

    private static object? Deserialize(StreamReadEvent streamEvent, params Type[] eventTypes)
    {
        string unqualifiedEventTypeName = streamEvent.EventTypeName.Split(',', 2)[0];
        Type? eventType = eventTypes.FirstOrDefault(type =>
            string.Equals(unqualifiedEventTypeName, type.Name, StringComparison.Ordinal)
            || string.Equals(unqualifiedEventTypeName, type.FullName, StringComparison.Ordinal));
        if (eventType is null)
        {
            return null;
        }

        if (!string.Equals(streamEvent.SerializationFormat, "json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A recognized EventStore event does not use JSON serialization.");
        }

        if (streamEvent.Payload is not { Length: > 0 })
        {
            throw new InvalidOperationException("A recognized EventStore event has an empty payload.");
        }

        using JsonDocument document = JsonDocument.Parse(streamEvent.Payload);

        return JsonSerializer.Deserialize(document.RootElement, eventType, JsonOptions)
            ?? throw new InvalidOperationException("A recognized EventStore event has a null payload.");
    }

    private static bool MatchesCapabilityIdentity(
        object payload,
        TenantReference tenant,
        MagicLinkCapabilityId capabilityId)
        => payload switch
        {
            MagicLinkConfirmationCapabilityIssued item => item.Tenant == tenant && item.CapabilityId == capabilityId,
            MagicLinkConfirmationCapabilityRevoked item => item.Tenant == tenant && item.CapabilityId == capabilityId,
            MagicLinkConfirmationCapabilityExpired item => item.Tenant == tenant && item.CapabilityId == capabilityId,
            MagicLinkConfirmationCapabilityUsed item => item.Tenant == tenant && item.CapabilityId == capabilityId,
            _ => false
        };

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
            _ => false
        };

    private static MagicLinkEndpointTokenState UnavailableTokenState()
        => new(null, null, UnavailableCatalog());

    private static ActivityTypeCatalogReadModel UnavailableCatalog()
        => new([], ProjectionFreshnessMetadata.Unavailable());

    private static bool IsFailClosedReadException(Exception exception, CancellationToken cancellationToken)
        => exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested;
}
