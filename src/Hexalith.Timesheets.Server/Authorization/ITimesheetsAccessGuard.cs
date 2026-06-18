namespace Hexalith.Timesheets.Server.Authorization;

public interface ITimesheetsAccessGuard
{
    ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
        TimesheetsAuthorizationRequest request,
        CancellationToken cancellationToken);

    ValueTask<TimesheetsAuthorizationDecision> ExecuteIfAuthorizedAsync(
        TimesheetsAuthorizationRequest request,
        Func<CancellationToken, ValueTask> trustedWork,
        CancellationToken cancellationToken);

    ValueTask<TimesheetsUiActionPolicyOutcome> EvaluateUiActionAsync(
        TimesheetsAuthorizationRequest request,
        TimesheetsUiActionVisibility deniedVisibility,
        CancellationToken cancellationToken);
}
