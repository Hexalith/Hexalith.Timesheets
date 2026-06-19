namespace Hexalith.Timesheets.Contracts.Policies;

public sealed record TimeEntryCommentPolicy(
    TimesheetsCommentPolicyDecision InternalDisplay,
    TimesheetsCommentPolicyDecision ExternalConfirmationDisplay,
    TimesheetsCommentPolicyDecision ProjectionInclusion,
    TimesheetsCommentPolicyDecision ExportInclusion,
    TimesheetsCommentPolicyDecision SupportDiagnostics,
    TimesheetsCommentRedactionRequirement RedactionRequirement,
    TimesheetsEvidenceRetentionCategory RetentionCategory)
{
    public static TimeEntryCommentPolicy SensitiveDefault { get; } = new(
        TimesheetsCommentPolicyDecision.Allowed,
        TimesheetsCommentPolicyDecision.Excluded,
        TimesheetsCommentPolicyDecision.PolicyRequired,
        TimesheetsCommentPolicyDecision.Excluded,
        TimesheetsCommentPolicyDecision.Excluded,
        TimesheetsCommentRedactionRequirement.RequiredBeforeExternalDisclosure,
        TimesheetsEvidenceRetentionCategory.CommentText);
}
