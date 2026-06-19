using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.ApprovedTimeLedger;

public sealed record ApprovedTimeLedgerQueryResult(
    TimesheetsAuthorizationDecision Authorization,
    ApprovedTimeLedgerReadModel? Page)
{
    public bool WasDisclosed => Authorization.IsAuthorized && Page is not null;

    public static ApprovedTimeLedgerQueryResult Disclosed(ApprovedTimeLedgerReadModel page)
    {
        ArgumentNullException.ThrowIfNull(page);

        return new(TimesheetsAuthorizationDecision.Allowed(), page);
    }

    public static ApprovedTimeLedgerQueryResult NotFoundOrDenied(TimesheetsAuthorizationDecision authorization)
        => new(authorization, null);
}
