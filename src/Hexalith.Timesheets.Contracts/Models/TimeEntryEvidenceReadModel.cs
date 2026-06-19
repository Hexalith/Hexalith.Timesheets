using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record TimeEntryEvidenceReadModel(
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
    AiEffortMetrics? AiMetrics,
    TimeEntryCorrectionState CorrectionState,
    ProjectionFreshnessMetadata ProjectionFreshness)
{
    public TimeEntryComment? Comment { get; init; }

    public TimeEntryEvidenceSourceAuthority SourceAuthority { get; init; } =
        TimeEntryEvidenceSourceAuthority.Unknown;

    public IReadOnlyList<TimeEntryEventLineageItem> EventLineage { get; init; } = [];

    public TimeEntryDisplayHydration DisplayHydration { get; init; } =
        TimeEntryDisplayHydration.Unknown;
}
