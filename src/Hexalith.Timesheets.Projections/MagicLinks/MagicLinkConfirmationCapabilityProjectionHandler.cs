using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using Dapr;

using Hexalith.EventStore.Client.Attributes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Contracts;

namespace Hexalith.Timesheets.Projections.MagicLinks;

/// <summary>Materializes token-free capability displays through the shared epoch fence.</summary>
[EventStoreDomain(TimesheetsDomain.Name)]
public sealed class MagicLinkConfirmationCapabilityProjectionHandler :
    IAsyncDomainSharedProjectionScratchCleanupHandler,
    IAsyncDomainSharedProjectionStreamedOwnerHandler,
    IDeclaresProjectionReadModelSlots
{
    private const string WriterId = "timesheets-capability-projection";
    private const int InlineViewLimitBytes = 48 * 1024;
    private const int ViewFragmentBytes = 40 * 1024;
    private const int MaxPhysicalValueBytes = 64 * 1024;
    private const int OwnerContributionFragmentBytes = 384 * 1024;
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);
    private readonly IReadModelStore _store;

    /// <summary>Creates the capability writer over the SDK read-model store.</summary>
    /// <param name="store">The SDK read-model store.</param>
    public MagicLinkConfirmationCapabilityProjectionHandler(IReadModelStore store)
        => _store = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>Gets the canonical shared capability slot declaration.</summary>
    public static IReadOnlyList<ProjectionReadModelSlotDeclaration> ProjectionReadModelSlots { get; } =
    [
        new(
            TimesheetsDomain.Name,
            MagicLinkConfirmationCapabilityProjection.ProjectionName,
            MagicLinkCapabilityReadModelAddress.SlotName,
            ProjectionReadModelSlotKind.Shared,
            declaresCanonicalWriter: true)
    ];

    /// <inheritdoc />
    public string Domain => TimesheetsDomain.Name;

    /// <inheritdoc />
    public string ProjectionType => MagicLinkConfirmationCapabilityProjection.ProjectionName;

    /// <inheritdoc />
    public string RebuildStoreName => MagicLinkCapabilityReadModelAddress.StateStoreName;

    /// <inheritdoc />
    public bool EpochEnabled => true;

    /// <inheritdoc />
    public string OrdinaryWriterId => WriterId;

    /// <inheritdoc />
    public SharedProjectionScope CreateScope(string tenantId)
        => new(RebuildStoreName, new AggregateIdentity(tenantId, Domain, "capability").TenantId,
            Domain, ProjectionType, [WriterId]);

    /// <summary>Reads a generation-selected display with expiry evaluated at observation time.</summary>
    public async Task<SharedProjectionReadResult<MagicLinkConfirmationCapabilityReadModel>> ReadObservedAsync(
        string tenantId,
        MagicLinkCapabilityId capabilityId,
        DateTimeOffset observedAtUtc,
        SharedProjectionEpochCoordinator coordinator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        SharedProjectionReadResult<MagicLinkCapabilityProjectionView> selected = await ReadViewAsync(
            tenantId, capabilityId, coordinator, cancellationToken).ConfigureAwait(false);
        return new SharedProjectionReadResult<MagicLinkConfirmationCapabilityReadModel>(
            selected.IsAvailable && !selected.IsStale ? selected.Value?.Observe(observedAtUtc) : null,
            selected.Generation,
            selected.IsStale,
            selected.IsAvailable);
    }

    /// <summary>Reads the complete capability contribution from one selected generation.</summary>
    public async Task<SharedProjectionReadResult<MagicLinkCapabilityProjectionView>> ReadViewAsync(
        string tenantId,
        MagicLinkCapabilityId capabilityId,
        SharedProjectionEpochCoordinator coordinator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        string key = MagicLinkCapabilityReadModelAddress.StateKey(tenantId, capabilityId);
        SharedProjectionScope scope = CreateScope(tenantId);
        for (int attempt = 0; attempt < 4; attempt++)
        {
            SharedProjectionReadResult<MagicLinkCapabilityProjectionStorage> selected = await ReadSelectedStorageAsync(
                coordinator, scope, key, cancellationToken).ConfigureAwait(false);
            if (!selected.IsAvailable || selected.Value is null)
            {
                return new SharedProjectionReadResult<MagicLinkCapabilityProjectionView>(
                    null, selected.Generation, selected.IsStale, selected.IsAvailable);
            }

            MagicLinkCapabilityProjectionView? value = null;
            Exception? restoreFailure = null;
            try
            {
                value = await RestoreViewAsync(scope, capabilityId, selected.Generation, selected.Value,
                    coordinator, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                restoreFailure = exception;
            }

            SharedProjectionReadResult<MagicLinkCapabilityProjectionStorage> after = await ReadSelectedStorageAsync(
                coordinator, scope, key, cancellationToken).ConfigureAwait(false);
            if (after.Generation != selected.Generation
                || !StringComparer.Ordinal.Equals(after.SelectedETag, selected.SelectedETag)
                || after.IsAvailable != selected.IsAvailable || after.IsStale != selected.IsStale)
            {
                continue;
            }

            if (restoreFailure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(restoreFailure).Throw();
            return new SharedProjectionReadResult<MagicLinkCapabilityProjectionView>(
                value, selected.Generation, selected.IsStale, selected.IsAvailable)
                { SelectedETag = selected.SelectedETag };
        }

        throw new SharedProjectionReadUnavailableException(
            new InvalidOperationException("The selected capability epoch changed during fragment read."));
    }

    private static async Task<SharedProjectionReadResult<MagicLinkCapabilityProjectionStorage>> ReadSelectedStorageAsync(
        SharedProjectionEpochCoordinator coordinator,
        SharedProjectionScope scope,
        string key,
        CancellationToken cancellationToken)
    {
        try
        {
            return await coordinator.ReadAsync<MagicLinkCapabilityProjectionStorage>(scope, key, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is DaprException or HttpRequestException or IOException
            or TimeoutException or TaskCanceledException)
        {
            throw new SharedProjectionReadUnavailableException(exception);
        }
    }

    /// <inheritdoc />
    public Task<DomainProjectionHandlerResult> ProjectAsync(
        ProjectionRequest request,
        string dispatchId,
        CancellationToken cancellationToken)
    {
        Validate(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(dispatchId);
        cancellationToken.ThrowIfCancellationRequested();
        // Direct writes cannot atomically update every capability in a multi-event request.
        // The SDK normally bypasses this method for epoch-enabled handlers.
        return Task.FromResult(DomainProjectionHandlerResult.Failed(ProjectionDispatchReasonCodes.HandlerFailure));
    }

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
        (object Payload, bool OwnerStream)? decoded;
        try
        {
            request = JsonSerializer.Deserialize<ProjectionRequest>(delivery.CanonicalPayload, s_json)
                ?? throw new InvalidOperationException("The capability delivery is empty.");
            Validate(request);
            if (!string.Equals(request.AggregateId, delivery.SourceStream, StringComparison.Ordinal)
                || request.Events.Length != 1)
            {
                throw new InvalidOperationException("The capability delivery has an invalid owner envelope.");
            }

            decoded = Decode(request.Events[0]);
            if (decoded is not null)
            {
                ValidateOwner(request, decoded.Value);
            }
        }
        catch (Exception exception) when (IsFoldFailure(exception))
        {
            throw Park(delivery);
        }

        if (decoded is null)
        {
            return [];
        }

        string key;
        try
        {
            key = MagicLinkCapabilityReadModelAddress.StateKey(request.TenantId, IdOf(decoded.Value.Payload));
        }
        catch (Exception exception) when (IsFoldFailure(exception))
        {
            throw Park(delivery);
        }

        SharedProjectionScope scope = CreateScope(request.TenantId);
        ReadModelEntry<MagicLinkCapabilityProjectionStorage> entry;
        try
        {
            entry = await coordinator
                .ReadGenerationAsync<MagicLinkCapabilityProjectionStorage>(scope, generation, key, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new SharedProjectionReadUnavailableException(exception);
        }

        MagicLinkCapabilityProjectionView? current;
        try
        {
            current = entry.Value is { } storage
                ? await RestoreViewAsync(scope, IdOf(decoded.Value.Payload), generation, storage, coordinator, cancellationToken)
                    .ConfigureAwait(false)
                : null;
        }
        catch (SharedProjectionReadUnavailableException)
        {
            throw;
        }
        catch (Exception exception) when (IsFoldFailure(exception))
        {
            // A valid journal event must remain retryable when previously selected storage is corrupt.
            // Parking it would acknowledge and discard the event even after the storage is repaired.
            throw new SharedProjectionReadUnavailableException(exception);
        }
        catch (Exception exception)
        {
            throw new SharedProjectionReadUnavailableException(exception);
        }
        MagicLinkCapabilityProjectionView next;
        try
        {
            next = Apply(
                current ?? MagicLinkCapabilityProjectionView.Empty,
                decoded.Value.Payload,
                decoded.Value.OwnerStream,
                request.TenantId,
                delivery.EnvelopePosition);
        }
        catch (Exception exception) when (IsFoldFailure(exception))
        {
            throw Park(delivery);
        }
        try
        {
            return WriteLiveView(request.TenantId, IdOf(decoded.Value.Payload), next, entry.Value, decoded.Value);
        }
        catch (Exception exception) when (IsFoldFailure(exception))
        {
            // Unsupported storage capacity must preserve the accepted journal head for recovery.
            throw new SharedProjectionReadUnavailableException(exception);
        }
    }

    /// <inheritdoc />
    public Task<DomainSharedProjectionRebuildCandidate> CreateEmptyCandidateAsync(
        DomainSharedProjectionRebuildIdentity identity,
        CancellationToken cancellationToken)
    {
        Validate(identity);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new DomainSharedProjectionRebuildCandidate(Array.Empty<byte>()));
    }

    /// <inheritdoc />
    public Task<DomainSharedProjectionRebuildCandidate> AccumulateAsync(
        DomainSharedProjectionRebuildIdentity identity,
        DomainSharedProjectionRebuildCandidate candidate,
        ProjectionRequest aggregateHistory,
        CancellationToken cancellationToken)
        => throw new NotSupportedException("Use the partitioned rebuild contract.");

    /// <inheritdoc />
    public Task<DomainProjectionRebuildPlan> FinalizeAsync(
        DomainSharedProjectionRebuildIdentity identity,
        DomainSharedProjectionRebuildCandidate candidate,
        CancellationToken cancellationToken)
        => throw new NotSupportedException("Use the partitioned rebuild contract.");

    /// <inheritdoc />
    public Task<ReadOnlyMemory<byte>> PreparePartitionAsync(
        DomainSharedProjectionRebuildIdentity identity,
        ProjectionRequest aggregateHistory,
        CancellationToken cancellationToken)
        => Task.FromResult<ReadOnlyMemory<byte>>(JsonSerializer.SerializeToUtf8Bytes(
            FoldOwner(identity, aggregateHistory, cancellationToken), s_json));

    /// <inheritdoc />
    public async IAsyncEnumerable<string> EnumerateScratchKeysAsync(
        DomainSharedProjectionRebuildIdentity identity,
        IDomainSharedProjectionPartitionCursor partitions,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Validate(identity);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        await foreach (OwnerContribution contribution in ReadContributionsAsync(partitions, cancellationToken)
            .ConfigureAwait(false))
        {
            string key = ContributionIndexKey(identity, contribution.CapabilityId);
            if (seen.Add(key))
            {
                yield return key;
            }

            string markerKey = partitions.ScratchMarkerKey(ContributionBatchId(identity, contribution));
            if (seen.Add(markerKey))
            {
                yield return markerKey;
            }
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ReadOnlyMemory<byte>>> PreparePartitionChunksAsync(
        DomainSharedProjectionRebuildIdentity identity,
        ProjectionRequest aggregateHistory,
        CancellationToken cancellationToken)
    {
        Dictionary<string, MagicLinkCapabilityProjectionView> owner = FoldOwner(identity, aggregateHistory, cancellationToken);
        var chunks = new List<ReadOnlyMemory<byte>>();
        foreach ((string key, MagicLinkCapabilityProjectionView view) in owner.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(view, s_json);
            int count = (bytes.Length + OwnerContributionFragmentBytes - 1) / OwnerContributionFragmentBytes;
            for (int ordinal = 0; ordinal < count; ordinal++)
            {
                chunks.Add(JsonSerializer.SerializeToUtf8Bytes(new OwnerContributionChunk(key, ordinal, count,
                    bytes.AsSpan(ordinal * OwnerContributionFragmentBytes,
                        Math.Min(OwnerContributionFragmentBytes, bytes.Length - ordinal * OwnerContributionFragmentBytes)).ToArray()), s_json));
            }
        }

        if (chunks.Count == 0)
        {
            chunks.Add(JsonSerializer.SerializeToUtf8Bytes(new OwnerContributionChunk(string.Empty, 0, 1, []), s_json));
        }

        return Task.FromResult<IReadOnlyList<ReadOnlyMemory<byte>>>(chunks);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ReadOnlyMemory<byte>> StreamPartitionChunksAsync(
        DomainSharedProjectionRebuildIdentity identity,
        string aggregateId,
        IAsyncEnumerable<ProjectionEventDto> events,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Validate(identity);
        var ownerRequest = new ProjectionRequest(identity.TenantId, identity.Domain, aggregateId, []);
        Validate(ownerRequest);
        MagicLinkCapabilityProjectionView? capabilityView = null;
        string? capabilityId = null;
        bool emitted = false;
        await foreach (ProjectionEventDto item in events.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            (object Payload, bool OwnerStream)? decoded = Decode(item);
            if (decoded is null) continue;
            ValidateOwner(ownerRequest, decoded.Value);
            MagicLinkCapabilityId id = IdOf(decoded.Value.Payload);
            long position = item.GlobalPosition > 0 ? item.GlobalPosition : item.SequenceNumber;
            if (decoded.Value.OwnerStream)
            {
                // A Time Entry terminal fact owns one capability. Emit each contribution
                // immediately; the SDK contribution index rejects duplicate terminal owners.
                MagicLinkCapabilityProjectionView view = Apply(MagicLinkCapabilityProjectionView.Empty,
                    decoded.Value.Payload, true, identity.TenantId, position);
                foreach (ReadOnlyMemory<byte> chunk in SerializeOwnerContribution(id.Value, view))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    emitted = true;
                    yield return chunk;
                }
            }
            else
            {
                if (capabilityId is not null && capabilityId != id.Value)
                    throw new InvalidOperationException("One capability stream contains another capability ID.");
                capabilityId = id.Value;
                capabilityView = Apply(capabilityView ?? MagicLinkCapabilityProjectionView.Empty,
                    decoded.Value.Payload, false, identity.TenantId, position);
            }
        }

        if (capabilityView is not null)
        {
            foreach (ReadOnlyMemory<byte> chunk in SerializeOwnerContribution(capabilityId!, capabilityView))
            {
                cancellationToken.ThrowIfCancellationRequested();
                emitted = true;
                yield return chunk;
            }
        }

        if (!emitted)
            yield return JsonSerializer.SerializeToUtf8Bytes(new OwnerContributionChunk(string.Empty, 0, 1, []), s_json);
    }

    private static IEnumerable<ReadOnlyMemory<byte>> SerializeOwnerContribution(
        string key, MagicLinkCapabilityProjectionView view)
    {
        // The contribution bytes are Base64-encoded inside a chunk and again
        // inside its fenced scratch batch. Both serialized layers fit 1 MiB.
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(view, s_json);
        int count = (bytes.Length + OwnerContributionFragmentBytes - 1) / OwnerContributionFragmentBytes;
        for (int ordinal = 0; ordinal < count; ordinal++)
            yield return JsonSerializer.SerializeToUtf8Bytes(new OwnerContributionChunk(key, ordinal, count,
                bytes.AsSpan(ordinal * OwnerContributionFragmentBytes,
                    Math.Min(OwnerContributionFragmentBytes, bytes.Length - ordinal * OwnerContributionFragmentBytes)).ToArray()), s_json);
    }

    /// <summary>Streams final mutations after indexing each capability's bounded owner locations.</summary>
    /// <inheritdoc />
    public async IAsyncEnumerable<ReadModelBatchOperation> StreamFinalizedOperationsAsync(
        DomainSharedProjectionRebuildIdentity identity,
        IDomainSharedProjectionPartitionCursor partitions,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Validate(identity);
        ArgumentNullException.ThrowIfNull(partitions);
        await foreach (OwnerContribution contribution in ReadContributionsAsync(partitions, cancellationToken).ConfigureAwait(false))
        {
            if (contribution.CapabilityId.Length == 0) continue;
            MagicLinkCapabilityProjectionView view = DeserializeContribution(contribution);
            ValidateViewIdentity(identity.TenantId, contribution.CapabilityId, view);
            await IndexContributionAsync(identity, partitions, contribution, view, cancellationToken).ConfigureAwait(false);
        }

        await foreach (OwnerContribution contribution in ReadContributionsAsync(partitions, cancellationToken).ConfigureAwait(false))
        {
            if (contribution.CapabilityId.Length == 0) continue;
            ContributionIndex index = await ReadContributionIndexAsync(identity, contribution.CapabilityId, cancellationToken)
                .ConfigureAwait(false);
            MagicLinkCapabilityProjectionView observed = DeserializeContribution(contribution);
            ValidateViewIdentity(identity.TenantId, contribution.CapabilityId, observed);
            if (observed.Issuance is not null && index.Issuance != contribution.Location
                || HasTerminal(observed) && index.Terminal != contribution.Location)
                throw new InvalidOperationException("A capability contribution index failed readback verification.");
            if (index.Issuance is { } issueLocation)
            {
                MagicLinkCapabilityProjectionView issuedView = await RestoreContributionAsync(partitions, issueLocation,
                    contribution.CapabilityId, cancellationToken).ConfigureAwait(false);
                ValidateViewIdentity(identity.TenantId, contribution.CapabilityId, issuedView);
                if (issuedView.Issuance is null) throw new InvalidOperationException("An indexed issuance is missing.");
            }
            if (index.Terminal is { } terminalLocation)
            {
                MagicLinkCapabilityProjectionView terminalView = await RestoreContributionAsync(partitions, terminalLocation,
                    contribution.CapabilityId, cancellationToken).ConfigureAwait(false);
                ValidateViewIdentity(identity.TenantId, contribution.CapabilityId, terminalView);
                if (!HasTerminal(terminalView)) throw new InvalidOperationException("An indexed terminal fact is missing.");
            }
            ContributionLocation first = Earlier(index.Issuance, index.Terminal)
                ?? throw new InvalidOperationException("A capability has no indexed contribution.");
            if (first != contribution.Location) continue;
            MagicLinkCapabilityProjectionView view = observed;
            if (index.Issuance is { } issued && issued != contribution.Location)
            {
                MagicLinkCapabilityProjectionView other = await RestoreContributionAsync(partitions, issued,
                    contribution.CapabilityId, cancellationToken).ConfigureAwait(false);
                view = Merge(view, other, identity.TenantId);
            }
            if (index.Terminal is { } terminal && terminal != contribution.Location
                && terminal != index.Issuance)
            {
                MagicLinkCapabilityProjectionView other = await RestoreContributionAsync(partitions, terminal,
                    contribution.CapabilityId, cancellationToken).ConfigureAwait(false);
                view = Merge(view, other, identity.TenantId);
            }

            ValidateViewIdentity(identity.TenantId, contribution.CapabilityId, view);
            foreach (ReadModelBatchOperation operation in WriteView(identity.TenantId,
                new MagicLinkCapabilityId(contribution.CapabilityId), view))
            {
                yield return operation;
            }
        }

        yield return ReadModelBatchOperation.Write($"capability-rebuild-marker:{identity.TenantId}",
            identity.OperationId, ReadModelBatchConcurrency.LastWrite);
    }

    private async Task IndexContributionAsync(DomainSharedProjectionRebuildIdentity identity,
        IDomainSharedProjectionPartitionCursor partitions,
        OwnerContribution contribution, MagicLinkCapabilityProjectionView view, CancellationToken cancellationToken)
    {
        bool hasIssuance = view.Issuance is not null;
        bool hasTerminal = HasTerminal(view);
        if (!hasIssuance && !hasTerminal)
            throw new InvalidOperationException("A capability owner has no issuance or terminal fact.");
        string key = ContributionIndexKey(identity, contribution.CapabilityId);
        for (int attempt = 0; attempt < 64; attempt++)
        {
            ReadModelEntry<ContributionIndex> entry = await _store.GetAsync<ContributionIndex>(RebuildStoreName, key,
                cancellationToken).ConfigureAwait(false);
            ContributionIndex current = entry.Value ?? new(null, null);
            if (hasIssuance && current.Issuance is { } oldIssue && oldIssue != contribution.Location
                || hasTerminal && current.Terminal is { } oldTerminal && oldTerminal != contribution.Location)
                throw new InvalidOperationException("A capability has conflicting owner contributions.");
            ContributionIndex updated = new(hasIssuance ? contribution.Location : current.Issuance,
                hasTerminal ? contribution.Location : current.Terminal);
            if (updated == current) return;
            ReadModelBatchConcurrency concurrency = entry.ETag is { Length: > 0 } etag
                ? ReadModelBatchConcurrency.Match(etag)
                : ReadModelBatchConcurrency.CreateOnly;
            if (await partitions.TryWriteScratchAsync(key, updated,
                ContributionBatchId(identity, contribution), concurrency, cancellationToken).ConfigureAwait(false)) return;
        }

        throw new InvalidOperationException("A capability contribution index exceeded its retry limit.");
    }

    private static string ContributionBatchId(DomainSharedProjectionRebuildIdentity identity,
        OwnerContribution contribution)
        => "scratch-index:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            identity.OperationId + "\0" + contribution.CapabilityId + "\0"
            + contribution.Location.OwnerOrdinal + ":" + contribution.Location.FirstChunkOrdinal)));

    private async Task<ContributionIndex> ReadContributionIndexAsync(DomainSharedProjectionRebuildIdentity identity,
        string capabilityId, CancellationToken cancellationToken)
        => (await _store.GetAsync<ContributionIndex>(RebuildStoreName,
            ContributionIndexKey(identity, capabilityId), cancellationToken).ConfigureAwait(false)).Value
            ?? throw new InvalidOperationException("A capability contribution index is missing.");

    private static string ContributionIndexKey(DomainSharedProjectionRebuildIdentity identity, string capabilityId)
        => "capability-rebuild-index:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            identity.TenantId + "\0" + identity.OperationId + "\0" + capabilityId)));

    private static ContributionLocation? Earlier(ContributionLocation? left, ContributionLocation? right)
    {
        if (left is null) return right;
        if (right is null) return left;
        return left.OwnerOrdinal < right.OwnerOrdinal
            || left.OwnerOrdinal == right.OwnerOrdinal && left.FirstChunkOrdinal <= right.FirstChunkOrdinal ? left : right;
    }

    private static async Task<MagicLinkCapabilityProjectionView> RestoreContributionAsync(
        IDomainSharedProjectionPartitionCursor partitions, ContributionLocation location,
        string capabilityId, CancellationToken cancellationToken)
    {
        IReadOnlyList<DomainSharedProjectionRebuildPartition> chunks = await partitions.ReadRangeAsync(
            location.OwnerOrdinal, location.FirstChunkOrdinal, location.ChunkCount, cancellationToken).ConfigureAwait(false);
        OwnerContribution contribution = DecodeContribution(chunks);
        if (contribution.CapabilityId != capabilityId || contribution.Location != location)
            throw new InvalidOperationException("A capability contribution location conflicts with its owner chunks.");
        return DeserializeContribution(contribution);
    }

    private static MagicLinkCapabilityProjectionView DeserializeContribution(OwnerContribution contribution)
        => JsonSerializer.Deserialize<MagicLinkCapabilityProjectionView>(contribution.Bytes, s_json)
            ?? throw new InvalidOperationException("A capability owner contribution is empty.");

    private static async IAsyncEnumerable<OwnerContribution> ReadContributionsAsync(
        IDomainSharedProjectionPartitionCursor partitions,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var current = new List<DomainSharedProjectionRebuildPartition>();
        int expected = 0;
        await foreach (DomainSharedProjectionRebuildPartition chunk in partitions.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            OwnerContributionChunk part = JsonSerializer.Deserialize<OwnerContributionChunk>(chunk.State.Span, s_json)
                ?? throw new InvalidOperationException("A capability owner chunk is empty.");
            if (current.Count == 0)
            {
                if (part.PartOrdinal != 0 || part.PartCount <= 0)
                    throw new InvalidOperationException("A capability owner chunk starts at an invalid part.");
                expected = part.PartCount;
            }
            else
            {
                OwnerContributionChunk first = JsonSerializer.Deserialize<OwnerContributionChunk>(current[0].State.Span, s_json)!;
                if (chunk.Ordinal != current[0].Ordinal || part.CapabilityId != first.CapabilityId
                    || part.PartCount != expected || part.PartOrdinal != current.Count
                    || chunk.ChunkOrdinal != current[0].ChunkOrdinal + current.Count)
                    throw new InvalidOperationException("A capability owner contribution is out of order.");
            }

            current.Add(chunk);
            if (current.Count == expected)
            {
                yield return DecodeContribution(current);
                current.Clear();
            }
        }

        if (current.Count != 0) throw new InvalidOperationException("A capability owner contribution is incomplete.");
    }

    private static OwnerContribution DecodeContribution(IReadOnlyList<DomainSharedProjectionRebuildPartition> chunks)
    {
        if (chunks.Count == 0) throw new InvalidOperationException("A capability owner contribution has no chunks.");
        using var bytes = new MemoryStream();
        string? capability = null;
        for (int ordinal = 0; ordinal < chunks.Count; ordinal++)
        {
            OwnerContributionChunk part = JsonSerializer.Deserialize<OwnerContributionChunk>(chunks[ordinal].State.Span, s_json)
                ?? throw new InvalidOperationException("A capability owner chunk is empty.");
            if (part.PartOrdinal != ordinal || part.PartCount != chunks.Count || part.Data is null
                || capability is not null && capability != part.CapabilityId)
                throw new InvalidOperationException("A capability owner contribution conflicts with its chunk sequence.");
            capability = part.CapabilityId;
            bytes.Write(part.Data);
        }

        return new OwnerContribution(capability!,
            new ContributionLocation(chunks[0].Ordinal, chunks[0].ChunkOrdinal, chunks.Count), bytes.ToArray());
    }

    private static void ValidateViewIdentity(string tenantId, string capabilityId, MagicLinkCapabilityProjectionView view)
        => ValidateStoredView(view, tenantId, new MagicLinkCapabilityId(capabilityId));

    private sealed record OwnerContributionChunk(string CapabilityId, int PartOrdinal, int PartCount, byte[] Data);
    private sealed record ContributionLocation(long OwnerOrdinal, int FirstChunkOrdinal, int ChunkCount);
    private sealed record ContributionIndex(ContributionLocation? Issuance, ContributionLocation? Terminal);
    private sealed record OwnerContribution(string CapabilityId, ContributionLocation Location, byte[] Bytes);

    private static Dictionary<string, MagicLinkCapabilityProjectionView> FoldOwner(
        DomainSharedProjectionRebuildIdentity identity,
        ProjectionRequest aggregateHistory,
        CancellationToken cancellationToken)
    {
        Validate(identity);
        Validate(aggregateHistory);
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(identity.TenantId, aggregateHistory.TenantId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The owner is outside the rebuild tenant.");
        }

        var views = new Dictionary<string, MagicLinkCapabilityProjectionView>(StringComparer.Ordinal);
        foreach (ProjectionEventDto item in ProjectionEventReader.Normalize(aggregateHistory.Events))
        {
            (object Payload, bool OwnerStream)? decoded = Decode(item);
            if (decoded is null)
            {
                continue;
            }

            ValidateOwner(aggregateHistory, decoded.Value);
            MagicLinkCapabilityId id = IdOf(decoded.Value.Payload);
            views.TryGetValue(id.Value, out MagicLinkCapabilityProjectionView? current);
            views[id.Value] = Apply(
                current ?? MagicLinkCapabilityProjectionView.Empty,
                decoded.Value.Payload,
                decoded.Value.OwnerStream,
                identity.TenantId,
                item.GlobalPosition > 0 ? item.GlobalPosition : item.SequenceNumber);
        }

        return views;
    }

    /// <inheritdoc />
    public Task<DomainProjectionRebuildPlan> FinalizePartitionsAsync(
        DomainSharedProjectionRebuildIdentity identity,
        IReadOnlyList<DomainSharedProjectionRebuildPartition> partitions,
        CancellationToken cancellationToken)
    {
        Validate(identity);
        ArgumentNullException.ThrowIfNull(partitions);
        cancellationToken.ThrowIfCancellationRequested();
        var all = new Dictionary<string, MagicLinkCapabilityProjectionView>(StringComparer.Ordinal);
        foreach (IGrouping<string, DomainSharedProjectionRebuildPartition> group in partitions
            .GroupBy(static partition => partition.AggregateId, StringComparer.Ordinal))
        {
            DomainSharedProjectionRebuildPartition[] ordered = [.. group.OrderBy(static partition => partition.ChunkOrdinal)];
            Dictionary<string, MagicLinkCapabilityProjectionView> owner;
            using (JsonDocument first = JsonDocument.Parse(ordered[0].State))
            {
                if (first.RootElement.TryGetProperty("capabilityId", out _))
                {
                    owner = new Dictionary<string, MagicLinkCapabilityProjectionView>(StringComparer.Ordinal);
                    for (int offset = 0; offset < ordered.Length;)
                    {
                        OwnerContributionChunk head = JsonSerializer.Deserialize<OwnerContributionChunk>(ordered[offset].State.Span, s_json)!;
                        OwnerContribution contribution = DecodeContribution(ordered.AsSpan(offset, head.PartCount).ToArray());
                        if (contribution.CapabilityId.Length != 0)
                            owner.Add(contribution.CapabilityId, DeserializeContribution(contribution));
                        offset += head.PartCount;
                    }
                }
                else
                {
                    byte[] ownerBytes = ordered.SelectMany(static partition => partition.State.ToArray()).ToArray();
                    owner = JsonSerializer.Deserialize<Dictionary<string, MagicLinkCapabilityProjectionView>>(
                        ownerBytes, s_json) ?? throw new InvalidOperationException("An owner partition is missing.");
                }
            }
            foreach ((string key, MagicLinkCapabilityProjectionView view) in owner)
            {
                all.TryGetValue(key, out MagicLinkCapabilityProjectionView? previous);
                all[key] = previous is null ? view : Merge(previous, view, identity.TenantId);
            }
        }

        var operations = new List<ReadModelBatchOperation>(all.Count + 1);
        foreach ((string key, MagicLinkCapabilityProjectionView view) in all.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            ValidateViewIdentity(identity.TenantId, key, view);
            operations.AddRange(WriteView(identity.TenantId, new MagicLinkCapabilityId(key), view));
        }

        // A marker also makes an empty replacement manifest explicit and deterministic.
        operations.Add(ReadModelBatchOperation.Write(
            $"capability-rebuild-marker:{identity.TenantId}",
            identity.OperationId,
            ReadModelBatchConcurrency.LastWrite));
        return Task.FromResult(new DomainProjectionRebuildPlan(RebuildStoreName, operations));
    }

    private static MagicLinkCapabilityProjectionView Merge(
        MagicLinkCapabilityProjectionView left,
        MagicLinkCapabilityProjectionView right,
        string tenantId)
    {
        if (left.Issuance is not null && right.Issuance is not null)
        {
            throw new InvalidOperationException("A capability has more than one issuance owner.");
        }

        if (HasTerminal(left) && HasTerminal(right))
        {
            throw new InvalidOperationException("A capability has multiple terminal transitions.");
        }

        MagicLinkCapabilityProjectionView merged = left with
        {
            Issuance = left.Issuance ?? right.Issuance,
            Used = left.Used ?? right.Used,
            Revoked = left.Revoked ?? right.Revoked,
            Expired = left.Expired ?? right.Expired,
            TerminalInCapabilityStream = left.TerminalInCapabilityStream || right.TerminalInCapabilityStream,
            TerminalPosition = left.TerminalPosition ?? right.TerminalPosition
        };
        if (merged.TerminalInCapabilityStream && left.Issuance is null && right.Issuance is not null)
        {
            throw new InvalidOperationException("Legacy terminal transition precedes issuance.");
        }

        return Publish(merged, tenantId);
    }

    private static IReadOnlyList<ReadModelBatchOperation> WriteLiveView(
        string tenantId, MagicLinkCapabilityId capabilityId, MagicLinkCapabilityProjectionView view,
        MagicLinkCapabilityProjectionStorage? previous, (object Payload, bool OwnerStream) decoded)
    {
        byte[] viewBytes = JsonSerializer.SerializeToUtf8Bytes(view, s_json);
        if (viewBytes.Length > 127L * ViewFragmentBytes)
            throw new InvalidOperationException("A capability view exceeds the supported storage limit.");
        IReadOnlyList<ReadModelBatchOperation> replacement = WriteView(tenantId, capabilityId, view);
        if (previous?.Continuation is not null)
            throw new InvalidOperationException("A capability continuation cannot be extended.");
        int retireCount = previous?.FragmentCount ?? 0;
        if (replacement.Count + retireCount <= 128)
        {
            var operations = replacement.ToList();
            if (previous?.Digest is { } digest)
                for (int ordinal = 0; ordinal < retireCount; ordinal++)
                    operations.Add(ReadModelBatchOperation.Delete(FragmentKey(tenantId, capabilityId, digest, ordinal),
                        ReadModelBatchConcurrency.IdempotentAbsent));
            return operations;
        }

        // Apply has already proved the complementary prior fact; only a completed lifecycle
        // may use the one-layer representation accepted by readers.
        if (previous is null || view.Issuance is null || !HasTerminal(view))
            throw new InvalidOperationException("A capability continuation requires a completed lifecycle.");
        MagicLinkCapabilityProjectionTransition transition = new(
            decoded.Payload is MagicLinkConfirmationCapabilityIssued ? view.Issuance : null,
            decoded.Payload as MagicLinkConfirmationCapabilityUsed,
            decoded.Payload as MagicLinkConfirmationCapabilityRevoked,
            decoded.Payload as MagicLinkConfirmationCapabilityExpired,
            decoded.Payload is not MagicLinkConfirmationCapabilityIssued && !decoded.OwnerStream,
            decoded.Payload is MagicLinkConfirmationCapabilityIssued ? null : view.TerminalPosition);
        byte[] transitionBytes = JsonSerializer.SerializeToUtf8Bytes(transition, s_json);
        int count = checked((transitionBytes.Length + ViewFragmentBytes - 1) / ViewFragmentBytes);
        if (count is < 1 or > 127)
            throw new InvalidOperationException("A capability transition exceeds the live fold limit.");
        string transitionDigest = Convert.ToHexString(SHA256.HashData(transitionBytes));
        var continuation = new MagicLinkCapabilityProjectionContinuation(tenantId, capabilityId.Value, previous,
            transitionDigest, transitionBytes.Length, count, Convert.ToHexString(SHA256.HashData(viewBytes)), viewBytes.Length);
        var result = new List<ReadModelBatchOperation>(count + 1);
        for (int ordinal = 0; ordinal < count; ordinal++)
            result.Add(ReadModelBatchOperation.Write(TransitionKey(tenantId, capabilityId, transitionDigest, ordinal),
                new MagicLinkCapabilityProjectionFragment(transitionBytes.AsSpan(ordinal * ViewFragmentBytes,
                    Math.Min(ViewFragmentBytes, transitionBytes.Length - ordinal * ViewFragmentBytes)).ToArray()),
                ReadModelBatchConcurrency.LastWrite));
        result.Add(ReadModelBatchOperation.Write(MagicLinkCapabilityReadModelAddress.StateKey(tenantId, capabilityId),
            new MagicLinkCapabilityProjectionStorage(null, null, 0, 0) { Continuation = continuation },
            ReadModelBatchConcurrency.LastWrite));
        if (result.Any(static operation => operation.CanonicalValue.Length > MaxPhysicalValueBytes))
            throw new InvalidOperationException("A capability continuation exceeds the physical value limit.");
        return result;
    }

    private static IReadOnlyList<ReadModelBatchOperation> WriteView(
        string tenantId,
        MagicLinkCapabilityId capabilityId,
        MagicLinkCapabilityProjectionView view)
    {
        string key = MagicLinkCapabilityReadModelAddress.StateKey(tenantId, capabilityId);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(view, s_json);
        if (bytes.Length <= InlineViewLimitBytes)
        {
            return [ReadModelBatchOperation.Write(key,
                new MagicLinkCapabilityProjectionStorage(view, null, 0, 0),
                ReadModelBatchConcurrency.LastWrite)];
        }

        string digest = Convert.ToHexString(SHA256.HashData(bytes));
        int count = checked((bytes.Length + ViewFragmentBytes - 1) / ViewFragmentBytes);
        if (count > 127) throw new InvalidOperationException("A capability view exceeds the supported storage limit.");
        var operations = new List<ReadModelBatchOperation>(count + 1);
        for (int ordinal = 0; ordinal < count; ordinal++)
        {
            byte[] fragment = bytes.AsSpan(
                ordinal * ViewFragmentBytes,
                Math.Min(ViewFragmentBytes, bytes.Length - ordinal * ViewFragmentBytes)).ToArray();
            ReadModelBatchOperation operation = ReadModelBatchOperation.Write(
                FragmentKey(tenantId, capabilityId, digest, ordinal),
                new MagicLinkCapabilityProjectionFragment(fragment),
                ReadModelBatchConcurrency.LastWrite);
            if (operation.CanonicalValue.Length > MaxPhysicalValueBytes)
            {
                throw new InvalidOperationException("A capability fragment exceeds the physical value limit.");
            }

            operations.Add(operation);
        }

        operations.Add(ReadModelBatchOperation.Write(key,
            new MagicLinkCapabilityProjectionStorage(null, digest, bytes.Length, count, ViewFragmentBytes),
            ReadModelBatchConcurrency.LastWrite));
        return operations;
    }

    private static async Task<MagicLinkCapabilityProjectionView> RestoreViewAsync(
        SharedProjectionScope scope,
        MagicLinkCapabilityId capabilityId,
        long generation,
        MagicLinkCapabilityProjectionStorage storage,
        SharedProjectionEpochCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        if (storage.Continuation is { } continuation)
        {
            if (storage.Inline is not null || storage.Digest is not null || storage.ByteLength != 0
                || storage.FragmentCount != 0 || storage.FragmentBytes != 0
                || continuation.TenantId != scope.TenantId || continuation.CapabilityId != capabilityId.Value
                || continuation.Base is null || continuation.Base.Continuation is not null
                || !IsDigest(continuation.TransitionDigest) || !IsDigest(continuation.ViewDigest)
                || continuation.TransitionByteLength <= 0 || continuation.TransitionByteLength > 127L * ViewFragmentBytes
                || continuation.TransitionFragmentCount != ((long)continuation.TransitionByteLength + ViewFragmentBytes - 1) / ViewFragmentBytes
                || continuation.ViewByteLength <= 0 || continuation.ViewByteLength > 127L * ViewFragmentBytes)
                throw new InvalidOperationException("The capability continuation is invalid.");
            MagicLinkCapabilityProjectionView baseView = await RestoreViewAsync(scope, capabilityId, generation,
                continuation.Base, coordinator, cancellationToken).ConfigureAwait(false);
            if ((baseView.Issuance is not null ? 1 : 0) + (HasTerminal(baseView) ? 1 : 0) != 1)
                throw new InvalidOperationException("A capability continuation requires exactly one base fact.");
            byte[] transitionBytes = new byte[continuation.TransitionByteLength];
            for (int ordinal = 0; ordinal < continuation.TransitionFragmentCount; ordinal++)
            {
                ReadModelEntry<MagicLinkCapabilityProjectionFragment> entry;
                try
                {
                    entry = await coordinator.ReadGenerationAsync<MagicLinkCapabilityProjectionFragment>(scope, generation,
                        TransitionKey(scope.TenantId, capabilityId, continuation.TransitionDigest, ordinal), cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception exception) { throw new SharedProjectionReadUnavailableException(exception); }
                int offset = ordinal * ViewFragmentBytes;
                int expected = Math.Min(ViewFragmentBytes, transitionBytes.Length - offset);
                if (entry.Value?.Data is not { } data || data.Length != expected)
                    throw new InvalidOperationException("A capability transition fragment is absent or malformed.");
                data.CopyTo(transitionBytes, offset);
            }
            if (Convert.ToHexString(SHA256.HashData(transitionBytes)) != continuation.TransitionDigest)
                throw new InvalidOperationException("The capability transition digest is invalid.");
            MagicLinkCapabilityProjectionTransition transition = JsonSerializer.Deserialize<MagicLinkCapabilityProjectionTransition>(
                transitionBytes, s_json) ?? throw new InvalidOperationException("A capability transition is empty.");
            int facts = (transition.Issuance is not null ? 1 : 0) + (transition.Used is not null ? 1 : 0)
                + (transition.Revoked is not null ? 1 : 0) + (transition.Expired is not null ? 1 : 0);
            if (facts != 1 || transition.Issuance is not null && (transition.TerminalInCapabilityStream || transition.TerminalPosition is not null)
                || transition.Issuance is null && transition.TerminalPosition is not > 0)
                throw new InvalidOperationException("A capability transition is ambiguous.");
            MagicLinkCapabilityProjectionView reconstructed;
            if (transition.Issuance is { } issuance)
            {
                MagicLinkCapabilityProjectionView contribution = Publish(MagicLinkCapabilityProjectionView.Empty with { Issuance = issuance }, scope.TenantId);
                ValidateStoredView(contribution, scope.TenantId, capabilityId);
                reconstructed = Merge(baseView, contribution, scope.TenantId);
            }
            else
            {
                object terminal = (object?)transition.Used ?? (object?)transition.Revoked ?? transition.Expired!;
                if (IdOf(terminal) != capabilityId)
                    throw new InvalidOperationException("A capability transition crosses its scope.");
                reconstructed = Apply(baseView, terminal, !transition.TerminalInCapabilityStream, scope.TenantId,
                    transition.TerminalPosition!.Value);
            }
            ValidateStoredView(reconstructed, scope.TenantId, capabilityId);
            if (reconstructed.Issuance is null || !HasTerminal(reconstructed))
                throw new InvalidOperationException("A capability continuation must complete its lifecycle.");
            byte[] completeBytes = JsonSerializer.SerializeToUtf8Bytes(reconstructed, s_json);
            if (completeBytes.Length != continuation.ViewByteLength
                || Convert.ToHexString(SHA256.HashData(completeBytes)) != continuation.ViewDigest)
                throw new InvalidOperationException("The reconstructed capability view digest is invalid.");
            return reconstructed;
        }

        if (storage.Inline is { } inline)
        {
            if (storage.Digest is not null || storage.ByteLength != 0 || storage.FragmentCount != 0
                || storage.FragmentBytes != 0)
            {
                throw new InvalidOperationException("The capability projection storage is ambiguous.");
            }

            ValidateStoredView(inline, scope.TenantId, capabilityId);
            return inline;
        }

        int fragmentBytes = storage.FragmentBytes == 0 ? 24 * 1024 : storage.FragmentBytes;
        if (storage.FragmentBytes is not (0 or ViewFragmentBytes)
            || !IsDigest(storage.Digest)
            || storage.ByteLength <= InlineViewLimitBytes
            || storage.ByteLength > 127L * fragmentBytes
            || storage.FragmentCount != ((long)storage.ByteLength + fragmentBytes - 1) / fragmentBytes
            || storage.FragmentCount > 127)
        {
            throw new InvalidOperationException("The capability projection fragment pointer is invalid.");
        }

        byte[] bytes = new byte[storage.ByteLength];
        for (int ordinal = 0; ordinal < storage.FragmentCount; ordinal++)
        {
            ReadModelEntry<MagicLinkCapabilityProjectionFragment> entry;
            try
            {
                entry = await coordinator
                    .ReadGenerationAsync<MagicLinkCapabilityProjectionFragment>(scope, generation,
                        FragmentKey(scope.TenantId, capabilityId, storage.Digest!, ordinal), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new SharedProjectionReadUnavailableException(exception);
            }
            int offset = ordinal * fragmentBytes;
            int expected = Math.Min(fragmentBytes, storage.ByteLength - offset);
            if (entry.Value?.Data is not { } data || data.Length != expected)
            {
                throw new InvalidOperationException("A capability projection fragment is absent or malformed.");
            }

            data.CopyTo(bytes, offset);
        }

        if (Convert.ToHexString(SHA256.HashData(bytes)) != storage.Digest)
        {
            throw new InvalidOperationException("The capability projection fragment digest is invalid.");
        }

        MagicLinkCapabilityProjectionView view = JsonSerializer.Deserialize<MagicLinkCapabilityProjectionView>(bytes, s_json)
            ?? throw new InvalidOperationException("The capability projection fragments contain no view.");
        ValidateStoredView(view, scope.TenantId, capabilityId);
        return view;
    }

    private static void ValidateStoredView(
        MagicLinkCapabilityProjectionView view,
        string tenantId,
        MagicLinkCapabilityId capabilityId)
    {
        if (view.Issuance is { } issue
            && (issue.Tenant?.TenantId != tenantId || issue.CapabilityId != capabilityId
                || !ValidStoredIssuance(issue))
            || view.Published is { } published
            && (published.Tenant.TenantId != tenantId || published.CapabilityId != capabilityId)
            || view.Used is { } used
            && (used.Tenant.TenantId != tenantId || used.CapabilityId != capabilityId)
            || view.Revoked is { } revoked
            && (revoked.Tenant.TenantId != tenantId || revoked.CapabilityId != capabilityId)
            || view.Expired is { } expired
            && (expired.Tenant.TenantId != tenantId || expired.CapabilityId != capabilityId)
            || (view.Used is not null ? 1 : 0)
                + (view.Revoked is not null ? 1 : 0)
                + (view.Expired is not null ? 1 : 0) > 1
            || (view.TerminalPosition is > 0) != HasTerminal(view)
            || view.TerminalPosition is <= 0
            || view.TerminalInCapabilityStream && (view.Issuance is null || !HasTerminal(view))
            || !Equals(Publish(view with { Published = null }, tenantId).Published, view.Published))
        {
            throw new InvalidOperationException("The capability projection view does not match its selected identity.");
        }
    }

    private static bool ValidStoredIssuance(MagicLinkConfirmationCapabilityReadModel issue)
        => issue.State == Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Issued
            && issue.ExpiryState == (issue.ExpiresAtUtc - issue.IssuedAtUtc <= TimeSpan.FromHours(24)
                ? MagicLinkExpiryState.ExpiringSoon : MagicLinkExpiryState.Active)
            && issue.StateBadgeText == "Issued"
            && issue.ExpiryBadgeText == (issue.ExpiryState == MagicLinkExpiryState.ExpiringSoon
                ? "Expiring soon" : "Active")
            && !string.IsNullOrWhiteSpace(issue.CapabilityId?.Value)
            && !string.IsNullOrWhiteSpace(issue.Contributor?.PartyId)
            && issue.Target is not null
            && issue.Target.TargetKind is TimeEntryTargetKind.Project or TimeEntryTargetKind.Work
            && !string.IsNullOrWhiteSpace(issue.Target.TargetId)
            && !string.IsNullOrWhiteSpace(issue.ActivityTypeId?.Value)
            && !string.IsNullOrWhiteSpace(issue.TimeEntryId?.Value)
            && issue.TargetKind is MagicLinkTargetKind.ProposedTimeEntry or MagicLinkTargetKind.ExistingTimeEntry
            && issue.AllowedAction is MagicLinkAllowedAction.Confirm or MagicLinkAllowedAction.Adjust or MagicLinkAllowedAction.ConfirmOrAdjust
            && issue.IssuedAtUtc != default
            && issue.ExpiresAtUtc > issue.IssuedAtUtc
            && !string.IsNullOrWhiteSpace(issue.Issuer?.PartyId)
            && !string.IsNullOrWhiteSpace(issue.IssueMetadata?.SourceSystem)
            && !string.IsNullOrWhiteSpace(issue.IssueMetadata?.SourceReference)
            && issue.UsedAtUtc is null && issue.RevokedAtUtc is null && issue.ExpiredAtUtc is null
            && issue.RevokedBy is null && issue.UseOutcomeCategory is null
            && issue.UseMetadata is null && issue.RevocationMetadata is null && issue.ExpiryMetadata is null
            && issue.ProjectionFreshness is { State: ProjectionFreshnessState.Fresh }
            && issue.ProjectionFreshness.AsOfUtc is null && issue.ProjectionFreshness.Detail is null
            && long.TryParse(issue.ProjectionFreshness.Cursor,
                System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture,
                out long position) && position > 0;

    private static string FragmentKey(string tenantId, MagicLinkCapabilityId capabilityId, string digest, int ordinal)
        => MagicLinkCapabilityReadModelAddress.StateKey(tenantId, capabilityId)
            + ":fragment:" + digest + ":" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static bool IsDigest(string? digest)
        => digest is { Length: 64 } && digest.All(static character => character is >= '0' and <= '9' or >= 'A' and <= 'F');

    private static string TransitionKey(string tenantId, MagicLinkCapabilityId capabilityId, string digest, int ordinal)
        => MagicLinkCapabilityReadModelAddress.StateKey(tenantId, capabilityId)
            + ":transition:" + digest + ":" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static MagicLinkCapabilityProjectionView Apply(
        MagicLinkCapabilityProjectionView current,
        object payload,
        bool ownerStream,
        string tenantId,
        long position)
    {
        if (payload is MagicLinkConfirmationCapabilityIssued issued)
        {
            ValidateIssued(issued, tenantId);
            if (current.Issuance is not null || current.TerminalInCapabilityStream)
            {
                throw new InvalidOperationException("Capability issuance conflicts with existing evidence.");
            }

            MagicLinkConfirmationCapabilityReadModel row = new MagicLinkConfirmationCapabilityProjection().Project(
                issued.CapabilityId,
                [new MagicLinkProjectionEvent(position.ToString(System.Globalization.CultureInfo.InvariantCulture), 1, issued)],
                new TimesheetsProjectionCheckpoint(tenantId, MagicLinkConfirmationCapabilityProjection.ProjectionName, position, ProjectionFreshness.Fresh),
                issued.IssuedAtUtc)
                ?? throw new InvalidOperationException("Capability issuance did not fold.");
            return Publish(current with { Issuance = row }, tenantId);
        }

        ValidateTerminal(payload, tenantId);
        if (HasTerminal(current) || (!ownerStream && current.Issuance is null))
        {
            throw new InvalidOperationException("Capability terminal evidence conflicts with issuance.");
        }

        MagicLinkCapabilityProjectionView next = payload switch
        {
            MagicLinkConfirmationCapabilityUsed used => current with { Used = used, TerminalInCapabilityStream = !ownerStream, TerminalPosition = position },
            MagicLinkConfirmationCapabilityRevoked revoked => current with { Revoked = revoked, TerminalInCapabilityStream = !ownerStream, TerminalPosition = position },
            MagicLinkConfirmationCapabilityExpired expired => current with { Expired = expired, TerminalInCapabilityStream = !ownerStream, TerminalPosition = position },
            _ => throw new InvalidOperationException("Unknown capability transition.")
        };
        return Publish(next, tenantId);
    }

    private static MagicLinkCapabilityProjectionView Publish(MagicLinkCapabilityProjectionView view, string tenantId)
    {
        if (view.Issuance is not { } issue)
        {
            return view with { Published = null };
        }

        if (issue.Tenant.TenantId != tenantId)
        {
            throw new InvalidOperationException("Capability issuance crosses the tenant.");
        }

        if (view.TerminalPosition is { } terminalPosition
            && (!long.TryParse(issue.ProjectionFreshness.Cursor,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out long issuancePosition)
                || terminalPosition <= issuancePosition))
        {
            throw new InvalidOperationException("Capability terminal position does not follow issuance.");
        }

        MagicLinkConfirmationCapabilityReadModel row = issue;
        if (view.Used is { } used)
        {
            if (used.TimeEntryId != issue.TimeEntryId || used.Contributor != issue.Contributor
                || used.UsedAtUtc < issue.IssuedAtUtc || used.UsedAtUtc >= issue.ExpiresAtUtc
                || (used.OutcomeCategory switch
                {
                    "confirmed" => issue.AllowedAction is not (MagicLinkAllowedAction.Confirm or MagicLinkAllowedAction.ConfirmOrAdjust),
                    "adjusted" => issue.AllowedAction is not (MagicLinkAllowedAction.Adjust or MagicLinkAllowedAction.ConfirmOrAdjust),
                    _ => true
                }))
            {
                throw new InvalidOperationException("Capability use does not match issuance.");
            }

            row = row with
            {
                State = Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Used,
                UsedAtUtc = used.UsedAtUtc,
                UseMetadata = used.Source,
                UseOutcomeCategory = used.OutcomeCategory,
                StateBadgeText = "Used"
            };
        }
        else if (view.Revoked is { } revoked)
        {
            if (revoked.TimeEntryId is not null && revoked.TimeEntryId != issue.TimeEntryId)
            {
                throw new InvalidOperationException("Capability revocation does not match issuance.");
            }

            row = row with
            {
                State = Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Revoked,
                RevokedBy = revoked.RevokedBy,
                RevokedAtUtc = revoked.RevokedAtUtc,
                RevocationMetadata = revoked.Source,
                StateBadgeText = "Revoked"
            };
        }
        else if (view.Expired is { } expired)
        {
            if (expired.TimeEntryId is not null && expired.TimeEntryId != issue.TimeEntryId
                || expired.ExpiredAtUtc < issue.ExpiresAtUtc)
            {
                throw new InvalidOperationException("Capability expiry does not match issuance.");
            }

            row = row with
            {
                State = Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Expired,
                ExpiredAtUtc = expired.ExpiredAtUtc,
                ExpiryMetadata = expired.Source,
                ExpiryState = MagicLinkExpiryState.Expired,
                ExpiryBadgeText = "Expired",
                StateBadgeText = "Expired"
            };
        }

        if (view.TerminalPosition is { } publishedTerminalPosition)
        {
            row = row with
            {
                ProjectionFreshness = row.ProjectionFreshness with
                {
                    Cursor = "issue:" + issue.ProjectionFreshness.Cursor + ";terminal:"
                        + publishedTerminalPosition.ToString(System.Globalization.CultureInfo.InvariantCulture)
                }
            };
        }

        return view with { Published = row };
    }

    private static bool HasTerminal(MagicLinkCapabilityProjectionView view)
        => view.Used is not null || view.Revoked is not null || view.Expired is not null;

    private static (object Payload, bool OwnerStream)? Decode(ProjectionEventDto item)
    {
        StoredMagicLinkIssued? issued = ProjectionEventReader.Deserialize<StoredMagicLinkIssued>(item);
        if (issued is not null) return (issued.Event ?? throw new InvalidOperationException("Issuance wrapper is empty."), false);
        StoredMagicLinkUsed? used = ProjectionEventReader.Deserialize<StoredMagicLinkUsed>(item);
        if (used is not null) return (used.Event ?? throw new InvalidOperationException("Use wrapper is empty."), true);
        StoredMagicLinkRevoked? revoked = ProjectionEventReader.Deserialize<StoredMagicLinkRevoked>(item);
        if (revoked is not null) return (revoked.Event ?? throw new InvalidOperationException("Revocation wrapper is empty."), true);
        StoredMagicLinkExpired? expired = ProjectionEventReader.Deserialize<StoredMagicLinkExpired>(item);
        if (expired is not null) return (expired.Event ?? throw new InvalidOperationException("Expiry wrapper is empty."), true);
        object? raw = (object?)ProjectionEventReader.Deserialize<MagicLinkConfirmationCapabilityIssued>(item)
            ?? (object?)ProjectionEventReader.Deserialize<MagicLinkConfirmationCapabilityUsed>(item)
            ?? (object?)ProjectionEventReader.Deserialize<MagicLinkConfirmationCapabilityRevoked>(item)
            ?? ProjectionEventReader.Deserialize<MagicLinkConfirmationCapabilityExpired>(item);
        return raw is null ? null : (raw, false);
    }

    private static MagicLinkCapabilityId IdOf(object payload) => payload switch
    {
        MagicLinkConfirmationCapabilityIssued item => item.CapabilityId,
        MagicLinkConfirmationCapabilityUsed item => item.CapabilityId,
        MagicLinkConfirmationCapabilityRevoked item => item.CapabilityId,
        MagicLinkConfirmationCapabilityExpired item => item.CapabilityId,
        _ => throw new InvalidOperationException("Unknown capability event.")
    } ?? throw new InvalidOperationException("Capability ID is missing.");

    private static void ValidateOwner(ProjectionRequest request, (object Payload, bool OwnerStream) decoded)
    {
        MagicLinkCapabilityId id = IdOf(decoded.Payload);
        string owner = decoded.OwnerStream
            ? decoded.Payload switch
            {
                MagicLinkConfirmationCapabilityUsed used => used.TimeEntryId?.Value,
                MagicLinkConfirmationCapabilityRevoked revoked => revoked.TimeEntryId?.Value,
                MagicLinkConfirmationCapabilityExpired expired => expired.TimeEntryId?.Value,
                _ => null
            } ?? throw new InvalidOperationException("Terminal owner is missing.")
            : id.Value;
        if (!string.Equals(owner, request.AggregateId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Capability event does not match its owner stream.");
        }
    }

    internal static void ValidateIssued(MagicLinkConfirmationCapabilityIssued item, string tenantId)
    {
        if (item.Tenant?.TenantId != tenantId
            || string.IsNullOrWhiteSpace(item.CapabilityId?.Value)
            || string.IsNullOrWhiteSpace(item.Contributor?.PartyId)
            || item.Target is null
            || item.Target.TargetKind is not (TimeEntryTargetKind.Project or TimeEntryTargetKind.Work)
            || string.IsNullOrWhiteSpace(item.Target.TargetId)
            || string.IsNullOrWhiteSpace(item.ActivityTypeId?.Value)
            || string.IsNullOrWhiteSpace(item.TimeEntryId?.Value)
            || string.IsNullOrWhiteSpace(item.TokenHash?.Value)
            || string.IsNullOrWhiteSpace(item.Issuer?.PartyId)
            || string.IsNullOrWhiteSpace(item.Source?.SourceSystem)
            || string.IsNullOrWhiteSpace(item.Source?.SourceReference)
            || !item.IsSingleUse
            || item.IssuedAtUtc == default || item.ExpiresAtUtc <= item.IssuedAtUtc
            || item.TargetKind is not (MagicLinkTargetKind.ProposedTimeEntry or MagicLinkTargetKind.ExistingTimeEntry)
            || item.AllowedAction is not (MagicLinkAllowedAction.Confirm or MagicLinkAllowedAction.Adjust or MagicLinkAllowedAction.ConfirmOrAdjust))
        {
            throw new InvalidOperationException("Capability issuance is incomplete or invalid.");
        }
    }

    private static void ValidateTerminal(object payload, string tenantId)
    {
        bool valid = payload switch
        {
            MagicLinkConfirmationCapabilityUsed used => used.Tenant?.TenantId == tenantId
                && used.CapabilityId is not null && used.Contributor is not null
                && used.TimeEntryId is not null && used.Source is not null
                && used.UsedAtUtc != default,
            MagicLinkConfirmationCapabilityRevoked revoked => revoked.Tenant?.TenantId == tenantId
                && revoked.CapabilityId is not null && revoked.RevokedBy is not null && revoked.Source is not null
                && revoked.RevokedAtUtc != default,
            MagicLinkConfirmationCapabilityExpired expired => expired.Tenant?.TenantId == tenantId
                && expired.CapabilityId is not null && expired.Source is not null
                && expired.ExpiredAtUtc != default,
            _ => false
        };
        if (!valid)
        {
            throw new InvalidOperationException("Capability terminal evidence is incomplete or invalid.");
        }
    }

    private static SharedProjectionFoldFailureException Park(SharedProjectionDelivery delivery)
    {
        string suffix = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            delivery.SourceStream + "\0" + delivery.EnvelopePosition.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        return new SharedProjectionFoldFailureException(
            delivery.SourceStream,
            delivery.EnvelopePosition,
            2,
            count => [ReadModelBatchOperation.Write(
                "capability:parked:" + suffix,
                new Dictionary<string, long> { ["position"] = delivery.EnvelopePosition, ["attempts"] = count },
                ReadModelBatchConcurrency.LastWrite)]);
    }

    private static bool IsFoldFailure(Exception exception)
        => exception is InvalidOperationException or ArgumentException or JsonException or NullReferenceException;

    private static void Validate(ProjectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Domain != TimesheetsDomain.Name
            || string.IsNullOrWhiteSpace(request.TenantId)
            || string.IsNullOrWhiteSpace(request.AggregateId)
            || request.Events is null)
        {
            throw new InvalidOperationException("The capability request is outside the projection route.");
        }
    }

    private static void Validate(DomainSharedProjectionRebuildIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (identity.Domain != TimesheetsDomain.Name
            || identity.ProjectionType != MagicLinkConfirmationCapabilityProjection.ProjectionName)
        {
            throw new InvalidOperationException("The rebuild identity targets another projection.");
        }
    }
}
