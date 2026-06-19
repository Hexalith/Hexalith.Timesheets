using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Commands.ActivityTypes;

public sealed record RenameProjectActivityType(
    ActivityTypeId ActivityTypeId,
    ProjectReference Project,
    string Label);
