using System.Globalization;
using System.Text.Json;

using Hexalith.EventStore.Client.Attributes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Timesheets.Contracts.Events.ActivityTypes;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.Runtime;

using Microsoft.Extensions.Logging;

namespace Hexalith.Timesheets.Projections.ActivityTypes;

/// <summary>Persists and rebuilds the tenant Activity Type catalog used by magic-link validation.</summary>
/// <remarks>
/// The explicit domain attribute is what makes this handler discoverable by the SDK's domain-telemetry
/// scan: that scan only instantiates handlers exposing a parameterless constructor, which this
/// store-injected handler deliberately does not.
/// </remarks>
[EventStoreDomain(TimesheetsEventStoreIntegration.DomainName)]
public sealed class TenantActivityTypeCatalogProjectionHandler(
    IReadModelStore readModelStore,
    ILogger<TenantActivityTypeCatalogProjectionHandler>? logger = null) :
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
                current => Guarded(() => Merge(current, aggregate, request.AggregateId, promoteCompleteLiveHistory: true)),
                new ReadModelWriteContext(
                    MagicLinkActivityTypeCatalogReadModelAddress.SlotName,
                    ProjectionType).WithEventDiagnostics(request.Events),
                logger,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return DomainProjectionHandlerResult.Completed();
        }
        catch (ProjectionFoldException)
        {
            // A merge defect is deterministic: reporting it as a transient store outage would have
            // the coordinator retry the same failing delivery forever. Store failures and an
            // exhausted retry budget stay Retryable below, even though they share CLR types.
            cancellationToken.ThrowIfCancellationRequested();
            return DomainProjectionHandlerResult.Failed(ProjectionDispatchReasonCodes.HandlerFailure);
        }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

        // Guarded because a persisted candidate can deserialize with a JSON-null ProjectionFreshness,
        // which Merge dereferences. Without the mapping that escapes as an unhandled
        // NullReferenceException instead of this handler's declared deterministic fold failure.
        ActivityTypeCatalogReadModel merged = Guarded(() => Merge(
            FromCandidate(candidate),
            aggregate,
            aggregateHistory.AggregateId,
            promoteCompleteLiveHistory: false));
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
        // Guarded because either the persisted read model or the rebuild candidate can deserialize with
        // a JSON-null ProjectionFreshness, which both cursor reads dereference. Without the mapping that
        // escapes as an unhandled NullReferenceException instead of a declared deterministic failure.
        ActivityTypeCatalogReadModel fresh = Guarded(() =>
        {
            ActivityTypeCatalogReadModel candidateModel = FromCandidate(candidate);
            string cursor = Math.Max(
                    ParseCursor(current.Value?.ProjectionFreshness.Cursor),
                    ParseCursor(candidateModel.ProjectionFreshness.Cursor))
                .ToString(CultureInfo.InvariantCulture);
            return candidateModel with
            {
                ProjectionFreshness = new ProjectionFreshnessMetadata(
                    ProjectionFreshnessState.Fresh,
                    cursor,
                    null,
                    null)
            };
        });

        return new DomainProjectionRebuildPlan(
            RebuildStoreName,
            [ReadModelBatchOperation.Write(key, fresh, ReadModelRebuildConcurrency.For(current))]);
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
                ActivityTypeId? activityTypeId = ActivityTypeIdFor(payload);
                if (activityTypeId is null
                    || string.IsNullOrWhiteSpace(activityTypeId.Value)
                    || !string.Equals(activityTypeId.Value, request.AggregateId, StringComparison.Ordinal))
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

        // A project-scoped Activity Type folds to no tenant-catalog item. Writing that empty model
        // would publish — and, on a first delivery, mark Fresh — a catalog this aggregate never
        // belonged to, so it contributes nothing instead.
        return model.Items.Count == 0 ? null : model;
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

        // A live delivery carries one aggregate's complete history, which establishes nothing about
        // whether the *tenant* catalog is complete. It may therefore keep an absent or already-Fresh
        // catalog Fresh, but must never raise a catalog the writer has explicitly marked behind —
        // only FinalizeAsync, which replays the whole tenant, may promote one of those to Fresh.
        ProjectionFreshnessState freshness = promoteCompleteLiveHistory
            ? current?.ProjectionFreshness.State switch
            {
                null or ProjectionFreshnessState.Fresh => ProjectionFreshnessState.Fresh,
                { } persisted => persisted
            }
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

    private static TResult Guarded<TResult>(Func<TResult> merge)
    {
        try
        {
            return merge();
        }
        catch (Exception exception)
            when (exception is InvalidOperationException or ArgumentException or NullReferenceException)
        {
            throw new ProjectionFoldException(exception);
        }
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
