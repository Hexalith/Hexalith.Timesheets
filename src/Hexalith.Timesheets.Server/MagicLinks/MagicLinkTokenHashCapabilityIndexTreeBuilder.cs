using Hexalith.EventStore.Client.Projections;

namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>Copies only changed trie paths and one tenant's paged roster into a new epoch.</summary>
public sealed class MagicLinkTokenHashCapabilityIndexTreeBuilder(
    MagicLinkTokenHashCapabilityIndexTreeReader reader,
    MagicLinkTokenHashCapabilityIndexTreeRoot selected,
    long generation)
{
    private readonly long _firstAllocatedId = selected.NextNodeId;
    private readonly Dictionary<long, MagicLinkTokenHashCapabilityIndexTreeNode> _nodes = [];
    private readonly Dictionary<long, MagicLinkTokenHashCapabilityIndexTenantRosterPage> _pages = [];
    private readonly HashSet<(long Generation, string Key)> _retired = [];
    private long _nextNodeId = selected.NextNodeId;
    private MagicLinkTokenHashCapabilityIndexNodeReference? _tokenRoot = selected.TokenRoot;
    private MagicLinkTokenHashCapabilityIndexNodeReference? _tenantRoot = selected.TenantRoot;
    private MagicLinkTokenHashCapabilityIndexNodeReference? _ownerRoot = selected.OwnerRoot;

    /// <summary>The private root after the current tenant, suitable for a unique migration checkpoint.</summary>
    public MagicLinkTokenHashCapabilityIndexTreeRoot SnapshotRoot
        => new(7, _nextNodeId, _tokenRoot, _tenantRoot) { OwnerRoot = _ownerRoot };

    /// <summary>Number of deferred retirements emitted by this tenant update.</summary>
    public int RetirementCount => _retired.Count;

    /// <summary>Replaces one tenant's complete bounded contribution set.</summary>
    public async Task ReplaceTenantAsync(string tenantId,
        IReadOnlyList<MagicLinkTokenHashCapabilityIndexDirectoryEntry> replacement,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(replacement);
        if (selected.StorageVersion != 7 || selected.NextNodeId < 0 || generation < 0
            || replacement.Count > 10_000)
            throw new InvalidOperationException("The selected numeric index root is invalid.");
        IReadOnlyList<MagicLinkTokenHashCapabilityIndexDirectoryEntry> prior =
            await reader.ReadRosterAsync(selected.TenantRoot, tenantId, cancellationToken)
                .ConfigureAwait(false) ?? [];
        MagicLinkTokenHashCapabilityIndexTenantRoster? oldRoster = await reader.FindRosterAsync(
            selected.TenantRoot, tenantId, cancellationToken).ConfigureAwait(false);
        foreach (MagicLinkTokenHashCapabilityIndexDirectoryEntry row in prior)
        {
            if (!await reader.ContainsExactRowAsync(selected.TokenRoot, row, cancellationToken)
                .ConfigureAwait(false)
                || await reader.FindOwnerAsync(selected.OwnerRoot, tenantId,
                    row.Candidate.CapabilityId.Value, cancellationToken).ConfigureAwait(false) != row)
                throw new SharedProjectionReadUnavailableException(
                    new InvalidOperationException("A selected tenant roster row is absent from an index trie."));
            _tokenRoot = await ChangeAsync(_tokenRoot,
                MagicLinkTokenHashCapabilityIndexTreeReader.TokenPath(row.TokenHash, row.Candidate),
                0, null, null, cancellationToken).ConfigureAwait(false);
            _ownerRoot = await ChangeAsync(_ownerRoot,
                MagicLinkTokenHashCapabilityIndexTreeReader.OwnerPath(tenantId,
                    row.Candidate.CapabilityId.Value), 0, null, null, cancellationToken)
                .ConfigureAwait(false);
        }

        if (replacement.Any(row => row?.Candidate?.Tenant?.TenantId != tenantId
                || string.IsNullOrWhiteSpace(row.TokenHash)
                || row.Candidate.CapabilityId is null)
            || replacement.Select(row => row.Candidate.CapabilityId.Value)
                .Distinct(StringComparer.Ordinal).Count() != replacement.Count)
            throw new InvalidOperationException("The tenant replacement has conflicting owner contributions.");
        MagicLinkTokenHashCapabilityIndexDirectoryEntry[] next = [.. replacement
            .OrderBy(row => MagicLinkTokenHashCapabilityIndexTreeReader.TokenPath(
                row.TokenHash, row.Candidate), StringComparer.Ordinal)];
        foreach (MagicLinkTokenHashCapabilityIndexDirectoryEntry row in next)
        {
            _tokenRoot = await ChangeAsync(_tokenRoot,
                MagicLinkTokenHashCapabilityIndexTreeReader.TokenPath(row.TokenHash, row.Candidate),
                0, row, null, cancellationToken).ConfigureAwait(false);
            _ownerRoot = await ChangeAsync(_ownerRoot,
                MagicLinkTokenHashCapabilityIndexTreeReader.OwnerPath(tenantId,
                    row.Candidate.CapabilityId.Value), 0, row, null, cancellationToken)
                .ConfigureAwait(false);
        }

        await ReplaceRosterAsync(tenantId, next, oldRoster, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Adds one ordinary issuance to both selected tries in the same SDK catch-up.</summary>
    public async Task AddContributionAsync(MagicLinkTokenHashCapabilityIndexDirectoryEntry contribution,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contribution);
        string tenantId = contribution.Candidate.Tenant.TenantId;
        string ownerId = contribution.Candidate.CapabilityId.Value;
        MagicLinkTokenHashCapabilityIndexDirectoryEntry? admitted = await reader.FindOwnerAsync(
            selected.OwnerRoot, tenantId, ownerId, cancellationToken).ConfigureAwait(false);
        if (admitted is not null)
        {
            if (!await reader.ContainsExactRowAsync(selected.TokenRoot, admitted,
                cancellationToken).ConfigureAwait(false))
                throw new SharedProjectionReadUnavailableException(
                    new InvalidOperationException("A selected owner admission is absent from the token tree."));
            throw new InvalidOperationException("The selected tenant roster already owns this capability.");
        }
        if (await reader.ContainsExactRowAsync(selected.TokenRoot, contribution,
            cancellationToken).ConfigureAwait(false))
            throw new SharedProjectionReadUnavailableException(
                new InvalidOperationException("A token row exists without its owner admission."));
        MagicLinkTokenHashCapabilityIndexTenantRoster? oldRoster = await reader.FindRosterAsync(
            selected.TenantRoot, tenantId, cancellationToken).ConfigureAwait(false);
        if (oldRoster?.ContributionCount >= 10_000)
            throw new InvalidOperationException("The selected tenant roster already owns this capability or is full.");
        _tokenRoot = await ChangeAsync(_tokenRoot,
            MagicLinkTokenHashCapabilityIndexTreeReader.TokenPath(contribution.TokenHash,
                contribution.Candidate), 0, contribution, null, cancellationToken).ConfigureAwait(false);
        _ownerRoot = await ChangeAsync(_ownerRoot,
            MagicLinkTokenHashCapabilityIndexTreeReader.OwnerPath(tenantId, ownerId), 0,
            contribution, null, cancellationToken).ConfigureAwait(false);
        await InsertRosterRowAsync(tenantId, contribution, oldRoster, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ReplaceRosterAsync(string tenantId,
        IReadOnlyList<MagicLinkTokenHashCapabilityIndexDirectoryEntry> next,
        MagicLinkTokenHashCapabilityIndexTenantRoster? oldRoster,
        CancellationToken cancellationToken)
    {
        if (oldRoster is not null)
            foreach (MagicLinkTokenHashCapabilityIndexTenantRosterPageReference page in oldRoster.Pages)
                Retire(page.Page, MagicLinkTokenHashCapabilityIndexTreeReader.RosterPageKey(page.Page.NodeId));
        var newPages = new List<MagicLinkTokenHashCapabilityIndexTenantRosterPageReference>((next.Count + 63) / 64);
        for (int offset = 0; offset < next.Count; offset += 64)
        {
            newPages.Add(SaveRosterPage([.. next.Skip(offset).Take(64)]));
        }
        var roster = new MagicLinkTokenHashCapabilityIndexTenantRoster(tenantId, next.Count, newPages);
        _tenantRoot = await ChangeAsync(_tenantRoot,
            MagicLinkTokenHashCapabilityIndexTreeReader.TenantPath(tenantId), 0, null, roster,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task InsertRosterRowAsync(string tenantId,
        MagicLinkTokenHashCapabilityIndexDirectoryEntry contribution,
        MagicLinkTokenHashCapabilityIndexTenantRoster? prior, CancellationToken cancellationToken)
    {
        var pages = prior is null
            ? new List<MagicLinkTokenHashCapabilityIndexTenantRosterPageReference>()
            : new List<MagicLinkTokenHashCapabilityIndexTenantRosterPageReference>(prior.Pages);
        if (pages.Count == 0)
            pages.Add(SaveRosterPage([contribution]));
        else
        {
            string path = MagicLinkTokenHashCapabilityIndexTreeReader.TokenPath(
                contribution.TokenHash, contribution.Candidate);
            // A short manifest prefix can be equal across every page of a concentrated
            // hash. Probe digest-checked page ends by binary search instead of scanning
            // all pages on every ordinary issuance.
            int low = 0;
            int high = pages.Count - 1;
            while (low < high)
            {
                int middle = low + ((high - low) / 2);
                MagicLinkTokenHashCapabilityIndexTenantRosterPage middlePage =
                    await reader.ReadRosterPageAsync(pages[middle], cancellationToken)
                        .ConfigureAwait(false);
                string last = MagicLinkTokenHashCapabilityIndexTreeReader.TokenPath(
                    middlePage.Rows[^1].TokenHash, middlePage.Rows[^1].Candidate);
                if (string.CompareOrdinal(path, last) <= 0) high = middle;
                else low = middle + 1;
            }
            int index = low;
            MagicLinkTokenHashCapabilityIndexTenantRosterPage selectedPage =
                await reader.ReadRosterPageAsync(pages[index], cancellationToken)
                    .ConfigureAwait(false);
            if (index > 0)
            {
                MagicLinkTokenHashCapabilityIndexTenantRosterPage preceding =
                    await reader.ReadRosterPageAsync(pages[index - 1], cancellationToken)
                        .ConfigureAwait(false);
                string previousLast = MagicLinkTokenHashCapabilityIndexTreeReader.TokenPath(
                    preceding.Rows[^1].TokenHash, preceding.Rows[^1].Candidate);
                string selectedFirst = MagicLinkTokenHashCapabilityIndexTreeReader.TokenPath(
                    selectedPage.Rows[0].TokenHash, selectedPage.Rows[0].Candidate);
                if (string.CompareOrdinal(previousLast, selectedFirst) >= 0
                    || string.CompareOrdinal(previousLast, path) >= 0)
                    throw new SharedProjectionReadUnavailableException(
                        new InvalidOperationException("The selected tenant roster page boundary is invalid."));
            }
            if (index + 1 < pages.Count)
            {
                MagicLinkTokenHashCapabilityIndexTenantRosterPage following =
                    await reader.ReadRosterPageAsync(pages[index + 1], cancellationToken)
                        .ConfigureAwait(false);
                string selectedLast = MagicLinkTokenHashCapabilityIndexTreeReader.TokenPath(
                    selectedPage.Rows[^1].TokenHash, selectedPage.Rows[^1].Candidate);
                string nextFirst = MagicLinkTokenHashCapabilityIndexTreeReader.TokenPath(
                    following.Rows[0].TokenHash, following.Rows[0].Candidate);
                if (string.CompareOrdinal(selectedLast, nextFirst) >= 0
                    || string.CompareOrdinal(path, nextFirst) >= 0)
                    throw new SharedProjectionReadUnavailableException(
                        new InvalidOperationException("The selected tenant roster page boundary is invalid."));
            }
            var rows = selectedPage.Rows.ToList();
            int insert = rows.FindIndex(row => string.CompareOrdinal(path,
                MagicLinkTokenHashCapabilityIndexTreeReader.TokenPath(row.TokenHash,
                    row.Candidate)) < 0);
            if (insert < 0) rows.Add(contribution);
            else rows.Insert(insert, contribution);
            Retire(pages[index].Page, MagicLinkTokenHashCapabilityIndexTreeReader.RosterPageKey(
                pages[index].Page.NodeId));
            pages.RemoveAt(index);
            if (rows.Count <= 64)
                pages.Insert(index, SaveRosterPage(rows));
            else
            {
                pages.Insert(index, SaveRosterPage(rows[..32]));
                pages.Insert(index + 1, SaveRosterPage(rows[32..]));
            }
        }
        var roster = new MagicLinkTokenHashCapabilityIndexTenantRoster(tenantId,
            checked((prior?.ContributionCount ?? 0) + 1), pages);
        MagicLinkTokenHashCapabilityIndexTreeReader.ValidateRosterManifest(roster, tenantId);
        _tenantRoot = await ChangeAsync(_tenantRoot,
            MagicLinkTokenHashCapabilityIndexTreeReader.TenantPath(tenantId), 0, null,
            roster, cancellationToken).ConfigureAwait(false);
    }

    private MagicLinkTokenHashCapabilityIndexTenantRosterPageReference SaveRosterPage(
        IReadOnlyList<MagicLinkTokenHashCapabilityIndexDirectoryEntry> rows)
    {
        var page = new MagicLinkTokenHashCapabilityIndexTenantRosterPage(rows);
        long id = AllocateId();
        _pages.Add(id, page);
        return new(new(generation, id,
            MagicLinkTokenHashCapabilityIndexTreeReader.RosterPageDigest(page)),
            MagicLinkTokenHashCapabilityIndexTreeReader.RosterPathPrefix(
                MagicLinkTokenHashCapabilityIndexTreeReader.TokenPath(rows[0].TokenHash,
                    rows[0].Candidate)));
    }

    /// <summary>Stages immutable nodes, the selected root, then recoverable retirements.</summary>
    public IEnumerable<ReadModelBatchOperation> Operations(bool includeSelectedRoot = true,
        long retirementOrdinalBase = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(retirementOrdinalBase);
        foreach ((long id, MagicLinkTokenHashCapabilityIndexTreeNode node) in _nodes.OrderBy(pair => pair.Key))
            yield return ReadModelBatchOperation.Write(
                MagicLinkTokenHashCapabilityIndexTreeReader.NodeKey(id), node,
                ReadModelBatchConcurrency.LastWrite);
        foreach ((long id, MagicLinkTokenHashCapabilityIndexTenantRosterPage page) in
            _pages.OrderBy(pair => pair.Key))
            yield return ReadModelBatchOperation.Write(
                MagicLinkTokenHashCapabilityIndexTreeReader.RosterPageKey(id), page,
                ReadModelBatchConcurrency.LastWrite);
        if (includeSelectedRoot)
        {
            yield return ReadModelBatchOperation.Write(MagicLinkTokenHashCapabilityIndexTreeReader.FormatKey,
                new MagicLinkTokenHashCapabilityIndexTreeFormat(7), ReadModelBatchConcurrency.LastWrite);
            yield return ReadModelBatchOperation.Write(MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
                SnapshotRoot, ReadModelBatchConcurrency.LastWrite);
        }
        long ordinal = retirementOrdinalBase;
        foreach ((long retiredGeneration, string key) in _retired
            .OrderBy(item => item.Generation).ThenBy(item => item.Key, StringComparer.Ordinal))
            yield return SharedProjectionRetiredValue.Stage(ordinal++, retiredGeneration, key);
    }

    private async Task<MagicLinkTokenHashCapabilityIndexNodeReference?> ChangeAsync(
        MagicLinkTokenHashCapabilityIndexNodeReference? reference, string path, int offset,
        MagicLinkTokenHashCapabilityIndexDirectoryEntry? row,
        MagicLinkTokenHashCapabilityIndexTenantRoster? roster,
        CancellationToken cancellationToken)
    {
        if (reference is null)
            return row is null && roster is null ? null : Save(new(path[offset..],
                new Dictionary<char, MagicLinkTokenHashCapabilityIndexNodeReference>(), row)
            {
                Roster = roster
            }, null);
        MagicLinkTokenHashCapabilityIndexTreeNode node = await LoadAsync(reference, cancellationToken)
            .ConfigureAwait(false);
        int common = 0;
        while (common < node.Prefix.Length && offset + common < path.Length
            && node.Prefix[common] == path[offset + common]) common++;
        if (common < node.Prefix.Length)
        {
            if (row is null && roster is null) return reference;
            char oldEdge = node.Prefix[common];
            MagicLinkTokenHashCapabilityIndexNodeReference oldChild = Save(node with
            {
                Prefix = node.Prefix[(common + 1)..]
            }, reference);
            var children = new Dictionary<char, MagicLinkTokenHashCapabilityIndexNodeReference>
            {
                [oldEdge] = oldChild
            };
            MagicLinkTokenHashCapabilityIndexDirectoryEntry? parentRow = null;
            MagicLinkTokenHashCapabilityIndexTenantRoster? parentRoster = null;
            if (offset + common == path.Length)
            {
                parentRow = row;
                parentRoster = roster;
            }
            else
            {
                char newEdge = path[offset + common];
                children.Add(newEdge, Save(new(path[(offset + common + 1)..],
                    new Dictionary<char, MagicLinkTokenHashCapabilityIndexNodeReference>(), row)
                {
                    Roster = roster
                }, null));
            }
            return Save(new(path.Substring(offset, common), children, parentRow)
            {
                Roster = parentRoster
            }, null);
        }

        offset += common;
        MagicLinkTokenHashCapabilityIndexTreeNode changed;
        if (offset == path.Length)
        {
            if (Equals(node.Row, row) && Equals(node.Roster, roster)) return reference;
            changed = node with { Row = row, Roster = roster };
        }
        else
        {
            char edge = path[offset];
            _ = node.Children.TryGetValue(edge, out MagicLinkTokenHashCapabilityIndexNodeReference? child);
            MagicLinkTokenHashCapabilityIndexNodeReference? changedChild = await ChangeAsync(child,
                path, offset + 1, row, roster, cancellationToken).ConfigureAwait(false);
            if (Equals(child, changedChild)) return reference;
            var children = new Dictionary<char, MagicLinkTokenHashCapabilityIndexNodeReference>(node.Children);
            if (changedChild is null) children.Remove(edge);
            else children[edge] = changedChild;
            changed = node with { Children = children };
        }
        if (changed.Row is null && changed.Roster is null && changed.Children.Count == 0)
        {
            Retire(reference, MagicLinkTokenHashCapabilityIndexTreeReader.NodeKey(reference.NodeId));
            return null;
        }
        if (changed.Row is null && changed.Roster is null && changed.Children.Count == 1)
        {
            (char edge, MagicLinkTokenHashCapabilityIndexNodeReference child) = changed.Children.Single();
            MagicLinkTokenHashCapabilityIndexTreeNode descendant = await LoadAsync(child, cancellationToken)
                .ConfigureAwait(false);
            Retire(child, MagicLinkTokenHashCapabilityIndexTreeReader.NodeKey(child.NodeId));
            return Save(descendant with { Prefix = changed.Prefix + edge + descendant.Prefix }, reference);
        }
        return Save(changed, reference);
    }

    private async Task<MagicLinkTokenHashCapabilityIndexTreeNode> LoadAsync(
        MagicLinkTokenHashCapabilityIndexNodeReference reference, CancellationToken cancellationToken)
        => reference.Generation == generation && _nodes.TryGetValue(reference.NodeId, out var staged)
            ? staged
            : await reader.ReadNodeAsync(reference, cancellationToken).ConfigureAwait(false);

    private MagicLinkTokenHashCapabilityIndexNodeReference Save(
        MagicLinkTokenHashCapabilityIndexTreeNode node,
        MagicLinkTokenHashCapabilityIndexNodeReference? replacing)
    {
        long id;
        if (replacing is not null && replacing.Generation == generation
            && replacing.NodeId >= _firstAllocatedId && _nodes.ContainsKey(replacing.NodeId))
            id = replacing.NodeId;
        else
        {
            id = AllocateId();
            if (replacing is not null)
                Retire(replacing, MagicLinkTokenHashCapabilityIndexTreeReader.NodeKey(replacing.NodeId));
        }
        _nodes[id] = node;
        return new(generation, id, MagicLinkTokenHashCapabilityIndexTreeReader.NodeDigest(node));
    }

    private long AllocateId() => checked(_nextNodeId++);

    private void Retire(MagicLinkTokenHashCapabilityIndexNodeReference reference, string key)
    {
        if (reference.Generation == generation && reference.NodeId >= _firstAllocatedId
            && _nodes.Remove(reference.NodeId)) return;
        _retired.Add((reference.Generation, key));
    }
}
