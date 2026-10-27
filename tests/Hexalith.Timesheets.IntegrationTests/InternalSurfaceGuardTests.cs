using System.Net;
using System.Security.Claims;

using Shouldly;

using Hexalith.Timesheets.Runtime;
using Hexalith.Timesheets.Endpoints.MagicLinks;
using Hexalith.Timesheets.Projections.MagicLinks;
using Hexalith.Timesheets.Projections.TimeEntries;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Hexalith.Timesheets.IntegrationTests;

/// <summary>
/// Proves the domain-service write surface is not published on the public ingress.
/// </summary>
public sealed class InternalSurfaceGuardTests
{
    public static TheoryData<string> ProtectedRoutes()
    {
        TheoryData<string> data = [];
        foreach (string route in new[]
        {
            "/process",
            "/replay-state",
            "/query",
            "/project",
            "/project/v2",
            "/project/v2/reconcile",
            "/project/rebuild/v1",
            "/project/rebuild/shared/v1",
            "/admin/operational-index-metadata",
            "/admin/timesheets/magic-link-index/migrate-v6",
            "/admin/timesheets/magic-link-index/migrate-v1",
            "/PROJECT/v2",
            "/Process",
            "/Admin/operational-index-metadata"
        })
        {
            data.Add(route);
        }

        return data;
    }

    [Fact]
    public void NormallyComposedHostRegistersBothMaterializedProjectionRoutes()
    {
        using GuardedFactory factory = new(allowOnAnyPort: false);
        using HttpClient client = factory.CreateClient();
        using IServiceScope scope = factory.Services.CreateScope();
        string[] routes = scope.ServiceProvider.GetServices<IAsyncDomainProjectionHandler>()
            .Where(static handler => handler.Domain == "timesheets")
            .Select(static handler => handler.ProjectionType)
            .ToArray();

        routes.ShouldContain(TimeEntryEvidenceProjection.ProjectionName);
        routes.ShouldContain(MagicLinkConfirmationCapabilityProjection.ProjectionName);
    }

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task DomainServiceRoutesAreRefusedWhenNoInternalPortIsConfigured(string route)
    {
        // Fail-closed is the whole point: a deployment that never configures the split must lose
        // projection delivery, not publish an unauthenticated write surface on its public ingress.
        using GuardedFactory factory = new(allowOnAnyPort: false);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsync(
            route,
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        // 404 rather than 403: a refusal that confirmed the route exists would hand an external
        // caller a map of the write surface.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("migrate-v6")]
    [InlineData("migrate-v1")]
    public async Task InternalMigrationRouteIsDefaultDeniedWithoutACertifiedOperatorAuthority(string route)
    {
        using GuardedFactory factory = new(allowOnAnyPort: true, authenticatedOperator: true,
            callerAuthorizer: new InternalMagicLinkMigrationCallerAuthorizer("migration", "caller-only"));
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.PostAsync(
            "/admin/timesheets/magic-link-index/" + route,
            new StringContent("{\"operationId\":\"migration\",\"certifiedInventoryFingerprint\":\"caller-only\",\"receiptId\":\"caller-only\"}",
                System.Text.Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("", "certified")]
    [InlineData("migration", "")]
    public async Task V6MigrationRouteOpaquelyRefusesBlankIdentifiers(string operationId, string fingerprint)
    {
        using GuardedFactory factory = new(allowOnAnyPort: true,
            migrationAuthority: new CountingMigration(), authenticatedOperator: true,
            callerAuthorizer: new InternalMagicLinkMigrationCallerAuthorizer(operationId, fingerprint));
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.PostAsync(
            "/admin/timesheets/magic-link-index/migrate-v6",
            new StringContent($"{{\"operationId\":\"{operationId}\",\"certifiedInventoryFingerprint\":\"{fingerprint}\"}}",
                System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("migrate-v6")]
    [InlineData("migrate-v1")]
    public async Task InternalMigrationRouteRequiresAnAuthenticatedOperator(string route)
    {
        var authority = new CountingMigration();
        var inventory = new InternalMagicLinkV1Inventory([]);
        var callerAuthorizer = new InternalMagicLinkMigrationCallerAuthorizer("migration", "server-receipt");
        using GuardedFactory factory = new(allowOnAnyPort: true,
            migrationAuthority: authority, inventory: inventory, callerAuthorizer: callerAuthorizer);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.PostAsync(
            "/admin/timesheets/magic-link-index/" + route,
            new StringContent("{\"operationId\":\"migration\",\"certifiedInventoryFingerprint\":\"caller-only\",\"receiptId\":\"server-receipt\"}",
                System.Text.Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);
        response.StatusCode.ShouldNotBe(HttpStatusCode.NoContent);
        authority.Calls.ShouldBe(0);
        inventory.Calls.ShouldBe(0);
        callerAuthorizer.Calls.ShouldBe(0);
    }

    /// <summary>Unbound callers include missing server bindings and replay of either operation or evidence.</summary>
    public static TheoryData<string, bool, string, string, string> UnboundMigrationCallers()
    {
        TheoryData<string, bool, string, string, string> data = [];
        foreach (string route in new[] { "migrate-v1", "migrate-v6" })
        {
            data.Add(route, false, "certified-operator", "migration", "server-receipt");
            data.Add(route, true, "another-operator", "migration", "server-receipt");
            data.Add(route, true, "certified-operator", "another-operation", "server-receipt");
            data.Add(route, true, "certified-operator", "migration", "another-receipt");
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(UnboundMigrationCallers))]
    public async Task UnboundMigrationCallerIsOpaquelyRefusedBeforeAnyMigrationIo(
        string route, bool installAuthorizer, string callerId, string operationId, string evidenceId)
    {
        var raw = new InMemoryReadModelStore();
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        await SeedMigrationHistoryAndPendingJournalAsync(raw, scope);
        var store = new InternalMagicLinkMigrationCountingStore(raw);
        var authority = new CountingMigration();
        var inventory = new InternalMagicLinkV1Inventory([]);
        var callerAuthorizer = new InternalMagicLinkMigrationCallerAuthorizer("migration", "server-receipt");
        using GuardedFactory factory = new(true, store, authority, authenticatedOperator: true,
            inventory: inventory, callerAuthorizer: installAuthorizer ? callerAuthorizer : null,
            operatorId: callerId);
        using HttpClient client = factory.CreateClient();
        store.ResetCalls();
        IReadOnlyDictionary<string, byte[]> before = raw.SnapshotRawEntries(scope.StoreName);
        using HttpResponseMessage response = await client.PostAsync(
            "/admin/timesheets/magic-link-index/" + route,
            new StringContent(System.Text.Json.JsonSerializer.Serialize(new
            {
                operationId,
                receiptId = evidenceId,
                certifiedInventoryFingerprint = evidenceId
            }), System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
        callerAuthorizer.Calls.ShouldBe(installAuthorizer ? 1 : 0);
        authority.Calls.ShouldBe(0);
        inventory.Calls.ShouldBe(0);
        store.Calls.ShouldBe(0);
        AssertRawStateUnchanged(before, raw.SnapshotRawEntries(scope.StoreName));
    }

    [Theory]
    [InlineData("migrate-v1")]
    [InlineData("migrate-v6")]
    public async Task BoundMigrationCallerStillRequiresIndependentCertification(string route)
    {
        var raw = new InMemoryReadModelStore();
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        await SeedMigrationHistoryAndPendingJournalAsync(raw, scope);
        var store = new InternalMagicLinkMigrationCountingStore(raw);
        var inventory = new InternalMagicLinkV1Inventory([]);
        var authority = new CountingMigration { Allowed = false };
        var callerAuthorizer = new InternalMagicLinkMigrationCallerAuthorizer("migration", "server-receipt");
        using GuardedFactory factory = new(true, store, authority, authenticatedOperator: true,
            inventory: inventory, callerAuthorizer: callerAuthorizer);
        using HttpClient client = factory.CreateClient();
        IReadOnlyDictionary<string, byte[]> before = raw.SnapshotRawEntries(scope.StoreName);
        using HttpResponseMessage response = await client.PostAsync(
            "/admin/timesheets/magic-link-index/" + route,
            new StringContent("{\"operationId\":\"migration\",\"receiptId\":\"server-receipt\",\"certifiedInventoryFingerprint\":\"server-receipt\"}",
                System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
        callerAuthorizer.Calls.ShouldBe(1);
        authority.Calls.ShouldBe(1);
        inventory.Calls.ShouldBe(route == "migrate-v1" ? 1 : 0);
        AssertRawStateUnchanged(before, raw.SnapshotRawEntries(scope.StoreName));
    }

    [Theory]
    [InlineData("migrate-v1", true)]
    [InlineData("migrate-v1", false)]
    [InlineData("migrate-v6", true)]
    [InlineData("migrate-v6", false)]
    public async Task CallerAuthorityOutageIsOpaqueBeforeAnyMigrationIo(string route, bool timeout)
    {
        var raw = new InMemoryReadModelStore();
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        await SeedMigrationHistoryAndPendingJournalAsync(raw, scope);
        var store = new InternalMagicLinkMigrationCountingStore(raw);
        var authority = new CountingMigration();
        var inventory = new InternalMagicLinkV1Inventory([]);
        var callerAuthorizer = new InternalMagicLinkMigrationCallerAuthorizer("migration", "server-receipt")
        {
            Failure = timeout ? new TimeoutException("private-authority-detail")
                : new HttpRequestException("private-authority-detail")
        };
        using GuardedFactory factory = new(true, store, authority, authenticatedOperator: true,
            inventory: inventory, callerAuthorizer: callerAuthorizer);
        using HttpClient client = factory.CreateClient();
        store.ResetCalls();
        IReadOnlyDictionary<string, byte[]> before = raw.SnapshotRawEntries(scope.StoreName);
        using HttpResponseMessage response = await client.PostAsync(
            "/admin/timesheets/magic-link-index/" + route,
            new StringContent("{\"operationId\":\"migration\",\"receiptId\":\"server-receipt\",\"certifiedInventoryFingerprint\":\"server-receipt\"}",
                System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
        callerAuthorizer.Calls.ShouldBe(1);
        authority.Calls.ShouldBe(0);
        inventory.Calls.ShouldBe(0);
        store.Calls.ShouldBe(0);
        AssertRawStateUnchanged(before, raw.SnapshotRawEntries(scope.StoreName));
    }

    [Theory]
    [InlineData("migrate-v1")]
    [InlineData("migrate-v6")]
    public async Task PermissionForOnlyTheOtherMigrationVersionIsRefusedBeforeAnyMigrationIo(string route)
    {
        var raw = new InMemoryReadModelStore();
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        await SeedMigrationHistoryAndPendingJournalAsync(raw, scope);
        var store = new InternalMagicLinkMigrationCountingStore(raw);
        var authority = new CountingMigration();
        var inventory = new InternalMagicLinkV1Inventory([]);
        var callerAuthorizer = new InternalMagicLinkMigrationCallerAuthorizer("migration", "server-receipt",
            allowV1: route != "migrate-v1", allowV6: route != "migrate-v6");
        using GuardedFactory factory = new(true, store, authority, authenticatedOperator: true,
            inventory: inventory, callerAuthorizer: callerAuthorizer);
        using HttpClient client = factory.CreateClient();
        store.ResetCalls();
        IReadOnlyDictionary<string, byte[]> before = raw.SnapshotRawEntries(scope.StoreName);
        using HttpResponseMessage response = await client.PostAsync(
            "/admin/timesheets/magic-link-index/" + route,
            new StringContent("{\"operationId\":\"migration\",\"receiptId\":\"server-receipt\",\"certifiedInventoryFingerprint\":\"server-receipt\"}",
                System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
        callerAuthorizer.Calls.ShouldBe(1);
        authority.Calls.ShouldBe(0);
        inventory.Calls.ShouldBe(0);
        store.Calls.ShouldBe(0);
        AssertRawStateUnchanged(before, raw.SnapshotRawEntries(scope.StoreName));
    }

    [Fact]
    public async Task AuthenticatedV1OperatorWithoutInstalledVerifiedInventoryIsOpaquelyRefused()
    {
        var store = new InMemoryReadModelStore();
        var authority = new CountingMigration();
        using GuardedFactory factory = new(allowOnAnyPort: true, store, authority, authenticatedOperator: true,
            callerAuthorizer: new InternalMagicLinkMigrationCallerAuthorizer("migration", "caller-only"));
        SharedProjectionEpochCoordinator coordinator = factory.Services.GetRequiredService<SharedProjectionEpochCoordinator>();
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        _ = await coordinator.RegisterWriterAsync(scope, MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId, TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.PostAsync("/admin/timesheets/magic-link-index/migrate-v1",
            new StringContent("{\"operationId\":\"migration\",\"receiptId\":\"caller-only\"}", System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        authority.Calls.ShouldBe(0);
        (await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken)).Epoch.ShouldBe(0);
    }

    [Fact]
    public async Task AuthenticatedV1OperatorWithDefaultAuthorityCannotReserveVerifiedInventory()
    {
        var store = new InMemoryReadModelStore();
        var inventory = new InternalMagicLinkV1Inventory([]);
        using GuardedFactory factory = new(allowOnAnyPort: true, store, authenticatedOperator: true, inventory: inventory,
            callerAuthorizer: new InternalMagicLinkMigrationCallerAuthorizer("migration", "server-receipt"));
        SharedProjectionEpochCoordinator coordinator = factory.Services.GetRequiredService<SharedProjectionEpochCoordinator>();
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        _ = await coordinator.RegisterWriterAsync(scope, MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId, TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.PostAsync("/admin/timesheets/magic-link-index/migrate-v1",
            new StringContent("{\"operationId\":\"migration\",\"receiptId\":\"server-receipt\"}", System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken)).Epoch.ShouldBe(0);
    }

    [Fact]
    public async Task VerifiedInternalV1RouteSelectsPersistedV7CandidatesAndRetainsDirectLegacy()
    {
        var store = new InMemoryReadModelStore();
        MagicLinkTokenHashCapabilityIndexDirectoryEntry[] rows =
        [new("shared", new(new TenantReference("tenant-a"), new MagicLinkCapabilityId("owner-a"))),
            new("shared", new(new TenantReference("tenant-b"), new MagicLinkCapabilityId("owner-b")))];
        var inventory = new InternalMagicLinkV1Inventory(rows);
        using GuardedFactory factory = new(allowOnAnyPort: true, store, new CountingMigration(),
            authenticatedOperator: true, inventory: inventory,
            callerAuthorizer: new InternalMagicLinkMigrationCallerAuthorizer("route-v1", "server-receipt", allowV6: false));
        SharedProjectionEpochCoordinator coordinator = factory.Services.GetRequiredService<SharedProjectionEpochCoordinator>();
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        _ = await coordinator.RegisterWriterAsync(scope, MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId, TestContext.Current.CancellationToken);
        await store.SaveAsync(scope.StoreName, MagicLinkTokenHashCapabilityIndexProjection.StateKey,
            new MagicLinkTokenHashCapabilityIndexReadModel(new Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry>()), TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.PostAsync("/admin/timesheets/magic-link-index/migrate-v1",
            new StringContent("{\"operationId\":\"route-v1\",\"receiptId\":\"server-receipt\"}", System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        MagicLinkTokenHashCapabilityIndexTreeRoot root = (await coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
            MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        var reader = new MagicLinkTokenHashCapabilityIndexTreeReader(new(store, store), scope);
        (await reader.FindCandidatesAsync(root.TokenRoot, "shared", TestContext.Current.CancellationToken)).Count.ShouldBe(2);
        foreach (MagicLinkTokenHashCapabilityIndexDirectoryEntry row in rows)
            (await reader.FindOwnerAsync(root.OwnerRoot, row.Candidate.Tenant.TenantId, row.Candidate.CapabilityId.Value,
                TestContext.Current.CancellationToken)).ShouldBe(row);
        (await store.GetAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope.StoreName,
            MagicLinkTokenHashCapabilityIndexProjection.StateKey, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        await AssertOtherCallerCannotReplayAsync(store, "migrate-v1", "route-v1", "server-receipt");
    }

    [Fact]
    public async Task CertifiedInternalOperatorRouteMigratesSelectedV6Candidates()
    {
        var store = new InMemoryReadModelStore();
        using GuardedFactory factory = new(allowOnAnyPort: true, store,
            new PermittedMigration(), authenticatedOperator: true,
            callerAuthorizer: new InternalMagicLinkMigrationCallerAuthorizer("route-migrate", "certified", allowV1: false));
        SharedProjectionEpochCoordinator coordinator = factory.Services
            .GetRequiredService<SharedProjectionEpochCoordinator>();
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        _ = await coordinator.RegisterWriterAsync(scope,
            MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        var ownerA = new MagicLinkTokenHashCapabilityIndexEntry(new TenantReference("tenant-a"),
            new MagicLinkCapabilityId("owner-a"));
        var ownerB = new MagicLinkTokenHashCapabilityIndexEntry(new TenantReference("tenant-b"),
            new MagicLinkCapabilityId("owner-b"));
        string hash = "shared";
        _ = await coordinator.BeginAsync(scope, "route-seed", "source",
            new Dictionary<string, long>(), TestContext.Current.CancellationToken);
        _ = await coordinator.StageStreamingAsync(scope, "route-seed", Seed(),
            TestContext.Current.CancellationToken);
        await coordinator.CommitAsync(scope, "route-seed", TestContext.Current.CancellationToken);

        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.PostAsync(
            "/admin/timesheets/magic-link-index/migrate-v6",
            new StringContent("{\"operationId\":\"route-migrate\",\"certifiedInventoryFingerprint\":\"certified\"}",
                System.Text.Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        SharedProjectionReadResult<MagicLinkTokenHashCapabilityIndexTreeRoot> root = await coordinator
            .ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
                MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
                TestContext.Current.CancellationToken);
        (await new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, scope)
            .FindCandidatesAsync(root.Value.ShouldNotBeNull().TokenRoot, hash,
                TestContext.Current.CancellationToken)).Count.ShouldBe(2);
        await AssertOtherCallerCannotReplayAsync(store, "migrate-v6", "route-migrate", "certified");

        async IAsyncEnumerable<ReadModelBatchOperation> Seed()
        {
            yield return ReadModelBatchOperation.Write(
                MagicLinkTokenHashCapabilityIndexProjection.ShardKey(hash),
                MagicLinkTokenHashCapabilityIndexProjection.FromContributions(
                [
                    new KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>(hash, ownerA),
                    new KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>(hash, ownerB)
                ]), ReadModelBatchConcurrency.LastWrite);
            await Task.Yield();
        }
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"operationId\":\"\",\"receiptId\":\"server-receipt\"}")]
    [InlineData("{\"operationId\":\"migration\",\"receiptId\":\"   \"}")]
    public async Task MalformedV1IdentifiersAreOpaquelyRefusedWithoutReservation(string body)
    {
        var store = new InMemoryReadModelStore();
        MagicLinkV1MigrationRequest? request = System.Text.Json.JsonSerializer.Deserialize<MagicLinkV1MigrationRequest>(body,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        using GuardedFactory factory = new(true, store, new CountingMigration(), authenticatedOperator: true,
            inventory: new InternalMagicLinkV1Inventory([]),
            callerAuthorizer: new InternalMagicLinkMigrationCallerAuthorizer(request?.OperationId, request?.ReceiptId));
        SharedProjectionEpochCoordinator coordinator = factory.Services.GetRequiredService<SharedProjectionEpochCoordinator>();
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        _ = await coordinator.RegisterWriterAsync(scope, MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId, TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.PostAsync("/admin/timesheets/magic-link-index/migrate-v1",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
        (await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken)).Epoch.ShouldBe(0);
    }

    [Fact]
    public async Task InvalidInstalledV1InventoryIsOpaquelyRefusedAndRetainsLegacy()
    {
        var store = new InMemoryReadModelStore();
        var inventory = new InternalMagicLinkV1Inventory([]) { InvalidDigest = true };
        using GuardedFactory factory = new(true, store, new CountingMigration(), authenticatedOperator: true, inventory: inventory,
            callerAuthorizer: new InternalMagicLinkMigrationCallerAuthorizer("route-v1", "server-receipt"));
        SharedProjectionEpochCoordinator coordinator = factory.Services.GetRequiredService<SharedProjectionEpochCoordinator>();
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        _ = await coordinator.RegisterWriterAsync(scope, MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId, TestContext.Current.CancellationToken);
        await store.SaveAsync(scope.StoreName, MagicLinkTokenHashCapabilityIndexProjection.StateKey,
            new MagicLinkTokenHashCapabilityIndexReadModel(new Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry>()), TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.PostAsync("/admin/timesheets/magic-link-index/migrate-v1",
            new StringContent("{\"operationId\":\"route-v1\",\"receiptId\":\"server-receipt\"}", System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
        (await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken)).Epoch.ShouldBe(0);
        (await store.GetAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope.StoreName,
            MagicLinkTokenHashCapabilityIndexProjection.StateKey, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task V1StoreAndSdkStageFailuresAreOpaqueRetryableWithDurablePrivateState(bool sdkFailure)
    {
        var store = new InMemoryReadModelStore();
        var interrupted = new InternalMagicLinkV1FaultStore(store, sdkFailure);
        MagicLinkTokenHashCapabilityIndexDirectoryEntry[] rows = [new("unique", new(new TenantReference("tenant"), new MagicLinkCapabilityId("owner")))];
        using GuardedFactory factory = new(true, interrupted, new CountingMigration(), authenticatedOperator: true,
            inventory: new InternalMagicLinkV1Inventory(rows),
            callerAuthorizer: new InternalMagicLinkMigrationCallerAuthorizer("route-v1", "server-receipt"));
        SharedProjectionEpochCoordinator coordinator = factory.Services.GetRequiredService<SharedProjectionEpochCoordinator>();
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        _ = await coordinator.RegisterWriterAsync(scope, MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId, TestContext.Current.CancellationToken);
        await store.SaveAsync(scope.StoreName, MagicLinkTokenHashCapabilityIndexProjection.StateKey,
            new MagicLinkTokenHashCapabilityIndexReadModel(new Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry>()), TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();
        async Task<HttpResponseMessage> SendAsync() => await client.PostAsync("/admin/timesheets/magic-link-index/migrate-v1",
            new StringContent("{\"operationId\":\"route-v1\",\"receiptId\":\"server-receipt\"}", System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);
        using HttpResponseMessage failed = await SendAsync();
        failed.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await failed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
        interrupted.Fired.ShouldBe(1);
        (await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken)).IsBuilding.ShouldBeTrue();
        (await coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
            MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, TestContext.Current.CancellationToken)).Value.ShouldBeNull();
        using HttpResponseMessage retried = await SendAsync();
        retried.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        MagicLinkTokenHashCapabilityIndexTreeRoot root = (await coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
            MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        (await new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, scope).FindOwnerAsync(root.OwnerRoot,
            "tenant", "owner", TestContext.Current.CancellationToken)).ShouldBe(rows[0]);
        (await store.GetAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope.StoreName,
            MagicLinkTokenHashCapabilityIndexProjection.StateKey, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
    }

    /// <summary>Lists the four public capability routes without supplying a user principal or workload credentials.</summary>
    /// <returns>The route and whether it requires POST.</returns>
    public static TheoryData<string, bool> PublicCapabilityRoutes() => new()
    {
        { "/api/timesheets/magic-links/confirm?t=opaque", false },
        { "/api/timesheets/magic-links/confirm/submit?t=opaque", true },
        { "/api/timesheets/magic-links/adjust?t=opaque", false },
        { "/api/timesheets/magic-links/adjust/submit?t=opaque", true }
    };

    /// <summary>Public capability routes reach their opaque denial without an authenticated principal.</summary>
    /// <param name="route">The public capability route.</param>
    /// <param name="post">Whether to send a POST body.</param>
    [Theory]
    [MemberData(nameof(PublicCapabilityRoutes))]
    public async Task ThePublicMagicLinkSurfaceStaysReachableWhenTheGuardIsClosed(string route, bool post)
    {
        // The guard must not collapse the routes this host exists to publish.
        using GuardedFactory factory = new(allowOnAnyPort: false);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = post
            ? await client.PostAsync(
                route,
                new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
                TestContext.Current.CancellationToken)
            : await client.GetAsync(route, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MetadataRouteIsNotSweptUpByTheGuard()
    {
        using GuardedFactory factory = new(allowOnAnyPort: false);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            "/metadata/timesheets",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Management operations remain under the workload fallback when public capability routes are open.</summary>
    /// <param name="route">The protected management route.</param>
    [Theory]
    [InlineData("/api/timesheets/magic-links/confirmation-capabilities")]
    [InlineData("/api/timesheets/magic-links/confirmation-capabilities/capability-1/revoke")]
    [InlineData("/api/timesheets/magic-links/confirmation-capabilities/capability-1/expire")]
    public async Task MagicLinkManagementRoutesRequireWorkloadCredentials(string route)
    {
        using GuardedFactory factory = new(allowOnAnyPort: false);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsync(
            route,
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private sealed class GuardedFactory(bool allowOnAnyPort,
        IReadModelStore? store = null,
        IMagicLinkIndexMigrationAuthority? migrationAuthority = null,
        bool authenticatedOperator = false,
        IMagicLinkV1VerifiedInventoryProvider? inventory = null,
        IMagicLinkIndexMigrationCallerAuthorizer? callerAuthorizer = null,
        string operatorId = "certified-operator") : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureLogging(static logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.Configure<InternalSurfaceOptions>(
                    options => options.AllowOnAnyPort = allowOnAnyPort);
                if (store is not null)
                {
                    services.RemoveAll<IReadModelStore>();
                    services.RemoveAll<IReadModelBatchStore>();
                    services.AddSingleton<IReadModelStore>(store);
                    services.AddSingleton<IReadModelBatchStore>((IReadModelBatchStore)store);
                }
                if (migrationAuthority is not null)
                {
                    services.RemoveAll<IMagicLinkIndexMigrationAuthority>();
                    services.AddSingleton(migrationAuthority);
                }
                if (authenticatedOperator)
                    services.AddSingleton<IStartupFilter>(new OperatorPrincipalFilter(operatorId));
                if (callerAuthorizer is not null)
                {
                    services.RemoveAll<IMagicLinkIndexMigrationCallerAuthorizer>();
                    services.AddSingleton(callerAuthorizer);
                }
                if (inventory is not null)
                {
                    services.RemoveAll<IMagicLinkV1VerifiedInventoryProvider>();
                    services.AddSingleton(inventory);
                }
            });
        }
    }

    private sealed class OperatorPrincipalFilter(string operatorId) : IStartupFilter
    {
        public Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> Configure(
            Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> next)
            => app =>
            {
                app.Use(async (context, following) =>
                {
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, operatorId)], "TestAuth"));
                    await following(context).ConfigureAwait(false);
                });
                next(app);
            };
    }

    private sealed class PermittedMigration : IMagicLinkIndexMigrationAuthority
    {
        public Task<bool> AuthorizeCertifiedMigrationAsync(string operationId,
            string certifiedInventoryFingerprint, CancellationToken cancellationToken)
            => Task.FromResult(operationId == "route-migrate"
                && certifiedInventoryFingerprint == "certified");
    }

    private sealed class CountingMigration : IMagicLinkIndexMigrationAuthority
    {
        public int Calls { get; private set; }
        public bool Allowed { get; init; } = true;

        public Task<bool> AuthorizeCertifiedMigrationAsync(string operationId,
            string certifiedInventoryFingerprint, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Allowed);
        }
    }

    private static async Task SeedMigrationHistoryAndPendingJournalAsync(InMemoryReadModelStore store,
        SharedProjectionScope scope)
    {
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(scope,
            MagicLinkTokenHashCapabilityIndexProjection.OrdinaryWriterId, TestContext.Current.CancellationToken);
        (await coordinator.JournalAsync(scope, lease, new("retained-owner", 1, [1], [], OwnerSequenceNumber: 1),
            TestContext.Current.CancellationToken)).ShouldBe(SharedProjectionJournalResult.Journaled);
        (await coordinator.CatchUpAsync(scope, (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>(
            [ReadModelBatchOperation.Write("retained-history", new MagicLinkTokenHashCapabilityIndexTreeFormat(0),
                ReadModelBatchConcurrency.LastWrite)]), TestContext.Current.CancellationToken)).ShouldBe(1);
        (await coordinator.JournalAsync(scope, lease, new("retained-owner", 2, [2], [], OwnerSequenceNumber: 2),
            TestContext.Current.CancellationToken)).ShouldBe(SharedProjectionJournalResult.Journaled);
        await store.SaveAsync(scope.StoreName, MagicLinkTokenHashCapabilityIndexProjection.StateKey,
            new MagicLinkTokenHashCapabilityIndexReadModel(new Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry>()),
            TestContext.Current.CancellationToken);
        (await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken)).PendingDeliveryCount.ShouldBe(1);
    }

    private static async Task AssertOtherCallerCannotReplayAsync(InMemoryReadModelStore raw,
        string route, string operationId, string evidenceId)
    {
        var store = new InternalMagicLinkMigrationCountingStore(raw);
        var authority = new CountingMigration();
        var inventory = new InternalMagicLinkV1Inventory([]);
        var callerAuthorizer = new InternalMagicLinkMigrationCallerAuthorizer(operationId, evidenceId);
        using GuardedFactory factory = new(true, store, authority, authenticatedOperator: true,
            inventory: inventory, callerAuthorizer: callerAuthorizer, operatorId: "another-operator");
        using HttpClient client = factory.CreateClient();
        store.ResetCalls();
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        IReadOnlyDictionary<string, byte[]> before = raw.SnapshotRawEntries(scope.StoreName);
        using HttpResponseMessage response = await client.PostAsync(
            "/admin/timesheets/magic-link-index/" + route,
            new StringContent(System.Text.Json.JsonSerializer.Serialize(new
            {
                operationId,
                receiptId = evidenceId,
                certifiedInventoryFingerprint = evidenceId
            }), System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
        callerAuthorizer.Calls.ShouldBe(1);
        authority.Calls.ShouldBe(0);
        inventory.Calls.ShouldBe(0);
        store.Calls.ShouldBe(0);
        AssertRawStateUnchanged(before, raw.SnapshotRawEntries(scope.StoreName));
    }

    private static void AssertRawStateUnchanged(IReadOnlyDictionary<string, byte[]> before,
        IReadOnlyDictionary<string, byte[]> after)
    {
        before.Count.ShouldBeGreaterThan(0);
        after.Keys.Order(StringComparer.Ordinal).ShouldBe(before.Keys.Order(StringComparer.Ordinal));
        foreach ((string key, byte[] value) in before)
            after[key].ShouldBe(value);
    }
}
