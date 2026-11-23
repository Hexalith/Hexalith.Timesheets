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
    ContributorCategory ContributorCategory,
    TimeEntryApprovalDecisionEvidence ApprovalDecision,
    TimeEntryLockEvidence LockEvidence,
    ApprovedTimeLedgerRowState RowState,
    ProjectionFreshnessMetadata ProjectionFreshness)
{
    public AiEffortMetrics? AiMetrics { get; init; }

    public TimeEntryApprovedCorrectionEvidence? ApprovedCorrection { get; init; }

    public TimeEntryCorrectionEvidence? Correction { get; init; }

    public IReadOnlyList<TimeEntryEventLineageItem> EventLineage { get; init; } = [];

    public TimeEntryDisplayHydration DisplayHydration { get; init; } =
        TimeEntryDisplayHydration.Unknown;

    public TimesheetsCommentPolicyDecision CommentProjectionState { get; init; } =
        TimesheetsCommentPolicyDecision.Unknown;

    public TimeEntryComment? Comment { get; init; }

    public static ApprovedTimeLedgerRowReadModel CurrentFromEvidence(TimeEntryEvidenceReadModel evidence)
        => CurrentFromEvidence(evidence, false);

    public static ApprovedTimeLedgerRowReadModel CurrentFromEvidence(TimeEntryEvidenceReadModel evidence, bool trustedPublicationAllowed)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        TimeEntryApprovalDecisionEvidence approvalDecision = evidence.ApprovalDecision
            ?? throw new ArgumentException("Approved ledger rows require approval decision evidence.", nameof(evidence));

        return new ApprovedTimeLedgerRowReadModel(
            evidence.TimeEntryId,
            evidence.Contributor,
            evidence.Target,
            evidence.ServiceDate,
            evidence.DurationMinutes,
            evidence.ActivityTypeId,
            evidence.ActivityTypeScope,
            evidence.BillableState,
            evidence.ContributorCategory,
            approvalDecision,
            evidence.LockEvidence,
            ApprovedTimeLedgerRowState.Current,
            evidence.ProjectionFreshness)
        {
            AiMetrics = evidence.AiMetrics,
            ApprovedCorrection = evidence.ApprovedCorrection,
            Correction = evidence.Correction,
            EventLineage = [.. evidence.EventLineage],
            DisplayHydration = evidence.DisplayHydration,
            CommentProjectionState = evidence.CommentProjectionState,
            Comment = evidence.Comment
        }.ApplyPublicationPolicy(trustedPublicationAllowed);
    }

    public static ApprovedTimeLedgerRowReadModel? SupersededFromApprovedCorrection(TimeEntryEvidenceReadModel evidence)
        => SupersededFromApprovedCorrection(evidence, false);

    public static ApprovedTimeLedgerRowReadModel? SupersededFromApprovedCorrection(TimeEntryEvidenceReadModel evidence, bool trustedPublicationAllowed)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        if (evidence.ApprovedCorrection is null || evidence.ApprovalDecision is null)
        {
            return null;
        }

        TimeEntryCorrectionValues priorValues = evidence.ApprovedCorrection.PreviousValues;

        return new ApprovedTimeLedgerRowReadModel(
            evidence.TimeEntryId,
            priorValues.Contributor,
            priorValues.Target,
            priorValues.ServiceDate,
            priorValues.DurationMinutes,
            priorValues.ActivityTypeId,
            priorValues.ActivityTypeScope ?? evidence.ActivityTypeScope,
            priorValues.BillableState,
            priorValues.ContributorCategory,
            evidence.ApprovalDecision,
            TimeEntryLockEvidence.Superseded(),
            ApprovedTimeLedgerRowState.Superseded,
            evidence.ProjectionFreshness)
        {
            AiMetrics = priorValues.AiMetrics,
            ApprovedCorrection = evidence.ApprovedCorrection,
            Correction = evidence.Correction,
            EventLineage = [.. evidence.EventLineage],
            DisplayHydration = evidence.DisplayHydration,
            Comment = priorValues.Comment
        }.ApplyPublicationPolicy(trustedPublicationAllowed);
    }

    /// <summary>Publishes only comment values allowed by both the trusted host and the issued decision.</summary>
    public ApprovedTimeLedgerRowReadModel ApplyPublicationPolicy(bool trustedPublicationAllowed)
    {
        TimesheetsCommentPolicyDecision state = Comment?.Policy.ResolvePublication(trustedPublicationAllowed)
            ?? (CommentProjectionState == TimesheetsCommentPolicyDecision.Allowed
                ? TimesheetsCommentPolicyDecision.PolicyRequired : CommentProjectionState);
        return SelectComments(comment => comment.Policy.ResolvePublication(trustedPublicationAllowed)
            == TimesheetsCommentPolicyDecision.Allowed ? comment : null) with { CommentProjectionState = state };
    }

    /// <summary>Applies a disclosure decision independently to every populated current or historical comment.</summary>
    public ApprovedTimeLedgerRowReadModel SelectComments(Func<TimeEntryComment, TimeEntryComment?> select)
    {
        ArgumentNullException.ThrowIfNull(select);
        TimeEntryCorrectionValues SelectValues(TimeEntryCorrectionValues values)
            => values.Comment is null ? values : values with { Comment = select(values.Comment) };

        return this with
        {
            Comment = Comment is null ? null : select(Comment),
            ApprovedCorrection = ApprovedCorrection is null ? null : ApprovedCorrection with
            {
                PreviousValues = SelectValues(ApprovedCorrection.PreviousValues),
                CorrectedValues = SelectValues(ApprovedCorrection.CorrectedValues)
            },
            Correction = Correction is null ? null : Correction with
            {
                PreviousValues = SelectValues(Correction.PreviousValues),
                CorrectedValues = SelectValues(Correction.CorrectedValues)
            }
        };
    }
}
