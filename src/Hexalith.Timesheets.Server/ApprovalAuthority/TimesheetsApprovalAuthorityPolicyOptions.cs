using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Server.ApprovalAuthority;

public sealed record TimesheetsApprovalAuthorityPolicyOptions
{
    public const string DefaultPolicyKey = "timesheets.approval-authority.v1";

    public const string DefaultPolicyVersion = "v1";

    public static TimesheetsApprovalAuthorityPolicyOptions Default { get; } = new();

    public string PolicyKey { get; init; } = DefaultPolicyKey;

    public string PolicyVersion { get; init; } = DefaultPolicyVersion;

    public IReadOnlySet<ApprovalAuthorityAction> SelfApprovalAllowedActions { get; init; } =
        new HashSet<ApprovalAuthorityAction>();

    public static int DefaultPrecedence(ApprovalAuthoritySource source)
    {
        return source switch
        {
            ApprovalAuthoritySource.SelfApprovalPolicy => 10,
            ApprovalAuthoritySource.ProjectApprover => 20,
            ApprovalAuthoritySource.WorkOwner => 20,
            ApprovalAuthoritySource.TenantAdministrator => 30,
            ApprovalAuthoritySource.FinanceReviewer => 40,
            ApprovalAuthoritySource.DefaultDeny => int.MaxValue,
            _ => int.MaxValue
        };
    }
}
