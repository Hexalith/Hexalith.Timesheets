using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models.MagicLinks;

/// <summary>Locates one tenant-scoped capability in the generation-selected shared view.</summary>
public static class MagicLinkCapabilityReadModelAddress
{
    /// <summary>The configured read-model store.</summary>
    public const string StateStoreName = "timesheets-readmodels";

    /// <summary>The generation-selected capability slot.</summary>
    public const string SlotName = "capability";

    /// <summary>Creates a tenant-and-capability logical key without token-derived material.</summary>
    public static string StateKey(string tenantId, MagicLinkCapabilityId capabilityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(capabilityId);
        if (tenantId.Contains(':', StringComparison.Ordinal)
            || capabilityId.Value.Contains(':', StringComparison.Ordinal))
        {
            throw new ArgumentException("Read-model identifiers cannot contain a reserved separator.");
        }

        return $"capability:{tenantId.ToLowerInvariant()}:{capabilityId.Value}";
    }
}
