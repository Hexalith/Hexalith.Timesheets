using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimeEntries;

public sealed record ExternalContributionCommandResult(
    TimeEntryCommandResult RecordResult,
    TimeEntrySubmissionCommandResult? SubmissionResult);

public sealed record TimeEntryConfirmationCommandResult(
    TimesheetsAuthorizationDecision Authorization,
    TimesheetsDomainResult? DomainResult,
    bool AggregateDispatched)
{
    public bool WasDispatched => Authorization.IsAuthorized && AggregateDispatched;
}
