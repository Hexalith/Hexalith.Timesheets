using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

/// <summary>Materialized evidence for one Timesheets owned time entry.</summary>
/// <param name="TimeEntryId">The owning time entry identifier.</param>
/// <param name="Target">The referenced effort target.</param>
/// <param name="Contributor">The referenced contributor.</param>
/// <param name="ActivityTypeId">The referenced activity type.</param>
/// <param name="ActivityTypeScope">The activity type scope.</param>
/// <param name="ServiceDate">The date on which the effort occurred.</param>
/// <param name="DurationMinutes">The recorded effort in minutes.</param>
/// <param name="BillableState">The recorded billability state.</param>
/// <param name="ApprovalState">The current approval state.</param>
/// <param name="ContributorCategory">The effective contributor category.</param>
/// <param name="AiMetrics">The recorded AI effort metrics, when present.</param>
/// <param name="CorrectionState">The current correction state.</param>
/// <param name="ProjectionFreshness">The read model freshness marker.</param>
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
    private IReadOnlyList<TimeEntryEventLineageItem>? _eventLineage;
    private IReadOnlyList<string>? _eventPayloadLineage;

    /// <summary>Gets the comment retained from the current canonical evidence.</summary>
    public TimeEntryComment? Comment { get; init; }

    /// <summary>Gets the current comment's projection policy even when its text is omitted.</summary>
    public TimesheetsCommentPolicyDecision CommentProjectionState { get; init; } = TimesheetsCommentPolicyDecision.Unknown;

    /// <summary>Version of the policy used to materialize comment publication fields.</summary>
    public int CommentPublicationPolicyVersion { get; init; }

    /// <summary>Gets the external contribution source when one was recorded.</summary>
    public ExternalContributionSource? ExternalSource { get; init; }

    /// <summary>Gets contributor confirmation evidence, when present.</summary>
    public TimeEntryContributorConfirmationEvidence? ContributorConfirmation { get; init; }

    /// <summary>Gets the authority from which the materialized evidence was derived.</summary>
    public TimeEntryEvidenceSourceAuthority SourceAuthority { get; init; } =
        TimeEntryEvidenceSourceAuthority.Unknown;

    /// <summary>Complete accepted owner event metadata, restored from compact storage when present.</summary>
    public IReadOnlyList<TimeEntryEventLineageItem> EventLineage
    {
        get => _eventLineage ??= PackedEventLineage is null
            ? []
            : TimeEntryEvidenceLineageCodec.UnpackEvents(PackedEventLineage);
        init => _eventLineage = value;
    }

    /// <summary>Canonical payload digests for the complete accepted owner prefix.</summary>
    public IReadOnlyList<string> EventPayloadLineage
    {
        get => _eventPayloadLineage ??= PackedPayloadLineage is null
            ? []
            : TimeEntryEvidenceLineageCodec.UnpackPayloads(PackedPayloadLineage);
        init => _eventPayloadLineage = value;
    }

    /// <summary>Compact persisted event lineage for bounded EventStore replay writes.</summary>
    public byte[]? PackedEventLineage { get; init; }

    /// <summary>Compact persisted payload lineage for bounded EventStore replay writes.</summary>
    public byte[]? PackedPayloadLineage { get; init; }

    /// <summary>Gets the current approval decision evidence.</summary>
    public TimeEntryApprovalDecisionEvidence? ApprovalDecision { get; init; }

    /// <summary>Gets the latest correction evidence.</summary>
    public TimeEntryCorrectionEvidence? Correction { get; init; }

    /// <summary>Gets evidence for an approved correction.</summary>
    public TimeEntryApprovedCorrectionEvidence? ApprovedCorrection { get; init; }

    /// <summary>Gets evidence for an external adjustment.</summary>
    public TimeEntryExternalAdjustmentEvidence? ExternalAdjustment { get; init; }

    /// <summary>Gets the current edit lock evidence.</summary>
    public TimeEntryLockEvidence LockEvidence { get; init; } =
        TimeEntryLockEvidence.Unlocked;

    /// <summary>Gets the availability of display-owned supporting data.</summary>
    public TimeEntryDisplayHydration DisplayHydration { get; init; } =
        TimeEntryDisplayHydration.Unknown;
}
