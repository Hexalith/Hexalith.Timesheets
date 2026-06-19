namespace Hexalith.Timesheets.Server.ApprovalAuthority;

public interface ITimesheetsApprovalAuthorityResolver
{
    ValueTask<ApprovalAuthorityResolutionResult> ResolveAsync(
        ApprovalAuthorityResolutionRequest request,
        CancellationToken cancellationToken);
}
