using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.Timesheets.Contracts;
using Hexalith.Timesheets.Projections.ActivityTypes;
using Hexalith.Timesheets.Projections.MagicLinks;
using Hexalith.Timesheets.Projections.TimeEntries;
using Hexalith.Timesheets.Server.MagicLinks;

[assembly: RouteCatalogDeclarationSource(typeof(Hexalith.Timesheets.Projections.TimesheetsProjectionRouteCatalogDeclarations))]

namespace Hexalith.Timesheets.Projections;

/// <summary>Complete Timesheets owner inventory for four commands and four installed named projections.</summary>
public static class TimesheetsProjectionRouteCatalogDeclarations
{
    /// <summary>Gets the installed Server command policies from the selected host assembly context.</summary>
    public static IReadOnlyList<CommandIntentDeclaration> Commands =>
        ReadServerOwner<IReadOnlyList<CommandIntentDeclaration>>("Commands");

    /// <summary>Gets the complete named projection route declarations.</summary>
    public static IReadOnlyList<RouteContractDeclaration> Projections { get; } =
    [
        Projection<MagicLinkTokenHashCapabilityIndexProjectionHandler>(
            "magic-link-token-hash-capability-index", MagicLinkTokenHashCapabilityIndexProjection.ProjectionName),
        Projection<MagicLinkConfirmationCapabilityProjectionHandler>(
            "magic-link-confirmation-capabilities", MagicLinkConfirmationCapabilityProjection.ProjectionName),
        Projection<TenantActivityTypeCatalogProjectionHandler>(
            "tenant-activity-type-catalog", TenantActivityTypeCatalogProjection.ProjectionName),
        Projection<TimeEntryEvidenceProjectionHandler>(
            "time-entry-evidence", TimeEntryEvidenceProjection.ProjectionName)
    ];

    /// <summary>Gets all eight compiled owner routes.</summary>
    public static IReadOnlyList<RouteContractDeclaration> Routes =>
        [.. Commands.Select(command => command.Route), .. Projections];

    /// <summary>Gets value-free operation schemas for all eight protected receivers.</summary>
    public static IReadOnlyList<RouteOperationContract> Operations =>
        [.. ReadServerOwner<IReadOnlyList<RouteOperationContract>>("Operations"),
            .. Projections.Select(route => RouteOperationSchema.Create(route,
            typeof(ProjectionDispatchRequest), typeof(ProjectionDispatchResponse),
            "internal", "UI-only", JsonSerializerOptions.Web))];

    /// <summary>Gets the canonical receiver method for every owner route.</summary>
    public static IReadOnlyDictionary<string, string> ReceiverMethods =>
        Routes.ToDictionary(route => route.RouteId,
            route => route.Kind == "command" ? "process" : "project/v2", StringComparer.Ordinal);

    private static T ReadServerOwner<T>(string property)
    {
        AssemblyLoadContext context = AssemblyLoadContext.GetLoadContext(typeof(TimesheetsProjectionRouteCatalogDeclarations).Assembly)
            ?? throw new InvalidOperationException("The selected Timesheets owner assembly context is unavailable.");
        Type source = context.LoadFromAssemblyName(new AssemblyName("Hexalith.Timesheets.Server"))
            .GetType("Hexalith.Timesheets.Server.Runtime.TimesheetsRouteCatalogDeclarations", throwOnError: true)!;
        return source.GetProperty(property, BindingFlags.Public | BindingFlags.Static)?.GetValue(null) is T inventory
            ? inventory : throw new InvalidOperationException("The installed Timesheets Server owner inventory is incomplete.");
    }

    private static RouteContractDeclaration Projection<THandler>(string slug, string projectionName) =>
        new($"timesheets-{slug}", "projection", TimesheetsDomain.Name, null, projectionName,
            "v1", null, "workload", EventStoreOperationIdentifiers.DomainServiceProject, null, [],
            new(typeof(THandler).FullName!, "exact", [projectionName], "domain-service", null, [projectionName]));
}
