using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record ActualTimeReportRowReadModel(
    TimeEntryTargetReference Target,
    string TenantLocalPeriodKey,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    PartyReference Contributor,
    ActivityTypeId ActivityTypeId,
    ActivityTypeScope ActivityTypeScope,
    BillableState BillableState,
    TimeEntryApprovalState ApprovalState,
    ContributorCategory ContributorCategory,
    int ActualMinutes,
    int SourceRowCount,
    int CorrectionCount,
    int SupersededCount,
    ActualTimeReportRowState RowState,
    ActualTimeReferenceStateMetadata ReferenceState,
    ProjectionFreshnessMetadata ProjectionFreshness)
{
    public TimeEntryDisplayHydration DisplayHydration { get; init; } =
        TimeEntryDisplayHydration.Unknown;

    public WorkPlannedEffortReadModel? WorkPlannedEffort { get; init; }
}
