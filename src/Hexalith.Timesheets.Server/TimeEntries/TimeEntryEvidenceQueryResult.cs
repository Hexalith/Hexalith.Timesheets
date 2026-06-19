using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimeEntries;

public sealed record TimeEntryEvidenceQueryResult(
    TimesheetsAuthorizationDecision Authorization,
    TimeEntryEvidenceReadModel? Evidence,
    TimeEntryEvidenceQueryOutcome Outcome)
{
    public bool WasDisclosed => Authorization.IsAuthorized
        && Outcome == TimeEntryEvidenceQueryOutcome.Disclosed
        && Evidence is not null;

    public static TimeEntryEvidenceQueryResult Disclosed(TimeEntryEvidenceReadModel evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        return new(
            TimesheetsAuthorizationDecision.Allowed(),
            evidence,
            TimeEntryEvidenceQueryOutcome.Disclosed);
    }

    public static TimeEntryEvidenceQueryResult NotFoundOrDenied(TimesheetsAuthorizationDecision authorization)
        => new(authorization, null, TimeEntryEvidenceQueryOutcome.NotFoundOrDenied);
}
