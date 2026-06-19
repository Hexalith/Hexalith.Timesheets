using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.ApprovalAuthority;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimeEntries;

public sealed record TimeEntryCorrectionCommandResult(
    TimeEntryId TimeEntryId,
    TimesheetsAuthorizationDecision CurrentAuthorization,
    TimesheetsAuthorizationDecision? CorrectedAuthorization,
    ApprovalAuthorityResolutionResult? AuthorityResolution,
    TimesheetsDomainResult? DomainResult,
    bool AggregateDispatched)
{
    public bool WasDispatched =>
        CurrentAuthorization.IsAuthorized
        && CorrectedAuthorization is { IsAuthorized: true }
        && AuthorityResolution is { IsAllowed: true }
        && AggregateDispatched;

    public bool HasAcceptedEvents => DomainResult is { IsSuccess: true };

    public bool IsNoOp => DomainResult is { IsNoOp: true };

    public bool HasCurrentAuthorizationDenial => !CurrentAuthorization.IsAuthorized;

    public bool HasCorrectedAuthorizationDenial =>
        CurrentAuthorization.IsAuthorized
        && CorrectedAuthorization is { IsAuthorized: false };

    public bool HasCorrectionPolicyDenial =>
        CurrentAuthorization.IsAuthorized
        && CorrectedAuthorization is { IsAuthorized: true }
        && AuthorityResolution is { IsAllowed: false };
}
