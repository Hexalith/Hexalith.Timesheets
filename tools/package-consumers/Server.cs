using System.Reflection;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.Runtime;

var decision = Hexalith.Timesheets.Server.Authorization.TimesheetsAuthorizationDecision.Denied("consumer");
if (decision.IsAuthorized || decision.Reason != "consumer") throw new InvalidOperationException("Authorization decision failed");

const string LedgerQueryRoute = "timesheets-query-approved-time-ledger";
string[] expectedCommandIds =
[
    "timesheets-commit-approved-time-export", "timesheets-commit-magic-link-issue",
    "timesheets-commit-magic-link-transition", "timesheets-commit-magic-link-use",
    "timesheets-commit-time-entry-confirmation", "timesheets-commit-time-entry-record",
    "timesheets-commit-project-time-entry-record", "timesheets-commit-project-time-entry-submit",
    "timesheets-commit-project-time-entry-approve", "timesheets-commit-project-time-entry-correct"
];
var expectedRouteIds = expectedCommandIds.Append(LedgerQueryRoute);
Assembly commandOwner = typeof(TimesheetsRouteCatalogDeclarations).Assembly;
var routes = CompiledRouteCatalogDeclarations.Read([commandOwner]);
var operations = CompiledRouteCatalogDeclarations.ReadOperations([commandOwner]);
var methods = TimesheetsRouteCatalogDeclarations.ReceiverMethods;
var commandRoutes = routes.Where(route => route.Kind == "command").ToArray();
var ledgerQuery = routes.Single(route => route.Kind == "query");
if (commandOwner.GetCustomAttribute<RouteCatalogDeclarationSourceAttribute>()?.SourceType != typeof(TimesheetsRouteCatalogDeclarations)
    || !Matches(TimesheetsRouteCatalogDeclarations.Commands.Select(command => command.Route.RouteId), expectedCommandIds)
    || !Matches(commandRoutes.Select(route => route.RouteId), expectedCommandIds)
    || !Matches(routes.Select(route => route.RouteId), expectedRouteIds)
    || !Matches(commandRoutes.Select(route => route.MessageType!), TimesheetsRouteCatalogDeclarations.PayloadTypes.Select(type => type.FullName!))
    || commandRoutes.Any(route => route.Execution?.HandlerType != route.MessageType || methods[route.RouteId] != "process")
    || ledgerQuery.RouteId != LedgerQueryRoute || ledgerQuery.MessageType != nameof(QueryApprovedTimeLedger)
    || ledgerQuery.Execution?.HandlerType != typeof(ApprovedTimeLedgerQueryHandler).FullName
    || methods[LedgerQueryRoute] != "query"
    || !Matches(operations.Select(operation => operation.RouteId), expectedRouteIds)
    || !Matches(methods.Keys, expectedRouteIds))
{
    throw new InvalidOperationException("The standalone Server catalog differs from its command and query receivers.");
}

static bool Matches(IEnumerable<string> actual, IEnumerable<string> expected) =>
    actual.Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal), StringComparer.Ordinal);
