using Hexalith.Timesheets.Contracts.Events.ActivityTypes;

namespace Hexalith.Timesheets.Server.ActivityTypes;

public sealed class ActivityTypeCatalogState
{
    private readonly Dictionary<string, ActivityTypeState> _items = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, ActivityTypeState> Items => _items;

    public bool TryGet(string activityTypeId, out ActivityTypeState? activityType)
        => _items.TryGetValue(activityTypeId, out activityType);

    public void Apply(ActivityTypeCreated @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        _items[@event.ActivityTypeId.Value] = new(
            @event.ActivityTypeId,
            @event.Scope,
            @event.Project,
            @event.Label,
            true,
            @event.DefaultBillableState);
    }

    public void Apply(ActivityTypeRenamed @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        if (_items.TryGetValue(@event.ActivityTypeId.Value, out ActivityTypeState? current))
        {
            _items[@event.ActivityTypeId.Value] = current with { Label = @event.Label };
        }
    }

    public void Apply(ActivityTypeMetadataUpdated @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        if (_items.TryGetValue(@event.ActivityTypeId.Value, out ActivityTypeState? current))
        {
            _items[@event.ActivityTypeId.Value] = current with { DefaultBillableState = @event.DefaultBillableState };
        }
    }

    public void Apply(ActivityTypeDeactivated @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        if (_items.TryGetValue(@event.ActivityTypeId.Value, out ActivityTypeState? current))
        {
            _items[@event.ActivityTypeId.Value] = current with { IsActive = false };
        }
    }

    public void Apply(ActivityTypeReactivated @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        if (_items.TryGetValue(@event.ActivityTypeId.Value, out ActivityTypeState? current))
        {
            _items[@event.ActivityTypeId.Value] = current with { IsActive = true };
        }
    }
}
