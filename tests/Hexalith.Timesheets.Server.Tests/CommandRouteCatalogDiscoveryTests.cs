using System.Reflection;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.Exports;
using Hexalith.Timesheets.Server.MagicLinks.Commands;
using Hexalith.Timesheets.Server.Runtime;
using Hexalith.Timesheets.Server.TimeEntries.Commands;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

/// <summary>Verifies the independent compiled Server declaration source.</summary>
public sealed class CommandRouteCatalogDiscoveryTests
{
    /// <summary>Finds the command and query routes, operation schemas, and receivers from the Server assembly.</summary>
    [Fact]
    public void StandaloneServerCatalogDeclaresExactlyTheInstalledCommandPayloads()
    {
        Assembly owner = typeof(TimesheetsRouteCatalogDeclarations).Assembly;
        owner.GetCustomAttributes<RouteCatalogDeclarationSourceAttribute>().Single().SourceType
            .ShouldBe(typeof(TimesheetsRouteCatalogDeclarations));
        (string Slug, Type Payload)[] expected =
        [
            ("commit-magic-link-issue", typeof(CommitMagicLinkIssue)),
            ("commit-magic-link-transition", typeof(CommitMagicLinkTransition)),
            ("commit-magic-link-use", typeof(CommitMagicLinkUse)),
            ("commit-time-entry-record", typeof(CommitTimeEntryRecord)),
            ("commit-time-entry-confirmation", typeof(CommitTimeEntryConfirmation)),
            ("commit-approved-time-export", typeof(CommitApprovedTimeExport)),
            ("commit-project-time-entry-record", typeof(CommitProjectTimeEntryRecord)),
            ("commit-project-time-entry-submit", typeof(CommitProjectTimeEntrySubmit)),
            ("commit-project-time-entry-approve", typeof(CommitProjectTimeEntryApprove)),
            ("commit-project-time-entry-correct", typeof(CommitProjectTimeEntryCorrect))
        ];
        IReadOnlyList<RouteContractDeclaration> routes = CompiledRouteCatalogDeclarations.Read([owner]);
        RouteContractDeclaration[] commandRoutes = routes.Where(route => route.Kind == "command").ToArray();
        commandRoutes.Select(route => (route.RouteId, route.Execution?.HandlerType))
            .ShouldBe(expected.Select(pair => ("timesheets-" + pair.Slug, pair.Payload.FullName)), ignoreOrder: true);
        commandRoutes.ShouldAllBe(route => route.MessageType == route.Execution!.HandlerType);

        const string LedgerQueryRoute = "timesheets-query-approved-time-ledger";
        RouteContractDeclaration query = routes.Single(route => route.Kind == "query");
        query.RouteId.ShouldBe(LedgerQueryRoute);
        query.MessageType.ShouldBe(nameof(QueryApprovedTimeLedger));
        query.Execution!.HandlerType.ShouldBe(typeof(ApprovedTimeLedgerQueryHandler).FullName);

        var expectedIds = expected.Select(pair => "timesheets-" + pair.Slug).Append(LedgerQueryRoute);
        routes.Select(route => route.RouteId).ShouldBe(expectedIds, ignoreOrder: true);
        CompiledRouteCatalogDeclarations.ReadOperations([owner]).Select(operation => operation.RouteId)
            .ShouldBe(expectedIds, ignoreOrder: true);
        IReadOnlyDictionary<string, string> methods = TimesheetsRouteCatalogDeclarations.ReceiverMethods;
        methods.Keys.ShouldBe(expectedIds, ignoreOrder: true);
        methods[LedgerQueryRoute].ShouldBe("query");
        commandRoutes.ShouldAllBe(route => methods[route.RouteId] == "process");
    }
}
