using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.Models.ActivityTypes;

namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>Defines the persisted tenant catalog address shared by its writer and loader.</summary>
public static class MagicLinkActivityTypeCatalogReadModelAddress
{
    /// <summary>The Dapr state-store component containing the catalog.</summary>
    public const string StateStoreName = TenantActivityTypeCatalogReadModelAddress.StateStoreName;

    /// <summary>The logical read-model slot.</summary>
    public const string SlotName = TenantActivityTypeCatalogReadModelAddress.SlotName;

    /// <summary>Creates the tenant-addressed catalog key.</summary>
    /// <param name="tenant">The owning tenant.</param>
    /// <returns>The persisted read-model key.</returns>
    public static string StateKey(TenantReference tenant)
        => TenantActivityTypeCatalogReadModelAddress.StateKey(tenant);
}
