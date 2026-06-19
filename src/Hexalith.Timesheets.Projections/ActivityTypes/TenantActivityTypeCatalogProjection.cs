using Hexalith.Timesheets.Contracts.Events.ActivityTypes;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Projections.ActivityTypes;

public sealed class TenantActivityTypeCatalogProjection
{
    public const string ProjectionName = "tenant-activity-type-catalog";

    public ActivityTypeCatalogReadModel Project(
        string tenantId,
        IEnumerable<ActivityTypeProjectionEvent> events,
        TimesheetsProjectionCheckpoint checkpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(checkpoint);

        Dictionary<string, ActivityTypeCatalogItem> items = new(StringComparer.Ordinal);
        HashSet<string> appliedMessageIds = new(StringComparer.Ordinal);

        foreach (ActivityTypeProjectionEvent projectionEvent in events
            .OrderBy(static projectionEvent => projectionEvent.SequenceNumber))
        {
            if (string.IsNullOrWhiteSpace(projectionEvent.MessageId)
                || !appliedMessageIds.Add(projectionEvent.MessageId))
            {
                continue;
            }

            Apply(items, projectionEvent.Payload);
        }

        return new(
            items.Values
                .OrderBy(static item => item.Label, StringComparer.Ordinal)
                .ThenBy(static item => item.ActivityTypeId.Value, StringComparer.Ordinal)
                .ToArray(),
            ToFreshnessMetadata(checkpoint));
    }

    public ActivityTypeCatalogReadModel ProjectForProject(
        string tenantId,
        ProjectReference project,
        IEnumerable<ActivityTypeProjectionEvent> events,
        TimesheetsProjectionCheckpoint checkpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(checkpoint);

        Dictionary<string, ActivityTypeCatalogItem> items = new(StringComparer.Ordinal);
        Dictionary<string, ProjectActivityTypeRestrictionState> restrictions = new(StringComparer.Ordinal);
        HashSet<string> appliedMessageIds = new(StringComparer.Ordinal);

        foreach (ActivityTypeProjectionEvent projectionEvent in events
            .OrderBy(static projectionEvent => projectionEvent.SequenceNumber))
        {
            if (string.IsNullOrWhiteSpace(projectionEvent.MessageId)
                || !appliedMessageIds.Add(projectionEvent.MessageId))
            {
                continue;
            }

            ApplyProjectSelection(items, restrictions, project, projectionEvent.Payload);
        }

        ProjectActivityTypeRestrictionState restriction = restrictions.TryGetValue(
            project.ProjectId,
            out ProjectActivityTypeRestrictionState? current)
            ? current
            : ProjectActivityTypeRestrictionState.Unrestricted;

        return new(
            items.Values
                .Select(item => ApplyAvailability(item, restriction))
                .OrderBy(static item => item.Label, StringComparer.Ordinal)
                .ThenBy(static item => item.ActivityTypeId.Value, StringComparer.Ordinal)
                .ToArray(),
            ToFreshnessMetadata(checkpoint));
    }

    private static void Apply(Dictionary<string, ActivityTypeCatalogItem> items, object payload)
    {
        if (payload is ActivityTypeCreated { Scope: ActivityTypeScope.Tenant, Project: null } created)
        {
            items[created.ActivityTypeId.Value] = CreateItem(created);
            return;
        }

        ApplyLifecycleTransition(items, payload);
    }

    private static void ApplyProjectSelection(
        Dictionary<string, ActivityTypeCatalogItem> items,
        Dictionary<string, ProjectActivityTypeRestrictionState> restrictions,
        ProjectReference project,
        object payload)
    {
        switch (payload)
        {
            case ActivityTypeCreated { Scope: ActivityTypeScope.Tenant, Project: null } created:
                items[created.ActivityTypeId.Value] = CreateItem(created);
                return;
            case ActivityTypeCreated created when created.Scope == ActivityTypeScope.Project && created.Project == project:
                items[created.ActivityTypeId.Value] = CreateItem(created);
                return;
            case ProjectActivityTypeCatalogRestrictionConfigured configured:
                restrictions[configured.Project.ProjectId] = new(
                    configured.IsRestricted,
                    configured.AllowedTenantActivityTypeIds.Select(static id => id.Value).ToHashSet(StringComparer.Ordinal),
                    configured.AllowedProjectActivityTypeIds.Select(static id => id.Value).ToHashSet(StringComparer.Ordinal));
                return;
            default:
                ApplyLifecycleTransition(items, payload);
                return;
        }
    }

    private static ActivityTypeCatalogItem CreateItem(ActivityTypeCreated created)
        => new(
            created.ActivityTypeId,
            created.Scope,
            created.Project,
            created.Label,
            true,
            created.DefaultBillableState);

    private static void ApplyLifecycleTransition(Dictionary<string, ActivityTypeCatalogItem> items, object payload)
    {
        switch (payload)
        {
            case ActivityTypeRenamed renamed when items.TryGetValue(renamed.ActivityTypeId.Value, out ActivityTypeCatalogItem? current):
                items[renamed.ActivityTypeId.Value] = current with { Label = renamed.Label };
                break;
            case ActivityTypeMetadataUpdated metadata when items.TryGetValue(metadata.ActivityTypeId.Value, out ActivityTypeCatalogItem? current):
                items[metadata.ActivityTypeId.Value] = current with { DefaultBillableState = metadata.DefaultBillableState };
                break;
            case ActivityTypeDeactivated deactivated when items.TryGetValue(deactivated.ActivityTypeId.Value, out ActivityTypeCatalogItem? current):
                items[deactivated.ActivityTypeId.Value] = current with
                {
                    IsActive = false,
                    ActiveState = ActivityTypeActiveState.Inactive,
                    StatusText = "Inactive",
                    IsAvailableForCapture = false
                };
                break;
            case ActivityTypeReactivated reactivated when items.TryGetValue(reactivated.ActivityTypeId.Value, out ActivityTypeCatalogItem? current):
                items[reactivated.ActivityTypeId.Value] = current with
                {
                    IsActive = true,
                    ActiveState = ActivityTypeActiveState.Active,
                    StatusText = "Active",
                    IsAvailableForCapture = true
                };
                break;
        }
    }

    private static ActivityTypeCatalogItem ApplyAvailability(
        ActivityTypeCatalogItem item,
        ProjectActivityTypeRestrictionState restriction)
    {
        bool allowedByRestriction = !restriction.IsRestricted
            || (item.Scope == ActivityTypeScope.Tenant
                && restriction.AllowedTenantActivityTypeIds.Contains(item.ActivityTypeId.Value))
            || (item.Scope == ActivityTypeScope.Project
                && restriction.AllowedProjectActivityTypeIds.Contains(item.ActivityTypeId.Value));
        bool available = item.IsActive && allowedByRestriction;

        return item with { IsAvailableForCapture = available };
    }

    private static ProjectionFreshnessMetadata ToFreshnessMetadata(TimesheetsProjectionCheckpoint checkpoint)
        => checkpoint.Freshness switch
        {
            ProjectionFreshness.Fresh => new(
                ProjectionFreshnessState.Fresh,
                checkpoint.SequenceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                null,
                null),
            ProjectionFreshness.Rebuilding => ProjectionFreshnessMetadata.Rebuilding(),
            ProjectionFreshness.Stale => ProjectionFreshnessMetadata.Stale(
                checkpoint.SequenceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ProjectionFreshness.Unavailable => ProjectionFreshnessMetadata.Unavailable(),
            _ => new(ProjectionFreshnessState.Unknown, null, null, "Projection freshness is unknown.")
        };

    private sealed record ProjectActivityTypeRestrictionState(
        bool IsRestricted,
        IReadOnlySet<string> AllowedTenantActivityTypeIds,
        IReadOnlySet<string> AllowedProjectActivityTypeIds)
    {
        public static ProjectActivityTypeRestrictionState Unrestricted { get; } = new(
            false,
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal));
    }
}
