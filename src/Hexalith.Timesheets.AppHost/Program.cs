using System.Security.Cryptography;

using Aspire.Hosting.ApplicationModel;

using Hexalith.EventStore.Aspire;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// This local topology uses a per-run Development signing key so workload assertions can
// bind tenant, domain, and actor claims. Retain the shared security helper for publish mode.
if (builder.ExecutionContext.IsRunMode)
{
    builder.Configuration["EnableKeycloak"] = "false";
}
_ = builder.AddHexalithEventStoreSecurity();

// The Timesheets host publishes two HTTP surfaces that must not share a listener:
//
//   "public"   — the public ingress. It carries the deliberately anonymous magic-link confirm and
//                adjust routes, so it is reachable by external contributors by design.
//   "internal" — the EventStore domain-service routes (/process, /replay-state, /query, /project*,
//                /admin/*). They carry no authorization of their own and can write the cross-tenant
//                token-hash index, the trust-bearing Activity Type catalog, and dispatch domain
//                commands. Declared non-external so it is not published off the pod network.
//
// InternalSurfaceGuard enforces the split inside the host: it refuses the domain-service paths on
// any port other than the one named here, and refuses them everywhere when nothing is configured.
// Declaring the port here is what turns that from an assumption into a wired control.
//
const int PublicPort = 8080;
const int InternalPort = 8081;

// Both endpoints are declared explicitly. This project ships no launchSettings.json, so AddProject
// contributes no endpoint of its own — declaring only the internal one would leave the magic-link
// routes unpublished rather than merely unauthenticated.
IResourceBuilder<ProjectResource> timesheets = builder
    .AddProject<Projects.Hexalith_Timesheets>("timesheets")
    .WithHttpEndpoint(name: "public", port: PublicPort, isProxied: false)
    .WithHttpEndpoint(name: "internal", port: InternalPort, isProxied: false)
    .WithEnvironment(
        "Timesheets__InternalSurface__Port",
        InternalPort.ToString(System.Globalization.CultureInfo.InvariantCulture));

if (builder.ExecutionContext.IsRunMode)
{
    const int EventStorePort = 8082;
    string componentsDirectory = Path.Combine(builder.AppHostDirectory, "DaprComponents");
    string actorStore = Path.Combine(componentsDirectory, "statestore.yaml");
    string timesheetsResources = Path.Combine(componentsDirectory, "Timesheets");
    if (!File.Exists(actorStore) || !File.Exists(Path.Combine(timesheetsResources, "timesheets-readmodels.yaml")))
    {
        throw new FileNotFoundException("Local Timesheets Dapr components are missing.");
    }

    (string? placement, string? scheduler) = AspireDaprLocalServiceEndpoints.Resolve(
        builder.Configuration[AspireDaprLocalServiceEndpoints.PlacementHostAddressKey],
        builder.Configuration[AspireDaprLocalServiceEndpoints.SchedulerHostAddressKey]);

    IResourceBuilder<ProjectResource> eventStore = builder
        .AddHexalithEventStoreGatewayProject()
        .WithEndpoint("http", endpoint => endpoint.Port = EventStorePort)
        .WithEnvironment("EventStore__DomainServices__Registrations__*|timesheets|v1__AppId", "timesheets")
        .WithEnvironment("EventStore__DomainServices__Registrations__*|timesheets|v1__MethodName", "process")
        .WithEnvironment("EventStore__DomainServices__Registrations__*|timesheets|v1__TenantId", "*")
        .WithEnvironment("EventStore__DomainServices__Registrations__*|timesheets|v1__Domain", "timesheets")
        .WithEnvironment("EventStore__DomainServices__Registrations__*|timesheets|v1__Version", "v1")
        .WithEventStoreTrustedEffectSubmitter("timesheets");

    HexalithEventStoreResources resources = builder.AddHexalithEventStore(
        eventStore,
        adminServer: null,
        adminUI: null,
        eventStoreDaprConfigPath: null,
        adminServerDaprConfigPath: null,
        resiliencyConfigPath: null,
        stateStoreComponentPath: actorStore,
        daprPlacementHostAddress: placement,
        daprSchedulerHostAddress: scheduler);

    _ = timesheets.AddEventStoreDomainModule(
        resources,
        "timesheets",
        isolatedDaprResourcesPath: timesheetsResources,
        daprPlacementHostAddress: placement,
        daprSchedulerHostAddress: scheduler,
        appPort: InternalPort);

    string signingKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    IResourceBuilder<ParameterResource> signing = builder.AddParameter(
        "timesheets-local-signing-key",
        () => signingKey,
        secret: true);
    ConfigureDevelopmentWorkload(eventStore, signing, "eventstore");
    ConfigureDevelopmentWorkload(timesheets, signing, "timesheets");
    _ = eventStore.WithGeneratedEventStoreAppChannelToken();
    _ = timesheets.WithGeneratedEventStoreAppChannelToken();
}

await builder
    .Build()
    .RunAsync()
    .ConfigureAwait(false);

static void ConfigureDevelopmentWorkload(
    IResourceBuilder<ProjectResource> resource,
    IResourceBuilder<ParameterResource> signingKey,
    string workload)
{
    _ = resource
        .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
        .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
        .WithEnvironment("Authentication__JwtBearer__Authority", string.Empty)
        .WithEnvironment("Authentication__JwtBearer__Issuer", "hexalith-timesheets-dev")
        .WithEnvironment("Authentication__JwtBearer__Audience", HexalithEventStoreSecurityOptions.DefaultAudience)
        .WithEnvironment("Authentication__JwtBearer__ValidAudiences__0", HexalithEventStoreSecurityOptions.DefaultAudience)
        .WithEnvironment("Authentication__JwtBearer__AllowedAlgorithms__0", "HS256")
        .WithEnvironment("Authentication__JwtBearer__SigningKey", signingKey)
        .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", "false")
        .WithEnvironment("Authentication__WorkloadIssuer__Workload", workload)
        .WithEnvironment("EventStore__Authentication__Authority", string.Empty)
        .WithEnvironment("EventStore__Authentication__Issuer", "hexalith-timesheets-dev")
        .WithEnvironment("EventStore__Authentication__Audience", HexalithEventStoreSecurityOptions.DefaultAudience)
        .WithEnvironment("EventStore__Authentication__SigningKey", signingKey);
}
