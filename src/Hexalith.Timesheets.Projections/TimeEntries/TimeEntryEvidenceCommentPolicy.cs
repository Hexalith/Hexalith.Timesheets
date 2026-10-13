using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Projections.TimeEntries;

/// <summary>Applies server-issued comment authority before evidence leaves the fold.</summary>
internal static class TimeEntryEvidenceCommentPolicy
{
    internal const int CurrentVersion = 2;

    internal static TimeEntryEvidenceReadModel Apply(TimeEntryEvidenceReadModel row,
        bool trustedPublicationAllowed, bool preserveStoredVersion = false)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row with
        {
            CommentProjectionState = Decision(row.Comment, row.CommentProjectionState,
                trustedPublicationAllowed),
            CommentPublicationPolicyVersion = preserveStoredVersion
                ? row.CommentPublicationPolicyVersion : trustedPublicationAllowed ? 2 : 1,
            Comment = Include(row.Comment, trustedPublicationAllowed),
            ExternalAdjustment = row.ExternalAdjustment is { } adjustment
                ? adjustment with
                {
                    PreviousValues = Apply(adjustment.PreviousValues, trustedPublicationAllowed),
                    AdjustedValues = Apply(adjustment.AdjustedValues, trustedPublicationAllowed)
                }
                : null,
            Correction = row.Correction is { } correction
                ? correction with
                {
                    PreviousValues = Apply(correction.PreviousValues, trustedPublicationAllowed),
                    CorrectedValues = Apply(correction.CorrectedValues, trustedPublicationAllowed)
                }
                : null,
            ApprovedCorrection = row.ApprovedCorrection is { } approved
                ? approved with
                {
                    PreviousValues = Apply(approved.PreviousValues, trustedPublicationAllowed),
                    CorrectedValues = Apply(approved.CorrectedValues, trustedPublicationAllowed)
                }
                : null
        };
    }

    private static TimeEntryCorrectionValues Apply(TimeEntryCorrectionValues values, bool trustedPublicationAllowed)
        => values with { Comment = Include(values.Comment, trustedPublicationAllowed) };

    private static TimeEntryComment? Include(TimeEntryComment? comment, bool trustedPublicationAllowed)
        => trustedPublicationAllowed && comment?.Policy.ServerPublicationVersion is 1 or CurrentVersion
            && comment.Policy.ServerProjectionInclusion == TimesheetsCommentPolicyDecision.Allowed
            ? comment
            : null;

    private static TimesheetsCommentPolicyDecision Decision(
        TimeEntryComment? comment, TimesheetsCommentPolicyDecision prior, bool trustedPublicationAllowed)
        => comment is null
            ? prior == TimesheetsCommentPolicyDecision.Allowed
                ? TimesheetsCommentPolicyDecision.PolicyRequired : prior
            : Include(comment, trustedPublicationAllowed) is not null
                ? TimesheetsCommentPolicyDecision.Allowed
                : comment.Policy.ServerPublicationVersion is 1 or CurrentVersion
                    && comment.Policy.ServerProjectionInclusion == TimesheetsCommentPolicyDecision.Excluded
                    ? TimesheetsCommentPolicyDecision.Excluded
                    : TimesheetsCommentPolicyDecision.PolicyRequired;

    internal static TimeEntryEvidenceReadModel WithoutPublication(TimeEntryEvidenceReadModel row)
        => Apply(row, false) with
        {
            CommentProjectionState = TimesheetsCommentPolicyDecision.Unknown,
            CommentPublicationPolicyVersion = 0
        };
}
