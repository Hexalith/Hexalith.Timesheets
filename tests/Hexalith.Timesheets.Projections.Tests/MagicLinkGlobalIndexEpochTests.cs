using System.Runtime.CompilerServices;
using System.Reflection;
using System.Security.Cryptography;
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

using Shouldly;

namespace Hexalith.Timesheets.Projections.Tests;

public sealed class MagicLinkGlobalIndexEpochTests
{
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task FreshOrdinaryDeliveryBootstrapsV7AndSelectsItsCandidate()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store,
            epochCoordinator: coordinator);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(scope,
            handler.OrdinaryWriterId, TestContext.Current.CancellationToken);
        const string hash = "fresh-ordinary-hash";
        var issued = new MagicLinkConfirmationCapabilityIssued(
            new MagicLinkCapabilityId("fresh-owner"), new TenantReference("tenant-1"),
            new PartyReference("party-1"), TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            new ActivityTypeId("activity-1"), new TimeEntryId("entry-1"),
            MagicLinkTargetKind.ProposedTimeEntry, MagicLinkAllowedAction.Confirm,
            new MagicLinkTokenHash(hash), DateTimeOffset.UtcNow.AddDays(1),
            new PartyReference("issuer-1"), DateTimeOffset.UtcNow,
            new MagicLinkAuditMetadata("timesheets", "fresh-ordinary"), true);
        var projectionEvent = new ProjectionEventDto(typeof(MagicLinkConfirmationCapabilityIssued).FullName!,
            JsonSerializer.SerializeToUtf8Bytes(issued, s_json), "json", 1, DateTimeOffset.UtcNow,
            "correlation-fresh", "message-fresh", "issuer-1", 1);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new ProjectionRequest(
            "tenant-1", "timesheets", "fresh-owner", [projectionEvent]), s_json);
        (await coordinator.JournalAsync(scope, lease,
            new SharedProjectionDelivery("tenant-1:timesheets:fresh-owner", 1, payload, [],
                OwnerSequenceNumber: 1), TestContext.Current.CancellationToken))
            .ShouldBe(SharedProjectionJournalResult.Journaled);
        (await coordinator.CatchUpAsync(scope,
            (delivery, generation, token) => handler.FoldCatchUpAsync(delivery, generation,
                coordinator, token), TestContext.Current.CancellationToken)).ShouldBe(1);
        SharedProjectionReadResult<MagicLinkTokenHashCapabilityIndexTreeRoot> selected = await coordinator
            .ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
                MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
                TestContext.Current.CancellationToken);
        MagicLinkTokenHashCapabilityIndexTreeRoot root = selected.Value.ShouldNotBeNull();
        (await new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, scope)
            .FindCandidatesAsync(root.TokenRoot, hash,
                TestContext.Current.CancellationToken)).Count.ShouldBe(1);
        (await coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope,
            MagicLinkTokenHashCapabilityIndexProjection.ShardKey(hash),
            TestContext.Current.CancellationToken)).Value.ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyIndexPreventsFreshOrdinaryV7Bootstrap(bool finalShard)
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store,
            epochCoordinator: coordinator);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        long selectedGeneration = 0;
        if (finalShard)
        {
            const string operation = "seed-final-legacy-shard";
            _ = await coordinator.BeginAsync(scope, operation, "legacy-inventory",
                new Dictionary<string, long>(), TestContext.Current.CancellationToken);
            _ = await coordinator.StageAsync(scope, operation,
                [ReadModelBatchOperation.Write(MagicLinkTokenHashCapabilityIndexProjection.ShardKey(
                        MagicLinkTokenHashCapabilityIndexProjection.ShardCount - 1),
                    MagicLinkTokenHashCapabilityIndexProjection.FromContributions([]),
                    ReadModelBatchConcurrency.LastWrite)], TestContext.Current.CancellationToken);
            await coordinator.CommitAsync(scope, operation, TestContext.Current.CancellationToken);
            selectedGeneration = (await coordinator.GetStatusAsync(scope,
                TestContext.Current.CancellationToken)).ActiveGeneration;
            (await coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope,
                MagicLinkTokenHashCapabilityIndexProjection.ShardKey(
                    MagicLinkTokenHashCapabilityIndexProjection.ShardCount - 1),
                TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        }
        else
            await store.SaveAsync(scope.StoreName, MagicLinkTokenHashCapabilityIndexProjection.StateKey,
                MagicLinkTokenHashCapabilityIndexProjection.FromContributions([]),
                TestContext.Current.CancellationToken);
        var issued = new MagicLinkConfirmationCapabilityIssued(
            new MagicLinkCapabilityId("owner-1"), new TenantReference("tenant-1"),
            new PartyReference("party-1"), TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            new ActivityTypeId("activity-1"), new TimeEntryId("entry-1"),
            MagicLinkTargetKind.ProposedTimeEntry, MagicLinkAllowedAction.Confirm,
            new MagicLinkTokenHash("legacy-root-hash"), DateTimeOffset.UtcNow.AddDays(1),
            new PartyReference("issuer-1"), DateTimeOffset.UtcNow,
            new MagicLinkAuditMetadata("timesheets", "legacy-root"), true);
        var projectionEvent = new ProjectionEventDto(typeof(MagicLinkConfirmationCapabilityIssued).FullName!,
            JsonSerializer.SerializeToUtf8Bytes(issued, s_json), "json", 1, DateTimeOffset.UtcNow,
            "correlation-legacy", "message-legacy", "issuer-1", 1);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new ProjectionRequest(
            "tenant-1", "timesheets", "owner-1", [projectionEvent]), s_json);
        var delivery = new SharedProjectionDelivery(
            "tenant-1:timesheets:owner-1", 1, payload, [], OwnerSequenceNumber: 1);
        if (finalShard)
        {
            IReadOnlyList<ReadModelBatchOperation> operations = await handler.FoldCatchUpAsync(
                delivery, selectedGeneration, coordinator, TestContext.Current.CancellationToken);
            operations.ShouldContain(operation => operation.Key
                == MagicLinkTokenHashCapabilityIndexProjection.ShardKey("legacy-root-hash"));
            operations.ShouldNotContain(operation => operation.Key
                == MagicLinkTokenHashCapabilityIndexTreeReader.RootKey);
        }
        else
            await Should.ThrowAsync<SharedProjectionReadUnavailableException>(() =>
                handler.FoldCatchUpAsync(delivery, selectedGeneration, coordinator,
                    TestContext.Current.CancellationToken));
        (await coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
            MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
            TestContext.Current.CancellationToken)).Value.ShouldBeNull();
    }

    [Fact]
    public async Task OrdinaryDirectoryInsertionUsesFullOwnerOrderAcrossSameHashPages()
    {
        const string hash = "multi-page-ordinary-owner-order";
        int shard = MagicLinkTokenHashCapabilityIndexProjection.ShardOrdinal(hash);
        MagicLinkTokenHashCapabilityIndexDirectoryEntry Row(string owner) => new(hash,
            new MagicLinkTokenHashCapabilityIndexEntry(new TenantReference("tenant-1"),
                new MagicLinkCapabilityId(owner)));
        var first = new MagicLinkTokenHashCapabilityIndexDirectoryPage([Row("owner-a"), Row("owner-b")]);
        var second = new MagicLinkTokenHashCapabilityIndexDirectoryPage([Row("owner-c"), Row("owner-d")]);
        MagicLinkTokenHashCapabilityIndexReadModel root =
            MagicLinkTokenHashCapabilityIndexProjection.FromContributions([]) with
            {
                DirectoryPages = [new(0, hash, hash), new(1, hash, hash)],
                DirectoryPageDigests = new Dictionary<int, string>
                {
                    [0] = MagicLinkTokenHashCapabilityIndexProjection.DirectoryPageDigest(first),
                    [1] = MagicLinkTokenHashCapabilityIndexProjection.DirectoryPageDigest(second)
                },
                NextCandidatePageId = 2
            };
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store,
            epochCoordinator: coordinator);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        _ = await coordinator.BeginAsync(scope, "seed-owner-order", "seed-owner-order",
            new Dictionary<string, long>(), TestContext.Current.CancellationToken);
        _ = await coordinator.StageAsync(scope, "seed-owner-order",
            [
                ReadModelBatchOperation.Write(MagicLinkTokenHashCapabilityIndexProjection.ShardKey(shard),
                    root, ReadModelBatchConcurrency.LastWrite),
                ReadModelBatchOperation.Write(MagicLinkTokenHashCapabilityIndexProjection.DirectoryPageKey(shard, 0),
                    first, ReadModelBatchConcurrency.LastWrite),
                ReadModelBatchOperation.Write(MagicLinkTokenHashCapabilityIndexProjection.DirectoryPageKey(shard, 1),
                    second, ReadModelBatchConcurrency.LastWrite)
            ], TestContext.Current.CancellationToken);
        await coordinator.CommitAsync(scope, "seed-owner-order", TestContext.Current.CancellationToken);
        SharedProjectionReadResult<MagicLinkTokenHashCapabilityIndexReadModel> selected = await coordinator
            .ReadAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope,
                MagicLinkTokenHashCapabilityIndexProjection.ShardKey(shard),
                TestContext.Current.CancellationToken);
        var issued = new MagicLinkConfirmationCapabilityIssued(
            new MagicLinkCapabilityId("owner-z"), new TenantReference("tenant-1"),
            new PartyReference("party-1"), TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            new ActivityTypeId("activity-1"), new TimeEntryId("entry-1"),
            MagicLinkTargetKind.ProposedTimeEntry, MagicLinkAllowedAction.Confirm,
            new MagicLinkTokenHash(hash), DateTimeOffset.UtcNow.AddDays(1),
            new PartyReference("issuer-1"), DateTimeOffset.UtcNow,
            new MagicLinkAuditMetadata("timesheets", "issue-owner-z"), true);
        var projectionEvent = new ProjectionEventDto(typeof(MagicLinkConfirmationCapabilityIssued).FullName!,
            JsonSerializer.SerializeToUtf8Bytes(issued, s_json), "json", 1, DateTimeOffset.UtcNow,
            "correlation-1", "message-1", "issuer-1", 1);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new ProjectionRequest(
            "tenant-1", "timesheets", "owner-z", [projectionEvent]), s_json);
        var delivery = new SharedProjectionDelivery("tenant-1:timesheets:owner-z", 1, payload, []);

        IReadOnlyList<ReadModelBatchOperation> operations = await handler.FoldCatchUpAsync(
            delivery, selected.Generation, coordinator, TestContext.Current.CancellationToken);

        ReadModelBatchOperation pageWrite = operations.Single(operation => operation.Key ==
            MagicLinkTokenHashCapabilityIndexProjection.DirectoryPageKey(shard, 1));
        MagicLinkTokenHashCapabilityIndexDirectoryPage changed = JsonSerializer.Deserialize<
            MagicLinkTokenHashCapabilityIndexDirectoryPage>(pageWrite.CanonicalValue.Span, s_json).ShouldNotBeNull();
        changed.Entries.Select(static row => row.Candidate.CapabilityId.Value)
            .ShouldBe(["owner-c", "owner-d", "owner-z"]);
        operations.ShouldNotContain(operation => operation.Key ==
            MagicLinkTokenHashCapabilityIndexProjection.DirectoryPageKey(shard, 0));
    }

    [Fact]
    public async Task MoreThanFourHundredNumericDirectoryPagesRemainSelectable()
    {
        const int pageCount = 401;
        int shard = MagicLinkTokenHashCapabilityIndexProjection.ShardOrdinal("large-directory-root");
        string[] hashes = Enumerable.Range(0, 250_000)
            .Select(index => $"directory-hash-{index:D6}")
            .Where(hash => MagicLinkTokenHashCapabilityIndexProjection.ShardOrdinal(hash) == shard)
            .Take(pageCount).Order(StringComparer.Ordinal).ToArray();
        hashes.Length.ShouldBe(pageCount);
        var pages = hashes.Select((hash, index) => new MagicLinkTokenHashCapabilityIndexDirectoryPage(
            [new MagicLinkTokenHashCapabilityIndexDirectoryEntry(hash,
                new MagicLinkTokenHashCapabilityIndexEntry(
                    new TenantReference(index % 2 == 0 ? "tenant-a" : "tenant-b"),
                    new MagicLinkCapabilityId($"owner-{index:D5}")))])) .ToArray();
        MagicLinkTokenHashCapabilityIndexReadModel root =
            MagicLinkTokenHashCapabilityIndexProjection.FromContributions([]) with
            {
                DirectoryPages = [.. pages.Select((page, index) =>
                    new MagicLinkTokenHashCapabilityIndexDirectoryReference(index,
                        page.Entries[0].TokenHash, page.Entries[0].TokenHash))],
                DirectoryPageDigests = pages.Select((page, index) => (page, index)).ToDictionary(
                    static item => item.index,
                    static item => MagicLinkTokenHashCapabilityIndexProjection.DirectoryPageDigest(item.page)),
                NextCandidatePageId = pageCount
            };
        MagicLinkTokenHashCapabilityIndexProjection.HasValidPageManifest(root).ShouldBeTrue();
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        _ = await coordinator.RegisterWriterAsync(scope,
            MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        _ = await coordinator.BeginAsync(scope, "large-directory", "large-directory-inventory",
            new Dictionary<string, long>(), TestContext.Current.CancellationToken);

        async IAsyncEnumerable<ReadModelBatchOperation> Operations()
        {
            for (int index = 0; index < pages.Length; index++)
            {
                yield return ReadModelBatchOperation.Write(
                    MagicLinkTokenHashCapabilityIndexProjection.DirectoryPageKey(shard, index),
                    pages[index], ReadModelBatchConcurrency.LastWrite);
            }
            yield return ReadModelBatchOperation.Write(
                MagicLinkTokenHashCapabilityIndexProjection.ShardKey(shard),
                root, ReadModelBatchConcurrency.LastWrite);
            await Task.CompletedTask;
        }

        _ = await coordinator.StageStreamingAsync(scope, "large-directory", Operations(),
            TestContext.Current.CancellationToken);
        await coordinator.CommitAsync(scope, "large-directory", TestContext.Current.CancellationToken);
        SharedProjectionReadResult<MagicLinkTokenHashCapabilityIndexReadModel> selected = await coordinator
            .ReadAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope,
                MagicLinkTokenHashCapabilityIndexProjection.ShardKey(shard),
                TestContext.Current.CancellationToken);
        selected.Value.ShouldNotBeNull().DirectoryPages.Count.ShouldBe(pageCount);
        MagicLinkTokenHashCapabilityIndexDirectoryReference middle = selected.Value.DirectoryPages[pageCount / 2];
        SharedProjectionReadResult<MagicLinkTokenHashCapabilityIndexDirectoryPage> interior = await coordinator
            .ReadAsync<MagicLinkTokenHashCapabilityIndexDirectoryPage>(scope,
                MagicLinkTokenHashCapabilityIndexProjection.DirectoryPageKey(shard, middle.PageId),
                TestContext.Current.CancellationToken);
        MagicLinkTokenHashCapabilityIndexProjection.HasValidDirectoryPage(selected.Value, shard,
            middle, interior.Value).ShouldBeTrue();
        interior.Value.ShouldNotBeNull().Entries[0].Candidate.Tenant.TenantId
            .ShouldBe(middle.PageId % 2 == 0 ? "tenant-a" : "tenant-b");
        string interiorHash = interior.Value.Entries[0].TokenHash;
        var loader = new EventStoreMagicLinkConfirmationCapabilityStateLoader(
            new FakeEventStoreGatewayClient(), store, new FixedIndexTokenGenerator(interiorHash),
            new FixedIndexTrustedContextAccessor(), coordinator);
        var resolve = typeof(EventStoreMagicLinkConfirmationCapabilityStateLoader).GetMethod(
            "ResolveCandidateAsync", BindingFlags.Instance | BindingFlags.NonPublic).ShouldNotBeNull();
        var resolved = (Task<MagicLinkTokenHashCapabilityIndexEntry?>)resolve.Invoke(loader,
            [new MagicLinkTokenHash(interiorHash), TestContext.Current.CancellationToken]).ShouldNotBeNull();
        (await resolved).ShouldBe(interior.Value.Entries[0].Candidate);
    }

    [Fact]
    public void SelectedDirectoryRejectsOverlapUnsortedInteriorDuplicateAndTampering()
    {
        string[] hashes = Enumerable.Range(0, 50_000)
            .Select(index => $"integrity-hash-{index:D5}")
            .Where(hash => MagicLinkTokenHashCapabilityIndexProjection.ShardOrdinal(hash)
                == MagicLinkTokenHashCapabilityIndexProjection.ShardOrdinal("integrity-hash-00000"))
            .Take(4).Order(StringComparer.Ordinal).ToArray();
        hashes.Length.ShouldBe(4);
        int shard = MagicLinkTokenHashCapabilityIndexProjection.ShardOrdinal(hashes[0]);
        MagicLinkTokenHashCapabilityIndexEntry Candidate(int index) => new(
            new TenantReference("tenant-a"), new MagicLinkCapabilityId($"owner-{index}"));
        MagicLinkTokenHashCapabilityIndexDirectoryEntry[] rows = [.. hashes.Select((hash, index) =>
            new MagicLinkTokenHashCapabilityIndexDirectoryEntry(hash, Candidate(index)))];
        var page = new MagicLinkTokenHashCapabilityIndexDirectoryPage(rows);
        var reference = new MagicLinkTokenHashCapabilityIndexDirectoryReference(0, hashes[0], hashes[^1]);
        MagicLinkTokenHashCapabilityIndexReadModel root =
            MagicLinkTokenHashCapabilityIndexProjection.FromContributions([]) with
            {
                DirectoryPages = [reference],
                DirectoryPageDigests = new Dictionary<int, string>
                {
                    [0] = MagicLinkTokenHashCapabilityIndexProjection.DirectoryPageDigest(page)
                },
                NextCandidatePageId = 1
            };
        MagicLinkTokenHashCapabilityIndexProjection.HasValidPageManifest(root).ShouldBeTrue();
        MagicLinkTokenHashCapabilityIndexProjection.HasValidDirectoryPage(root, shard, reference, page)
            .ShouldBeTrue();
        MagicLinkTokenHashCapabilityIndexProjection.HasValidPageManifest(root with
        {
            Contributions = new Dictionary<string, IReadOnlyList<MagicLinkTokenHashCapabilityIndexEntry>>
            {
                [hashes[1]] = [Candidate(1)]
            },
            Entries = new Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry>
            {
                [hashes[1]] = Candidate(1)
            }
        }).ShouldBeFalse();
        MagicLinkTokenHashCapabilityIndexProjection.HasValidDirectoryPage(root, shard, reference,
            new MagicLinkTokenHashCapabilityIndexDirectoryPage(
                [rows[0], rows[2], rows[1], rows[3]])).ShouldBeFalse();
        MagicLinkTokenHashCapabilityIndexProjection.HasValidDirectoryPage(root, shard, reference,
            new MagicLinkTokenHashCapabilityIndexDirectoryPage(
                [rows[0], rows[1], rows[1], rows[3]])).ShouldBeFalse();
        MagicLinkTokenHashCapabilityIndexProjection.HasValidDirectoryPage(root, shard, reference,
            new MagicLinkTokenHashCapabilityIndexDirectoryPage(
                [rows[0], rows[1] with { Candidate = Candidate(99) }, rows[2], rows[3]])).ShouldBeFalse();
        MagicLinkTokenHashCapabilityIndexProjection.HasValidDirectoryPage(root, (shard + 1) % 256,
            reference, page).ShouldBeFalse();
        MagicLinkTokenHashCapabilityIndexProjection.HasValidDirectoryPage(root, shard, reference, null)
            .ShouldBeFalse();
    }

    [Fact]
    public async Task TenantSliceRebuildRejectsTamperedSelectedCollisionPage()
    {
        const string hash = "selected-collision-integrity";
        int shard = MagicLinkTokenHashCapabilityIndexProjection.ShardOrdinal(hash);
        var first = new MagicLinkTokenHashCapabilityIndexEntry(
            new TenantReference("tenant-a"), new MagicLinkCapabilityId("owner-a"));
        var second = new MagicLinkTokenHashCapabilityIndexEntry(
            new TenantReference("tenant-b"), new MagicLinkCapabilityId("owner-b"));
        var clean = new MagicLinkTokenHashCapabilityIndexPage(hash, [first, second]);
        var corrupt = new MagicLinkTokenHashCapabilityIndexPage(hash, [first, first]);
        MagicLinkTokenHashCapabilityIndexReadModel root =
            MagicLinkTokenHashCapabilityIndexProjection.FromContributions([]) with
            {
                CandidatePageCounts = new Dictionary<string, int> { [hash] = 1 },
                CandidatePageIds = new Dictionary<string, IReadOnlyList<int>> { [hash] = [0] },
                CandidatePageDigests = new Dictionary<int, string>
                {
                    [0] = MagicLinkTokenHashCapabilityIndexProjection.CandidatePageDigest(clean)
                },
                NextCandidatePageId = 1
            };
        MagicLinkTokenHashCapabilityIndexProjection.HasValidPageManifest(root).ShouldBeTrue();
        MagicLinkTokenHashCapabilityIndexProjection.HasValidCandidatePage(root, hash, 0, clean)
            .ShouldBeTrue();
        MagicLinkTokenHashCapabilityIndexProjection.HasValidCandidatePage(root, hash, 0, corrupt)
            .ShouldBeFalse();

        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store,
            epochCoordinator: coordinator);
        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        _ = await coordinator.BeginAsync(scope, "seed-corrupt-collision", "sealed-inventory",
            new Dictionary<string, long>(), TestContext.Current.CancellationToken);
        _ = await coordinator.StageAsync(scope, "seed-corrupt-collision",
            [
                ReadModelBatchOperation.Write(MagicLinkTokenHashCapabilityIndexProjection.ShardKey(shard),
                    root, ReadModelBatchConcurrency.LastWrite),
                ReadModelBatchOperation.Write(MagicLinkTokenHashCapabilityIndexProjection.CandidatePageKey(shard, 0),
                    corrupt, ReadModelBatchConcurrency.LastWrite)
            ], TestContext.Current.CancellationToken);
        await coordinator.CommitAsync(scope, "seed-corrupt-collision", TestContext.Current.CancellationToken);
        var identity = new DomainSharedProjectionRebuildIdentity("tenant-b", "timesheets",
            handler.ProjectionType, "reject-corrupt-selected", "catalog");
        _ = await coordinator.BeginAsync(scope, identity.OperationId, "new-inventory",
            new Dictionary<string, long>(), TestContext.Current.CancellationToken);
        await Should.ThrowAsync<SharedProjectionReadUnavailableException>(async () =>
        {
            await foreach (ReadModelBatchOperation _ in handler.StreamFinalizedOperationsAsync(
                identity, new FixedCursor([]), TestContext.Current.CancellationToken))
            {
            }
        });
        SharedProjectionReadResult<MagicLinkTokenHashCapabilityIndexReadModel> selected = await coordinator
            .ReadAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope,
                MagicLinkTokenHashCapabilityIndexProjection.ShardKey(shard),
                TestContext.Current.CancellationToken);
        selected.Value.ShouldNotBeNull().CandidatePageDigests[0]
            .ShouldBe(MagicLinkTokenHashCapabilityIndexProjection.CandidatePageDigest(clean));
    }

    [Fact]
    public void CollisionSurvivesEitherTenantSliceRebuildAndRestoresOnlyAfterPrune()
    {
        const string hash = "shared-hash";
        var first = new MagicLinkTokenHashCapabilityIndexEntry(
            new TenantReference("tenant-a"), new MagicLinkCapabilityId("owner-a"));
        var second = new MagicLinkTokenHashCapabilityIndexEntry(
            new TenantReference("tenant-b"), new MagicLinkCapabilityId("owner-b"));
        MagicLinkTokenHashCapabilityIndexReadModel selected =
            MagicLinkTokenHashCapabilityIndexProjection.FromContributions(
                [new(hash, first), new(hash, second)]);
        selected.Entries.ShouldNotContainKey(hash);
        selected.Contributions[hash].Count.ShouldBe(2);

        var roundTrip = JsonSerializer.Deserialize<MagicLinkTokenHashCapabilityIndexReadModel>(
            JsonSerializer.SerializeToUtf8Bytes(selected, s_json), s_json).ShouldNotBeNull();
        MagicLinkTokenHashCapabilityIndexReadModel rebuiltA =
            MagicLinkTokenHashCapabilityIndexProjection.ReplaceTenant(roundTrip, "tenant-a",
                MagicLinkTokenHashCapabilityIndexProjection.FromContributions([new(hash, first)]));
        rebuiltA.Entries.ShouldNotContainKey(hash);
        MagicLinkTokenHashCapabilityIndexReadModel rebuiltB =
            MagicLinkTokenHashCapabilityIndexProjection.ReplaceTenant(rebuiltA, "tenant-b",
                MagicLinkTokenHashCapabilityIndexProjection.FromContributions([new(hash, second)]));
        rebuiltB.Entries.ShouldNotContainKey(hash);

        MagicLinkTokenHashCapabilityIndexReadModel pruned =
            MagicLinkTokenHashCapabilityIndexProjection.ReplaceTenant(rebuiltB, "tenant-a",
                MagicLinkTokenHashCapabilityIndexProjection.FromContributions([]));
        pruned.Entries[hash].ShouldBe(second);
        pruned.Contributions[hash].Count.ShouldBe(1);
    }

    [Fact]
    public async Task PartialTenantRebuildRejectsLegacyIndexUntilCertifiedMigration()
    {
        const string hash = "legacy-unique-hash";
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store,
            epochCoordinator: coordinator);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        var legacy = new MagicLinkTokenHashCapabilityIndexReadModel(
            new Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry>
            {
                [hash] = new(new TenantReference("tenant-1"), new MagicLinkCapabilityId("legacy-owner"))
            });
        _ = await coordinator.BeginAsync(scope, "seed-legacy", "seed-legacy-inventory",
            new Dictionary<string, long>(), TestContext.Current.CancellationToken);
        _ = await coordinator.StageAsync(scope, "seed-legacy",
            [ReadModelBatchOperation.Write(MagicLinkTokenHashCapabilityIndexProjection.ShardKey(hash),
                legacy, ReadModelBatchConcurrency.LastWrite)], TestContext.Current.CancellationToken);
        await coordinator.CommitAsync(scope, "seed-legacy", TestContext.Current.CancellationToken);

        var identity = new DomainSharedProjectionRebuildIdentity("tenant-2", "timesheets",
            handler.ProjectionType, "partial-legacy-rebuild", "catalog");
        _ = await coordinator.BeginAsync(scope, identity.OperationId, "partial-legacy-inventory",
            new Dictionary<string, long>(), TestContext.Current.CancellationToken);
        await Should.ThrowAsync<SharedProjectionReadUnavailableException>(() => coordinator.StageStreamingAsync(scope, identity.OperationId,
            handler.StreamFinalizedOperationsAsync(identity, new FixedCursor([]),
                TestContext.Current.CancellationToken), TestContext.Current.CancellationToken));
        await coordinator.AbortAsync(scope, identity.OperationId,
            (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]),
            TestContext.Current.CancellationToken);

        SharedProjectionReadResult<MagicLinkTokenHashCapabilityIndexReadModel> selected =
            await coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope,
                MagicLinkTokenHashCapabilityIndexProjection.ShardKey(hash),
                TestContext.Current.CancellationToken);
        selected.Value.ShouldNotBeNull().Entries.ContainsKey(hash).ShouldBeTrue();
    }

    [Fact]
    public async Task TenThousandHighEntropyCandidatesFinalizeIntoBoundedSelectedShards()
    {
        const int count = 10_000;
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store,
            epochCoordinator: coordinator);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-1"),
            handler.OrdinaryWriterId, TestContext.Current.CancellationToken);
        var identity = new DomainSharedProjectionRebuildIdentity("tenant-1", "timesheets",
            handler.ProjectionType, "large-index-rebuild", "catalog");
        DomainSharedProjectionRebuildPartition[] partitions = [.. Enumerable.Range(0, count)
            .Select(index =>
            {
                string owner = $"capability-{index:D5}";
                string hash = Convert.ToHexString(SHA256.HashData(BitConverter.GetBytes(index)));
                var candidate = new MagicLinkTokenHashCapabilityIndexEntry(
                    new TenantReference("tenant-1"), new MagicLinkCapabilityId(owner));
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    TokenHash = hash,
                    Candidate = candidate
                }, s_json);
                return new DomainSharedProjectionRebuildPartition(index, owner, bytes);
            })];
        var cursor = new FixedCursor(partitions);
        _ = await coordinator.BeginAsync(handler.CreateScope("tenant-1"), identity.OperationId,
            "large-index-inventory", new Dictionary<string, long>(), TestContext.Current.CancellationToken);
        var operations = new List<ReadModelBatchOperation>();
        await foreach (ReadModelBatchOperation operation in handler.StreamFinalizedOperationsAsync(
            identity, cursor, TestContext.Current.CancellationToken))
            operations.Add(operation);

        operations.Count.ShouldBeGreaterThan(count);
        operations.ShouldAllBe(operation => operation.CanonicalValue.Length < 1_000_000);
        MagicLinkTokenHashCapabilityIndexTreeRoot root = JsonSerializer.Deserialize<
            MagicLinkTokenHashCapabilityIndexTreeRoot>(operations.Single(operation => operation.Key ==
                MagicLinkTokenHashCapabilityIndexTreeReader.RootKey).CanonicalValue.Span, s_json)
            .ShouldNotBeNull();
        root.StorageVersion.ShouldBe(7);
        root.NextNodeId.ShouldBeGreaterThan(count);
    }

    [Fact]
    public async Task ConcentratedLongOwnerCollisionStagesTenThousandBoundedRows()
    {
        const int count = 10_000;
        const string collision = "same-hash-for-all-owners";
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store,
            epochCoordinator: coordinator);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        var identity = new DomainSharedProjectionRebuildIdentity("tenant-1", "timesheets",
            handler.ProjectionType, "concentrated-index-rebuild", "catalog");
        DomainSharedProjectionRebuildPartition[] partitions = [.. Enumerable.Range(0, count)
            .Select(index =>
            {
                string owner = $"owner-{index:D5}-" + new string((char)('A' + index % 26), 108);
                var candidate = new MagicLinkTokenHashCapabilityIndexEntry(
                    new TenantReference("tenant-1"), new MagicLinkCapabilityId(owner));
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    TokenHash = collision,
                    Candidate = candidate
                }, s_json);
                return new DomainSharedProjectionRebuildPartition(index, owner, bytes);
            })];
        _ = await coordinator.BeginAsync(scope, identity.OperationId, "concentrated-inventory",
            new Dictionary<string, long>(), TestContext.Current.CancellationToken);
        _ = await coordinator.StageStreamingAsync(scope, identity.OperationId,
            handler.StreamFinalizedOperationsAsync(identity, new FixedCursor(partitions),
                TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        (await coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
            MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
            TestContext.Current.CancellationToken)).Value.ShouldBeNull();
        await coordinator.CommitAsync(scope, identity.OperationId, TestContext.Current.CancellationToken);
        MagicLinkTokenHashCapabilityIndexTreeRoot selected = (await coordinator
            .ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
                MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
                TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        var reader = new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, scope);
        (await reader.ReadRosterAsync(selected.TenantRoot, "tenant-1",
            TestContext.Current.CancellationToken)).ShouldNotBeNull().Count.ShouldBe(count);
        (await reader.FindCandidatesAsync(selected.TokenRoot, collision,
            TestContext.Current.CancellationToken)).Count.ShouldBe(2);

        var prune = identity with { OperationId = "prune-concentrated" };
        _ = await coordinator.BeginAsync(scope, prune.OperationId, "prune-inventory",
            new Dictionary<string, long>(), TestContext.Current.CancellationToken);
        _ = await coordinator.StageStreamingAsync(scope, prune.OperationId,
            handler.StreamFinalizedOperationsAsync(prune, new FixedCursor([]),
                TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        await coordinator.CommitAsync(scope, prune.OperationId, TestContext.Current.CancellationToken);
        MagicLinkTokenHashCapabilityIndexTreeRoot pruned = (await coordinator
            .ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
                MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
                TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        (await reader.FindCandidatesAsync(pruned.TokenRoot, collision,
            TestContext.Current.CancellationToken)).Count.ShouldBe(0);
    }

    [Fact]
    public async Task OneShardTenThousandDistinctMaximumOwnersUseBoundedDirectoryPages()
    {
        const int count = 10_000;
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store,
            epochCoordinator: coordinator);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        int shard = MagicLinkTokenHashCapabilityIndexProjection.ShardOrdinal("one-shard-target");
        var partitions = new List<DomainSharedProjectionRebuildPartition>(count);
        var expected = new Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry>(StringComparer.Ordinal);
        for (int candidate = 0; partitions.Count < count; candidate++)
        {
            string hash = Convert.ToHexString(SHA256.HashData(BitConverter.GetBytes(candidate)))
                + Convert.ToHexString(SHA256.HashData(BitConverter.GetBytes(~candidate)));
            if (MagicLinkTokenHashCapabilityIndexProjection.ShardOrdinal(hash) != shard) continue;
            int ordinal = partitions.Count;
            string prefix = $"owner-{ordinal:D5}-";
            string owner = prefix + new string((char)('A' + ordinal % 26),
                MagicLinkCapabilityId.MaxLength - prefix.Length);
            var candidateOwner = new MagicLinkTokenHashCapabilityIndexEntry(
                new TenantReference("tenant-1"), new MagicLinkCapabilityId(owner));
            var contribution = new
            {
                TokenHash = hash,
                Candidate = candidateOwner
            };
            partitions.Add(new DomainSharedProjectionRebuildPartition(ordinal, owner,
                JsonSerializer.SerializeToUtf8Bytes(contribution, s_json)));
            expected.Add(hash, candidateOwner);
        }

        var identity = new DomainSharedProjectionRebuildIdentity("tenant-1", "timesheets",
            handler.ProjectionType, "one-shard-distinct-rebuild", "catalog");
        _ = await coordinator.BeginAsync(scope, identity.OperationId, "one-shard-inventory",
            new Dictionary<string, long>(), TestContext.Current.CancellationToken);
        _ = await coordinator.StageStreamingAsync(scope, identity.OperationId,
            handler.StreamFinalizedOperationsAsync(identity, new FixedCursor(partitions),
                TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        await coordinator.CommitAsync(scope, identity.OperationId, TestContext.Current.CancellationToken);
        MagicLinkTokenHashCapabilityIndexTreeRoot selected = (await coordinator
            .ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
                MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
                TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        var reader = new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, scope);
        IReadOnlyList<MagicLinkTokenHashCapabilityIndexDirectoryEntry> roster =
            (await reader.ReadRosterAsync(selected.TenantRoot, "tenant-1",
                TestContext.Current.CancellationToken)).ShouldNotBeNull();
        roster.Count.ShouldBe(count);
        foreach (MagicLinkTokenHashCapabilityIndexDirectoryEntry row in roster)
        {
            expected[row.TokenHash].ShouldBe(row.Candidate);
            (await reader.FindCandidatesAsync(selected.TokenRoot, row.TokenHash,
                TestContext.Current.CancellationToken)).ShouldBe([row.Candidate]);
        }
        string interiorHash = roster[count / 2].TokenHash;
        (await reader.FindCandidatesAsync(selected.TokenRoot, interiorHash,
            TestContext.Current.CancellationToken)).ShouldBe([expected[interiorHash]]);
        var gateway = new FakeEventStoreGatewayClient();
        var loader = new EventStoreMagicLinkConfirmationCapabilityStateLoader(gateway, store,
            new FixedIndexTokenGenerator(interiorHash), new FixedIndexTrustedContextAccessor(), coordinator);
        var resolve = typeof(EventStoreMagicLinkConfirmationCapabilityStateLoader).GetMethod(
            "ResolveCandidateAsync", BindingFlags.Instance | BindingFlags.NonPublic).ShouldNotBeNull();
        var candidateTask = (Task<MagicLinkTokenHashCapabilityIndexEntry?>)resolve.Invoke(loader,
            [new MagicLinkTokenHash(interiorHash), TestContext.Current.CancellationToken]).ShouldNotBeNull();
        (await candidateTask).ShouldBe(expected[interiorHash]);
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
}
