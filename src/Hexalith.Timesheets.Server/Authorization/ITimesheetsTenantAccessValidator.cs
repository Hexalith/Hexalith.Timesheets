namespace Hexalith.Timesheets.Server.Authorization;

public interface ITimesheetsTenantAccessValidator
{
    ValueTask<TimesheetsTenantAccessResult> ValidateAsync(
        TimesheetsRequestContext context,
        TimesheetsOperation operation,
        CancellationToken cancellationToken);
}
