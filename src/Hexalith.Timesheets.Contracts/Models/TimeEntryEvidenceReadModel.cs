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

    public ExternalContributionSource? ExternalSource { get; init; }

    public TimeEntryContributorConfirmationEvidence? ContributorConfirmation { get; init; }

    public TimeEntryEvidenceSourceAuthority SourceAuthority { get; init; } =
        TimeEntryEvidenceSourceAuthority.Unknown;

    public IReadOnlyList<TimeEntryEventLineageItem> EventLineage { get; init; } = [];

    public TimeEntryApprovalDecisionEvidence? ApprovalDecision { get; init; }

    public TimeEntryCorrectionEvidence? Correction { get; init; }

    public TimeEntryApprovedCorrectionEvidence? ApprovedCorrection { get; init; }

    public TimeEntryLockEvidence LockEvidence { get; init; } =
        TimeEntryLockEvidence.Unlocked;

    public TimeEntryDisplayHydration DisplayHydration { get; init; } =
        TimeEntryDisplayHydration.Unknown;
}
