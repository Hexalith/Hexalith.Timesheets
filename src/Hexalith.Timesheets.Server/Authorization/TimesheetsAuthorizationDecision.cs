namespace Hexalith.Timesheets.Server.Authorization;

public sealed record TimesheetsAuthorizationDecision(
    bool IsAuthorized,
    TimesheetsDenialCategory DenialCategory,
    string Reason)
{
    public static TimesheetsAuthorizationDecision Denied(string reason)
    {
        return Denied(TimesheetsDenialCategory.UnconfiguredPolicy, reason);
    }

    public static TimesheetsAuthorizationDecision Denied(
        TimesheetsDenialCategory denialCategory,
        string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (denialCategory == TimesheetsDenialCategory.None)
        {
            throw new ArgumentOutOfRangeException(nameof(denialCategory), denialCategory, "Denied decisions require a denial category.");
        }

        return new(false, denialCategory, reason);
    }

    public static TimesheetsAuthorizationDecision Allowed()
    {
        return new(true, TimesheetsDenialCategory.None, "authorized");
    }
}
