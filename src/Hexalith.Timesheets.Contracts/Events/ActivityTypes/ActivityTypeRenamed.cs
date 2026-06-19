using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Events.ActivityTypes;

public sealed record ActivityTypeRenamed(
    ActivityTypeId ActivityTypeId,
    string Label);
