using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimeEntries;

public sealed record TimeEntryCommandResult(
    TimesheetsAuthorizationDecision Authorization,
    TimesheetsDomainResult? DomainResult,
    bool AggregateDispatched = false)
{
    public bool WasDispatched => Authorization.IsAuthorized && AggregateDispatched;
}
