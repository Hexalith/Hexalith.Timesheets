using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Commands.TimeEntries;

public sealed record RecordTimeEntry(
    TimeEntryId TimeEntryId,
    TimeEntryTargetReference Target,
    PartyReference Contributor,
    ActivityTypeId ActivityTypeId,
    DateOnly ServiceDate,
    int DurationMinutes,
    BillableState BillableState,
    ContributorCategory ContributorCategory,
    AiEffortMetrics? AiMetrics)
{
    public TimeEntryComment? Comment { get; init; }
}
