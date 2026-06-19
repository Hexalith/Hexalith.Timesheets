using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Commands.ActivityTypes;

public sealed record RenameActivityType(
    ActivityTypeId ActivityTypeId,
    string Label);
