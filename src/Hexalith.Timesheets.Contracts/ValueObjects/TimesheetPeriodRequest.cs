namespace Hexalith.Timesheets.Contracts.ValueObjects;

public sealed record TimesheetPeriodRequest(
    TimesheetPeriodKind PeriodKind,
    DateOnly LocalAnchorDate);
