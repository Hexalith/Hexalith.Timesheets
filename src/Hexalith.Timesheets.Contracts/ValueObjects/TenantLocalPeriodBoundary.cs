namespace Hexalith.Timesheets.Contracts.ValueObjects;

public sealed record TenantLocalPeriodBoundary(
    TimesheetPeriodKind PeriodKind,
    string PeriodKey,
    DateOnly LocalStartDate,
    DateOnly LocalEndDate,
    string TenantTimeZoneId);
