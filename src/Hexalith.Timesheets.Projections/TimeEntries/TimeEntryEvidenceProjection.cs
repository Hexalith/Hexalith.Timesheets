using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Projections.TimeEntries;

public sealed class TimeEntryEvidenceProjection
{
    public const string ProjectionName = "time-entry-evidence";

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

        TimeEntryEvidenceReadModel? model = null;
        HashSet<string> appliedMessageIds = new(StringComparer.Ordinal);
        List<TimeEntryEventLineageItem> lineage = [];

        foreach (TimeEntryProjectionEvent projectionEvent in events
            .OrderBy(static projectionEvent => projectionEvent.SequenceNumber))
        {
            if (string.IsNullOrWhiteSpace(projectionEvent.MessageId)
                || !appliedMessageIds.Add(projectionEvent.MessageId))
            {
                continue;
            }

            if (projectionEvent.Payload is TimeEntryRecorded recorded
                && recorded.TimeEntryId == timeEntryId)
            {
                lineage.Add(CreateLineageItem(projectionEvent));
                model = Apply(recorded, checkpoint, lineage);
            }
            else if (projectionEvent.Payload is TimeEntrySubmitted submitted
                && submitted.TimeEntryId == timeEntryId
                && model is not null
                && model.ApprovalState == TimeEntryApprovalState.Draft)
            {
                lineage.Add(CreateLineageItem(projectionEvent));
                model = Apply(submitted, model, checkpoint, lineage);
            }
            else if (projectionEvent.Payload is TimeEntryApproved approved
                && approved.TimeEntryId == timeEntryId
                && model?.ApprovalState == TimeEntryApprovalState.Submitted)
            {
                lineage.Add(CreateLineageItem(projectionEvent));
                model = Apply(approved, model, checkpoint, lineage);
            }
            else if (projectionEvent.Payload is TimeEntryRejected rejected
                && rejected.TimeEntryId == timeEntryId
                && model?.ApprovalState == TimeEntryApprovalState.Submitted)
            {
                lineage.Add(CreateLineageItem(projectionEvent));
                model = Apply(rejected, model, checkpoint, lineage);
            }
            else if (projectionEvent.Payload is TimeEntryCorrected corrected
                && corrected.TimeEntryId == timeEntryId
                && model?.ApprovalState == TimeEntryApprovalState.Rejected)
            {
                lineage.Add(CreateLineageItem(projectionEvent));
                model = Apply(corrected, model, checkpoint, lineage);
            }
            else if (projectionEvent.Payload is TimeEntryApprovedCorrected approvedCorrected
                && approvedCorrected.TimeEntryId == timeEntryId
                && model?.ApprovalState == TimeEntryApprovalState.Approved)
            {
                lineage.Add(CreateLineageItem(projectionEvent));
                model = Apply(approvedCorrected, model, checkpoint, lineage);
            }
        }

        return model;
    }

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
            ToFreshnessMetadata(checkpoint))
        {
            Comment = recorded.Comment,
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
            ProjectionFreshness = ToFreshnessMetadata(checkpoint),
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
            ProjectionFreshness = ToFreshnessMetadata(checkpoint),
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
            ProjectionFreshness = ToFreshnessMetadata(checkpoint),
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
            ServiceDate = corrected.CorrectedValues.ServiceDate,
            DurationMinutes = corrected.CorrectedValues.DurationMinutes,
            BillableState = corrected.CorrectedValues.BillableState,
            ApprovalState = corrected.ApprovalState,
            ContributorCategory = corrected.CorrectedValues.ContributorCategory,
            AiMetrics = corrected.CorrectedValues.AiMetrics,
            CorrectionState = corrected.CorrectionState,
            Comment = corrected.CorrectedValues.Comment,
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
            ProjectionFreshness = ToFreshnessMetadata(checkpoint),
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
            ServiceDate = corrected.CorrectedValues.ServiceDate,
            DurationMinutes = corrected.CorrectedValues.DurationMinutes,
            BillableState = corrected.CorrectedValues.BillableState,
            ApprovalState = corrected.ApprovalState,
            ContributorCategory = corrected.CorrectedValues.ContributorCategory,
            AiMetrics = corrected.CorrectedValues.AiMetrics,
            CorrectionState = corrected.CorrectionState,
            Comment = corrected.CorrectedValues.Comment,
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
            ProjectionFreshness = ToFreshnessMetadata(checkpoint),
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

    private static ProjectionFreshnessMetadata ToFreshnessMetadata(TimesheetsProjectionCheckpoint checkpoint)
        => checkpoint.Freshness switch
        {
            ProjectionFreshness.Fresh => new(
                ProjectionFreshnessState.Fresh,
                checkpoint.SequenceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                null,
                null),
            ProjectionFreshness.Rebuilding => ProjectionFreshnessMetadata.Rebuilding(),
            ProjectionFreshness.Stale => ProjectionFreshnessMetadata.Stale(
                checkpoint.SequenceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ProjectionFreshness.Unavailable => ProjectionFreshnessMetadata.Unavailable(),
            _ => new(ProjectionFreshnessState.Unknown, null, null, "Projection freshness is unknown.")
        };
}
