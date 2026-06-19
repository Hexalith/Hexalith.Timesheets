using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.ApprovalAuthority;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.TimeEntries;

namespace Hexalith.Timesheets.Server.TimesheetPeriods;

public sealed record TimesheetPeriodApprovalCommandResult(
    TimesheetsAuthorizationDecision Authorization,
    ApprovalAuthorityResolutionResult? AuthorityResolution,
    TimesheetsDomainResult? PeriodResult,
    IReadOnlyList<TimeEntryApprovalCommandResult> EntryResults,
    IReadOnlyList<TimesheetPeriodBlockingEntryGuidance> BlockingGuidance)
{
    public bool WasPeriodDispatched => Authorization.IsAuthorized
        && AuthorityResolution is { IsAllowed: true }
        && PeriodResult is { IsSuccess: true };

    public bool HasAcceptedEntryEvents => EntryResults.Any(static entry =>
        entry.DomainResult is { IsSuccess: true });

    public bool HasBlockedEntries => BlockingGuidance.Count > 0;
}
