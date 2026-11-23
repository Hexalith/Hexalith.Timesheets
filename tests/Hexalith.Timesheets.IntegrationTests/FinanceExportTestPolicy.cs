using Hexalith.Timesheets.Server.Policies;

namespace Hexalith.Timesheets.IntegrationTests;

internal static class FinanceExportTestPolicy
{
    internal static TimesheetsEvidencePolicyOptions Configured { get; } = new()
    {
        LegalHoldPolicyConfigured = true,
        TenantRetentionOverridePolicyConfigured = true,
        CommentSensitivityPolicyConfigured = true,
        ProjectionCommentsAllowed = true,
        ExportCommentsAllowed = true,
        ExportCommentsAllowedByTenant = new Dictionary<string, bool>(StringComparer.Ordinal) { ["tenant-1"] = true }
    };
}
