using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record TimeEntryQueryRowReadModel(
    TimeEntryId TimeEntryId,
    TimeEntryTargetReference Target,
    PartyReference Contributor,
    ActivityTypeId ActivityTypeId,
    DateOnly ServiceDate,
    int DurationMinutes,
    BillableState BillableState,
    TimeEntryApprovalState ApprovalState,
    TimeEntryCorrectionState CorrectionState,
    ContributorCategory ContributorCategory,
    TimeEntrySourceType SourceType,
    ProjectionFreshnessMetadata ProjectionFreshness)
{
    public TimeEntryDisplayHydration DisplayHydration { get; init; } =
        TimeEntryDisplayHydration.Unknown;

    public static TimeEntryQueryRowReadModel FromEvidence(TimeEntryEvidenceReadModel evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        return new(
            evidence.TimeEntryId,
            evidence.Target,
            evidence.Contributor,
            evidence.ActivityTypeId,
            evidence.ServiceDate,
            evidence.DurationMinutes,
            evidence.BillableState,
            evidence.ApprovalState,
            evidence.CorrectionState,
            evidence.ContributorCategory,
            ResolveSourceType(evidence),
            evidence.ProjectionFreshness)
        {
            DisplayHydration = evidence.DisplayHydration
        };
    }

    private static TimeEntrySourceType ResolveSourceType(TimeEntryEvidenceReadModel evidence)
        => evidence.ContributorCategory switch
        {
            ContributorCategory.Employee => TimeEntrySourceType.Employee,
            ContributorCategory.ExternalContributor when evidence.ExternalSource is not null => TimeEntrySourceType.ExternalContributor,
            ContributorCategory.AutomatedAgent when evidence.AiMetrics is not null => TimeEntrySourceType.AutomatedAgent,
            _ => TimeEntrySourceType.Unknown
        };
}
