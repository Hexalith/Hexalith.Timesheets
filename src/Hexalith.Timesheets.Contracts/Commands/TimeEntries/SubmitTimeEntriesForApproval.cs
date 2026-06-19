using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Commands.TimeEntries;

public sealed record SubmitTimeEntriesForApproval(
    TimeEntrySubmissionId TimeEntrySubmissionId,
    IReadOnlyList<TimeEntryId> TimeEntryIds,
    TimeEntrySubmissionScope SubmissionScope);
