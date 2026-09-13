using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>A non-authoritative candidate for one hashed magic-link token.</summary>
/// <param name="Tenant">The tenant that owns the capability stream.</param>
/// <param name="CapabilityId">The aggregate identifier of the capability stream.</param>
public sealed record MagicLinkTokenHashCapabilityIndexEntry(
    TenantReference Tenant,
    MagicLinkCapabilityId CapabilityId);
