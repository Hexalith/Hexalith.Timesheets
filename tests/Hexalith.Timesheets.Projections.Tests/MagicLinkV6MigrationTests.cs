using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks;

using Shouldly;

namespace Hexalith.Timesheets.Projections.Tests;

public sealed class MagicLinkV6MigrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CertifiedRepairGuardsPersistedDirectoryPagesAndRefusesIdenticalContentEtagDrift(bool drift)
    {
        var fixture = await PrepareRepairFixtureAsync(directory: true);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        string physical = PhysicalKey(scope, fixture.Witness.SourceGeneration, fixture.SourceKey);
        ReadModelEntry<JsonNode> original = await fixture.Coordinator.ReadGenerationJsonAsync(scope,
            fixture.Witness.SourceGeneration, fixture.SourceKey, TestContext.Current.CancellationToken);
        byte[] source = fixture.Store.SnapshotRaw(scope.StoreName, physical)!;
        var metadata = RepairMetadataKeys().ToDictionary(key => key,
            key => fixture.Store.SnapshotRaw(scope.StoreName, PhysicalKey(scope, fixture.Witness.SourceGeneration, key)));
        if (drift)
        {
            fixture.Store.SeedRaw(scope.StoreName, physical, JsonDocument.Parse(source));
            fixture.Store.SnapshotRaw(scope.StoreName, physical).ShouldBe(source);
            (await fixture.Coordinator.ReadGenerationJsonAsync(scope, fixture.Witness.SourceGeneration,
                fixture.SourceKey, TestContext.Current.CancellationToken)).ETag.ShouldNotBe(original.ETag);
            await Should.ThrowAsync<InvalidOperationException>(() => RecoverFixtureAsync(fixture.Store, fixture.Digest));
            foreach (var item in metadata)
                fixture.Store.SnapshotRaw(scope.StoreName, PhysicalKey(scope, fixture.Witness.SourceGeneration, item.Key)).ShouldBe(item.Value);
            (await fixture.Coordinator.GetAbortRepairAsync(scope, TestContext.Current.CancellationToken)).ShouldBeNull();
        }
        else
        {
            await RecoverFixtureAsync(fixture.Store, fixture.Digest);
            SharedProjectionAbortRepairReceipt receipt = (await fixture.Coordinator.GetAbortRepairAsync(scope,
                TestContext.Current.CancellationToken)).ShouldNotBeNull();
            SharedProjectionAbortRepairGuard guard = receipt.Request.SourceGuards.Single(value => value.LogicalKey == fixture.SourceKey);
            guard.ExpectedETag.ShouldBe(original.ETag);
            string contentDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(original.Value!.ToJsonString())));
            guard.ContentDigest.ShouldBe(contentDigest);
            guard.SourceGeneration.ShouldBe(original.SourceGeneration);
            guard.PointerETag.ShouldBe(original.PointerETag);
            fixture.Store.SnapshotRaw(scope.StoreName, physical).ShouldBe(source);
            await AssertFixtureDeliveryAsync(fixture.Coordinator, scope, fixture.Issued);
        }
        SharedProjectionEpochStatus after = await fixture.Coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        after.ActiveGeneration.ShouldBe(fixture.Witness.SourceGeneration);
        after.IsBuilding.ShouldBeFalse(); after.IsAborting.ShouldBe(drift); after.PendingDeliveryCount.ShouldBe(drift ? 1 : 0);
    }

    [Fact]
    public async Task FreshMigrationRefusesPresentJsonNullShardBeforeReservingAnEpoch()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        MagicLinkTokenHashCapabilityIndexDirectoryEntry original = await SeedMetadataSourceAsync(coordinator, scope);
        SharedProjectionEpochStatus before = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        int nullShard = (MagicLinkTokenHashCapabilityIndexProjection.ShardOrdinal(original.TokenHash) + 1) % 256;
        string nullKey = MagicLinkTokenHashCapabilityIndexProjection.ShardKey(nullShard);
        store.SeedRaw(scope.StoreName, PhysicalKey(scope, before.ActiveGeneration, nullKey), JsonDocument.Parse("null"));
        ReadModelEntry<JsonNode> observed = await coordinator.ReadGenerationJsonAsync(scope, before.ActiveGeneration,
            nullKey, TestContext.Current.CancellationToken);
        observed.Value.ShouldBeNull(); observed.ETag.ShouldNotBeNullOrEmpty();
        string healthyKey = PhysicalKey(scope, before.ActiveGeneration, MagicLinkTokenHashCapabilityIndexProjection.ShardKey(original.TokenHash));
        byte[] healthy = store.SnapshotRaw(scope.StoreName, healthyKey)!;
        var migration = new MagicLinkTokenHashCapabilityIndexV6Migration(coordinator, store, new PermittedCertifiedMigration());
        InvalidOperationException refusal = await Should.ThrowAsync<InvalidOperationException>(() => migration.MigrateAsync(
            "explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
        refusal.Message.ShouldBe("A present v6 shard cannot contain JSON null.");
        SharedProjectionEpochStatus after = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        after.Epoch.ShouldBe(before.Epoch); after.ActiveGeneration.ShouldBe(before.ActiveGeneration);
        after.IsBuilding.ShouldBeFalse(); after.IsAborting.ShouldBeFalse(); after.PendingDeliveryCount.ShouldBe(0);
        store.SnapshotRaw(scope.StoreName, healthyKey).ShouldBe(healthy);
        (await coordinator.ReadGenerationJsonAsync(scope, before.ActiveGeneration, nullKey,
            TestContext.Current.CancellationToken)).ETag.ShouldBe(observed.ETag);
        store.SnapshotRaw(scope.StoreName, MagicLinkTokenHashCapabilityIndexV6Migration.AbortWitnessKey(before.Epoch + 1)).ShouldBeNull();
    }

    [Theory]
    [InlineData("partial-delete")]
    [InlineData("inherited")]
    [InlineData("inherited-source-pointer-drift")]
    [InlineData("inherited-metadata-pointer-drift")]
    [InlineData("reserved-source-drift")]
    [InlineData("deleted-source-drift")]
    public async Task ColdRepairRetainsAllMetadataAndEnforcesReservedSourceAndCopyOnWriteProvenance(string scenario)
    {
        bool inherited = scenario.StartsWith("inherited", StringComparison.Ordinal);
        bool refuses = scenario.EndsWith("drift", StringComparison.Ordinal);
        var fixture = await PrepareRepairFixtureAsync(inherited: inherited);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        long generation = fixture.Witness.SourceGeneration;
        string[] keys = RepairMetadataKeys();
        var originals = new Dictionary<string, ReadModelEntry<JsonNode>>(StringComparer.Ordinal);
        foreach (string key in keys)
            originals.Add(key, await fixture.Coordinator.ReadGenerationJsonAsync(scope, generation, key, TestContext.Current.CancellationToken));
        ReadModelEntry<JsonNode> source = await fixture.Coordinator.ReadGenerationJsonAsync(scope, generation,
            fixture.SourceKey, TestContext.Current.CancellationToken);
        if (inherited)
        {
            source.SourceGeneration.ShouldNotBeNull().ShouldBeLessThan(generation); source.PointerETag.ShouldNotBeNullOrEmpty();
            foreach (var item in originals)
            {
                item.Value.SourceGeneration.ShouldNotBeNull().ShouldBeLessThan(generation); item.Value.PointerETag.ShouldNotBeNullOrEmpty();
                fixture.Store.SnapshotRaw(scope.StoreName, PhysicalKey(scope, generation, item.Key)).ShouldBeNull();
            }
        }
        bool afterDelete = scenario is "partial-delete" or "deleted-source-drift";
        fixture.Fault.InterruptRepairReservation = !afterDelete;
        fixture.Fault.InterruptRepairedKey = afterDelete ? PhysicalKey(scope, generation, keys[0]) : null;
        var migration = new MagicLinkTokenHashCapabilityIndexV6Migration(fixture.Coordinator, fixture.Store,
            new PermittedCertifiedMigration(), new CertifiedAbortLayout(fixture.Digest));
        await Should.ThrowAsync<IOException>(() => migration.RecoverAbortAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
        SharedProjectionAbortRepairReceipt receipt = (await fixture.Coordinator.GetAbortRepairAsync(scope, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        byte[] immutable = JsonSerializer.SerializeToUtf8Bytes(receipt);
        receipt.Request.Values.Count(value => value.OriginalJson is not null).ShouldBe(3);
        foreach (var item in originals)
        {
            SharedProjectionAbortRepairValue value = receipt.Request.Values.Single(value => value.LogicalKey == item.Key);
            JsonNode.DeepEquals(JsonNode.Parse(value.OriginalJson!), item.Value.Value).ShouldBeTrue();
            value.ExpectedETag.ShouldBe(item.Value.ETag);
            value.SourceGeneration.ShouldBe(item.Value.SourceGeneration); value.PointerETag.ShouldBe(item.Value.PointerETag);
        }
        SharedProjectionAbortRepairGuard guard = receipt.Request.SourceGuards.Single(value => value.LogicalKey == fixture.SourceKey);
        guard.ExpectedETag.ShouldBe(source.ETag); guard.SourceGeneration.ShouldBe(source.SourceGeneration);
        guard.PointerETag.ShouldBe(source.PointerETag);
        var remaining = new Dictionary<string, ReadModelEntry<JsonNode>>(StringComparer.Ordinal);
        foreach (string key in keys)
        {
            ReadModelEntry<JsonNode> current = await fixture.Coordinator.ReadGenerationJsonAsync(scope, generation, key, TestContext.Current.CancellationToken);
            remaining.Add(key, current);
            if (afterDelete && key == keys[0]) current.ETag.ShouldBeNullOrEmpty();
            else JsonNode.DeepEquals(current.Value, originals[key].Value).ShouldBeTrue();
        }
        if (scenario.Contains("pointer-drift", StringComparison.Ordinal))
        {
            string logical = scenario == "inherited-source-pointer-drift" ? fixture.SourceKey : keys[0];
            string pointer = "shared-cow-pointer:" + scope.ComputeHash() + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(logical)));
            byte[] bytes = fixture.Store.SnapshotRaw(scope.StoreName, pointer)!;
            fixture.Store.SeedRaw(scope.StoreName, pointer, JsonDocument.Parse(bytes));
            fixture.Store.SnapshotRaw(scope.StoreName, pointer).ShouldBe(bytes);
            (await fixture.Coordinator.ReadGenerationJsonAsync(scope, generation, logical,
                TestContext.Current.CancellationToken)).PointerETag.ShouldNotBe(logical == fixture.SourceKey ? source.PointerETag : originals[logical].PointerETag);
        }
        else if (refuses)
        {
            string physical = PhysicalKey(scope, source.SourceGeneration!.Value, fixture.SourceKey);
            byte[] bytes = fixture.Store.SnapshotRaw(scope.StoreName, physical)!;
            fixture.Store.SeedRaw(scope.StoreName, physical, JsonDocument.Parse(bytes));
            fixture.Store.SnapshotRaw(scope.StoreName, physical).ShouldBe(bytes);
            (await fixture.Coordinator.ReadGenerationJsonAsync(scope, generation, fixture.SourceKey,
                TestContext.Current.CancellationToken)).ETag.ShouldNotBe(source.ETag);
        }
        var coldCoordinator = new SharedProjectionEpochCoordinator(fixture.Store, fixture.Store);
        byte[] checkpoint = JsonSerializer.SerializeToUtf8Bytes(await coldCoordinator.GetCheckpointAsync(scope,
            "review-pending-owner", TestContext.Current.CancellationToken));
        if (refuses)
        {
            await Should.ThrowAsync<InvalidOperationException>(() => RecoverFixtureAsync(fixture.Store, fixture.Digest));
            foreach (var item in remaining)
            {
                ReadModelEntry<JsonNode> current = await coldCoordinator.ReadGenerationJsonAsync(scope, generation, item.Key, TestContext.Current.CancellationToken);
                current.ETag.ShouldBe(item.Value.ETag); JsonNode.DeepEquals(current.Value, item.Value.Value).ShouldBeTrue();
            }
        }
        else
        {
            await RecoverFixtureAsync(fixture.Store, fixture.Digest);
            foreach (string key in keys)
            {
                (await coldCoordinator.ReadGenerationJsonAsync(scope, generation, key, TestContext.Current.CancellationToken)).ETag.ShouldBeNullOrEmpty();
                if (inherited)
                    JsonNode.DeepEquals(JsonNode.Parse(fixture.Store.SnapshotRaw(scope.StoreName,
                        PhysicalKey(scope, originals[key].SourceGeneration!.Value, key))!), originals[key].Value).ShouldBeTrue();
            }
            await AssertFixtureDeliveryAsync(coldCoordinator, scope, fixture.Issued);
        }
        JsonSerializer.SerializeToUtf8Bytes(await coldCoordinator.GetAbortRepairAsync(scope, TestContext.Current.CancellationToken)).ShouldBe(immutable);
        JsonSerializer.SerializeToUtf8Bytes(await coldCoordinator.GetCheckpointAsync(scope,
            "review-pending-owner", TestContext.Current.CancellationToken)).ShouldBe(checkpoint);
        SharedProjectionEpochStatus after = await coldCoordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        after.Epoch.ShouldBe(fixture.Witness.Epoch); after.ActiveGeneration.ShouldBe(generation);
        after.IsBuilding.ShouldBeFalse(); after.IsAborting.ShouldBe(refuses); after.PendingDeliveryCount.ShouldBe(refuses ? 1 : 0);
    }

    private static string[] RepairMetadataKeys() => [MagicLinkTokenHashCapabilityIndexTreeReader.FormatKey,
        MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, MagicLinkTokenHashCapabilityIndexProjection.StateKey];

    private static async Task RecoverFixtureAsync(InMemoryReadModelStore store, string digest)
    {
        var cold = new MagicLinkTokenHashCapabilityIndexV6Migration(new SharedProjectionEpochCoordinator(store, store), store,
            new PermittedCertifiedMigration(), new CertifiedAbortLayout(digest));
        await cold.RecoverAbortAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken);
    }

    private static async Task AssertFixtureDeliveryAsync(SharedProjectionEpochCoordinator coordinator,
        SharedProjectionScope scope, MagicLinkConfirmationCapabilityIssued issued)
    {
        (await coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope,
            MagicLinkTokenHashCapabilityIndexProjection.ShardKey(issued.TokenHash.Value), TestContext.Current.CancellationToken))
            .Value.ShouldNotBeNull().Contributions[issued.TokenHash.Value].ShouldContain(new MagicLinkTokenHashCapabilityIndexEntry(issued.Tenant, issued.CapabilityId));
    }

    private static async Task<(InMemoryReadModelStore Store, V6AbortDrainFaultStore Fault,
        SharedProjectionEpochCoordinator Coordinator, MagicLinkV6MigrationAbortWitness Witness,
        string Digest, MagicLinkConfirmationCapabilityIssued Issued, string SourceKey)> PrepareRepairFixtureAsync(
        bool directory = false, bool inherited = false)
    {
        var store = new InMemoryReadModelStore();
        var interrupted = new InterruptSealStore(store);
        var coordinator = new SharedProjectionEpochCoordinator(interrupted, store);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        MagicLinkTokenHashCapabilityIndexDirectoryEntry original = await SeedMetadataSourceAsync(coordinator, scope);
        long originalGeneration = (await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken)).ActiveGeneration;
        int shard = MagicLinkTokenHashCapabilityIndexProjection.ShardOrdinal(original.TokenHash);
        string sourceKey = MagicLinkTokenHashCapabilityIndexProjection.ShardKey(shard);
        if (directory)
        {
            var page = new MagicLinkTokenHashCapabilityIndexDirectoryPage([original]);
            var root = MagicLinkTokenHashCapabilityIndexProjection.FromContributions([]) with
            {
                DirectoryPages = [new(0, original.TokenHash, original.TokenHash)],
                DirectoryPageDigests = new Dictionary<int, string> { [0] = MagicLinkTokenHashCapabilityIndexProjection.DirectoryPageDigest(page) },
                NextCandidatePageId = 1
            };
            MagicLinkTokenHashCapabilityIndexProjection.HasValidPageManifest(root).ShouldBeTrue();
            MagicLinkTokenHashCapabilityIndexProjection.HasValidDirectoryPage(root, shard, root.DirectoryPages[0], page).ShouldBeTrue();
            store.SeedRaw(scope.StoreName, PhysicalKey(scope, originalGeneration, sourceKey), root);
            sourceKey = MagicLinkTokenHashCapabilityIndexProjection.DirectoryPageKey(shard, 0);
            store.SeedRaw(scope.StoreName, PhysicalKey(scope, originalGeneration, sourceKey), page);
        }
        if (inherited)
        {
            _ = await coordinator.BeginAsync(scope, "sparse-selected-v6", "same-v6-source", new Dictionary<string, long>(), TestContext.Current.CancellationToken);
            _ = await coordinator.StageStreamingAsync(scope, "sparse-selected-v6", AsAsync([
                ReadModelBatchOperation.Write("fixture:sparse-generation", new { Selected = true }, ReadModelBatchConcurrency.LastWrite)]), TestContext.Current.CancellationToken);
            await coordinator.CommitAsync(scope, "sparse-selected-v6", TestContext.Current.CancellationToken);
        }
        var migration = new MagicLinkTokenHashCapabilityIndexV6Migration(coordinator, interrupted, new PermittedCertifiedMigration());
        long selected = (await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken)).ActiveGeneration;
        string digest = await migration.ComputeAbortRepairSourceFingerprintAsync(selected, TestContext.Current.CancellationToken);
        interrupted.InterruptAfterNextSeal = true;
        await Should.ThrowAsync<IOException>(() => migration.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
        SharedProjectionEpochStatus before = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        foreach (string kind in new[] { "format", "root", "single" })
            _ = CorruptSelectedMetadata(store, scope, inherited ? originalGeneration : selected, kind);
        if (inherited)
            foreach (string key in RepairMetadataKeys())
            {
                string pointer = "shared-cow-pointer:" + scope.ComputeHash() + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
                store.SeedRaw(scope.StoreName, pointer, new { LatestGeneration = originalGeneration,
                    LatestDeleted = false, PreviousGeneration = 0, PreviousDeleted = true, RetiredGeneration = (long?)null });
            }
        string pendingHash = "review-pending-hash";
        while (MagicLinkTokenHashCapabilityIndexProjection.ShardOrdinal(pendingHash) == shard) pendingHash += "x";
        var issued = new MagicLinkConfirmationCapabilityIssued(new("review-pending-owner"), new("tenant-a"), new("party-a"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-a")), new("activity-a"), new("entry-a"),
            MagicLinkTargetKind.ProposedTimeEntry, MagicLinkAllowedAction.Confirm, new(pendingHash),
            DateTimeOffset.UnixEpoch.AddDays(1), new("issuer-a"), DateTimeOffset.UnixEpoch.AddHours(1), new("timesheets", "pending"), true);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var projectionEvent = new ProjectionEventDto(nameof(StoredMagicLinkIssued), JsonSerializer.SerializeToUtf8Bytes(new StoredMagicLinkIssued(issued), json),
            "json", 1, DateTimeOffset.UnixEpoch, "correlation", "message", "user", 2);
        var delivery = new SharedProjectionDelivery("review-pending-owner", 2,
            JsonSerializer.SerializeToUtf8Bytes(new ProjectionRequest("tenant-a", "timesheets", "review-pending-owner", [projectionEvent]), json), [4, 2]);
        SharedProjectionLease lease = await coordinator.RefreshLeaseAsync(scope, MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId, TestContext.Current.CancellationToken);
        (await coordinator.JournalAsync(scope, lease, delivery, TestContext.Current.CancellationToken)).ShouldBe(SharedProjectionJournalResult.Journaled);
        interrupted.InterruptAfterAborting = true;
        await Should.ThrowAsync<IOException>(() => coordinator.AbortAsync(scope, "explicit-v7", (_, _, _) =>
            Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]), TestContext.Current.CancellationToken));
        MagicLinkV6MigrationAbortWitness witness = (await store.GetAsync<MagicLinkV6MigrationAbortWitness>(scope.StoreName,
            MagicLinkTokenHashCapabilityIndexV6Migration.AbortWitnessKey(before.Epoch), TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        var fault = new V6AbortDrainFaultStore(store);
        return (store, fault, new SharedProjectionEpochCoordinator(fault, fault), witness, digest, issued, sourceKey);
    }

    [Theory]
    [InlineData(935, 262144, true)]
    [InlineData(935, 262145, false)]
    [InlineData(3840, 0, false)]
    [InlineData(3841, 0, false)]
    public async Task CertifiedRepairHonorsSdkManifestBoundsWithoutLosingPersistedEvidence(
        int pageCount, int requestBytes, bool accepted)
    {
        const string operation = "explicit-v7";
        const string inventory = "certified-selected-v6";
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        MagicLinkTokenHashCapabilityIndexDirectoryEntry original = await SeedMetadataSourceAsync(coordinator, scope);
        long generation = (await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken)).ActiveGeneration;
        int shard = MagicLinkTokenHashCapabilityIndexProjection.ShardOrdinal(original.TokenHash);
        var sourceKeys = Enumerable.Range(0, MagicLinkTokenHashCapabilityIndexProjection.ShardCount)
            .Select(MagicLinkTokenHashCapabilityIndexProjection.ShardKey).ToList();
        var digests = new Dictionary<int, string>();
        for (int pageId = 0; pageId < pageCount; pageId++)
        {
            var page = new MagicLinkTokenHashCapabilityIndexPage(original.TokenHash,
                [pageId == 0 ? original.Candidate : Row("tenant-a", $"capacity-owner-{pageId}", original.TokenHash).Candidate]);
            digests.Add(pageId, MagicLinkTokenHashCapabilityIndexProjection.CandidatePageDigest(page));
            string key = MagicLinkTokenHashCapabilityIndexProjection.CandidatePageKey(shard, pageId);
            sourceKeys.Add(key);
            store.SeedRaw(scope.StoreName, PhysicalKey(scope, generation, key), page);
        }
        var root = MagicLinkTokenHashCapabilityIndexProjection.FromContributions([]) with
        {
            CandidatePageCounts = new Dictionary<string, int> { [original.TokenHash] = pageCount },
            CandidatePageIds = new Dictionary<string, IReadOnlyList<int>>
                { [original.TokenHash] = Enumerable.Range(0, pageCount).ToArray() },
            CandidatePageDigests = digests,
            NextCandidatePageId = pageCount
        };
        MagicLinkTokenHashCapabilityIndexProjection.HasValidPageManifest(root).ShouldBeTrue();
        store.SeedRaw(scope.StoreName, PhysicalKey(scope, generation,
            MagicLinkTokenHashCapabilityIndexProjection.ShardKey(shard)), root);
        var migration = new MagicLinkTokenHashCapabilityIndexV6Migration(coordinator, store, new PermittedCertifiedMigration());
        // This traverses and validates every selected v6 collision page before corruption.
        string sourceDigest = await migration.ComputeAbortRepairSourceFingerprintAsync(generation, TestContext.Current.CancellationToken);
        long epoch = await coordinator.BeginAsync(scope, operation, inventory, new Dictionary<string, long>(),
            TestContext.Current.CancellationToken, operationTenantId: scope.TenantId);
        var witness = new MagicLinkV6MigrationAbortWitness(scope.StoreName, scope.TenantId, scope.Domain,
            scope.Family, operation, inventory, epoch, generation);
        string witnessKey = MagicLinkTokenHashCapabilityIndexV6Migration.AbortWitnessKey(epoch);
        (await store.TrySaveAsync(scope.StoreName, witnessKey, witness, string.Empty, TestContext.Current.CancellationToken)).ShouldBeTrue();
        var issued = new MagicLinkConfirmationCapabilityIssued(new("capacity-pending-owner"), new("tenant-a"), new("party-a"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-a")), new("activity-a"), new("entry-a"),
            MagicLinkTargetKind.ProposedTimeEntry, MagicLinkAllowedAction.Confirm, new("capacity-pending-hash"),
            DateTimeOffset.UnixEpoch.AddDays(1), new("issuer-a"), DateTimeOffset.UnixEpoch.AddHours(1), new("timesheets", "pending"), true);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var projectionEvent = new ProjectionEventDto(nameof(StoredMagicLinkIssued), JsonSerializer.SerializeToUtf8Bytes(new StoredMagicLinkIssued(issued), json),
            "json", 1, DateTimeOffset.UnixEpoch, "correlation", "message", "user", 2);
        var delivery = new SharedProjectionDelivery("capacity-pending-owner", 2,
            JsonSerializer.SerializeToUtf8Bytes(new ProjectionRequest("tenant-a", "timesheets", "capacity-pending-owner", [projectionEvent]), json), [1, 2]);
        SharedProjectionLease lease = await coordinator.RefreshLeaseAsync(scope,
            MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId, TestContext.Current.CancellationToken);
        (await coordinator.JournalAsync(scope, lease, delivery, TestContext.Current.CancellationToken)).ShouldBe(SharedProjectionJournalResult.Journaled);
        await Should.ThrowAsync<IOException>(() => coordinator.AbortAsync(scope, operation, (_, _, _) =>
            throw new IOException("Interrupt before draining the original pending delivery"), TestContext.Current.CancellationToken));

        string damagedKey = PhysicalKey(scope, generation, MagicLinkTokenHashCapabilityIndexTreeReader.FormatKey);
        int padding = 1;
        SharedProjectionAbortRepairRequest measured;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            store.SeedRaw(scope.StoreName, damagedKey, new string('x', padding));
            measured = await MeasureRequestAsync();
            if (requestBytes == 0 || JsonSerializer.SerializeToUtf8Bytes(measured).Length == requestBytes) break;
            padding += requestBytes - JsonSerializer.SerializeToUtf8Bytes(measured).Length;
            padding.ShouldBeGreaterThan(0, "the valid v6 inventory itself must fit below the byte boundary");
        }
        measured = await MeasureRequestAsync();
        padding.ShouldBeLessThan(1024, "the complete source-guard inventory must already be near the SDK byte bound");
        measured.SourceGuards.Length.ShouldBe(256 + pageCount);
        int actualBytes = JsonSerializer.SerializeToUtf8Bytes(measured).Length;
        if (requestBytes != 0) actualBytes.ShouldBe(requestBytes);
        else actualBytes.ShouldBeGreaterThan(256 * 1024);
        TestContext.Current.TestOutputHelper?.WriteLine($"Valid v6 manifest: {measured.SourceGuards.Length} guards; {actualBytes} SDK request bytes; accepted={accepted}.");
        byte[] corrupt = store.SnapshotRaw(scope.StoreName, damagedKey)!;
        byte[] ownerCheckpoint = JsonSerializer.SerializeToUtf8Bytes((await coordinator.GetCheckpointAsync(scope,
            delivery.SourceStream, TestContext.Current.CancellationToken)).ShouldNotBeNull());
        byte[] originalWitness = store.SnapshotRaw(scope.StoreName, witnessKey)!;
        var selectedSource = sourceKeys.ToDictionary(key => key,
            key => store.SnapshotRaw(scope.StoreName, PhysicalKey(scope, generation, key)), StringComparer.Ordinal);
        var coldCoordinator = new SharedProjectionEpochCoordinator(store, store);
        var cold = new MagicLinkTokenHashCapabilityIndexV6Migration(coldCoordinator, store,
            new PermittedCertifiedMigration(), new CertifiedAbortLayout(sourceDigest));
        SharedProjectionEpochStatus before = await coldCoordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        string epochKey = "shared-epoch:" + scope.ComputeHash();
        byte[] control = store.SnapshotRaw(scope.StoreName, epochKey)!;
        if (accepted)
        {
            await cold.RecoverAbortAsync(operation, inventory, TestContext.Current.CancellationToken);
            SharedProjectionAbortRepairReceipt receipt = (await coldCoordinator.GetAbortRepairAsync(scope, TestContext.Current.CancellationToken)).ShouldNotBeNull();
            JsonSerializer.SerializeToUtf8Bytes(receipt.Request).Length.ShouldBe(actualBytes);
            receipt.Request.SourceGuards.Length.ShouldBe(measured.SourceGuards.Length);
            receipt.Request.Values.Single(value => value.OriginalJson is not null).OriginalJson.ShouldBe(JsonSerializer.Serialize(new string('x', padding)));
            store.SnapshotRaw(scope.StoreName, damagedKey).ShouldBeNull();
            (await coldCoordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope,
                MagicLinkTokenHashCapabilityIndexProjection.ShardKey(issued.TokenHash.Value), TestContext.Current.CancellationToken))
                .Value.ShouldNotBeNull().Contributions[issued.TokenHash.Value].ShouldContain(new MagicLinkTokenHashCapabilityIndexEntry(issued.Tenant, issued.CapabilityId));
        }
        else
        {
            await Should.ThrowAsync<ArgumentException>(() => cold.RecoverAbortAsync(operation, inventory, TestContext.Current.CancellationToken));
            store.SnapshotRaw(scope.StoreName, damagedKey).ShouldBe(corrupt);
            store.SnapshotRaw(scope.StoreName, epochKey).ShouldBe(control);
            (await coldCoordinator.GetAbortRepairAsync(scope, TestContext.Current.CancellationToken)).ShouldBeNull();
        }
        SharedProjectionEpochStatus after = await coldCoordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        after.ActiveGeneration.ShouldBe(generation);
        after.Epoch.ShouldBe(before.Epoch);
        after.IsBuilding.ShouldBeFalse();
        after.IsAborting.ShouldBe(!accepted);
        after.PendingDeliveryCount.ShouldBe(accepted ? 0 : 1);
        store.SnapshotRaw(scope.StoreName, witnessKey).ShouldBe(originalWitness);
        JsonSerializer.SerializeToUtf8Bytes(await coldCoordinator.GetCheckpointAsync(scope,
            delivery.SourceStream, TestContext.Current.CancellationToken)).ShouldBe(ownerCheckpoint);
        foreach (var item in selectedSource.Where(item => !accepted || item.Key !=
            MagicLinkTokenHashCapabilityIndexProjection.ShardKey(issued.TokenHash.Value)))
            store.SnapshotRaw(scope.StoreName, PhysicalKey(scope, generation, item.Key)).ShouldBe(item.Value);

        async Task<SharedProjectionAbortRepairRequest> MeasureRequestAsync()
        {
            var guards = new List<SharedProjectionAbortRepairGuard>();
            foreach (string key in sourceKeys)
            {
                ReadModelEntry<JsonNode> entry = await coordinator.ReadGenerationJsonAsync(scope, generation, key, TestContext.Current.CancellationToken);
                string? digest = string.IsNullOrEmpty(entry.ETag) ? null
                    : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(entry.Value?.ToJsonString() ?? "null")));
                guards.Add(new(key, entry.ETag ?? string.Empty, digest)
                    { SourceGeneration = entry.SourceGeneration, PointerETag = entry.PointerETag });
            }
            var values = new List<SharedProjectionAbortRepairValue>();
            foreach (string key in new[] { MagicLinkTokenHashCapabilityIndexTreeReader.FormatKey,
                MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, MagicLinkTokenHashCapabilityIndexProjection.StateKey })
            {
                ReadModelEntry<JsonNode> entry = await coordinator.ReadGenerationJsonAsync(scope, generation, key, TestContext.Current.CancellationToken);
                values.Add(new(key, entry.ETag ?? string.Empty, string.IsNullOrEmpty(entry.ETag) ? null : entry.Value?.ToJsonString() ?? "null")
                    { SourceGeneration = entry.SourceGeneration, PointerETag = entry.PointerETag });
            }
            return new(operation, inventory, epoch, generation, "original-layout-certificate", values.ToArray()) { SourceGuards = guards.ToArray() };
        }
    }

    [Theory]
    [InlineData("format", null)]
    [InlineData("root", null)]
    [InlineData("single", null)]
    [InlineData("format-json", null)]
    [InlineData("format-null", null)]
    [InlineData("format", "prepared-drain")]
    [InlineData("format", "committed-drain")]
    [InlineData("format", "reserved-repair")]
    [InlineData("format", "deleted-repair")]
    [InlineData("format", "reserved-foreign-certificate")]
    [InlineData("format", "foreign-keys")]
    [InlineData("format", "duplicate-keys")]
    [InlineData("format", "missing-keys")]
    [InlineData("format", "foreign-witness")]
    [InlineData("format", "foreign-source")]
    [InlineData("format", "layout-drift")]
    [InlineData("format", "null-source")]
    public async Task CertifiedRepairRestoresOriginalV6AbsenceAndDrainsPendingDeliveryAfterColdRestart(string damage, string? refusal)
    {
        var store = new InMemoryReadModelStore();
        var interrupted = new InterruptSealStore(store);
        var coordinator = new SharedProjectionEpochCoordinator(interrupted, store);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        MagicLinkTokenHashCapabilityIndexDirectoryEntry original = await SeedMetadataSourceAsync(coordinator, scope);
        var authority = new PermittedCertifiedMigration();
        var migration = new MagicLinkTokenHashCapabilityIndexV6Migration(coordinator, interrupted, authority);
        // Capture complete original key/ETag/content/absence evidence before corruption.
        string sourceDigest = await migration.ComputeAbortRepairSourceFingerprintAsync(
            (await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken)).ActiveGeneration,
            TestContext.Current.CancellationToken);
        interrupted.InterruptAfterNextSeal = true;
        await Should.ThrowAsync<IOException>(() => migration.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
        SharedProjectionEpochStatus before = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        string damagedKey = CorruptSelectedMetadata(store, scope, before.ActiveGeneration, damage);
        byte[] corrupt = store.SnapshotRaw(scope.StoreName, damagedKey)!;
        var issued = new MagicLinkConfirmationCapabilityIssued(new("repair-pending-owner"), new("tenant-a"), new("party-a"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-a")), new("activity-a"), new("entry-a"),
            MagicLinkTargetKind.ProposedTimeEntry, MagicLinkAllowedAction.Confirm, new("repair-pending-hash"),
            DateTimeOffset.UnixEpoch.AddDays(1), new("issuer-a"), DateTimeOffset.UnixEpoch.AddHours(1), new("timesheets", "pending"), true);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var projectionEvent = new ProjectionEventDto(nameof(StoredMagicLinkIssued), JsonSerializer.SerializeToUtf8Bytes(new StoredMagicLinkIssued(issued), json),
            "json", 1, DateTimeOffset.UnixEpoch, "correlation", "message", "user", 2);
        var delivery = new SharedProjectionDelivery("repair-pending-owner", 2,
            JsonSerializer.SerializeToUtf8Bytes(new ProjectionRequest("tenant-a", "timesheets", "repair-pending-owner", [projectionEvent]), json), []);
        SharedProjectionLease lease = await coordinator.RefreshLeaseAsync(scope, MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId, TestContext.Current.CancellationToken);
        (await coordinator.JournalAsync(scope, lease, delivery, TestContext.Current.CancellationToken)).ShouldBe(SharedProjectionJournalResult.Journaled);
        interrupted.InterruptAfterAborting = true;
        await Should.ThrowAsync<IOException>(() => migration.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
        // Default certification refuses, preserving the original bytes and journal.
        await Should.ThrowAsync<InvalidOperationException>(() => migration.RecoverAbortAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
        store.SnapshotRaw(scope.StoreName, damagedKey).ShouldBe(corrupt);
        (await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken)).PendingDeliveryCount.ShouldBe(1);
        var drainFault = new V6AbortDrainFaultStore(store);
        var coldCoordinator = new SharedProjectionEpochCoordinator(drainFault, drainFault);
        var certification = new CertifiedAbortLayout(sourceDigest)
        {
            Transform = certificate => refusal switch
            {
                "foreign-keys" => certificate with { OriginallyAbsentMetadataKeys = ["foreign", .. certificate.OriginallyAbsentMetadataKeys.Skip(1)] },
                "duplicate-keys" => certificate with { OriginallyAbsentMetadataKeys = [certificate.OriginallyAbsentMetadataKeys[0], certificate.OriginallyAbsentMetadataKeys[0], certificate.OriginallyAbsentMetadataKeys[2]] },
                "missing-keys" => certificate with { OriginallyAbsentMetadataKeys = [] },
                "foreign-witness" => certificate with { Witness = certificate.Witness with { Epoch = certificate.Witness.Epoch + 1 } },
                "foreign-source" => certificate with { SourceDigest = new string('0', 64) },
                _ => certificate
            }
        };
        if (refusal == "layout-drift")
        {
            string key = PhysicalKey(scope, before.ActiveGeneration, MagicLinkTokenHashCapabilityIndexProjection.ShardKey(original.TokenHash));
            var root = (await store.GetAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope.StoreName, key, TestContext.Current.CancellationToken)).Value!;
            byte[] prior = store.SnapshotRaw(scope.StoreName, key)!;
            store.SeedRaw(scope.StoreName, key, JsonDocument.Parse(prior)); // Identical rows/JSON, different original-layout ETag.
            store.SnapshotRaw(scope.StoreName, key).ShouldBe(prior);
        }
        if (refusal == "null-source")
        {
            int shard = (MagicLinkTokenHashCapabilityIndexProjection.ShardOrdinal(original.TokenHash) + 1) % 256;
            store.SeedRaw(scope.StoreName, PhysicalKey(scope, before.ActiveGeneration,
                MagicLinkTokenHashCapabilityIndexProjection.ShardKey(shard)), JsonDocument.Parse("null"));
        }
        var cold = new MagicLinkTokenHashCapabilityIndexV6Migration(coldCoordinator, store, authority, certification);
        if (refusal is not null && refusal is not ("prepared-drain" or "committed-drain"
            or "reserved-repair" or "deleted-repair" or "reserved-foreign-certificate"))
        {
            await Should.ThrowAsync<InvalidOperationException>(() => cold.RecoverAbortAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
            store.SnapshotRaw(scope.StoreName, damagedKey).ShouldBe(corrupt);
            SharedProjectionEpochStatus refused = await coldCoordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
            refused.IsAborting.ShouldBeTrue(); refused.PendingDeliveryCount.ShouldBe(1);
            refused.ActiveGeneration.ShouldBe(before.ActiveGeneration);
            return;
        }
        if (refusal is "prepared-drain" or "committed-drain" or "reserved-repair"
            or "deleted-repair" or "reserved-foreign-certificate")
        {
            drainFault.InterruptPrepared = refusal == "prepared-drain";
            drainFault.InterruptRepairReservation = refusal is "reserved-repair" or "reserved-foreign-certificate";
            drainFault.InterruptRepairedKey = refusal == "deleted-repair" ? damagedKey : null;
            drainFault.InterruptCommittedKey = refusal == "committed-drain"
                ? PhysicalKey(scope, before.ActiveGeneration, MagicLinkTokenHashCapabilityIndexProjection.ShardKey("repair-pending-hash")) : null;
            if (refusal != "committed-drain")
                await Should.ThrowAsync<IOException>(() => cold.RecoverAbortAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
            else
                await Should.ThrowAsync<SharedProjectionWriteUnavailableException>(() => cold.RecoverAbortAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
            (await coldCoordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken)).PendingDeliveryCount.ShouldBe(1);
            if (refusal is "reserved-repair" or "deleted-repair" or "reserved-foreign-certificate")
            {
                SharedProjectionAbortRepairReceipt reserved = (await coldCoordinator.GetAbortRepairAsync(scope, TestContext.Current.CancellationToken)).ShouldNotBeNull();
                JsonNode.DeepEquals(JsonNode.Parse(reserved.Request.Values.Single(value => value.OriginalJson is not null).OriginalJson!),
                    JsonNode.Parse(corrupt)).ShouldBeTrue();
                store.SnapshotRaw(scope.StoreName, damagedKey).ShouldBe(refusal == "deleted-repair" ? null : corrupt);
            }
            coldCoordinator = new SharedProjectionEpochCoordinator(store, store);
            cold = new MagicLinkTokenHashCapabilityIndexV6Migration(coldCoordinator, store, authority, certification);
            if (refusal == "reserved-foreign-certificate")
            {
                var foreign = new MagicLinkTokenHashCapabilityIndexV6Migration(coldCoordinator, store, authority,
                    new CertifiedAbortLayout(sourceDigest) { Transform = certificate => certificate with { CertificateId = "foreign-certificate" } });
                await Should.ThrowAsync<InvalidOperationException>(() => foreign.RecoverAbortAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
                store.SnapshotRaw(scope.StoreName, damagedKey).ShouldBe(corrupt);
                SharedProjectionEpochStatus refused = await coldCoordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
                refused.IsAborting.ShouldBeTrue(); refused.PendingDeliveryCount.ShouldBe(1);
                refused.ActiveGeneration.ShouldBe(before.ActiveGeneration);
                // The original immutable binding remains resumable after rejecting the foreign certificate.
            }
        }
        await cold.RecoverAbortAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken);
        SharedProjectionEpochStatus after = await coldCoordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        after.IsAborting.ShouldBeFalse(); after.IsBuilding.ShouldBeFalse(); after.PendingDeliveryCount.ShouldBe(0);
        after.ActiveGeneration.ShouldBe(before.ActiveGeneration);
        store.SnapshotRaw(scope.StoreName, damagedKey).ShouldBeNull();
        var retained = (await coldCoordinator.GetAbortRepairAsync(scope, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        retained.Request.Values.Count(value => value.OriginalJson is not null).ShouldBe(1);
        JsonNode.DeepEquals(JsonNode.Parse(retained.Request.Values.Single(value => value.OriginalJson is not null).OriginalJson!),
            JsonNode.Parse(corrupt)).ShouldBeTrue();
        var pending = (await coldCoordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope,
            MagicLinkTokenHashCapabilityIndexProjection.ShardKey("repair-pending-hash"), TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        pending.Contributions["repair-pending-hash"].ShouldContain(new MagicLinkTokenHashCapabilityIndexEntry(issued.Tenant, issued.CapabilityId));
    }


    [Fact]
    public void OriginalThreeArgumentMigrationConstructorRemainsAvailable()
        => typeof(MagicLinkTokenHashCapabilityIndexV6Migration).GetConstructor([
            typeof(SharedProjectionEpochCoordinator), typeof(IReadModelStore), typeof(IMagicLinkIndexMigrationAuthority)])
            .ShouldNotBeNull();

    [Fact]
    public async Task ColdAuthorizedAbortRecoveryResumesAndRepeatedRecoveryNeverPromotes()
    {
        var store = new InMemoryReadModelStore();
        var interrupted = new InterruptSealStore(store);
        var coordinator = new SharedProjectionEpochCoordinator(interrupted, store);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        _ = await SeedMetadataSourceAsync(coordinator, scope);
        var authority = new PermittedCertifiedMigration();
        var migration = new MagicLinkTokenHashCapabilityIndexV6Migration(coordinator, interrupted, authority);
        interrupted.InterruptAfterNextSeal = true;
        await Should.ThrowAsync<IOException>(() => migration.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
        SharedProjectionEpochStatus before = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        string corruptKey = CorruptSelectedMetadata(store, scope, before.ActiveGeneration, "format");
        ReadModelEntry<JsonObject> corrupt = await store.GetAsync<JsonObject>(scope.StoreName, corruptKey, TestContext.Current.CancellationToken);
        interrupted.InterruptAfterAborting = true;
        await Should.ThrowAsync<IOException>(() => migration.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
        (await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken)).IsAborting.ShouldBeTrue();
        ReadModelEntry<MagicLinkV6MigrationAbortWitness> witness = await store.GetAsync<MagicLinkV6MigrationAbortWitness>(
            scope.StoreName, MagicLinkTokenHashCapabilityIndexV6Migration.AbortWitnessKey(before.Epoch), TestContext.Current.CancellationToken);
        witness.Value.ShouldNotBeNull().InventoryFingerprint.ShouldBe("certified-selected-v6");

        // New coordinator and migration objects model a process restart with only durable bindings.
        var coldCoordinator = new SharedProjectionEpochCoordinator(store, store);
        var cold = new MagicLinkTokenHashCapabilityIndexV6Migration(coldCoordinator, store, authority);
        await Should.ThrowAsync<InvalidOperationException>(() => cold.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
        // Assert migration retry itself completed cleanup before an explicit idempotent recovery can mask it.
        SharedProjectionEpochStatus afterRetry = await coldCoordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        afterRetry.IsAborting.ShouldBeFalse(); afterRetry.IsBuilding.ShouldBeFalse(); afterRetry.PendingDeliveryCount.ShouldBe(0);
        afterRetry.ActiveGeneration.ShouldBe(before.ActiveGeneration); afterRetry.Epoch.ShouldBe(before.Epoch);
        await cold.RecoverAbortAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken);
        SharedProjectionEpochStatus after = await coldCoordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        after.IsAborting.ShouldBeFalse(); after.IsBuilding.ShouldBeFalse(); after.PendingDeliveryCount.ShouldBe(0);
        after.ActiveGeneration.ShouldBe(before.ActiveGeneration); after.Epoch.ShouldBe(before.Epoch);
        (await store.GetAsync<JsonObject>(scope.StoreName, corruptKey, TestContext.Current.CancellationToken)).ETag.ShouldBe(corrupt.ETag);
        (await store.GetAsync<MagicLinkV6MigrationAbortWitness>(scope.StoreName,
            MagicLinkTokenHashCapabilityIndexV6Migration.AbortWitnessKey(before.Epoch), TestContext.Current.CancellationToken)).ETag.ShouldBe(witness.ETag);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ColdRecoveryDrainsHealthyPendingDeliveryAndResumesInterruptedStageDeletion(bool interruptDeletion)
    {
        var store = new InMemoryReadModelStore();
        var interrupted = new InterruptSealStore(store);
        var batches = new InterruptCheckpointBatch(store);
        var coordinator = new SharedProjectionEpochCoordinator(interrupted, batches);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        MagicLinkTokenHashCapabilityIndexDirectoryEntry original = await SeedMetadataSourceAsync(coordinator, scope);
        var migration = new MagicLinkTokenHashCapabilityIndexV6Migration(coordinator, interrupted, new PermittedCertifiedMigration());
        interrupted.InterruptAfterNextSeal = true;
        await Should.ThrowAsync<IOException>(() => migration.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
        SharedProjectionEpochStatus before = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        string privatePrefix = "shared-generation:" + scope.ComputeHash() + ":" + before.Epoch + ":";
        string[] privateKeys = batches.WrittenKeys.Where(key => key.StartsWith(privatePrefix, StringComparison.Ordinal)).ToArray();
        string[] metadataKeys = batches.WrittenKeys.Where(key => (key.Contains(":stage-page:", StringComparison.Ordinal) || key.Contains(":stage-key:", StringComparison.Ordinal))).ToArray();
        privateKeys.ShouldNotBeEmpty(); metadataKeys.ShouldNotBeEmpty();
        var issued = new MagicLinkConfirmationCapabilityIssued(new("pending-owner"), new("tenant-a"), new("party-a"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-a")), new("activity-a"), new("entry-a"),
            MagicLinkTargetKind.ProposedTimeEntry, MagicLinkAllowedAction.Confirm, new("pending-hash"),
            DateTimeOffset.UnixEpoch.AddDays(1), new("issuer-a"), DateTimeOffset.UnixEpoch.AddHours(1),
            new("timesheets", "pending-source"), true);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var projectionEvent = new ProjectionEventDto(nameof(StoredMagicLinkIssued),
            JsonSerializer.SerializeToUtf8Bytes(new StoredMagicLinkIssued(issued), json), "json", 1,
            DateTimeOffset.UnixEpoch, "correlation", "message", "user", 2);
        var delivery = new SharedProjectionDelivery("pending-owner", 2,
            JsonSerializer.SerializeToUtf8Bytes(new ProjectionRequest("tenant-a", "timesheets", "pending-owner", [projectionEvent]), json), []);
        SharedProjectionLease lease = await coordinator.RefreshLeaseAsync(scope,
            MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId, TestContext.Current.CancellationToken);
        (await coordinator.JournalAsync(scope, lease, delivery, TestContext.Current.CancellationToken)).ShouldBe(SharedProjectionJournalResult.Journaled);
        interrupted.InterruptAfterAborting = true;
        await Should.ThrowAsync<IOException>(() => coordinator.AbortAsync(scope, "explicit-v7", (_, _, _) =>
            Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]), TestContext.Current.CancellationToken));
        (await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken)).PendingDeliveryCount.ShouldBe(1);
        batches.InterruptDuringStageDeletion = interruptDeletion;
        if (interruptDeletion)
        {
            await Should.ThrowAsync<IOException>(() => migration.RecoverAbortAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
            (await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken)).IsAborting.ShouldBeTrue();
            // This fault follows an actual private-value delete commit, before metadata cleanup.
            privateKeys.Any(key => store.SnapshotRaw(scope.StoreName, key) is null).ShouldBeTrue();
            metadataKeys.Any(key => store.SnapshotRaw(scope.StoreName, key) is not null).ShouldBeTrue();
        }
        var coldCoordinator = new SharedProjectionEpochCoordinator(store, store);
        var cold = new MagicLinkTokenHashCapabilityIndexV6Migration(coldCoordinator, store, new PermittedCertifiedMigration());
        await cold.RecoverAbortAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken);
        SharedProjectionEpochStatus after = await coldCoordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        after.IsAborting.ShouldBeFalse(); after.IsBuilding.ShouldBeFalse(); after.PendingDeliveryCount.ShouldBe(0);
        after.ActiveGeneration.ShouldBe(before.ActiveGeneration);
        foreach (string key in privateKeys.Concat(metadataKeys)) store.SnapshotRaw(scope.StoreName, key).ShouldBeNull();
        var pending = (await coldCoordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope,
            MagicLinkTokenHashCapabilityIndexProjection.ShardKey("pending-hash"), TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        pending.Contributions["pending-hash"].ShouldContain(new MagicLinkTokenHashCapabilityIndexEntry(issued.Tenant, issued.CapabilityId));
        var selectedOriginal = (await coldCoordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope,
            MagicLinkTokenHashCapabilityIndexProjection.ShardKey(original.TokenHash), TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        selectedOriginal.Contributions[original.TokenHash].ShouldContain(original.Candidate);
        await cold.RecoverAbortAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("scope")]
    [InlineData("operation")]
    [InlineData("inventory")]
    [InlineData("epoch")]
    [InlineData("generation")]
    [InlineData("corrupt-json")]
    [InlineData("authority")]
    [InlineData("caller-operation")]
    [InlineData("caller-inventory")]
    public async Task AbortRecoveryRefusesForeignMissingOrCorruptBindingsWithoutMutation(string mismatch)
    {
        var store = new InMemoryReadModelStore();
        var interrupted = new InterruptSealStore(store);
        var coordinator = new SharedProjectionEpochCoordinator(interrupted, store);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        var sourceRow = await SeedMetadataSourceAsync(coordinator, scope);
        var authority = new PermittedCertifiedMigration { AllowAnyFingerprint = true };
        var migration = new MagicLinkTokenHashCapabilityIndexV6Migration(coordinator, interrupted, authority);
        interrupted.InterruptAfterNextSeal = true;
        await Should.ThrowAsync<IOException>(() => migration.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
        SharedProjectionEpochStatus before = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        _ = CorruptSelectedMetadata(store, scope, before.ActiveGeneration, "format");
        interrupted.InterruptAfterAborting = true;
        await Should.ThrowAsync<IOException>(() => migration.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
        string witnessKey = MagicLinkTokenHashCapabilityIndexV6Migration.AbortWitnessKey(before.Epoch);
        ReadModelEntry<MagicLinkV6MigrationAbortWitness> entry = await store.GetAsync<MagicLinkV6MigrationAbortWitness>(scope.StoreName,
            witnessKey, TestContext.Current.CancellationToken);
        MagicLinkV6MigrationAbortWitness original = entry.Value.ShouldNotBeNull();
        if (mismatch == "missing")
            (await store.TryEraseAsync(scope.StoreName, witnessKey, entry.ETag!, TestContext.Current.CancellationToken)).ShouldBeTrue();
        else if (mismatch == "corrupt-json") store.SeedRaw(scope.StoreName, witnessKey, new { Epoch = "invalid" });
        else if (mismatch == "authority") authority.Denied = true;
        else if (!mismatch.StartsWith("caller-", StringComparison.Ordinal))
            store.SeedRaw(scope.StoreName, witnessKey, mismatch switch
            {
                "scope" => original with { TenantId = "foreign" },
                "operation" => original with { OperationId = "foreign" },
                "inventory" => original with { InventoryFingerprint = "foreign" },
                "epoch" => original with { Epoch = original.Epoch + 1 },
                _ => original with { SourceGeneration = original.SourceGeneration + 1 }
            });
        string epochKey = "shared-epoch:" + scope.ComputeHash();
        var epochBefore = await store.GetAsync<JsonObject>(scope.StoreName, epochKey, TestContext.Current.CancellationToken);
        var witnessBefore = await store.GetAsync<JsonObject>(scope.StoreName, witnessKey, TestContext.Current.CancellationToken);
        string[] sourceKeys =
        [
            PhysicalKey(scope, before.ActiveGeneration, MagicLinkTokenHashCapabilityIndexTreeReader.FormatKey),
            PhysicalKey(scope, before.ActiveGeneration, MagicLinkTokenHashCapabilityIndexTreeReader.RootKey),
            PhysicalKey(scope, before.ActiveGeneration, MagicLinkTokenHashCapabilityIndexProjection.StateKey),
            PhysicalKey(scope, before.ActiveGeneration, MagicLinkTokenHashCapabilityIndexProjection.ShardKey(sourceRow.TokenHash)),
            MagicLinkTokenHashCapabilityIndexProjection.StateKey
        ];
        async Task<Dictionary<string, ReadModelEntry<JsonObject>>> SnapshotSourceAsync()
        {
            var snapshots = new Dictionary<string, ReadModelEntry<JsonObject>>(StringComparer.Ordinal);
            foreach (string key in sourceKeys)
                snapshots[key] = await store.GetAsync<JsonObject>(scope.StoreName, key, TestContext.Current.CancellationToken);
            return snapshots;
        }
        async Task AssertSourceUnchangedAsync(Dictionary<string, ReadModelEntry<JsonObject>> snapshots)
        {
            foreach ((string key, ReadModelEntry<JsonObject> snapshot) in snapshots)
            {
                var retained = await store.GetAsync<JsonObject>(scope.StoreName, key, TestContext.Current.CancellationToken);
                retained.ETag.ShouldBe(snapshot.ETag);
                (retained.Value?.ToJsonString()).ShouldBe(snapshot.Value?.ToJsonString());
            }
        }
        var recoverySource = await SnapshotSourceAsync();
        Exception? failure = await Record.ExceptionAsync(() => migration.RecoverAbortAsync(
            mismatch == "caller-operation" ? "foreign" : "explicit-v7",
            mismatch == "caller-inventory" ? "foreign" : "certified-selected-v6", TestContext.Current.CancellationToken));
        failure.ShouldNotBeNull();
        await AssertSourceUnchangedAsync(recoverySource);
        var migrationSource = await SnapshotSourceAsync();
        (await Record.ExceptionAsync(() => migration.MigrateAsync(
            mismatch == "caller-operation" ? "foreign" : "explicit-v7",
            mismatch == "caller-inventory" ? "foreign" : "certified-selected-v6",
            TestContext.Current.CancellationToken))).ShouldNotBeNull();
        await AssertSourceUnchangedAsync(migrationSource);
        (await store.GetAsync<JsonObject>(scope.StoreName, epochKey, TestContext.Current.CancellationToken)).ETag.ShouldBe(epochBefore.ETag);
        (await store.GetAsync<JsonObject>(scope.StoreName, witnessKey, TestContext.Current.CancellationToken)).ETag.ShouldBe(witnessBefore.ETag);
        (await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken)).IsAborting.ShouldBeTrue();
    }

    [Fact]
    public async Task RecoveryRechecksEpochAfterWitnessReadAndNeverAbortsReplacement()
    {
        var store = new InMemoryReadModelStore();
        var interrupted = new InterruptSealStore(store);
        var coordinator = new SharedProjectionEpochCoordinator(interrupted, store);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        _ = await SeedMetadataSourceAsync(coordinator, scope);
        var migration = new MagicLinkTokenHashCapabilityIndexV6Migration(coordinator, interrupted, new PermittedCertifiedMigration());
        interrupted.InterruptAfterNextSeal = true;
        await Should.ThrowAsync<IOException>(() => migration.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
        SharedProjectionEpochStatus old = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        _ = CorruptSelectedMetadata(store, scope, old.ActiveGeneration, "format");
        interrupted.InterruptAfterAborting = true;
        await Should.ThrowAsync<IOException>(() => migration.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
        long replacementEpoch = 0;
        interrupted.BeforeWitnessReturnAsync = async () =>
        {
            await coordinator.AbortAsync(scope, "explicit-v7", (_, _, _) =>
                Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]), TestContext.Current.CancellationToken);
            // Deliberately simulate a second operator violating the unique-ID rule. The SDK's
            // operation-only AbortAsync would otherwise accept this replacement reservation.
            replacementEpoch = await coordinator.BeginAsync(scope, "explicit-v7", "replacement-inventory",
                new Dictionary<string, long>(), TestContext.Current.CancellationToken, operationTenantId: scope.TenantId);
        };
        await Should.ThrowAsync<InvalidOperationException>(() => migration.RecoverAbortAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
        replacementEpoch.ShouldBeGreaterThan(old.Epoch);
        SharedProjectionEpochStatus retained = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        retained.Epoch.ShouldBe(replacementEpoch); retained.ActiveGeneration.ShouldBe(old.ActiveGeneration);
        retained.OperationId.ShouldBe("explicit-v7"); retained.IsBuilding.ShouldBeTrue(); retained.IsAborting.ShouldBeFalse();
        (await store.GetAsync<MagicLinkV6MigrationAbortWitness>(scope.StoreName,
            MagicLinkTokenHashCapabilityIndexV6Migration.AbortWitnessKey(replacementEpoch), TestContext.Current.CancellationToken)).Value.ShouldBeNull();
    }

    [Fact]
    public async Task WitnessPersistedBeforeWriteFaultReusesBindingAndETagOnRetry()
    {
        var store = new InMemoryReadModelStore();
        var interrupted = new InterruptSealStore(store);
        var coordinator = new SharedProjectionEpochCoordinator(interrupted, store);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        _ = await SeedMetadataSourceAsync(coordinator, scope);
        interrupted.InterruptAfterWitnessWrite = true;
        var migration = new MagicLinkTokenHashCapabilityIndexV6Migration(coordinator, interrupted, new PermittedCertifiedMigration());
        await Should.ThrowAsync<IOException>(() => migration.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
        SharedProjectionEpochStatus before = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        before.IsBuilding.ShouldBeTrue(); before.IsAborting.ShouldBeFalse();
        before.ActiveGeneration.ShouldBeLessThan(before.Epoch);
        string witnessKey = MagicLinkTokenHashCapabilityIndexV6Migration.AbortWitnessKey(before.Epoch);
        ReadModelEntry<MagicLinkV6MigrationAbortWitness> witness = await store.GetAsync<MagicLinkV6MigrationAbortWitness>(
            scope.StoreName, witnessKey, TestContext.Current.CancellationToken);
        witness.Value.ShouldNotBeNull().SourceGeneration.ShouldBe(before.ActiveGeneration);
        witness.Value.InventoryFingerprint.ShouldBe("certified-selected-v6");
        var cold = new MagicLinkTokenHashCapabilityIndexV6Migration(new SharedProjectionEpochCoordinator(store, store),
            store, new PermittedCertifiedMigration());
        await cold.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken);
        ReadModelEntry<MagicLinkV6MigrationAbortWitness> reused = await store.GetAsync<MagicLinkV6MigrationAbortWitness>(
            scope.StoreName, witnessKey, TestContext.Current.CancellationToken);
        reused.Value.ShouldBe(witness.Value); reused.ETag.ShouldBe(witness.ETag);
        (await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken)).ActiveGeneration.ShouldBe(before.Epoch);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WitnessInitializationFailureLeavesBuildingRetryable(bool validationFailure)
    {
        var store = new InMemoryReadModelStore();
        var interrupted = new InterruptSealStore(store);
        var coordinator = new SharedProjectionEpochCoordinator(interrupted, store);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        _ = await SeedMetadataSourceAsync(coordinator, scope);
        interrupted.WitnessWriteFailure = validationFailure ? new InvalidOperationException("Witness fault") : new IOException("Witness fault");
        var migration = new MagicLinkTokenHashCapabilityIndexV6Migration(coordinator, interrupted, new PermittedCertifiedMigration());
        (await Record.ExceptionAsync(() => migration.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken))).ShouldNotBeNull();
        SharedProjectionEpochStatus before = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        before.IsBuilding.ShouldBeTrue(); before.IsAborting.ShouldBeFalse(); before.ActiveGeneration.ShouldBeLessThan(before.Epoch);
        (await store.GetAsync<MagicLinkV6MigrationAbortWitness>(scope.StoreName,
            MagicLinkTokenHashCapabilityIndexV6Migration.AbortWitnessKey(before.Epoch), TestContext.Current.CancellationToken)).Value.ShouldBeNull();
        await Should.ThrowAsync<InvalidOperationException>(() => migration.RecoverAbortAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
        await migration.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken);
        (await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken)).ActiveGeneration.ShouldBe(before.Epoch);
        await Should.ThrowAsync<InvalidOperationException>(() => migration.RecoverAbortAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("format", false)]
    [InlineData("root", false)]
    [InlineData("selected-single", false)]
    [InlineData("direct-single", false)]
    [InlineData("format", true)]
    [InlineData("root", true)]
    [InlineData("selected-single", true)]
    [InlineData("direct-single", true)]
    [InlineData("format-json", false)]
    [InlineData("root-json", false)]
    [InlineData("selected-single-json", false)]
    [InlineData("direct-single-json", false)]
    [InlineData("format-json", true)]
    [InlineData("root-json", true)]
    [InlineData("selected-single-json", true)]
    [InlineData("direct-single-json", true)]
    [InlineData("wrapped-format-json", false)]
    [InlineData("wrapped-format-json", true)]
    public async Task InvalidSelectedMetadataOnRetryReleasesOnlyItsBuildingFence(string damage, bool sealedStage)
    {
        var store = new InMemoryReadModelStore();
        var interrupted = new InterruptSealStore(store);
        var batches = new InterruptCheckpointBatch(store);
        var coordinator = new SharedProjectionEpochCoordinator(interrupted, batches);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        var row = await SeedMetadataSourceAsync(coordinator, scope);
        var migration = new MagicLinkTokenHashCapabilityIndexV6Migration(coordinator, interrupted, new PermittedCertifiedMigration());
        if (sealedStage) interrupted.InterruptAfterNextSeal = true;
        else batches.InterruptBeforeFirstTenantCheckpoint = true;
        await Should.ThrowAsync<IOException>(() => migration.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
        SharedProjectionEpochStatus before = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        before.IsBuilding.ShouldBeTrue();
        (before.StageFingerprint is not null).ShouldBe(sealedStage);
        string damagedKey = CorruptSelectedMetadata(store, scope, before.ActiveGeneration, damage);
        interrupted.WrapInvalidJson = damage.StartsWith("wrapped-", StringComparison.Ordinal);
        string sourceKey = PhysicalKey(scope, before.ActiveGeneration, MagicLinkTokenHashCapabilityIndexProjection.ShardKey(row.TokenHash));
        var keys = new[] { damagedKey, sourceKey };
        var snapshots = new Dictionary<string, ReadModelEntry<JsonObject>>();
        foreach (string key in keys)
            snapshots[key] = await store.GetAsync<JsonObject>(scope.StoreName, key, TestContext.Current.CancellationToken);
        int appends = batches.StageAppendCalls;

        Exception? refused = await Record.ExceptionAsync(() => migration.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
        if (damage.StartsWith("wrapped-", StringComparison.Ordinal)) refused.ShouldBeOfType<SharedProjectionReadUnavailableException>()
            .InnerException.ShouldBeOfType<System.Text.Json.JsonException>();
        else if (damage.EndsWith("-json", StringComparison.Ordinal)) refused.ShouldBeOfType<System.Text.Json.JsonException>();
        else refused.ShouldBeOfType<InvalidOperationException>();

        SharedProjectionEpochStatus after = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        after.IsBuilding.ShouldBeFalse(); after.IsAborting.ShouldBeFalse();
        after.ActiveGeneration.ShouldBe(before.ActiveGeneration);
        after.Epoch.ShouldBe(before.Epoch);
        batches.StageAppendCalls.ShouldBe(appends);
        foreach (string key in keys)
        {
            ReadModelEntry<JsonObject> retained = await store.GetAsync<JsonObject>(scope.StoreName, key, TestContext.Current.CancellationToken);
            retained.ETag.ShouldBe(snapshots[key].ETag);
            retained.Value?.ToJsonString().ShouldBe(snapshots[key].Value?.ToJsonString());
        }
    }

    [Theory]
    [InlineData("format")]
    [InlineData("root")]
    [InlineData("selected-single")]
    [InlineData("direct-single")]
    public async Task FreshInvalidSelectedMetadataNeverReservesAFence(string damage)
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        _ = await SeedMetadataSourceAsync(coordinator, scope);
        SharedProjectionEpochStatus before = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        string key = CorruptSelectedMetadata(store, scope, before.ActiveGeneration, damage);
        ReadModelEntry<JsonObject> snapshot = await store.GetAsync<JsonObject>(scope.StoreName, key, TestContext.Current.CancellationToken);
        var migration = new MagicLinkTokenHashCapabilityIndexV6Migration(coordinator, store, new PermittedCertifiedMigration());
        await Should.ThrowAsync<InvalidOperationException>(() => migration.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
        SharedProjectionEpochStatus after = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        after.IsBuilding.ShouldBeFalse(); after.ActiveGeneration.ShouldBe(before.ActiveGeneration); after.Epoch.ShouldBe(before.Epoch);
        (await store.GetAsync<JsonObject>(scope.StoreName, key, TestContext.Current.CancellationToken)).ETag.ShouldBe(snapshot.ETag);
    }

    [Theory]
    [InlineData("foreign-operation")]
    [InlineData("foreign-inventory")]
    [InlineData("authority-denied")]
    public async Task RetryMustProveOperationAndInventoryAuthorityBeforeOwningAbort(string mismatch)
    {
        var store = new InMemoryReadModelStore();
        var interrupted = new InterruptSealStore(store);
        var coordinator = new SharedProjectionEpochCoordinator(interrupted, store);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        _ = await SeedMetadataSourceAsync(coordinator, scope);
        var authority = new PermittedCertifiedMigration { AllowAnyFingerprint = true };
        var migration = new MagicLinkTokenHashCapabilityIndexV6Migration(coordinator, interrupted, authority);
        interrupted.InterruptAfterNextSeal = true;
        await Should.ThrowAsync<IOException>(() => migration.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
        SharedProjectionEpochStatus before = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        if (mismatch == "authority-denied")
            authority.Denied = true;
        Exception? failure = await Record.ExceptionAsync(() => migration.MigrateAsync(
            mismatch == "foreign-operation" ? "other-operation" : "explicit-v7",
            mismatch == "foreign-inventory" ? "other-certified-inventory" : "certified-selected-v6", TestContext.Current.CancellationToken));
        if (mismatch == "authority-denied") failure.ShouldBeOfType<UnauthorizedAccessException>();
        else failure.ShouldBeOfType<InvalidOperationException>();
        SharedProjectionEpochStatus after = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        after.IsBuilding.ShouldBeTrue(); after.ActiveGeneration.ShouldBe(before.ActiveGeneration);
        after.Epoch.ShouldBe(before.Epoch); after.StageFingerprint.ShouldBe(before.StageFingerprint);
        authority.Denied = false;
        await migration.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken);
        await migration.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken);
        (await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken)).ActiveGeneration.ShouldBe(before.Epoch);
    }

    private static async Task<MagicLinkTokenHashCapabilityIndexDirectoryEntry> SeedMetadataSourceAsync(
        SharedProjectionEpochCoordinator coordinator, SharedProjectionScope scope)
    {
        _ = await coordinator.RegisterWriterAsync(scope, MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId, TestContext.Current.CancellationToken);
        var row = Row("tenant-a", "owner-a", "hash-a");
        _ = await coordinator.BeginAsync(scope, "metadata-source", "source", new Dictionary<string, long>(), TestContext.Current.CancellationToken);
        _ = await coordinator.StageStreamingAsync(scope, "metadata-source", AsAsync([
            ReadModelBatchOperation.Write(MagicLinkTokenHashCapabilityIndexProjection.ShardKey(row.TokenHash),
                MagicLinkTokenHashCapabilityIndexProjection.FromContributions([new KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>(row.TokenHash, row.Candidate)]),
                ReadModelBatchConcurrency.LastWrite)]), TestContext.Current.CancellationToken);
        await coordinator.CommitAsync(scope, "metadata-source", TestContext.Current.CancellationToken);
        return row;
    }

    private static string CorruptSelectedMetadata(InMemoryReadModelStore store, SharedProjectionScope scope, long generation, string damage)
    {
        bool invalidJsonShape = damage.EndsWith("-json", StringComparison.Ordinal);
        bool jsonNull = damage.EndsWith("-null", StringComparison.Ordinal);
        string kind = invalidJsonShape || jsonNull ? damage[..^5] : damage;
        if (kind.StartsWith("wrapped-", StringComparison.Ordinal)) kind = kind[8..];
        string logicalKey = kind switch
        {
            "format" => MagicLinkTokenHashCapabilityIndexTreeReader.FormatKey,
            "root" => MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
            _ => MagicLinkTokenHashCapabilityIndexProjection.StateKey
        };
        string key = kind == "direct-single" ? logicalKey : PhysicalKey(scope, generation, logicalKey);
        object value = jsonNull ? JsonDocument.Parse("null") : invalidJsonShape ? new { StorageVersion = "invalid-json-shape" } : kind switch
        {
            "format" => new MagicLinkTokenHashCapabilityIndexTreeFormat(6),
            "root" => new MagicLinkTokenHashCapabilityIndexTreeRoot(6, 0, null, null),
            _ => MagicLinkTokenHashCapabilityIndexProjection.FromContributions([])
        };
        store.SeedRaw(scope.StoreName, key, value);
        return key;
    }

    [Fact]
    public async Task UnverifiedV6SourceFailsBeforeReservingTheGlobalEpoch()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        _ = await coordinator.RegisterWriterAsync(scope,
            MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        var row = Row("tenant-a", "a1", "hash-a");
        var malformed = MagicLinkTokenHashCapabilityIndexProjection.FromContributions(
            [new KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>(
                row.TokenHash, row.Candidate)]) with { LegacyContributionsUnverified = true };
        _ = await coordinator.BeginAsync(scope, "seed-unverified", "source",
            new Dictionary<string, long>(), TestContext.Current.CancellationToken);
        _ = await coordinator.StageStreamingAsync(scope, "seed-unverified", AsAsync(
            [ReadModelBatchOperation.Write(MagicLinkTokenHashCapabilityIndexProjection.ShardKey(
                row.TokenHash), malformed, ReadModelBatchConcurrency.LastWrite)]),
            TestContext.Current.CancellationToken);
        await coordinator.CommitAsync(scope, "seed-unverified", TestContext.Current.CancellationToken);
        SharedProjectionEpochStatus before = await coordinator.GetStatusAsync(scope,
            TestContext.Current.CancellationToken);
        var migration = new MagicLinkTokenHashCapabilityIndexV6Migration(coordinator, store,
            new PermittedCertifiedMigration());
        await Should.ThrowAsync<InvalidOperationException>(() => migration.MigrateAsync(
            "explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
        SharedProjectionEpochStatus after = await coordinator.GetStatusAsync(scope,
            TestContext.Current.CancellationToken);
        after.IsBuilding.ShouldBeFalse();
        after.ActiveGeneration.ShouldBe(before.ActiveGeneration);
        after.Epoch.ShouldBe(before.Epoch);
    }

    [Fact]
    public async Task FinalRootPageCanBeResealedAfterAnInterruptedSeal()
    {
        var inner = new InMemoryReadModelStore();
        var interruptedStore = new InterruptSealStore(inner);
        var coordinator = new SharedProjectionEpochCoordinator(interruptedStore, inner);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        _ = await coordinator.RegisterWriterAsync(scope,
            MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        MagicLinkTokenHashCapabilityIndexDirectoryEntry row = Row("tenant-a", "owner-a", "hash-a");
        _ = await coordinator.BeginAsync(scope, "seed-for-seal", "source",
            new Dictionary<string, long>(), TestContext.Current.CancellationToken);
        _ = await coordinator.StageStreamingAsync(scope, "seed-for-seal", AsAsync(
            [ReadModelBatchOperation.Write(MagicLinkTokenHashCapabilityIndexProjection.ShardKey(
                row.TokenHash), MagicLinkTokenHashCapabilityIndexProjection.FromContributions(
                [new KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>(
                    row.TokenHash, row.Candidate)]), ReadModelBatchConcurrency.LastWrite)]),
            TestContext.Current.CancellationToken);
        await coordinator.CommitAsync(scope, "seed-for-seal", TestContext.Current.CancellationToken);
        var migration = new MagicLinkTokenHashCapabilityIndexV6Migration(coordinator,
            interruptedStore, new PermittedCertifiedMigration());
        interruptedStore.InterruptNextSeal = true;
        await Should.ThrowAsync<IOException>(() => migration.MigrateAsync("explicit-v7",
            "certified-selected-v6", TestContext.Current.CancellationToken));
        (await coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
            MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
            TestContext.Current.CancellationToken)).Value.ShouldBeNull();
        await migration.MigrateAsync("explicit-v7", "certified-selected-v6",
            TestContext.Current.CancellationToken);
        (await coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
            MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
            TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task ChangedSelectedV6SourceAfterCheckpointAbortsWithoutPromoting()
    {
        var store = new InMemoryReadModelStore();
        var interruptedBatches = new InterruptCheckpointBatch(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, interruptedBatches);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        _ = await coordinator.RegisterWriterAsync(scope,
            MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        MagicLinkTokenHashCapabilityIndexDirectoryEntry first = Row("tenant-a", "owner-a", "hash-a");
        _ = await coordinator.BeginAsync(scope, "seed-for-race", "source",
            new Dictionary<string, long>(), TestContext.Current.CancellationToken);
        _ = await coordinator.StageStreamingAsync(scope, "seed-for-race", AsAsync(
            [ReadModelBatchOperation.Write(MagicLinkTokenHashCapabilityIndexProjection.ShardKey(
                first.TokenHash), MagicLinkTokenHashCapabilityIndexProjection.FromContributions(
                [new KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>(
                    first.TokenHash, first.Candidate)]), ReadModelBatchConcurrency.LastWrite)]),
            TestContext.Current.CancellationToken);
        await coordinator.CommitAsync(scope, "seed-for-race", TestContext.Current.CancellationToken);
        long sourceGeneration = (await coordinator.GetStatusAsync(scope,
            TestContext.Current.CancellationToken)).ActiveGeneration;
        var migration = new MagicLinkTokenHashCapabilityIndexV6Migration(coordinator, store,
            new PermittedCertifiedMigration());
        interruptedBatches.InterruptBeforeFirstTenantCheckpoint = true;
        await Should.ThrowAsync<IOException>(() => migration.MigrateAsync("explicit-v7",
            "certified-selected-v6", TestContext.Current.CancellationToken));

        string sourceKey = PhysicalKey(scope, sourceGeneration,
            MagicLinkTokenHashCapabilityIndexProjection.ShardKey(first.TokenHash));
        ReadModelEntry<MagicLinkTokenHashCapabilityIndexReadModel> selected = await store
            .GetAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope.StoreName, sourceKey,
                TestContext.Current.CancellationToken);
        MagicLinkTokenHashCapabilityIndexDirectoryEntry changed = Row("tenant-b", "owner-b",
            first.TokenHash);
        var changedRoot = MagicLinkTokenHashCapabilityIndexProjection.FromContributions(
        [
            new KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>(first.TokenHash,
                first.Candidate),
            new KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>(changed.TokenHash,
                changed.Candidate)
        ]);
        (await store.TrySaveAsync(scope.StoreName, sourceKey, changedRoot,
            selected.ETag.ShouldNotBeNull(), TestContext.Current.CancellationToken)).ShouldBeTrue();
        await Should.ThrowAsync<InvalidOperationException>(() => migration.MigrateAsync("explicit-v7",
            "certified-selected-v6", TestContext.Current.CancellationToken));
        SharedProjectionEpochStatus after = await coordinator.GetStatusAsync(scope,
            TestContext.Current.CancellationToken);
        after.IsBuilding.ShouldBeFalse();
        after.ActiveGeneration.ShouldBe(sourceGeneration);
        (await coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
            MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
            TestContext.Current.CancellationToken)).Value.ShouldBeNull();
    }

    [Fact]
    public async Task ExplicitMigrationPreservesAmbiguityAndEveryTenantAcrossRepeatedSourceScans()
    {
        var store = new InMemoryReadModelStore();
        var interruptedBatches = new InterruptCheckpointBatch(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, interruptedBatches);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        _ = await coordinator.RegisterWriterAsync(scope,
            MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        MagicLinkTokenHashCapabilityIndexDirectoryEntry[] rows = [
            Row("tenant-a", "a1", "shared"), Row("tenant-b", "b1", "shared"),
            Row("tenant-b", "b2", "b-only"), Row("tenant-c", "c1", "c-only")
        ];
        _ = await coordinator.BeginAsync(scope, "selected-v6", "certified-v6",
            new Dictionary<string, long>(), TestContext.Current.CancellationToken);
        ReadModelBatchOperation[] roots = [.. rows
            .GroupBy(row => MagicLinkTokenHashCapabilityIndexProjection.ShardOrdinal(row.TokenHash))
            .Select(group => ReadModelBatchOperation.Write(
                MagicLinkTokenHashCapabilityIndexProjection.ShardKey(group.Key),
                MagicLinkTokenHashCapabilityIndexProjection.FromContributions(group.Select(row =>
                    new KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>(
                        row.TokenHash, row.Candidate))), ReadModelBatchConcurrency.LastWrite)),
            .. rows.Select(row => ReadModelBatchOperation.Write(
                MagicLinkTokenHashCapabilityIndexProjection.OwnerAdmissionKey(
                    row.Candidate.Tenant.TenantId, row.Candidate.CapabilityId.Value),
                new MagicLinkTokenHashCapabilityIndexOwnerAdmission(row.TokenHash, row.Candidate),
                ReadModelBatchConcurrency.LastWrite))];
        _ = await coordinator.StageStreamingAsync(scope, "selected-v6", AsAsync(roots),
            TestContext.Current.CancellationToken);
        await coordinator.CommitAsync(scope, "selected-v6", TestContext.Current.CancellationToken);
        long oldGeneration = (await coordinator.GetStatusAsync(scope,
            TestContext.Current.CancellationToken)).ActiveGeneration;

        var migration = new MagicLinkTokenHashCapabilityIndexV6Migration(coordinator, store,
            new PermittedCertifiedMigration());
        interruptedBatches.InterruptBeforeFirstTenantCheckpoint = true;
        await Should.ThrowAsync<IOException>(() => migration.MigrateAsync("explicit-v7",
            "certified-selected-v6", TestContext.Current.CancellationToken));
        (await coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
            MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
            TestContext.Current.CancellationToken)).Value.ShouldBeNull();
        await migration.MigrateAsync("explicit-v7", "certified-selected-v6",
            TestContext.Current.CancellationToken);
        await migration.MigrateAsync("explicit-v7", "certified-selected-v6",
            TestContext.Current.CancellationToken);

        SharedProjectionReadResult<MagicLinkTokenHashCapabilityIndexTreeRoot> selected = await coordinator
            .ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
                MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
                TestContext.Current.CancellationToken);
        MagicLinkTokenHashCapabilityIndexTreeRoot root = selected.Value.ShouldNotBeNull();
        var reader = new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, scope);
        (await reader.FindCandidatesAsync(root.TokenRoot, "shared",
            TestContext.Current.CancellationToken)).Count.ShouldBe(2);
        (await reader.FindCandidatesAsync(root.TokenRoot, "b-only",
            TestContext.Current.CancellationToken)).Count.ShouldBe(1);
        (await reader.FindCandidatesAsync(root.TokenRoot, "c-only",
            TestContext.Current.CancellationToken)).Count.ShouldBe(1);
        (await reader.ReadRosterAsync(root.TenantRoot, "tenant-a",
            TestContext.Current.CancellationToken)).ShouldNotBeNull().Count.ShouldBe(1);
        (await reader.ReadRosterAsync(root.TenantRoot, "tenant-b",
            TestContext.Current.CancellationToken)).ShouldNotBeNull().Count.ShouldBe(2);
        string oldShard = MagicLinkTokenHashCapabilityIndexProjection.ShardKey("shared");
        string oldAdmission = MagicLinkTokenHashCapabilityIndexProjection.OwnerAdmissionKey(
            "tenant-a", "a1");
        for (int attempt = 0; attempt < 100; attempt++)
        {
            ReadModelEntry<MagicLinkTokenHashCapabilityIndexReadModel> old = await store
                .GetAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope.StoreName,
                    PhysicalKey(scope, oldGeneration, oldShard), TestContext.Current.CancellationToken);
            if (old.Value is null) break;
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
        (await store.GetAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope.StoreName,
            PhysicalKey(scope, oldGeneration, oldShard), TestContext.Current.CancellationToken))
            .Value.ShouldBeNull();
        (await store.GetAsync<MagicLinkTokenHashCapabilityIndexOwnerAdmission>(scope.StoreName,
            PhysicalKey(scope, oldGeneration, oldAdmission), TestContext.Current.CancellationToken))
            .Value.ShouldBeNull();
        SharedProjectionEpochStatus migrated = await coordinator.GetStatusAsync(scope,
            TestContext.Current.CancellationToken);
        await Should.ThrowAsync<InvalidOperationException>(() => migration.MigrateAsync(
            "another-migration", "certified-selected-v6", TestContext.Current.CancellationToken));
        (await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken))
            .ActiveGeneration.ShouldBe(migrated.ActiveGeneration);
    }

    [Fact]
    public async Task MaximumWidthV6TenantMigratesAcrossBoundedStreamingPages()
    {
        var store = new InMemoryReadModelStore();
        var observed = new MagicLinkV1FaultStore(store);
        var coordinator = new SharedProjectionEpochCoordinator(observed, observed);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        _ = await coordinator.RegisterWriterAsync(scope,
            MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        string tenant = "t" + new string('\u0001', 255);
        MagicLinkTokenHashCapabilityIndexDirectoryEntry[] rows = Enumerable.Range(0, 128)
            .Select(index => Row(tenant, "o" + new string('\u0002', 114) + index.ToString("D5"),
                new string(index < 64 ? 'A' : 'B', 128))).ToArray();
        ReadModelBatchOperation[] roots = [.. rows
            .GroupBy(row => MagicLinkTokenHashCapabilityIndexProjection.ShardOrdinal(row.TokenHash))
            .Select(group => ReadModelBatchOperation.Write(
                MagicLinkTokenHashCapabilityIndexProjection.ShardKey(group.Key),
                MagicLinkTokenHashCapabilityIndexProjection.FromContributions(group.Select(row =>
                    new KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>(row.TokenHash,
                        row.Candidate))), ReadModelBatchConcurrency.LastWrite))];
        _ = await coordinator.BeginAsync(scope, "wide-v6", "certified-source",
            new Dictionary<string, long>(), TestContext.Current.CancellationToken);
        _ = await coordinator.StageStreamingAsync(scope, "wide-v6", AsAsync(roots),
            TestContext.Current.CancellationToken);
        await coordinator.CommitAsync(scope, "wide-v6", TestContext.Current.CancellationToken);

        var migration = new MagicLinkTokenHashCapabilityIndexV6Migration(coordinator, observed,
            new PermittedCertifiedMigration());
        await migration.MigrateAsync("wide-v7", "certified-selected-v6",
            TestContext.Current.CancellationToken);
        MagicLinkTokenHashCapabilityIndexTreeRoot root = (await coordinator
            .ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
                MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
                TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        var reader = new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, scope);
        (await reader.ReadRosterAsync(root.TenantRoot, tenant,
            TestContext.Current.CancellationToken)).ShouldNotBeNull().Count.ShouldBe(rows.Length);
        observed.MaximumPhysicalBytes.ShouldBeLessThan(256 * 1024);
        observed.MaximumRosterBytes.ShouldBeGreaterThan(64 * 1024);
        observed.ByteSplitPages.ShouldBeGreaterThan(0);
    }

    [Theory]
    [InlineData("unchanged")]
    [InlineData("long-tenant")]
    [InlineData("source")]
    [InlineData("checkpoint")]
    [InlineData("checkpoint-retirement")]
    [InlineData("checkpoint-cursor")]
    [InlineData("checkpoint-initial-root")]
    [InlineData("checkpoint-initial-retirement")]
    [InlineData("checkpoint-initial-cursor")]
    [InlineData("root")]
    [InlineData("node")]
    [InlineData("owner")]
    [InlineData("roster")]
    [InlineData("format")]
    [InlineData("membership")]
    [InlineData("checkpoint-tenant")]
    public async Task SealedResumeValidatesSourceCheckpointAndEveryStagedMembershipBeforePromotion(string damage)
    {
        var store = new InMemoryReadModelStore();
        var interrupted = new InterruptSealStore(store);
        var batches = new InterruptCheckpointBatch(store);
        var coordinator = new SharedProjectionEpochCoordinator(interrupted, batches);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        _ = await coordinator.RegisterWriterAsync(scope, MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        string tenant = damage == "long-tenant" ? new string('t', 1000) : "tenant-a";
        MagicLinkTokenHashCapabilityIndexDirectoryEntry row = Row(tenant, "owner-a", "hash-a");
        _ = await coordinator.BeginAsync(scope, "sealed-source", "source", new Dictionary<string, long>(), TestContext.Current.CancellationToken);
        _ = await coordinator.StageStreamingAsync(scope, "sealed-source", AsAsync([
            ReadModelBatchOperation.Write(MagicLinkTokenHashCapabilityIndexProjection.ShardKey(row.TokenHash),
                MagicLinkTokenHashCapabilityIndexProjection.FromContributions([
                    new KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>(row.TokenHash, row.Candidate)]),
                ReadModelBatchConcurrency.LastWrite)]), TestContext.Current.CancellationToken);
        await coordinator.CommitAsync(scope, "sealed-source", TestContext.Current.CancellationToken);
        long selected = (await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken)).ActiveGeneration;
        var migration = new MagicLinkTokenHashCapabilityIndexV6Migration(coordinator, interrupted, new PermittedCertifiedMigration());
        interrupted.InterruptAfterNextSeal = true;
        Exception? interruption = await Record.ExceptionAsync(() => migration.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
        interruption.ShouldBeOfType<IOException>(interruption?.ToString());
        SharedProjectionEpochStatus sealedStatus = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        sealedStatus.IsBuilding.ShouldBeTrue(); sealedStatus.StageFingerprint.ShouldNotBeNull();
        int appends = batches.StageAppendCalls;
        long epoch = sealedStatus.Epoch;
        string rootKey = PhysicalKey(scope, epoch, MagicLinkTokenHashCapabilityIndexTreeReader.RootKey);
        MagicLinkTokenHashCapabilityIndexTreeRoot root = store.Snapshot<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope.StoreName, rootKey).ShouldNotBeNull();
        string checkpointKey = PhysicalKey(scope, epoch, "timesheets:magic-links:token-hash-capability-index:v7:migration:"
            + epoch.ToString("D16") + ":" + 1L.ToString("D16"));
        MagicLinkTokenHashCapabilityIndexMigrationCheckpoint checkpoint = store.Snapshot<MagicLinkTokenHashCapabilityIndexMigrationCheckpoint>(scope.StoreName, checkpointKey).ShouldNotBeNull();
        switch (damage)
        {
            case "source":
                var extra = Row("tenant-b", "owner-b", row.TokenHash);
                store.SeedRaw(scope.StoreName, PhysicalKey(scope, selected, MagicLinkTokenHashCapabilityIndexProjection.ShardKey(row.TokenHash)),
                    MagicLinkTokenHashCapabilityIndexProjection.FromContributions([
                        new KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>(row.TokenHash, row.Candidate),
                        new KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>(extra.TokenHash, extra.Candidate)]));
                break;
            case "checkpoint": store.SeedRaw(scope.StoreName, checkpointKey, checkpoint with { SourceDigest = new string('A', 64) }); break;
            case "checkpoint-tenant": store.SeedRaw(scope.StoreName, checkpointKey, checkpoint with { LastTenantId = "tenant-z" }); break;
            case "checkpoint-retirement": store.SeedRaw(scope.StoreName, checkpointKey, checkpoint with { NextRetirementOrdinal = checkpoint.NextRetirementOrdinal + 1 }); break;
            case "checkpoint-cursor": store.SeedRaw(scope.StoreName, checkpointKey, checkpoint with { NextPageOrdinal = checkpoint.NextPageOrdinal + 1 }); break;
            case "checkpoint-initial-root":
            case "checkpoint-initial-retirement":
            case "checkpoint-initial-cursor":
                string initialKey = PhysicalKey(scope, epoch, "timesheets:magic-links:token-hash-capability-index:v7:migration:"
                    + epoch.ToString("D16") + ":" + 0L.ToString("D16"));
                MagicLinkTokenHashCapabilityIndexMigrationCheckpoint initial = store.Snapshot<MagicLinkTokenHashCapabilityIndexMigrationCheckpoint>(scope.StoreName, initialKey).ShouldNotBeNull();
                store.SeedRaw(scope.StoreName, initialKey, damage switch
                {
                    "checkpoint-initial-root" => initial with { Root = initial.Root with { NextNodeId = 1 } },
                    "checkpoint-initial-retirement" => initial with { NextRetirementOrdinal = 1 },
                    _ => initial with { NextPageOrdinal = 2 }
                });
                break;
            case "root": store.SeedRaw(scope.StoreName, rootKey, root with { NextNodeId = 0 }); break;
            case "format": store.SeedRaw(scope.StoreName, PhysicalKey(scope, epoch, MagicLinkTokenHashCapabilityIndexTreeReader.FormatKey), new MagicLinkTokenHashCapabilityIndexTreeFormat(6)); break;
            case "node":
            case "owner":
                MagicLinkTokenHashCapabilityIndexNodeReference nodeRef = (damage == "node" ? root.TokenRoot : root.OwnerRoot).ShouldNotBeNull();
                string nodeKey = PhysicalKey(scope, nodeRef.Generation, MagicLinkTokenHashCapabilityIndexTreeReader.NodeKey(nodeRef.NodeId));
                MagicLinkTokenHashCapabilityIndexTreeNode node = store.Snapshot<MagicLinkTokenHashCapabilityIndexTreeNode>(scope.StoreName, nodeKey).ShouldNotBeNull();
                store.SeedRaw(scope.StoreName, nodeKey, node with { Prefix = "damaged" });
                break;
            case "roster":
                var reader = new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, scope);
                MagicLinkTokenHashCapabilityIndexTenantRoster roster = (await reader.FindRosterAsync(root.TenantRoot, tenant, TestContext.Current.CancellationToken)).ShouldNotBeNull();
                var page = roster.Pages[0].Page;
                store.SeedRaw(scope.StoreName, PhysicalKey(scope, page.Generation, MagicLinkTokenHashCapabilityIndexTreeReader.RosterPageKey(page.NodeId)),
                    new MagicLinkTokenHashCapabilityIndexTenantRosterPage([Row(tenant, "wrong-owner", row.TokenHash)]));
                break;
            case "membership":
                root = root with { OwnerRoot = null };
                store.SeedRaw(scope.StoreName, rootKey, root);
                store.SeedRaw(scope.StoreName, checkpointKey, checkpoint with { Root = root });
                break;
        }
        if (damage is "unchanged" or "long-tenant")
        {
            await migration.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken);
            SharedProjectionEpochStatus after = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
            after.ActiveGeneration.ShouldBe(epoch); after.IsBuilding.ShouldBeFalse();
            var reader = new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, scope);
            (await reader.FindOwnerAsync(root.OwnerRoot, tenant, "owner-a", TestContext.Current.CancellationToken)).ShouldBe(row);
            (await reader.ReadRosterAsync(root.TenantRoot, tenant, TestContext.Current.CancellationToken)).ShouldNotBeNull().ShouldBe([row]);
        }
        else
        {
            Exception? failure = await Record.ExceptionAsync(() => migration.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
            failure.ShouldNotBeNull();
            (failure is InvalidOperationException or SharedProjectionReadUnavailableException).ShouldBeTrue();
            SharedProjectionEpochStatus refused = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
            refused.ActiveGeneration.ShouldBe(selected);
            refused.IsBuilding.ShouldBeFalse();
            (await coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope, MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
                TestContext.Current.CancellationToken)).Value.ShouldBeNull();
        }
        batches.StageAppendCalls.ShouldBe(appends);
    }

    [Fact]
    public async Task SealedRetryPreservesEveryTenantAndSharedHashWithoutAppendingStages()
    {
        var store = new InMemoryReadModelStore();
        var interrupted = new InterruptSealStore(store);
        var batches = new InterruptCheckpointBatch(store);
        var coordinator = new SharedProjectionEpochCoordinator(interrupted, batches);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        _ = await coordinator.RegisterWriterAsync(scope, MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        MagicLinkTokenHashCapabilityIndexDirectoryEntry[] rows =
        [
            Row("tenant-a", "owner-a1", "shared"), Row("tenant-a", "owner-a2", "a-only"),
            Row("tenant-b", "owner-b", "shared"), Row("tenant-c", "owner-c", "shared")
        ];
        ReadModelBatchOperation[] shards = [.. rows
            .GroupBy(row => MagicLinkTokenHashCapabilityIndexProjection.ShardOrdinal(row.TokenHash))
            .Select(group => ReadModelBatchOperation.Write(MagicLinkTokenHashCapabilityIndexProjection.ShardKey(group.Key),
                MagicLinkTokenHashCapabilityIndexProjection.FromContributions(group.Select(row =>
                    new KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>(row.TokenHash, row.Candidate))),
                ReadModelBatchConcurrency.LastWrite))];
        _ = await coordinator.BeginAsync(scope, "sealed-multi-source", "source", new Dictionary<string, long>(), TestContext.Current.CancellationToken);
        _ = await coordinator.StageStreamingAsync(scope, "sealed-multi-source", AsAsync(shards), TestContext.Current.CancellationToken);
        await coordinator.CommitAsync(scope, "sealed-multi-source", TestContext.Current.CancellationToken);
        var migration = new MagicLinkTokenHashCapabilityIndexV6Migration(coordinator, interrupted, new PermittedCertifiedMigration());
        interrupted.InterruptAfterNextSeal = true;
        await Should.ThrowAsync<IOException>(() => migration.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
        SharedProjectionEpochStatus sealedStatus = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        sealedStatus.IsBuilding.ShouldBeTrue();
        sealedStatus.StageFingerprint.ShouldNotBeNull();
        int appends = batches.StageAppendCalls;

        await migration.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken);

        SharedProjectionEpochStatus promoted = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        promoted.ActiveGeneration.ShouldBe(sealedStatus.Epoch);
        promoted.IsBuilding.ShouldBeFalse();
        batches.StageAppendCalls.ShouldBe(appends);
        MagicLinkTokenHashCapabilityIndexTreeRoot root = (await coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
            MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        var reader = new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, scope);
        foreach (MagicLinkTokenHashCapabilityIndexDirectoryEntry row in rows)
        {
            (await reader.ContainsExactRowAsync(root.TokenRoot, row, TestContext.Current.CancellationToken)).ShouldBeTrue();
            (await reader.FindOwnerAsync(root.OwnerRoot, row.Candidate.Tenant.TenantId, row.Candidate.CapabilityId.Value,
                TestContext.Current.CancellationToken)).ShouldBe(row);
        }
        foreach (var tenant in rows.GroupBy(row => row.Candidate.Tenant.TenantId))
        {
            IReadOnlyList<MagicLinkTokenHashCapabilityIndexDirectoryEntry> roster = (await reader.ReadRosterAsync(root.TenantRoot,
                tenant.Key, TestContext.Current.CancellationToken)).ShouldNotBeNull();
            roster.Count.ShouldBe(tenant.Count());
            new HashSet<MagicLinkTokenHashCapabilityIndexDirectoryEntry>(roster).SetEquals(tenant).ShouldBeTrue();
        }
        (await reader.FindCandidatesAsync(root.TokenRoot, "shared", TestContext.Current.CancellationToken)).Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SealedResumeTransportFailureOrCancellationRemainsRetryable(bool cancellation)
    {
        var store = new InMemoryReadModelStore();
        var interrupted = new InterruptSealStore(store);
        var coordinator = new SharedProjectionEpochCoordinator(interrupted, store);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        _ = await coordinator.RegisterWriterAsync(scope, MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId, TestContext.Current.CancellationToken);
        var row = Row("tenant-a", "owner-a", "hash-a");
        _ = await coordinator.BeginAsync(scope, "retry-source", "source", new Dictionary<string, long>(), TestContext.Current.CancellationToken);
        _ = await coordinator.StageStreamingAsync(scope, "retry-source", AsAsync([
            ReadModelBatchOperation.Write(MagicLinkTokenHashCapabilityIndexProjection.ShardKey(row.TokenHash),
                MagicLinkTokenHashCapabilityIndexProjection.FromContributions([new KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>(row.TokenHash, row.Candidate)]),
                ReadModelBatchConcurrency.LastWrite)]), TestContext.Current.CancellationToken);
        await coordinator.CommitAsync(scope, "retry-source", TestContext.Current.CancellationToken);
        var migration = new MagicLinkTokenHashCapabilityIndexV6Migration(coordinator, interrupted, new PermittedCertifiedMigration());
        interrupted.InterruptAfterNextSeal = true;
        await Should.ThrowAsync<IOException>(() => migration.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
        SharedProjectionEpochStatus before = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        interrupted.FaultReadKey = PhysicalKey(scope, before.Epoch, MagicLinkTokenHashCapabilityIndexTreeReader.RootKey);
        if (cancellation)
        {
            using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            interrupted.WaitForReadCancellation = true;
            Task retry = migration.MigrateAsync("explicit-v7", "certified-selected-v6", caller.Token);
            await interrupted.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            retry.IsCompleted.ShouldBeFalse();
            caller.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(() => retry.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
            caller.IsCancellationRequested.ShouldBeTrue();
        }
        else
        {
            await Should.ThrowAsync<IOException>(() => migration.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken));
        }
        SharedProjectionEpochStatus failed = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        failed.IsBuilding.ShouldBeTrue(); failed.ActiveGeneration.ShouldBe(before.ActiveGeneration);
        await migration.MigrateAsync("explicit-v7", "certified-selected-v6", TestContext.Current.CancellationToken);
        (await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken)).ActiveGeneration.ShouldBe(before.Epoch);
    }

    private static MagicLinkTokenHashCapabilityIndexDirectoryEntry Row(
        string tenant, string owner, string hash)
        => new(hash, new MagicLinkTokenHashCapabilityIndexEntry(
            new TenantReference(tenant), new MagicLinkCapabilityId(owner)));

    private static string PhysicalKey(SharedProjectionScope scope, long generation, string logicalKey)
        => "shared-generation:" + scope.ComputeHash() + ":" + generation + ":"
            + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(logicalKey)));

    private static async IAsyncEnumerable<ReadModelBatchOperation> AsAsync(
        IEnumerable<ReadModelBatchOperation> operations)
    {
        foreach (ReadModelBatchOperation operation in operations)
        {
            yield return operation;
            await Task.Yield();
        }
    }

    private sealed class PermittedCertifiedMigration : IMagicLinkIndexMigrationAuthority
    {
        public bool AllowAnyFingerprint { get; init; }

        public bool Denied { get; set; }

        public Task<bool> AuthorizeCertifiedMigrationAsync(string operationId,
            string certifiedInventoryFingerprint, CancellationToken cancellationToken)
            => Task.FromResult(!Denied && (AllowAnyFingerprint || certifiedInventoryFingerprint == "certified-selected-v6"));
    }

    private sealed class InterruptCheckpointBatch(InMemoryReadModelStore inner) : IReadModelBatchStore
    {
        private int _physicalStages;

        public bool InterruptBeforeFirstTenantCheckpoint { get; set; }

        public int StageAppendCalls { get; private set; }

        public bool InterruptDuringStageDeletion { get; set; }

        public HashSet<string> WrittenKeys { get; } = new(StringComparer.Ordinal);

        public async Task<ReadModelBatchResult> ExecuteAsync(ReadModelBatch batch,
            CancellationToken cancellationToken = default)
        {
            if (batch.Scope.BatchId.StartsWith("stage:", StringComparison.Ordinal)
                && batch.Operations.Any(operation => operation.Kind == ReadModelBatchOperationKind.Write
                    && operation.Key.StartsWith("shared-generation:", StringComparison.Ordinal))) StageAppendCalls++;
            if (InterruptBeforeFirstTenantCheckpoint && batch.Operations.Any(operation =>
                    operation.Key.StartsWith("shared-generation:", StringComparison.Ordinal))
                && ++_physicalStages == 3)
            {
                InterruptBeforeFirstTenantCheckpoint = false;
                throw new IOException("Interrupted after private node flush and before its checkpoint.");
            }
            foreach (ReadModelBatchOperation operation in batch.Operations.Where(static operation => operation.Kind == ReadModelBatchOperationKind.Write))
                WrittenKeys.Add(operation.Key);
            ReadModelBatchResult result = await inner.ExecuteAsync(batch, cancellationToken);
            if (result.IsSuccess && InterruptDuringStageDeletion && batch.Operations.Any(operation =>
                operation.Kind == ReadModelBatchOperationKind.Delete && operation.Key.StartsWith("shared-generation:", StringComparison.Ordinal)))
            {
                InterruptDuringStageDeletion = false;
                throw new IOException("Interrupted after deleting private stage values.");
            }
            return result;
        }
    }

    private sealed class InterruptSealStore(InMemoryReadModelStore inner) : IReadModelStore, IReadModelConditionalEraser
    {
        public bool InterruptAfterAborting { get; set; }

        public Exception? WitnessWriteFailure { get; set; }

        public bool InterruptAfterWitnessWrite { get; set; }

        public Func<Task>? BeforeWitnessReturnAsync { get; set; }

        public bool InterruptNextSeal { get; set; }

        public bool InterruptAfterNextSeal { get; set; }

        public string? FaultReadKey { get; set; }

        public bool WaitForReadCancellation { get; set; }

        public bool WrapInvalidJson { get; set; }

        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ReadModelEntry<TValue>> GetAsync<TValue>(string storeName, string key,
            CancellationToken cancellationToken = default) where TValue : class
        {
            if (typeof(TValue) == typeof(MagicLinkV6MigrationAbortWitness) && BeforeWitnessReturnAsync is { } callback)
            {
                BeforeWitnessReturnAsync = null;
                return ReadBeforeCallbackAsync<TValue>(storeName, key, callback, cancellationToken);
            }
            if (key == FaultReadKey)
            {
                FaultReadKey = null;
                if (WaitForReadCancellation)
                {
                    ReadStarted.TrySetResult();
                    return AwaitReadCancellationAsync<TValue>(cancellationToken);
                }
                return Task.FromException<ReadModelEntry<TValue>>(new IOException("Transient sealed-stage read failure."));
            }
            return WrapInvalidJson ? ReadWrappedAsync<TValue>(storeName, key, cancellationToken)
                : inner.GetAsync<TValue>(storeName, key, cancellationToken);
        }

        private async Task<ReadModelEntry<TValue>> ReadBeforeCallbackAsync<TValue>(string storeName,
            string key, Func<Task> callback, CancellationToken cancellationToken) where TValue : class
        {
            ReadModelEntry<TValue> entry = await inner.GetAsync<TValue>(storeName, key, cancellationToken);
            await callback();
            return entry;
        }

        private async Task<ReadModelEntry<TValue>> ReadWrappedAsync<TValue>(string storeName, string key, CancellationToken token)
            where TValue : class
        {
            try { return await inner.GetAsync<TValue>(storeName, key, token); }
            catch (System.Text.Json.JsonException exception) { throw new SharedProjectionReadUnavailableException(exception); }
        }

        private static async Task<ReadModelEntry<TValue>> AwaitReadCancellationAsync<TValue>(CancellationToken cancellationToken)
            where TValue : class
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("A stalled read must end through caller cancellation.");
        }

        public Task<bool> TryEraseAsync(string storeName, string key, string etag, CancellationToken cancellationToken = default)
            => inner.TryEraseAsync(storeName, key, etag, cancellationToken);

        public Task<(bool Present, string Etag)> TryReadEtagAsync(string storeName, string key, CancellationToken cancellationToken = default)
            => inner.TryReadEtagAsync(storeName, key, cancellationToken);

        public Task SaveAsync<TValue>(string storeName, string key, TValue value,
            CancellationToken cancellationToken = default) where TValue : class
            => inner.SaveAsync(storeName, key, value, cancellationToken);

        public async Task<bool> TrySaveAsync<TValue>(string storeName, string key, TValue value,
            string etag, CancellationToken cancellationToken = default) where TValue : class
        {
            if (value is MagicLinkV6MigrationAbortWitness && WitnessWriteFailure is { } witnessFailure)
            {
                WitnessWriteFailure = null;
                throw witnessFailure;
            }
            if (InterruptNextSeal && value.GetType().Name == "SharedProjectionEpochState"
                && value.GetType().GetProperty("OperationId")?.GetValue(value) is "explicit-v7"
                && value.GetType().GetProperty("StageFingerprint")?.GetValue(value) is string)
            {
                InterruptNextSeal = false;
                throw new IOException("Interrupted after the final root page and before sealing.");
            }
            bool saved = await inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
            if (saved && InterruptAfterWitnessWrite && value is MagicLinkV6MigrationAbortWitness)
            {
                InterruptAfterWitnessWrite = false;
                throw new IOException("Interrupted after persisting the immutable witness.");
            }
            if (saved && InterruptAfterAborting && value.GetType().Name == "SharedProjectionEpochState"
                && value.GetType().GetProperty("Phase")?.GetValue(value)?.ToString() == "Aborting")
            {
                InterruptAfterAborting = false;
                throw new IOException("Interrupted after durable Aborting transition.");
            }
            if (saved && InterruptAfterNextSeal && value.GetType().Name == "SharedProjectionEpochState"
                && value.GetType().GetProperty("OperationId")?.GetValue(value) is "explicit-v7"
                && value.GetType().GetProperty("StageFingerprint")?.GetValue(value) is string)
            {
                InterruptAfterNextSeal = false;
                throw new IOException("Interrupted after the sealed stage persisted and before promotion.");
            }
            return saved;
        }
    }
}
