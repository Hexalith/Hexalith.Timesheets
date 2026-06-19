using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Server.ApprovalAuthority;

public interface IApprovalAuthoritySourceProvider
{
    ApprovalAuthoritySource Source { get; }

    int Precedence { get; }

    ValueTask<ApprovalAuthoritySourceResult> EvaluateAsync(
        ApprovalAuthorityResolutionRequest request,
        CancellationToken cancellationToken);
}
