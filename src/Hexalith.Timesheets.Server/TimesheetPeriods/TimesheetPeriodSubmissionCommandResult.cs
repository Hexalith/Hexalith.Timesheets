using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.TimeEntries;

namespace Hexalith.Timesheets.Server.TimesheetPeriods;

public sealed record TimesheetPeriodSubmissionCommandResult(
    TimesheetsAuthorizationDecision Authorization,
    TimesheetsDomainResult? PeriodResult,
    IReadOnlyList<TimeEntrySubmissionEntryResult> EntryResults,
    IReadOnlyList<TimesheetPeriodBlockingEntryGuidance> BlockingGuidance)
{
    public bool WasPeriodDispatched => Authorization.IsAuthorized
        && PeriodResult is { IsSuccess: true };

    public bool HasAcceptedEntryEvents => EntryResults.Any(static entry =>
        entry.DomainResult is { IsSuccess: true });

    public bool HasBlockedEntries => BlockingGuidance.Count > 0;

    public IReadOnlyList<TimeEntryId> ValidTimeEntryIds { get; init; } = [];
}
