namespace Hexalith.Timesheets.Contracts.Models;

public sealed record ActivityTypeCatalogReadModel(
    IReadOnlyList<ActivityTypeCatalogItem> Items,
    ProjectionFreshnessMetadata ProjectionFreshness);
