using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Timesheets.Contracts.Events.ActivityTypes;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.ActivityTypes;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections.ActivityTypes;
using Hexalith.Timesheets.Server.MagicLinks;

using Shouldly;

namespace Hexalith.Timesheets.Projections.Tests;

public sealed class TenantActivityTypeCatalogEpochTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RebuildPublicationPreservesAcceptedAdditionRenameDeactivationAndPolicy(bool recover)
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new TenantActivityTypeCatalogProjectionHandler(store);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId, Cancellation);
        await DeliverAsync("activity-1", 1, 1, Created("activity-1", "Original"));
        var identity = new DomainSharedProjectionRebuildIdentity("tenant-1", "timesheets", handler.ProjectionType,
            "rebuild-1", "catalog-1");
        await coordinator.BeginAsync(scope, identity.OperationId, "inventory-1",
            new Dictionary<string, long> { ["activity-1"] = 1 }, Cancellation);
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(identity, Cancellation);
        candidate = await handler.AccumulateAsync(identity, candidate, Request("activity-1", 1, 1,
            Created("activity-1", "Original")), Cancellation);
        lease = await coordinator.RefreshLeaseAsync(scope, handler.OrdinaryWriterId, Cancellation);
        SharedProjectionDelivery[] concurrent =
        [
            Delivery("activity-2", 1, 2, Created("activity-2", "Added")),
            Delivery("activity-1", 2, 3, new ActivityTypeRenamed(new("activity-1"), "Renamed")),
            Delivery("activity-1", 3, 4, new ActivityTypeDeactivated(new("activity-1"))),
            Delivery("policy-stream", 1, 5, new ProjectActivityTypeCatalogRestrictionConfigured(
                new("project-1"), true, [new("activity-2")], []))
        ];
        foreach (SharedProjectionDelivery delivery in concurrent)
            (await coordinator.JournalAsync(scope, lease, delivery, Cancellation))
                .ShouldBe(SharedProjectionJournalResult.Backpressure);
        (await coordinator.GetStatusAsync(scope, Cancellation)).PendingDeliveryCount.ShouldBe(0);
        (await coordinator.ReadAsync<ActivityTypeCatalogReadModel>(scope, Key(), Cancellation)).IsStale.ShouldBeTrue();
        DomainProjectionRebuildPlan plan = await handler.FinalizeAsync(identity, candidate, Cancellation);
        await coordinator.StageAsync(scope, identity.OperationId, plan.Operations, Cancellation);
        if (recover)
        {
            // Reconstruct both SDK and handler around persisted state after staging.
            coordinator = new(store, store);
            handler = new(store);
        }
        await coordinator.CommitAsync(scope, identity.OperationId, Cancellation);
        foreach (SharedProjectionDelivery delivery in concurrent)
        {
            (await coordinator.JournalAsync(scope, lease, delivery, Cancellation))
                .ShouldBe(SharedProjectionJournalResult.Journaled);
            (await CatchUpAsync()).ShouldBe(1);
        }
        // Repeating publication/catch-up must not reapply accepted deliveries.
        await coordinator.CommitAsync(scope, identity.OperationId, Cancellation);
        (await CatchUpAsync()).ShouldBe(0);
        SharedProjectionReadResult<ActivityTypeCatalogReadModel> selected = await coordinator
            .ReadAsync<ActivityTypeCatalogReadModel>(scope, Key(), Cancellation);
        selected.IsAvailable.ShouldBeTrue();
        selected.IsStale.ShouldBeFalse();
        ActivityTypeCatalogReadModel model = selected.Value.ShouldNotBeNull();
        model.Items.Single(row => row.ActivityTypeId.Value == "activity-1").Label.ShouldBe("Renamed");
        model.Items.Single(row => row.ActivityTypeId.Value == "activity-1").IsActive.ShouldBeFalse();
        model.Items.Single(row => row.ActivityTypeId.Value == "activity-2").Label.ShouldBe("Added");
        model.ProjectPolicy.ShouldNotBeNull().IsComplete.ShouldBeTrue();
        ProjectActivityTypeCatalogRestrictionSnapshot restrictive = model.ProjectPolicy.Restrictions.ShouldHaveSingleItem();
        restrictive.SourceAggregateId.ShouldBe("policy-stream");
        restrictive.IsRestricted.ShouldBeTrue();
        restrictive.Project.ShouldBe(new ProjectReference("project-1"));
        restrictive.AllowedTenantActivityTypeIds.ShouldBe([new ActivityTypeId("activity-2")]);
        await DeliverAsync("policy-stream", 2, 6, new ProjectActivityTypeCatalogRestrictionConfigured(
            new("project-1"), false, [], []));
        (await coordinator.ReadAsync<ActivityTypeCatalogReadModel>(scope, Key(), Cancellation)).Value
            .ShouldNotBeNull().ProjectPolicy.ShouldNotBeNull().Restrictions.ShouldHaveSingleItem().IsRestricted.ShouldBeFalse();
        (await store.GetAsync<ActivityTypeCatalogReadModel>(scope.StoreName, Key(), Cancellation)).Value.ShouldBeNull();
        // Another tenant's generation cannot supply the canonical tenant's policy or items.
        SharedProjectionScope other = handler.CreateScope("tenant-2");
        await coordinator.RegisterWriterAsync(other, handler.OrdinaryWriterId, Cancellation);
        (await coordinator.ReadAsync<ActivityTypeCatalogReadModel>(other, Key(), Cancellation)).Value.ShouldBeNull();

        async Task<int> CatchUpAsync() => await coordinator.CatchUpAsync(scope,
            (delivery, generation, token) => handler.FoldCatchUpAsync(delivery, generation, coordinator, token), Cancellation);
        async Task DeliverAsync(string owner, long sequence, long position, object payload)
        {
            SharedProjectionDelivery delivery = Delivery(owner, sequence, position, payload);
            (await coordinator.JournalAsync(scope, lease, delivery, Cancellation))
                .ShouldBe(SharedProjectionJournalResult.Journaled);
            await CatchUpAsync();
        }
    }

    [Fact]
    public async Task CancellationRetainsJournalAndRecreatedCoordinatorCompletesCatchUp()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new TenantActivityTypeCatalogProjectionHandler(store);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId, Cancellation);
        SharedProjectionDelivery delivery = Delivery("activity-1", 1, 1, Created("activity-1", "Delivery"));
        await coordinator.JournalAsync(scope, lease, delivery, Cancellation);
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        bool interruptedAfterPrepare = false;
        int folds = 0;
        store.BatchFaultHook = (phase, _, _) =>
        {
            if (phase == ReadModelBatchPhase.MarkerPrepared)
            {
                interruptedAfterPrepare = true;
                cancelled.Cancel();
                cancelled.Token.ThrowIfCancellationRequested();
            }
            return Task.CompletedTask;
        };
        await Should.ThrowAsync<SharedProjectionWriteUnavailableException>(() => coordinator.CatchUpAsync(scope,
            (item, generation, token) =>
            {
                folds++;
                return handler.FoldCatchUpAsync(item, generation, coordinator, token);
            }, cancelled.Token));
        interruptedAfterPrepare.ShouldBeTrue();
        cancelled.IsCancellationRequested.ShouldBeTrue();
        folds.ShouldBe(1);
        (await coordinator.GetStatusAsync(scope, Cancellation)).PendingDeliveryCount.ShouldBe(1);
        store.BatchFaultHook = null;
        coordinator = new(store, store);
        (await coordinator.CatchUpAsync(scope, (_, _, _) =>
            throw new InvalidOperationException("A durable prepared mutation must be recovered without refolding."), Cancellation)).ShouldBe(1);
        (await coordinator.JournalAsync(scope, lease, delivery, Cancellation)).ShouldBe(SharedProjectionJournalResult.AlreadyJournaled);
        (await coordinator.CatchUpAsync(scope, (_, _, _) =>
            throw new InvalidOperationException("A completed delivery must not be folded again."), Cancellation)).ShouldBe(0);
        (await coordinator.GetStatusAsync(scope, Cancellation)).PendingDeliveryCount.ShouldBe(0);
        ActivityTypeCatalogReadModel catalog = (await coordinator.ReadAsync<ActivityTypeCatalogReadModel>(scope, Key(), Cancellation))
            .Value.ShouldNotBeNull();
        catalog.Items.ShouldHaveSingleItem().Label.ShouldBe("Delivery");
        catalog.ProjectPolicy.ShouldNotBeNull().IsComplete.ShouldBeFalse();
    }

    [Fact]
    public async Task RebuildRetainsConflictingRestrictionSourcesInsteadOfGuessingAnOwner()
    {
        var store = new InMemoryReadModelStore();
        var handler = new TenantActivityTypeCatalogProjectionHandler(store);
        var identity = new DomainSharedProjectionRebuildIdentity("tenant-1", "timesheets", handler.ProjectionType, "op", "catalog");
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(identity, Cancellation);
        foreach (string source in new[] { "policy-a", "policy-b" })
            candidate = await handler.AccumulateAsync(identity, candidate, Request(source, 1, 1,
                new ProjectActivityTypeCatalogRestrictionConfigured(new("project-1"), true, [], [])), Cancellation);
        var plan = await handler.FinalizeAsync(identity, candidate, Cancellation);
        ActivityTypeCatalogReadModel catalog = JsonSerializer.Deserialize<ActivityTypeCatalogReadModel>(
            plan.Operations.Single().CanonicalValue.Span, Json).ShouldNotBeNull();
        catalog.ProjectPolicy.ShouldNotBeNull().IsComplete.ShouldBeTrue();
        catalog.ProjectPolicy.Restrictions.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData("null-restrictions")]
    [InlineData("foreign-tenant")]
    [InlineData("null-allowed")]
    public async Task PersistedMalformedPolicyCannotBecomeVerifiedUnrestrictedAfterLiveDelivery(string malformed)
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new TenantActivityTypeCatalogProjectionHandler(store);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId, Cancellation);
        var policy = new TenantActivityTypeCatalogPolicySnapshot(new(malformed == "foreign-tenant" ? "tenant-2" : "tenant-1"),
            true, malformed == "null-restrictions" ? null! : [new("policy", new("project-1"), true, null!, [])]);
        var corrupt = new ActivityTypeCatalogReadModel([], ProjectionFreshnessMetadata.Fresh) { ProjectPolicy = policy };
        await coordinator.BeginAsync(scope, "seed-corrupt", "inventory", new Dictionary<string, long>(), Cancellation);
        await coordinator.StageAsync(scope, "seed-corrupt", [ReadModelBatchOperation.Write(Key(), corrupt,
            ReadModelBatchConcurrency.LastWrite)], Cancellation);
        await coordinator.CommitAsync(scope, "seed-corrupt", Cancellation);
        lease = await coordinator.RefreshLeaseAsync(scope, handler.OrdinaryWriterId, Cancellation);
        SharedProjectionDelivery delivery = Delivery("activity-1", 1, 1, Created("activity-1", "Unrelated"));
        await coordinator.JournalAsync(scope, lease, delivery, Cancellation);
        await Should.ThrowAsync<SharedProjectionRetryableFoldException>(() => coordinator.CatchUpAsync(scope,
            (item, generation, token) => handler.FoldCatchUpAsync(item, generation, coordinator, token), Cancellation));
        SharedProjectionReadResult<ActivityTypeCatalogReadModel> selected = await coordinator
            .ReadAsync<ActivityTypeCatalogReadModel>(scope, Key(), Cancellation);
        selected.IsStale.ShouldBeTrue();
        selected.Value.ShouldNotBeNull().Items.ShouldBeEmpty();
        selected.Value.ProjectPolicy.ShouldNotBeNull().HasValidShapeForTenant(new("tenant-1")).ShouldBeFalse();
        (await coordinator.GetStatusAsync(scope, Cancellation)).PendingDeliveryCount.ShouldBe(1);
        coordinator = new(store, store);
        (await coordinator.CatchUpAsync(scope, (item, generation, token) =>
            handler.FoldCatchUpAsync(item, generation, coordinator, token), Cancellation)).ShouldBe(1);
        SharedProjectionReadResult<ActivityTypeCatalogReadModel> parkedCatalog = await coordinator
            .ReadAsync<ActivityTypeCatalogReadModel>(scope, Key(), Cancellation);
        parkedCatalog.IsDegraded.ShouldBeTrue();
        parkedCatalog.Value.ShouldNotBeNull().Items.ShouldBeEmpty();
        string suffix = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("activity-1\0" + "1")));
        Dictionary<string, long> parked = (await coordinator.ReadAsync<Dictionary<string, long>>(scope,
            "catalog:parked:" + suffix, Cancellation)).Value.ShouldNotBeNull();
        parked.Count.ShouldBe(2);
        parked["position"].ShouldBe(1);
        parked["attempts"].ShouldBe(2);
        (await coordinator.GetStatusAsync(scope, Cancellation)).PendingDeliveryCount.ShouldBe(0);
        var loader = new EventStoreMagicLinkConfirmationCapabilityStateLoader(new FakeEventStoreGatewayClient(),
            store, new CryptographicMagicLinkTokenGenerator(), new FixedIndexTrustedContextAccessor(), coordinator);
        (await loader.LoadActivityTypeCatalogAsync(Cancellation)).ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Unavailable);
        coordinator = new(store, store);
        (await coordinator.CatchUpAsync(scope, (_, _, _) =>
            throw new InvalidOperationException("Parked delivery must not be folded again."), Cancellation)).ShouldBe(0);

        var identity = new DomainSharedProjectionRebuildIdentity("tenant-1", "timesheets", handler.ProjectionType,
            "invalid-candidate", "catalog");
        var candidate = new DomainSharedProjectionRebuildCandidate(JsonSerializer.SerializeToUtf8Bytes(corrupt, Json));
        await Should.ThrowAsync<ProjectionFoldException>(() => handler.FinalizeAsync(identity, candidate, Cancellation));
        await Should.ThrowAsync<ProjectionFoldException>(() => handler.AccumulateAsync(identity, candidate,
            Request("activity-1", 1, 1, Created("activity-1", "Unrelated")), Cancellation));
        await store.SaveAsync(scope.StoreName, Key(), corrupt, Cancellation);
        (await handler.ProjectAsync(Request("activity-1", 1, 1, Created("activity-1", "Unrelated")), "direct", Cancellation))
            .Status.ShouldBe(ProjectionDispatchStatus.Failed);
        (await store.GetAsync<ActivityTypeCatalogReadModel>(scope.StoreName, Key(), Cancellation)).Value
            .ShouldNotBeNull().Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompleteFencedRebuildRecoversMalformedDirectPolicyWithoutChangingLegacyBytes()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new TenantActivityTypeCatalogProjectionHandler(store);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        var corrupt = new ActivityTypeCatalogReadModel([], ProjectionFreshnessMetadata.Fresh)
        {
            ProjectPolicy = new(new("tenant-1"), true, null!)
        };
        await store.SaveAsync(scope.StoreName, Key(), corrupt, Cancellation);
        byte[] original = store.SnapshotRaw(scope.StoreName, Key()).ShouldNotBeNull();
        await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId, Cancellation);
        var identity = new DomainSharedProjectionRebuildIdentity("tenant-1", "timesheets", handler.ProjectionType,
            "authoritative-recovery", "catalog");
        await coordinator.BeginAsync(scope, identity.OperationId, "certified-inventory",
            new Dictionary<string, long> { ["activity-1"] = 1 }, Cancellation);
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(identity, Cancellation);
        candidate = await handler.AccumulateAsync(identity, candidate, Request("activity-1", 1, 1,
            Created("activity-1", "Recovered")), Cancellation);
        DomainProjectionRebuildPlan plan = await handler.FinalizeAsync(identity, candidate, Cancellation);
        await coordinator.StageAsync(scope, identity.OperationId, plan.Operations, Cancellation);
        await coordinator.CommitAsync(scope, identity.OperationId, Cancellation);
        (await coordinator.CatchUpAsync(scope, (item, generation, token) =>
            handler.FoldCatchUpAsync(item, generation, coordinator, token), Cancellation)).ShouldBe(0);
        SharedProjectionReadResult<ActivityTypeCatalogReadModel> selected = await coordinator
            .ReadAsync<ActivityTypeCatalogReadModel>(scope, Key(), Cancellation);
        selected.IsAvailable.ShouldBeTrue();
        selected.IsStale.ShouldBeFalse();
        selected.IsDegraded.ShouldBeFalse();
        selected.Value.ShouldNotBeNull().Items.ShouldHaveSingleItem().Label.ShouldBe("Recovered");
        selected.Value.ProjectPolicy.ShouldNotBeNull().IsComplete.ShouldBeTrue();
        SharedProjectionLease lease = await coordinator.RefreshLeaseAsync(scope, handler.OrdinaryWriterId, Cancellation);
        await coordinator.JournalAsync(scope, lease, Delivery("activity-2", 1, 2, Created("activity-2", "Pending")), Cancellation);
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.BeginAsync(scope, "uncovered-recovery",
            "incomplete-inventory", new Dictionary<string, long>(), Cancellation, allowCoveredJournal: true));
        (await coordinator.GetStatusAsync(scope, Cancellation)).PendingDeliveryCount.ShouldBe(1);
        (await coordinator.ReadAsync<ActivityTypeCatalogReadModel>(scope, Key(), Cancellation)).IsStale.ShouldBeTrue();
        (await coordinator.CatchUpAsync(scope, (item, generation, token) =>
            handler.FoldCatchUpAsync(item, generation, coordinator, token), Cancellation)).ShouldBe(1);
        (await coordinator.ReadAsync<ActivityTypeCatalogReadModel>(scope, Key(), Cancellation)).Value
            .ShouldNotBeNull().Items.Count.ShouldBe(2);
        store.SnapshotRaw(scope.StoreName, Key()).ShouldNotBeNull().ShouldBe(original);
    }

    private static ActivityTypeCreated Created(string id, string label)
        => new(new(id), ActivityTypeScope.Tenant, null, label, BillableState.Billable);
    private static string Key() => TenantActivityTypeCatalogReadModelAddress.StateKey(new TenantReference("tenant-1"));
    private static ProjectionRequest Request(string owner, long sequence, long position, object payload)
        => new("tenant-1", "timesheets", owner, [new(payload.GetType().FullName!,
            JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType(), Json), "json", sequence,
            DateTimeOffset.UnixEpoch, "correlation-1", $"{owner}-{sequence}", "actor-1", 1) { GlobalPosition = position }]);
    private static SharedProjectionDelivery Delivery(string owner, long sequence, long position, object payload)
        => new(owner, position, JsonSerializer.SerializeToUtf8Bytes(Request(owner, sequence, position, payload), Json), [],
            OwnerSequenceNumber: sequence);
}
