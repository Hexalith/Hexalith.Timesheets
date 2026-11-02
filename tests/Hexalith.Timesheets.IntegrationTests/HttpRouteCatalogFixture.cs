using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Timesheets.Server.Runtime;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hexalith.Timesheets.IntegrationTests;

/// <summary>Activates an isolated Development catalog through the platform lifecycle and real receiver guard.</summary>
internal sealed class HttpRouteCatalogFixture
{
    private readonly SemaphoreSlim _activation = new(1, 1);
    private RouteCatalogGenerationReadback? _generation;
    private bool _hasGateway;

    /// <summary>Replaces only authority storage; the platform authority, trust validator and guards remain installed.</summary>
    public void Configure(IServiceCollection services, Func<HttpMessageHandler, HttpMessageHandler>? invocationTransport = null)
    {
        _hasGateway = invocationTransport is not null;
        services.RemoveAll<IRouteCatalogStateStore>();
        services.AddSingleton<IRouteCatalogStateStore, HttpRouteCatalogStateStore>();
        services.AddSingleton<IOptions<RouteCatalogHostOptions>>(Options.Create(new RouteCatalogHostOptions
        {
            Profile = "Development",
            DevelopmentFixture = true,
            InvocationTransportFactory = invocationTransport
        }));
        services.AddEventStoreRouteCatalogObservation((provider, _) =>
        {
            using IServiceScope scope = provider.CreateScope();
            return Task.FromResult(Observe(scope.ServiceProvider));
        });
    }

    /// <summary>Uses the committed readback, without accepting caller-supplied generation authority.</summary>
    public async Task AddHeadersAsync(IServiceProvider services, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await _activation.WaitAsync(cancellationToken);
        try
        {
            if (_generation is null)
            {
                using IServiceScope scope = services.CreateScope();
                IServiceProvider provider = scope.ServiceProvider;
                RouteCatalogHostObservation observation = Observe(provider);
                RouteCatalogHostIdentity identity = ObservedRouteCatalogHost.Identity(observation);
                RouteCatalogHostObservation? gateway = _hasGateway ? ObserveGateway(provider, observation) : null;
                RouteCatalogHostIdentity[] hosts = gateway is null ? [identity] : [identity, ObservedRouteCatalogHost.Identity(gateway)];
                provider.GetRequiredService<IOptions<RouteCatalogHostOptions>>().Value.Identity = identity;
                RouteCatalogEnvelope envelope = RouteCatalogCodec.Seal(new(2, 1, "Development", false,
                    observation.Declarations, observation.Targets, observation.AdmissionPolicies!, "", ""));
                var candidate = new RouteCatalogActivation("http-fixture-bootstrap", RouteCatalogCodec.Encode(envelope),
                    RouteCatalogCodec.ComputeRootDigest(envelope), null, hosts, "Development-fixture", "",
                    [.. observation.AuxiliaryOperations!, .. gateway?.AuxiliaryOperations ?? []], observation.ProjectionCapabilities);
                candidate = candidate with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(candidate) };
                IRouteCatalogAuthority authority = provider.GetRequiredService<IRouteCatalogAuthority>();
                await authority.PrepareAsync(candidate, cancellationToken);
                await authority.ReadyAsync(candidate.AttemptId, cancellationToken);
                if (gateway is not null)
                {
                    var gatewayOptions = Options.Create(new RouteCatalogHostOptions
                    {
                        Identity = hosts[1], Profile = "Development", DevelopmentFixture = true
                    });
                    var gatewayAuthority = new RouteCatalogAuthority(provider.GetRequiredService<IRouteCatalogStateStore>(),
                        new DevelopmentRouteCatalogTrustValidator(gatewayOptions, provider.GetRequiredService<IHostEnvironment>()),
                        gatewayOptions, new ObservedRouteCatalogHost(_ => Task.FromResult(gateway)));
                    await gatewayAuthority.ReadyAsync(candidate.AttemptId, cancellationToken);
                }
                await authority.CommitAsync(candidate.AttemptId, cancellationToken);
                _generation = await authority.ReadbackAsync(cancellationToken);
            }

            request.Headers.Add(RouteCatalogInvocationHandler.RootHeader, _generation.RootDigest);
            request.Headers.Add(RouteCatalogInvocationHandler.EpochHeader,
                _generation.Epoch.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            _activation.Release();
        }
    }

    private RouteCatalogHostObservation Observe(IServiceProvider services)
    {
        DomainProjectionIdentityOptions identity = services.GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
        IAsyncDomainProjectionHandler[] handlers = services.GetServices<IAsyncDomainProjectionHandler>().ToArray();
        RouteContractDeclaration[] projections = handlers.Select(handler => new RouteContractDeclaration(
            $"fixture-{handler.Domain}-{handler.ProjectionType}", "projection", handler.Domain, null,
            handler.ProjectionType, identity.ServiceVersion, null, "workload",
            EventStoreDomainServiceRoutes.Operational.Single(route => route.Route == "/project/v2").Operation,
            null, [], new(handler.GetType().FullName!, "exact", [handler.ProjectionType], "domain-service", null,
                [handler.ProjectionType]))).ToArray();
        RouteContractDeclaration[] declarations = [.. TimesheetsRouteCatalogDeclarations.Routes, .. projections];
        IDomainProcessor processor = services.GetRequiredKeyedService<IDomainProcessor>(TimesheetsEventStoreIntegration.DomainName);
        if (processor is not IDomainCommandPayloadInventory inventory
            || !inventory.CommandPayloadTypes.Select(type => type.FullName).Order(StringComparer.Ordinal)
                .SequenceEqual(declarations.Where(declaration => declaration.Kind == "command")
                    .Select(declaration => declaration.Execution!.HandlerType).Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new InvalidOperationException("The fixture command routes differ from the installed processor inventory.");
        }
        IIdempotencyIntentAdapter[] installed = services.GetServices<IIdempotencyIntentAdapter>().ToArray();
        IDeclaredIdempotencyIntentAdapter[] adapters = installed.OfType<IDeclaredIdempotencyIntentAdapter>().ToArray();
        if (installed.Length != TimesheetsRouteCatalogDeclarations.Commands.Count
            || adapters.Length != installed.Length
            || adapters.Any(adapter => !TimesheetsRouteCatalogDeclarations.Commands.Any(command =>
                JsonSerializer.Serialize(command) == JsonSerializer.Serialize(adapter.Declaration))))
        {
            throw new InvalidOperationException("The fixture admissions differ from installed owner adapters.");
        }
        Endpoint[] endpoints = [.. services.GetRequiredService<EndpointDataSource>().Endpoints];
        RouteCatalogEntry[] targets = declarations.Select(declaration =>
        {
            string method = declaration.Kind == "command" ? "process" : "project/v2";
            var canonical = EventStoreDomainServiceRoutes.Operational.Single(route => route.Route == "/" + method);
            RouteEndpoint? endpoint = endpoints.OfType<RouteEndpoint>()
                .SingleOrDefault(candidate => candidate.RoutePattern.RawText == "/" + method);
            if (endpoint?.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.SequenceEqual(["POST"], StringComparer.Ordinal) != true
                || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null
                || !endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(policy => policy.Policy == canonical.Policy)
                || declaration.Operation != canonical.Operation)
            {
                throw new InvalidOperationException("The fixture command or projection target differs from the mapped workload receiver.");
            }
            return new RouteCatalogEntry(declaration.RouteId, 1, RouteCatalogCodec.ComputeDeclarationDigest(declaration),
                identity.AppId, method, declaration.ContractVersion, declaration.CredentialKind,
                declaration.Operation, false, "*");
        }).ToArray();
        IdempotencyCatalogEntry[] admissions = adapters
            .Select(adapter => DevelopmentIdempotencyBindings.Bind(adapter.Declaration, 1, identity.AppId)).ToArray();
        RouteCatalogStateCoordinates coordinates = services.GetRequiredService<IRouteCatalogStateStore>().Coordinates!;
        string[] hosts = _hasGateway ? [identity.AppId, services.GetRequiredService<IOptionsMonitor<TimesheetsEventStoreGatewayOptions>>().CurrentValue.AppId]
            : [identity.AppId];
        // These retained documents describe only this in-process fixture, with no sidecar or deployed claims.
        byte[][] topology =
        [
            JsonSerializer.SerializeToUtf8Bytes(new
            {
                kind = "Component", metadata = new { name = coordinates.StateStore },
                spec = new { type = "state.redis", metadata = new[] { new { name = "keyPrefix", value = "none" } } },
                scopes = hosts
            }),
            JsonSerializer.SerializeToUtf8Bytes(new
            {
                spec = new { accessControl = new { defaultAction = "deny", trustDomain = "public" } }
            })
        ];
        WorkloadAuthenticationOptions workload = services.GetRequiredService<IOptionsMonitor<WorkloadAuthenticationOptions>>()
            .Get(EventStoreWorkloadAuthenticationDefaults.WorkloadScheme);
        var validation = services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(EventStoreWorkloadAuthenticationDefaults.WorkloadScheme).TokenValidationParameters;
        return new(identity.AppId, identity.AppId, declarations, targets, hosts, topology,
            JsonSerializer.SerializeToUtf8Bytes(new
            {
                environment = services.GetRequiredService<IHostEnvironment>().EnvironmentName,
                workload.Audience, workload.AllowedCallers, workload.MaximumLifetimeSeconds,
                validation.ValidIssuer, validation.ValidAlgorithms, validation.ValidateLifetime,
                validation.RequireExpirationTime, validation.ClockSkew
            }),
            admissions, RouteCatalogDomainObservation.Capabilities(services),
            RouteCatalogDomainObservation.Auxiliary(services, endpoints),
            coordinates);
    }

    private static RouteCatalogHostObservation ObserveGateway(IServiceProvider services, RouteCatalogHostObservation local)
    {
        string appId = services.GetRequiredService<IOptionsMonitor<TimesheetsEventStoreGatewayOptions>>().CurrentValue.AppId;
        return new(appId, appId, [], [], local.RequiredHostIds, local.TopologyDocuments,
            JsonSerializer.SerializeToUtf8Bytes(new { transport = "CapturingWorkloadGatewayHandler", redirect = false,
                method = "POST", path = "api/v1/commands/workload" }), [], [],
            [new(appId, "api/v1/commands/workload", EventStoreWorkloadOperations.GatewayCommandSubmit, "timesheets")],
            local.AuthorityCoordinates);
    }
}
