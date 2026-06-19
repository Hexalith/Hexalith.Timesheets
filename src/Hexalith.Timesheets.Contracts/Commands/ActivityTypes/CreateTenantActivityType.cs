using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Commands.ActivityTypes;

public sealed record CreateTenantActivityType(
    ActivityTypeId ActivityTypeId,
    string Label,
    BillableState DefaultBillableState);
