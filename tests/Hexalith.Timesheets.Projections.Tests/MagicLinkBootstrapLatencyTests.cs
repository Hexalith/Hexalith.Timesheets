using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks;

using Microsoft.Extensions.Http.Resilience;

using Shouldly;

namespace Hexalith.Timesheets.Projections.Tests;

/// <summary>Qualifies the local bootstrap envelope without claiming deployed latency acceptance.</summary>
public sealed class MagicLinkBootstrapLatencyTests(ITestOutputHelper output) : IDomainSharedProjectionPartitionCursor
{
    private const string Operation = "bootstrap-latency";
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    /// <summary>Includes setup latency and freezes one snapshot independently of later cleanup reads.</summary>
    [Fact]
    public async Task LogicalSnapshotIncludesSetupAndRemainsFrozenAfterIndependentMetadataReads()
    {
        var store = new BootstrapObservingReadModelStore(new InMemoryReadModelStore())
            { ReadDelay = TimeSpan.FromMilliseconds(20) };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        store.Start(cancellation.Token);
        _ = await store.GetAsync<object>("profile", "setup", cancellation.Token);
        _ = await store.GetAsync<object>("profile", "cleanup", CancellationToken.None);
        BootstrapReadMeasurement measurement = store.Freeze();
        measurement.Elapsed.ShouldBeGreaterThanOrEqualTo(store.ReadDelay);
        measurement.Reads["metadata"].ShouldBe(1);
        measurement.IndependentMetadataReads.ShouldBe(1);
        _ = await store.GetAsync<object>("profile", "later-cleanup", CancellationToken.None);
        store.Freeze().ShouldBeSameAs(measurement);
        measurement.Reads["metadata"].ShouldBe(1);
        measurement.IndependentMetadataReads.ShouldBe(1);
    }

    /// <summary>Measures logical inventory and metadata API calls, including setup, under the gateway budget.</summary>
    /// <param name="streamed">Whether to finalize a streamed tenant rebuild.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DelayedCombinedInventoryPersistsV7WithinGatewayAttemptBudget(bool streamed)
    {
        var inner = new InMemoryReadModelStore();
        var store = new BootstrapObservingReadModelStore(inner) { ReadDelay = TimeSpan.FromMilliseconds(2) };
        var coordinator = new SharedProjectionEpochCoordinator(store, inner);
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store, epochCoordinator: coordinator);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        TimeSpan budget = GatewayAttemptBudget();
        cancellation.CancelAfter(budget);
        store.Start(cancellation.Token);
        await PrepareAsync(streamed, handler, coordinator, scope, cancellation.Token);
        long generation = (await coordinator.GetStatusAsync(scope, cancellation.Token)).ActiveGeneration;
        await AttemptAsync(streamed, handler, coordinator, scope, cancellation.Token);

        BootstrapReadMeasurement measurement = store.Freeze();
        WriteMeasurement(streamed, "completed", measurement, store.ReadDelay, budget);
        measurement.Reads["direct"].ShouldBe(257);
        measurement.Reads["selected"].ShouldBe(257);
        string[] legacyKeys = [MagicLinkTokenHashCapabilityIndexProjection.StateKey,
            .. Enumerable.Range(0, 256).Select(MagicLinkTokenHashCapabilityIndexProjection.ShardKey)];
        measurement.IndexKeys["direct"].Order(StringComparer.Ordinal).ShouldBe(legacyKeys.Order(StringComparer.Ordinal));
        measurement.IndexKeys["selected"].Order(StringComparer.Ordinal).ShouldBe(legacyKeys.Select(key =>
            "shared-generation:" + scope.ComputeHash() + ":" + generation + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))))
            .Order(StringComparer.Ordinal));
        measurement.Reads["metadata"].ShouldBeGreaterThan(257);
        measurement.Maximum["direct"].ShouldBe(16);
        measurement.Maximum["selected"].ShouldBe(streamed ? 1 : 16);
        // SDK scratch cleanup may continue reading metadata after commit. Both
        // inventories must have drained before the foreground operation returns.
        measurement.ActiveIndexReads.ShouldBe(0);
        measurement.Elapsed.ShouldBeLessThan(budget);
        await AssertPersistedAsync(streamed, inner, inner, scope);
    }

    /// <summary>Uses the real default gateway attempt budget, preserves durable pending work and recovers on retry.</summary>
    /// <param name="streamed">Whether to finalize a streamed tenant rebuild.</param>
    /// <param name="phase">The delayed inventory phase.</param>
    [Theory]
    [InlineData(false, "direct")]
    [InlineData(false, "selected")]
    [InlineData(true, "direct")]
    [InlineData(true, "selected")]
    public async Task GatewayBudgetCancellationInEitherInventoryPreservesPendingWorkAndRetrySucceeds(
        bool streamed, string phase)
    {
        var inner = new InMemoryReadModelStore();
        var store = new BootstrapObservingReadModelStore(inner) { BlockPhase = phase };
        var coordinator = new SharedProjectionEpochCoordinator(store, inner);
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store, epochCoordinator: coordinator);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        TimeSpan budget = GatewayAttemptBudget();
        cancellation.CancelAfter(budget);
        store.Start(cancellation.Token);
        await PrepareAsync(streamed, handler, coordinator, scope, cancellation.Token);
        SharedProjectionEpochStatus before = await coordinator.GetStatusAsync(scope, cancellation.Token);

        await Should.ThrowAsync<OperationCanceledException>(() => AttemptAsync(
            streamed, handler, coordinator, scope, cancellation.Token));

        BootstrapReadMeasurement measurement = store.Freeze();
        WriteMeasurement(streamed, "cancelled-" + phase, measurement, store.ReadDelay, budget);
        cancellation.IsCancellationRequested.ShouldBeTrue();
        measurement.Elapsed.ShouldBeGreaterThanOrEqualTo(budget - TimeSpan.FromMilliseconds(100));
        measurement.Elapsed.ShouldBeLessThan(budget + TimeSpan.FromSeconds(3));
        measurement.Active.ShouldBe(0);
        if (phase == "selected") measurement.Reads["direct"].ShouldBe(257);
        // Recreate all handler/coordinator objects over the same durable fake;
        // successful retry cannot depend on a prior in-memory handler state.
        var recovered = new SharedProjectionEpochCoordinator(inner, inner);
        SharedProjectionEpochStatus pending = await recovered.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        pending.ActiveGeneration.ShouldBe(before.ActiveGeneration);
        pending.Epoch.ShouldBe(before.Epoch);
        pending.OperationId.ShouldBe(before.OperationId);
        pending.PendingDeliveryCount.ShouldBe(before.PendingDeliveryCount);
        pending.PendingFoldFailures.ShouldBe(before.PendingFoldFailures);
        (await recovered.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
            MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, TestContext.Current.CancellationToken)).Value.ShouldBeNull();
        (await recovered.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeFormat>(scope,
            MagicLinkTokenHashCapabilityIndexTreeReader.FormatKey, TestContext.Current.CancellationToken)).Value.ShouldBeNull();
        var retry = new MagicLinkTokenHashCapabilityIndexProjectionHandler(inner, epochCoordinator: recovered);
        await AttemptAsync(streamed, retry, recovered, scope, TestContext.Current.CancellationToken);
        await AssertPersistedAsync(streamed, inner, inner, scope);
    }

    /// <summary>Profiles every raw SDK state read, including envelope/marker/batch IO and setup.</summary>
    /// <param name="streamed">Whether to finalize a streamed tenant rebuild.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompleteRawSdkStateReadProfilePersistsV7(bool streamed)
    {
        using var backend = new InMemoryDaprStateClient();
        var store = new DaprReadModelStore(backend);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store, epochCoordinator: coordinator);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        var gate = new object();
        var reads = new Dictionary<string, int>(StringComparer.Ordinal);
        int writes = 0;
        bool collecting = true;
        var elapsed = Stopwatch.StartNew();
        // This synchronous seam sees the real SDK byte-state and batch reads.
        // Serial bounded spin waits conservatively add cost even when SDK logical
        // reads would overlap a real transport. This is not an HTTP cancellation test.
        const int waitMicroseconds = 25;
        backend.BeforeRead = key =>
        {
            lock (gate)
            {
                if (!collecting) return;
                reads[key] = reads.GetValueOrDefault(key) + 1;
            }
            long until = Stopwatch.GetTimestamp() + Stopwatch.Frequency * waitMicroseconds / 1_000_000;
            while (Stopwatch.GetTimestamp() < until) Thread.SpinWait(16);
        };
        backend.BeforeWrite = _ => { lock (gate) { if (collecting) writes++; } };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        TimeSpan budget = GatewayAttemptBudget();
        cancellation.CancelAfter(budget);

        await PrepareAsync(streamed, handler, coordinator, scope, cancellation.Token);
        await AttemptAsync(streamed, handler, coordinator, scope, cancellation.Token);

        Dictionary<string, int> frozen;
        int frozenWrites;
        TimeSpan frozenElapsed;
        lock (gate)
        {
            collecting = false;
            elapsed.Stop();
            frozen = new(reads, StringComparer.Ordinal);
            frozenWrites = writes;
            frozenElapsed = elapsed.Elapsed;
        }
        int batchReads = frozen.Where(pair => pair.Key.StartsWith("hxrmb:", StringComparison.Ordinal)).Sum(pair => pair.Value);
        frozen.Values.Sum().ShouldBeGreaterThan(514);
        batchReads.ShouldBeGreaterThan(0);
        frozenWrites.ShouldBeGreaterThan(0);
        frozen.Keys.ShouldContain(key => key.StartsWith("shared-generation:", StringComparison.Ordinal));
        frozenElapsed.ShouldBeLessThan(budget);
        output.WriteLine(JsonSerializer.Serialize(new { profile = "raw DaprReadModelStore SDK state IO",
            path = streamed ? "streamed" : "ordinary", includesSetup = true, waitMicroseconds,
            elapsedSeconds = frozenElapsed.TotalSeconds, budgetSeconds = budget.TotalSeconds,
            rawReads = frozen.Values.Sum(), distinctRawKeys = frozen.Count, batchReads, rawWrites = frozenWrites,
            cleanup = "includes concurrent cleanup reads started before foreground return; later cleanup excluded",
            composedHttpCancellation = "deferred" }));
        await AssertPersistedAsync(streamed, store, store, scope);
    }

    private static TimeSpan GatewayAttemptBudget()
    {
        // EventStore AddServiceDefaults installs AddStandardResilienceHandler
        // without an AttemptTimeout override. Resolve the installed package's
        // default rather than mistaking the journey's socket/loader timeout for it.
        TimeSpan timeout = new HttpStandardResilienceOptions().AttemptTimeout.Timeout;
        timeout.ShouldBe(TimeSpan.FromSeconds(10));
        return timeout;
    }

    private void WriteMeasurement(bool streamed, string result, BootstrapReadMeasurement measurement,
        TimeSpan readDelay, TimeSpan budget)
        => output.WriteLine(JsonSerializer.Serialize(new { profile = "logical IReadModelStore.GetAsync; excludes raw batch IO",
            path = streamed ? "streamed" : "ordinary", result, includesSetup = true,
            delayMilliseconds = readDelay.TotalMilliseconds, elapsedSeconds = measurement.Elapsed.TotalSeconds,
            budgetSeconds = budget.TotalSeconds, reads = measurement.Reads, maximumConcurrency = measurement.Maximum,
            independentMetadataReadsIncludingCleanup = measurement.IndependentMetadataReads }));

    private static async Task PrepareAsync(bool streamed, MagicLinkTokenHashCapabilityIndexProjectionHandler handler,
        SharedProjectionEpochCoordinator coordinator, SharedProjectionScope scope, CancellationToken cancellationToken)
    {
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId,
            cancellationToken);
        if (streamed)
            _ = await coordinator.BeginAsync(scope, Operation, "inventory", new Dictionary<string, long>(),
                cancellationToken);
        else
        {
            var issued = new MagicLinkConfirmationCapabilityIssued(new MagicLinkCapabilityId("owner-1"),
                new TenantReference("tenant-1"), new PartyReference("party-1"),
                TimeEntryTargetReference.ForProject(new ProjectReference("project-1")), new ActivityTypeId("activity-1"),
                new TimeEntryId("entry-1"), MagicLinkTargetKind.ProposedTimeEntry, MagicLinkAllowedAction.Confirm,
                new MagicLinkTokenHash("bootstrap-hash"), DateTimeOffset.UtcNow.AddDays(1), new PartyReference("issuer-1"),
                DateTimeOffset.UtcNow, new MagicLinkAuditMetadata("timesheets", "bootstrap"), true);
            var projectionEvent = new ProjectionEventDto(typeof(MagicLinkConfirmationCapabilityIssued).FullName!,
                JsonSerializer.SerializeToUtf8Bytes(issued, s_json), "json", 1, DateTimeOffset.UtcNow,
                "correlation-bootstrap", "message-bootstrap", "issuer-1", 1);
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new ProjectionRequest("tenant-1", "timesheets",
                "owner-1", [projectionEvent]), s_json);
            (await coordinator.JournalAsync(scope, lease, new SharedProjectionDelivery("tenant-1:timesheets:owner-1",
                1, payload, [], OwnerSequenceNumber: 1), cancellationToken))
                .ShouldBe(SharedProjectionJournalResult.Journaled);
        }
    }

    private async Task AttemptAsync(bool streamed, MagicLinkTokenHashCapabilityIndexProjectionHandler handler,
        SharedProjectionEpochCoordinator coordinator, SharedProjectionScope scope, CancellationToken cancellationToken)
    {
        if (streamed)
        {
            var identity = new DomainSharedProjectionRebuildIdentity("tenant-1", "timesheets", handler.ProjectionType,
                Operation, "catalog");
            _ = await coordinator.StageStreamingAsync(scope, Operation,
                handler.StreamFinalizedOperationsAsync(identity, this, cancellationToken), cancellationToken);
            await coordinator.CommitAsync(scope, Operation, cancellationToken);
        }
        else
            (await coordinator.CatchUpAsync(scope, (delivery, generation, token) =>
                handler.FoldCatchUpAsync(delivery, generation, coordinator, token), cancellationToken)).ShouldBe(1);
    }

    private static async Task AssertPersistedAsync(bool streamed, IReadModelStore inner, IReadModelBatchStore batches, SharedProjectionScope scope)
    {
        var reader = new SharedProjectionEpochCoordinator(inner, batches);
        MagicLinkTokenHashCapabilityIndexTreeRoot root = (await reader.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(
            scope, MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        root.StorageVersion.ShouldBe(7);
        (await reader.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeFormat>(scope,
            MagicLinkTokenHashCapabilityIndexTreeReader.FormatKey, TestContext.Current.CancellationToken))
            .Value.ShouldNotBeNull().StorageVersion.ShouldBe(7);
        if (!streamed)
            (await new MagicLinkTokenHashCapabilityIndexTreeReader(reader, scope).FindCandidatesAsync(root.TokenRoot,
                "bootstrap-hash", TestContext.Current.CancellationToken)).Count.ShouldBe(1);
        SharedProjectionEpochStatus status = await reader.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        status.PendingDeliveryCount.ShouldBe(0);
        status.IsBuilding.ShouldBeFalse();
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<DomainSharedProjectionRebuildPartition> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.CompletedTask;
        yield break;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DomainSharedProjectionRebuildPartition>> ReadRangeAsync(long ownerOrdinal,
        int firstChunkOrdinal, int chunkCount, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<bool> TryWriteScratchAsync<TValue>(string key, TValue value, string batchId,
        ReadModelBatchConcurrency concurrency, CancellationToken cancellationToken) where TValue : class
        => throw new NotSupportedException();

    /// <inheritdoc />
    public string ScratchMarkerKey(string batchId) => throw new NotSupportedException();
}
