using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.Timesheets.Server.MagicLinks;

namespace Hexalith.Timesheets.Projections.MagicLinks;

/// <summary>
/// Explicit, resumable full migration of a certified selected v6 index. This operator action scans
/// the complete source once per tenant, keeping only that tenant's bounded rows in memory. It must
/// run in a maintenance window: the global epoch backpressures owner-sequenced delivery until promotion.
/// </summary>
public sealed class MagicLinkTokenHashCapabilityIndexV6Migration(
    SharedProjectionEpochCoordinator coordinator,
    IReadModelStore readModelStore,
    IMagicLinkIndexMigrationAuthority authority)
{
    private const int AppendWidth = 8;
    private const int AppendByteLimit = 256 * 1024;

    /// <summary>
    /// Begins or resumes the named migration and atomically selects v7 after validating every v6
    /// root and page. The caller must supply its certified inventory fingerprint and a unique ID.
    /// </summary>
    public async Task MigrateAsync(string operationId, string certifiedInventoryFingerprint,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(certifiedInventoryFingerprint);
        if (!await authority.AuthorizeCertifiedMigrationAsync(operationId,
            certifiedInventoryFingerprint, cancellationToken).ConfigureAwait(false))
            throw new UnauthorizedAccessException("A certified full-index migration authority is unavailable.");
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        SharedProjectionEpochStatus before = await coordinator.GetStatusAsync(scope, cancellationToken)
            .ConfigureAwait(false);
        ReadModelEntry<MagicLinkTokenHashCapabilityIndexTreeFormat> selectedFormat = await coordinator
            .ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexTreeFormat>(scope,
                before.ActiveGeneration, MagicLinkTokenHashCapabilityIndexTreeReader.FormatKey,
                cancellationToken).ConfigureAwait(false);
        ReadModelEntry<MagicLinkTokenHashCapabilityIndexTreeRoot> selectedTree = await coordinator
            .ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
                before.ActiveGeneration, MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
                cancellationToken).ConfigureAwait(false);
        if (selectedFormat.Value is not null || selectedTree.Value is not null)
        {
            if (selectedFormat.Value?.StorageVersion == 7
                && selectedTree.Value is { StorageVersion: 7 }
                && before.ActiveGeneration == before.Epoch
                && string.Equals(before.OperationId, operationId, StringComparison.Ordinal)) return;
            throw new InvalidOperationException("The selected global index is not a v6 migration source.");
        }
        ReadModelEntry<MagicLinkTokenHashCapabilityIndexReadModel> selectedSingle = await coordinator
            .ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope,
                before.ActiveGeneration, MagicLinkTokenHashCapabilityIndexProjection.StateKey,
                cancellationToken).ConfigureAwait(false);
        ReadModelEntry<MagicLinkTokenHashCapabilityIndexReadModel> directSingle = await readModelStore
            .GetAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope.StoreName,
                MagicLinkTokenHashCapabilityIndexProjection.StateKey, cancellationToken)
            .ConfigureAwait(false);
        if (selectedSingle.Value is not null || directSingle.Value is not null)
            throw new InvalidOperationException("A legacy single-root index requires a separate certified migration.");
        // Validate every selected source page before reserving the global writer fence. A malformed
        // v6 source cannot strand owner delivery in Building. The digest pins the preflight source.
        var preflight = await FindNextTenantAsync(scope, before.ActiveGeneration, null,
            cancellationToken).ConfigureAwait(false);
        string preflightDigest = preflight.Digest;
        long epoch = await coordinator.BeginAsync(scope, operationId, certifiedInventoryFingerprint,
            new Dictionary<string, long>(StringComparer.Ordinal), cancellationToken,
            operationTenantId: scope.TenantId).ConfigureAwait(false);
        SharedProjectionEpochStatus status = await coordinator.GetStatusAsync(scope, cancellationToken)
            .ConfigureAwait(false);
        if (status.ActiveGeneration == epoch && !status.IsBuilding) return;
        if (!status.IsBuilding || status.Epoch != epoch || status.ActiveGeneration >= epoch)
            throw new InvalidOperationException("The certified v6 migration epoch is not building.");
        if (status.StageFingerprint is not null)
        {
            await coordinator.CommitAsync(scope, operationId, cancellationToken).ConfigureAwait(false);
            return;
        }
        try
        {
        long sourceGeneration = status.ActiveGeneration;
        ReadModelEntry<MagicLinkTokenHashCapabilityIndexTreeFormat> oldFormat = await coordinator
            .ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexTreeFormat>(scope, sourceGeneration,
                MagicLinkTokenHashCapabilityIndexTreeReader.FormatKey, cancellationToken)
            .ConfigureAwait(false);
        ReadModelEntry<MagicLinkTokenHashCapabilityIndexTreeRoot> oldRoot = await coordinator
            .ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope, sourceGeneration,
                MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, cancellationToken)
            .ConfigureAwait(false);
        ReadModelEntry<MagicLinkTokenHashCapabilityIndexReadModel> oldSingle = await coordinator
            .ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope, sourceGeneration,
                MagicLinkTokenHashCapabilityIndexProjection.StateKey, cancellationToken)
            .ConfigureAwait(false);
        if (oldFormat.Value is not null || oldRoot.Value is not null || oldSingle.Value is not null)
            throw new InvalidOperationException("Migration requires a selected v6 shard index only.");

        MagicLinkTokenHashCapabilityIndexMigrationCheckpoint? checkpoint = null;
        for (long ordinal = 0; ; ordinal++)
        {
            ReadModelEntry<MagicLinkTokenHashCapabilityIndexMigrationCheckpoint> entry = await coordinator
                .ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexMigrationCheckpoint>(scope, epoch, CheckpointKey(epoch, ordinal),
                    cancellationToken).ConfigureAwait(false);
            if (entry.Value is null) break;
            MagicLinkTokenHashCapabilityIndexMigrationCheckpoint current = entry.Value;
            if (current.Ordinal != ordinal || current.SourceGeneration != sourceGeneration
                || current.Root is not { StorageVersion: 7 } || current.NextPageOrdinal < 1
                || current.NextRetirementOrdinal < 0 || current.SourceDigest is not { Length: 64 }
                || ordinal == 0 && current.LastTenantId is not null
                || ordinal > 0 && (current.LastTenantId is null || checkpoint is null)
                || checkpoint is not null
                    && (string.CompareOrdinal(current.LastTenantId, checkpoint.LastTenantId) <= 0
                        || current.NextPageOrdinal <= checkpoint.NextPageOrdinal
                        || current.SourceDigest != checkpoint.SourceDigest))
                throw new InvalidOperationException("A private migration checkpoint is inconsistent.");
            SharedProjectionStreamingStageStatus stage = await coordinator
                .GetStreamingStageStatusAsync(scope, operationId, cancellationToken)
                .ConfigureAwait(false);
            if (stage.CompletedPageCount < current.NextPageOrdinal)
                await coordinator.AppendStreamingPageAsync(scope, operationId,
                    current.NextPageOrdinal - 1,
                    [ReadModelBatchOperation.Write(CheckpointKey(epoch, ordinal), current,
                        ReadModelBatchConcurrency.LastWrite)], cancellationToken).ConfigureAwait(false);
            checkpoint = current;
        }

        if (checkpoint is null)
        {
            checkpoint = new MagicLinkTokenHashCapabilityIndexMigrationCheckpoint(0,
                sourceGeneration, preflightDigest, null,
                new MagicLinkTokenHashCapabilityIndexTreeRoot(7, 0, null, null), 0, 1);
            await coordinator.AppendStreamingPageAsync(scope, operationId, 0,
                [ReadModelBatchOperation.Write(CheckpointKey(epoch, 0), checkpoint,
                    ReadModelBatchConcurrency.LastWrite)], cancellationToken).ConfigureAwait(false);
        }

        var reader = new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, scope);
        long tenantOrdinal = checkpoint.Ordinal + 1;
        int pageOrdinal = checkpoint.NextPageOrdinal;
        long retirementOrdinal = checkpoint.NextRetirementOrdinal;
        MagicLinkTokenHashCapabilityIndexTreeRoot root = checkpoint.Root;
        string? lastTenant = checkpoint.LastTenantId;
        string sourceDigest = checkpoint.SourceDigest;
        if (sourceDigest != preflightDigest)
            throw new InvalidOperationException("The certified v6 source changed before resume.");

        for (; ; )
        {
            (string? nextTenant, List<MagicLinkTokenHashCapabilityIndexDirectoryEntry> rows,
                string digest) = await FindNextTenantAsync(scope, sourceGeneration, lastTenant,
                    cancellationToken).ConfigureAwait(false);
            if (sourceDigest is not null && sourceDigest != digest)
                throw new InvalidOperationException("The selected v6 source changed during migration.");
            sourceDigest = digest;
            if (nextTenant is null) break;
            var builder = new MagicLinkTokenHashCapabilityIndexTreeBuilder(reader, root, epoch);
            await builder.ReplaceTenantAsync(nextTenant, rows, cancellationToken).ConfigureAwait(false);
            pageOrdinal = await AppendOperationsAsync(scope, operationId,
                builder.Operations(false, retirementOrdinal), pageOrdinal, cancellationToken)
                .ConfigureAwait(false);
            retirementOrdinal = checked(retirementOrdinal + builder.RetirementCount);
            root = builder.SnapshotRoot;
            var completed = new MagicLinkTokenHashCapabilityIndexMigrationCheckpoint(tenantOrdinal, sourceGeneration, sourceDigest,
                nextTenant, root, retirementOrdinal, checked(pageOrdinal + 1));
            await coordinator.AppendStreamingPageAsync(scope, operationId, pageOrdinal,
                [ReadModelBatchOperation.Write(CheckpointKey(epoch, tenantOrdinal), completed,
                    ReadModelBatchConcurrency.LastWrite)], cancellationToken).ConfigureAwait(false);
            pageOrdinal++;
            tenantOrdinal++;
            lastTenant = nextTenant;
        }

        (pageOrdinal, retirementOrdinal) = await AppendV6RetirementsAsync(scope,
            sourceGeneration, operationId, pageOrdinal, retirementOrdinal,
            cancellationToken).ConfigureAwait(false);

        await coordinator.AppendStreamingPageAsync(scope, operationId, pageOrdinal,
        [
            ReadModelBatchOperation.Write(MagicLinkTokenHashCapabilityIndexTreeReader.FormatKey,
                new MagicLinkTokenHashCapabilityIndexTreeFormat(7), ReadModelBatchConcurrency.LastWrite),
            ReadModelBatchOperation.Write(MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
                root, ReadModelBatchConcurrency.LastWrite)
        ], cancellationToken).ConfigureAwait(false);
        _ = await coordinator.SealStreamingAsync(scope, operationId, cancellationToken).ConfigureAwait(false);
        await coordinator.CommitAsync(scope, operationId, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            SharedProjectionEpochStatus failed = await coordinator.GetStatusAsync(scope,
                CancellationToken.None).ConfigureAwait(false);
            if (failed.IsBuilding && string.Equals(failed.OperationId, operationId,
                StringComparison.Ordinal))
            {
                var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(
                    readModelStore, epochCoordinator: coordinator);
                await coordinator.AbortAsync(scope, operationId,
                    (delivery, generation, token) => handler.FoldCatchUpAsync(delivery,
                        generation, coordinator, token), CancellationToken.None).ConfigureAwait(false);
            }
            throw;
        }
    }

    private async Task<int> AppendOperationsAsync(SharedProjectionScope scope, string operationId,
        IEnumerable<ReadModelBatchOperation> operations, int pageOrdinal,
        CancellationToken cancellationToken)
    {
        var page = new List<ReadModelBatchOperation>(AppendWidth);
        int pageBytes = 0;
        foreach (ReadModelBatchOperation operation in operations)
        {
            int bytes = checked(operation.CanonicalValue.Length + Encoding.UTF8.GetByteCount(operation.Key)
                + Encoding.UTF8.GetByteCount(operation.ValueTypeName) + 1024);
            if (bytes > AppendByteLimit)
                throw new InvalidOperationException("A private v6 migration value exceeds the SDK append limit.");
            if (page.Count == AppendWidth || pageBytes + bytes > AppendByteLimit)
            {
                await coordinator.AppendStreamingPageAsync(scope, operationId, pageOrdinal++, page,
                    cancellationToken).ConfigureAwait(false);
                page = new List<ReadModelBatchOperation>(AppendWidth);
                pageBytes = 0;
            }
            page.Add(operation);
            pageBytes += bytes;
        }
        if (page.Count != 0)
            await coordinator.AppendStreamingPageAsync(scope, operationId, pageOrdinal++, page,
                cancellationToken).ConfigureAwait(false);
        return pageOrdinal;
    }

    private async Task<(int PageOrdinal, long RetirementOrdinal)> AppendV6RetirementsAsync(
        SharedProjectionScope scope, long sourceGeneration, string operationId,
        int pageOrdinal, long retirementOrdinal, CancellationToken cancellationToken)
    {
        var page = new List<ReadModelBatchOperation>(AppendWidth);
        async Task AddAsync(string key)
        {
            page.Add(SharedProjectionRetiredValue.Stage(retirementOrdinal++, sourceGeneration, key));
            if (page.Count < AppendWidth) return;
            await coordinator.AppendStreamingPageAsync(scope, operationId, pageOrdinal++, page,
                cancellationToken).ConfigureAwait(false);
            page = new List<ReadModelBatchOperation>(AppendWidth);
        }

        for (int shard = 0; shard < MagicLinkTokenHashCapabilityIndexProjection.ShardCount; shard++)
        {
            string shardKey = MagicLinkTokenHashCapabilityIndexProjection.ShardKey(shard);
            ReadModelEntry<MagicLinkTokenHashCapabilityIndexReadModel> selected = await coordinator
                .ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope,
                    sourceGeneration, shardKey, cancellationToken).ConfigureAwait(false);
            if (selected.Value is not { } root) continue;
            await AddAsync(shardKey).ConfigureAwait(false);
            foreach (IReadOnlyList<int> ids in root.CandidatePageIds.Values)
                foreach (int id in ids)
                    await AddAsync(MagicLinkTokenHashCapabilityIndexProjection.CandidatePageKey(shard,
                        id)).ConfigureAwait(false);
            foreach (MagicLinkTokenHashCapabilityIndexDirectoryReference reference in root.DirectoryPages)
                await AddAsync(MagicLinkTokenHashCapabilityIndexProjection.DirectoryPageKey(shard,
                    reference.PageId)).ConfigureAwait(false);
        }
        await foreach (MagicLinkTokenHashCapabilityIndexDirectoryEntry row in ReadV6RowsAsync(
            scope, sourceGeneration, cancellationToken).ConfigureAwait(false))
            await AddAsync(MagicLinkTokenHashCapabilityIndexProjection.OwnerAdmissionKey(
                row.Candidate.Tenant.TenantId, row.Candidate.CapabilityId.Value)).ConfigureAwait(false);
        if (page.Count > 0)
            await coordinator.AppendStreamingPageAsync(scope, operationId, pageOrdinal++, page,
                cancellationToken).ConfigureAwait(false);
        return (pageOrdinal, retirementOrdinal);
    }

    private async Task<(string? TenantId, List<MagicLinkTokenHashCapabilityIndexDirectoryEntry> Rows,
        string Digest)> FindNextTenantAsync(SharedProjectionScope scope, long sourceGeneration,
        string? afterTenant, CancellationToken cancellationToken)
    {
        string? minimum = null;
        var selected = new List<MagicLinkTokenHashCapabilityIndexDirectoryEntry>();
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await foreach (MagicLinkTokenHashCapabilityIndexDirectoryEntry row in ReadV6RowsAsync(scope,
            sourceGeneration, cancellationToken).ConfigureAwait(false))
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(row);
            hash.AppendData(BitConverter.GetBytes(bytes.Length));
            hash.AppendData(bytes);
            string tenant = row.Candidate.Tenant.TenantId;
            if (afterTenant is not null && string.CompareOrdinal(tenant, afterTenant) <= 0
                || minimum is not null && string.CompareOrdinal(tenant, minimum) > 0) continue;
            if (minimum is null || string.CompareOrdinal(tenant, minimum) < 0)
            {
                minimum = tenant;
                selected.Clear();
            }
            selected.Add(row);
            if (selected.Count > 10_000)
                throw new InvalidOperationException("A v6 tenant exceeds the supported 10,000-owner migration capacity.");
        }
        return (minimum, selected, Convert.ToHexString(hash.GetHashAndReset()));
    }

    private async IAsyncEnumerable<MagicLinkTokenHashCapabilityIndexDirectoryEntry> ReadV6RowsAsync(
        SharedProjectionScope scope, long generation,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        bool hadShard = false;
        for (int shard = 0; shard < MagicLinkTokenHashCapabilityIndexProjection.ShardCount; shard++)
        {
            ReadModelEntry<MagicLinkTokenHashCapabilityIndexReadModel> selected = await coordinator
                .ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope, generation,
                    MagicLinkTokenHashCapabilityIndexProjection.ShardKey(shard), cancellationToken)
                .ConfigureAwait(false);
            MagicLinkTokenHashCapabilityIndexReadModel? root = selected.Value;
            if (root is null) continue;
            hadShard = true;
            if (!MagicLinkTokenHashCapabilityIndexProjection.HasValidPageManifest(root)
                || root.StorageVersion != 6 || root.LegacyContributionsUnverified)
                throw new InvalidOperationException("A selected v6 shard is unverified or malformed.");
            foreach ((string tokenHash, IReadOnlyList<MagicLinkTokenHashCapabilityIndexEntry> owners)
                in root.Contributions.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (MagicLinkTokenHashCapabilityIndexProjection.ShardOrdinal(tokenHash) != shard)
                    throw new InvalidOperationException("A v6 inline row is in the wrong shard.");
                foreach (MagicLinkTokenHashCapabilityIndexEntry owner in owners)
                    yield return new MagicLinkTokenHashCapabilityIndexDirectoryEntry(tokenHash, owner);
            }
            foreach ((string tokenHash, IReadOnlyList<int> ids) in root.CandidatePageIds
                .OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (MagicLinkTokenHashCapabilityIndexProjection.ShardOrdinal(tokenHash) != shard)
                    throw new InvalidOperationException("A v6 collision row is in the wrong shard.");
                var uniqueOwners = new HashSet<MagicLinkTokenHashCapabilityIndexEntry>();
                foreach (int id in ids)
                {
                    ReadModelEntry<MagicLinkTokenHashCapabilityIndexPage> page = await coordinator
                        .ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexPage>(scope, generation,
                            MagicLinkTokenHashCapabilityIndexProjection.CandidatePageKey(shard, id),
                            cancellationToken).ConfigureAwait(false);
                    if (!MagicLinkTokenHashCapabilityIndexProjection.HasValidCandidatePage(root,
                        tokenHash, id, page.Value))
                        throw new InvalidOperationException("A selected v6 collision page is invalid.");
                    foreach (MagicLinkTokenHashCapabilityIndexEntry owner in page.Value!.Candidates)
                    {
                        if (!uniqueOwners.Add(owner))
                            throw new InvalidOperationException("A selected v6 collision owner is duplicated.");
                        yield return new MagicLinkTokenHashCapabilityIndexDirectoryEntry(tokenHash, owner);
                    }
                }
            }
            foreach (MagicLinkTokenHashCapabilityIndexDirectoryReference reference in root.DirectoryPages)
            {
                ReadModelEntry<MagicLinkTokenHashCapabilityIndexDirectoryPage> page = await coordinator
                    .ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexDirectoryPage>(scope, generation,
                        MagicLinkTokenHashCapabilityIndexProjection.DirectoryPageKey(shard, reference.PageId),
                        cancellationToken).ConfigureAwait(false);
                if (!MagicLinkTokenHashCapabilityIndexProjection.HasValidDirectoryPage(root, shard,
                    reference, page.Value))
                    throw new InvalidOperationException("A selected v6 directory page is invalid.");
                foreach (MagicLinkTokenHashCapabilityIndexDirectoryEntry row in page.Value!.Entries)
                    yield return row;
            }
        }
        if (!hadShard)
            throw new InvalidOperationException("There is no selected v6 inventory to migrate.");
    }

    private static string CheckpointKey(long epoch, long ordinal)
        => "timesheets:magic-links:token-hash-capability-index:v7:migration:"
            + epoch.ToString("D16", System.Globalization.CultureInfo.InvariantCulture) + ":"
            + ordinal.ToString("D16", System.Globalization.CultureInfo.InvariantCulture);

}

/// <summary>Private staged migration progress, addressed only by epoch and numeric ordinal.</summary>
public sealed record MagicLinkTokenHashCapabilityIndexMigrationCheckpoint(
    long Ordinal, long SourceGeneration, string SourceDigest, string? LastTenantId,
    MagicLinkTokenHashCapabilityIndexTreeRoot Root, long NextRetirementOrdinal, int NextPageOrdinal);
