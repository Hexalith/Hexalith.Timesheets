using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Projections.TimeEntries;

/// <summary>Folds one owner's recognized stored events into display evidence.</summary>
public sealed class TimeEntryEvidenceProjection
{
    /// <summary>The registered aggregate-owned projection name.</summary>
    public const string ProjectionName = "time-entry-evidence";

    /// <summary>Projects one owner's canonical event history into a Time Entry evidence row.</summary>
    public TimeEntryEvidenceReadModel? Project(
        string tenantId,
        TimeEntryId timeEntryId,
        IEnumerable<TimeEntryProjectionEvent> events,
        TimesheetsProjectionCheckpoint checkpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(timeEntryId);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(checkpoint);

        checkpoint.ValidateTenant(tenantId);

        TimeEntryEvidenceReadModel? model = null;
        List<TimeEntryEventLineageItem> lineage = [];

        TimeEntryProjectionEvent[] delivered = [.. events];
        if (delivered.Any(item => !StringComparer.Ordinal.Equals(item.TenantId, tenantId)))
        {
            throw new InvalidOperationException("Time Entry delivery tenant does not match the requested projection.");
        }

        foreach (TimeEntryProjectionEvent projectionEvent in TimeEntryStoredEventNormalizer.Normalize(delivered))
        {
            if (!StringComparer.Ordinal.Equals(projectionEvent.TenantId, tenantId)
                || !MatchesTenant(projectionEvent.Payload, tenantId))
            {
                throw new InvalidOperationException("Time Entry event tenant does not match the requested projection.");
            }

            TimeEntryEvidenceReadModel? next = ProjectNextCore(timeEntryId, model, projectionEvent, checkpoint, []);
            if (next is not null)
            {
                lineage.Add(CreateLineageItem(projectionEvent));
                model = next;
            }
        }

        return model is null ? null : model with { EventLineage = [.. lineage] };
    }

    /// <summary>Applies one contiguous recognized owner event to a verified selected prefix.</summary>
    internal TimeEntryEvidenceReadModel? ProjectNext(
        string tenantId, TimeEntryId timeEntryId, TimeEntryEvidenceReadModel? current,
        TimeEntryProjectionEvent item, TimesheetsProjectionCheckpoint checkpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(timeEntryId);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(checkpoint);
        checkpoint.ValidateTenant(tenantId);
        if (!string.Equals(item.TenantId, tenantId, StringComparison.Ordinal)
            || !MatchesTenant(item.Payload, tenantId))
            throw new InvalidOperationException("The Time Entry suffix has another tenant.");
        IReadOnlyList<TimeEntryEventLineageItem> lineage = [.. current?.EventLineage ?? [],
            CreateLineageItem(item)];
        return ProjectNextCore(timeEntryId, current, item, checkpoint, lineage);
    }

    private static TimeEntryEvidenceReadModel? ProjectNextCore(
        TimeEntryId timeEntryId, TimeEntryEvidenceReadModel? current,
        TimeEntryProjectionEvent item, TimesheetsProjectionCheckpoint checkpoint,
        IReadOnlyList<TimeEntryEventLineageItem> lineage)
        => item.Payload switch
        {
            TimeEntryRecorded recorded when recorded.TimeEntryId == timeEntryId && current is null
                && recorded.ApprovalState == TimeEntryApprovalState.Draft
                => Apply(recorded, checkpoint, lineage),
            TimeEntrySubmitted submitted when submitted.TimeEntryId == timeEntryId
                && current?.ApprovalState == TimeEntryApprovalState.Draft
                && submitted.ApprovalState == TimeEntryApprovalState.Submitted
                => Apply(submitted, current, checkpoint, lineage),
            TimeEntryContributorConfirmed confirmed when confirmed.TimeEntryId == timeEntryId
                && current is not null
                => Apply(confirmed, current, checkpoint, lineage),
            TimeEntryAdjustedThroughMagicLink adjusted when adjusted.TimeEntryId == timeEntryId
                && current?.ApprovalState == TimeEntryApprovalState.Draft
                && current.CorrectionState == TimeEntryCorrectionState.None
                && MatchesPreviousScope(adjusted.PreviousValues, current)
                && (adjusted.AdjustedValues.ActivityTypeScope is null
                    || adjusted.AdjustedValues.ActivityTypeScope == adjusted.ActivityTypeScope)
                => Apply(adjusted, current, checkpoint, lineage),
            TimeEntryApproved approved when approved.TimeEntryId == timeEntryId
                && current?.ApprovalState == TimeEntryApprovalState.Submitted
                && approved.ApprovalState == TimeEntryApprovalState.Approved
                => Apply(approved, current, checkpoint, lineage),
            TimeEntryRejected rejected when rejected.TimeEntryId == timeEntryId
                && current?.ApprovalState == TimeEntryApprovalState.Submitted
                && rejected.ApprovalState == TimeEntryApprovalState.Rejected
                => Apply(rejected, current, checkpoint, lineage),
            TimeEntryCorrected corrected when corrected.TimeEntryId == timeEntryId
                && current?.ApprovalState == TimeEntryApprovalState.Rejected
                && current.CorrectionState == TimeEntryCorrectionState.None
                && corrected.ApprovalState == TimeEntryApprovalState.Draft
                && corrected.CorrectionState == TimeEntryCorrectionState.Corrected
                && MatchesPreviousScope(corrected.PreviousValues, current)
                && corrected.RejectionDecisionId == current.ApprovalDecision?.TimeEntryApprovalDecisionId
                && corrected.RejectionReason == current.ApprovalDecision?.Reason
                => Apply(corrected, current, checkpoint, lineage),
            TimeEntryApprovedCorrected corrected when corrected.TimeEntryId == timeEntryId
                && current?.ApprovalState == TimeEntryApprovalState.Approved
                && current.CorrectionState == TimeEntryCorrectionState.None
                && current.ApprovalDecision is not null
                && corrected.ApprovalState == TimeEntryApprovalState.Approved
                && corrected.CorrectionState == TimeEntryCorrectionState.Corrected
                && MatchesPreviousScope(corrected.PreviousValues, current)
                && corrected.SourceApprovalDecisionId == current.ApprovalDecision.TimeEntryApprovalDecisionId
                && corrected.SourceApprovalScope == current.ApprovalDecision.ApprovalScope
                => Apply(corrected, current, checkpoint, lineage),
            _ when IsSelectedOwnerEvent(item.Payload, timeEntryId)
                => throw new InvalidOperationException("Time Entry evidence contradicts its preceding lifecycle or scope."),
            _ => null
        };

    private static bool MatchesPreviousScope(TimeEntryCorrectionValues previous, TimeEntryEvidenceReadModel current)
        => previous.ActivityTypeScope is null || previous.ActivityTypeScope == current.ActivityTypeScope;

    private static bool IsSelectedOwnerEvent(object payload, TimeEntryId owner)
        => payload switch
        {
            TimeEntryRecorded item => item.TimeEntryId == owner,
            TimeEntrySubmitted item => item.TimeEntryId == owner,
            TimeEntryContributorConfirmed item => item.TimeEntryId == owner,
            TimeEntryAdjustedThroughMagicLink item => item.TimeEntryId == owner,
            TimeEntryApproved item => item.TimeEntryId == owner,
            TimeEntryRejected item => item.TimeEntryId == owner,
            TimeEntryCorrected item => item.TimeEntryId == owner,
            TimeEntryApprovedCorrected item => item.TimeEntryId == owner,
            _ => false
        };

    /// <summary>Checks the tenant reference of recognized stored payloads.</summary>
    internal static bool MatchesTenant(object payload, string tenantId)
        => payload switch
        {
            TimeEntrySubmitted item => item.Tenant.TenantId == tenantId,
            TimeEntryContributorConfirmed item => item.Tenant.TenantId == tenantId,
            TimeEntryAdjustedThroughMagicLink item => item.Tenant.TenantId == tenantId,
            TimeEntryApproved item => item.Tenant.TenantId == tenantId,
            TimeEntryRejected item => item.Tenant.TenantId == tenantId,
            TimeEntryCorrected item => item.Tenant.TenantId == tenantId,
            TimeEntryApprovedCorrected item => item.Tenant.TenantId == tenantId,
            MagicLinkConfirmationCapabilityUsed item => item.Tenant.TenantId == tenantId,
            MagicLinkConfirmationCapabilityRevoked item => item.Tenant.TenantId == tenantId,
            MagicLinkConfirmationCapabilityExpired item => item.Tenant.TenantId == tenantId,
            _ => true
        };

    private static TimeEntryEvidenceReadModel Apply(
        TimeEntryRecorded recorded,
        TimesheetsProjectionCheckpoint checkpoint,
        IReadOnlyList<TimeEntryEventLineageItem> lineage)
        => new(
            recorded.TimeEntryId,
            recorded.Target,
            recorded.Contributor,
            recorded.ActivityTypeId,
            recorded.ActivityTypeScope,
            recorded.ServiceDate,
            recorded.DurationMinutes,
            recorded.BillableState,
            recorded.ApprovalState,
            recorded.ContributorCategory,
            recorded.AiMetrics,
            TimeEntryCorrectionState.None,
            ProjectionFreshnessMetadataMapper.ToMetadata(checkpoint))
        {
            Comment = recorded.Comment,
            CommentProjectionState = recorded.Comment?.Policy.ProjectionInclusion
                ?? TimesheetsCommentPolicyDecision.Unknown,
            ExternalSource = recorded.ExternalSource,
            SourceAuthority = TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents,
            EventLineage = [.. lineage],
            LockEvidence = TimeEntryLockEvidence.Unlocked,
            DisplayHydration = TimeEntryDisplayHydration.Unavailable()
        };

    private static TimeEntryEvidenceReadModel Apply(
        TimeEntrySubmitted submitted,
        TimeEntryEvidenceReadModel current,
        TimesheetsProjectionCheckpoint checkpoint,
        IReadOnlyList<TimeEntryEventLineageItem> lineage)
        => current with
        {
            ApprovalState = submitted.ApprovalState,
            ProjectionFreshness = ProjectionFreshnessMetadataMapper.ToMetadata(checkpoint),
            SourceAuthority = TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents,
            EventLineage = [.. lineage],
            LockEvidence = TimeEntryLockEvidence.Unlocked,
            DisplayHydration = current.DisplayHydration == TimeEntryDisplayHydration.Unknown
                ? TimeEntryDisplayHydration.Unavailable()
                : current.DisplayHydration
        };

    private static TimeEntryEvidenceReadModel Apply(
        TimeEntryContributorConfirmed confirmed,
        TimeEntryEvidenceReadModel current,
        TimesheetsProjectionCheckpoint checkpoint,
        IReadOnlyList<TimeEntryEventLineageItem> lineage)
        => current with
        {
            ContributorConfirmation = new(
                confirmed.TimeEntryId,
                confirmed.Contributor,
                confirmed.ConfirmedAtUtc,
                confirmed.Source),
            ProjectionFreshness = ProjectionFreshnessMetadataMapper.ToMetadata(checkpoint),
            SourceAuthority = TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents,
            EventLineage = [.. lineage],
            LockEvidence = current.LockEvidence,
            DisplayHydration = current.DisplayHydration == TimeEntryDisplayHydration.Unknown
                ? TimeEntryDisplayHydration.Unavailable()
                : current.DisplayHydration
        };

    private static TimeEntryEvidenceReadModel Apply(
        TimeEntryAdjustedThroughMagicLink adjusted,
        TimeEntryEvidenceReadModel current,
        TimesheetsProjectionCheckpoint checkpoint,
        IReadOnlyList<TimeEntryEventLineageItem> lineage)
        => current with
        {
            ActivityTypeId = adjusted.AdjustedValues.ActivityTypeId,
            ActivityTypeScope = adjusted.ActivityTypeScope,
            ServiceDate = adjusted.AdjustedValues.ServiceDate,
            DurationMinutes = adjusted.AdjustedValues.DurationMinutes,
            BillableState = adjusted.AdjustedValues.BillableState,
            ContributorCategory = adjusted.AdjustedValues.ContributorCategory,
            AiMetrics = adjusted.AdjustedValues.AiMetrics,
            Comment = adjusted.AdjustedValues.Comment,
            CommentProjectionState = adjusted.AdjustedValues.Comment?.Policy.ProjectionInclusion
                ?? TimesheetsCommentPolicyDecision.Unknown,
            ExternalAdjustment = new(
                adjusted.TimeEntryId,
                adjusted.Contributor,
                adjusted.Tenant,
                adjusted.AdjustedAtUtc,
                adjusted.PreviousValues,
                adjusted.AdjustedValues,
                adjusted.Source),
            ProjectionFreshness = ProjectionFreshnessMetadataMapper.ToMetadata(checkpoint),
            SourceAuthority = TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents,
            EventLineage = [.. lineage],
            LockEvidence = TimeEntryLockEvidence.Unlocked,
            DisplayHydration = current.DisplayHydration == TimeEntryDisplayHydration.Unknown
                ? TimeEntryDisplayHydration.Unavailable()
                : current.DisplayHydration
        };

    private static TimeEntryEvidenceReadModel Apply(
        TimeEntryApproved approved,
        TimeEntryEvidenceReadModel current,
        TimesheetsProjectionCheckpoint checkpoint,
        IReadOnlyList<TimeEntryEventLineageItem> lineage)
        => current with
        {
            ApprovalState = approved.ApprovalState,
            ApprovalDecision = new(
                approved.TimeEntryId,
                approved.TimeEntryApprovalDecisionId,
                approved.Approver,
                approved.Tenant,
                approved.DecidedAtUtc,
                approved.ApprovalState,
                approved.ApprovalScope,
                approved.AuthoritySource,
                null),
            ProjectionFreshness = ProjectionFreshnessMetadataMapper.ToMetadata(checkpoint),
            SourceAuthority = TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents,
            EventLineage = [.. lineage],
            LockEvidence = TimeEntryLockEvidence.Approved(
                approved.TimeEntryApprovalDecisionId,
                approved.ApprovalScope,
                approved.Approver,
                approved.DecidedAtUtc),
            DisplayHydration = current.DisplayHydration == TimeEntryDisplayHydration.Unknown
                ? TimeEntryDisplayHydration.Unavailable()
                : current.DisplayHydration
        };

    private static TimeEntryEvidenceReadModel Apply(
        TimeEntryRejected rejected,
        TimeEntryEvidenceReadModel current,
        TimesheetsProjectionCheckpoint checkpoint,
        IReadOnlyList<TimeEntryEventLineageItem> lineage)
        => current with
        {
            ApprovalState = rejected.ApprovalState,
            ApprovalDecision = new(
                rejected.TimeEntryId,
                rejected.TimeEntryApprovalDecisionId,
                rejected.Approver,
                rejected.Tenant,
                rejected.DecidedAtUtc,
                rejected.ApprovalState,
                rejected.ApprovalScope,
                rejected.AuthoritySource,
                rejected.Reason),
            ProjectionFreshness = ProjectionFreshnessMetadataMapper.ToMetadata(checkpoint),
            SourceAuthority = TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents,
            EventLineage = [.. lineage],
            LockEvidence = TimeEntryLockEvidence.Unlocked,
            DisplayHydration = current.DisplayHydration == TimeEntryDisplayHydration.Unknown
                ? TimeEntryDisplayHydration.Unavailable()
                : current.DisplayHydration
        };

    private static TimeEntryEvidenceReadModel Apply(
        TimeEntryCorrected corrected,
        TimeEntryEvidenceReadModel current,
        TimesheetsProjectionCheckpoint checkpoint,
        IReadOnlyList<TimeEntryEventLineageItem> lineage)
        => current with
        {
            Target = corrected.CorrectedValues.Target,
            Contributor = corrected.CorrectedValues.Contributor,
            ActivityTypeId = corrected.CorrectedValues.ActivityTypeId,
            // Legacy corrections omitted scope; projection replay must retain the preceding scope
            // rather than infer historical ownership from today's catalog.
            ActivityTypeScope = corrected.CorrectedValues.ActivityTypeScope ?? current.ActivityTypeScope,
            ServiceDate = corrected.CorrectedValues.ServiceDate,
            DurationMinutes = corrected.CorrectedValues.DurationMinutes,
            BillableState = corrected.CorrectedValues.BillableState,
            ApprovalState = corrected.ApprovalState,
            ContributorCategory = corrected.CorrectedValues.ContributorCategory,
            AiMetrics = corrected.CorrectedValues.AiMetrics,
            CorrectionState = corrected.CorrectionState,
            Comment = corrected.CorrectedValues.Comment,
            CommentProjectionState = corrected.CorrectedValues.Comment?.Policy.ProjectionInclusion
                ?? TimesheetsCommentPolicyDecision.Unknown,
            Correction = new(
                corrected.TimeEntryId,
                corrected.TimeEntryCorrectionId,
                corrected.CorrectedBy,
                corrected.Tenant,
                corrected.CorrectedAtUtc,
                corrected.RejectionReason,
                corrected.RejectionDecisionId,
                corrected.PreviousValues,
                corrected.CorrectedValues,
                corrected.CorrectionState),
            ProjectionFreshness = ProjectionFreshnessMetadataMapper.ToMetadata(checkpoint),
            SourceAuthority = TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents,
            EventLineage = [.. lineage],
            LockEvidence = corrected.CorrectionState == TimeEntryCorrectionState.Superseded
                ? TimeEntryLockEvidence.Superseded()
                : TimeEntryLockEvidence.Unlocked,
            DisplayHydration = current.DisplayHydration == TimeEntryDisplayHydration.Unknown
                ? TimeEntryDisplayHydration.Unavailable()
                : current.DisplayHydration
        };

    private static TimeEntryEvidenceReadModel Apply(
        TimeEntryApprovedCorrected corrected,
        TimeEntryEvidenceReadModel current,
        TimesheetsProjectionCheckpoint checkpoint,
        IReadOnlyList<TimeEntryEventLineageItem> lineage)
        => current with
        {
            Target = corrected.CorrectedValues.Target,
            Contributor = corrected.CorrectedValues.Contributor,
            ActivityTypeId = corrected.CorrectedValues.ActivityTypeId,
            // Legacy corrections omitted scope; projection replay must retain the preceding scope
            // rather than infer historical ownership from today's catalog.
            ActivityTypeScope = corrected.CorrectedValues.ActivityTypeScope ?? current.ActivityTypeScope,
            ServiceDate = corrected.CorrectedValues.ServiceDate,
            DurationMinutes = corrected.CorrectedValues.DurationMinutes,
            BillableState = corrected.CorrectedValues.BillableState,
            ApprovalState = corrected.ApprovalState,
            ContributorCategory = corrected.CorrectedValues.ContributorCategory,
            AiMetrics = corrected.CorrectedValues.AiMetrics,
            CorrectionState = corrected.CorrectionState,
            Comment = corrected.CorrectedValues.Comment,
            CommentProjectionState = corrected.CorrectedValues.Comment?.Policy.ProjectionInclusion
                ?? TimesheetsCommentPolicyDecision.Unknown,
            ApprovedCorrection = new(
                corrected.TimeEntryId,
                corrected.TimeEntryCorrectionId,
                corrected.CorrectedBy,
                corrected.Tenant,
                corrected.CorrectedAtUtc,
                corrected.Reason,
                corrected.SourceApprovalDecisionId,
                corrected.SourceApprovalScope,
                corrected.PreviousValues,
                corrected.CorrectedValues,
                corrected.ApprovalState,
                corrected.CorrectionState),
            ProjectionFreshness = ProjectionFreshnessMetadataMapper.ToMetadata(checkpoint),
            SourceAuthority = TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents,
            EventLineage = [.. lineage],
            LockEvidence = current.LockEvidence.LockState == TimeEntryLockState.LockedFromDirectEdit
                ? current.LockEvidence
                : TimeEntryLockEvidence.Approved(
                    corrected.SourceApprovalDecisionId,
                    corrected.SourceApprovalScope,
                    corrected.CorrectedBy,
                    corrected.CorrectedAtUtc),
            DisplayHydration = current.DisplayHydration == TimeEntryDisplayHydration.Unknown
                ? TimeEntryDisplayHydration.Unavailable()
                : current.DisplayHydration
        };

    private static TimeEntryEventLineageItem CreateLineageItem(TimeEntryProjectionEvent projectionEvent)
        => new(
            projectionEvent.Payload.GetType().Name,
            projectionEvent.SequenceNumber,
            TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents);

}
