using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Events.ActivityTypes;

public sealed record ActivityTypeMetadataUpdated(
    ActivityTypeId ActivityTypeId,
    BillableState DefaultBillableState);
