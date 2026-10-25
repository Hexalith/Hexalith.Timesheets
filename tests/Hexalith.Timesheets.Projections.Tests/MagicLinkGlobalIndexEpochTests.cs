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

    /// <summary>Rejects malformed streamed owner content before exposing any selected mutation.</summary>
    /// <param name="json">The malformed owner contribution.</param>
    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{"tokenHash":null,"candidate":{"tenant":{"tenantId":"tenant-1"},"capabilityId":{"value":"capability-1"}}}""")]
    [InlineData("""{"tokenHash":"","candidate":{"tenant":{"tenantId":"tenant-1"},"capabilityId":{"value":"capability-1"}}}""")]
    [InlineData("""{"tokenHash":" ","candidate":{"tenant":{"tenantId":"tenant-1"},"capabilityId":{"value":"capability-1"}}}""")]
    [InlineData("""{"tokenHash":"hash","candidate":null}""")]
    [InlineData("""{"tokenHash":"hash","candidate":{"tenant":null,"capabilityId":{"value":"capability-1"}}}""")]
    [InlineData("""{"tokenHash":"hash","candidate":{"tenant":{"tenantId":null},"capabilityId":{"value":"capability-1"}}}""")]
    [InlineData("""{"tokenHash":"hash","candidate":{"tenant":{"tenantId":" "},"capabilityId":{"value":"capability-1"}}}""")]
    [InlineData("""{"tokenHash":"hash","candidate":{"tenant":{"tenantId":"tenant-1"},"capabilityId":null}}""")]
    [InlineData("""{"tokenHash":"hash","candidate":{"tenant":{"tenantId":"tenant-1"},"capabilityId":{"value":null}}}""")]
    [InlineData("""{"tokenHash":"hash","candidate":{"tenant":{"tenantId":"tenant-1"},"capabilityId":{"value":" "}}}""")]
    [InlineData("""{"tokenHash":"hash","candidate":{"tenant":{"tenantId":"tenant-2"},"capabilityId":{"value":"capability-1"}}}""")]
    [InlineData("""{"tokenHash":"hash","candidate":{"tenant":{"tenantId":"tenant-1"},"capabilityId":{"value":"other-owner"}}}""")]
    public async Task MalformedStreamedPartitionsFailBeforeYieldingOrWriting(string json)
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store,
            epochCoordinator: coordinator);
        var identity = new DomainSharedProjectionRebuildIdentity("tenant-1", "timesheets",
            handler.ProjectionType, "invalid-streamed-content", "catalog");
        const string key = MagicLinkTokenHashCapabilityIndexProjection.StateKey;
        MagicLinkTokenHashCapabilityIndexReadModel preserved =
            MagicLinkTokenHashCapabilityIndexProjection.FromContributions(
                [new("existing-hash", new(new TenantReference("tenant-2"), new MagicLinkCapabilityId("existing-owner")))]);
        await store.SaveAsync(handler.RebuildStoreName, key, preserved, TestContext.Current.CancellationToken);
        ReadModelEntry<MagicLinkTokenHashCapabilityIndexReadModel> before = await store
            .GetAsync<MagicLinkTokenHashCapabilityIndexReadModel>(handler.RebuildStoreName, key,
                TestContext.Current.CancellationToken);
        int writes = 0;
        store.ConcurrentWriteBeforeTrySave = () => writes++;
        ReadOnlyMemory<byte> valid = JsonSerializer.SerializeToUtf8Bytes(new
        {
            TokenHash = "valid-hash",
            Candidate = new MagicLinkTokenHashCapabilityIndexEntry(
                new TenantReference("tenant-1"), new MagicLinkCapabilityId("capability-0"))
        }, s_json);
        var cursor = new FixedCursor(
        [
            new(0, "capability-0", valid),
            new(1, "capability-1", System.Text.Encoding.UTF8.GetBytes(json))
        ]);
        var operations = new List<ReadModelBatchOperation>();

        _ = await Should.ThrowAsync<ProjectionFoldException>(async () =>
        {
            await foreach (ReadModelBatchOperation operation in handler.StreamFinalizedOperationsAsync(
                identity, cursor, TestContext.Current.CancellationToken))
            {
                operations.Add(operation);
            }
        });

        operations.ShouldBeEmpty();
        writes.ShouldBe(0);
        ReadModelEntry<MagicLinkTokenHashCapabilityIndexReadModel> after = await store
            .GetAsync<MagicLinkTokenHashCapabilityIndexReadModel>(handler.RebuildStoreName, key,
                TestContext.Current.CancellationToken);
        after.ETag.ShouldBe(before.ETag);
        JsonSerializer.Serialize(after.Value, s_json).ShouldBe(JsonSerializer.Serialize(before.Value, s_json));
    }

    /// <summary>Keeps a producer's JSON-null no-issuance contribution valid through commit.</summary>
    [Fact]
    public async Task StreamedNullContributionCommitsAnEmptyIndex()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store,
            epochCoordinator: coordinator);
        var identity = new DomainSharedProjectionRebuildIdentity("tenant-1", "timesheets",
            handler.ProjectionType, "null-streamed-content", "catalog");
        SharedProjectionScope scope = handler.CreateScope(identity.TenantId);
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        _ = await coordinator.BeginAsync(scope, identity.OperationId, "no-issuance-inventory",
            new Dictionary<string, long>(), TestContext.Current.CancellationToken);
        ReadOnlyMemory<byte> noIssuance = await handler.PreparePartitionAsync(identity,
            new ProjectionRequest(identity.TenantId, identity.Domain, "no-issuance-owner", []),
            TestContext.Current.CancellationToken);
        System.Text.Encoding.UTF8.GetString(noIssuance.Span).ShouldBe("null");

        _ = await coordinator.StageStreamingAsync(scope, identity.OperationId,
            handler.StreamFinalizedOperationsAsync(identity,
                new FixedCursor([new(0, "no-issuance-owner", noIssuance)]), TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        await coordinator.CommitAsync(scope, identity.OperationId, TestContext.Current.CancellationToken);

        SharedProjectionReadResult<MagicLinkTokenHashCapabilityIndexTreeRoot> selected = await coordinator
            .ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
                MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, TestContext.Current.CancellationToken);
        selected.IsAvailable.ShouldBeTrue();
        MagicLinkTokenHashCapabilityIndexTreeRoot root = selected.Value.ShouldNotBeNull();
        root.StorageVersion.ShouldBe(7);
        root.TokenRoot.ShouldBeNull();
        var reader = new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, scope);
        (await reader.ReadRosterAsync(root.TenantRoot, identity.TenantId,
            TestContext.Current.CancellationToken)).ShouldNotBeNull().ShouldBeEmpty();
    }

    /// <summary>Does not reclassify cursor transport faults as malformed contribution content.</summary>
    /// <param name="failure">The cursor fault category.</param>
    [Theory]
    [InlineData("http")]
    [InlineData("invalid-operation")]
    [InlineData("json")]
    public async Task StreamedCursorReadFaultsRetainTheirOriginalException(string failure)
    {
        Exception outage = failure switch
        {
            "http" => new HttpRequestException("Cursor unavailable."),
            "invalid-operation" => new InvalidOperationException("Cursor unavailable."),
            "json" => new JsonException("Cursor unavailable."),
            _ => throw new InvalidOperationException("Unknown cursor fault.")
        };
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store,
            epochCoordinator: coordinator);
        var identity = new DomainSharedProjectionRebuildIdentity("tenant-1", "timesheets",
            handler.ProjectionType, "unavailable-streamed-cursor", "catalog");
        SharedProjectionScope scope = handler.CreateScope(identity.TenantId);
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        _ = await coordinator.BeginAsync(scope, "seed-before-cursor-fault", "preserved-inventory",
            new Dictionary<string, long>(), TestContext.Current.CancellationToken);
        string key = MagicLinkTokenHashCapabilityIndexProjection.ShardKey("existing-hash");
        MagicLinkTokenHashCapabilityIndexReadModel preserved =
            MagicLinkTokenHashCapabilityIndexProjection.FromContributions(
                [new("existing-hash", new(new TenantReference("tenant-2"), new MagicLinkCapabilityId("existing-owner")))]);
        _ = await coordinator.StageAsync(scope, "seed-before-cursor-fault",
            [ReadModelBatchOperation.Write(key, preserved, ReadModelBatchConcurrency.LastWrite)],
            TestContext.Current.CancellationToken);
        await coordinator.CommitAsync(scope, "seed-before-cursor-fault", TestContext.Current.CancellationToken);
        long generation = (await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken))
            .ActiveGeneration;
        ReadModelEntry<MagicLinkTokenHashCapabilityIndexReadModel> before = await coordinator
            .ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope, generation, key,
                TestContext.Current.CancellationToken);
        ReadOnlyMemory<byte> valid = JsonSerializer.SerializeToUtf8Bytes(new
        {
            TokenHash = "valid-hash",
            Candidate = new MagicLinkTokenHashCapabilityIndexEntry(
                new TenantReference("tenant-1"), new MagicLinkCapabilityId("capability-1"))
        }, s_json);
        int writes = 0;
        store.ConcurrentWriteBeforeTrySave = () => writes++;
        var operations = new List<ReadModelBatchOperation>();

        Exception? thrown = await Record.ExceptionAsync(async () =>
        {
            await foreach (ReadModelBatchOperation operation in handler.StreamFinalizedOperationsAsync(
                identity, new FixedCursor([new(0, "capability-1", valid)], outage),
                TestContext.Current.CancellationToken))
            {
                operations.Add(operation);
            }
        });

        thrown.ShouldBeSameAs(outage);
        operations.ShouldBeEmpty();
        writes.ShouldBe(0);
        ReadModelEntry<MagicLinkTokenHashCapabilityIndexReadModel> after = await coordinator
            .ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope, generation, key,
                TestContext.Current.CancellationToken);
        after.ETag.ShouldBe(before.ETag);
        JsonSerializer.Serialize(after.Value, s_json).ShouldBe(JsonSerializer.Serialize(before.Value, s_json));
        SharedProjectionReadResult<MagicLinkTokenHashCapabilityIndexReadModel> selected = await coordinator
            .ReadAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope, key, TestContext.Current.CancellationToken);
        selected.IsAvailable.ShouldBeTrue();
        JsonSerializer.Serialize(selected.Value, s_json).ShouldBe(JsonSerializer.Serialize(preserved, s_json));
    }

    /// <summary>Cancellation takes precedence over invalid streamed contribution content.</summary>
    [Fact]
    public async Task StreamedPartitionCancellationRemainsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store,
            epochCoordinator: new SharedProjectionEpochCoordinator(store, store));
        var identity = new DomainSharedProjectionRebuildIdentity("tenant-1", "timesheets",
            handler.ProjectionType, "canceled-streamed-content", "catalog");
        var cursor = new FixedCursor([new(0, "capability-1", System.Text.Encoding.UTF8.GetBytes("{"))],
            beforeYield: cancellation.Cancel);
        int writes = 0;
        store.ConcurrentWriteBeforeTrySave = () => writes++;

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await foreach (ReadModelBatchOperation _ in handler.StreamFinalizedOperationsAsync(
                identity, cursor, cancellation.Token))
            {
            }
        });

        writes.ShouldBe(0);
    }

    /// <summary>Exercises both bootstrap paths, boundary shards, and persisted recovery shapes.</summary>
    public static TheoryData<bool, int, string> DirectShardCases
    {
        get
        {
            var cases = new TheoryData<bool, int, string>();
            foreach (bool streamed in new[] { false, true })
            {
                foreach (int shard in new[] { 0, 255 })
                foreach (string shape in new[] { "populated", "empty", "corrupt", "null", "invalid" })
                    cases.Add(streamed, shard, shape);
                cases.Add(streamed, -1, "null");
            }
            return cases;
        }
    }

    /// <summary>Never replaces any direct legacy row, even empty, malformed or JSON-null evidence.</summary>
    /// <param name="streamed">Whether to use tenant rebuild finalization.</param>
    /// <param name="shard">The direct physical shard ordinal.</param>
    /// <param name="shape">The persisted legacy row shape.</param>
    [Theory]
    [MemberData(nameof(DirectShardCases))]
    public async Task DirectLegacyShardsRefuseBothBootstrapPathsWithoutLosingRecoveryEvidence(
        bool streamed, int shard, string shape)
    {
        var inner = new InMemoryReadModelStore();
        var store = new DirectShardObservingReadModelStore(inner);
        var coordinator = new SharedProjectionEpochCoordinator(store, inner);
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store, epochCoordinator: coordinator);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        string key = shard < 0 ? MagicLinkTokenHashCapabilityIndexProjection.StateKey
            : MagicLinkTokenHashCapabilityIndexProjection.ShardKey(shard);
        using JsonDocument persisted = JsonDocument.Parse(shape switch
        {
            "populated" => """{"entries":{"legacy-hash":{"tenant":{"tenantId":"tenant-2"},"capabilityId":{"value":"legacy-owner"}}}}""",
            "empty" => """{"entries":{}}""",
            "corrupt" => """{"entries":null,"contributions":null}""",
            "null" => "null",
            "invalid" => "\"corrupt-row\"",
            _ => throw new InvalidOperationException("Unknown persisted fixture.")
        });
        inner.SeedRaw(scope.StoreName, key, persisted);
        byte[] before = inner.SnapshotRaw(scope.StoreName, key).ShouldNotBeNull();
        string? beforeETag = (await inner.GetAsync<object>(scope.StoreName, key,
            TestContext.Current.CancellationToken)).ETag;
        await PrepareBootstrapAttemptAsync(streamed, handler, coordinator, scope);
        SharedProjectionEpochStatus recovery = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);

        await Should.ThrowAsync<SharedProjectionReadUnavailableException>(() => AttemptBootstrapAsync(
            streamed, handler, coordinator, scope, TestContext.Current.CancellationToken));

        inner.SnapshotRaw(scope.StoreName, key).ShouldBe(before);
        (await inner.GetAsync<object>(scope.StoreName, key, TestContext.Current.CancellationToken))
            .ETag.ShouldBe(beforeETag);
        await AssertBootstrapUnpublishedAsync(coordinator, scope, recovery);
        if (shard < 0) store.DirectReads.ShouldBeEmpty();
        else store.MaximumConcurrentReads.ShouldBeInRange(1, 16);
    }

    /// <summary>Scans all direct shards before a selected v6 shard can short-circuit ordinary detection.</summary>
    /// <param name="streamed">Whether to use tenant rebuild finalization.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectedLegacyShardCannotHideLastDirectLegacyShard(bool streamed)
    {
        var inner = new InMemoryReadModelStore();
        var store = new DirectShardObservingReadModelStore(inner);
        var coordinator = new SharedProjectionEpochCoordinator(store, inner);
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store, epochCoordinator: coordinator);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId, TestContext.Current.CancellationToken);
        _ = await coordinator.BeginAsync(scope, "seed-first-selected", "selected-inventory",
            new Dictionary<string, long>(), TestContext.Current.CancellationToken);
        _ = await coordinator.StageAsync(scope, "seed-first-selected", [ReadModelBatchOperation.Write(
            MagicLinkTokenHashCapabilityIndexProjection.ShardKey(0),
            MagicLinkTokenHashCapabilityIndexProjection.FromContributions([]), ReadModelBatchConcurrency.LastWrite)],
            TestContext.Current.CancellationToken);
        await coordinator.CommitAsync(scope, "seed-first-selected", TestContext.Current.CancellationToken);
        string key = MagicLinkTokenHashCapabilityIndexProjection.ShardKey(255);
        await inner.SaveAsync(scope.StoreName, key, MagicLinkTokenHashCapabilityIndexProjection.FromContributions([]),
            TestContext.Current.CancellationToken);
        byte[] before = inner.SnapshotRaw(scope.StoreName, key).ShouldNotBeNull();
        string? beforeETag = (await inner.GetAsync<object>(scope.StoreName, key,
            TestContext.Current.CancellationToken)).ETag;
        await PrepareBootstrapAttemptAsync(streamed, handler, coordinator, scope);
        SharedProjectionEpochStatus recovery = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);

        await Should.ThrowAsync<SharedProjectionReadUnavailableException>(() => AttemptBootstrapAsync(
            streamed, handler, coordinator, scope, TestContext.Current.CancellationToken));

        store.DirectReads.Count.ShouldBe(256);
        inner.SnapshotRaw(scope.StoreName, key).ShouldBe(before);
        (await inner.GetAsync<object>(scope.StoreName, key, TestContext.Current.CancellationToken))
            .ETag.ShouldBe(beforeETag);
        await AssertBootstrapUnpublishedAsync(coordinator, scope, recovery);
        (await coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope,
            MagicLinkTokenHashCapabilityIndexProjection.ShardKey(0), TestContext.Current.CancellationToken))
            .Value.ShouldNotBeNull();
    }

    /// <summary>Proves all 256 direct addresses are inspected under the fixed concurrency ceiling.</summary>
    /// <param name="streamed">Whether to use tenant rebuild finalization.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyDirectShardInventoryBootstrapsWithAtMostSixteenConcurrentReads(bool streamed)
    {
        var inner = new InMemoryReadModelStore();
        var store = new DirectShardObservingReadModelStore(inner);
        var coordinator = new SharedProjectionEpochCoordinator(store, inner);
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store, epochCoordinator: coordinator);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        await PrepareBootstrapAttemptAsync(streamed, handler, coordinator, scope);

        await AttemptBootstrapAsync(streamed, handler, coordinator, scope, TestContext.Current.CancellationToken);
        if (streamed) await coordinator.CommitAsync(scope, "direct-shard-bootstrap", TestContext.Current.CancellationToken);

        store.DirectReads.Keys.Order(StringComparer.Ordinal).ShouldBe(Enumerable.Range(0, 256)
            .Select(MagicLinkTokenHashCapabilityIndexProjection.ShardKey).Order(StringComparer.Ordinal));
        store.MaximumConcurrentReads.ShouldBeInRange(1, 16);
        (await coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
            MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, TestContext.Current.CancellationToken))
            .Value.ShouldNotBeNull().StorageVersion.ShouldBe(7);
    }

    /// <summary>Preserves direct-root/shard failures and cancellation with retryable recovery evidence.</summary>
    /// <param name="streamed">Whether to use tenant rebuild finalization.</param>
    /// <param name="failure">The injected direct read failure.</param>
    /// <param name="singleRoot">Whether to fault the legacy single-root address.</param>
    [Theory]
    [InlineData(false, "io", false)]
    [InlineData(true, "io", false)]
    [InlineData(false, "json", false)]
    [InlineData(true, "json", false)]
    [InlineData(false, "cancel", false)]
    [InlineData(true, "cancel", false)]
    [InlineData(false, "io", true)]
    [InlineData(true, "io", true)]
    [InlineData(false, "json", true)]
    [InlineData(true, "json", true)]
    [InlineData(false, "cancel", true)]
    [InlineData(true, "cancel", true)]
    public async Task DirectIndexReadFailureOrCancellationKeepsBootstrapRetryable(
        bool streamed, string failure, bool singleRoot)
    {
        var inner = new InMemoryReadModelStore();
        var store = new DirectShardObservingReadModelStore(inner);
        var coordinator = new SharedProjectionEpochCoordinator(store, inner);
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store, epochCoordinator: coordinator);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        string key = singleRoot ? MagicLinkTokenHashCapabilityIndexProjection.StateKey
            : MagicLinkTokenHashCapabilityIndexProjection.ShardKey(255);
        using JsonDocument? persisted = singleRoot ? JsonDocument.Parse("null") : null;
        if (persisted is not null) inner.SeedRaw(scope.StoreName, key, persisted);
        byte[]? before = inner.SnapshotRaw(scope.StoreName, key);
        string? beforeETag = (await inner.GetAsync<object>(scope.StoreName, key,
            TestContext.Current.CancellationToken)).ETag;
        await PrepareBootstrapAttemptAsync(streamed, handler, coordinator, scope);
        SharedProjectionEpochStatus recovery = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Exception outage = failure == "json" ? new JsonException("Direct index unavailable.")
            : new IOException("Direct index unavailable.");
        store.BeforeGet = address =>
        {
            if (address != key) return;
            if (failure == "cancel")
            {
                cancellation.Cancel();
                cancellation.Token.ThrowIfCancellationRequested();
            }
            throw outage;
        };
        if (failure == "cancel")
            await Should.ThrowAsync<OperationCanceledException>(() => AttemptBootstrapAsync(
                streamed, handler, coordinator, scope, cancellation.Token));
        else
        {
            SharedProjectionReadUnavailableException exception = await Should.ThrowAsync<SharedProjectionReadUnavailableException>(
                () => AttemptBootstrapAsync(streamed, handler, coordinator, scope, cancellation.Token));
            exception.InnerException.ShouldBeSameAs(outage);
        }
        inner.SnapshotRaw(scope.StoreName, key).ShouldBe(before);
        (await inner.GetAsync<object>(scope.StoreName, key, TestContext.Current.CancellationToken))
            .ETag.ShouldBe(beforeETag);
        await AssertBootstrapUnpublishedAsync(coordinator, scope, recovery);
        if (singleRoot) store.DirectReads.ShouldBeEmpty();
        else store.MaximumConcurrentReads.ShouldBeInRange(1, 16);

        store.BeforeGet = null;
        if (singleRoot)
        {
            // Recovering transport access must not discard the persisted JSON-null root.
            await Should.ThrowAsync<SharedProjectionReadUnavailableException>(() => AttemptBootstrapAsync(
                streamed, handler, coordinator, scope, TestContext.Current.CancellationToken));
            inner.SnapshotRaw(scope.StoreName, key).ShouldBe(before);
            (await inner.GetAsync<object>(scope.StoreName, key, TestContext.Current.CancellationToken))
                .ETag.ShouldBe(beforeETag);
            await AssertBootstrapUnpublishedAsync(coordinator, scope, recovery);
        }
        else
        {
            await AttemptBootstrapAsync(streamed, handler, coordinator, scope, TestContext.Current.CancellationToken);
            if (streamed) await coordinator.CommitAsync(scope, "direct-shard-bootstrap", TestContext.Current.CancellationToken);
            (await coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
                MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, TestContext.Current.CancellationToken))
                .Value.ShouldNotBeNull().StorageVersion.ShouldBe(7);
        }
    }

    /// <summary>Drains held reads after a synchronous store fault before refusal or retry can complete.</summary>
    /// <param name="streamed">Whether to use tenant rebuild finalization.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SynchronousDirectShardFaultDrainsStartedReadsBeforeRetry(bool streamed)
    {
        var inner = new InMemoryReadModelStore();
        var store = new DirectShardObservingReadModelStore(inner);
        var coordinator = new SharedProjectionEpochCoordinator(store, inner);
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store, epochCoordinator: coordinator);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        await PrepareBootstrapAttemptAsync(streamed, handler, coordinator, scope);
        SharedProjectionEpochStatus recovery = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var faultReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var outage = new IOException("Synchronous direct read failure.");
        store.BeforeGet = key =>
        {
            if (key != MagicLinkTokenHashCapabilityIndexProjection.ShardKey(1)) return;
            faultReached.TrySetResult();
            throw outage;
        };
        store.BeforeDirectRead = (_, token) => release.Task.WaitAsync(token);
        Task attempt = AttemptBootstrapAsync(streamed, handler, coordinator, scope, TestContext.Current.CancellationToken);
        bool returnedBeforeDrain;
        int heldReads;
        try
        {
            await faultReached.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            returnedBeforeDrain = attempt.IsCompleted;
            heldReads = store.ActiveReads;
        }
        finally
        {
            release.TrySetResult();
        }
        SharedProjectionReadUnavailableException exception = await Should.ThrowAsync<SharedProjectionReadUnavailableException>(
            () => attempt.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        exception.InnerException.ShouldBeSameAs(outage);
        returnedBeforeDrain.ShouldBeFalse();
        heldReads.ShouldBeInRange(1, 15);
        store.ActiveReads.ShouldBe(0);
        store.MaximumConcurrentReads.ShouldBeInRange(1, 16);
        await AssertBootstrapUnpublishedAsync(coordinator, scope, recovery);

        store.BeforeGet = null;
        store.BeforeDirectRead = null;
        await AttemptBootstrapAsync(streamed, handler, coordinator, scope, TestContext.Current.CancellationToken);
        if (streamed) await coordinator.CommitAsync(scope, "direct-shard-bootstrap", TestContext.Current.CancellationToken);
        store.ActiveReads.ShouldBe(0);
        store.MaximumConcurrentReads.ShouldBeInRange(1, 16);
        (await coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
            MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, TestContext.Current.CancellationToken))
            .Value.ShouldNotBeNull().StorageVersion.ShouldBe(7);
    }

    private static async Task PrepareBootstrapAttemptAsync(bool streamed,
        MagicLinkTokenHashCapabilityIndexProjectionHandler handler, SharedProjectionEpochCoordinator coordinator,
        SharedProjectionScope scope)
    {
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        if (streamed)
            _ = await coordinator.BeginAsync(scope, "direct-shard-bootstrap", "direct-shard-inventory",
                new Dictionary<string, long>(), TestContext.Current.CancellationToken);
        else
            (await coordinator.JournalAsync(scope, lease, BootstrapDelivery(), TestContext.Current.CancellationToken))
                .ShouldBe(SharedProjectionJournalResult.Journaled);
    }

    private static async Task AttemptBootstrapAsync(bool streamed,
        MagicLinkTokenHashCapabilityIndexProjectionHandler handler, SharedProjectionEpochCoordinator coordinator,
        SharedProjectionScope scope, CancellationToken cancellationToken)
    {
        if (streamed)
        {
            var identity = new DomainSharedProjectionRebuildIdentity("tenant-1", "timesheets", handler.ProjectionType,
                "direct-shard-bootstrap", "catalog");
            _ = await coordinator.StageStreamingAsync(scope, identity.OperationId,
                handler.StreamFinalizedOperationsAsync(identity, new FixedCursor([]), cancellationToken), cancellationToken);
        }
        else
            (await coordinator.CatchUpAsync(scope, (delivery, generation, token) =>
                handler.FoldCatchUpAsync(delivery, generation, coordinator, token), cancellationToken)).ShouldBe(1);
    }

    private static async Task AssertBootstrapUnpublishedAsync(SharedProjectionEpochCoordinator coordinator,
        SharedProjectionScope scope, SharedProjectionEpochStatus before)
    {
        SharedProjectionEpochStatus after = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        after.ActiveGeneration.ShouldBe(before.ActiveGeneration);
        after.Epoch.ShouldBe(before.Epoch);
        after.OperationId.ShouldBe(before.OperationId);
        after.IsBuilding.ShouldBe(before.IsBuilding);
        after.PendingDeliveryCount.ShouldBe(before.PendingDeliveryCount);
        after.PendingFoldFailures.ShouldBe(before.PendingFoldFailures);
        after.ParkingEvidence.ShouldBe(before.ParkingEvidence);
        (await coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
            MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, TestContext.Current.CancellationToken)).Value.ShouldBeNull();
        (await coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeFormat>(scope,
            MagicLinkTokenHashCapabilityIndexTreeReader.FormatKey, TestContext.Current.CancellationToken)).Value.ShouldBeNull();
    }

    private static SharedProjectionDelivery BootstrapDelivery()
    {
        var issued = new MagicLinkConfirmationCapabilityIssued(new MagicLinkCapabilityId("fresh-owner"),
            new TenantReference("tenant-1"), new PartyReference("party-1"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-1")), new ActivityTypeId("activity-1"),
            new TimeEntryId("entry-1"), MagicLinkTargetKind.ProposedTimeEntry, MagicLinkAllowedAction.Confirm,
            new MagicLinkTokenHash("fresh-ordinary-hash"), DateTimeOffset.UtcNow.AddDays(1),
            new PartyReference("issuer-1"), DateTimeOffset.UtcNow, new MagicLinkAuditMetadata("timesheets", "bootstrap"), true);
        var projectionEvent = new ProjectionEventDto(typeof(MagicLinkConfirmationCapabilityIssued).FullName!,
            JsonSerializer.SerializeToUtf8Bytes(issued, s_json), "json", 1, DateTimeOffset.UtcNow,
            "correlation-bootstrap", "message-bootstrap", "issuer-1", 1);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new ProjectionRequest("tenant-1", "timesheets",
            "fresh-owner", [projectionEvent]), s_json);
        return new SharedProjectionDelivery("tenant-1:timesheets:fresh-owner", 1, payload, [], OwnerSequenceNumber: 1);
    }

    private sealed class FixedCursor(IReadOnlyList<DomainSharedProjectionRebuildPartition> partitions,
        Exception? readException = null,
        Action? beforeYield = null)
        : IDomainSharedProjectionPartitionCursor
    {
        public async IAsyncEnumerable<DomainSharedProjectionRebuildPartition> ReadAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (DomainSharedProjectionRebuildPartition partition in partitions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                beforeYield?.Invoke();
                yield return partition;
            }
            if (readException is not null) throw readException;
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
