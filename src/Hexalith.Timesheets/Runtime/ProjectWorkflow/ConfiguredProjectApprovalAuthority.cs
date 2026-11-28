using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ApprovalAuthority;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Runtime.ProjectWorkflow;

/// <summary>Resolves approval and correction authority only for the enrolled Project.</summary>
internal sealed class ConfiguredProjectApprovalAuthority(ProjectWorkflowOptions options) : IApprovalAuthoritySourceProvider
{
    /// <inheritdoc />
    public ApprovalAuthoritySource Source => ApprovalAuthoritySource.ProjectApprover;

    /// <inheritdoc />
    public int Precedence => TimesheetsApprovalAuthorityPolicyOptions.DefaultPrecedence(Source);

    /// <inheritdoc />
    public ValueTask<ApprovalAuthoritySourceResult> EvaluateAsync(
        ApprovalAuthorityResolutionRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TimesheetsAuthorizationRequest authorization = request.AuthorizationRequest;
        if (!options.IsConfigured || !authorization.Context.IsProjectWorkflow
            || authorization.Context.Tenant?.TenantId != options.TenantId
            || authorization.Project?.ProjectId != options.ProjectId)
        {
            return ValueTask.FromResult(ApprovalAuthoritySourceResult.Unavailable(Source));
        }

        return ValueTask.FromResult(options.ApproverPartyIds.Contains(authorization.Context.Actor?.PartyId, StringComparer.Ordinal)
            && request.Action is ApprovalAuthorityAction.EntryApproval or ApprovalAuthorityAction.CorrectionAuthorization
            ? ApprovalAuthoritySourceResult.Allowed(Source, ProjectionFreshnessMetadata.Fresh)
            : ApprovalAuthoritySourceResult.Denied(Source, ApprovalAuthorityDecisionState.Denied,
                TimesheetsDenialCategory.InsufficientRole, "The workflow scope is unavailable.", ProjectionFreshnessMetadata.Fresh));
    }
}
