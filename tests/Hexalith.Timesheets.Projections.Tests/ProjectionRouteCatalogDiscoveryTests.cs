using System.Reflection;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Timesheets.Projections.ActivityTypes;
using Hexalith.Timesheets.Projections.ApprovedTimeLedger;
using Hexalith.Timesheets.Projections.MagicLinks;
using Hexalith.Timesheets.Projections.TimeEntries;

using Shouldly;

namespace Hexalith.Timesheets.Projections.Tests;

/// <summary>Verifies the independent compiled Projections declaration source.</summary>
public sealed class ProjectionRouteCatalogDiscoveryTests
{
    /// <summary>Finds the five named projections and their schemas without a Server reference.</summary>
    [Fact]
    public void StandaloneProjectionCatalogDeclaresOnlyNamedProjectionReceivers()
    {
        Assembly owner = typeof(TimesheetsProjectionRouteCatalogDeclarations).Assembly;
        owner.GetReferencedAssemblies().ShouldNotContain(assembly => assembly.Name == "Hexalith.Timesheets.Server");
        owner.GetCustomAttributes<RouteCatalogDeclarationSourceAttribute>().Single().SourceType
            .ShouldBe(typeof(TimesheetsProjectionRouteCatalogDeclarations));
        TimesheetsProjectionRouteCatalogDeclarations.Commands.ShouldBeEmpty();

        (string Name, Type Handler)[] expected =
        [
            ("approved-time-ledger", typeof(ApprovedTimeLedgerProjectionHandler)),
            ("magic-link-token-hash-capability-index", typeof(MagicLinkTokenHashCapabilityIndexProjectionHandler)),
            ("magic-link-confirmation-capabilities", typeof(MagicLinkConfirmationCapabilityProjectionHandler)),
            ("tenant-activity-type-catalog", typeof(TenantActivityTypeCatalogProjectionHandler)),
            ("time-entry-evidence", typeof(TimeEntryEvidenceProjectionHandler))
        ];
        IReadOnlyList<RouteContractDeclaration> routes = CompiledRouteCatalogDeclarations.Read([owner]);
        routes.Select(route => (route.ProjectionType, route.Execution?.HandlerType))
            .ShouldBe(expected.Select(pair => ((string?)pair.Name, pair.Handler.FullName)), ignoreOrder: true);
        var expectedIds = expected.Select(pair => "timesheets-" + pair.Name);
        routes.Select(route => route.RouteId).ShouldBe(expectedIds, ignoreOrder: true);
        routes.ShouldAllBe(route => route.Kind == "projection");
        CompiledRouteCatalogDeclarations.ReadOperations([owner]).Select(operation => operation.RouteId)
            .ShouldBe(expectedIds, ignoreOrder: true);
        IReadOnlyDictionary<string, string> methods = TimesheetsProjectionRouteCatalogDeclarations.ReceiverMethods;
        methods.Keys.ShouldBe(expectedIds, ignoreOrder: true);
        methods.Values.ShouldAllBe(method => method == "project/v2");
    }
}
