using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Server.MagicLinks;

public sealed record MagicLinkTokenHashCapabilityIndexReadModel(
    IReadOnlyDictionary<string, MagicLinkTokenHashCapabilityIndexEntry> Entries);

public sealed record MagicLinkTokenHashCapabilityIndexEntry(
    TenantReference Tenant,
    MagicLinkCapabilityId CapabilityId);
