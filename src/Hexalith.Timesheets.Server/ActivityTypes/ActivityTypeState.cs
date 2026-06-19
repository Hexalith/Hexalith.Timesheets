using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Server.ActivityTypes;

public sealed record ActivityTypeState(
    ActivityTypeId ActivityTypeId,
    ActivityTypeScope Scope,
    ProjectReference? Project,
    string Label,
    bool IsActive,
    BillableState DefaultBillableState);
