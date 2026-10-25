namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>Rebuildable token-hash candidates used to locate authoritative capability streams.</summary>
public sealed record MagicLinkTokenHashCapabilityIndexReadModel(
    IReadOnlyDictionary<string, MagicLinkTokenHashCapabilityIndexEntry> Entries)
{
    /// <summary>Selected storage format; older shards are not trusted for candidate lookup.</summary>
    public int StorageVersion { get; init; }

    /// <summary>Bounded numeric pages holding hashes beyond the inline directory.</summary>
    public IReadOnlyList<MagicLinkTokenHashCapabilityIndexDirectoryReference> DirectoryPages { get; init; }
        = [];

    /// <summary>All authoritative owner contributions, including hashes with ambiguous owners.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<MagicLinkTokenHashCapabilityIndexEntry>> Contributions { get; init; }
        = new Dictionary<string, IReadOnlyList<MagicLinkTokenHashCapabilityIndexEntry>>(StringComparer.Ordinal);

    /// <summary>Marks an inherited unique-only row whose omitted collisions cannot be reconstructed.</summary>
    public bool LegacyContributionsUnverified { get; init; }

    /// <summary>Number of selected bounded candidate pages for each hash in this shard.</summary>
    public IReadOnlyDictionary<string, int> CandidatePageCounts { get; init; }
        = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Allocated shard-local numeric page IDs for each hash; never derived from the hash.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<int>> CandidatePageIds { get; init; }
        = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);

    /// <summary>Content digests binding every selected collision page to this manifest.</summary>
    public IReadOnlyDictionary<int, string> CandidatePageDigests { get; init; }
        = new Dictionary<int, string>();

    /// <summary>Content digests binding every selected directory page to this manifest.</summary>
    public IReadOnlyDictionary<int, string> DirectoryPageDigests { get; init; }
        = new Dictionary<int, string>();

    /// <summary>The next shard-local numeric page ID for ordinary delivery.</summary>
    public int NextCandidatePageId { get; init; }
}
