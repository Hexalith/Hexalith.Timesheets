namespace Hexalith.Timesheets.Server.Authorization;

public interface ITimesheetsPolicyEvaluator
{
    ValueTask<TimesheetsPolicyEvaluationResult> EvaluateAsync(
        TimesheetsAuthorizationRequest request,
        CancellationToken cancellationToken);
}
