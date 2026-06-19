using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Events.ActivityTypes;

public sealed record ActivityTypeCreated(
    ActivityTypeId ActivityTypeId,
    ActivityTypeScope Scope,
    ProjectReference? Project,
    string Label,
    BillableState DefaultBillableState);
