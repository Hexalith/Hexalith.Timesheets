using System.Globalization;

using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Server.TimesheetPeriods;

public static class TenantLocalPeriodBoundaryCalculator
{
    public static TenantLocalPeriodBoundary Calculate(
        TimesheetPeriodRequest request,
        TenantTimesheetPeriodPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentException.ThrowIfNullOrWhiteSpace(policy.TenantTimeZoneId);

        _ = TimeZoneInfo.FindSystemTimeZoneById(policy.TenantTimeZoneId);

        return request.PeriodKind switch
        {
            TimesheetPeriodKind.Weekly => Weekly(request.LocalAnchorDate, policy),
            TimesheetPeriodKind.Monthly => Monthly(request.LocalAnchorDate, policy.TenantTimeZoneId),
            _ => throw new ArgumentOutOfRangeException(
                nameof(request),
                request.PeriodKind,
                "Timesheet Period kind must be Weekly or Monthly.")
        };
    }

    private static TenantLocalPeriodBoundary Weekly(DateOnly anchor, TenantTimesheetPeriodPolicy policy)
    {
        int delta = ((7 + (int)anchor.DayOfWeek - (int)policy.WeekStartsOn) % 7);
        DateOnly start = anchor.AddDays(-delta);
        DateOnly end = start.AddDays(6);
        string key = string.Create(
            CultureInfo.InvariantCulture,
            $"{start:yyyy-MM-dd}/{end:yyyy-MM-dd}");

        return new(
            TimesheetPeriodKind.Weekly,
            key,
            start,
            end,
            policy.TenantTimeZoneId);
    }

    private static TenantLocalPeriodBoundary Monthly(DateOnly anchor, string tenantTimeZoneId)
    {
        DateOnly start = new(anchor.Year, anchor.Month, 1);
        DateOnly end = start.AddMonths(1).AddDays(-1);
        string key = string.Create(CultureInfo.InvariantCulture, $"{anchor:yyyy-MM}");

        return new(
            TimesheetPeriodKind.Monthly,
            key,
            start,
            end,
            tenantTimeZoneId);
    }
}
