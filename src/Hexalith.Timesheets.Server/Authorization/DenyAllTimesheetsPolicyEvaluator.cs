namespace Hexalith.Timesheets.Server.Authorization;

public sealed class DenyAllTimesheetsPolicyEvaluator : ITimesheetsPolicyEvaluator
{
    public ValueTask<TimesheetsPolicyEvaluationResult> EvaluateAsync(
        TimesheetsAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ValueTask.FromResult(TimesheetsPolicyEvaluationResult.Denied(
            TimesheetsDenialCategory.UnconfiguredPolicy,
            "Authority cannot be resolved."));
    }
}
