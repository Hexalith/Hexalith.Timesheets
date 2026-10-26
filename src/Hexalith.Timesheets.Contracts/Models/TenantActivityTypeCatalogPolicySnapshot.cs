using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

/// <summary>Identifies the tenant and coverage of the catalog's event-derived Project policy.</summary>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="IsComplete">Whether a complete tenant rebuild established policy coverage.</param>
/// <param name="Restrictions">The latest contribution per source and Project, retaining ambiguous owners.</param>
public sealed record TenantActivityTypeCatalogPolicySnapshot(
    TenantReference Tenant,
    bool IsComplete,
    IReadOnlyList<ProjectActivityTypeCatalogRestrictionSnapshot> Restrictions)
{
    /// <summary>Checks the stored evidence shape without certifying full tenant coverage.</summary>
    /// <param name="tenant">The independently resolved owning tenant.</param>
    /// <returns>Whether all contributions have valid identifiers and distinct source/Project pairs.</returns>
    public bool HasValidShapeForTenant(TenantReference? tenant)
        => tenant is not null && Tenant == tenant && Restrictions is not null
            && Restrictions.All(static item => item is not null
                && !string.IsNullOrWhiteSpace(item.SourceAggregateId)
                && item.Project is not null && !string.IsNullOrWhiteSpace(item.Project.ProjectId)
                && ValidIds(item.AllowedTenantActivityTypeIds) && ValidIds(item.AllowedProjectActivityTypeIds))
            && Restrictions.GroupBy(static item => (item.SourceAggregateId, item.Project.ProjectId))
                .All(static group => group.Count() == 1);

    private static bool ValidIds(IReadOnlyList<ActivityTypeId>? ids)
        => ids is not null && ids.All(static id => id is not null && !string.IsNullOrWhiteSpace(id.Value))
            && ids.Distinct().Count() == ids.Count;
}
