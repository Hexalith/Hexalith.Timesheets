using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record TimeEntryCorrectionValues(
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
