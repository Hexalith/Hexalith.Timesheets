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
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.Runtime;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed partial class MagicLinkConfirmationHttpBoundaryTests
{
    /// <summary>Rejects missing owner routes and changed operation schemas during fixture activation.</summary>
    /// <param name="scenario">The owner fact changed before catalog sealing.</param>
    [Theory]
    [InlineData("missing-owner")]
    [InlineData("wrong-schema")]
    public async Task FixtureCatalogActivationRejectsChangedOwnerInventoryWithoutStateWrite(string scenario)
    {
        var store = new InMemoryReadModelStore();
        using MagicLinkHttpBoundaryFactory factory = new(batchStore: store);
        const string RouteId = "timesheets-magic-link-token-hash-capability-index";
        factory.RouteCatalog.ObservationTransform = observation => scenario == "missing-owner"
            ? observation with
            {
                Declarations = observation.Declarations.Where(route => route.RouteId != RouteId).ToArray(),
                Operations = observation.Operations!.Where(operation => operation.RouteId != RouteId).ToArray()
            }
            : observation with
            {
                Operations = observation.Operations!.Select(operation =>
                {
                    if (operation.RouteId != RouteId) return operation;
                    RouteOperationContract changed = operation with
                    { RequestSchema = JsonSerializer.SerializeToElement(new { type = "string" }) };
                    return changed with { Digest = RouteCatalogCodec.ComputeOperationContractDigest(changed) };
                }).ToArray()
            };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/project/v2");

        await Should.ThrowAsync<RouteCatalogValidationException>(() => factory.AddCatalogHeadersAsync(request));

        store.SnapshotRawEntries(MagicLinkTokenHashCapabilityIndexProjection.StateStoreName).ShouldBeEmpty();
        factory.Gateway.SubmissionCount.ShouldBe(0);
    }

    /// <summary>Checks configured Program enrollment before its private bootstrap worker starts.</summary>
    [Fact]
    public void ConfiguredHostEnrollsCompleteOwnerInSdkDevelopmentBootstrap()
    {
        string directory = Directory.CreateTempSubdirectory("timesheets-catalog-enrollment-").FullName;
        try
        {
            bool inspected = false;
            using WebApplicationFactory<Program> root = new();
            using WebApplicationFactory<Program> configured = root.WithWebHostBuilder(host =>
            {
                host.UseEnvironment("Development");
                host.UseSetting("EventStore:RouteCatalog:DevelopmentBootstrap:Directory", directory);
                host.UseSetting("EventStore:RouteCatalog:DevelopmentBootstrap:HostId", "timesheets");
                host.UseSetting("EventStore:RouteCatalog:DevelopmentBootstrap:CoordinatorHostId", "eventstore");
                host.UseSetting("EventStore:RouteCatalog:DevelopmentBootstrap:AttemptId", "enrollment-test");
                host.UseSetting("EventStore:RouteCatalog:DevelopmentBootstrap:RequiredHostIds:0", "timesheets");
                host.UseSetting("EventStore:RouteCatalog:DevelopmentBootstrap:RequiredHostIds:1", "eventstore");
                host.ConfigureServices(services =>
                {
                    ServiceDescriptor bootstrap = services.Single(descriptor =>
                        descriptor.ServiceType == typeof(IHostedService)
                        && descriptor.ImplementationFactory?.Method.DeclaringType?.FullName?.StartsWith(
                            typeof(DevelopmentRouteCatalogBootstrap).FullName + "+", StringComparison.Ordinal) == true);
                    using ServiceProvider empty = new ServiceCollection().BuildServiceProvider();
                    bootstrap.ImplementationFactory!(empty).GetType().Name.ShouldBe("DevelopmentRouteCatalogBootstrapWorker");
                    services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IRouteCatalogHostObserver));
                    var owners = new[] { typeof(TimesheetsRouteCatalogDeclarations).Assembly,
                        typeof(TimesheetsProjectionRouteCatalogDeclarations).Assembly };
                    owners.SelectMany(owner => owner.GetCustomAttributes<RouteCatalogDeclarationSourceAttribute>())
                        .Single().SourceType.ShouldBe(typeof(TimesheetsProjectionRouteCatalogDeclarations));
                    CompiledRouteCatalogDeclarations.Read(owners).Count.ShouldBe(8);
                    CompiledRouteCatalogDeclarations.ReadOperations(owners).Count.ShouldBe(8);
                    TimesheetsProjectionRouteCatalogDeclarations.Commands.Count.ShouldBe(4);
                    inspected = true;
                    throw new InvalidOperationException("configured-catalog-probe-complete");
                });
            });
            Exception? failure = null;
            try { using HttpClient client = configured.CreateClient(); }
            catch (Exception exception) { failure = exception; }
            failure.ShouldNotBeNull().ToString().ShouldContain("configured-catalog-probe-complete");
            inspected.ShouldBeTrue();
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
        declarations.Count.ShouldBe(8);
        declarations.Select(route => route.RouteId).ShouldBe(new[]
        {
            "timesheets-commit-magic-link-issue", "timesheets-commit-magic-link-transition",
            "timesheets-commit-magic-link-use", "timesheets-commit-time-entry-record",
            "timesheets-magic-link-token-hash-capability-index", "timesheets-magic-link-confirmation-capabilities",
            "timesheets-tenant-activity-type-catalog", "timesheets-time-entry-evidence"
        }, ignoreOrder: true);
        declarations.Count(route => route.Kind == "command").ShouldBe(4);
        declarations.Where(route => route.Kind == "projection").Select(route => route.ProjectionType)
            .ShouldBe(TimesheetsProjectionRouteCatalogDeclarations.Projections.Select(route => route.ProjectionType), ignoreOrder: true);
        operations.Select(operation => operation.RouteId)
            .ShouldBe(declarations.Select(route => route.RouteId), ignoreOrder: true);
        EndpointDataSource endpoints = services.GetRequiredService<EndpointDataSource>();
        IReadOnlyList<RouteCatalogEntry> targets = DevelopmentDomainRouteCatalog.Targets(services, endpoints.Endpoints, declarations);
        targets.Count.ShouldBe(8);
        targets.Where(target => target.Method == "process").Count().ShouldBe(4);
        targets.Where(target => target.Method == "project/v2").Count().ShouldBe(4);

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
