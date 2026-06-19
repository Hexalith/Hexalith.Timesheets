using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimesheetPeriods;

public enum TimesheetPeriodSummaryQueryOutcome
{
    NotFoundOrDenied = 0,
    Disclosed = 1
}

public sealed record TimesheetPeriodSummaryQueryResult(
    TimesheetsAuthorizationDecision Authorization,
    TimesheetPeriodSummaryReadModel? Summary,
    TimesheetPeriodSummaryQueryOutcome Outcome)
{
    public bool WasDisclosed => Authorization.IsAuthorized
        && Outcome == TimesheetPeriodSummaryQueryOutcome.Disclosed
        && Summary is not null;

    public static TimesheetPeriodSummaryQueryResult Disclosed(TimesheetPeriodSummaryReadModel summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new(
            TimesheetsAuthorizationDecision.Allowed(),
            summary,
            TimesheetPeriodSummaryQueryOutcome.Disclosed);
    }

    public static TimesheetPeriodSummaryQueryResult NotFoundOrDenied(TimesheetsAuthorizationDecision authorization)
        => new(authorization, null, TimesheetPeriodSummaryQueryOutcome.NotFoundOrDenied);
}
