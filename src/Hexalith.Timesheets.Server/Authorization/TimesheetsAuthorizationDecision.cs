namespace Hexalith.Timesheets.Server.Authorization;

public sealed record TimesheetsAuthorizationDecision(bool IsAuthorized, string Reason)
{
    public static TimesheetsAuthorizationDecision Denied(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new(false, reason);
    }

    public static TimesheetsAuthorizationDecision Allowed()
    {
        return new(true, "authorized");
    }
}
