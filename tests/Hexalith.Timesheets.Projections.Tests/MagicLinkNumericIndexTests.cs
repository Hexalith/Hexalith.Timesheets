using System.Runtime.CompilerServices;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks;

using Shouldly;

namespace Hexalith.Timesheets.Projections.Tests;

public sealed class MagicLinkNumericIndexTests
{
    [Fact]
    public async Task RepeatedTenantReplacementKeepsUnrelatedPagesAndGlobalAmbiguity()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        _ = await coordinator.RegisterWriterAsync(scope,
            MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        MagicLinkTokenHashCapabilityIndexDirectoryEntry[] unrelated = [.. Enumerable.Range(0, 2_048)
            .Select(index => Row("tenant-b", $"owner-{index:D5}", $"hash-{index:D5}"))];
        unrelated[0] = Row("tenant-b", "owner-00000", "shared-hash");
        await ReplaceAsync(coordinator, scope, "tenant-b", "seed-b", unrelated);
        await ReplaceAsync(coordinator, scope, "tenant-a", "seed-a",
            [Row("tenant-a", "first", "shared-hash"), Row("tenant-a", "second", "a-only")]);
        await AssertCandidatesAsync(coordinator, scope, "shared-hash", 2);

        int changed = await ReplaceAsync(coordinator, scope, "tenant-a", "replace-a",
            [Row("tenant-a", "first", "a-new")]);
        changed.ShouldBeLessThan(300);
        await AssertCandidatesAsync(coordinator, scope, "shared-hash", 1);
        await AssertCandidatesAsync(coordinator, scope, "a-only", 0);
        await AssertCandidatesAsync(coordinator, scope, "a-new", 1);
        foreach (MagicLinkTokenHashCapabilityIndexDirectoryEntry row in unrelated)
            await AssertCandidatesAsync(coordinator, scope, row.TokenHash, 1);

        int pruned = await ReplaceAsync(coordinator, scope, "tenant-a", "prune-a", []);
        pruned.ShouldBeLessThan(300);
        await AssertCandidatesAsync(coordinator, scope, "shared-hash", 1);
        await AssertCandidatesAsync(coordinator, scope, "a-new", 0);
        foreach (MagicLinkTokenHashCapabilityIndexDirectoryEntry row in unrelated)
            await AssertCandidatesAsync(coordinator, scope, row.TokenHash, 1);
    }

    [Fact]
    public async Task OrdinaryInsertionFindsFullOrderAcrossEqualPrefixPages()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        _ = await coordinator.RegisterWriterAsync(scope,
            MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        const string hash = "one-concentrated-hash";
        for (int index = 0; index < 130; index++)
            (await AddAsync(coordinator, scope, $"owner-{index:D5}", hash))
                .ShouldBeLessThan(40);
        (await AddAsync(coordinator, scope, "owner-00064-extra", hash)).ShouldBeLessThan(40);
        (await AddAsync(coordinator, scope, "owner-00130", hash)).ShouldBeLessThan(40);

        MagicLinkTokenHashCapabilityIndexTreeRoot root = (await coordinator
            .ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
                MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
                TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        var reader = new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, scope);
        IReadOnlyList<MagicLinkTokenHashCapabilityIndexDirectoryEntry> rows =
            (await reader.ReadRosterAsync(root.TenantRoot, "tenant-a",
                TestContext.Current.CancellationToken)).ShouldNotBeNull();
        rows.Select(row => row.Candidate.CapabilityId.Value).ShouldBe(
            Enumerable.Range(0, 130).Select(index => $"owner-{index:D5}")
                .Append("owner-00064-extra").Append("owner-00130")
                .OrderBy(owner => owner, StringComparer.Ordinal));
        (await reader.FindOwnerAsync(root.OwnerRoot, "tenant-a", "owner-00064-extra",
            TestContext.Current.CancellationToken)).ShouldBe(
                Row("tenant-a", "owner-00064-extra", hash));
        (await reader.FindCandidatesAsync(root.TokenRoot, hash,
            TestContext.Current.CancellationToken)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task TenantReplacementPreservesCandidatesBeyondFourHundredGlobalPages()
    {
        const int perTenant = 8_700;
        const string sharedHash = "shared-global-hash";
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        _ = await coordinator.RegisterWriterAsync(scope,
            MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        string[] tenants = ["tenant-a", "tenant-b", "tenant-c", "tenant-d"];
        foreach (string tenant in tenants)
            await ReplaceThroughHandlerAsync(store, coordinator, scope, tenant, "seed-" + tenant,
                [.. Enumerable.Range(0, perTenant).Select(index =>
                    Row(tenant, $"owner-{index:D5}", index == 0
                        ? sharedHash : $"hash-{tenant}-{index:D5}"))]);

        var reader = new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, scope);
        MagicLinkTokenHashCapabilityIndexTreeRoot root = (await coordinator
            .ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
                MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
                TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        int totalPages = 0;
        foreach (string tenant in tenants)
        {
            MagicLinkTokenHashCapabilityIndexTenantRoster roster =
                (await reader.FindRosterAsync(root.TenantRoot, tenant,
                    TestContext.Current.CancellationToken)).ShouldNotBeNull();
            totalPages += roster.Pages.Count;
            (await reader.ReadRosterAsync(root.TenantRoot, tenant,
                TestContext.Current.CancellationToken)).ShouldNotBeNull().Count.ShouldBe(perTenant);
            await AssertCandidatesAsync(coordinator, scope, $"hash-{tenant}-04350", 1);
        }
        totalPages.ShouldBeGreaterThan(400);
        await AssertCandidatesAsync(coordinator, scope, sharedHash, 2);
        foreach (string tenant in tenants)
            await AssertExactAsync(coordinator, scope, Row(tenant, "owner-00000", sharedHash), true);

        int changed = await ReplaceThroughHandlerAsync(store, coordinator, scope, "tenant-a", "replace-tenant-a",
            [Row("tenant-a", "replacement", "hash-tenant-a-replacement")]);
        changed.ShouldBeLessThan(perTenant * 3);
        await AssertCandidatesAsync(coordinator, scope, sharedHash, 2);
        await AssertExactAsync(coordinator, scope, Row("tenant-a", "owner-00000", sharedHash), false);
        await AssertCandidatesAsync(coordinator, scope, "hash-tenant-a-04350", 0);
        await AssertCandidatesAsync(coordinator, scope, "hash-tenant-a-replacement", 1);
        foreach (string tenant in tenants[1..])
            await AssertCandidatesAsync(coordinator, scope, $"hash-{tenant}-04350", 1);
        int pruned = await ReplaceThroughHandlerAsync(store, coordinator, scope, "tenant-a", "prune-tenant-a", []);
        pruned.ShouldBeLessThan(100);
        await AssertCandidatesAsync(coordinator, scope, "hash-tenant-a-replacement", 0);
        foreach (string tenant in tenants[1..])
            await AssertCandidatesAsync(coordinator, scope, $"hash-{tenant}-04350", 1);

        int changedB = await ReplaceThroughHandlerAsync(store, coordinator, scope, "tenant-b", "replace-tenant-b",
            [Row("tenant-b", "replacement", "hash-tenant-b-replacement")]);
        changedB.ShouldBeLessThan(perTenant * 3);
        await AssertCandidatesAsync(coordinator, scope, sharedHash, 2);
        await AssertExactAsync(coordinator, scope, Row("tenant-b", "owner-00000", sharedHash), false);
        await AssertCandidatesAsync(coordinator, scope, "hash-tenant-b-04350", 0);
        await AssertCandidatesAsync(coordinator, scope, "hash-tenant-b-replacement", 1);
        foreach (string tenant in tenants[2..])
            await AssertCandidatesAsync(coordinator, scope, $"hash-{tenant}-04350", 1);

        (await ReplaceThroughHandlerAsync(store, coordinator, scope, "tenant-b", "prune-tenant-b", []))
            .ShouldBeLessThan(100);
        await AssertCandidatesAsync(coordinator, scope, sharedHash, 2);
        foreach (string tenant in tenants[2..])
            await AssertExactAsync(coordinator, scope, Row(tenant, "owner-00000", sharedHash), true);
        await AssertCandidatesAsync(coordinator, scope, "hash-tenant-b-replacement", 0);
        (await ReplaceThroughHandlerAsync(store, coordinator, scope, "tenant-a", "restore-tenant-a",
            [Row("tenant-a", "restored", "hash-tenant-a-restored")]))
            .ShouldBeLessThan(100);
        await AssertCandidatesAsync(coordinator, scope, "hash-tenant-a-restored", 1);
        foreach (string tenant in tenants[2..])
            await AssertCandidatesAsync(coordinator, scope, $"hash-{tenant}-04350", 1);
    }

    private static MagicLinkTokenHashCapabilityIndexDirectoryEntry Row(
        string tenant, string owner, string hash)
        => new(hash, new MagicLinkTokenHashCapabilityIndexEntry(
            new TenantReference(tenant), new MagicLinkCapabilityId(owner)));

    private static async Task<int> AddAsync(SharedProjectionEpochCoordinator coordinator,
        SharedProjectionScope scope, string owner, string hash)
    {
        string operation = "add-" + owner;
        _ = await coordinator.BeginAsync(scope, operation, "inventory-" + operation,
            new Dictionary<string, long>(), TestContext.Current.CancellationToken);
        MagicLinkTokenHashCapabilityIndexTreeRoot root = (await coordinator
            .ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
                MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
                TestContext.Current.CancellationToken)).Value
            ?? new MagicLinkTokenHashCapabilityIndexTreeRoot(7, 0, null, null);
        SharedProjectionEpochStatus status = await coordinator.GetStatusAsync(scope,
            TestContext.Current.CancellationToken);
        var builder = new MagicLinkTokenHashCapabilityIndexTreeBuilder(
            new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, scope), root, status.Epoch);
        await builder.AddContributionAsync(Row("tenant-a", owner, hash),
            TestContext.Current.CancellationToken);
        ReadModelBatchOperation[] operations = [.. builder.Operations()];
        _ = await coordinator.StageStreamingAsync(scope, operation, AsAsync(operations),
            TestContext.Current.CancellationToken);
        await coordinator.CommitAsync(scope, operation, TestContext.Current.CancellationToken);
        return operations.Length;
    }

    private static async Task<int> ReplaceAsync(SharedProjectionEpochCoordinator coordinator,
        SharedProjectionScope scope, string tenant, string operation,
        IReadOnlyList<MagicLinkTokenHashCapabilityIndexDirectoryEntry> rows)
    {
        _ = await coordinator.BeginAsync(scope, operation, "inventory-" + operation,
            new Dictionary<string, long>(), TestContext.Current.CancellationToken);
        SharedProjectionReadResult<MagicLinkTokenHashCapabilityIndexTreeRoot> selected = await coordinator
            .ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
                MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
                TestContext.Current.CancellationToken);
        MagicLinkTokenHashCapabilityIndexTreeRoot root = selected.Value
            ?? new MagicLinkTokenHashCapabilityIndexTreeRoot(7, 0, null, null);
        SharedProjectionEpochStatus status = await coordinator.GetStatusAsync(scope,
            TestContext.Current.CancellationToken);
        var builder = new MagicLinkTokenHashCapabilityIndexTreeBuilder(
            new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, scope), root, status.Epoch);
        await builder.ReplaceTenantAsync(tenant, rows, TestContext.Current.CancellationToken);
        ReadModelBatchOperation[] operations = [.. builder.Operations()];
        _ = await coordinator.StageStreamingAsync(scope, operation, AsAsync(operations),
            TestContext.Current.CancellationToken);
        await coordinator.CommitAsync(scope, operation, TestContext.Current.CancellationToken);
        return operations.Length;
    }

    private static async Task<int> ReplaceThroughHandlerAsync(InMemoryReadModelStore store,
        SharedProjectionEpochCoordinator coordinator, SharedProjectionScope scope,
        string tenant, string operation,
        IReadOnlyList<MagicLinkTokenHashCapabilityIndexDirectoryEntry> rows)
    {
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store,
            epochCoordinator: coordinator);
        var identity = new DomainSharedProjectionRebuildIdentity(tenant, "timesheets",
            handler.ProjectionType, operation, "catalog");
        DomainSharedProjectionRebuildPartition[] partitions = [.. rows.Select((row, index) =>
            new DomainSharedProjectionRebuildPartition(index, row.Candidate.CapabilityId.Value,
                JsonSerializer.SerializeToUtf8Bytes(new
                {
                    row.TokenHash,
                    row.Candidate
                }, new JsonSerializerOptions(JsonSerializerDefaults.Web))))];
        _ = await coordinator.BeginAsync(scope, operation, "inventory-" + operation,
            new Dictionary<string, long>(), TestContext.Current.CancellationToken,
            operationTenantId: tenant);
        var operations = new List<ReadModelBatchOperation>();
        await foreach (ReadModelBatchOperation item in handler.StreamFinalizedOperationsAsync(
            identity, new FixedCursor(partitions), TestContext.Current.CancellationToken))
            operations.Add(item);
        _ = await coordinator.StageStreamingAsync(scope, operation, AsAsync(operations),
            TestContext.Current.CancellationToken);
        await coordinator.CommitAsync(scope, operation, TestContext.Current.CancellationToken);
        return operations.Count;
    }

    private sealed class FixedCursor(IReadOnlyList<DomainSharedProjectionRebuildPartition> partitions)
        : IDomainSharedProjectionPartitionCursor
    {
        public async IAsyncEnumerable<DomainSharedProjectionRebuildPartition> ReadAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (DomainSharedProjectionRebuildPartition partition in partitions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return partition;
            }
            await Task.CompletedTask;
        }

        public Task<IReadOnlyList<DomainSharedProjectionRebuildPartition>> ReadRangeAsync(
            long ownerOrdinal, int firstChunkOrdinal, int chunkCount, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<bool> TryWriteScratchAsync<TValue>(string key, TValue value, string batchId,
            ReadModelBatchConcurrency concurrency, CancellationToken cancellationToken)
            where TValue : class => throw new NotSupportedException();

        public string ScratchMarkerKey(string batchId) => throw new NotSupportedException();
    }

    private static async Task AssertCandidatesAsync(SharedProjectionEpochCoordinator coordinator,
        SharedProjectionScope scope, string hash, int expected)
    {
        SharedProjectionReadResult<MagicLinkTokenHashCapabilityIndexTreeRoot> selected = await coordinator
            .ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
                MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
                TestContext.Current.CancellationToken);
        MagicLinkTokenHashCapabilityIndexTreeRoot root = selected.Value.ShouldNotBeNull();
        var reader = new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, scope);
        (await reader.FindCandidatesAsync(root.TokenRoot, hash,
            TestContext.Current.CancellationToken)).Count.ShouldBe(expected);
    }

    private static async Task AssertExactAsync(SharedProjectionEpochCoordinator coordinator,
        SharedProjectionScope scope, MagicLinkTokenHashCapabilityIndexDirectoryEntry row,
        bool expected)
    {
        MagicLinkTokenHashCapabilityIndexTreeRoot root = (await coordinator
            .ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
                MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
                TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        var reader = new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, scope);
        (await reader.ContainsExactRowAsync(root.TokenRoot, row,
            TestContext.Current.CancellationToken)).ShouldBe(expected);
    }

    private static async IAsyncEnumerable<ReadModelBatchOperation> AsAsync(
        IEnumerable<ReadModelBatchOperation> operations)
    {
        foreach (ReadModelBatchOperation operation in operations)
        {
            yield return operation;
            await Task.Yield();
        }
    }
}
