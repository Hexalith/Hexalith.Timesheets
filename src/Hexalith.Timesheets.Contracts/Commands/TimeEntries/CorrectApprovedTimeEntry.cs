using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Commands.TimeEntries;

public sealed record CorrectApprovedTimeEntry(
    TimeEntryId TimeEntryId,
    TimeEntryCorrectionId TimeEntryCorrectionId,
    TimeEntryTargetReference Target,
    PartyReference Contributor,
    ActivityTypeId ActivityTypeId,
    DateOnly ServiceDate,
    int DurationMinutes,
    BillableState BillableState,
    ContributorCategory ContributorCategory,
    AiEffortMetrics? AiMetrics,
    TimeEntryCorrectionReason Reason)
{
    public TimeEntryComment? Comment { get; init; }
}
