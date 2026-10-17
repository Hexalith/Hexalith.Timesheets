using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks;

using Shouldly;

namespace Hexalith.Timesheets.Projections.Tests;

/// <summary>Persisted SDK proof using simulated verification; no real inventory authority is installed.</summary>
public sealed class MagicLinkV1MigrationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectOrSelectedV1RecoversAllCertifiedCandidatesIncludingCrossTenantCollisions(bool selected)
    {
        var store = new InMemoryReadModelStore();
        SharedProjectionEpochCoordinator coordinator = await SetupAsync(store, store, selected);
        MagicLinkTokenHashCapabilityIndexDirectoryEntry[] rows = Rows();
        var inventory = new SimulatedMagicLinkV1Inventory(rows);
        var migration = Migration(coordinator, store, inventory);
        long sourceGeneration = (await coordinator.GetStatusAsync(Scope(), Token)).ActiveGeneration;
        await migration.MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token);
        await AssertCandidatesAsync(coordinator, rows);
        await Migration(new SharedProjectionEpochCoordinator(store, store), store, inventory)
            .MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token);
        (await store.GetAsync<MagicLinkTokenHashCapabilityIndexReadModel>(Scope().StoreName,
            MagicLinkTokenHashCapabilityIndexProjection.StateKey, Token)).Value.ShouldNotBeNull();
        if (selected)
            (await coordinator.ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexReadModel>(Scope(),
                sourceGeneration, MagicLinkTokenHashCapabilityIndexProjection.StateKey, Token)).Value.ShouldNotBeNull();
        await Should.ThrowAsync<InvalidOperationException>(() => migration.MigrateAsync("changed-operation",
            inventory.Receipt.ReceiptId, Token));
        inventory.Receipt = inventory.Receipt with { SourceCutRevision = "changed-cut" };
        await Should.ThrowAsync<InvalidOperationException>(() => migration.MigrateAsync("explicit-v1",
            inventory.Receipt.ReceiptId, Token));
        await AssertCandidatesAsync(coordinator, rows);
    }

    [Theory]
    [InlineData("authority")]
    [InlineData("unavailable")]
    [InlineData("revoked")]
    [InlineData("installation")]
    [InlineData("quiescence")]
    public async Task MissingAuthorityCoverageOrQuiescenceNeverReservesAnEpoch(string missing)
    {
        var store = new InMemoryReadModelStore();
        SharedProjectionEpochCoordinator coordinator = await SetupAsync(store, store);
        var inventory = new SimulatedMagicLinkV1Inventory(Rows());
        var authority = new SimulatedMagicLinkV1Authority(inventory);
        if (missing == "authority") authority.Permitted = false;
        if (missing == "unavailable") inventory.Available = false;
        if (missing == "revoked") inventory.Valid = false;
        if (missing == "installation") inventory.Receipt = inventory.Receipt with { InstallationCoverageReceiptId = "" };
        if (missing == "quiescence") inventory.Receipt = inventory.Receipt with { QuiescenceReceiptId = "" };
        await Should.ThrowAsync<Exception>(() => new MagicLinkTokenHashCapabilityIndexV1Migration(coordinator,
            store, authority, inventory).MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token));
        await AssertLegacyOnlyAsync(coordinator, 0);
        (await coordinator.GetStatusAsync(Scope(), Token)).Epoch.ShouldBe(0);
    }

    [Theory]
    [InlineData("digest")]
    [InlineData("count")]
    [InlineData("duplicate")]
    [InlineData("conflicting-owner")]
    [InlineData("owner-order")]
    [InlineData("tenant-order")]
    [InlineData("tenant-bound")]
    [InlineData("tenant-width")]
    [InlineData("hash-width")]
    public async Task InvalidCompleteInventoryIsRejectedBeforeReservation(string invalid)
    {
        var store = new InMemoryReadModelStore();
        SharedProjectionEpochCoordinator coordinator = await SetupAsync(store, store);
        var inventory = new SimulatedMagicLinkV1Inventory(Rows());
        if (invalid == "digest") inventory.Receipt = inventory.Receipt with { ExpectedContributionDigest = new string('0', 64) };
        if (invalid == "count") inventory.Receipt = inventory.Receipt with { ExpectedContributionCount = 1 };
        if (invalid == "duplicate") inventory = new([Row("a", "owner", "shared"), Row("a", "owner", "shared")]);
        if (invalid == "conflicting-owner") inventory = new([Row("a", "owner", "first"), Row("a", "owner", "second")]);
        if (invalid == "owner-order") inventory = new([Row("a", "z", "first"), Row("a", "a", "second")]);
        if (invalid == "tenant-order") inventory = new([Row("z", "owner", "first"), Row("a", "owner", "second")]);
        if (invalid == "tenant-bound") inventory = new(Enumerable.Range(0, 10_001)
            .Select(index => Row("a", index.ToString("D5"), "shared")).ToArray());
        if (invalid == "tenant-width") inventory = new([Row(new string('a', 257), "owner", "shared")]);
        if (invalid == "hash-width") inventory = new([Row("a", "owner", new string('a', 129))]);
        await Should.ThrowAsync<InvalidOperationException>(() => Migration(coordinator, store, inventory)
            .MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token));
        await AssertLegacyOnlyAsync(coordinator, 0);
        (await coordinator.GetStatusAsync(Scope(), Token)).Epoch.ShouldBe(0);
    }

    [Theory]
    [InlineData("selected-v6")]
    [InlineData("direct-v6")]
    [InlineData("selected-v7")]
    [InlineData("direct-v7")]
    [InlineData("no-v1")]
    public async Task MixedOrMissingLegacyLayoutsAreRefusedBeforeReservation(string layout)
    {
        var store = new InMemoryReadModelStore();
        SharedProjectionEpochCoordinator coordinator = await SetupAsync(store, store);
        if (layout.StartsWith("selected", StringComparison.Ordinal))
        {
            _ = await coordinator.BeginAsync(Scope(), "mixed-seed", "fixture", new Dictionary<string, long>(), Token);
            string key = layout == "selected-v6" ? MagicLinkTokenHashCapabilityIndexProjection.ShardKey(255)
                : MagicLinkTokenHashCapabilityIndexTreeReader.FormatKey;
            ReadModelBatchOperation operation = layout == "selected-v6"
                ? ReadModelBatchOperation.Write(key, MagicLinkTokenHashCapabilityIndexProjection.FromContributions([]), ReadModelBatchConcurrency.LastWrite)
                : ReadModelBatchOperation.Write(key, new MagicLinkTokenHashCapabilityIndexTreeFormat(7), ReadModelBatchConcurrency.LastWrite);
            _ = await coordinator.StageAsync(Scope(), "mixed-seed", [operation], Token);
            await coordinator.CommitAsync(Scope(), "mixed-seed", Token);
        }
        if (layout == "direct-v6") store.SeedRaw(Scope().StoreName,
            MagicLinkTokenHashCapabilityIndexProjection.ShardKey(255), MagicLinkTokenHashCapabilityIndexProjection.FromContributions([]));
        if (layout == "direct-v7") store.SeedRaw(Scope().StoreName,
            MagicLinkTokenHashCapabilityIndexTreeReader.FormatKey, new MagicLinkTokenHashCapabilityIndexTreeFormat(7));
        if (layout == "no-v1")
        {
            var empty = new InMemoryReadModelStore();
            store = empty;
            coordinator = new(empty, empty);
            _ = await coordinator.RegisterWriterAsync(Scope(), MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId, Token);
        }
        SharedProjectionEpochStatus before = await coordinator.GetStatusAsync(Scope(), Token);
        var inventory = new SimulatedMagicLinkV1Inventory(Rows());
        await Should.ThrowAsync<InvalidOperationException>(() => Migration(coordinator, store, inventory)
            .MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token));
        SharedProjectionEpochStatus after = await coordinator.GetStatusAsync(Scope(), Token);
        after.Epoch.ShouldBe(before.Epoch);
        after.ActiveGeneration.ShouldBe(before.ActiveGeneration);
        after.IsBuilding.ShouldBeFalse();
    }

    [Fact]
    public async Task PendingLegacyDeliveryIsRefusedWithoutFabricatingCapturedIdentities()
    {
        var store = new InMemoryReadModelStore();
        SharedProjectionEpochCoordinator coordinator = await SetupAsync(store, store);
        SharedProjectionLease lease = await coordinator.RefreshLeaseAsync(Scope(), MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId, Token);
        (await coordinator.JournalAsync(Scope(), lease, new SharedProjectionDelivery(
            "legacy-owner", 1, [1], [], OwnerSequenceNumber: 1), Token)).ShouldBe(SharedProjectionJournalResult.Journaled);
        var inventory = new SimulatedMagicLinkV1Inventory(Rows());
        await Should.ThrowAsync<InvalidOperationException>(() => Migration(coordinator, store, inventory)
            .MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token));
        SharedProjectionEpochStatus status = await coordinator.GetStatusAsync(Scope(), Token);
        status.Epoch.ShouldBe(0);
        status.PendingDeliveryCount.ShouldBe(1);
        status.IsBuilding.ShouldBeFalse();
    }

    [Theory]
    [InlineData("node-before")]
    [InlineData("node-after")]
    [InlineData("checkpoint-before")]
    [InlineData("checkpoint-after")]
    [InlineData("checkpoint-advance")]
    [InlineData("seal")]
    [InlineData("commit")]
    [InlineData("commit-marker")]
    [InlineData("cancel-node")]
    public async Task InterruptedPrivatePagesCheckpointSealAndCommitResumeAfterRecreation(string fault)
    {
        var store = new InMemoryReadModelStore();
        var faultStore = new MagicLinkV1FaultStore(store);
        SharedProjectionEpochCoordinator coordinator = await SetupAsync(faultStore, faultStore);
        var inventory = new SimulatedMagicLinkV1Inventory(Rows());
        faultStore.Fault = fault;
        await Should.ThrowAsync<Exception>(() => Migration(coordinator, faultStore, inventory)
            .MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token));
        faultStore.Fired.ShouldBe(1);
        if (fault != "commit-marker")
        {
            (await coordinator.GetStatusAsync(Scope(), Token)).IsBuilding.ShouldBeTrue();
            (await coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(Scope(), MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, Token)).Value.ShouldBeNull();
        }
        coordinator = new(faultStore, faultStore);
        await Migration(coordinator, faultStore, inventory).MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token);
        await AssertCandidatesAsync(coordinator, Rows());
        (await coordinator.GetStatusAsync(Scope(), Token)).IsBuilding.ShouldBeFalse();
    }

    [Theory]
    [InlineData("receipt")]
    [InlineData("revoked-lookup")]
    [InlineData("direct-root")]
    [InlineData("selected-root")]
    [InlineData("source-cut")]
    [InlineData("changed-stream")]
    public async Task InvalidatedStagedEvidenceAbortsWithLegacySelectedAndPrivateGenerationIsolated(string invalid)
    {
        var store = new InMemoryReadModelStore();
        var faultStore = new MagicLinkV1FaultStore(store);
        SharedProjectionEpochCoordinator coordinator = await SetupAsync(faultStore, faultStore, invalid == "selected-root");
        long source = (await coordinator.GetStatusAsync(Scope(), Token)).ActiveGeneration;
        var inventory = new SimulatedMagicLinkV1Inventory(Rows());
        var authority = new SimulatedMagicLinkV1Authority(inventory);
        faultStore.Fault = "checkpoint-after";
        await Should.ThrowAsync<IOException>(() => new MagicLinkTokenHashCapabilityIndexV1Migration(coordinator,
            faultStore, authority, inventory).MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token));
        if (invalid == "receipt") inventory.Valid = false;
        if (invalid == "revoked-lookup") inventory.Available = false;
        if (invalid == "authority") authority.Permitted = false;
        if (invalid == "direct-root") store.SeedRaw(Scope().StoreName, MagicLinkTokenHashCapabilityIndexProjection.StateKey,
            Legacy(Row("tenant-changed", "changed", "changed")));
        if (invalid == "selected-root") store.SeedRaw(Scope().StoreName,
            PhysicalKey(source, MagicLinkTokenHashCapabilityIndexProjection.StateKey), Legacy(Row("tenant-changed", "changed", "changed")));
        if (invalid == "source-cut") inventory.Valid = false; // independent verifier rejects the changed external cut
        if (invalid == "changed-stream") inventory.Rows = [Row("tenant-a", "owner-a", "wrong"), .. Rows().Skip(1)];
        await Should.ThrowAsync<Exception>(() => new MagicLinkTokenHashCapabilityIndexV1Migration(new(faultStore, faultStore),
            faultStore, authority, inventory).MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token));
        await AssertLegacyOnlyAsync(coordinator, source);
        (await store.GetAsync<MagicLinkTokenHashCapabilityIndexReadModel>(Scope().StoreName,
            MagicLinkTokenHashCapabilityIndexProjection.StateKey, Token)).Value.ShouldNotBeNull();
        inventory.Valid = true;
        inventory.Available = true;
        inventory.Rows = Rows();
        authority.Permitted = true;
        await new MagicLinkTokenHashCapabilityIndexV1Migration(new(faultStore, faultStore), faultStore,
            authority, inventory).MigrateAsync("recovered-after-abort", inventory.Receipt.ReceiptId, Token);
        await AssertCandidatesAsync(new(faultStore, faultStore), Rows());
    }

    [Fact]
    public async Task ReceiptRevocationDuringStagingIsCheckedAgainBeforePromotion()
    {
        var store = new InMemoryReadModelStore();
        var faultStore = new MagicLinkV1FaultStore(store);
        SharedProjectionEpochCoordinator coordinator = await SetupAsync(faultStore, faultStore);
        var inventory = new SimulatedMagicLinkV1Inventory(Rows());
        faultStore.RevokeAfterFirstTenant = true;
        faultStore.AfterFirstTenant = () => inventory.Valid = false;
        await Should.ThrowAsync<InvalidOperationException>(() => Migration(coordinator, faultStore, inventory)
            .MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token));
        await AssertLegacyOnlyAsync(coordinator, 0);
    }

    [Fact]
    public async Task InterruptedAbortCleanupRetainsReceiptBindingAfterErasingFirstCheckpoint()
    {
        var store = new InMemoryReadModelStore();
        var faultStore = new MagicLinkV1FaultStore(store);
        SharedProjectionEpochCoordinator coordinator = await SetupAsync(faultStore, faultStore);
        var inventory = new SimulatedMagicLinkV1Inventory(Rows());
        MagicLinkV1InventoryReceipt original = inventory.Receipt;
        faultStore.RevokeAfterFirstTenant = true;
        faultStore.AfterFirstTenant = () => inventory.Valid = false;
        faultStore.Fault = "abort-cleanup";
        await Should.ThrowAsync<IOException>(() => Migration(coordinator, faultStore, inventory)
            .MigrateAsync("explicit-v1", original.ReceiptId, Token));
        faultStore.Fired.ShouldBe(1);
        SharedProjectionEpochStatus failed = await coordinator.GetStatusAsync(Scope(), Token);
        failed.IsAborting.ShouldBeTrue();
        failed.ActiveGeneration.ShouldBe(0);
        (await store.GetAsync<MagicLinkV1MigrationCheckpoint>(Scope().StoreName, PhysicalKey(failed.Epoch,
            MagicLinkTokenHashCapabilityIndexV1Migration.CheckpointKey(failed.Epoch, 0)), Token)).Value.ShouldBeNull();
        MagicLinkV1MigrationAbortWitness witness = (await store.GetAsync<MagicLinkV1MigrationAbortWitness>(Scope().StoreName,
            MagicLinkTokenHashCapabilityIndexV1Migration.AbortWitnessKey(failed.Epoch), Token)).Value.ShouldNotBeNull();
        witness.OperationId.ShouldBe("explicit-v1");
        witness.Receipt.ShouldBe(original);
        witness.SourceGeneration.ShouldBe(0);
        witness.SourceDigest.Length.ShouldBe(64);
        (await coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(Scope(),
            MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, Token)).Value.ShouldBeNull();

        coordinator = new(faultStore, faultStore);
        var migration = Migration(coordinator, faultStore, inventory);
        await Should.ThrowAsync<InvalidOperationException>(() => migration.MigrateAsync("another-operation", original.ReceiptId, Token));
        await Should.ThrowAsync<InvalidOperationException>(() => migration.MigrateAsync("explicit-v1", "another-receipt", Token));
        inventory.Receipt = original with { SourceCutRevision = "different-receipt-content" };
        await Should.ThrowAsync<InvalidOperationException>(() => migration.MigrateAsync("explicit-v1", original.ReceiptId, Token));
        (await coordinator.GetStatusAsync(Scope(), Token)).IsAborting.ShouldBeTrue();

        inventory.Receipt = original;
        inventory.Available = false; // revoked lookup must not prevent cleanup with retained exact evidence
        var deniedAuthority = new SimulatedMagicLinkV1Authority(inventory) { Permitted = false };
        await Should.ThrowAsync<UnauthorizedAccessException>(() => new MagicLinkTokenHashCapabilityIndexV1Migration(
            coordinator, faultStore, deniedAuthority, inventory).MigrateAsync("explicit-v1", original.ReceiptId, Token));
        (await coordinator.GetStatusAsync(Scope(), Token)).IsAborting.ShouldBeTrue();
        await Should.ThrowAsync<InvalidOperationException>(() => migration.MigrateAsync("explicit-v1", original.ReceiptId, Token));
        await AssertLegacyOnlyAsync(coordinator, 0);
        (await store.GetAsync<MagicLinkV1MigrationAbortWitness>(Scope().StoreName,
            MagicLinkTokenHashCapabilityIndexV1Migration.AbortWitnessKey(failed.Epoch), Token)).Value.ShouldBe(witness);
        inventory.Available = true;
        inventory.Valid = true;
        await Should.ThrowAsync<InvalidOperationException>(() => migration.MigrateAsync("explicit-v1", original.ReceiptId, Token));
        (await coordinator.GetStatusAsync(Scope(), Token)).Epoch.ShouldBe(failed.Epoch);
        await migration.MigrateAsync("fresh-after-abort", original.ReceiptId, Token);
        await AssertCandidatesAsync(coordinator, Rows());
    }

    [Fact]
    public async Task ChangedReceiptRetryCannotAbortAnExistingPrivateOperation()
    {
        var store = new InMemoryReadModelStore();
        var faultStore = new MagicLinkV1FaultStore(store);
        SharedProjectionEpochCoordinator coordinator = await SetupAsync(faultStore, faultStore);
        var inventory = new SimulatedMagicLinkV1Inventory(Rows());
        faultStore.Fault = "checkpoint-after";
        await Should.ThrowAsync<IOException>(() => Migration(coordinator, faultStore, inventory)
            .MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token));
        MagicLinkV1InventoryReceipt original = inventory.Receipt;
        inventory.Receipt = original with { SourceCutRevision = "different-receipt" };
        await Should.ThrowAsync<InvalidOperationException>(() => Migration(coordinator, faultStore, inventory)
            .MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token));
        (await coordinator.GetStatusAsync(Scope(), Token)).IsBuilding.ShouldBeTrue();
        inventory.Receipt = original;
        await Migration(coordinator, faultStore, inventory).MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token);
        await AssertCandidatesAsync(coordinator, Rows());
    }

    [Fact]
    public async Task PhysicalStagingUsesAtMostEightMutationsAndSixtyFourRowsPerRosterPage()
    {
        var store = new InMemoryReadModelStore();
        var observed = new MagicLinkV1FaultStore(store);
        SharedProjectionEpochCoordinator coordinator = await SetupAsync(observed, observed);
        MagicLinkTokenHashCapabilityIndexDirectoryEntry[] rows = [.. Enumerable.Range(0, 160)
            .Select(index => Row("tenant-a", index.ToString("D5"), "shared")), Row("tenant-b", "terminal-owner", "shared")];
        var inventory = new SimulatedMagicLinkV1Inventory(rows);
        await Migration(coordinator, observed, inventory).MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token);
        observed.MaximumPhysicalMutations.ShouldBeInRange(1, 8);
        observed.MaximumRosterRows.ShouldBeInRange(1, 64);
        await AssertCandidatesAsync(coordinator, rows);
    }

    [Fact]
    public async Task VerifiedEmptyInventoryCanPromoteOnlyWithRealReceiptShapeAndLegacySource()
    {
        var store = new InMemoryReadModelStore();
        SharedProjectionEpochCoordinator coordinator = await SetupAsync(store, store);
        var inventory = new SimulatedMagicLinkV1Inventory([]);
        await Migration(coordinator, store, inventory).MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token);
        (await coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(Scope(),
            MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, Token)).Value.ShouldNotBeNull().TokenRoot.ShouldBeNull();
    }

    private static MagicLinkTokenHashCapabilityIndexV1Migration Migration(SharedProjectionEpochCoordinator coordinator,
        IReadModelStore store, SimulatedMagicLinkV1Inventory inventory)
        => new(coordinator, store, new SimulatedMagicLinkV1Authority(inventory), inventory);

    [Theory]
    [InlineData("receipt", false)]
    [InlineData("authority", false)]
    [InlineData("root", false)]
    [InlineData("receipt", true)]
    [InlineData("authority", true)]
    [InlineData("root", true)]
    public async Task FinalReplayAndSuccessfulSealChangesAreCheckedBeforePromotion(string changed, bool afterSeal)
    {
        var store = new InMemoryReadModelStore();
        var faultStore = new MagicLinkV1FaultStore(store);
        SharedProjectionEpochCoordinator coordinator = await SetupAsync(faultStore, faultStore);
        var inventory = new SimulatedMagicLinkV1Inventory(Rows());
        var authority = new SimulatedMagicLinkV1Authority(inventory);
        int fired = 0;
        void Change()
        {
            fired++;
            if (changed == "receipt") inventory.Valid = false;
            if (changed == "authority") authority.Permitted = false;
            if (changed == "root") store.SeedRaw(Scope().StoreName, MagicLinkTokenHashCapabilityIndexProjection.StateKey,
                Legacy(Row("changed", "owner", "changed")));
        }
        if (afterSeal) faultStore.AfterSeal = Change;
        else inventory.BeforeEnumeration = enumeration => { if (enumeration == 4) Change(); };
        await Should.ThrowAsync<Exception>(() => new MagicLinkTokenHashCapabilityIndexV1Migration(coordinator,
            faultStore, authority, inventory).MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token));
        fired.ShouldBe(1);
        if (afterSeal) faultStore.SealCallbacks.ShouldBe(1);
        (await coordinator.GetStatusAsync(Scope(), Token)).ActiveGeneration.ShouldBe(0);
        if (changed == "authority")
        {
            (await coordinator.GetStatusAsync(Scope(), Token)).IsBuilding.ShouldBeTrue();
            authority.Permitted = true;
            inventory.Valid = false;
            await Should.ThrowAsync<InvalidOperationException>(() => new MagicLinkTokenHashCapabilityIndexV1Migration(coordinator,
                faultStore, authority, inventory).MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token));
        }
        await AssertLegacyOnlyAsync(coordinator, 0);
    }

    [Theory]
    [InlineData("begin-after")]
    [InlineData("checkpoint-zero-before")]
    public async Task BeginWithoutCheckpointZeroKeepsFullReceiptOwnershipForRevokedLookupRecovery(string fault)
    {
        var store = new InMemoryReadModelStore();
        var faultStore = new MagicLinkV1FaultStore(store);
        SharedProjectionEpochCoordinator coordinator = await SetupAsync(faultStore, faultStore);
        var inventory = new SimulatedMagicLinkV1Inventory(Rows());
        MagicLinkV1InventoryReceipt original = inventory.Receipt;
        faultStore.Fault = fault;
        await Should.ThrowAsync<IOException>(() => Migration(coordinator, faultStore, inventory).MigrateAsync("explicit-v1", original.ReceiptId, Token));
        faultStore.Fired.ShouldBe(1);
        (await coordinator.GetStatusAsync(Scope(), Token)).IsBuilding.ShouldBeTrue();
        (await store.GetAsync<MagicLinkV1MigrationCheckpoint>(Scope().StoreName,
            PhysicalKey(1, MagicLinkTokenHashCapabilityIndexV1Migration.CheckpointKey(1, 0)), Token)).Value.ShouldBeNull();
        coordinator = new(faultStore, faultStore);
        var migration = Migration(coordinator, faultStore, inventory);
        await Should.ThrowAsync<InvalidOperationException>(() => migration.MigrateAsync("different-op", original.ReceiptId, Token));
        await Should.ThrowAsync<InvalidOperationException>(() => migration.MigrateAsync("explicit-v1", "different-receipt", Token));
        inventory.Receipt = original with { QuiescenceReceiptId = "" };
        await Should.ThrowAsync<InvalidOperationException>(() => migration.MigrateAsync("explicit-v1", original.ReceiptId, Token));
        (await coordinator.GetStatusAsync(Scope(), Token)).IsBuilding.ShouldBeTrue();
        inventory.Receipt = original;
        inventory.Available = false;
        await Should.ThrowAsync<InvalidOperationException>(() => migration.MigrateAsync("explicit-v1", original.ReceiptId, Token));
        await AssertLegacyOnlyAsync(coordinator, 0);
        inventory.Available = true;
        await migration.MigrateAsync("fresh-operation", original.ReceiptId, Token);
        await AssertCandidatesAsync(coordinator, Rows());
    }

    [Fact]
    public async Task DeniedMatchingRetryPreservesPersistedPrivateOperationThenRecoversWithAuthority()
    {
        var store = new InMemoryReadModelStore();
        var faultStore = new MagicLinkV1FaultStore(store);
        SharedProjectionEpochCoordinator coordinator = await SetupAsync(faultStore, faultStore);
        var inventory = new SimulatedMagicLinkV1Inventory(Rows());
        var authority = new SimulatedMagicLinkV1Authority(inventory);
        var migration = new MagicLinkTokenHashCapabilityIndexV1Migration(coordinator, faultStore, authority, inventory);
        faultStore.Fault = "checkpoint-after";
        await Should.ThrowAsync<IOException>(() => migration.MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token));
        SharedProjectionEpochStatus before = await coordinator.GetStatusAsync(Scope(), Token);
        ReadModelEntry<MagicLinkV1MigrationCheckpoint> checkpoint = await store.GetAsync<MagicLinkV1MigrationCheckpoint>(Scope().StoreName,
            PhysicalKey(before.Epoch, MagicLinkTokenHashCapabilityIndexV1Migration.CheckpointKey(before.Epoch, 1)), Token);
        authority.Permitted = false;
        await Should.ThrowAsync<UnauthorizedAccessException>(() => migration.MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token));
        await Should.ThrowAsync<UnauthorizedAccessException>(() => new MagicLinkTokenHashCapabilityIndexV1Migration(coordinator,
            faultStore, new DenyAllMagicLinkIndexMigrationAuthority(), inventory).MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token));
        AssertStatusEqual(await coordinator.GetStatusAsync(Scope(), Token), before);
        (await store.GetAsync<MagicLinkV1MigrationCheckpoint>(Scope().StoreName,
            PhysicalKey(before.Epoch, MagicLinkTokenHashCapabilityIndexV1Migration.CheckpointKey(before.Epoch, 1)), Token)).ETag.ShouldBe(checkpoint.ETag);
        (await store.GetAsync<MagicLinkV1MigrationAbortWitness>(Scope().StoreName,
            MagicLinkTokenHashCapabilityIndexV1Migration.AbortWitnessKey(before.Epoch), Token)).Value.ShouldBeNull();
        authority.Permitted = true;
        await migration.MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token);
        await AssertCandidatesAsync(coordinator, Rows());
    }

    [Fact]
    public async Task MalformedChangedReceiptCannotAbortReservedOriginalReceipt()
    {
        var store = new InMemoryReadModelStore();
        var faultStore = new MagicLinkV1FaultStore(store);
        SharedProjectionEpochCoordinator coordinator = await SetupAsync(faultStore, faultStore);
        var inventory = new SimulatedMagicLinkV1Inventory(Rows());
        MagicLinkV1InventoryReceipt original = inventory.Receipt;
        faultStore.Fault = "checkpoint-after";
        await Should.ThrowAsync<IOException>(() => Migration(coordinator, faultStore, inventory).MigrateAsync("explicit-v1", original.ReceiptId, Token));
        SharedProjectionEpochStatus before = await coordinator.GetStatusAsync(Scope(), Token);
        inventory.Receipt = original with { ExpectedContributionDigest = "", InstallationCoverageReceiptId = "" };
        await Should.ThrowAsync<InvalidOperationException>(() => Migration(coordinator, faultStore, inventory).MigrateAsync("explicit-v1", original.ReceiptId, Token));
        AssertStatusEqual(await coordinator.GetStatusAsync(Scope(), Token), before);
        inventory.Receipt = original;
        await Migration(new(faultStore, faultStore), faultStore, inventory).MigrateAsync("explicit-v1", original.ReceiptId, Token);
        await AssertCandidatesAsync(coordinator, Rows());
    }

    [Fact]
    public async Task CorruptedCheckpointRootCannotDiscardCompletedTenantMembership()
    {
        var store = new InMemoryReadModelStore();
        var faultStore = new MagicLinkV1FaultStore(store);
        SharedProjectionEpochCoordinator coordinator = await SetupAsync(faultStore, faultStore);
        var inventory = new SimulatedMagicLinkV1Inventory(Rows());
        faultStore.Fault = "checkpoint-advance";
        await Should.ThrowAsync<IOException>(() => Migration(coordinator, faultStore, inventory).MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token));
        string key = PhysicalKey(1, MagicLinkTokenHashCapabilityIndexV1Migration.CheckpointKey(1, 1));
        MagicLinkV1MigrationCheckpoint checkpoint = (await store.GetAsync<MagicLinkV1MigrationCheckpoint>(Scope().StoreName, key, Token)).Value.ShouldNotBeNull();
        store.SeedRaw(Scope().StoreName, key, checkpoint with { Root = new(7, checkpoint.Root.NextNodeId, null, null) });
        await Should.ThrowAsync<InvalidOperationException>(() => Migration(new(faultStore, faultStore), faultStore, inventory)
            .MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token));
        await AssertLegacyOnlyAsync(coordinator, 0);
    }

    [Fact]
    public async Task SelectedCommitMarkerRepairNeedsOperatorAuthorityAndOriginalIdentityWithoutCurrentCertification()
    {
        var store = new InMemoryReadModelStore();
        var faultStore = new MagicLinkV1FaultStore(store);
        SharedProjectionEpochCoordinator coordinator = await SetupAsync(faultStore, faultStore);
        var inventory = new SimulatedMagicLinkV1Inventory(Rows());
        MagicLinkV1InventoryReceipt original = inventory.Receipt;
        var authority = new SimulatedMagicLinkV1Authority(inventory);
        faultStore.Fault = "commit-marker";
        await Should.ThrowAsync<IOException>(() => new MagicLinkTokenHashCapabilityIndexV1Migration(coordinator,
            faultStore, authority, inventory).MigrateAsync("explicit-v1", original.ReceiptId, Token));
        faultStore.Fired.ShouldBe(1);
        string marker = "shared-epoch:" + Scope().ComputeHash() + ":cow-committed:1";
        inventory.Valid = false;
        inventory.Available = false;
        authority.Permitted = false;
        var migration = new MagicLinkTokenHashCapabilityIndexV1Migration(new(faultStore, faultStore), faultStore, authority, inventory);
        await Should.ThrowAsync<UnauthorizedAccessException>(() => migration.MigrateAsync("explicit-v1", original.ReceiptId, Token));
        (await store.TryReadEtagAsync(Scope().StoreName, marker, Token)).Present.ShouldBeFalse();
        authority.Permitted = true;
        await Should.ThrowAsync<InvalidOperationException>(() => migration.MigrateAsync("explicit-v1", "different-receipt", Token));
        inventory.Available = true;
        inventory.Receipt = original with { SourceCutRevision = "changed" };
        await Should.ThrowAsync<InvalidOperationException>(() => migration.MigrateAsync("explicit-v1", original.ReceiptId, Token));
        (await store.TryReadEtagAsync(Scope().StoreName, marker, Token)).Present.ShouldBeFalse();
        inventory.Receipt = original;
        inventory.Available = false;
        await migration.MigrateAsync("explicit-v1", original.ReceiptId, Token);
        (await store.TryReadEtagAsync(Scope().StoreName, marker, Token)).Present.ShouldBeTrue();
        (await coordinator.GetStatusAsync(Scope(), Token)).Epoch.ShouldBe(1);
        await AssertCandidatesAsync(coordinator, Rows());
    }

    [Fact]
    public async Task TenThousandMaximumWidthContributionsPersistWithinMeasuredByteBounds()
    {
        var store = new InMemoryReadModelStore();
        var observed = new MagicLinkV1FaultStore(store);
        SharedProjectionEpochCoordinator coordinator = await SetupAsync(observed, observed);
        string tenant = "t" + new string('\u0001', 255);
        string hash = new('A', 128);
        MagicLinkTokenHashCapabilityIndexDirectoryEntry[] rows = Enumerable.Range(0, 10_000)
            .Select(index => Row(tenant, "o" + new string('\u0002', 114) + index.ToString("D5"), hash)).ToArray();
        var inventory = new SimulatedMagicLinkV1Inventory(rows);
        await Migration(coordinator, observed, inventory).MigrateAsync("maximum-width", inventory.Receipt.ReceiptId, Token);
        observed.MaximumPhysicalMutations.ShouldBeInRange(1, 8);
        observed.MaximumPhysicalBytes.ShouldBeLessThan(256 * 1024);
        observed.MaximumRosterBytes.ShouldBeGreaterThan(64 * 1024);
        observed.ByteSplitPages.ShouldBeGreaterThan(0);
        MagicLinkTokenHashCapabilityIndexTreeRoot root = (await coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(Scope(),
            MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, Token)).Value.ShouldNotBeNull();
        var reader = new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, Scope());
        (await reader.FindCandidatesAsync(root.TokenRoot, hash, Token)).Count.ShouldBe(2);
        foreach (MagicLinkTokenHashCapabilityIndexDirectoryEntry row in rows)
        {
            (await reader.ContainsExactRowAsync(root.TokenRoot, row, Token)).ShouldBeTrue();
            (await reader.FindOwnerAsync(root.OwnerRoot, tenant, row.Candidate.CapabilityId.Value, Token)).ShouldBe(row);
        }
        var roster = (await reader.ReadRosterAsync(root.TenantRoot, tenant, Token)).ShouldNotBeNull();
        roster.Count.ShouldBe(10_000);
        new HashSet<MagicLinkTokenHashCapabilityIndexDirectoryEntry>(roster).SetEquals(rows).ShouldBeTrue();
        System.Console.WriteLine($"Measured migration: max physical page bytes={observed.MaximumPhysicalBytes}; max roster bytes={observed.MaximumRosterBytes}; byte-split pages={observed.ByteSplitPages}; owners={roster.Count}.");
    }

    [Fact]
    public async Task ExistingSdkOwnerCheckpointSurvivesMigrationAndMissingContinuityStaysDeliveryGap()
    {
        var store = new InMemoryReadModelStore();
        SharedProjectionEpochCoordinator coordinator = await SetupAsync(store, store);
        SharedProjectionLease lease = await coordinator.RefreshLeaseAsync(Scope(), MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId, Token);
        const string source = "tenant-a:timesheets:owner-a";
        (await coordinator.JournalAsync(Scope(), lease, new(source, 1, [1], [], OwnerSequenceNumber: 1), Token)).ShouldBe(SharedProjectionJournalResult.Journaled);
        (await coordinator.CatchUpAsync(Scope(), (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>(
            [ReadModelBatchOperation.Write("fixture-owner-continuity", new MagicLinkTokenHashCapabilityIndexTreeFormat(0), ReadModelBatchConcurrency.LastWrite)]), Token)).ShouldBe(1);
        SharedProjectionCheckpoint checkpoint = (await coordinator.GetCheckpointAsync(Scope(), source, Token)).ShouldNotBeNull();
        (await coordinator.JournalAsync(Scope(), lease, new("missing-owner", 2, [2], [], OwnerSequenceNumber: 2), Token)).ShouldBe(SharedProjectionJournalResult.DeliveryGap);
        var inventory = new SimulatedMagicLinkV1Inventory(Rows());
        await Migration(coordinator, store, inventory).MigrateAsync("continuity", inventory.Receipt.ReceiptId, Token);
        (await coordinator.GetCheckpointAsync(Scope(), source, Token)).ShouldBe(checkpoint);
        lease = await coordinator.RefreshLeaseAsync(Scope(), MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId, Token);
        (await coordinator.JournalAsync(Scope(), lease, new("missing-owner", 2, [2], [], OwnerSequenceNumber: 2), Token)).ShouldBe(SharedProjectionJournalResult.DeliveryGap);
        (await coordinator.JournalAsync(Scope(), lease, new(source, 2, [2], [], OwnerSequenceNumber: 2), Token)).ShouldBe(SharedProjectionJournalResult.Journaled);
        (await coordinator.GetStatusAsync(Scope(), Token)).PendingDeliveryCount.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterveningSuccessfulMigrationIsRetainedAcrossPreflightAndLateReservationRaces(bool afterOwnership)
    {
        var store = new InMemoryReadModelStore();
        var faultStore = new MagicLinkV1FaultStore(store);
        SharedProjectionEpochCoordinator coordinator = await SetupAsync(faultStore, faultStore);
        var olderInventory = new SimulatedMagicLinkV1Inventory(Rows());
        var interveningInventory = new SimulatedMagicLinkV1Inventory(Rows());
        int fired = 0;
        async Task InterveneAsync()
        {
            fired++;
            await Migration(coordinator, faultStore, interveningInventory).MigrateAsync("intervening-operation",
                interveningInventory.Receipt.ReceiptId, Token);
        }
        if (afterOwnership) faultStore.AfterOwnershipPersist = InterveneAsync;
        else olderInventory.BeforeEnumeration = enumeration => { if (enumeration == 1) InterveneAsync().GetAwaiter().GetResult(); };
        await Should.ThrowAsync<InvalidOperationException>(() => Migration(coordinator, faultStore, olderInventory)
            .MigrateAsync("explicit-v1", olderInventory.Receipt.ReceiptId, Token));
        fired.ShouldBe(1);
        if (afterOwnership) faultStore.OwnershipCallbacks.ShouldBe(1);
        SharedProjectionEpochStatus final = await coordinator.GetStatusAsync(Scope(), Token);
        final.ActiveGeneration.ShouldBe(1);
        final.Epoch.ShouldBe(afterOwnership ? 2 : 1);
        final.IsBuilding.ShouldBeFalse();
        final.IsAborting.ShouldBeFalse();
        await AssertCandidatesAsync(coordinator, Rows());
        MagicLinkV1MigrationCheckpoint completed = (await coordinator.ReadGenerationAsync<MagicLinkV1MigrationCheckpoint>(Scope(),
            1, MagicLinkTokenHashCapabilityIndexV1Migration.CompletionKey, Token)).Value.ShouldNotBeNull();
        completed.OperationId.ShouldBe("intervening-operation");
        if (afterOwnership)
        {
            await Should.ThrowAsync<InvalidOperationException>(() => Migration(coordinator, faultStore, olderInventory)
                .MigrateAsync("explicit-v1", olderInventory.Receipt.ReceiptId, Token));
            (await coordinator.GetStatusAsync(Scope(), Token)).Epoch.ShouldBe(2);
            (await coordinator.GetStatusAsync(Scope(), Token)).ActiveGeneration.ShouldBe(1);
        }
    }

    [Fact]
    public async Task ConcurrentAbortDuringUnboundOwnershipRecoveryAbandonsOnlyActuallyAcquiredEpoch()
    {
        var store = new InMemoryReadModelStore();
        var faultStore = new MagicLinkV1FaultStore(store);
        SharedProjectionEpochCoordinator coordinator = await SetupAsync(faultStore, faultStore);
        var inventory = new SimulatedMagicLinkV1Inventory(Rows());
        faultStore.Fault = "begin-after";
        await Should.ThrowAsync<IOException>(() => Migration(coordinator, faultStore, inventory)
            .MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token));
        int fired = 0;
        inventory.BeforeReceiptLookup = () =>
        {
            fired++;
            inventory.Valid = false;
            Should.Throw<InvalidOperationException>(() => Migration(new(faultStore, faultStore), faultStore, inventory)
                .MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token).GetAwaiter().GetResult());
            inventory.Valid = true;
        };
        await Should.ThrowAsync<InvalidOperationException>(() => Migration(new(faultStore, faultStore), faultStore, inventory)
            .MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token));
        fired.ShouldBe(1);
        await AssertLegacyOnlyAsync(coordinator, 0);
        (await coordinator.GetStatusAsync(Scope(), Token)).Epoch.ShouldBe(2);
        await Should.ThrowAsync<InvalidOperationException>(() => Migration(coordinator, faultStore, inventory)
            .MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token));
        (await coordinator.GetStatusAsync(Scope(), Token)).Epoch.ShouldBe(2);
        await Migration(coordinator, faultStore, inventory).MigrateAsync("fresh-after-raced-abort", inventory.Receipt.ReceiptId, Token);
        await AssertCandidatesAsync(coordinator, Rows());
    }

    [Theory]
    [InlineData("missing-format")]
    [InlineData("mixed-root")]
    [InlineData("empty-root")]
    public async Task SelectedCompletionCannotRepairMarkerForMissingOrInvalidV7Layout(string invalid)
    {
        var store = new InMemoryReadModelStore();
        var faultStore = new MagicLinkV1FaultStore(store);
        SharedProjectionEpochCoordinator coordinator = await SetupAsync(faultStore, faultStore);
        var inventory = new SimulatedMagicLinkV1Inventory(Rows());
        faultStore.Fault = "commit-marker";
        await Should.ThrowAsync<IOException>(() => Migration(coordinator, faultStore, inventory)
            .MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token));
        string formatKey = PhysicalKey(1, MagicLinkTokenHashCapabilityIndexTreeReader.FormatKey);
        string rootKey = PhysicalKey(1, MagicLinkTokenHashCapabilityIndexTreeReader.RootKey);
        MagicLinkTokenHashCapabilityIndexTreeRoot original = (await store.GetAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(Scope().StoreName, rootKey, Token)).Value.ShouldNotBeNull();
        if (invalid == "missing-format")
        {
            var etag = await store.TryReadEtagAsync(Scope().StoreName, formatKey, Token);
            (await store.TryEraseAsync(Scope().StoreName, formatKey, etag.Etag, Token)).ShouldBeTrue();
        }
        else store.SeedRaw(Scope().StoreName, rootKey, invalid == "mixed-root" ? original with { StorageVersion = 6 }
            : new MagicLinkTokenHashCapabilityIndexTreeRoot(7, original.NextNodeId, null, null));
        inventory.Available = false;
        inventory.Valid = false;
        await Should.ThrowAsync<Exception>(() => Migration(new(faultStore, faultStore), faultStore, inventory)
            .MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token));
        (await store.TryReadEtagAsync(Scope().StoreName, "shared-epoch:" + Scope().ComputeHash() + ":cow-committed:1", Token)).Present.ShouldBeFalse();
        store.SeedRaw(Scope().StoreName, formatKey, new MagicLinkTokenHashCapabilityIndexTreeFormat(7));
        store.SeedRaw(Scope().StoreName, rootKey, original);
        await Migration(new(faultStore, faultStore), faultStore, inventory).MigrateAsync("explicit-v1", inventory.Receipt.ReceiptId, Token);
        await AssertCandidatesAsync(coordinator, Rows());
    }

    private static async Task<SharedProjectionEpochCoordinator> SetupAsync(IReadModelStore store,
        IReadModelBatchStore batches, bool selected = false)
    {
        var coordinator = new SharedProjectionEpochCoordinator(store, batches);
        _ = await coordinator.RegisterWriterAsync(Scope(), MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId, Token);
        // Unique-only legacy deliberately omits both colliding owners and terminal capability candidates.
        MagicLinkTokenHashCapabilityIndexReadModel legacy = Legacy(Row("tenant-b", "owner-b2", "unique"));
        await store.SaveAsync(Scope().StoreName, MagicLinkTokenHashCapabilityIndexProjection.StateKey, legacy, Token);
        if (selected)
        {
            _ = await coordinator.BeginAsync(Scope(), "selected-legacy", "fixture-source", new Dictionary<string, long>(), Token);
            _ = await coordinator.StageAsync(Scope(), "selected-legacy",
                [ReadModelBatchOperation.Write(MagicLinkTokenHashCapabilityIndexProjection.StateKey, legacy, ReadModelBatchConcurrency.LastWrite)], Token);
            await coordinator.CommitAsync(Scope(), "selected-legacy", Token);
        }
        return coordinator;
    }

    private static async Task AssertLegacyOnlyAsync(SharedProjectionEpochCoordinator coordinator, long source)
    {
        SharedProjectionEpochStatus status = await coordinator.GetStatusAsync(Scope(), Token);
        status.ActiveGeneration.ShouldBe(source);
        status.IsBuilding.ShouldBeFalse();
        status.IsAborting.ShouldBeFalse();
        (await coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(Scope(),
            MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, Token)).Value.ShouldBeNull();
    }

    private static async Task AssertCandidatesAsync(SharedProjectionEpochCoordinator coordinator,
        IReadOnlyList<MagicLinkTokenHashCapabilityIndexDirectoryEntry> rows)
    {
        MagicLinkTokenHashCapabilityIndexTreeRoot root = (await coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(Scope(),
            MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, Token)).Value.ShouldNotBeNull();
        root.StorageVersion.ShouldBe(7);
        root.OwnerRoot.ShouldNotBeNull();
        var reader = new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, Scope());
        (await reader.FindCandidatesAsync(root.TokenRoot, "shared", Token)).Count.ShouldBe(2);
        foreach (MagicLinkTokenHashCapabilityIndexDirectoryEntry row in rows)
        {
            (await reader.ContainsExactRowAsync(root.TokenRoot, row, Token)).ShouldBeTrue();
            (await reader.FindOwnerAsync(root.OwnerRoot, row.Candidate.Tenant.TenantId,
                row.Candidate.CapabilityId.Value, Token)).ShouldBe(row);
        }
        foreach (IGrouping<string, MagicLinkTokenHashCapabilityIndexDirectoryEntry> tenant in rows.GroupBy(row => row.Candidate.Tenant.TenantId))
            (await reader.ReadRosterAsync(root.TenantRoot, tenant.Key, Token)).ShouldNotBeNull().Count.ShouldBe(tenant.Count());
    }

    private static MagicLinkTokenHashCapabilityIndexReadModel Legacy(MagicLinkTokenHashCapabilityIndexDirectoryEntry row)
        => new(new Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry> { [row.TokenHash] = row.Candidate });

    private static MagicLinkTokenHashCapabilityIndexDirectoryEntry[] Rows() =>
    [Row("tenant-a", "owner-a", "shared"), Row("tenant-b", "owner-b1", "shared"),
        Row("tenant-b", "owner-b2", "unique"), Row("tenant-c", "terminal-owner", "terminal")];

    private static MagicLinkTokenHashCapabilityIndexDirectoryEntry Row(string tenant, string owner, string hash)
        => new(hash, new(new TenantReference(tenant), new MagicLinkCapabilityId(owner)));

    private static SharedProjectionScope Scope() => MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();

    private static string PhysicalKey(long generation, string logicalKey)
        => "shared-generation:" + Scope().ComputeHash() + ":" + generation + ":"
            + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(logicalKey)));

    private static void AssertStatusEqual(SharedProjectionEpochStatus actual, SharedProjectionEpochStatus expected)
    {
        actual.Epoch.ShouldBe(expected.Epoch);
        actual.ActiveGeneration.ShouldBe(expected.ActiveGeneration);
        actual.OperationId.ShouldBe(expected.OperationId);
        actual.StageFingerprint.ShouldBe(expected.StageFingerprint);
        actual.IsBuilding.ShouldBe(expected.IsBuilding);
        actual.IsAborting.ShouldBe(expected.IsAborting);
        actual.PendingDeliveryCount.ShouldBe(expected.PendingDeliveryCount);
        actual.IsOffboarded.ShouldBe(expected.IsOffboarded);
        actual.OperationTenantId.ShouldBe(expected.OperationTenantId);
        actual.IsCapacityLimited.ShouldBe(expected.IsCapacityLimited);
    }
}
