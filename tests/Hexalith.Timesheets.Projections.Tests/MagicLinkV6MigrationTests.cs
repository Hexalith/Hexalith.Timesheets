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
        string kind = invalidJsonShape ? damage[..^5] : damage;
        if (kind.StartsWith("wrapped-", StringComparison.Ordinal)) kind = kind[8..];
        string logicalKey = kind switch
        {
            "format" => MagicLinkTokenHashCapabilityIndexTreeReader.FormatKey,
            "root" => MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
            _ => MagicLinkTokenHashCapabilityIndexProjection.StateKey
        };
        string key = kind == "direct-single" ? logicalKey : PhysicalKey(scope, generation, logicalKey);
        object value = invalidJsonShape ? new { StorageVersion = "invalid-json-shape" } : kind switch
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
