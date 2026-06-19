using Hexalith.Timesheets.Contracts.Events.ActivityTypes;
using Hexalith.Timesheets.Contracts.Models;
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

    private static void Apply(Dictionary<string, ActivityTypeCatalogItem> items, object payload)
    {
        switch (payload)
        {
            case ActivityTypeCreated created when created.Scope == ActivityTypeScope.Tenant && created.Project is null:
                items[created.ActivityTypeId.Value] = new(
                    created.ActivityTypeId,
                    created.Scope,
                    created.Project,
                    created.Label,
                    true,
                    created.DefaultBillableState);
                break;
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
}
