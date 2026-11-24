using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.Policies;

/// <summary>Replaces caller publication claims with a decision from a server-owned authority.</summary>
public sealed class TimeEntryCommentPublicationIssuer(
    ITimeEntryCommentPublicationAuthority? authority = null)
{
    /// <summary>The current issued-decision contract version.</summary>
    public const int CurrentVersion = Hexalith.Timesheets.Contracts.TimesheetsDomain.CommentPublicationVersion;

    private readonly ITimeEntryCommentPublicationAuthority _authority = authority
        ?? new DenyAllTimeEntryCommentPublicationAuthority();

    /// <summary>Issues a decision for one comment, or returns null when no comment was supplied.</summary>
    public TimeEntryComment? Issue(TimesheetsRequestContext context, TimeEntryComment? comment)
        => Issue(context, comment, evaluateFinance: true);

    /// <summary>Issues publication decisions, with optional finance evaluation for stored recording verification.</summary>
    internal TimeEntryComment? Issue(TimesheetsRequestContext context, TimeEntryComment? comment, bool evaluateFinance)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (comment is null) return null;
        if (comment.Policy is null) return comment;
        bool allowed = _authority.MayPublish(context, comment);
        TimesheetsCommentPolicyDecision decision = allowed
            ? TimesheetsCommentPolicyDecision.Allowed
            : TimesheetsCommentPolicyDecision.PolicyRequired;
        TimeEntryComment externalCandidate = new(comment.Text, comment.Policy with
        {
            RedactionRequirement = TimesheetsCommentRedactionRequirement.RequiredBeforeExternalDisclosure
        });
        bool mayDisplay = _authority.MayDisplayExternally(context, externalCandidate);
        bool clearedForUnredactedDisplay = mayDisplay
            && _authority.MayDisplayUnredactedExternally(context, externalCandidate);
        TimesheetsCommentPolicyDecision externalDecision = clearedForUnredactedDisplay
            ? TimesheetsCommentPolicyDecision.Allowed
            : TimesheetsCommentPolicyDecision.Excluded;
        bool exportCleared = evaluateFinance
            && comment.Policy.RedactionRequirement is TimesheetsCommentRedactionRequirement.NotRequired
                or TimesheetsCommentRedactionRequirement.RequiredBeforeExternalDisclosure
            && _authority.MayExportUnredacted(context, comment.Text);
        return new TimeEntryComment(comment.Text, comment.Policy with
        {
            ProjectionInclusion = decision,
            ServerProjectionInclusion = decision,
            ExternalConfirmationDisplay = externalDecision,
            ServerExternalConfirmationDisplay = externalDecision,
            ServerExportInclusion = exportCleared
                ? TimesheetsCommentPolicyDecision.Allowed
                : TimesheetsCommentPolicyDecision.Excluded,
            RedactionRequirement = clearedForUnredactedDisplay
                ? TimesheetsCommentRedactionRequirement.NotRequired
                : TimesheetsCommentRedactionRequirement.RequiredBeforeExternalDisclosure,
            ServerPublicationVersion = CurrentVersion
        });
    }
}
