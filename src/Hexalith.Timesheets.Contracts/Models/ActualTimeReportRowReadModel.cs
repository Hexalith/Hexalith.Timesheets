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
    public int? AiWallClockDurationMilliseconds { get; init; }

    public int? AiModelRuntimeMilliseconds { get; init; }

    public int? AiBillableEffortMinutes { get; init; }

    public long? AiProviderInputTokenCount { get; init; }

    public long? AiProviderOutputTokenCount { get; init; }

    public long? AiProviderTotalTokenCount { get; init; }

    public AiMetricAvailability AiMetricAvailability { get; init; } =
        AiMetricAvailability.Unavailable;

    public AiTokenMetricAvailability AiTokenAvailability { get; init; } =
        AiTokenMetricAvailability.Unavailable;

    public AiEffortMetricSourceMetadata? AiMetricSourceMetadata { get; init; }

    public TimeEntryDisplayHydration DisplayHydration { get; init; } =
        TimeEntryDisplayHydration.Unknown;

    public WorkPlannedEffortReadModel? WorkPlannedEffort { get; init; }
}
