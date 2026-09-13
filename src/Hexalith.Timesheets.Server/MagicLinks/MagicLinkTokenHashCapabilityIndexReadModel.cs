namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>Rebuildable token-hash candidates used to locate authoritative capability streams.</summary>
public sealed record MagicLinkTokenHashCapabilityIndexReadModel(
    IReadOnlyDictionary<string, MagicLinkTokenHashCapabilityIndexEntry> Entries);
