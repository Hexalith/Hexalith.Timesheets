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
    /// <summary>Version of the server-issued publication decision; zero identifies untrusted history.</summary>
    public int ServerPublicationVersion { get; init; }

    /// <summary>Per-comment publication decision issued by the server, independent of caller policy.</summary>
    public TimesheetsCommentPolicyDecision ServerProjectionInclusion { get; init; } =
        TimesheetsCommentPolicyDecision.PolicyRequired;

    /// <summary>Server-issued decision for disclosure to an external confirmation recipient.</summary>
    public TimesheetsCommentPolicyDecision ServerExternalConfirmationDisplay { get; init; } =
        TimesheetsCommentPolicyDecision.PolicyRequired;

    /// <summary>Resolves projection publication from the issued decision and the trusted host policy.</summary>
    public TimesheetsCommentPolicyDecision ResolvePublication(bool trustedHostAllowsPublication)
        => ServerPublicationVersion is 1 or TimesheetsDomain.CommentPublicationVersion
            ? ServerProjectionInclusion switch
            {
                TimesheetsCommentPolicyDecision.Allowed when trustedHostAllowsPublication => TimesheetsCommentPolicyDecision.Allowed,
                TimesheetsCommentPolicyDecision.Excluded => TimesheetsCommentPolicyDecision.Excluded,
                _ => TimesheetsCommentPolicyDecision.PolicyRequired
            }
            : TimesheetsCommentPolicyDecision.PolicyRequired;

    public static TimeEntryCommentPolicy SensitiveDefault { get; } = new(
        TimesheetsCommentPolicyDecision.Allowed,
        TimesheetsCommentPolicyDecision.Excluded,
        TimesheetsCommentPolicyDecision.PolicyRequired,
        TimesheetsCommentPolicyDecision.Excluded,
        TimesheetsCommentPolicyDecision.Excluded,
        TimesheetsCommentRedactionRequirement.RequiredBeforeExternalDisclosure,
        TimesheetsEvidenceRetentionCategory.CommentText);
}
