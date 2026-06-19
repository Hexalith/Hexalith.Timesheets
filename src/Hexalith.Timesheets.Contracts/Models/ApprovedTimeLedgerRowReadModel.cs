using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record ApprovedTimeLedgerRowReadModel(
    TimeEntryId TimeEntryId,
    PartyReference Contributor,
    TimeEntryTargetReference Target,
    DateOnly ServiceDate,
    int DurationMinutes,
    ActivityTypeId ActivityTypeId,
    ActivityTypeScope ActivityTypeScope,
    BillableState BillableState,
    TimeEntryApprovalDecisionEvidence ApprovalDecision,
    TimeEntryLockEvidence LockEvidence,
    ApprovedTimeLedgerRowState RowState,
    ProjectionFreshnessMetadata ProjectionFreshness)
{
    public TimeEntryApprovedCorrectionEvidence? ApprovedCorrection { get; init; }

    public TimeEntryCorrectionEvidence? Correction { get; init; }

    public IReadOnlyList<TimeEntryEventLineageItem> EventLineage { get; init; } = [];

    public TimeEntryDisplayHydration DisplayHydration { get; init; } =
        TimeEntryDisplayHydration.Unknown;

    public TimesheetsCommentPolicyDecision CommentProjectionState { get; init; } =
        TimesheetsCommentPolicyDecision.Unknown;

    public TimeEntryComment? Comment { get; init; }

    public static ApprovedTimeLedgerRowReadModel CurrentFromEvidence(TimeEntryEvidenceReadModel evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        TimeEntryApprovalDecisionEvidence approvalDecision = evidence.ApprovalDecision
            ?? throw new ArgumentException("Approved ledger rows require approval decision evidence.", nameof(evidence));

        return new(
            evidence.TimeEntryId,
            evidence.Contributor,
            evidence.Target,
            evidence.ServiceDate,
            evidence.DurationMinutes,
            evidence.ActivityTypeId,
            evidence.ActivityTypeScope,
            evidence.BillableState,
            approvalDecision,
            evidence.LockEvidence,
            ApprovedTimeLedgerRowState.Current,
            evidence.ProjectionFreshness)
        {
            ApprovedCorrection = Sanitize(evidence.ApprovedCorrection),
            Correction = Sanitize(evidence.Correction),
            EventLineage = [.. evidence.EventLineage],
            DisplayHydration = evidence.DisplayHydration,
            CommentProjectionState = ResolveCommentState(evidence.Comment),
            Comment = ResolveProjectedComment(evidence.Comment)
        };
    }

    public static ApprovedTimeLedgerRowReadModel? SupersededFromApprovedCorrection(TimeEntryEvidenceReadModel evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        if (evidence.ApprovedCorrection is null || evidence.ApprovalDecision is null)
        {
            return null;
        }

        TimeEntryCorrectionValues priorValues = evidence.ApprovedCorrection.PreviousValues;

        return new(
            evidence.TimeEntryId,
            priorValues.Contributor,
            priorValues.Target,
            priorValues.ServiceDate,
            priorValues.DurationMinutes,
            priorValues.ActivityTypeId,
            evidence.ActivityTypeScope,
            priorValues.BillableState,
            evidence.ApprovalDecision,
            TimeEntryLockEvidence.Superseded(),
            ApprovedTimeLedgerRowState.Superseded,
            evidence.ProjectionFreshness)
        {
            ApprovedCorrection = Sanitize(evidence.ApprovedCorrection),
            Correction = Sanitize(evidence.Correction),
            EventLineage = [.. evidence.EventLineage],
            DisplayHydration = evidence.DisplayHydration,
            CommentProjectionState = ResolveCommentState(priorValues.Comment),
            Comment = ResolveProjectedComment(priorValues.Comment)
        };
    }

    private static TimesheetsCommentPolicyDecision ResolveCommentState(TimeEntryComment? comment)
        => comment?.Policy.ProjectionInclusion ?? TimesheetsCommentPolicyDecision.Unknown;

    private static TimeEntryComment? ResolveProjectedComment(TimeEntryComment? comment)
        => comment?.Policy.ProjectionInclusion == TimesheetsCommentPolicyDecision.Allowed
            ? comment
            : null;

    private static TimeEntryApprovedCorrectionEvidence? Sanitize(TimeEntryApprovedCorrectionEvidence? evidence)
        => evidence is null
            ? null
            : evidence with
            {
                PreviousValues = Sanitize(evidence.PreviousValues),
                CorrectedValues = Sanitize(evidence.CorrectedValues)
            };

    private static TimeEntryCorrectionEvidence? Sanitize(TimeEntryCorrectionEvidence? evidence)
        => evidence is null
            ? null
            : evidence with
            {
                PreviousValues = Sanitize(evidence.PreviousValues),
                CorrectedValues = Sanitize(evidence.CorrectedValues)
            };

    private static TimeEntryCorrectionValues Sanitize(TimeEntryCorrectionValues values)
        => values with
        {
            Comment = ResolveProjectedComment(values.Comment)
        };
}
