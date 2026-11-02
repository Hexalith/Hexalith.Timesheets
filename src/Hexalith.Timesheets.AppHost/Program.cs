using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

using Aspire.Hosting.ApplicationModel;

using Hexalith.EventStore.Aspire;
using Hexalith.Timesheets.AppHost;
using Microsoft.Extensions.Hosting;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

const int DefaultPublicPort = 8080;
const int InternalPort = 8081;
int publicPort = ResolveLocalPublicPort(builder, DefaultPublicPort);

string defaultComponentsDirectory = Path.Combine(builder.AppHostDirectory, "DaprComponents");
string componentsDirectory = defaultComponentsDirectory;
bool fixtureEnabled = builder.Configuration["TIMESHEETS_LIVE_DAPR_FIXTURE"] == "1";
bool securitySmoke = builder.Configuration["TIMESHEETS_LIVE_DAPR_SECURITY_SMOKE"] == "1";
string? fixtureComponents = builder.Configuration["TIMESHEETS_LIVE_DAPR_COMPONENTS"];
string? gatewayExecutable = builder.Configuration["TIMESHEETS_LIVE_DAPR_GATEWAY_EXECUTABLE"];
string? gatewayWorkingDirectory = builder.Configuration["TIMESHEETS_LIVE_DAPR_GATEWAY_WORKING_DIRECTORY"];
string? catalogProfile = builder.Configuration["TIMESHEETS_LIVE_DAPR_CATALOG_PROFILE"];
string? catalogFixtureId = builder.Configuration["TIMESHEETS_LIVE_DAPR_CATALOG_FIXTURE_ID"];
bool catalogEnabled = catalogProfile is not null;
string? gatewayCatalogPolicy = builder.Configuration["TIMESHEETS_LIVE_DAPR_CATALOG_GATEWAY_POLICY"];
string? timesheetsCatalogPolicy = builder.Configuration["TIMESHEETS_LIVE_DAPR_CATALOG_TIMESHEETS_POLICY"];
DevelopmentRouteCatalogCompositionOptions? catalogOptions = null;
IsolatedGatewayLaunch.ValidateBuildMode(gatewayExecutable, gatewayWorkingDirectory);
if ((gatewayExecutable is not null || gatewayWorkingDirectory is not null)
    && (!builder.ExecutionContext.IsRunMode || !fixtureEnabled || string.IsNullOrWhiteSpace(fixtureComponents)))
{
    throw new InvalidOperationException("Compiled gateway launch requires a validated isolated run-mode fixture.");
}
if ((gatewayExecutable is not null || gatewayWorkingDirectory is not null)
    && (string.IsNullOrWhiteSpace(gatewayExecutable) || string.IsNullOrWhiteSpace(gatewayWorkingDirectory)))
{
    throw new InvalidOperationException("Compiled gateway launch requires both executable and working directory.");
}
if (fixtureEnabled || fixtureComponents is not null)
{
    if (!builder.ExecutionContext.IsRunMode || !fixtureEnabled)
    {
        throw new InvalidOperationException("Isolated Dapr components require an explicit run-mode fixture opt-in.");
    }

    if (string.IsNullOrWhiteSpace(fixtureComponents))
    {
        throw new InvalidOperationException("Isolated Dapr fixture requires an explicit component directory.");
    }

    componentsDirectory = Path.GetFullPath(fixtureComponents);
    int redisPort = ValidateFixtureComponents(defaultComponentsDirectory, componentsDirectory);
    if (redisPort == publicPort)
    {
        throw new InvalidOperationException("Local public port is missing or unsafe.");
    }
    ValidateFixtureControlPlane(builder.Configuration[AspireDaprLocalServiceEndpoints.PlacementHostAddressKey],
        builder.Configuration[AspireDaprLocalServiceEndpoints.SchedulerHostAddressKey]);
}
if (builder.ExecutionContext.IsRunMode
    && (!File.Exists(Path.Combine(componentsDirectory, "statestore.yaml"))
        || !File.Exists(Path.Combine(componentsDirectory, "Timesheets", "timesheets-readmodels.yaml"))))
{
    throw new InvalidOperationException("Local Timesheets Dapr components are missing.");
}
if (securitySmoke && (!builder.ExecutionContext.IsRunMode || !fixtureEnabled || string.IsNullOrWhiteSpace(fixtureComponents)))
{
    throw new InvalidOperationException("Security smoke requires a validated isolated run-mode fixture.");
}
if (catalogEnabled || gatewayCatalogPolicy is not null || timesheetsCatalogPolicy is not null || catalogFixtureId is not null)
{
    if (!builder.ExecutionContext.IsRunMode || !builder.Environment.IsDevelopment() || !fixtureEnabled
        || !catalogEnabled || string.IsNullOrWhiteSpace(catalogFixtureId) || string.IsNullOrWhiteSpace(gatewayCatalogPolicy)
        || string.IsNullOrWhiteSpace(timesheetsCatalogPolicy) || securitySmoke)
    {
        throw new InvalidOperationException("Development route catalog requires a complete isolated Development fixture selection.");
    }

    catalogOptions = ValidatePrivateCatalogInput(catalogProfile!, gatewayCatalogPolicy!, timesheetsCatalogPolicy!, catalogFixtureId!,
        componentsDirectory, Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..")));
    if (builder.Configuration["TIMESHEETS_LIVE_DAPR_MODEL_ONLY"] == "1"
        || builder.Configuration["TIMESHEETS_LIVE_DAPR_CONSTRUCTION_ONLY"] == "1")
    {
        throw new InvalidOperationException("Private route catalog selection requires a complete runtime lifecycle.");
    }
}

// The checked-in guard suite uses this exit before constructing any resource.
// Invalid broker/control-plane inputs therefore remain harmless even if a
// validation check regresses. Ordinary selection can be verified the same way.
if (builder.Configuration["TIMESHEETS_LIVE_DAPR_VALIDATE_ONLY"] == "1")
{
    Console.WriteLine(fixtureComponents is null ? "Dapr component selection: checked-in defaults." : "Dapr component selection: isolated fixture.");
    Console.WriteLine(gatewayExecutable is null ? "Gateway launch selection: project." : "Gateway launch selection: compiled executable.");
    Console.WriteLine(securitySmoke ? "Security selection: isolated smoke." : "Security selection: ordinary defaults.");
    Console.WriteLine(catalogEnabled ? "Catalog selection: private isolated Development." : "Catalog selection: ordinary defaults.");
    return;
}

// This local topology uses a per-run Development signing key so workload assertions can
// bind tenant, domain, and actor claims. Retain the shared security helper for publish mode.
DevelopmentRouteCatalogPreparedOwner? preparedOwner = null;
DevelopmentRouteCatalogComposition? routeCatalog = null;
try
{
if (builder.ExecutionContext.IsRunMode)
{
    builder.Configuration["EnableKeycloak"] = securitySmoke ? "true" : "false";
    if (securitySmoke)
    {
        builder.Configuration["KeycloakPersistent"] = "false";
    }
}
HexalithEventStoreSecurityResources? security = builder.AddHexalithEventStoreSecurity();
if (securitySmoke)
{
    // The helper owns realm import and credential generation. The existing fixture
    // binding lets the runner prove ownership without retaining credential values.
    _ = security!.Keycloak.WithEnvironment("TIMESHEETS_LIVE_DAPR_COMPONENTS", componentsDirectory)
        .WithContainerRuntimeArgs("--pull=never", "--label", "hexalith.timesheets.components=" + componentsDirectory);
}

// The Timesheets host declares two local HTTP surfaces that must not share a listener:
//
//   "public"   — local anonymous magic-link confirm and adjust routes. Anonymous route metadata
//                grants no deployment ingress or external network exposure.
//   "internal" — the EventStore domain-service routes (/process, /replay-state, /query, /project*,
//                /admin/*). They carry no authorization of their own and can write the cross-tenant
//                token-hash index, the trust-bearing Activity Type catalog, and dispatch domain
//                commands. The current endpoint model leaves IsExternal false on both endpoints;
//                that publish-time annotation does not prove deployed network isolation.
//
// InternalSurfaceGuard enforces the split inside the host: it refuses the domain-service paths on
// any port other than the one named here, and refuses them everywhere when nothing is configured.
// Declaring the port here is what turns that from an assumption into a wired control.
//
// Both endpoints are declared explicitly. This project ships no launchSettings.json, so AddProject
// contributes no endpoint of its own — declaring only the internal one would leave the magic-link
// routes without a local public listener.
IResourceBuilder<ProjectResource> timesheets = builder
    .AddProject<Projects.Hexalith_Timesheets>("timesheets")
    .WithHttpEndpoint(name: "public", port: publicPort, isProxied: false)
    .WithHttpEndpoint(name: "internal", port: InternalPort, isProxied: false)
    .WithEnvironment(
        "Timesheets__InternalSurface__Port",
        InternalPort.ToString(System.Globalization.CultureInfo.InvariantCulture))
    .WithEnvironment("Timesheets__LocalPublicPort", publicPort.ToString(System.Globalization.CultureInfo.InvariantCulture));

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

    if (fixtureEnabled && gatewayExecutable is not null)
    {
        IsolatedGatewayLaunch.Apply(eventStore, gatewayExecutable, gatewayWorkingDirectory!);
    }

    HexalithEventStoreResources resources = builder.AddHexalithEventStore(
        eventStore,
        adminServer: null,
        adminUI: null,
        eventStoreDaprConfigPath: gatewayCatalogPolicy,
        adminServerDaprConfigPath: null,
        resiliencyConfigPath: null,
        stateStoreComponentPath: actorStore,
        pubSubComponentPath: fixtureComponents is null ? null : Path.Combine(componentsDirectory, "pubsub.yaml"),
        daprPlacementHostAddress: placement,
        daprSchedulerHostAddress: scheduler);

    _ = timesheets.AddEventStoreDomainModule(
        resources,
        "timesheets",
        daprConfigPath: timesheetsCatalogPolicy,
        isolatedDaprResourcesPath: timesheetsResources,
        daprPlacementHostAddress: placement,
        daprSchedulerHostAddress: scheduler,
        appPort: InternalPort);

    if (fixtureEnabled)
    {
        _ = eventStore.WithHttpHealthCheck("/ready", endpointName: "http");
        // Gateway startup discovers this module's metadata. Its /ready check
        // additionally requires fixture protocol activation after HTTP startup.
        // Preserve the SDK reference, replacing only this dependency's wait.
        foreach (WaitAnnotation wait in timesheets.Resource.Annotations.OfType<WaitAnnotation>()
            .Where(wait => ReferenceEquals(wait.Resource, eventStore.Resource)).ToArray())
        {
            timesheets.Resource.Annotations.Remove(wait);
        }

        _ = timesheets.WaitForStart(eventStore);
    }

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
    if (fixtureEnabled)
    {
        _ = timesheets.WithEnvironment("Timesheets__LiveDaprFixture__Enabled", "true");
    }

    if (catalogEnabled)
    {
        preparedOwner = await timesheets.PrepareEventStoreDevelopmentOwnerAsync(
            "Hexalith.Timesheets.Projections.dll", "Hexalith.Timesheets.Server.dll", TimeSpan.FromMinutes(5));
        routeCatalog = builder.AddEventStoreDevelopmentRouteCatalog(eventStore,
            [preparedOwner.Owner],
            [eventStore.CaptureEventStoreDevelopmentReceiver(timesheets),
             timesheets.CaptureEventStoreDevelopmentReceiver(eventStore)],
            actorStore, catalogOptions!);
    }

    // All endpoint-affecting wiring is complete. The model probe does not need
    // generated app-channel secrets or a runnable resource graph.
    if (ReportEndpointModelIfRequested(builder, timesheets))
    {
        return;
    }

    // The catalog SDK binds every receiver's channel credential itself.
    // Ordinary local starts keep their existing single Timesheets credential.
    if (!catalogEnabled)
    {
        _ = timesheets.WithGeneratedEventStoreAppChannelToken();
    }
}

if (ReportEndpointModelIfRequested(builder, timesheets))
{
    return;
}

if (builder.Configuration["TIMESHEETS_LIVE_DAPR_CONSTRUCTION_ONLY"] == "1")
{
    if (!builder.ExecutionContext.IsRunMode)
    {
        throw new InvalidOperationException("Full construction probes require run mode.");
    }

    // Construct and dispose the complete graph, including credential generation.
    // Never start resources. Report counts only; parameter values remain secret.
    SecurityConstructionProbe.ReadinessHandler readiness = SecurityConstructionProbe.ConfigureReadiness(builder);
    await using DistributedApplication application = builder.Build();
    await SecurityConstructionProbe.VerifyReadinessAsync(builder, application.Services, readiness);
    await SecurityConstructionProbe.VerifyAsync(builder, security);
    Console.WriteLine("Timesheets construction model: " + System.Text.Json.JsonSerializer.Serialize(new
    {
        gatewayCredentials = builder.Resources.OfType<ParameterResource>().Count(parameter => parameter.Name == "eventstore-app-api-token"),
        timesheetsCredentials = builder.Resources.OfType<ParameterResource>().Count(parameter => parameter.Name == "timesheets-app-api-token"),
    }));
    return;
}

await using (DistributedApplication application = builder.Build())
{
    await application.RunAsync().ConfigureAwait(false);
}
}
finally
{
    try
    {
        if (routeCatalog is not null)
        {
            await routeCatalog.DisposeAsync();
        }
    }
    finally
    {
        if (preparedOwner is not null)
        {
            await preparedOwner.DisposeAsync();
        }
    }
}

static DevelopmentRouteCatalogCompositionOptions ValidatePrivateCatalogInput(
    string profile, string gatewayPolicy, string timesheetsPolicy, string fixtureId, string componentsDirectory, string workspaceRoot)
{
    string root = Path.GetFullPath(Path.GetDirectoryName(profile)
        ?? throw new InvalidOperationException("Private route catalog profile directory is missing."));
    if (!Regex.IsMatch(fixtureId, "^[0-9a-f]{32}$")
        || Path.GetFileName(profile) != "owner-profile.json"
        || Path.GetFileName(gatewayPolicy) != "gateway-policy.yaml"
        || Path.GetFileName(timesheetsPolicy) != "timesheets-policy.yaml"
        || !Path.GetFileName(root).StartsWith("timesheets-route-catalog-", StringComparison.Ordinal)
        || Path.GetFullPath(Path.GetDirectoryName(root)!) != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))
        || root == Path.GetFullPath(componentsDirectory) || root == workspaceRoot
        || root.StartsWith(workspaceRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)
        || CatalogPathHasLink(root))
    {
        throw new InvalidOperationException("Private route catalog profile is not isolated from source and components.");
    }

    if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(root) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite
        | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
    {
        throw new InvalidOperationException("Private route catalog profile directory is not private.");
    }

    foreach (string path in new[] { profile, gatewayPolicy, timesheetsPolicy })
    {
        if (Path.GetFullPath(Path.GetDirectoryName(path)!) != root || new FileInfo(path).LinkTarget is not null
            || !File.Exists(path) || !OperatingSystem.IsWindows() && (File.GetUnixFileMode(path) &
                (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                 | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
        {
            throw new InvalidOperationException("Private route catalog input is absent or unsafe.");
        }
    }

    if (!string.Equals(File.ReadAllText(gatewayPolicy), ExpectedCatalogPolicy("gateway", "timesheets", [
        ("api/v1/commands/bound/workload", "POST"), ("api/v1/commands/workload", "POST"),
        ("api/v1/commands/status/workload/*", "GET"), ("api/v1/streams/read/workload", "POST")]), StringComparison.Ordinal)
        || !string.Equals(File.ReadAllText(timesheetsPolicy), ExpectedCatalogPolicy("timesheets", "eventstore", [
            ("process", "POST"), ("replay-state", "POST"), ("query", "POST"),
            ("project/v2", "POST"), ("project/rebuild/v1", "POST"),
            ("project/rebuild/shared/v1", "POST"), ("admin/operational-index-metadata", "POST")]), StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Private route catalog receiver policies differ from the closed fixture.");
    }

    DevelopmentRouteCatalogCompositionOptions options = JsonSerializer.Deserialize<DevelopmentRouteCatalogCompositionOptions>(
        File.ReadAllText(profile)) ?? throw new InvalidOperationException("Private route catalog owner profile is unavailable.");
    if (Path.GetFullPath(options.CustodyDirectory) != Path.Combine(root, "custody")
        || options.AuthorityComponentName != "timesheets-route-authority"
        || options.AuthorityStateKey != "timesheets-route-" + fixtureId
        || options.ActiveDigestKeyGeneration != "fixture-v1" || options.SecretMapGeneration != "fixture-v1"
        || options.SecretStore != "secretstore" || options.LogicalSecretName != "eventstore-idempotency-ring"
        || options.DigestKeys is null || options.DigestKeys.Count != 1
        || !options.DigestKeys.TryGetValue("fixture-v1", out string? digestKey)
        || !ValidFixtureDigestKey(digestKey))
    {
        throw new InvalidOperationException("Private route catalog owner profile differs from the selected fixture.");
    }

    return options;
}

static bool CatalogPathHasLink(string path)
{
    for (DirectoryInfo? directory = new(path); directory is not null; directory = directory.Parent)
    {
        if (directory.LinkTarget is not null || (directory.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            return true;
        }
    }
    return false;
}

static bool ValidFixtureDigestKey(string? value)
{
    try { return value is not null && Convert.FromBase64String(value).Length == 32; }
    catch (FormatException) { return false; }
}

static string ExpectedCatalogPolicy(string receiver, string caller, (string Method, string Verb)[] operations)
    => "apiVersion: dapr.io/v1alpha1\nkind: Configuration\nmetadata:\n"
       + "  name: timesheets-catalog-" + receiver + "\nspec:\n"
       + "  accessControl:\n    defaultAction: deny\n    trustDomain: public\n"
       + "    policies:\n      - appId: " + caller + "\n"
       + "        defaultAction: deny\n        trustDomain: public\n"
       + "        namespace: default\n        operations:\n"
       + string.Join("\n", operations.Select(operation => "          - name: " + operation.Method + "\n"
           + "            httpVerb: [" + operation.Verb + "]\n"
           + "            action: allow")) + "\n";

static bool ReportEndpointModelIfRequested(
    IDistributedApplicationBuilder builder,
    IResourceBuilder<ProjectResource> timesheets)
{
    if (builder.Configuration["TIMESHEETS_LIVE_DAPR_MODEL_ONLY"] != "1")
    {
        return false;
    }

    // Only allowlisted nonsensitive endpoint fields leave the model. This is
    // before Build/Run and endpoint allocation, and proves no runtime acceptance.
    Console.WriteLine("Timesheets endpoint model: " + System.Text.Json.JsonSerializer.Serialize(
        timesheets.Resource.Annotations.OfType<EndpointAnnotation>()
            .OrderBy(static endpoint => endpoint.Name, StringComparer.Ordinal)
            .Select(static endpoint => new { name = endpoint.Name, port = endpoint.Port,
                isExternal = endpoint.IsExternal, isExplicitlyProxied = endpoint.IsExplicitlyProxied })));
    return true;
}

static int ResolveLocalPublicPort(IDistributedApplicationBuilder builder, int defaultPort)
{
    string? configured = builder.Configuration["Timesheets:LocalPublicPort"];
    if (configured is not null && !builder.ExecutionContext.IsRunMode)
    {
        throw new InvalidOperationException("Local public port overrides require run mode.");
    }

    int port = defaultPort;
    if (configured is not null && (!Regex.IsMatch(configured, "^[0-9]+$")
        || !int.TryParse(configured, System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out port)
        || port is < 1024 or > 65535 or 8081 or 8082 or 3501 or 6379 or 2379 or 6050 or 6060 or 50005 or 50006))
    {
        throw new InvalidOperationException("Local public port is missing or unsafe.");
    }

    foreach (string key in new[] { AspireDaprLocalServiceEndpoints.PlacementHostAddressKey,
        AspireDaprLocalServiceEndpoints.SchedulerHostAddressKey })
    {
        string? endpoint = builder.Configuration[key]?.Trim();
        if (endpoint is not null
            && int.TryParse(endpoint.AsSpan(endpoint.LastIndexOf(':') + 1), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out int infrastructurePort)
            && infrastructurePort == port)
        {
            throw new InvalidOperationException("Local public port is missing or unsafe.");
        }
    }

    return port;
}

// Fixture files are exact copies of the tracked defaults with only redisHost replaced.
// Comparing the remaining text preserves component names, scopes, database separation,
// and credential/key-prefix settings without adding a YAML dependency or parser dialect.
static int ValidateFixtureComponents(string defaults, string fixture)
{
    string[] relativePaths = ["statestore.yaml", Path.Combine("Timesheets", "timesheets-readmodels.yaml"), "pubsub.yaml"];
    string? endpoint = null;
    int redisPort = 0;
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
            redisPort = port;
            string normalized = contents.Remove(match.Index, match.Length).Insert(
                match.Index, "    - name: redisHost\n      value: \"localhost:6379\"");
            if (!string.Equals(expected, normalized, StringComparison.Ordinal))
            {
                throw new InvalidOperationException();
            }
        }
        return redisPort;
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
        Match match = Regex.Match(address?.Trim() ?? string.Empty, "^127\\.0\\.0\\.1:([0-9]{1,5})$");
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
