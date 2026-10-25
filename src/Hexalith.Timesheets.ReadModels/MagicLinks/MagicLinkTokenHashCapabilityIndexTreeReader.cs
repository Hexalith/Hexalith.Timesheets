using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;

namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>Reads content-bound numeric index nodes from an explicitly pinned generation.</summary>
public sealed class MagicLinkTokenHashCapabilityIndexTreeReader(
    SharedProjectionEpochCoordinator coordinator,
    SharedProjectionScope scope)
{
    private const string Prefix = "timesheets:magic-links:token-hash-capability-index:v7:";

    /// <summary>Selected root key; the SDK's generation selection is its only visibility point.</summary>
    public const string RootKey = Prefix + "root";

    /// <summary>Selected format witness staged with the v7 root.</summary>
    public const string FormatKey = Prefix + "format";

    /// <summary>Physical node IDs are allocator-assigned numbers, independent of token data.</summary>
    public static string NodeKey(long nodeId) => Prefix + "node:" + nodeId.ToString("D16",
        System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Physical roster-page IDs share the numeric allocator with trie nodes.</summary>
    public static string RosterPageKey(long nodeId) => Prefix + "roster-page:" + nodeId.ToString("D16",
        System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Encodes the full sortable contribution identity, with no token in its storage key.</summary>
    public static string TokenPath(string hash, MagicLinkTokenHashCapabilityIndexEntry owner)
        => hash + "!" + Encode(owner.Tenant.TenantId) + "!" + Encode(owner.CapabilityId.Value);

    /// <summary>Encodes one tenant identity for the independent contribution-roster trie.</summary>
    public static string TenantPath(string tenantId) => Encode(tenantId);

    /// <summary>Owner admission is indexed independently of the token hash.</summary>
    public static string OwnerPath(string tenantId, string capabilityId)
        => Encode(tenantId) + "!" + Encode(capabilityId);

    /// <summary>Short ordered hint; equal prefixes are resolved against physical page rows.</summary>
    public static string RosterPathPrefix(string path) => path[..Math.Min(path.Length, 32)];

    /// <summary>Finds one exact owner admission without reading its tenant's roster pages.</summary>
    public async Task<MagicLinkTokenHashCapabilityIndexDirectoryEntry?> FindOwnerAsync(
        MagicLinkTokenHashCapabilityIndexNodeReference? root, string tenantId, string capabilityId,
        CancellationToken cancellationToken = default)
    {
        MagicLinkTokenHashCapabilityIndexTreeNode? leaf = await FindNodeAsync(root,
            OwnerPath(tenantId, capabilityId), 0, cancellationToken).ConfigureAwait(false);
        if (leaf is null || leaf.Row is null && leaf.Roster is null) return null;
        MagicLinkTokenHashCapabilityIndexDirectoryEntry row = leaf.Row
            ?? throw new SharedProjectionReadUnavailableException(
                new InvalidOperationException("A selected owner admission is invalid."));
        if (row.Candidate?.Tenant?.TenantId != tenantId
            || row.Candidate.CapabilityId?.Value != capabilityId
            || string.IsNullOrWhiteSpace(row.TokenHash))
            throw new SharedProjectionReadUnavailableException(
                new InvalidOperationException("A selected owner admission has mismatched identity."));
        return row;
    }

    /// <summary>Returns up to two candidates; a second candidate proves global ambiguity.</summary>
    public async Task<IReadOnlyList<MagicLinkTokenHashCapabilityIndexEntry>> FindCandidatesAsync(
        MagicLinkTokenHashCapabilityIndexNodeReference? root, string hash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        var found = new List<MagicLinkTokenHashCapabilityIndexEntry>(2);
        await VisitPrefixAsync(root, hash + "!", 0, string.Empty, hash, found,
            new HashSet<(long Generation, long NodeId)>(), cancellationToken).ConfigureAwait(false);
        return found;
    }

    /// <summary>Verifies that a roster row also exists at its exact token-trie path.</summary>
    public async Task<bool> ContainsExactRowAsync(
        MagicLinkTokenHashCapabilityIndexNodeReference? root,
        MagicLinkTokenHashCapabilityIndexDirectoryEntry row,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        string path = TokenPath(row.TokenHash, row.Candidate);
        MagicLinkTokenHashCapabilityIndexTreeNode? leaf = await FindNodeAsync(root, path, 0,
            cancellationToken).ConfigureAwait(false);
        return leaf?.Row == row;
    }

    /// <summary>Loads and checks one tenant's complete bounded contribution roster.</summary>
    public async Task<IReadOnlyList<MagicLinkTokenHashCapabilityIndexDirectoryEntry>?> ReadRosterAsync(
        MagicLinkTokenHashCapabilityIndexNodeReference? root, string tenantId,
        CancellationToken cancellationToken = default)
    {
        MagicLinkTokenHashCapabilityIndexTenantRoster? selected = await FindRosterAsync(root,
            tenantId, cancellationToken).ConfigureAwait(false);
        if (selected is null) return null;
        MagicLinkTokenHashCapabilityIndexTenantRoster roster = selected;
        ValidateRosterManifest(roster, tenantId);
        var rows = new List<MagicLinkTokenHashCapabilityIndexDirectoryEntry>(roster.ContributionCount);
        string? previousPath = null;
        foreach (MagicLinkTokenHashCapabilityIndexTenantRosterPageReference pageRef in roster.Pages)
        {
            MagicLinkTokenHashCapabilityIndexTenantRosterPage page = await ReadRosterPageAsync(pageRef,
                cancellationToken).ConfigureAwait(false);
            foreach (MagicLinkTokenHashCapabilityIndexDirectoryEntry row in page.Rows)
            {
                string path = TokenPath(row.TokenHash, row.Candidate);
                if (previousPath is not null && string.CompareOrdinal(previousPath, path) >= 0)
                    throw new SharedProjectionReadUnavailableException(
                        new InvalidOperationException("A selected tenant roster is not ordered."));
                previousPath = path;
            }
            rows.AddRange(page.Rows);
        }
        if (rows.Count != roster.ContributionCount
            || rows.Any(row => row?.Candidate?.Tenant?.TenantId != tenantId
                || string.IsNullOrWhiteSpace(row.TokenHash)
                || row.Candidate.CapabilityId is null)
            || rows.Select(row => row.Candidate.CapabilityId.Value).Distinct(StringComparer.Ordinal).Count()
                != rows.Count)
            throw new SharedProjectionReadUnavailableException(
                new InvalidOperationException("A selected tenant roster is inconsistent."));
        return rows;
    }

    /// <summary>Reads one digest-bound page and verifies its ordered range hint.</summary>
    public async Task<MagicLinkTokenHashCapabilityIndexTenantRosterPage> ReadRosterPageAsync(
        MagicLinkTokenHashCapabilityIndexTenantRosterPageReference reference,
        CancellationToken cancellationToken = default)
    {
        if (reference?.Page is null || reference.FirstPathPrefix is null)
            throw new SharedProjectionReadUnavailableException(
                new InvalidOperationException("A selected tenant roster page reference is invalid."));
        ReadModelEntry<MagicLinkTokenHashCapabilityIndexTenantRosterPage> entry = await coordinator
            .ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexTenantRosterPage>(scope,
                reference.Page.Generation, RosterPageKey(reference.Page.NodeId), cancellationToken)
            .ConfigureAwait(false);
        MagicLinkTokenHashCapabilityIndexTenantRosterPage page = entry.Value
            ?? throw new SharedProjectionReadUnavailableException(
                new InvalidOperationException("A selected tenant roster page is missing."));
        if (page.Rows is null || page.Rows.Count is < 1 or > 64
            || !string.Equals(reference.Page.Digest, RosterPageDigest(page), StringComparison.Ordinal)
            || page.Rows.Any(row => row?.Candidate?.Tenant is null
                || row.Candidate.CapabilityId is null || string.IsNullOrWhiteSpace(row.TokenHash))
            || !string.Equals(reference.FirstPathPrefix,
                RosterPathPrefix(TokenPath(page.Rows[0].TokenHash, page.Rows[0].Candidate)),
                StringComparison.Ordinal))
            throw new SharedProjectionReadUnavailableException(
                new InvalidOperationException("A selected tenant roster page is invalid."));
        string? previous = null;
        foreach (MagicLinkTokenHashCapabilityIndexDirectoryEntry row in page.Rows)
        {
            string path = TokenPath(row.TokenHash, row.Candidate);
            if (previous is not null && string.CompareOrdinal(previous, path) >= 0)
                throw new SharedProjectionReadUnavailableException(
                    new InvalidOperationException("A selected tenant roster page is not ordered."));
            previous = path;
        }
        return page;
    }

    /// <summary>Checks bounded, sorted page hints before an incremental update.</summary>
    public static void ValidateRosterManifest(MagicLinkTokenHashCapabilityIndexTenantRoster roster,
        string tenantId)
    {
        if (!string.Equals(roster.TenantId, tenantId, StringComparison.Ordinal)
            || roster.ContributionCount is < 0 or > 10_000 || roster.Pages is null
            || roster.Pages.Count > (roster.ContributionCount + 31) / 32
            || roster.Pages.Count == 0 && roster.ContributionCount != 0
            || roster.Pages.Count != 0 && roster.ContributionCount == 0
            || roster.Pages.Any(page => page?.Page is null
                || page.FirstPathPrefix is null or { Length: > 32 }
                || page.Page.Generation < 0 || page.Page.NodeId < 0
                || page.Page.Digest is not { Length: 64 })
            || roster.Pages.Zip(roster.Pages.Skip(1)).Any(pair =>
                string.CompareOrdinal(pair.First.FirstPathPrefix,
                    pair.Second.FirstPathPrefix) > 0))
            throw new SharedProjectionReadUnavailableException(
                new InvalidOperationException("A selected tenant roster manifest is invalid."));
    }

    /// <summary>Finds the selected manifest before reading its bounded pages.</summary>
    public async Task<MagicLinkTokenHashCapabilityIndexTenantRoster?> FindRosterAsync(
        MagicLinkTokenHashCapabilityIndexNodeReference? root, string tenantId,
        CancellationToken cancellationToken = default)
    {
        MagicLinkTokenHashCapabilityIndexTreeNode? leaf = await FindNodeAsync(root,
            TenantPath(tenantId), 0, cancellationToken).ConfigureAwait(false);
        if (leaf is null) return null;
        if (leaf.Roster is null && leaf.Row is null) return null;
        MagicLinkTokenHashCapabilityIndexTenantRoster roster = leaf.Roster
            ?? throw new SharedProjectionReadUnavailableException(
                new InvalidOperationException("A selected tenant roster is invalid."));
        if (!string.Equals(roster.TenantId, tenantId, StringComparison.Ordinal))
            throw new SharedProjectionReadUnavailableException(
                new InvalidOperationException("A selected tenant roster has a mismatched owner."));
        ValidateRosterManifest(roster, tenantId);
        return roster;
    }

    /// <summary>Loads one referenced immutable node and verifies its content binding.</summary>
    public async Task<MagicLinkTokenHashCapabilityIndexTreeNode> ReadNodeAsync(
        MagicLinkTokenHashCapabilityIndexNodeReference reference,
        CancellationToken cancellationToken = default)
    {
        if (reference.Generation < 0 || reference.NodeId < 0 || reference.Digest is not { Length: 64 })
            throw new SharedProjectionReadUnavailableException(
                new InvalidOperationException("A selected index node reference is invalid."));
        ReadModelEntry<MagicLinkTokenHashCapabilityIndexTreeNode> entry = await coordinator
            .ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexTreeNode>(scope,
                reference.Generation, NodeKey(reference.NodeId), cancellationToken).ConfigureAwait(false);
        MagicLinkTokenHashCapabilityIndexTreeNode node = entry.Value
            ?? throw new SharedProjectionReadUnavailableException(
                new InvalidOperationException("A selected index node is missing."));
        if (node.Prefix is null || node.Children is null || node.Children.Count > 256
            || node.Row is not null && node.Roster is not null
            || node.Children.Any(pair => !IsPathCharacter(pair.Key)
                || pair.Value is null || pair.Value.Generation < 0 || pair.Value.NodeId < 0
                || pair.Value.Digest is not { Length: 64 })
            || !string.Equals(reference.Digest, NodeDigest(node), StringComparison.Ordinal))
            throw new SharedProjectionReadUnavailableException(
                new InvalidOperationException("A selected index node is invalid."));
        return node;
    }

    /// <summary>Canonical digest of one immutable node.</summary>
    public static string NodeDigest(MagicLinkTokenHashCapabilityIndexTreeNode node)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            node.Prefix,
            node.Row,
            node.Roster,
            Children = node.Children.OrderBy(pair => pair.Key).Select(pair => new
            {
                Edge = pair.Key,
                pair.Value.Generation,
                pair.Value.NodeId,
                pair.Value.Digest
            }).ToArray()
        })));

    /// <summary>Canonical digest of one bounded tenant roster page.</summary>
    public static string RosterPageDigest(MagicLinkTokenHashCapabilityIndexTenantRosterPage page)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(page.Rows)));

    private static string Encode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=')
            .Replace('+', '-').Replace('/', '_');
    }

    private static bool IsPathCharacter(char value)
        => value is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9'
            or '!' or '-' or '_';

    private async Task<MagicLinkTokenHashCapabilityIndexTreeNode?> FindNodeAsync(
        MagicLinkTokenHashCapabilityIndexNodeReference? reference, string path, int offset,
        CancellationToken cancellationToken)
    {
        if (reference is null) return null;
        MagicLinkTokenHashCapabilityIndexTreeNode node = await ReadNodeAsync(reference,
            cancellationToken).ConfigureAwait(false);
        if (!path.AsSpan(offset).StartsWith(node.Prefix.AsSpan(), StringComparison.Ordinal)) return null;
        offset += node.Prefix.Length;
        if (offset == path.Length) return node;
        return node.Children.TryGetValue(path[offset], out MagicLinkTokenHashCapabilityIndexNodeReference? child)
            ? await FindNodeAsync(child, path, offset + 1, cancellationToken).ConfigureAwait(false)
            : null;
    }

    private async Task VisitPrefixAsync(MagicLinkTokenHashCapabilityIndexNodeReference? reference,
        string prefix, int offset, string walked, string hash,
        List<MagicLinkTokenHashCapabilityIndexEntry> found,
        HashSet<(long Generation, long NodeId)> visited, CancellationToken cancellationToken)
    {
        if (reference is null || found.Count >= 2) return;
        if (!visited.Add((reference.Generation, reference.NodeId)))
            throw new SharedProjectionReadUnavailableException(
                new InvalidOperationException("A selected index node is reused on one lookup path."));
        MagicLinkTokenHashCapabilityIndexTreeNode node = await ReadNodeAsync(reference,
            cancellationToken).ConfigureAwait(false);
        walked += node.Prefix;
        int common = 0;
        while (common < node.Prefix.Length && offset + common < prefix.Length
            && node.Prefix[common] == prefix[offset + common]) common++;
        if (common < node.Prefix.Length && offset + common < prefix.Length) return;
        if (offset + common < prefix.Length)
        {
            if (node.Children.TryGetValue(prefix[offset + common],
                out MagicLinkTokenHashCapabilityIndexNodeReference? child))
                await VisitPrefixAsync(child, prefix, offset + common + 1,
                    walked + prefix[offset + common], hash, found, visited,
                    cancellationToken).ConfigureAwait(false);
            return;
        }
        await CollectFirstTwoAsync(node, walked, hash, found, visited,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task CollectFirstTwoAsync(MagicLinkTokenHashCapabilityIndexTreeNode node,
        string walked, string hash, List<MagicLinkTokenHashCapabilityIndexEntry> found,
        HashSet<(long Generation, long NodeId)> visited, CancellationToken cancellationToken)
    {
        if (node.Roster is not null)
            throw new SharedProjectionReadUnavailableException(
                new InvalidOperationException("A tenant roster appeared in the token index."));
        if (node.Row is { } row)
        {
            if (row.Candidate?.Tenant is null || row.Candidate.CapabilityId is null
                || !string.Equals(row.TokenHash, hash, StringComparison.Ordinal)
                || !string.Equals(TokenPath(row.TokenHash, row.Candidate), walked,
                    StringComparison.Ordinal)
                || found.Contains(row.Candidate))
                throw new SharedProjectionReadUnavailableException(
                    new InvalidOperationException("A selected index row has mismatched identity."));
            found.Add(row.Candidate);
        }
        foreach ((char edge, MagicLinkTokenHashCapabilityIndexNodeReference child) in
            node.Children.OrderBy(pair => pair.Key))
        {
            if (found.Count >= 2) break;
            if (!visited.Add((child.Generation, child.NodeId)))
                throw new SharedProjectionReadUnavailableException(
                    new InvalidOperationException("A selected index node is reused on one lookup path."));
            MagicLinkTokenHashCapabilityIndexTreeNode next = await ReadNodeAsync(child,
                cancellationToken).ConfigureAwait(false);
            await CollectFirstTwoAsync(next, walked + edge + next.Prefix, hash, found,
                visited, cancellationToken).ConfigureAwait(false);
        }
    }
}
