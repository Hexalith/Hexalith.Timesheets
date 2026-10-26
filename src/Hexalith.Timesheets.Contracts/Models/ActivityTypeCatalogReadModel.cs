namespace Hexalith.Timesheets.Contracts.Models;

public sealed record ActivityTypeCatalogReadModel(
    IReadOnlyList<ActivityTypeCatalogItem> Items,
    ProjectionFreshnessMetadata ProjectionFreshness)
{
    /// <summary>Gets event-derived Project policy evidence; legacy catalogs have no such evidence.</summary>
    public TenantActivityTypeCatalogPolicySnapshot? ProjectPolicy { get; init; }
}
