using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Projections.MagicLinks;
using Hexalith.Timesheets.Runtime;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.Runtime;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed partial class MagicLinkConfirmationHttpBoundaryTests
{
    /// <summary>Rejects missing owner routes and changed operation schemas during fixture activation.</summary>
    /// <param name="scenario">The owner fact changed before catalog sealing.</param>
    [Theory]
    [InlineData("missing-owner")]
    [InlineData("missing-server-owner")]
    [InlineData("wrong-schema")]
    public async Task FixtureCatalogActivationRejectsChangedOwnerInventoryWithoutStateWrite(string scenario)
    {
        var store = new InMemoryReadModelStore();
        using MagicLinkHttpBoundaryFactory factory = new(batchStore: store);
        const string RouteId = "timesheets-magic-link-token-hash-capability-index";
        factory.RouteCatalog.ObservationTransform = observation => scenario switch
        {
            "missing-owner" => observation with
            {
                Declarations = observation.Declarations.Where(route => route.RouteId != RouteId).ToArray(),
                Operations = observation.Operations!.Where(operation => operation.RouteId != RouteId).ToArray()
            },
            "missing-server-owner" => observation with
            {
                Declarations = observation.Declarations.Where(route => route.Kind != "command").ToArray(),
                Operations = observation.Operations!.Where(operation => observation.Declarations
                    .Any(route => route.Kind != "command" && route.RouteId == operation.RouteId)).ToArray()
            },
            _ => observation with
            {
                Operations = observation.Operations!.Select(operation =>
                {
                    if (operation.RouteId != RouteId) return operation;
                    RouteOperationContract changed = operation with
                    { RequestSchema = JsonSerializer.SerializeToElement(new { type = "string" }) };
                    return changed with { Digest = RouteCatalogCodec.ComputeOperationContractDigest(changed) };
                }).ToArray()
            }
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/project/v2");

        await Should.ThrowAsync<RouteCatalogValidationException>(() => factory.AddCatalogHeadersAsync(request));

        store.SnapshotRawEntries(MagicLinkTokenHashCapabilityIndexProjection.StateStoreName).ShouldBeEmpty();
        factory.Gateway.SubmissionCount.ShouldBe(0);
    }

    /// <summary>Checks the observer and actual receiver inventory enrolled by Program in Development.</summary>
    [Fact]
    public async Task ConfiguredHostEnrollsCompleteOwnerInSdkDevelopmentBootstrap()
    {
        string directory = Directory.CreateTempSubdirectory("timesheets-catalog-enrollment-").FullName;
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            string component = Path.Combine(directory, "component.json");
            string acl = Path.Combine(directory, "acl.json");
            var state = new HttpRouteCatalogStateStore();
            await File.WriteAllTextAsync(component, """
                {"kind":"Component","metadata":{"name":"http-fixture-catalog"},"scopes":["timesheets","eventstore"],
                 "spec":{"type":"state.redis","metadata":[{"name":"keyPrefix","value":"none"}]}}
                """, deadline.Token);
            await File.WriteAllTextAsync(acl, """
                {"spec":{"accessControl":{"defaultAction":"deny","trustDomain":"public","policies":[]}}}
                """, deadline.Token);

            const string Bootstrap = "EventStore:RouteCatalog:DevelopmentBootstrap:";
            await using WebApplicationFactory<Program> root = new();
            await using WebApplicationFactory<Program> configured = root.WithWebHostBuilder(host =>
            {
                host.UseEnvironment("Development");
                host.UseSetting(DaprAppChannelToken.ConfigurationKey, "timesheets-catalog-test-token");
                host.UseSetting("EventStore:DomainService:AppId", "timesheets");
                host.UseSetting("Authentication:JwtBearer:Issuer", "timesheets-test-issuer");
                host.UseSetting("Authentication:JwtBearer:Audience", "hexalith-eventstore");
                host.UseSetting("Authentication:JwtBearer:SigningKey", Convert.ToBase64String(new byte[32]));
                host.UseSetting("Authentication:JwtBearer:AllowedAlgorithms:0", SecurityAlgorithms.HmacSha256);
                host.UseSetting("Authentication:JwtBearer:RequireHttpsMetadata", "false");
                host.UseSetting("Authentication:WorkloadIssuer:Workload", "eventstore");
                host.UseSetting(Bootstrap + "Directory", directory);
                host.UseSetting(Bootstrap + "HostId", "timesheets");
                host.UseSetting(Bootstrap + "CoordinatorHostId", "eventstore");
                host.UseSetting(Bootstrap + "AttemptId", "enrollment-test");
                host.UseSetting(Bootstrap + "RequiredHostIds:0", "timesheets");
                host.UseSetting(Bootstrap + "RequiredHostIds:1", "eventstore");
                host.UseSetting(Bootstrap + "ComponentPath", component);
                host.UseSetting(Bootstrap + "AccessControlPath", acl);
                host.ConfigureServices(services =>
                {
                    var readModels = new InMemoryReadModelStore();
                    services.AddDataProtection().UseEphemeralDataProtectionProvider();
                    services.RemoveAll<IRouteCatalogStateStore>();
                    services.AddSingleton<IRouteCatalogStateStore>(state);
                    services.RemoveAll<IReadModelStore>();
                    services.AddSingleton<IReadModelStore>(readModels);
                    services.RemoveAll<IReadModelBatchStore>();
                    services.AddSingleton<IReadModelBatchStore>(readModels);
                });
            });
            using HttpClient client = configured.CreateClient();
            configured.Services.GetRequiredService<IRouteCatalogHostObserver>().ShouldBeOfType<ObservedRouteCatalogHost>();
            string observationPath = Path.Combine(directory, "timesheets.observation.json");
            while (!File.Exists(observationPath))
            {
                await Task.Delay(25, deadline.Token);
            }

            var observation = JsonSerializer.Deserialize<RouteCatalogHostObservation>(
                await File.ReadAllBytesAsync(observationPath, deadline.Token)).ShouldNotBeNull();
            observation.AuthorityCoordinates.ShouldBe(state.Coordinates);
            var operations = observation.Operations.ShouldNotBeNull();
            var expectedRoutes = TimesheetsHostRouteCatalogDeclarations.Routes;
            var expectedMethods = TimesheetsHostRouteCatalogDeclarations.ReceiverMethods;
            observation.Declarations.Count.ShouldBe(16);
            operations.Count.ShouldBe(16);
            observation.Targets.Count.ShouldBe(16);
            observation.Declarations.Select(RouteCatalogCodec.ComputeDeclarationDigest)
                .ShouldBe(expectedRoutes.Select(RouteCatalogCodec.ComputeDeclarationDigest), ignoreOrder: true);
            operations.Select(operation => operation.Digest)
                .ShouldBe(TimesheetsHostRouteCatalogDeclarations.Operations.Select(operation => operation.Digest), ignoreOrder: true);
            observation.Targets.Select(target => (target.RouteId, target.Method, target.DeclarationDigest))
                .ShouldBe(expectedRoutes.Select(route =>
                    (route.RouteId, expectedMethods[route.RouteId], RouteCatalogCodec.ComputeDeclarationDigest(route))), ignoreOrder: true);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Certifies the exact compiled owner inventory against installed handlers and protected SDK receivers.</summary>
    [Fact]
    public void ComposedCatalogRequiresEveryInstalledOwnerAndMatchingReceiver()
    {
        var store = new InMemoryReadModelStore();
        using MagicLinkHttpBoundaryFactory factory = new(batchStore: store);
        using HttpClient client = factory.CreateClient();
        using IServiceScope scope = factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        var owners = new[] { typeof(TimesheetsRouteCatalogDeclarations).Assembly,
            typeof(TimesheetsProjectionRouteCatalogDeclarations).Assembly };
        IReadOnlyList<RouteContractDeclaration> declarations = CompiledRouteCatalogDeclarations.Read(owners);
        IReadOnlyList<RouteOperationContract> operations = CompiledRouteCatalogDeclarations.ReadOperations(owners);
        declarations.Count.ShouldBe(16);
        declarations.Select(route => route.RouteId).ShouldBe(new[]
        {
            "timesheets-commit-magic-link-issue", "timesheets-commit-magic-link-transition",
            "timesheets-commit-magic-link-use", "timesheets-commit-time-entry-record",
            "timesheets-commit-time-entry-confirmation", "timesheets-commit-approved-time-export",
            "timesheets-commit-project-time-entry-record", "timesheets-commit-project-time-entry-submit",
            "timesheets-commit-project-time-entry-approve", "timesheets-commit-project-time-entry-correct",
            "timesheets-magic-link-token-hash-capability-index", "timesheets-magic-link-confirmation-capabilities",
            "timesheets-tenant-activity-type-catalog", "timesheets-time-entry-evidence",
            "timesheets-approved-time-ledger", "timesheets-query-approved-time-ledger"
        }, ignoreOrder: true);
        declarations.Count(route => route.Kind == "command").ShouldBe(10);
        declarations.Where(route => route.Kind == "projection").Select(route => route.ProjectionType)
            .ShouldBe(TimesheetsProjectionRouteCatalogDeclarations.Projections.Select(route => route.ProjectionType), ignoreOrder: true);
        operations.Select(operation => operation.RouteId)
            .ShouldBe(declarations.Select(route => route.RouteId), ignoreOrder: true);
        var hostAssembly = typeof(Program).Assembly;
        hostAssembly.GetCustomAttributes<RouteCatalogDeclarationSourceAttribute>().Single().SourceType
            .ShouldBe(typeof(TimesheetsHostRouteCatalogDeclarations));
        IReadOnlyList<RouteContractDeclaration> hostDeclarations = CompiledRouteCatalogDeclarations.Read([hostAssembly]);
        hostDeclarations.Select(RouteCatalogCodec.ComputeDeclarationDigest)
            .ShouldBe(declarations.Select(RouteCatalogCodec.ComputeDeclarationDigest), ignoreOrder: true);
        CompiledRouteCatalogDeclarations.ReadOperations([hostAssembly]).Select(operation => operation.Digest)
            .ShouldBe(operations.Select(operation => operation.Digest), ignoreOrder: true);
        TimesheetsHostRouteCatalogDeclarations.Commands.Select(command => JsonSerializer.Serialize(command))
            .ShouldBe(TimesheetsRouteCatalogAdapters.Create().Select(adapter => JsonSerializer.Serialize(adapter.Declaration)), ignoreOrder: true);

        EndpointDataSource endpoints = services.GetRequiredService<EndpointDataSource>();
        IReadOnlyList<RouteCatalogEntry> targets = DevelopmentDomainRouteCatalog.Targets(services, endpoints.Endpoints, hostDeclarations);
        targets.Count.ShouldBe(16);
        targets.Count(target => target.Method == "process").ShouldBe(10);
        targets.Count(target => target.Method == "project/v2").ShouldBe(5);
        targets.Single(target => target.Method == "query").RouteId.ShouldBe("timesheets-query-approved-time-ledger");
        TimesheetsHostRouteCatalogDeclarations.ReceiverMethods
            .ShouldBe(targets.Select(target => KeyValuePair.Create(target.RouteId, target.Method)), ignoreOrder: true);

        Should.Throw<RouteCatalogUnavailableException>(() => CompiledRouteCatalogDeclarations.Read([.. owners, hostAssembly]));
        Should.Throw<RouteCatalogUnavailableException>(() => CompiledRouteCatalogDeclarations.ReadOperations([.. owners, hostAssembly]));
        Should.Throw<RouteCatalogUnavailableException>(() => DevelopmentDomainRouteCatalog.Targets(
            services, endpoints.Endpoints, CompiledRouteCatalogDeclarations.Read([owners[0]])));
        Should.Throw<RouteCatalogUnavailableException>(() => DevelopmentDomainRouteCatalog.Targets(
            services, endpoints.Endpoints, declarations.Where(route => route.RouteId != "timesheets-commit-time-entry-record").ToArray()));
        RouteContractDeclaration projection = declarations.Single(route =>
            route.ProjectionType == MagicLinkTokenHashCapabilityIndexProjection.ProjectionName);
        Should.Throw<RouteCatalogUnavailableException>(() => DevelopmentDomainRouteCatalog.Targets(
            services, endpoints.Endpoints, declarations.Where(route => route != projection).ToArray()));
        Should.Throw<RouteCatalogUnavailableException>(() => DevelopmentDomainRouteCatalog.Targets(
            services, endpoints.Endpoints, declarations.Select(route => route == projection
                ? route with { Execution = route.Execution! with { HandlerType = typeof(object).FullName! } }
                : route).ToArray()));
        store.SnapshotRawEntries(MagicLinkTokenHashCapabilityIndexProjection.StateStoreName).ShouldBeEmpty();
        factory.Gateway.SubmissionCount.ShouldBe(0);
    }

    /// <summary>Proves supported fixture activation preserves receiver admission and unchanged projection state on refusal.</summary>
    /// <param name="scenario">The absent or conflicting generation or projection evidence.</param>
    [Theory]
    [InlineData("unactivated")]
    [InlineData("missing-root")]
    [InlineData("wrong-root")]
    [InlineData("multiple-roots")]
    [InlineData("missing-epoch")]
    [InlineData("wrong-epoch")]
    [InlineData("multiple-epochs")]
    [InlineData("wrong-fingerprint")]
    [InlineData("unknown-projection")]
    [InlineData("foreign-domain")]
    [InlineData("noncanonical-method")]
    public async Task FixtureCatalogRefusesUnboundProjectionWithoutWritingState(string scenario)
    {
        var store = new InMemoryReadModelStore();
        using MagicLinkHttpBoundaryFactory factory = new(batchStore: store);
        using HttpClient client = factory.CreateClient();
        var capability = new MagicLinkCapabilityId("catalog-admission-capability");
        ProjectionDispatchRequest valid = DeadlineDelivery(factory, capability,
            MagicLinkHttpBoundaryFactory.ProjectionEvent(1, new MagicLinkConfirmationCapabilityIssued(
                capability, Tenant(), Contributor(), TimeEntryTargetReference.ForProject(Project()), ActivityId(), new("catalog-entry"),
                MagicLinkTargetKind.ProposedTimeEntry,
                MagicLinkAllowedAction.Confirm, new("catalog-token-hash"),
                ObservedAtUtc.AddDays(1), Operator(), ObservedAtUtc, new("timesheets", "catalog-admission"), true)));
        ProjectionDispatchRequest invalid = scenario switch
        {
            "wrong-fingerprint" => valid with { CatalogFingerprint = new string('f', 64) },
            "unknown-projection" => valid with { ProjectionTypes = ["unknown-projection"] },
            "foreign-domain" => valid with { Request = valid.Request with { Domain = "foreign" } },
            _ => valid
        };
        using var request = new HttpRequestMessage(HttpMethod.Post,
            scenario == "noncanonical-method" ? "/PROJECT/v2" : "/project/v2")
        {
            Content = JsonContent.Create(invalid, options: JsonOptions)
        };
        request.Headers.Add(DaprAppChannelToken.HeaderName, "timesheets-test-app-channel-token");
        request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName,
            await factory.IssueAssertionAsync(EventStoreWorkloadOperations.DomainServiceProject));
        if (scenario != "unactivated")
        {
            await factory.AddCatalogHeadersAsync(request);
        }

        string header = scenario.Contains("epoch", StringComparison.Ordinal)
            ? RouteCatalogInvocationHandler.EpochHeader : RouteCatalogInvocationHandler.RootHeader;
        if (scenario.StartsWith("missing-", StringComparison.Ordinal))
        {
            request.Headers.Remove(header);
        }
        else if (scenario is "wrong-root" or "wrong-epoch")
        {
            request.Headers.Remove(header);
            request.Headers.Add(header, scenario == "wrong-root" ? new string('f', 64) : "999");
        }
        else if (scenario.StartsWith("multiple-", StringComparison.Ordinal))
        {
            request.Headers.Add(header, request.Headers.GetValues(header).Single());
        }

        using HttpResponseMessage refused = await client.SendAsync(request, TestContext.Current.CancellationToken);

        refused.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        store.SnapshotRawEntries(MagicLinkTokenHashCapabilityIndexProjection.StateStoreName).ShouldBeEmpty();
        factory.Gateway.SubmissionCount.ShouldBe(0);

        using HttpResponseMessage accepted = await factory.PostProjectionAsync(client, "/project/v2", valid);
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var epoch = new SharedProjectionEpochCoordinator(store, store);
        var scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        var root = (await epoch.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
            MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        var reader = new MagicLinkTokenHashCapabilityIndexTreeReader(epoch, scope);
        (await reader.FindCandidatesAsync(root.TokenRoot, "catalog-token-hash", TestContext.Current.CancellationToken))
            .ShouldHaveSingleItem().ShouldBe(new MagicLinkTokenHashCapabilityIndexEntry(Tenant(), capability));
    }
}
