namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>One bounded page of a tenant's canonical index rows.</summary>
public sealed record MagicLinkTokenHashCapabilityIndexTenantRosterPage(
    IReadOnlyList<MagicLinkTokenHashCapabilityIndexDirectoryEntry> Rows);
