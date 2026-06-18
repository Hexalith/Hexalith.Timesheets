namespace Hexalith.Timesheets.Server.Authorization;

public sealed class DenyAllTimesheetsAuthorizationGate : ITimesheetsAuthorizationGate
{
    public ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
        TimesheetsRequestContext context,
        TimesheetsOperation operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return ValueTask.FromResult(
            TimesheetsAuthorizationDecision.Denied("Timesheets authorization is not configured."));
    }
}
