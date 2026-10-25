using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.Timesheets.Contracts;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;

namespace Hexalith.Timesheets.Server.MagicLinks;

public static class MagicLinkTokenHashCapabilityIndexProjection
{
    /// <summary>The canonical named projection route.</summary>
    public const string ProjectionName = "magic-link-token-hash-capability-index";

    /// <summary>The Dapr state-store component containing the candidate index.</summary>
    public const string StateStoreName = "timesheets-readmodels";

    /// <summary>The fixed cross-tenant candidate-index key.</summary>
    public const string StateKey = "timesheets:magic-links:token-hash-capability-index:v1";

    /// <summary>The number of bounded selected index shards.</summary>
    public const int ShardCount = 256;

    /// <summary>Maximum contributions stored in one physical selected page.</summary>
    public const int CandidatesPerPage = 64;

    /// <summary>Maximum distinct hashes kept inside a selected shard root.</summary>
    public const int InlineHashLimit = 64;

    /// <summary>The selected directory format with numeric, hash-free physical page addresses.</summary>
    public const int CurrentStorageVersion = 6;

    /// <summary>The global epoch writer identity for pre-tenant token lookup.</summary>
    public const string OrdinaryWriterId = "timesheets-token-candidate-index";

    /// <summary>Gets the fixed global selection scope shared by all tenant slices.</summary>
    public static SharedProjectionScope GlobalScope()
        => new(StateStoreName, "global-index", TimesheetsDomain.Name, ProjectionName, [OrdinaryWriterId])
        {
            CopyOnWrite = true
        };

    /// <summary>Derives a stable bounded shard key without including the token hash in the key.</summary>
    public static string ShardKey(string tokenHash)
    {
        return ShardKey(ShardOrdinal(tokenHash));
    }

    /// <summary>Selects one of the fixed index shards for a token hash.</summary>
    public static int ShardOrdinal(string tokenHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        return SHA256.HashData(Encoding.UTF8.GetBytes(tokenHash))[0];
    }

    /// <summary>Gets one fixed shard key by ordinal.</summary>
    public static string ShardKey(int shard)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shard);
        if (shard >= ShardCount) throw new ArgumentOutOfRangeException(nameof(shard));
        return $"timesheets:magic-links:token-hash-capability-index:v2:shard:{shard:X2}";
    }

    /// <summary>Gets a selected page key from fixed shard and allocated numeric page ordinals.</summary>
    public static string CandidatePageKey(int shard, int page)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shard);
        if (shard >= ShardCount) throw new ArgumentOutOfRangeException(nameof(shard));
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        return $"timesheets:magic-links:token-hash-capability-index:v4:shard:{shard:X2}:page:{page:D6}";
    }

    /// <summary>Gets a numeric directory page key without embedding a token hash.</summary>
    public static string DirectoryPageKey(int shard, int page)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shard);
        if (shard >= ShardCount) throw new ArgumentOutOfRangeException(nameof(shard));
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        return $"timesheets:magic-links:token-hash-capability-index:v5:shard:{shard:X2}:page:{page:D6}";
    }

    /// <summary>Gets an owner-derived admission key without token material.</summary>
    public static string OwnerAdmissionKey(string tenantId, string capabilityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityId);
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            tenantId + "\0" + capabilityId)));
        return "timesheets:magic-links:token-hash-capability-index:v5:owner:" + digest;
    }

    /// <summary>Checks that selected page IDs are unique, bounded, and match their hash manifests.</summary>
    public static bool HasValidPageManifest(MagicLinkTokenHashCapabilityIndexReadModel current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.Entries is null || current.Contributions is null
            || current.DirectoryPages is null
            || current.StorageVersion != CurrentStorageVersion
                && (current.StorageVersion != 0 || current.DirectoryPages.Count != 0)
            || current.Entries.Count > InlineHashLimit
            || current.Contributions.Count > InlineHashLimit
            || current.CandidatePageCounts is null || current.CandidatePageIds is null
            || current.CandidatePageDigests is null || current.DirectoryPageDigests is null
            || current.NextCandidatePageId < 0
            || current.CandidatePageCounts.Count != current.CandidatePageIds.Count)
            return false;
        // Legacy unique-only shards may be retained as explicitly unverified data
        // during a tenant-slice rebuild, but they are never selected by the loader.
        if (current.StorageVersion == 0)
            return current.DirectoryPages.Count == 0 && current.CandidatePageCounts.Count == 0
                && current.CandidatePageDigests.Count == 0 && current.DirectoryPageDigests.Count == 0;
        var used = new HashSet<int>();
        string? priorLast = null;
        foreach (MagicLinkTokenHashCapabilityIndexDirectoryReference page in current.DirectoryPages)
        {
            if (page is null || page.PageId < 0 || page.PageId >= current.NextCandidatePageId
                || string.IsNullOrWhiteSpace(page.FirstHash) || string.IsNullOrWhiteSpace(page.LastHash)
                || string.CompareOrdinal(page.FirstHash, page.LastHash) > 0
                || priorLast is not null && string.CompareOrdinal(priorLast, page.FirstHash) > 0
                || !used.Add(page.PageId)
                || !current.DirectoryPageDigests.TryGetValue(page.PageId, out string? digest)
                || !IsDigest(digest)) return false;
            priorLast = page.LastHash;
        }
        if (current.Entries.Keys.Any(hash => current.DirectoryPages.Any(page =>
                string.CompareOrdinal(page.FirstHash, hash) <= 0
                && string.CompareOrdinal(hash, page.LastHash) <= 0))
            || current.Contributions.Keys.Any(hash => current.DirectoryPages.Any(page =>
                string.CompareOrdinal(page.FirstHash, hash) <= 0
                && string.CompareOrdinal(hash, page.LastHash) <= 0))
            || current.CandidatePageCounts.Keys.Any(hash => current.DirectoryPages.Any(page =>
                string.CompareOrdinal(page.FirstHash, hash) <= 0
                && string.CompareOrdinal(hash, page.LastHash) <= 0))
            || current.Contributions.Keys.Any(current.CandidatePageCounts.ContainsKey)) return false;
        foreach ((string hash, IReadOnlyList<MagicLinkTokenHashCapabilityIndexEntry> owners)
            in current.Contributions)
        {
            if (owners is null || owners.Count is < 1 or > CandidatesPerPage
                || owners.Distinct().Count() != owners.Count
                || owners.Any(static owner => owner?.Tenant is null || owner.CapabilityId is null
                    || string.IsNullOrWhiteSpace(owner.Tenant.TenantId)
                    || string.IsNullOrWhiteSpace(owner.CapabilityId.Value))) return false;
            if (owners.Count == 1)
            {
                if (!current.Entries.TryGetValue(hash, out var candidate)
                    || candidate != owners[0]) return false;
            }
            else if (current.Entries.ContainsKey(hash)) return false;
        }
        if (current.Entries.Keys.Any(hash => !current.Contributions.ContainsKey(hash))) return false;
        foreach ((string hash, int count) in current.CandidatePageCounts)
        {
            if (string.IsNullOrWhiteSpace(hash) || count <= 0 || count > 10_000
                || !current.CandidatePageIds.TryGetValue(hash, out IReadOnlyList<int>? ids)
                || ids is null || ids.Count != count)
                return false;
            foreach (int id in ids)
                if (id < 0 || id >= current.NextCandidatePageId || !used.Add(id)
                    || !current.CandidatePageDigests.TryGetValue(id, out string? digest)
                    || !IsDigest(digest)) return false;
        }
        return current.CandidatePageDigests.Count == current.CandidatePageIds.Values.Sum(ids => ids.Count)
            && current.DirectoryPageDigests.Count == current.DirectoryPages.Count;
    }

    /// <summary>Computes a stable digest of a collision page's complete content.</summary>
    public static string CandidatePageDigest(MagicLinkTokenHashCapabilityIndexPage page)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            page.TokenHash,
            Candidates = page.Candidates.Select(static item => new[]
            {
                item.Tenant.TenantId, item.CapabilityId.Value
            }).ToArray()
        })));

    /// <summary>Computes a stable digest of a directory page's complete content.</summary>
    public static string DirectoryPageDigest(MagicLinkTokenHashCapabilityIndexDirectoryPage page)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            page.Entries.Select(static item => new[]
            {
                item.TokenHash, item.Candidate.Tenant.TenantId, item.Candidate.CapabilityId.Value
            }).ToArray())));

    /// <summary>Validates one selected collision page against its root manifest.</summary>
    public static bool HasValidCandidatePage(MagicLinkTokenHashCapabilityIndexReadModel root,
        string hash, int pageId, MagicLinkTokenHashCapabilityIndexPage? page)
        => page?.Candidates is { Count: > 0 } candidates
            && candidates.Count <= CandidatesPerPage
            && string.Equals(page.TokenHash, hash, StringComparison.Ordinal)
            && candidates.All(static item => item?.Tenant is not null
                && !string.IsNullOrWhiteSpace(item.Tenant.TenantId)
                && item.CapabilityId is not null
                && !string.IsNullOrWhiteSpace(item.CapabilityId.Value))
            && candidates.Distinct().Count() == candidates.Count
            && root.CandidatePageDigests.TryGetValue(pageId, out string? digest)
            && string.Equals(digest, CandidatePageDigest(page), StringComparison.Ordinal);

    /// <summary>Validates every row and the content binding of one selected directory page.</summary>
    public static bool HasValidDirectoryPage(MagicLinkTokenHashCapabilityIndexReadModel root,
        int shard, MagicLinkTokenHashCapabilityIndexDirectoryReference reference,
        MagicLinkTokenHashCapabilityIndexDirectoryPage? page)
    {
        if (page?.Entries is not { Count: > 0 } rows || rows.Count > CandidatesPerPage
            || rows[0]?.TokenHash != reference.FirstHash || rows[^1]?.TokenHash != reference.LastHash
            || !root.DirectoryPageDigests.TryGetValue(reference.PageId, out string? digest)
            || !IsDigest(digest)) return false;
        MagicLinkTokenHashCapabilityIndexDirectoryEntry? previous = null;
        foreach (MagicLinkTokenHashCapabilityIndexDirectoryEntry row in rows)
        {
            if (row is null || string.IsNullOrWhiteSpace(row.TokenHash)
                || row.Candidate?.Tenant is null || row.Candidate.CapabilityId is null
                || string.IsNullOrWhiteSpace(row.Candidate.Tenant.TenantId)
                || string.IsNullOrWhiteSpace(row.Candidate.CapabilityId.Value)
                || ShardOrdinal(row.TokenHash) != shard
                || string.CompareOrdinal(reference.FirstHash, row.TokenHash) > 0
                || string.CompareOrdinal(row.TokenHash, reference.LastHash) > 0
                || previous is not null && CompareDirectoryRows(previous, row) >= 0) return false;
            previous = row;
        }
        return string.Equals(digest, DirectoryPageDigest(page), StringComparison.Ordinal);
    }

    private static bool IsDigest(string? value)
        => value is { Length: 64 } && value.All(static character => char.IsAsciiHexDigitUpper(character));

    /// <summary>Compares directory rows by their canonical hash and owner order.</summary>
    public static int CompareDirectoryRows(MagicLinkTokenHashCapabilityIndexDirectoryEntry first,
        MagicLinkTokenHashCapabilityIndexDirectoryEntry second)
    {
        int comparison = string.CompareOrdinal(first.TokenHash, second.TokenHash);
        if (comparison != 0) return comparison;
        comparison = string.CompareOrdinal(first.Candidate.Tenant.TenantId, second.Candidate.Tenant.TenantId);
        return comparison != 0 ? comparison : string.CompareOrdinal(first.Candidate.CapabilityId.Value,
            second.Candidate.CapabilityId.Value);
    }

    /// <summary>Builds deterministic candidate content from issuance events without persisting it.</summary>
    /// <remarks>
    /// Retained as a pure composition helper. Its result does not select a generation, establish
    /// source coverage or owner delivery continuity, or authorize a migration. Canonical projection
    /// handlers and the EventStore coordinator own persisted rebuilds and selected-generation writes.
    /// </remarks>
    /// <param name="issuedEvents">The issuance events to fold.</param>
    /// <returns>The rebuilt candidate index.</returns>
    public static MagicLinkTokenHashCapabilityIndexReadModel Rebuild(
        IEnumerable<MagicLinkConfirmationCapabilityIssued> issuedEvents)
    {
        ArgumentNullException.ThrowIfNull(issuedEvents);
        return FromContributions(issuedEvents.Select(static issued =>
            new KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>(issued.TokenHash.Value,
                new MagicLinkTokenHashCapabilityIndexEntry(issued.Tenant, issued.CapabilityId))));
    }

    /// <summary>Idempotently applies one issuance candidate.</summary>
    /// <param name="current">The current index, if present.</param>
    /// <param name="issued">The authoritative issuance event.</param>
    /// <returns>The updated index.</returns>
    public static MagicLinkTokenHashCapabilityIndexReadModel Apply(
        MagicLinkTokenHashCapabilityIndexReadModel? current,
        MagicLinkConfirmationCapabilityIssued issued)
    {
        ArgumentNullException.ThrowIfNull(issued);

        return FromContributions(Expand(current).Append(
            new KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>(issued.TokenHash.Value,
                new MagicLinkTokenHashCapabilityIndexEntry(issued.Tenant, issued.CapabilityId)))) with
        {
            LegacyContributionsUnverified = HasUnverifiedLegacyContributions(current)
        };
    }

    /// <summary>Replaces exactly one tenant slice while preserving all other tenants.</summary>
    /// <param name="current">The current shared index, if present.</param>
    /// <param name="tenantId">The tenant slice to replace.</param>
    /// <param name="replacement">The rebuilt tenant slice.</param>
    /// <returns>The merged, canonical shared index.</returns>
    public static MagicLinkTokenHashCapabilityIndexReadModel ReplaceTenant(
        MagicLinkTokenHashCapabilityIndexReadModel? current,
        string tenantId,
        MagicLinkTokenHashCapabilityIndexReadModel replacement)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(replacement);

        return FromContributions(Expand(current)
            .Where(pair => !string.Equals(pair.Value.Tenant.TenantId, tenantId, StringComparison.Ordinal))
            .Concat(Expand(replacement))) with
        {
            LegacyContributionsUnverified = HasUnverifiedLegacyContributions(current)
                || HasUnverifiedLegacyContributions(replacement)
        };
    }

    /// <summary>Builds a selected shard from every owner contribution, retaining ambiguous hashes.</summary>
    public static MagicLinkTokenHashCapabilityIndexReadModel FromContributions(
        IEnumerable<KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);
        var all = contributions
            .GroupBy(static pair => pair.Key, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key,
                static group => (IReadOnlyList<MagicLinkTokenHashCapabilityIndexEntry>)group
                    .Select(static pair => pair.Value)
                    .Distinct()
                    .OrderBy(static candidate => candidate.Tenant.TenantId, StringComparer.Ordinal)
                    .ThenBy(static candidate => candidate.CapabilityId.Value, StringComparer.Ordinal)
                    .ToArray(), StringComparer.Ordinal);
        return new MagicLinkTokenHashCapabilityIndexReadModel(all
            .Where(static pair => pair.Value.Count == 1)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value[0], StringComparer.Ordinal))
        {
            Contributions = all,
            StorageVersion = CurrentStorageVersion
        };
    }

    /// <summary>Enumerates stored contributions, including legacy unique-only rows.</summary>
    public static IEnumerable<KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>> Expand(
        MagicLinkTokenHashCapabilityIndexReadModel? current)
    {
        if (current is null) yield break;
        if (current.Contributions is { Count: > 0 })
        {
            foreach ((string hash, IReadOnlyList<MagicLinkTokenHashCapabilityIndexEntry> candidates) in current.Contributions)
                foreach (MagicLinkTokenHashCapabilityIndexEntry candidate in candidates)
                    yield return new KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>(hash, candidate);
        }
        else
        {
            foreach (KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry> pair in current.Entries)
                yield return pair;
        }
    }

    private static bool HasUnverifiedLegacyContributions(MagicLinkTokenHashCapabilityIndexReadModel? current)
        => current is not null && (current.LegacyContributionsUnverified
            || current.Entries is null || current.Contributions is null
            || current.Entries.Count > 0 && current.Contributions.Count == 0);
}
