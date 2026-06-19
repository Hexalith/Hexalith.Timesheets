namespace Hexalith.Timesheets.Contracts.Policies;

public sealed record TimesheetsEvidencePolicyDescriptor(
    IReadOnlyList<EvidenceRetentionRule> RetentionRules,
    IReadOnlyList<CommentSensitivityRule> CommentRules,
    IReadOnlyList<string> LaunchReadinessGaps)
{
    public static TimesheetsEvidencePolicyDescriptor FailClosedDefault { get; } = new(
        DefaultRetentionRules(),
        DefaultCommentRules(commentExportDecision: TimesheetsCommentPolicyDecision.Excluded),
        [
            "Legal-hold policy is unresolved.",
            "Tenant-specific retention overrides are unresolved.",
            "Comment sensitivity policy is unresolved."
        ]);

    public static IReadOnlyList<EvidenceRetentionRule> DefaultRetentionRules()
    {
        return
        [
            new(
                TimesheetsEvidenceRetentionCategory.TimeEntryEvidence,
                TimesheetsRetentionPosture.RetainedByDefault,
                "Time Entry evidence is retained by default."),
            new(
                TimesheetsEvidenceRetentionCategory.CommentText,
                TimesheetsRetentionPosture.LegalHoldRequired,
                "Comment retention policy is unresolved."),
            new(
                TimesheetsEvidenceRetentionCategory.ExportRecord,
                TimesheetsRetentionPosture.TenantOverrideRequired,
                "Export record retention requires tenant policy."),
            new(
                TimesheetsEvidenceRetentionCategory.MagicLinkConfirmationAuditMetadata,
                TimesheetsRetentionPosture.LegalHoldRequired,
                "Confirmation audit metadata retention is unresolved.")
        ];
    }

    public static IReadOnlyList<CommentSensitivityRule> DefaultCommentRules(
        TimesheetsCommentPolicyDecision commentExportDecision)
    {
        return
        [
            new(
                TimesheetsCommentVisibilityScope.InternalDisplay,
                TimesheetsCommentPolicyDecision.Allowed,
                TimesheetsCommentRedactionRequirement.NotRequired,
                TimesheetsEvidenceRetentionCategory.CommentText,
                "Comments may contain sensitive information."),
            new(
                TimesheetsCommentVisibilityScope.ExternalConfirmationDisplay,
                TimesheetsCommentPolicyDecision.Excluded,
                TimesheetsCommentRedactionRequirement.RequiredBeforeExternalDisclosure,
                TimesheetsEvidenceRetentionCategory.CommentText,
                "Comments may be excluded by policy."),
            new(
                TimesheetsCommentVisibilityScope.ProjectionReadModel,
                TimesheetsCommentPolicyDecision.PolicyRequired,
                TimesheetsCommentRedactionRequirement.RequiredBeforeExternalDisclosure,
                TimesheetsEvidenceRetentionCategory.CommentText,
                "Comment projection requires policy."),
            new(
                TimesheetsCommentVisibilityScope.ExportOutput,
                commentExportDecision,
                TimesheetsCommentRedactionRequirement.RequiredBeforeExport,
                TimesheetsEvidenceRetentionCategory.CommentText,
                "Export comments only when policy allows it."),
            new(
                TimesheetsCommentVisibilityScope.SupportDiagnostics,
                TimesheetsCommentPolicyDecision.Excluded,
                TimesheetsCommentRedactionRequirement.RequiredBeforeExternalDisclosure,
                TimesheetsEvidenceRetentionCategory.CommentText,
                "Comments are excluded from diagnostics.")
        ];
    }
}
