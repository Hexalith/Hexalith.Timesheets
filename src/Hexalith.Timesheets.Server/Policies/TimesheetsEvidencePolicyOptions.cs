using Hexalith.Timesheets.Contracts.Policies;

namespace Hexalith.Timesheets.Server.Policies;

public sealed record TimesheetsEvidencePolicyOptions
{
    public static TimesheetsEvidencePolicyOptions FailClosedDefault { get; } = new();

    public bool LegalHoldPolicyConfigured { get; init; }

    public bool TenantRetentionOverridePolicyConfigured { get; init; }

    public bool CommentSensitivityPolicyConfigured { get; init; }

    public bool ExportCommentsAllowed { get; init; }

    public IReadOnlyList<string> LaunchReadinessGaps
    {
        get
        {
            List<string> gaps = [];

            if (!LegalHoldPolicyConfigured)
            {
                gaps.Add("Legal-hold policy is unresolved.");
            }

            if (!TenantRetentionOverridePolicyConfigured)
            {
                gaps.Add("Tenant-specific retention overrides are unresolved.");
            }

            if (!CommentSensitivityPolicyConfigured)
            {
                gaps.Add("Comment sensitivity policy is unresolved.");
            }

            return gaps;
        }
    }

    public TimesheetsEvidencePolicyDescriptor EffectivePolicy => new(
        TimesheetsEvidencePolicyDescriptor.DefaultRetentionRules(),
        TimesheetsEvidencePolicyDescriptor.DefaultCommentRules(
            ExportCommentsAllowed
                ? TimesheetsCommentPolicyDecision.Allowed
                : TimesheetsCommentPolicyDecision.Excluded),
        LaunchReadinessGaps);
}
