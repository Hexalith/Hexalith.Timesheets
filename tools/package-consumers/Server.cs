using System.Reflection;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Timesheets.Server.Runtime;

var decision = Hexalith.Timesheets.Server.Authorization.TimesheetsAuthorizationDecision.Denied("consumer");
if (decision.IsAuthorized || decision.Reason != "consumer") throw new InvalidOperationException("Authorization decision failed");

string[] expectedRouteIds =
[
    "timesheets-commit-approved-time-export", "timesheets-commit-magic-link-issue",
    "timesheets-commit-magic-link-transition", "timesheets-commit-magic-link-use",
    "timesheets-commit-time-entry-confirmation", "timesheets-commit-time-entry-record"
];
Assembly commandOwner = typeof(TimesheetsRouteCatalogDeclarations).Assembly;
var commandRoutes = CompiledRouteCatalogDeclarations.Read([commandOwner]);
var commandOperations = CompiledRouteCatalogDeclarations.ReadOperations([commandOwner]);
var commandMethods = TimesheetsRouteCatalogDeclarations.ReceiverMethods;
if (commandOwner.GetCustomAttribute<RouteCatalogDeclarationSourceAttribute>()?.SourceType != typeof(TimesheetsRouteCatalogDeclarations)
    || !Matches(TimesheetsRouteCatalogDeclarations.Commands.Select(command => command.Route.RouteId), expectedRouteIds)
    || !Matches(commandRoutes.Select(route => route.RouteId), expectedRouteIds)
    || !Matches(commandRoutes.Select(route => route.MessageType!), TimesheetsRouteCatalogDeclarations.PayloadTypes.Select(type => type.FullName!))
    || commandRoutes.Any(route => route.Kind != "command" || route.Execution?.HandlerType != route.MessageType)
    || !Matches(commandOperations.Select(operation => operation.RouteId), expectedRouteIds)
    || !Matches(commandMethods.Keys, expectedRouteIds)
    || commandMethods.Values.Any(method => method != "process"))
{
    throw new InvalidOperationException("The standalone command catalog differs from the six installed command payloads.");
}

static bool Matches(IEnumerable<string> actual, IEnumerable<string> expected) =>
    actual.Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal), StringComparer.Ordinal);
