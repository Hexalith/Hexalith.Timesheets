using System.Reflection;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Timesheets.Server.Exports;
using Hexalith.Timesheets.Server.MagicLinks.Commands;
using Hexalith.Timesheets.Server.Runtime;
using Hexalith.Timesheets.Server.TimeEntries.Commands;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

/// <summary>Verifies the independent compiled Server declaration source.</summary>
public sealed class CommandRouteCatalogDiscoveryTests
{
    /// <summary>Finds all six command routes, operation schemas, and process receivers from the Server assembly.</summary>
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
            ("commit-approved-time-export", typeof(CommitApprovedTimeExport))
        ];
        IReadOnlyList<RouteContractDeclaration> routes = CompiledRouteCatalogDeclarations.Read([owner]);
        routes.Select(route => (route.RouteId, route.Execution?.HandlerType))
            .ShouldBe(expected.Select(pair => ("timesheets-" + pair.Slug, pair.Payload.FullName)), ignoreOrder: true);
        routes.ShouldAllBe(route => route.Kind == "command" && route.MessageType == route.Execution!.HandlerType);
        var expectedIds = expected.Select(pair => "timesheets-" + pair.Slug);
        CompiledRouteCatalogDeclarations.ReadOperations([owner]).Select(operation => operation.RouteId)
            .ShouldBe(expectedIds, ignoreOrder: true);
        IReadOnlyDictionary<string, string> methods = TimesheetsRouteCatalogDeclarations.ReceiverMethods;
        methods.Keys.ShouldBe(expectedIds, ignoreOrder: true);
        methods.Values.ShouldAllBe(method => method == "process");
    }
}
