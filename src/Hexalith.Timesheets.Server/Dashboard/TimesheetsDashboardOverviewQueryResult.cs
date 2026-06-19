using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.Dashboard;

public sealed record TimesheetsDashboardOverviewQueryResult(
    TimesheetsAuthorizationDecision Authorization,
    TimesheetsDashboardOverviewReadModel? Overview)
{
    public bool WasDisclosed => Authorization.IsAuthorized && Overview is not null;

    public static TimesheetsDashboardOverviewQueryResult Disclosed(TimesheetsDashboardOverviewReadModel overview)
    {
        ArgumentNullException.ThrowIfNull(overview);

        return new(TimesheetsAuthorizationDecision.Allowed(), overview);
    }

    public static TimesheetsDashboardOverviewQueryResult NotFoundOrDenied(TimesheetsAuthorizationDecision authorization)
        => new(authorization, null);
}
