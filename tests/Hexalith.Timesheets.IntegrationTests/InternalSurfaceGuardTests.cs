using System.Net;
using System.Security.Claims;

using Shouldly;

using Hexalith.Timesheets.Runtime;
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
    public void Normally_composed_host_registers_both_materialized_projection_routes()
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
    public async Task Domain_service_routes_are_refused_when_no_internal_port_is_configured(string route)
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

    [Fact]
    public async Task Internal_migration_route_is_default_denied_without_a_certified_operator_authority()
    {
        using GuardedFactory factory = new(allowOnAnyPort: true, authenticatedOperator: true);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.PostAsync(
            "/admin/timesheets/magic-link-index/migrate-v6",
            new StringContent("{\"operationId\":\"migration\",\"certifiedInventoryFingerprint\":\"caller-only\"}",
                System.Text.Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Internal_migration_route_requires_an_authenticated_operator()
    {
        var authority = new CountingMigration();
        using GuardedFactory factory = new(allowOnAnyPort: true,
            migrationAuthority: authority);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.PostAsync(
            "/admin/timesheets/magic-link-index/migrate-v6",
            new StringContent("{\"operationId\":\"migration\",\"certifiedInventoryFingerprint\":\"caller-only\"}",
                System.Text.Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);
        response.StatusCode.ShouldNotBe(HttpStatusCode.NoContent);
        authority.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Certified_internal_operator_route_migrates_selected_v6_candidates()
    {
        var store = new InMemoryReadModelStore();
        using GuardedFactory factory = new(allowOnAnyPort: true, store,
            new PermittedMigration(), authenticatedOperator: true);
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
    public async Task The_public_magic_link_surface_stays_reachable_when_the_guard_is_closed(string route, bool post)
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
    public async Task Metadata_route_is_not_swept_up_by_the_guard()
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
    public async Task Magic_link_management_routes_require_workload_credentials(string route)
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
        InMemoryReadModelStore? store = null,
        IMagicLinkIndexMigrationAuthority? migrationAuthority = null,
        bool authenticatedOperator = false) : WebApplicationFactory<Program>
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
                    services.AddSingleton<IReadModelBatchStore>(store);
                }
                if (migrationAuthority is not null)
                {
                    services.RemoveAll<IMagicLinkIndexMigrationAuthority>();
                    services.AddSingleton(migrationAuthority);
                }
                if (authenticatedOperator)
                    services.AddSingleton<IStartupFilter, OperatorPrincipalFilter>();
            });
        }
    }

    private sealed class OperatorPrincipalFilter : IStartupFilter
    {
        public Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> Configure(
            Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> next)
            => app =>
            {
                app.Use(async (context, following) =>
                {
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "certified-operator")], "TestAuth"));
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

        public Task<bool> AuthorizeCertifiedMigrationAsync(string operationId,
            string certifiedInventoryFingerprint, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(true);
        }
    }
}
