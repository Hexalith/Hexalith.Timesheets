namespace Hexalith.Timesheets.Server.TimesheetPeriods;

public sealed record TenantTimesheetPeriodPolicy(
    string TenantTimeZoneId,
    DayOfWeek WeekStartsOn)
{
    public static TenantTimesheetPeriodPolicy DefaultUtc { get; } = new("UTC", DayOfWeek.Monday);
}
