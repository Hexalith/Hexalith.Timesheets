using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.OperationalReports;

public sealed record ActualTimeReportQueryResult(
    TimesheetsAuthorizationDecision Authorization,
    ActualTimeReportReadModel? Page)
{
    public bool WasDisclosed => Authorization.IsAuthorized && Page is not null;

    public static ActualTimeReportQueryResult Disclosed(ActualTimeReportReadModel page)
    {
        ArgumentNullException.ThrowIfNull(page);

        return new(TimesheetsAuthorizationDecision.Allowed(), page);
    }

    public static ActualTimeReportQueryResult NotFoundOrDenied(TimesheetsAuthorizationDecision authorization)
        => new(authorization, null);
}
