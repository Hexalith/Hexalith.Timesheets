using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Timesheets.Contracts.Events.ActivityTypes;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;
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

            return index.Value?.Entries.TryGetValue(tokenHash.Value, out MagicLinkTokenHashCapabilityIndexEntry? candidate) == true
                ? candidate
                : null;
        }
        catch (Exception ex) when (IsFailClosedReadException(ex))
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
        catch (Exception ex) when (IsFailClosedReadException(ex))
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
        catch (Exception ex) when (IsFailClosedReadException(ex))
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
            StreamReadEvent[] events = await ReadAllEventsAsync(
                tenant,
                aggregateId: null,
                cancellationToken).ConfigureAwait(false);

            ActivityTypeCatalogState state = new();
            HashSet<string> appliedMessageIds = new(StringComparer.Ordinal);

            foreach (StreamReadEvent streamEvent in OrderedDistinct(events, appliedMessageIds))
            {
                object? payload = Deserialize(
                    streamEvent,
                    typeof(ActivityTypeCreated),
                    typeof(ActivityTypeRenamed),
                    typeof(ActivityTypeMetadataUpdated),
                    typeof(ActivityTypeDeactivated),
                    typeof(ActivityTypeReactivated));

                switch (payload)
                {
                    case ActivityTypeCreated created when created.Scope == ActivityTypeScope.Tenant && created.Project is null:
                        state.Apply(created);
                        break;
                    case ActivityTypeRenamed renamed:
                        state.Apply(renamed);
                        break;
                    case ActivityTypeMetadataUpdated updated:
                        state.Apply(updated);
                        break;
                    case ActivityTypeDeactivated deactivated:
                        state.Apply(deactivated);
                        break;
                    case ActivityTypeReactivated reactivated:
                        state.Apply(reactivated);
                        break;
                }
            }

            return new ActivityTypeCatalogReadModel(
                state.Items.Values
                    .Select(static item => new ActivityTypeCatalogItem(
                        item.ActivityTypeId,
                        item.Scope,
                        item.Project,
                        item.Label,
                        item.IsActive,
                        item.DefaultBillableState))
                    .OrderBy(static item => item.Label, StringComparer.Ordinal)
                    .ThenBy(static item => item.ActivityTypeId.Value, StringComparer.Ordinal)
                    .ToArray(),
                new ProjectionFreshnessMetadata(
                    ProjectionFreshnessState.Fresh,
                    events.Length == 0
                        ? "0"
                        : events.Max(static @event => @event.SequenceNumber).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    // AsOfUtc is left null so the folded catalog is deterministic across reads (AC2):
                    // the cursor (max sequence) already conveys freshness, matching the canonical
                    // TenantActivityTypeCatalogProjection Fresh metadata. Avoids a direct system clock.
                    null,
                    null));
        }
        catch (Exception ex) when (IsFailClosedReadException(ex))
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
        ReplayContinuationToken? continuation = null;

        do
        {
            StreamReadPage page = await _eventStore
                .ReadStreamAsync(
                    new StreamReadRequest(
                        tenant.TenantId,
                        TimesheetsEventStoreIntegration.DomainName,
                        aggregateId,
                        ContinuationToken: continuation,
                        PageSize: StreamPageSize),
                    cancellationToken)
                .ConfigureAwait(false);

            events.AddRange(page.Events);
            continuation = page.Metadata.NextContinuationToken;
        }
        while (continuation is not null);

        return events.ToArray();
    }

    private static IEnumerable<StreamReadEvent> OrderedDistinct(
        IEnumerable<StreamReadEvent> events,
        HashSet<string> appliedMessageIds)
    {
        foreach (StreamReadEvent streamEvent in events.OrderBy(static @event => @event.SequenceNumber))
        {
            if (string.IsNullOrWhiteSpace(streamEvent.MessageId)
                || !appliedMessageIds.Add(streamEvent.MessageId))
            {
                continue;
            }

            yield return streamEvent;
        }
    }

    private static object? Deserialize(StreamReadEvent streamEvent, params Type[] eventTypes)
    {
        Type? eventType = eventTypes.FirstOrDefault(type =>
            string.Equals(streamEvent.EventTypeName, type.Name, StringComparison.Ordinal)
            || streamEvent.EventTypeName.EndsWith(type.Name, StringComparison.Ordinal));
        if (eventType is null)
        {
            return null;
        }

        using JsonDocument document = streamEvent.Payload is { Length: > 0 }
            ? JsonDocument.Parse(streamEvent.Payload)
            : JsonDocument.Parse("{}");

        return JsonSerializer.Deserialize(document.RootElement, eventType, JsonOptions);
    }

    private static MagicLinkEndpointTokenState UnavailableTokenState()
        => new(null, null, UnavailableCatalog());

    private static ActivityTypeCatalogReadModel UnavailableCatalog()
        => new([], ProjectionFreshnessMetadata.Unavailable());

    private static bool IsFailClosedReadException(Exception exception)
        => exception is not OperationCanceledException;
}
