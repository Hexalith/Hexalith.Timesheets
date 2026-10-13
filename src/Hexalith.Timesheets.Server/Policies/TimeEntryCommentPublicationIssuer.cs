using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.Policies;

/// <summary>Replaces caller publication claims with a decision from a server-owned authority.</summary>
public sealed class TimeEntryCommentPublicationIssuer(
    ITimeEntryCommentPublicationAuthority? authority = null)
{
    /// <summary>The current issued-decision contract version.</summary>
    public const int CurrentVersion = 2;

    private readonly ITimeEntryCommentPublicationAuthority _authority = authority
        ?? new DenyAllTimeEntryCommentPublicationAuthority();

    /// <summary>Issues a decision for one comment, or returns null when no comment was supplied.</summary>
    public TimeEntryComment? Issue(TimesheetsRequestContext context, TimeEntryComment? comment)
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
        return new TimeEntryComment(comment.Text, comment.Policy with
        {
            ProjectionInclusion = decision,
            ServerProjectionInclusion = decision,
            ExternalConfirmationDisplay = externalDecision,
            ServerExternalConfirmationDisplay = externalDecision,
            RedactionRequirement = clearedForUnredactedDisplay
                ? TimesheetsCommentRedactionRequirement.NotRequired
                : TimesheetsCommentRedactionRequirement.RequiredBeforeExternalDisclosure,
            ServerPublicationVersion = CurrentVersion
        });
    }
}
