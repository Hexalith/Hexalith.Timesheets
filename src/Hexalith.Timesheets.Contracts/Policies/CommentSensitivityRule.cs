namespace Hexalith.Timesheets.Contracts.Policies;

public sealed record CommentSensitivityRule(
    TimesheetsCommentVisibilityScope Scope,
    TimesheetsCommentPolicyDecision Decision,
    TimesheetsCommentRedactionRequirement RedactionRequirement,
    TimesheetsEvidenceRetentionCategory RetentionCategory,
    string SafeGuidance);
