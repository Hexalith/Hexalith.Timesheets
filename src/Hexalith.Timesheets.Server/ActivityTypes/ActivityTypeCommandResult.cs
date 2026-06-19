using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.ActivityTypes;

public sealed record ActivityTypeCommandResult(
    TimesheetsAuthorizationDecision Authorization,
    TimesheetsDomainResult? DomainResult)
{
    public bool WasDispatched => Authorization.IsAuthorized && DomainResult is not null;
}
