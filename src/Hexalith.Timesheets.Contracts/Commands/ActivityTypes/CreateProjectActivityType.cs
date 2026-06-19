using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Commands.ActivityTypes;

public sealed record CreateProjectActivityType(
    ActivityTypeId ActivityTypeId,
    ProjectReference Project,
    string Label,
    BillableState DefaultBillableState);
