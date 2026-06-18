namespace Hexalith.Timesheets.Server.Authorization;

public sealed class DenyAllTimesheetsTenantAccessValidator : ITimesheetsTenantAccessValidator
{
    public ValueTask<TimesheetsTenantAccessResult> ValidateAsync(
        TimesheetsRequestContext context,
        TimesheetsOperation operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return ValueTask.FromResult(TimesheetsTenantAccessResult.Denied(
            TimesheetsTenantAccessState.UnconfiguredPolicy,
            "Authority cannot be resolved."));
    }
}
