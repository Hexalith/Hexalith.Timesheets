using System.Security.Cryptography;
using System.Text.RegularExpressions;

using Aspire.Hosting.ApplicationModel;

using Hexalith.EventStore.Aspire;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

string defaultComponentsDirectory = Path.Combine(builder.AppHostDirectory, "DaprComponents");
string componentsDirectory = defaultComponentsDirectory;
string? fixtureComponents = builder.Configuration["TIMESHEETS_LIVE_DAPR_COMPONENTS"];
if (fixtureComponents is not null)
{
    if (!builder.ExecutionContext.IsRunMode || builder.Configuration["TIMESHEETS_LIVE_DAPR_FIXTURE"] != "1"
        || string.IsNullOrWhiteSpace(fixtureComponents))
    {
        throw new InvalidOperationException("Isolated Dapr components require an explicit run-mode fixture opt-in.");
    }

    componentsDirectory = Path.GetFullPath(fixtureComponents);
    ValidateFixtureComponents(defaultComponentsDirectory, componentsDirectory);
    ValidateFixtureControlPlane(builder.Configuration[AspireDaprLocalServiceEndpoints.PlacementHostAddressKey],
        builder.Configuration[AspireDaprLocalServiceEndpoints.SchedulerHostAddressKey]);
}
if (builder.ExecutionContext.IsRunMode
    && (!File.Exists(Path.Combine(componentsDirectory, "statestore.yaml"))
        || !File.Exists(Path.Combine(componentsDirectory, "Timesheets", "timesheets-readmodels.yaml"))))
{
    throw new InvalidOperationException("Local Timesheets Dapr components are missing.");
}

// The checked-in guard suite uses this exit before constructing any resource.
// Invalid broker/control-plane inputs therefore remain harmless even if a
// validation check regresses. Ordinary selection can be verified the same way.
if (builder.Configuration["TIMESHEETS_LIVE_DAPR_VALIDATE_ONLY"] == "1")
{
    Console.WriteLine(fixtureComponents is null ? "Dapr component selection: checked-in defaults." : "Dapr component selection: isolated fixture.");
    return;
}

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
    string actorStore = Path.Combine(componentsDirectory, "statestore.yaml");
    string timesheetsResources = Path.Combine(componentsDirectory, "Timesheets");

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
        pubSubComponentPath: fixtureComponents is null ? null : Path.Combine(componentsDirectory, "pubsub.yaml"),
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
    if (fixtureComponents is not null)
    {
        // Bind child processes to this invocation so cleanup can prove resource exit.
        _ = eventStore.WithEnvironment("TIMESHEETS_LIVE_DAPR_COMPONENTS", componentsDirectory);
        _ = timesheets.WithEnvironment("TIMESHEETS_LIVE_DAPR_COMPONENTS", componentsDirectory);
    }
    if (builder.Configuration["TIMESHEETS_LIVE_DAPR_FIXTURE"] == "1")
    {
        _ = timesheets.WithEnvironment("Timesheets__LiveDaprFixture__Enabled", "true");
    }

    _ = eventStore.WithGeneratedEventStoreAppChannelToken();
    _ = timesheets.WithGeneratedEventStoreAppChannelToken();
}

await builder
    .Build()
    .RunAsync()
    .ConfigureAwait(false);

// Fixture files are exact copies of the tracked defaults with only redisHost replaced.
// Comparing the remaining text preserves component names, scopes, database separation,
// and credential/key-prefix settings without adding a YAML dependency or parser dialect.
static void ValidateFixtureComponents(string defaults, string fixture)
{
    string[] relativePaths = ["statestore.yaml", Path.Combine("Timesheets", "timesheets-readmodels.yaml"), "pubsub.yaml"];
    string? endpoint = null;
    try
    {
        string[] actual = Directory.GetFiles(fixture, "*", SearchOption.AllDirectories);
        if (actual.Length != relativePaths.Length || actual.Any(path => new FileInfo(path).LinkTarget is not null)
            || new DirectoryInfo(fixture).LinkTarget is not null
            || new DirectoryInfo(Path.Combine(fixture, "Timesheets")).LinkTarget is not null)
        {
            throw new InvalidOperationException();
        }

        foreach (string relativePath in relativePaths)
        {
            string expected = relativePath == "pubsub.yaml" ? FixturePubSubTemplate() : File.ReadAllText(Path.Combine(defaults, relativePath));
            string contents = File.ReadAllText(Path.Combine(fixture, relativePath));
            Match match = Regex.Match(contents, "(?m)^    - name: redisHost\\n      value: \"(127\\.0\\.0\\.1:([0-9]{1,5}))\"$");
            if (!match.Success || !int.TryParse(match.Groups[2].Value, out int port)
                || port is < 1024 or > 65535 || port == 6379
                || (endpoint is not null && endpoint != match.Groups[1].Value))
            {
                throw new InvalidOperationException();
            }

            endpoint = match.Groups[1].Value;
            string normalized = contents.Remove(match.Index, match.Length).Insert(
                match.Index, "    - name: redisHost\n      value: \"localhost:6379\"");
            if (!string.Equals(expected, normalized, StringComparison.Ordinal))
            {
                throw new InvalidOperationException();
            }
        }
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
    {
        throw new InvalidOperationException("Isolated Dapr components are missing or unsafe.");
    }
}

static string FixturePubSubTemplate() => """
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: pubsub
spec:
  type: pubsub.redis
  version: v1
  metadata:
    - name: redisHost
      value: "localhost:6379"
    - name: redisPassword
      value: ""
scopes:
  - eventstore
""" + "\n";

static void ValidateFixtureControlPlane(string? placement, string? scheduler)
{
    string?[] addresses = [placement, scheduler];
    HashSet<int> ports = [];
    foreach (string? address in addresses)
    {
        Match match = Regex.Match(address ?? string.Empty, "^127\\.0\\.0\\.1:([0-9]{1,5})$");
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out int port)
            || port is < 1024 or > 65535 || port is 6050 or 6060 or 50005 or 50006
            || !ports.Add(port))
        {
            throw new InvalidOperationException("Isolated Dapr placement and scheduler endpoints are missing or unsafe.");
        }
    }
}

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
