using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Events.TimeEntries;

public sealed record TimeEntryRecorded(
    TimeEntryId TimeEntryId,
    TimeEntryTargetReference Target,
    PartyReference Contributor,
    ActivityTypeId ActivityTypeId,
    ActivityTypeScope ActivityTypeScope,
    DateOnly ServiceDate,
    int DurationMinutes,
    BillableState BillableState,
    TimeEntryApprovalState ApprovalState,
    ContributorCategory ContributorCategory,
    AiEffortMetrics? AiMetrics);
