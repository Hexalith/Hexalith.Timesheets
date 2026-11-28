using System.Reflection;
using System.Runtime.Loader;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Timesheets.Projections;

const string ServerAssembly = "Hexalith.Timesheets.Server";
int serverLoaded = 0;
AppDomain.CurrentDomain.AssemblyLoad += ObserveAssemblyLoad;

string[] projectionNames =
[
    "approved-time-ledger", "magic-link-confirmation-capabilities", "magic-link-token-hash-capability-index",
    "tenant-activity-type-catalog", "time-entry-evidence"
];
string[] expectedRouteIds = projectionNames.Select(name => "timesheets-" + name).ToArray();
Assembly projectionOwner = typeof(TimesheetsProjectionRouteCatalogDeclarations).Assembly;
if (projectionOwner.GetReferencedAssemblies().Any(assembly => assembly.Name == ServerAssembly)
    || projectionOwner.GetCustomAttribute<RouteCatalogDeclarationSourceAttribute>()?.SourceType
        != typeof(TimesheetsProjectionRouteCatalogDeclarations))
{
    throw new InvalidOperationException("The Projections package must independently own its compiled declarations.");
}

var projectionRoutes = TimesheetsProjectionRouteCatalogDeclarations.Routes;
var projectionOperations = TimesheetsProjectionRouteCatalogDeclarations.Operations;
var projectionMethods = TimesheetsProjectionRouteCatalogDeclarations.ReceiverMethods;
var projectionHandlers = projectionOwner.GetTypes().Where(type =>
    type.IsClass && !type.IsAbstract && typeof(IAsyncDomainProjectionHandler).IsAssignableFrom(type));
if (TimesheetsProjectionRouteCatalogDeclarations.Commands.Count != 0
    || !Matches(TimesheetsProjectionRouteCatalogDeclarations.Projections.Select(route => route.ProjectionType!), projectionNames)
    || !Matches(projectionRoutes.Select(route => route.RouteId), expectedRouteIds)
    || !Matches(projectionRoutes.Select(route => route.Execution!.HandlerType), projectionHandlers.Select(type => type.FullName!))
    || projectionRoutes.Any(route => route.Kind != "projection" || route.RouteId != "timesheets-" + route.ProjectionType)
    || !Matches(projectionOperations.Select(operation => operation.RouteId), expectedRouteIds)
    || !Matches(projectionMethods.Keys, expectedRouteIds)
    || projectionMethods.Values.Any(method => method != "project/v2")
    || !Matches(CompiledRouteCatalogDeclarations.Read([projectionOwner]).Select(route => route.RouteId), expectedRouteIds)
    || !Matches(CompiledRouteCatalogDeclarations.ReadOperations([projectionOwner]).Select(operation => operation.RouteId), expectedRouteIds))
{
    throw new InvalidOperationException("The standalone projection catalog differs from the five named handlers.");
}

var checkpoint = new Hexalith.Timesheets.Projections.TimesheetsProjectionCheckpoint(
    "consumer", "test", 7, Hexalith.Timesheets.Projections.ProjectionFreshness.Fresh);
var metadata = Hexalith.Timesheets.Projections.ProjectionFreshnessMetadataMapper.ToMetadata(checkpoint);
if (!checkpoint.CanServeReads || metadata.Cursor != "7") throw new InvalidOperationException("Projection mapping failed");
AppDomain.CurrentDomain.AssemblyLoad -= ObserveAssemblyLoad;
if (Volatile.Read(ref serverLoaded) != 0
    || AssemblyLoadContext.All.SelectMany(context => context.Assemblies).Any(assembly => assembly.GetName().Name == ServerAssembly))
{
    throw new InvalidOperationException("The Projections-only consumer loaded Timesheets Server.");
}

void ObserveAssemblyLoad(object? sender, AssemblyLoadEventArgs args)
{
    if (args.LoadedAssembly.GetName().Name == ServerAssembly)
    {
        Interlocked.Exchange(ref serverLoaded, 1);
    }
}

static bool Matches(IEnumerable<string> actual, IEnumerable<string> expected) =>
    actual.Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal), StringComparer.Ordinal);
