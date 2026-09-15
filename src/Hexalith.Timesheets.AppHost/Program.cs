using Hexalith.EventStore.Aspire;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// Initialize the shared local security service through the EventStore Aspire helper.
// Full Timesheets runtime topology remains owned by later infrastructure stories.
_ = builder.AddHexalithEventStoreSecurity();

// The Timesheets host publishes two HTTP surfaces that must not share a listener:
//
//   "http"     — the public ingress. It carries the deliberately anonymous magic-link confirm and
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
// This host has no EventStore resource to reach yet (that is the deferred infrastructure story), so
// under `aspire start` it comes up and fails closed rather than serving magic links.
const int InternalPort = 8081;

_ = builder
    .AddProject<Projects.Hexalith_Timesheets>("timesheets")
    .WithHttpEndpoint(name: "internal", port: InternalPort, isProxied: false)
    .WithEnvironment("Timesheets__InternalSurface__Port", InternalPort.ToString(System.Globalization.CultureInfo.InvariantCulture));

await builder
    .Build()
    .RunAsync()
    .ConfigureAwait(false);
