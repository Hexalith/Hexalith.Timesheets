using Hexalith.Timesheets.Contracts.References;

namespace Hexalith.Timesheets.Contracts.Models.ActivityTypes;

/// <summary>Canonical persisted address of the tenant Activity Type catalog.</summary>
public static class TenantActivityTypeCatalogReadModelAddress
{
    /// <summary>The Dapr state-store component containing the catalog.</summary>
    public const string StateStoreName = "timesheets-readmodels";

    /// <summary>The logical read-model slot.</summary>
    public const string SlotName = "catalog";

    /// <summary>The canonical named projection route.</summary>
    public const string ProjectionName = "tenant-activity-type-catalog";

    /// <summary>Gets the legacy physical key retained as a persisted compatibility contract.</summary>
    /// <param name="tenant">The owning tenant.</param>
    /// <returns>The unchanged v1 catalog key.</returns>
    public static string StateKey(TenantReference tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        return $"timesheets:magic-links:activity-type-catalog:{tenant.TenantId}:v1";
    }
}
