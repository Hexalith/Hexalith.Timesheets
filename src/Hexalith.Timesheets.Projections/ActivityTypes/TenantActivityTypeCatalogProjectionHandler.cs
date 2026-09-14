using System.Globalization;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Timesheets.Contracts.Events.ActivityTypes;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.Runtime;

namespace Hexalith.Timesheets.Projections.ActivityTypes;

/// <summary>Persists and rebuilds the tenant Activity Type catalog used by magic-link validation.</summary>
public sealed class TenantActivityTypeCatalogProjectionHandler(IReadModelStore readModelStore) :
    IAsyncDomainSharedProjectionRebuildHandler,
    IDeclaresProjectionReadModelSlots
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Gets the canonical shared catalog slot declaration.</summary>
    public static IReadOnlyList<ProjectionReadModelSlotDeclaration> ProjectionReadModelSlots { get; } =
    [
        new(
            TimesheetsEventStoreIntegration.DomainName,
            TenantActivityTypeCatalogProjection.ProjectionName,
            MagicLinkActivityTypeCatalogReadModelAddress.SlotName,
            ProjectionReadModelSlotKind.Shared,
            declaresCanonicalWriter: true)
    ];

    /// <inheritdoc />
    public string Domain => TimesheetsEventStoreIntegration.DomainName;

    /// <inheritdoc />
    public string ProjectionType => TenantActivityTypeCatalogProjection.ProjectionName;

    /// <inheritdoc />
    public string RebuildStoreName => MagicLinkActivityTypeCatalogReadModelAddress.StateStoreName;

    /// <inheritdoc />
    public async Task<DomainProjectionHandlerResult> ProjectAsync(
        ProjectionRequest request,
        string dispatchId,
        CancellationToken cancellationToken)
    {
        Validate(request, dispatchId);
        cancellationToken.ThrowIfCancellationRequested();

        ActivityTypeCatalogReadModel? aggregate;
        try
        {
            aggregate = FoldAggregate(request);
        }
        catch (InvalidOperationException)
        {
            return DomainProjectionHandlerResult.Failed(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }

        if (aggregate is null)
        {
            return DomainProjectionHandlerResult.AlreadyCompleted();
        }

        TenantReference tenant = new(request.TenantId);
        try
        {
            _ = await ReadModelWritePolicy.UpdateAsync<ActivityTypeCatalogReadModel>(
                readModelStore,
                RebuildStoreName,
                MagicLinkActivityTypeCatalogReadModelAddress.StateKey(tenant),
                current => Merge(current, aggregate, request.AggregateId, promoteCompleteLiveHistory: true),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return DomainProjectionHandlerResult.Completed();
        }
        catch (InvalidOperationException)
        {
            return DomainProjectionHandlerResult.Retryable(ProjectionDispatchReasonCodes.DeliveryStateUnavailable);
        }
    }

    /// <inheritdoc />
    public Task<DomainSharedProjectionRebuildCandidate> CreateEmptyCandidateAsync(
        DomainSharedProjectionRebuildIdentity identity,
        CancellationToken cancellationToken)
    {
        Validate(identity);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ToCandidate(new ActivityTypeCatalogReadModel(
            [],
            new ProjectionFreshnessMetadata(ProjectionFreshnessState.Rebuilding, "0", null, null))));
    }

    /// <inheritdoc />
    public Task<DomainSharedProjectionRebuildCandidate> AccumulateAsync(
        DomainSharedProjectionRebuildIdentity identity,
        DomainSharedProjectionRebuildCandidate candidate,
        ProjectionRequest aggregateHistory,
        CancellationToken cancellationToken)
    {
        Validate(identity);
        ArgumentNullException.ThrowIfNull(candidate);
        Validate(aggregateHistory, identity.OperationId);
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(identity.TenantId, aggregateHistory.TenantId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The aggregate history is outside the rebuild tenant.");
        }

        ActivityTypeCatalogReadModel? aggregate = FoldAggregate(aggregateHistory);
        if (aggregate is null)
        {
            return Task.FromResult(candidate);
        }

        ActivityTypeCatalogReadModel merged = Merge(
            FromCandidate(candidate),
            aggregate,
            aggregateHistory.AggregateId,
            promoteCompleteLiveHistory: false);
        return Task.FromResult(ToCandidate(merged));
    }

    /// <inheritdoc />
    public async Task<DomainProjectionRebuildPlan> FinalizeAsync(
        DomainSharedProjectionRebuildIdentity identity,
        DomainSharedProjectionRebuildCandidate candidate,
        CancellationToken cancellationToken)
    {
        Validate(identity);
        ArgumentNullException.ThrowIfNull(candidate);
        cancellationToken.ThrowIfCancellationRequested();

        TenantReference tenant = new(identity.TenantId);
        string key = MagicLinkActivityTypeCatalogReadModelAddress.StateKey(tenant);
        ReadModelEntry<ActivityTypeCatalogReadModel> current = await readModelStore
            .GetAsync<ActivityTypeCatalogReadModel>(RebuildStoreName, key, cancellationToken)
            .ConfigureAwait(false);
        ActivityTypeCatalogReadModel candidateModel = FromCandidate(candidate);
        string cursor = Math.Max(
                ParseCursor(current.Value?.ProjectionFreshness.Cursor),
                ParseCursor(candidateModel.ProjectionFreshness.Cursor))
            .ToString(CultureInfo.InvariantCulture);
        ActivityTypeCatalogReadModel fresh = candidateModel with
        {
            ProjectionFreshness = new ProjectionFreshnessMetadata(
                ProjectionFreshnessState.Fresh,
                cursor,
                null,
                null)
        };
        ReadModelBatchConcurrency concurrency = current.ETag is { Length: > 0 } etag
            ? ReadModelBatchConcurrency.Match(etag)
            : ReadModelBatchConcurrency.CreateOnly;

        return new DomainProjectionRebuildPlan(
            RebuildStoreName,
            [ReadModelBatchOperation.Write(key, fresh, concurrency)]);
    }

    private static ActivityTypeCatalogReadModel? FoldAggregate(ProjectionRequest request)
    {
        List<ActivityTypeProjectionEvent> events = [];
        bool creationObserved = false;
        IReadOnlyList<ProjectionEventDto> normalizedEvents = ProjectionEventReader.Normalize(request.Events);
        foreach (ProjectionEventDto projectionEvent in normalizedEvents)
        {
            object? payload = DeserializeActivityTypeEvent(projectionEvent);
            if (payload is not null)
            {
                if (!string.Equals(ActivityTypeIdFor(payload).Value, request.AggregateId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("An Activity Type event does not match its aggregate identifier.");
                }

                creationObserved |= payload is ActivityTypeCreated;
                events.Add(new ActivityTypeProjectionEvent(
                    string.IsNullOrWhiteSpace(projectionEvent.MessageId)
                        ? $"sequence:{projectionEvent.SequenceNumber.ToString(CultureInfo.InvariantCulture)}"
                        : projectionEvent.MessageId,
                    projectionEvent.SequenceNumber,
                    payload));
            }
        }

        if (events.Count == 0)
        {
            return null;
        }

        if (!creationObserved)
        {
            throw new InvalidOperationException("The Activity Type history does not contain its creation event.");
        }

        long cursor = normalizedEvents
            .Select(static item => item.GlobalPosition > 0 ? item.GlobalPosition : item.SequenceNumber)
            .DefaultIfEmpty(0)
            .Max();
        ActivityTypeCatalogReadModel model = new TenantActivityTypeCatalogProjection().Project(
            request.TenantId,
            events,
            new TimesheetsProjectionCheckpoint(
                request.TenantId,
                TenantActivityTypeCatalogProjection.ProjectionName,
                cursor,
                ProjectionFreshness.Fresh));
        if (model.Items.Count > 1
            || model.Items.Any(item => !string.Equals(item.ActivityTypeId.Value, request.AggregateId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("The Activity Type history does not match its aggregate scope.");
        }

        return model;
    }

    private static object? DeserializeActivityTypeEvent(ProjectionEventDto projectionEvent)
        => (object?)ProjectionEventReader.Deserialize<ActivityTypeCreated>(projectionEvent)
            ?? (object?)ProjectionEventReader.Deserialize<ActivityTypeRenamed>(projectionEvent)
            ?? (object?)ProjectionEventReader.Deserialize<ActivityTypeMetadataUpdated>(projectionEvent)
            ?? (object?)ProjectionEventReader.Deserialize<ActivityTypeDeactivated>(projectionEvent)
            ?? ProjectionEventReader.Deserialize<ActivityTypeReactivated>(projectionEvent);

    private static ActivityTypeId ActivityTypeIdFor(object payload)
        => payload switch
        {
            ActivityTypeCreated created => created.ActivityTypeId,
            ActivityTypeRenamed renamed => renamed.ActivityTypeId,
            ActivityTypeMetadataUpdated updated => updated.ActivityTypeId,
            ActivityTypeDeactivated deactivated => deactivated.ActivityTypeId,
            ActivityTypeReactivated reactivated => reactivated.ActivityTypeId,
            _ => throw new InvalidOperationException("The projection payload is not an Activity Type event.")
        };

    private static ActivityTypeCatalogReadModel Merge(
        ActivityTypeCatalogReadModel? current,
        ActivityTypeCatalogReadModel aggregate,
        string aggregateId,
        bool promoteCompleteLiveHistory)
    {
        Dictionary<string, ActivityTypeCatalogItem> items = (current?.Items ?? [])
            .Where(item => !string.Equals(item.ActivityTypeId.Value, aggregateId, StringComparison.Ordinal))
            .ToDictionary(static item => item.ActivityTypeId.Value, static item => item, StringComparer.Ordinal);
        foreach (ActivityTypeCatalogItem item in aggregate.Items)
        {
            items[item.ActivityTypeId.Value] = item;
        }

        ProjectionFreshnessState freshness = promoteCompleteLiveHistory
            ? ProjectionFreshnessState.Fresh
            : ProjectionFreshnessState.Rebuilding;
        string cursor = Math.Max(
                ParseCursor(current?.ProjectionFreshness.Cursor),
                ParseCursor(aggregate.ProjectionFreshness.Cursor))
            .ToString(CultureInfo.InvariantCulture);
        return new ActivityTypeCatalogReadModel(
            items.Values
                .OrderBy(static item => item.Label, StringComparer.Ordinal)
                .ThenBy(static item => item.ActivityTypeId.Value, StringComparer.Ordinal)
                .ToArray(),
            new ProjectionFreshnessMetadata(freshness, cursor, null, null));
    }

    private static long ParseCursor(string? cursor)
        => long.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out long value) && value >= 0
            ? value
            : 0;

    private static DomainSharedProjectionRebuildCandidate ToCandidate(ActivityTypeCatalogReadModel model)
        => new(JsonSerializer.SerializeToUtf8Bytes(model, s_jsonOptions));

    private static ActivityTypeCatalogReadModel FromCandidate(DomainSharedProjectionRebuildCandidate candidate)
        => JsonSerializer.Deserialize<ActivityTypeCatalogReadModel>(candidate.State.Span, s_jsonOptions)
            ?? throw new InvalidOperationException("The Activity Type catalog rebuild candidate is malformed.");

    private static void Validate(ProjectionRequest request, string dispatchId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(dispatchId);
        if (!string.Equals(request.Domain, TimesheetsEventStoreIntegration.DomainName, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(request.TenantId)
            || string.IsNullOrWhiteSpace(request.AggregateId))
        {
            throw new ArgumentException("The projection request is outside the Timesheets route.", nameof(request));
        }
    }

    private static void Validate(DomainSharedProjectionRebuildIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (!string.Equals(identity.Domain, TimesheetsEventStoreIntegration.DomainName, StringComparison.Ordinal)
            || !string.Equals(identity.ProjectionType, TenantActivityTypeCatalogProjection.ProjectionName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The shared rebuild identity targets a different projection.");
        }
    }
}
