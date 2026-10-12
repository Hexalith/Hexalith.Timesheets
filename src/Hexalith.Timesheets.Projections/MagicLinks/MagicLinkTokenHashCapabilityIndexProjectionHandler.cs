using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;

using Dapr;

using Hexalith.EventStore.Client.Attributes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.Runtime;

using Microsoft.Extensions.Logging;

namespace Hexalith.Timesheets.Projections.MagicLinks;

/// <summary>Persists and rebuilds the non-authoritative magic-link token-hash candidate index.</summary>
/// <remarks>
/// The explicit domain attribute is what makes this handler discoverable by the SDK's domain-telemetry
/// scan: that scan only instantiates handlers exposing a parameterless constructor, which this
/// store-injected handler deliberately does not.
/// </remarks>
[EventStoreDomain(TimesheetsEventStoreIntegration.DomainName)]
public sealed class MagicLinkTokenHashCapabilityIndexProjectionHandler(
    IReadModelStore readModelStore,
    ILoggerFactory? loggerFactory = null,
    SharedProjectionEpochCoordinator? epochCoordinator = null) :
    IAsyncDomainSharedProjectionStreamedOwnerHandler,
    IDeclaresProjectionReadModelSlots
{
    /// <summary>The logical read-model slot this handler is the canonical writer for.</summary>
    public const string SlotName = "index";

    // An explicit category rather than ILogger<T>: this type's own name carries the word the
    // DiagnosticsPrivacyTests source scan forbids on any logging line, and that guard is worth
    // keeping strict. Nothing token-derived is ever logged; the category is a fixed literal.
    private const string LogCategory = "Hexalith.Timesheets.Projections.CandidateIndexProjection";

    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger? _logger = loggerFactory?.CreateLogger(LogCategory);
    private readonly SharedProjectionEpochCoordinator? _epochCoordinator = epochCoordinator;

    /// <summary>Gets the canonical shared slot declaration.</summary>
    public static IReadOnlyList<ProjectionReadModelSlotDeclaration> ProjectionReadModelSlots { get; } =
    [
        new(
            TimesheetsEventStoreIntegration.DomainName,
            MagicLinkTokenHashCapabilityIndexProjection.ProjectionName,
            SlotName,
            ProjectionReadModelSlotKind.Shared,
            declaresCanonicalWriter: true)
    ];

    /// <inheritdoc />
    public string Domain => TimesheetsEventStoreIntegration.DomainName;

    /// <inheritdoc />
    public string ProjectionType => MagicLinkTokenHashCapabilityIndexProjection.ProjectionName;

    /// <inheritdoc />
    public string RebuildStoreName => MagicLinkTokenHashCapabilityIndexProjection.StateStoreName;

    /// <inheritdoc />
    public bool EpochEnabled => true;

    /// <inheritdoc />
    public bool GlobalEpochScope => true;

    /// <inheritdoc />
    public string OrdinaryWriterId => MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId;

    /// <inheritdoc />
    public SharedProjectionScope CreateScope(string tenantId)
        => MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReadModelBatchOperation>> FoldCatchUpAsync(
        SharedProjectionDelivery delivery,
        long generation,
        SharedProjectionEpochCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(coordinator);
        ProjectionRequest request;
        MagicLinkConfirmationCapabilityIssued? issued;
        try
        {
            request = JsonSerializer.Deserialize<ProjectionRequest>(delivery.CanonicalPayload, s_jsonOptions)
                ?? throw new InvalidOperationException("Index delivery is empty.");
            Validate(request, "epoch-catch-up");
            issued = FoldIssuance(request);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException
            or JsonException or NullReferenceException)
        {
            throw Park(delivery);
        }
        if (issued is null) return [];
        SharedProjectionScope scope = CreateScope(request.TenantId);
        ReadModelEntry<MagicLinkTokenHashCapabilityIndexTreeFormat> format = await coordinator
            .ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexTreeFormat>(scope, generation,
                MagicLinkTokenHashCapabilityIndexTreeReader.FormatKey, cancellationToken)
            .ConfigureAwait(false);
        ReadModelEntry<MagicLinkTokenHashCapabilityIndexTreeRoot> tree = await coordinator
            .ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope, generation,
                MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, cancellationToken)
            .ConfigureAwait(false);
        if (format.Value is not null || tree.Value is not null
            || !await HasSelectedLegacyIndexAsync(scope, generation, coordinator,
                cancellationToken).ConfigureAwait(false))
        {
            if ((format.Value is not null || tree.Value is not null)
                && (format.Value?.StorageVersion != 7 || tree.Value is not { StorageVersion: 7 }))
                throw new SharedProjectionReadUnavailableException(
                    new InvalidOperationException("The selected numeric index root is invalid."));
            MagicLinkTokenHashCapabilityIndexTreeRoot selectedTree = tree.Value
                ?? new MagicLinkTokenHashCapabilityIndexTreeRoot(7, 0, null, null);
            var reader = new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, scope);
            var builder = new MagicLinkTokenHashCapabilityIndexTreeBuilder(reader, selectedTree, generation);
            try
            {
                await builder.AddContributionAsync(new MagicLinkTokenHashCapabilityIndexDirectoryEntry(
                    issued.TokenHash.Value,
                    new MagicLinkTokenHashCapabilityIndexEntry(issued.Tenant, issued.CapabilityId)),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                throw Park(delivery);
            }
            return [.. builder.Operations()];
        }
        int shard = MagicLinkTokenHashCapabilityIndexProjection.ShardOrdinal(issued.TokenHash.Value);
        string key = MagicLinkTokenHashCapabilityIndexProjection.ShardKey(shard);
        ReadModelEntry<MagicLinkTokenHashCapabilityIndexReadModel> selected;
        try
        {
            selected = await coordinator
                .ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexReadModel>(CreateScope(request.TenantId),
                    generation, key, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is DaprException or IOException or HttpRequestException
            or TimeoutException or TaskCanceledException)
        { throw new SharedProjectionReadUnavailableException(exception); }
        MagicLinkTokenHashCapabilityIndexReadModel current = selected.Value
            ?? MagicLinkTokenHashCapabilityIndexProjection.FromContributions([]);
        if (!MagicLinkTokenHashCapabilityIndexProjection.HasValidPageManifest(current))
            throw new SharedProjectionReadUnavailableException(
                new InvalidOperationException("The selected index page manifest is invalid."));
        string hash = issued.TokenHash.Value;
        var owner = new MagicLinkTokenHashCapabilityIndexEntry(issued.Tenant, issued.CapabilityId);
        string ownerKey = MagicLinkTokenHashCapabilityIndexProjection.OwnerAdmissionKey(
            issued.Tenant.TenantId, issued.CapabilityId.Value);
        ReadModelEntry<MagicLinkTokenHashCapabilityIndexOwnerAdmission> admitted;
        try
        {
            admitted = await coordinator.ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexOwnerAdmission>(
                CreateScope(request.TenantId), generation, ownerKey, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is DaprException or IOException or HttpRequestException
            or TimeoutException or TaskCanceledException)
        { throw new SharedProjectionReadUnavailableException(exception); }
        if (admitted.Value is not null) throw Park(delivery);
        if (current.Contributions.Any(pair => pair.Value.Contains(owner)))
        {
            throw Park(delivery);
        }
        ReadModelBatchOperation admission = ReadModelBatchOperation.Write(ownerKey,
            new MagicLinkTokenHashCapabilityIndexOwnerAdmission(hash, owner),
            ReadModelBatchConcurrency.LastWrite);
        if (current.CandidatePageCounts.TryGetValue(hash, out int pageCount) && pageCount > 0)
        {
            IReadOnlyList<int> pageIds = current.CandidatePageIds[hash];
            int lastPageId = pageIds[^1];
            string pageKey = MagicLinkTokenHashCapabilityIndexProjection.CandidatePageKey(shard, lastPageId);
            ReadModelEntry<MagicLinkTokenHashCapabilityIndexPage> page;
            try
            {
                page = await coordinator
                    .ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexPage>(CreateScope(request.TenantId),
                        generation, pageKey, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception) when (exception is DaprException or IOException or HttpRequestException
                or TimeoutException or TaskCanceledException)
            { throw new SharedProjectionReadUnavailableException(exception); }
            if (!MagicLinkTokenHashCapabilityIndexProjection.HasValidCandidatePage(current,
                hash, lastPageId, page.Value))
                throw new SharedProjectionReadUnavailableException(
                    new InvalidOperationException("A selected index contribution page is unavailable."));
            if (page.Value!.Candidates.Contains(owner)) throw Park(delivery);
            bool full = page.Value.Candidates.Count >= MagicLinkTokenHashCapabilityIndexProjection.CandidatesPerPage;
            int writePageId = full ? current.NextCandidatePageId : lastPageId;
            var nextPage = new MagicLinkTokenHashCapabilityIndexPage(hash, full
                ? [owner]
                : [.. page.Value.Candidates, owner]);
            var pageDigests = new Dictionary<int, string>(current.CandidatePageDigests)
            {
                [writePageId] = MagicLinkTokenHashCapabilityIndexProjection.CandidatePageDigest(nextPage)
            };
            var counts = new Dictionary<string, int>(current.CandidatePageCounts, StringComparer.Ordinal)
            {
                [hash] = full ? pageCount + 1 : pageCount
            };
            var pageIdsByHash = new Dictionary<string, IReadOnlyList<int>>(current.CandidatePageIds,
                StringComparer.Ordinal)
            {
                [hash] = full ? [.. pageIds, writePageId] : pageIds
            };
            return
            [
                admission,
                ReadModelBatchOperation.Write(
                    MagicLinkTokenHashCapabilityIndexProjection.CandidatePageKey(shard, writePageId),
                    nextPage, ReadModelBatchConcurrency.LastWrite),
                ReadModelBatchOperation.Write(key, current with
                    {
                        CandidatePageCounts = counts,
                        CandidatePageIds = pageIdsByHash,
                        CandidatePageDigests = pageDigests,
                        NextCandidatePageId = full ? checked(writePageId + 1) : current.NextCandidatePageId
                    },
                    ReadModelBatchConcurrency.LastWrite)
            ];
        }

        var inline = current.Contributions.TryGetValue(hash, out var previous)
            ? previous.ToList()
            : current.Entries.TryGetValue(hash, out var legacy) ? [legacy] : [];
        if (inline.Contains(owner)) throw Park(delivery);
        if (inline.Count == 0 && (current.Contributions.Count >= MagicLinkTokenHashCapabilityIndexProjection.InlineHashLimit
            || current.DirectoryPages.Count > 0))
        {
            return await AddDirectoryContributionAsync(current, shard, hash, owner, key, admission, delivery,
                coordinator, generation, cancellationToken).ConfigureAwait(false);
        }
        inline.Add(owner);
        var contributions = new Dictionary<string, IReadOnlyList<MagicLinkTokenHashCapabilityIndexEntry>>(
            current.Contributions, StringComparer.Ordinal);
        var entries = new Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry>(current.Entries,
            StringComparer.Ordinal);
        if (inline.Count <= MagicLinkTokenHashCapabilityIndexProjection.CandidatesPerPage)
        {
            contributions[hash] = inline;
            if (inline.Count == 1) entries[hash] = inline[0];
            else entries.Remove(hash);
            return [admission, ReadModelBatchOperation.Write(key, current with
            {
                Entries = entries,
                Contributions = contributions,
                LegacyContributionsUnverified = current.LegacyContributionsUnverified
                    || current.Entries.Count > 0 && current.Contributions.Count == 0
            }, ReadModelBatchConcurrency.LastWrite)];
        }
        contributions.Remove(hash);
        entries.Remove(hash);
        var pageCounts = new Dictionary<string, int>(current.CandidatePageCounts, StringComparer.Ordinal)
        {
            [hash] = 2
        };
        int firstPageId = current.NextCandidatePageId;
        int secondPageId = checked(firstPageId + 1);
        var pageIdsAfterSplit = new Dictionary<string, IReadOnlyList<int>>(current.CandidatePageIds,
            StringComparer.Ordinal)
        {
            [hash] = [firstPageId, secondPageId]
        };
        var firstPage = new MagicLinkTokenHashCapabilityIndexPage(hash, inline.Take(
            MagicLinkTokenHashCapabilityIndexProjection.CandidatesPerPage).ToArray());
        var secondPage = new MagicLinkTokenHashCapabilityIndexPage(hash, inline.Skip(
            MagicLinkTokenHashCapabilityIndexProjection.CandidatesPerPage).ToArray());
        var splitDigests = new Dictionary<int, string>(current.CandidatePageDigests)
        {
            [firstPageId] = MagicLinkTokenHashCapabilityIndexProjection.CandidatePageDigest(firstPage),
            [secondPageId] = MagicLinkTokenHashCapabilityIndexProjection.CandidatePageDigest(secondPage)
        };
        return
        [
            admission,
            ReadModelBatchOperation.Write(
                MagicLinkTokenHashCapabilityIndexProjection.CandidatePageKey(shard, firstPageId),
                firstPage,
                ReadModelBatchConcurrency.LastWrite),
            ReadModelBatchOperation.Write(
                MagicLinkTokenHashCapabilityIndexProjection.CandidatePageKey(shard, secondPageId),
                secondPage,
                ReadModelBatchConcurrency.LastWrite),
            ReadModelBatchOperation.Write(key, current with
            {
                Entries = entries,
                Contributions = contributions,
                CandidatePageCounts = pageCounts,
                CandidatePageIds = pageIdsAfterSplit,
                CandidatePageDigests = splitDigests,
                NextCandidatePageId = checked(secondPageId + 1),
                LegacyContributionsUnverified = current.LegacyContributionsUnverified
                    || current.Entries.Count > 0 && current.Contributions.Count == 0
            }, ReadModelBatchConcurrency.LastWrite)
        ];
    }

    private async Task<bool> HasSelectedLegacyIndexAsync(SharedProjectionScope scope,
        long generation, SharedProjectionEpochCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        ReadModelEntry<MagicLinkTokenHashCapabilityIndexReadModel> single = await coordinator
            .ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope, generation,
                MagicLinkTokenHashCapabilityIndexProjection.StateKey, cancellationToken)
            .ConfigureAwait(false);
        if (single.Value is not null)
            throw new SharedProjectionReadUnavailableException(
                new InvalidOperationException("A selected legacy single-root index requires migration."));
        ReadModelEntry<MagicLinkTokenHashCapabilityIndexReadModel> direct = await readModelStore
            .GetAsync<MagicLinkTokenHashCapabilityIndexReadModel>(RebuildStoreName,
                MagicLinkTokenHashCapabilityIndexProjection.StateKey, cancellationToken)
            .ConfigureAwait(false);
        if (direct.Value is not null)
            throw new SharedProjectionReadUnavailableException(
                new InvalidOperationException("A direct legacy single-root index requires migration."));
        // The first v7 issuance must inspect every possible legacy shard before
        // bootstrapping. Bound the concurrent state reads so this check fits the
        // live projection deadline without flooding the Dapr state store.
        const int shardReadParallelism = 16;
        for (int first = 0; first < MagicLinkTokenHashCapabilityIndexProjection.ShardCount;
            first += shardReadParallelism)
        {
            Task<ReadModelEntry<MagicLinkTokenHashCapabilityIndexReadModel>>[] reads =
                [.. Enumerable.Range(first, Math.Min(shardReadParallelism,
                        MagicLinkTokenHashCapabilityIndexProjection.ShardCount - first))
                    .Select(shard => coordinator
                        .ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope, generation,
                            MagicLinkTokenHashCapabilityIndexProjection.ShardKey(shard), cancellationToken))];
            ReadModelEntry<MagicLinkTokenHashCapabilityIndexReadModel>[] existing =
                await Task.WhenAll(reads).ConfigureAwait(false);
            if (existing.Any(entry => entry.Value is not null)) return true;
        }
        return false;
    }

    /// <inheritdoc />
    public async Task<DomainProjectionHandlerResult> ProjectAsync(
        ProjectionRequest request,
        string dispatchId,
        CancellationToken cancellationToken)
    {
        Validate(request, dispatchId);
        cancellationToken.ThrowIfCancellationRequested();

        MagicLinkConfirmationCapabilityIssued? issued;
        try
        {
            issued = FoldIssuance(request);
        }
        catch (InvalidOperationException)
        {
            return DomainProjectionHandlerResult.Failed(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }

        if (issued is null)
        {
            return DomainProjectionHandlerResult.AlreadyCompleted();
        }

        try
        {
            _ = await ReadModelWritePolicy.UpdateAsync<MagicLinkTokenHashCapabilityIndexReadModel>(
                readModelStore,
                RebuildStoreName,
                MagicLinkTokenHashCapabilityIndexProjection.StateKey,
                current => Guarded(() => ApplyChecked(current, issued)),
                new ReadModelWriteContext(SlotName, ProjectionType).WithEventDiagnostics(request.Events),
                _logger,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return DomainProjectionHandlerResult.Completed();
        }
        catch (IndexCandidateConflictException)
        {
            return DomainProjectionHandlerResult.Failed(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }
        catch (ProjectionFoldException)
        {
            // An apply defect is deterministic: reporting it as a transient store outage would have
            // the coordinator retry the same failing delivery forever. Store failures and an
            // exhausted retry budget stay Retryable below, even though they share CLR types.
            cancellationToken.ThrowIfCancellationRequested();
            return DomainProjectionHandlerResult.Failed(ProjectionDispatchReasonCodes.HandlerFailure);
        }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return DomainProjectionHandlerResult.Retryable(ProjectionDispatchReasonCodes.DeliveryStateUnavailable);
        }
    }

    /// <inheritdoc />
    public Task<DomainSharedProjectionRebuildCandidate> CreateEmptyCandidateAsync(
        DomainSharedProjectionRebuildIdentity identity,
        CancellationToken cancellationToken)
    {
        Validate(identity);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ToCandidate(new IndexRebuildCandidate([])));
    }

    /// <inheritdoc />
    public Task<DomainSharedProjectionRebuildCandidate> AccumulateAsync(
        DomainSharedProjectionRebuildIdentity identity,
        DomainSharedProjectionRebuildCandidate candidate,
        ProjectionRequest aggregateHistory,
        CancellationToken cancellationToken)
    {
        Validate(identity);
        ArgumentNullException.ThrowIfNull(candidate);
        Validate(aggregateHistory, identity.OperationId);
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(identity.TenantId, aggregateHistory.TenantId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The aggregate history is outside the rebuild tenant.");
        }

        IndexRebuildCandidate current = FromCandidate(candidate);
        MagicLinkConfirmationCapabilityIssued? issued = FoldIssuance(aggregateHistory);
        if (issued is null)
        {
            return Task.FromResult(candidate);
        }

        return Task.FromResult(ToCandidate(AddCandidate(current, issued)));
    }

    /// <inheritdoc />
    public async Task<DomainProjectionRebuildPlan> FinalizeAsync(
        DomainSharedProjectionRebuildIdentity identity,
        DomainSharedProjectionRebuildCandidate candidate,
        CancellationToken cancellationToken)
    {
        Validate(identity);
        ArgumentNullException.ThrowIfNull(candidate);
        cancellationToken.ThrowIfCancellationRequested();

        ReadModelEntry<MagicLinkTokenHashCapabilityIndexReadModel> current = await readModelStore
            .GetAsync<MagicLinkTokenHashCapabilityIndexReadModel>(
                RebuildStoreName,
                MagicLinkTokenHashCapabilityIndexProjection.StateKey,
                cancellationToken)
            .ConfigureAwait(false);
        MagicLinkTokenHashCapabilityIndexReadModel merged = MagicLinkTokenHashCapabilityIndexProjection.ReplaceTenant(
            current.Value,
            identity.TenantId,
            ToLoaderVisibleIndex(FromCandidate(candidate), identity.TenantId));

        return new DomainProjectionRebuildPlan(
            RebuildStoreName,
            [ReadModelBatchOperation.Write(
                MagicLinkTokenHashCapabilityIndexProjection.StateKey,
                merged,
                ReadModelRebuildConcurrency.For(current))]);
    }

    /// <inheritdoc />
    public Task<ReadOnlyMemory<byte>> PreparePartitionAsync(
        DomainSharedProjectionRebuildIdentity identity,
        ProjectionRequest aggregateHistory,
        CancellationToken cancellationToken)
    {
        Validate(identity);
        Validate(aggregateHistory, identity.OperationId);
        cancellationToken.ThrowIfCancellationRequested();
        MagicLinkConfirmationCapabilityIssued? issued = FoldIssuance(aggregateHistory);
        return Task.FromResult<ReadOnlyMemory<byte>>(JsonSerializer.SerializeToUtf8Bytes(
            ToOwnerContribution(issued), s_jsonOptions));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ReadOnlyMemory<byte>> StreamPartitionChunksAsync(
        DomainSharedProjectionRebuildIdentity identity,
        string aggregateId,
        IAsyncEnumerable<ProjectionEventDto> events,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Validate(identity);
        var owner = new ProjectionRequest(identity.TenantId, identity.Domain, aggregateId, []);
        Validate(owner, identity.OperationId);
        MagicLinkConfirmationCapabilityIssued? issued = null;
        await foreach (ProjectionEventDto item in events.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            MagicLinkConfirmationCapabilityIssued? next = FoldIssuance(owner with { Events = [item] });
            if (next is null) continue;
            if (issued is not null)
                throw new InvalidOperationException("One owner has multiple index issuances.");
            issued = next;
        }

        yield return JsonSerializer.SerializeToUtf8Bytes(ToOwnerContribution(issued), s_jsonOptions);
    }

    /// <inheritdoc />
    public Task<DomainProjectionRebuildPlan> FinalizePartitionsAsync(
        DomainSharedProjectionRebuildIdentity identity,
        IReadOnlyList<DomainSharedProjectionRebuildPartition> partitions,
        CancellationToken cancellationToken)
        => throw new NotSupportedException("Use the bounded streaming finalization contract.");

    /// <inheritdoc />
    public async IAsyncEnumerable<ReadModelBatchOperation> StreamFinalizedOperationsAsync(
        DomainSharedProjectionRebuildIdentity identity,
        IDomainSharedProjectionPartitionCursor partitions,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Validate(identity);
        ArgumentNullException.ThrowIfNull(partitions);
        SharedProjectionEpochCoordinator coordinator = _epochCoordinator
            ?? throw new InvalidOperationException("The global index epoch coordinator is unavailable.");
        var replacement = new Dictionary<int, List<KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>>>();
        await foreach (DomainSharedProjectionRebuildPartition partition in partitions.ReadAllAsync(
            cancellationToken).ConfigureAwait(false))
        {
            IndexRebuildCandidateEntry? item = JsonSerializer.Deserialize<IndexRebuildCandidateEntry?>(
                partition.State.Span, s_jsonOptions);
            if (item is null) continue;
            if (item.Candidate?.Tenant?.TenantId != identity.TenantId
                || item.Candidate.CapabilityId?.Value != partition.AggregateId)
                throw new InvalidOperationException("An index owner contribution has mismatched identity.");
            int shard = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(item.TokenHash))[0];
            if (!replacement.TryGetValue(shard, out var entries))
            {
                entries = [];
                replacement.Add(shard, entries);
            }
            entries.Add(new(item.TokenHash, item.Candidate));
        }

        SharedProjectionScope scope = CreateScope(identity.TenantId);
        SharedProjectionEpochStatus status = await coordinator.GetStatusAsync(scope, cancellationToken)
            .ConfigureAwait(false);
        if (!status.IsBuilding || !string.Equals(status.OperationId, identity.OperationId,
            StringComparison.Ordinal))
            throw new InvalidOperationException("The global index rebuild epoch is not reserved.");
        SharedProjectionReadResult<MagicLinkTokenHashCapabilityIndexTreeFormat> format = await coordinator
            .ReadAsync<MagicLinkTokenHashCapabilityIndexTreeFormat>(scope,
                MagicLinkTokenHashCapabilityIndexTreeReader.FormatKey, cancellationToken)
            .ConfigureAwait(false);
        SharedProjectionReadResult<MagicLinkTokenHashCapabilityIndexTreeRoot> selected = await coordinator
            .ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
                MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, cancellationToken)
            .ConfigureAwait(false);
        if (!format.IsAvailable || !selected.IsAvailable)
            throw new SharedProjectionReadUnavailableException(
                new InvalidOperationException("The selected numeric index root is unavailable."));
        MagicLinkTokenHashCapabilityIndexTreeRoot root;
        if (format.Value is null && selected.Value is null)
        {
            // A v6 selected index has no tenant roster. Its full-global migration must be
            // explicit; a tenant-only rebuild cannot safely discover stale owner rows.
            SharedProjectionReadResult<MagicLinkTokenHashCapabilityIndexReadModel> singleRoot = await coordinator
                .ReadAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope,
                    MagicLinkTokenHashCapabilityIndexProjection.StateKey, cancellationToken)
                .ConfigureAwait(false);
            if (!singleRoot.IsAvailable || singleRoot.Value is not null)
                throw new SharedProjectionReadUnavailableException(
                    new InvalidOperationException("A legacy single-root index requires a certified full migration."));
            ReadModelEntry<MagicLinkTokenHashCapabilityIndexReadModel> directSingleRoot = await readModelStore
                .GetAsync<MagicLinkTokenHashCapabilityIndexReadModel>(RebuildStoreName,
                    MagicLinkTokenHashCapabilityIndexProjection.StateKey, cancellationToken)
                .ConfigureAwait(false);
            if (directSingleRoot.Value is not null)
                throw new SharedProjectionReadUnavailableException(
                    new InvalidOperationException("A direct legacy index requires a certified full migration."));
            for (int shard = 0; shard < MagicLinkTokenHashCapabilityIndexProjection.ShardCount; shard++)
            {
                SharedProjectionReadResult<MagicLinkTokenHashCapabilityIndexReadModel> legacy = await coordinator
                    .ReadAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope,
                        MagicLinkTokenHashCapabilityIndexProjection.ShardKey(shard), cancellationToken)
                    .ConfigureAwait(false);
                if (!legacy.IsAvailable || legacy.Value is not null)
                    throw new SharedProjectionReadUnavailableException(
                        new InvalidOperationException("An existing index requires a certified full migration."));
            }
            root = new MagicLinkTokenHashCapabilityIndexTreeRoot(7, 0, null, null);
        }
        else if (format.Value?.StorageVersion == 7 && selected.Value is { StorageVersion: 7 } v7)
        {
            root = v7;
        }
        else
        {
            throw new SharedProjectionReadUnavailableException(
                new InvalidOperationException("The selected numeric index format is inconsistent."));
        }
        MagicLinkTokenHashCapabilityIndexDirectoryEntry[] tenantRows = [.. replacement.Values
            .SelectMany(static entries => entries.Select(static item =>
                new MagicLinkTokenHashCapabilityIndexDirectoryEntry(item.Key, item.Value)))];
        var reader = new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, scope);
        var builder = new MagicLinkTokenHashCapabilityIndexTreeBuilder(reader, root, status.Epoch);
        await builder.ReplaceTenantAsync(identity.TenantId, tenantRows, cancellationToken)
            .ConfigureAwait(false);
        foreach (ReadModelBatchOperation operation in builder.Operations())
            yield return operation;
    }

    private static async Task<IReadOnlyList<ReadModelBatchOperation>> AddDirectoryContributionAsync(
        MagicLinkTokenHashCapabilityIndexReadModel current,
        int shard,
        string hash,
        MagicLinkTokenHashCapabilityIndexEntry owner,
        string shardKey,
        ReadModelBatchOperation admission,
        SharedProjectionDelivery delivery,
        SharedProjectionEpochCoordinator coordinator,
        long generation,
        CancellationToken cancellationToken)
    {
        var references = current.DirectoryPages.ToList();
        var row = new MagicLinkTokenHashCapabilityIndexDirectoryEntry(hash, owner);
        if (references.Count == 0)
        {
            int pageId = current.NextCandidatePageId;
            references.Add(new(pageId, hash, hash));
            var newPage = new MagicLinkTokenHashCapabilityIndexDirectoryPage([row]);
            var digests = new Dictionary<int, string>(current.DirectoryPageDigests)
            {
                [pageId] = MagicLinkTokenHashCapabilityIndexProjection.DirectoryPageDigest(newPage)
            };
            return [admission,
                ReadModelBatchOperation.Write(
                    MagicLinkTokenHashCapabilityIndexProjection.DirectoryPageKey(shard, pageId),
                    newPage,
                    ReadModelBatchConcurrency.LastWrite),
                ReadModelBatchOperation.Write(shardKey, current with
                {
                    DirectoryPages = references,
                    DirectoryPageDigests = digests,
                    NextCandidatePageId = checked(pageId + 1)
                }, ReadModelBatchConcurrency.LastWrite)];
        }

        int index = references.FindIndex(reference => string.CompareOrdinal(reference.LastHash, hash) >= 0);
        if (index < 0) index = references.Count - 1;
        MagicLinkTokenHashCapabilityIndexDirectoryReference target;
        IReadOnlyList<MagicLinkTokenHashCapabilityIndexDirectoryEntry> existing;
        while (true)
        {
            target = references[index];
            ReadModelEntry<MagicLinkTokenHashCapabilityIndexDirectoryPage> selected;
            try
            {
                selected = await coordinator.ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexDirectoryPage>(
                    MagicLinkTokenHashCapabilityIndexProjection.GlobalScope(), generation,
                    MagicLinkTokenHashCapabilityIndexProjection.DirectoryPageKey(shard, target.PageId),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception) when (exception is DaprException or IOException or HttpRequestException
                or TimeoutException or TaskCanceledException)
            { throw new SharedProjectionReadUnavailableException(exception); }
            if (!MagicLinkTokenHashCapabilityIndexProjection.HasValidDirectoryPage(current,
                shard, target, selected.Value))
                throw new SharedProjectionReadUnavailableException(
                    new InvalidOperationException("A selected index directory page is unavailable."));
            existing = selected.Value!.Entries;
            // A hash can span pages. Select by the full owner row so an insertion
            // after one page cannot precede smaller owners on the next page.
            if (MagicLinkTokenHashCapabilityIndexProjection.CompareDirectoryRows(row, existing[^1]) <= 0
                || index == references.Count - 1
                || string.CompareOrdinal(references[index + 1].FirstHash, hash) > 0)
                break;
            index++;
        }
        if (existing.Contains(row)) throw Park(delivery);
        var rows = existing.Append(row)
            .OrderBy(static item => item.TokenHash, StringComparer.Ordinal)
            .ThenBy(static item => item.Candidate.Tenant.TenantId, StringComparer.Ordinal)
            .ThenBy(static item => item.Candidate.CapabilityId.Value, StringComparer.Ordinal).ToArray();
        if (rows.Length <= MagicLinkTokenHashCapabilityIndexProjection.CandidatesPerPage)
        {
            references[index] = new(target.PageId, rows[0].TokenHash, rows[^1].TokenHash);
            var updatedPage = new MagicLinkTokenHashCapabilityIndexDirectoryPage(rows);
            var digests = new Dictionary<int, string>(current.DirectoryPageDigests)
            {
                [target.PageId] = MagicLinkTokenHashCapabilityIndexProjection.DirectoryPageDigest(updatedPage)
            };
            return [admission,
                ReadModelBatchOperation.Write(
                    MagicLinkTokenHashCapabilityIndexProjection.DirectoryPageKey(shard, target.PageId),
                    updatedPage,
                    ReadModelBatchConcurrency.LastWrite),
                ReadModelBatchOperation.Write(shardKey, current with
                {
                    DirectoryPages = references,
                    DirectoryPageDigests = digests
                },
                    ReadModelBatchConcurrency.LastWrite)];
        }

        int split = rows.Length / 2;
        MagicLinkTokenHashCapabilityIndexDirectoryEntry[] left = rows[..split];
        MagicLinkTokenHashCapabilityIndexDirectoryEntry[] right = rows[split..];
        int newPageId = current.NextCandidatePageId;
        references[index] = new(target.PageId, left[0].TokenHash, left[^1].TokenHash);
        references.Insert(index + 1, new(newPageId, right[0].TokenHash, right[^1].TokenHash));
        var leftPage = new MagicLinkTokenHashCapabilityIndexDirectoryPage(left);
        var rightPage = new MagicLinkTokenHashCapabilityIndexDirectoryPage(right);
        var splitDigests = new Dictionary<int, string>(current.DirectoryPageDigests)
        {
            [target.PageId] = MagicLinkTokenHashCapabilityIndexProjection.DirectoryPageDigest(leftPage),
            [newPageId] = MagicLinkTokenHashCapabilityIndexProjection.DirectoryPageDigest(rightPage)
        };
        return [admission,
            ReadModelBatchOperation.Write(
                MagicLinkTokenHashCapabilityIndexProjection.DirectoryPageKey(shard, target.PageId),
                leftPage,
                ReadModelBatchConcurrency.LastWrite),
            ReadModelBatchOperation.Write(
                MagicLinkTokenHashCapabilityIndexProjection.DirectoryPageKey(shard, newPageId),
                rightPage,
                ReadModelBatchConcurrency.LastWrite),
            ReadModelBatchOperation.Write(shardKey, current with
            {
                DirectoryPages = references,
                DirectoryPageDigests = splitDigests,
                NextCandidatePageId = checked(newPageId + 1)
            }, ReadModelBatchConcurrency.LastWrite)];
    }

    private static IndexRebuildCandidateEntry? ToOwnerContribution(
        MagicLinkConfirmationCapabilityIssued? issued)
        => issued is null ? null : new IndexRebuildCandidateEntry(issued.TokenHash.Value,
            new MagicLinkTokenHashCapabilityIndexEntry(issued.Tenant, issued.CapabilityId));

    private static MagicLinkTokenHashCapabilityIndexReadModel ApplyChecked(
        MagicLinkTokenHashCapabilityIndexReadModel? current,
        MagicLinkConfirmationCapabilityIssued issued)
    {
        return MagicLinkTokenHashCapabilityIndexProjection.Apply(current, issued);
    }

    private static MagicLinkTokenHashCapabilityIndexReadModel Guarded(
        Func<MagicLinkTokenHashCapabilityIndexReadModel> apply)
    {
        try
        {
            return apply();
        }
        catch (Exception exception)
            when (exception is InvalidOperationException or ArgumentException or NullReferenceException)
        {
            throw new ProjectionFoldException(exception);
        }
    }

    private static MagicLinkConfirmationCapabilityIssued? FoldIssuance(ProjectionRequest request)
    {
        long firstSequence = request.Events is not { Length: > 0 } ? 1
            : request.Events.Min(static item => item is null ? 0 : item.SequenceNumber);
        MagicLinkConfirmationCapabilityIssued[] issuances = ProjectionEventReader.Normalize(
                request.Events, firstSequence)
            .Select(static projectionEvent =>
            {
                StoredMagicLinkIssued? wrapper = ProjectionEventReader.Deserialize<StoredMagicLinkIssued>(projectionEvent);
                if (wrapper is not null)
                    return wrapper.Event ?? throw new InvalidOperationException("Issuance wrapper is empty.");
                return ProjectionEventReader.Deserialize<MagicLinkConfirmationCapabilityIssued>(projectionEvent);
            })
            .Where(static item => item is not null)
            .Cast<MagicLinkConfirmationCapabilityIssued>()
            .ToArray();
        if (issuances.Length == 0)
        {
            return null;
        }

        if (issuances.Length != 1)
        {
            throw new InvalidOperationException("The issuance event does not match its projection scope.");
        }

        MagicLinkConfirmationCapabilityIssued issuance = issuances[0];
        MagicLinkConfirmationCapabilityProjectionHandler.ValidateIssued(issuance, request.TenantId);
        if (issuance.Tenant is null
            || string.IsNullOrWhiteSpace(issuance.Tenant.TenantId)
            || issuance.CapabilityId is null
            || string.IsNullOrWhiteSpace(issuance.CapabilityId.Value)
            || issuance.TokenHash is null
            || string.IsNullOrWhiteSpace(issuance.TokenHash.Value)
            || !string.Equals(issuance.Tenant.TenantId, request.TenantId, StringComparison.Ordinal)
            || !string.Equals(issuance.CapabilityId.Value, request.AggregateId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The issuance event does not match its projection scope.");
        }

        return issuance;
    }

    private static SharedProjectionFoldFailureException Park(SharedProjectionDelivery delivery)
    {
        string suffix = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            delivery.SourceStream + "\0" + delivery.EnvelopePosition.ToString(
                System.Globalization.CultureInfo.InvariantCulture))));
        return new SharedProjectionFoldFailureException(delivery.SourceStream,
            delivery.EnvelopePosition, 2,
            count => [ReadModelBatchOperation.Write("index:parked:" + suffix,
                new Dictionary<string, long> { ["position"] = delivery.EnvelopePosition, ["attempts"] = count },
                ReadModelBatchConcurrency.LastWrite)]);
    }

    private static IndexRebuildCandidate AddCandidate(
        IndexRebuildCandidate current,
        MagicLinkConfirmationCapabilityIssued issued)
        => CanonicalCandidate(current.Entries.Append(new IndexRebuildCandidateEntry(
            issued.TokenHash.Value,
            new MagicLinkTokenHashCapabilityIndexEntry(issued.Tenant, issued.CapabilityId))));

    private static IndexRebuildCandidate CanonicalCandidate(IEnumerable<IndexRebuildCandidateEntry> entries)
        => new(entries
            .Distinct()
            .OrderBy(static entry => entry.TokenHash, StringComparer.Ordinal)
            .ThenBy(static entry => entry.Candidate.Tenant.TenantId, StringComparer.Ordinal)
            .ThenBy(static entry => entry.Candidate.CapabilityId.Value, StringComparer.Ordinal)
            .ToArray());

    private static MagicLinkTokenHashCapabilityIndexReadModel ToLoaderVisibleIndex(
        IndexRebuildCandidate candidate,
        string tenantId)
    {
        if (candidate.Entries.Any(entry =>
                !string.Equals(entry.Candidate.Tenant.TenantId, tenantId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("The magic-link index candidate contains another tenant's entry.");
        }

        return MagicLinkTokenHashCapabilityIndexProjection.FromContributions(candidate.Entries
            .Select(static entry => new KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>(
                entry.TokenHash, entry.Candidate)));
    }

    private static DomainSharedProjectionRebuildCandidate ToCandidate(IndexRebuildCandidate candidate)
        => new(JsonSerializer.SerializeToUtf8Bytes(candidate, s_jsonOptions));

    private static IndexRebuildCandidate FromCandidate(
        DomainSharedProjectionRebuildCandidate candidate)
        => CanonicalCandidate(JsonSerializer.Deserialize<IndexRebuildCandidate>(
                candidate.State.Span,
                s_jsonOptions)
            ?.Entries
            ?? throw new InvalidOperationException("The magic-link index rebuild candidate is malformed."));

    private static void Validate(ProjectionRequest request, string dispatchId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(dispatchId);
        if (!string.Equals(request.Domain, TimesheetsEventStoreIntegration.DomainName, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(request.TenantId)
            || string.IsNullOrWhiteSpace(request.AggregateId))
        {
            throw new ArgumentException("The projection request is outside the Timesheets route.", nameof(request));
        }
    }

    private static void Validate(DomainSharedProjectionRebuildIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (!string.Equals(identity.Domain, TimesheetsEventStoreIntegration.DomainName, StringComparison.Ordinal)
            || !string.Equals(identity.ProjectionType, MagicLinkTokenHashCapabilityIndexProjection.ProjectionName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The shared rebuild identity targets a different projection.");
        }
    }
}
