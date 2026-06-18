namespace Hexalith.Timesheets.Server.Authorization;

public sealed record TimesheetsPolicyEvaluationResult(bool IsAllowed, TimesheetsDenialCategory DenialCategory, string Reason)
{
    public static TimesheetsPolicyEvaluationResult Allowed()
    {
        return new(true, TimesheetsDenialCategory.None, "authorized");
    }

    public static TimesheetsPolicyEvaluationResult Denied(
        TimesheetsDenialCategory denialCategory,
        string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (denialCategory == TimesheetsDenialCategory.None)
        {
            throw new ArgumentOutOfRangeException(nameof(denialCategory), denialCategory, "Denied policy evaluations require a denial category.");
        }

        return new(false, denialCategory, reason);
    }
}
