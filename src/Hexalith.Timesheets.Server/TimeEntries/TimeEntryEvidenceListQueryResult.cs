using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimeEntries;

public sealed record TimeEntryEvidenceListQueryResult(
    TimesheetsAuthorizationDecision Authorization,
    TimeEntryQueryReadModel? Page)
{
    public bool WasDisclosed => Authorization.IsAuthorized && Page is not null;

    public static TimeEntryEvidenceListQueryResult Disclosed(TimeEntryQueryReadModel page)
    {
        ArgumentNullException.ThrowIfNull(page);

        return new(TimesheetsAuthorizationDecision.Allowed(), page);
    }

    public static TimeEntryEvidenceListQueryResult NotFoundOrDenied(TimesheetsAuthorizationDecision authorization)
        => new(authorization, null);
}
