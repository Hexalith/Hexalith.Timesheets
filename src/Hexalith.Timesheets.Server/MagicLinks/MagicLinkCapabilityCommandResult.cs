using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.MagicLinks;

public sealed record MagicLinkCapabilityCommandResult(
    TimesheetsAuthorizationDecision Authorization,
    TimesheetsDomainResult? DomainResult,
    MagicLinkIssueResponse? IssueResponse = null)
{
    public bool WasDispatched => Authorization.IsAuthorized && DomainResult is not null;
}
