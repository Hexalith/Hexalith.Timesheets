using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.ApprovalAuthority;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimeEntries;

public sealed record TimeEntryApprovalCommandResult(
    TimeEntryId TimeEntryId,
    TimesheetsAuthorizationDecision Authorization,
    ApprovalAuthorityResolutionResult? AuthorityResolution,
    TimesheetsDomainResult? DomainResult,
    bool AggregateDispatched)
{
    public bool WasDispatched =>
        Authorization.IsAuthorized
        && AuthorityResolution is { IsAllowed: true }
        && AggregateDispatched;

    public bool HasAcceptedEvents => DomainResult is { IsSuccess: true };

    public bool IsNoOp => DomainResult is { IsNoOp: true };

    public bool HasAuthorityDenial =>
        Authorization.IsAuthorized
        && AuthorityResolution is { IsAllowed: false };
}
