using Hexalith.Parties.Contracts.Models;
using Hexalith.Projects.Contracts.Queries;
using Hexalith.Projects.Contracts.Ui;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Contracts.Queries;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.References;

namespace Hexalith.Timesheets.Runtime.ProjectWorkflow;

/// <summary>Admits only the enrolled tenant, a current member and active Parties and Project.</summary>
internal sealed class GatewayProjectWorkflowReferences(ProjectWorkflowOptions options, ProjectWorkflowSiblingQueries queries) :
    ITimesheetsTenantAccessValidator, IContributorPartyValidator, IProjectReferenceValidator
{
    private const string Refusal = "The workflow scope is unavailable.";

    /// <inheritdoc />
    public async ValueTask<TimesheetsTenantAccessResult> ValidateAsync(
        TimesheetsRequestContext context, TimesheetsOperation operation, CancellationToken cancellationToken)
    {
        if (!InScope(context))
        {
            return TimesheetsTenantAccessResult.Denied(TimesheetsTenantAccessState.NonMember, Refusal);
        }

        string tenant = context.Tenant!.TenantId;
        string actor = context.Actor!.PartyId;
        UserTenantMembership? membership = await queries.ReadTenantMembershipAsync(tenant, actor, cancellationToken)
            .ConfigureAwait(false);
        if (membership is not { Status: TenantStatus.Active,
                Role: TenantRole.TenantOwner or TenantRole.TenantContributor or TenantRole.TenantReader }
            || operation == TimesheetsOperation.Command && membership.Role == TenantRole.TenantReader)
        {
            return TimesheetsTenantAccessResult.Denied(TimesheetsTenantAccessState.NonMember, Refusal);
        }

        ReferenceValidationResult party = await ValidateAsync(context, context.Actor, cancellationToken).ConfigureAwait(false);
        return party.IsValid ? TimesheetsTenantAccessResult.Authorized()
            : TimesheetsTenantAccessResult.Denied(TimesheetsTenantAccessState.UnavailableSiblingAuthority, Refusal);
    }

    /// <inheritdoc />
    public async ValueTask<ReferenceValidationResult> ValidateAsync(
        TimesheetsRequestContext context, PartyReference contributor, CancellationToken cancellationToken)
    {
        if (!InScope(context) || string.IsNullOrWhiteSpace(contributor.PartyId))
        {
            return ReferenceValidationResult.Denied(ReferenceValidationState.TenantMismatch, Refusal);
        }

        PartyDetail? found = await queries.ReadPartyAsync(context.Tenant!.TenantId, contributor.PartyId,
            cancellationToken).ConfigureAwait(false);
        return found is { IsActive: true, IsRestricted: false, IsErased: false, Freshness.Status: ProjectionFreshnessStatus.Current }
            && found.Id == contributor.PartyId
            ? ReferenceValidationResult.Valid()
            : ReferenceValidationResult.Denied(ReferenceValidationState.Unavailable, Refusal);
    }

    /// <inheritdoc />
    public async ValueTask<ReferenceValidationResult> ValidateAsync(
        TimesheetsRequestContext context, ProjectReference project, CancellationToken cancellationToken)
    {
        if (!InScope(context) || project.ProjectId != options.ProjectId)
        {
            return ReferenceValidationResult.Denied(ReferenceValidationState.TenantMismatch, Refusal);
        }

        GetProjectResponse? found = await queries.ReadProjectAsync(context.Tenant!.TenantId, project.ProjectId,
            cancellationToken).ConfigureAwait(false);
        return found is { LifecycleState: ProjectLifecycle.Active, Snapshot: { ProjectVersion: > 0,
            ResponseState: AdmissionResponseState.Complete or AdmissionResponseState.Partial } }
            && found.ProjectId == project.ProjectId
            ? ReferenceValidationResult.Valid()
            : ReferenceValidationResult.Denied(ReferenceValidationState.Unavailable, Refusal);
    }

    private bool InScope(TimesheetsRequestContext context) => context.IsProjectWorkflow && options.IsConfigured
        && context.Tenant?.TenantId == options.TenantId && !string.IsNullOrWhiteSpace(context.Actor?.PartyId);
}
