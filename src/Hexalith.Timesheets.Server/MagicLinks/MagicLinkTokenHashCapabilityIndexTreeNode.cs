namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>One immutable compressed-trie node; an optional row terminates its path.</summary>
public sealed record MagicLinkTokenHashCapabilityIndexTreeNode(
    string Prefix,
    IReadOnlyDictionary<char, MagicLinkTokenHashCapabilityIndexNodeReference> Children,
    MagicLinkTokenHashCapabilityIndexDirectoryEntry? Row)
{
    /// <summary>Tenant-only leaf payload; exactly one of Row and Roster may be set.</summary>
    public MagicLinkTokenHashCapabilityIndexTenantRoster? Roster { get; init; }
}
