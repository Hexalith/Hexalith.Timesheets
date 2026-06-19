using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Commands.ActivityTypes;

public sealed record UpdateActivityTypeMetadata(
    ActivityTypeId ActivityTypeId,
    BillableState DefaultBillableState);
