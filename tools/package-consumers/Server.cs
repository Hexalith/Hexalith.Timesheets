var decision = Hexalith.Timesheets.Server.Authorization.TimesheetsAuthorizationDecision.Denied("consumer");
if (decision.IsAuthorized || decision.Reason != "consumer") throw new InvalidOperationException("Authorization decision failed");
var commands = Hexalith.Timesheets.Server.Runtime.TimesheetsRouteCatalogDeclarations.Commands;
if (commands.Count != 5 || commands.Count(command => command.Route.RouteId == "timesheets-commit-approved-time-export") != 1)
    throw new InvalidOperationException("Finance receipt route inventory failed");
