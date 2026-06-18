namespace Hexalith.Timesheets.Server.Authorization;

public interface ITimesheetsAuthorizationGate
{
    ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
        TimesheetsRequestContext context,
        TimesheetsOperation operation,
        CancellationToken cancellationToken);
}
