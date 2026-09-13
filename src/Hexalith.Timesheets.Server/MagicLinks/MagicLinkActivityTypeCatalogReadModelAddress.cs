using Hexalith.Timesheets.Contracts.References;

namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>Defines the persisted tenant catalog address shared by its writer and loader.</summary>
public static class MagicLinkActivityTypeCatalogReadModelAddress
{
    /// <summary>The Dapr state-store component containing the catalog.</summary>
    public const string StateStoreName = "statestore";

    /// <summary>The logical read-model slot.</summary>
    public const string SlotName = "catalog";

    /// <summary>Creates the tenant-addressed catalog key.</summary>
    /// <param name="tenant">The owning tenant.</param>
    /// <returns>The persisted read-model key.</returns>
    public static string StateKey(TenantReference tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        return $"timesheets:magic-links:activity-type-catalog:{tenant.TenantId}:v1";
    }
}
