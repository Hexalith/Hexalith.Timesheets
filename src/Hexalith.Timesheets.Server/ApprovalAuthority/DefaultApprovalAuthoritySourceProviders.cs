using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Server.ApprovalAuthority;

public sealed class DefaultProjectApprovalAuthoritySourceProvider : IApprovalAuthoritySourceProvider
{
    public ApprovalAuthoritySource Source => ApprovalAuthoritySource.ProjectApprover;

    public int Precedence => TimesheetsApprovalAuthorityPolicyOptions.DefaultPrecedence(Source);

    public ValueTask<ApprovalAuthoritySourceResult> EvaluateAsync(
        ApprovalAuthorityResolutionRequest request,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(ApprovalAuthoritySourceResult.Unavailable(Source));
    }
}

public sealed class DefaultWorkApprovalAuthoritySourceProvider : IApprovalAuthoritySourceProvider
{
    public ApprovalAuthoritySource Source => ApprovalAuthoritySource.WorkOwner;

    public int Precedence => TimesheetsApprovalAuthorityPolicyOptions.DefaultPrecedence(Source);

    public ValueTask<ApprovalAuthoritySourceResult> EvaluateAsync(
        ApprovalAuthorityResolutionRequest request,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(ApprovalAuthoritySourceResult.Unavailable(Source));
    }
}

public sealed class DefaultTenantApprovalAuthoritySourceProvider : IApprovalAuthoritySourceProvider
{
    public ApprovalAuthoritySource Source => ApprovalAuthoritySource.TenantAdministrator;

    public int Precedence => TimesheetsApprovalAuthorityPolicyOptions.DefaultPrecedence(Source);

    public ValueTask<ApprovalAuthoritySourceResult> EvaluateAsync(
        ApprovalAuthorityResolutionRequest request,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(ApprovalAuthoritySourceResult.Unavailable(Source));
    }
}

public sealed class DefaultFinanceApprovalAuthoritySourceProvider : IApprovalAuthoritySourceProvider
{
    public ApprovalAuthoritySource Source => ApprovalAuthoritySource.FinanceReviewer;

    public int Precedence => TimesheetsApprovalAuthorityPolicyOptions.DefaultPrecedence(Source);

    public ValueTask<ApprovalAuthoritySourceResult> EvaluateAsync(
        ApprovalAuthorityResolutionRequest request,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(ApprovalAuthoritySourceResult.Unavailable(Source));
    }
}
