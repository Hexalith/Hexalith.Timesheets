using Hexalith.EventStore.Client.Projections;
using Hexalith.Timesheets.Contracts;
using Hexalith.Timesheets.Contracts.Models.ActivityTypes;

namespace Hexalith.Timesheets.ReadModels.ActivityTypes;

/// <summary>Names the SDK tenant-scoped fence for the canonical catalog's logical address.</summary>
public static class TenantActivityTypeCatalogEpoch
{
    /// <summary>The sole admitted catalog writer.</summary>
    public const string OrdinaryWriterId = "tenant-activity-type-catalog-writer";

    /// <summary>Creates a tenant-specific catalog scope, preserving the canonical projection family.</summary>
    /// <param name="tenantId">The owning tenant identifier.</param>
    /// <returns>The SDK epoch scope.</returns>
    public static SharedProjectionScope CreateScope(string tenantId)
        => new(TenantActivityTypeCatalogReadModelAddress.StateStoreName, tenantId, TimesheetsDomain.Name,
            TenantActivityTypeCatalogReadModelAddress.ProjectionName, [OrdinaryWriterId]);
}
