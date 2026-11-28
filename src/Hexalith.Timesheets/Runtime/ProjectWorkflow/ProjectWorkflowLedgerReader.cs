using Hexalith.EventStore.Client.Projections;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.ActivityTypes;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.ReadModels.ActivityTypes;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Runtime.ProjectWorkflow;

/// <summary>Requires current scope and catalog evidence before reading the enrolled Project's ledger.</summary>
internal sealed class ProjectWorkflowLedgerReader(
    ProjectWorkflowOptions options, ITimesheetsAccessGuard guard,
    SharedProjectionEpochCoordinator coordinator, ProjectionBackedApprovedTimeLedgerReader reader) :
    IApprovedTimeLedgerProjectionReader
{
    /// <inheritdoc />
    public ValueTask<bool> IsDeliveryAvailableAsync(TimesheetsRequestContext context, CancellationToken cancellationToken)
        => options.IsConfigured && context.Tenant?.TenantId == options.TenantId
            ? reader.IsDeliveryAvailableAsync(context, cancellationToken)
            : ValueTask.FromResult(false);

    /// <inheritdoc />
    public async ValueTask<ApprovedTimeLedgerReadModel?> QueryAsync(
        TimesheetsRequestContext context, QueryApprovedTimeLedger query, CancellationToken cancellationToken)
    {
        if (!context.IsProjectWorkflow || !options.IsConfigured || context.Tenant is not { } tenant
            || tenant.TenantId != options.TenantId || query.Work is not null
            || query.Project is not null && query.Project.ProjectId != options.ProjectId)
        {
            return null;
        }

        try
        {
            var project = new ProjectReference(options.ProjectId!);
            if (!(await guard.AuthorizeAsync(new(context, TimesheetsOperation.ProjectionRead)
            {
                Project = project, Contributor = query.Contributor
            }, cancellationToken).ConfigureAwait(false)).IsAuthorized)
            {
                return null;
            }

            SharedProjectionReadResult<ActivityTypeCatalogReadModel> catalog = await coordinator.ReadAsync<ActivityTypeCatalogReadModel>(
                TenantActivityTypeCatalogEpoch.CreateScope(tenant.TenantId), TenantActivityTypeCatalogReadModelAddress.StateKey(tenant),
                cancellationToken).ConfigureAwait(false);
            if (catalog is not { IsAvailable: true, IsStale: false, IsDegraded: false,
                Value: { ProjectionFreshness.State: ProjectionFreshnessState.Fresh, ProjectPolicy: { IsComplete: true } policy,
                    Items: { } items } }
                || !policy.HasValidShapeForTenant(tenant)
                || items.Any(item => item?.ActivityTypeId is null || item.Scope != ActivityTypeScope.Tenant || item.Project is not null)
                || items.Select(item => item.ActivityTypeId).Distinct().Count() != items.Count
                || policy.Restrictions.Count(item => item.Project == project) > 1)
            {
                return null;
            }

            PartyReference? contributor = options.ApproverPartyIds.Contains(context.Actor?.PartyId, StringComparer.Ordinal)
                ? query.Contributor : context.Actor;
            return await reader.QueryAsync(context, query with { Project = project, Contributor = contributor }, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}
