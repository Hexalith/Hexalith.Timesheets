using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Attributes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Timesheets.Contracts.Events.ActivityTypes;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Contracts.Models.ActivityTypes;
using Hexalith.Timesheets.Contracts;
using Hexalith.Timesheets.ReadModels.ActivityTypes;

using Microsoft.Extensions.Logging;

namespace Hexalith.Timesheets.Projections.ActivityTypes;

/// <summary>Persists and rebuilds the tenant Activity Type catalog used by magic-link validation.</summary>
/// <remarks>
/// The explicit domain attribute is what makes this handler discoverable by the SDK's domain-telemetry
/// scan: that scan only instantiates handlers exposing a parameterless constructor, which this
/// store-injected handler deliberately does not.
/// </remarks>
[EventStoreDomain(TimesheetsDomain.Name)]
public sealed class TenantActivityTypeCatalogProjectionHandler(
    IReadModelStore readModelStore,
    ILogger<TenantActivityTypeCatalogProjectionHandler>? logger = null) :
    IAsyncDomainSharedProjectionEpochHandler,
    IDeclaresProjectionReadModelSlots
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Gets the canonical shared catalog slot declaration.</summary>
    public static IReadOnlyList<ProjectionReadModelSlotDeclaration> ProjectionReadModelSlots { get; } =
    [
        new(
            TimesheetsDomain.Name,
            TenantActivityTypeCatalogProjection.ProjectionName,
            TenantActivityTypeCatalogReadModelAddress.SlotName,
            ProjectionReadModelSlotKind.Shared,
            declaresCanonicalWriter: true)
    ];

    /// <inheritdoc />
    public string Domain => TimesheetsDomain.Name;

    /// <inheritdoc />
    public string ProjectionType => TenantActivityTypeCatalogProjection.ProjectionName;

    /// <inheritdoc />
    public string RebuildStoreName => TenantActivityTypeCatalogReadModelAddress.StateStoreName;

    /// <inheritdoc />
    public bool EpochEnabled => true;

    /// <inheritdoc />
    public string OrdinaryWriterId => TenantActivityTypeCatalogEpoch.OrdinaryWriterId;

    /// <inheritdoc />
    public SharedProjectionScope CreateScope(string tenantId) => TenantActivityTypeCatalogEpoch.CreateScope(tenantId);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReadModelBatchOperation>> FoldCatchUpAsync(
        SharedProjectionDelivery delivery,
        long generation,
        SharedProjectionEpochCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(coordinator);
        ProjectionRequest request;
        try
        {
            request = JsonSerializer.Deserialize<ProjectionRequest>(delivery.CanonicalPayload, s_jsonOptions)
                ?? throw new InvalidOperationException("Catalog delivery is empty.");
            Validate(request, "epoch-catch-up");
            if (delivery.SourceStream != request.AggregateId || request.Events.Length != 1
                || request.Events[0].GlobalPosition != delivery.EnvelopePosition
                || request.Events[0].SequenceNumber != delivery.OwnerSequenceNumber)
            {
                throw new InvalidOperationException("Catalog delivery does not match its source.");
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or JsonException)
        {
            throw Park(delivery);
        }

        var tenant = new TenantReference(request.TenantId);
        string key = TenantActivityTypeCatalogReadModelAddress.StateKey(tenant);
        ReadModelEntry<ActivityTypeCatalogReadModel> selected = await coordinator
            .ReadGenerationAsync<ActivityTypeCatalogReadModel>(CreateScope(request.TenantId), generation, key, cancellationToken)
            .ConfigureAwait(false);
        // A legacy direct writer must be reconciled by an authoritative tenant rebuild. A new
        // generation must never silently hide its catalog or certify absent restriction evidence.
        if (selected.Value is null && (await readModelStore.GetAsync<ActivityTypeCatalogReadModel>(
                RebuildStoreName, key, cancellationToken).ConfigureAwait(false)).Value is not null)
        {
            throw new SharedProjectionReadUnavailableException(new InvalidOperationException(
                "The direct catalog requires an authoritative fenced rebuild."));
        }

        try
        {
            ProjectionEventDto item = ProjectionEventReader.Normalize(request.Events, delivery.OwnerSequenceNumber)[0];
            object? payload = DeserializeActivityTypeEvent(item);
            if (payload is null) return [];
            ActivityTypeCatalogReadModel? current = selected.Value;
            if (current is { Items: null } || current is { ProjectionFreshness: null }
                || current?.ProjectPolicy is { } policy && !policy.HasValidShapeForTenant(tenant))
            {
                throw new InvalidOperationException("The selected catalog is malformed.");
            }

            Dictionary<string, ActivityTypeCatalogItem> items = (current?.Items ?? [])
                .ToDictionary(static row => row.ActivityTypeId.Value, StringComparer.Ordinal);
            List<ProjectActivityTypeCatalogRestrictionSnapshot> restrictions = [.. current?.ProjectPolicy?.Restrictions ?? []];
            if (payload is ProjectActivityTypeCatalogRestrictionConfigured configured)
            {
                ValidateRestriction(configured);
                restrictions.RemoveAll(row => row.SourceAggregateId == request.AggregateId
                    && row.Project == configured.Project);
                restrictions.Add(new(request.AggregateId, configured.Project, configured.IsRestricted,
                    configured.AllowedTenantActivityTypeIds, configured.AllowedProjectActivityTypeIds));
            }
            else
            {
                if (ActivityTypeIdFor(payload).Value != request.AggregateId)
                    throw new InvalidOperationException("Activity Type delivery does not match its owner.");
                TenantActivityTypeCatalogProjection.Apply(items, payload);
                // Project-scoped types and their lifecycle never create tenant catalog evidence.
                if (items.Count == 0 && current is null) return [];
            }

            ProjectionFreshnessState freshness = current?.ProjectionFreshness.State ?? ProjectionFreshnessState.Fresh;
            var updated = new ActivityTypeCatalogReadModel(
                items.Values.OrderBy(static row => row.Label, StringComparer.Ordinal)
                    .ThenBy(static row => row.ActivityTypeId.Value, StringComparer.Ordinal).ToArray(),
                new(freshness, Math.Max(ParseCursor(current?.ProjectionFreshness.Cursor), item.GlobalPosition)
                    .ToString(CultureInfo.InvariantCulture), null, null))
            {
                ProjectPolicy = new(tenant, current?.ProjectPolicy?.IsComplete == true,
                    restrictions.OrderBy(static row => row.SourceAggregateId, StringComparer.Ordinal)
                        .ThenBy(static row => row.Project.ProjectId, StringComparer.Ordinal).ToArray())
            };
            return [ReadModelBatchOperation.Write(key, updated, ReadModelBatchConcurrency.LastWrite)];
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or NullReferenceException)
        {
            throw Park(delivery);
        }
    }

    private static SharedProjectionFoldFailureException Park(SharedProjectionDelivery delivery)
    {
        string suffix = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            delivery.SourceStream + "\0" + delivery.EnvelopePosition.ToString(CultureInfo.InvariantCulture))));
        return new(delivery.SourceStream, delivery.EnvelopePosition, 2,
            attempts => [ReadModelBatchOperation.Write("catalog:parked:" + suffix,
                new Dictionary<string, long> { ["position"] = delivery.EnvelopePosition, ["attempts"] = attempts },
                ReadModelBatchConcurrency.LastWrite)]);
    }

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
                TenantActivityTypeCatalogReadModelAddress.StateKey(tenant),
                current => Guarded(() => Merge(current, aggregate, request.AggregateId, promoteCompleteLiveHistory: true)),
                new ReadModelWriteContext(
                    TenantActivityTypeCatalogReadModelAddress.SlotName,
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
            ProjectionDeliveryLog.StateUnavailable(logger, "activity-catalog");
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
            new ProjectionFreshnessMetadata(ProjectionFreshnessState.Rebuilding, "0", null, null))
        {
            ProjectPolicy = new(new TenantReference(identity.TenantId), true, [])
        }));
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

        ActivityTypeCatalogReadModel current = FromCandidate(candidate);
        if (current.ProjectPolicy!.Tenant != new TenantReference(identity.TenantId))
            throw new ProjectionFoldException(new InvalidOperationException("The candidate belongs to another tenant."));
        ActivityTypeCatalogReadModel? aggregate = FoldAggregate(aggregateHistory);
        if (aggregate is null)
        {
            return Task.FromResult(candidate);
        }

        // Candidate validation and merging both report deterministic fold failures.
        ActivityTypeCatalogReadModel merged = Guarded(() => Merge(
            current,
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
        string key = TenantActivityTypeCatalogReadModelAddress.StateKey(tenant);
        ReadModelEntry<ActivityTypeCatalogReadModel> current = await readModelStore
            .GetAsync<ActivityTypeCatalogReadModel>(RebuildStoreName, key, cancellationToken)
            .ConfigureAwait(false);
        // Guarded because either the persisted read model or the rebuild candidate can deserialize with
        // a JSON-null ProjectionFreshness, which both cursor reads dereference. Without the mapping that
        // escapes as an unhandled NullReferenceException instead of a declared deterministic failure.
        ActivityTypeCatalogReadModel fresh = Guarded(() =>
        {
            ActivityTypeCatalogReadModel candidateModel = FromCandidate(candidate);
            if (candidateModel.ProjectPolicy!.Tenant != tenant)
                throw new InvalidOperationException("The catalog policy tenant or evidence is invalid.");
            string cursor = Math.Max(
                    ParseCursor(current.Value?.ProjectionFreshness.Cursor),
                    ParseCursor(candidateModel.ProjectionFreshness.Cursor))
                .ToString(CultureInfo.InvariantCulture);
            return candidateModel with
            {
                ProjectPolicy = candidateModel.ProjectPolicy is { } policy
                    ? policy with { IsComplete = true }
                    : throw new InvalidOperationException("The candidate has no policy materialization."),
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
                ActivityTypeId? activityTypeId = payload is ProjectActivityTypeCatalogRestrictionConfigured configured
                    ? null : ActivityTypeIdFor(payload);
                if (payload is ProjectActivityTypeCatalogRestrictionConfigured restriction)
                {
                    ValidateRestriction(restriction);
                }
                else if (activityTypeId is null
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

        if (!creationObserved && events.Any(static item => item.Payload is not ProjectActivityTypeCatalogRestrictionConfigured))
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
        return model.Items.Count == 0 && model.ProjectPolicy!.Restrictions.Count == 0 ? null : model with
        {
            ProjectPolicy = model.ProjectPolicy! with
            {
                IsComplete = false,
                Restrictions = model.ProjectPolicy!.Restrictions
                    .Select(row => row with { SourceAggregateId = request.AggregateId }).ToArray()
            }
        };
    }

    private static object? DeserializeActivityTypeEvent(ProjectionEventDto projectionEvent)
        => (object?)ProjectionEventReader.Deserialize<ActivityTypeCreated>(projectionEvent)
            ?? (object?)ProjectionEventReader.Deserialize<ActivityTypeRenamed>(projectionEvent)
            ?? (object?)ProjectionEventReader.Deserialize<ActivityTypeMetadataUpdated>(projectionEvent)
            ?? (object?)ProjectionEventReader.Deserialize<ActivityTypeDeactivated>(projectionEvent)
            ?? (object?)ProjectionEventReader.Deserialize<ActivityTypeReactivated>(projectionEvent)
            ?? ProjectionEventReader.Deserialize<ProjectActivityTypeCatalogRestrictionConfigured>(projectionEvent);

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
        if (current is { Items: null } || aggregate.Items is null
            || aggregate.ProjectPolicy is not { } aggregatePolicy
            || !aggregatePolicy.HasValidShapeForTenant(aggregatePolicy.Tenant)
            || current?.ProjectPolicy is { } persistedPolicy && !persistedPolicy.HasValidShapeForTenant(aggregatePolicy.Tenant))
        {
            throw new InvalidOperationException("The Activity Type catalog items are missing.");
        }

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
            new ProjectionFreshnessMetadata(freshness, cursor, null, null))
        {
            ProjectPolicy = new(
                aggregate.ProjectPolicy!.Tenant,
                current?.ProjectPolicy?.IsComplete == true,
                (current?.ProjectPolicy?.Restrictions ?? [])
                    .Where(row => row.SourceAggregateId != aggregateId)
                    .Concat(aggregate.ProjectPolicy.Restrictions)
                    .OrderBy(static row => row.SourceAggregateId, StringComparer.Ordinal)
                    .ThenBy(static row => row.Project.ProjectId, StringComparer.Ordinal).ToArray())
        };
    }

    private static void ValidateRestriction(ProjectActivityTypeCatalogRestrictionConfigured configured)
    {
        if (configured.Project is null || string.IsNullOrWhiteSpace(configured.Project.ProjectId)
            || configured.AllowedTenantActivityTypeIds is null || configured.AllowedProjectActivityTypeIds is null
            || configured.AllowedTenantActivityTypeIds.Concat(configured.AllowedProjectActivityTypeIds)
                .Any(static id => id is null || string.IsNullOrWhiteSpace(id.Value))
            || configured.AllowedTenantActivityTypeIds.Distinct().Count() != configured.AllowedTenantActivityTypeIds.Count
            || configured.AllowedProjectActivityTypeIds.Distinct().Count() != configured.AllowedProjectActivityTypeIds.Count)
        {
            throw new InvalidOperationException("The Project restriction event is malformed.");
        }
    }

    private static TResult Guarded<TResult>(Func<TResult> merge)
    {
        try
        {
            return merge();
        }
        catch (Exception exception)
            when (exception is InvalidOperationException or ArgumentException or NullReferenceException or JsonException)
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
        => Guarded(() =>
        {
            ActivityTypeCatalogReadModel model = JsonSerializer.Deserialize<ActivityTypeCatalogReadModel>(
                candidate.State.Span, s_jsonOptions)
                ?? throw new InvalidOperationException("The Activity Type catalog rebuild candidate is malformed.");
            if (model.ProjectPolicy is not { IsComplete: true } policy || !policy.HasValidShapeForTenant(policy.Tenant)
                || model.ProjectionFreshness is null || model.Items is null || model.Items.Any(static item => item is null
                || item.ActivityTypeId is null || string.IsNullOrWhiteSpace(item.ActivityTypeId.Value)))
            {
                throw new InvalidOperationException("The Activity Type catalog rebuild items are malformed.");
            }

            return model;
        });

    private static void Validate(ProjectionRequest request, string dispatchId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(dispatchId);
        if (!string.Equals(request.Domain, TimesheetsDomain.Name, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(request.TenantId)
            || string.IsNullOrWhiteSpace(request.AggregateId))
        {
            throw new ArgumentException("The projection request is outside the Timesheets route.", nameof(request));
        }
    }

    private static void Validate(DomainSharedProjectionRebuildIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (!string.Equals(identity.Domain, TimesheetsDomain.Name, StringComparison.Ordinal)
            || !string.Equals(identity.ProjectionType, TenantActivityTypeCatalogProjection.ProjectionName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The shared rebuild identity targets a different projection.");
        }
    }
}
