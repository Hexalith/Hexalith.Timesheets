using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimeEntries;

public sealed record TimeEntrySubmissionEntryResult(
    TimeEntryId TimeEntryId,
    TimesheetsAuthorizationDecision Authorization,
    TimesheetsDomainResult? DomainResult,
    bool AggregateDispatched)
{
    public bool WasDispatched => Authorization.IsAuthorized && AggregateDispatched;
}

public sealed record TimeEntrySubmissionCommandResult(
    IReadOnlyList<TimeEntrySubmissionEntryResult> Entries)
{
    public bool HasAcceptedEvents => Entries.Any(static entry =>
        entry.DomainResult is { IsSuccess: true });

    public bool HasBlockedEntries => Entries.Any(static entry =>
        !entry.Authorization.IsAuthorized || entry.DomainResult is { IsRejection: true });

    public bool IsPartial => HasAcceptedEvents && HasBlockedEntries;
}
