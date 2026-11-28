using System.Reflection;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Server.Runtime;

[assembly: RouteCatalogDeclarationSource(typeof(Hexalith.Timesheets.Runtime.TimesheetsHostRouteCatalogDeclarations))]

namespace Hexalith.Timesheets.Runtime;

/// <summary>Exposes the complete Timesheets owner inventory for single-assembly HTTP host discovery.</summary>
public static class TimesheetsHostRouteCatalogDeclarations
{
    private static readonly Assembly[] _owners =
    [
        typeof(TimesheetsRouteCatalogDeclarations).Assembly,
        typeof(TimesheetsProjectionRouteCatalogDeclarations).Assembly
    ];

    /// <summary>Gets the command intent policies for the installed Timesheets commands.</summary>
    public static IReadOnlyList<CommandIntentDeclaration> Commands => TimesheetsRouteCatalogDeclarations.Commands;

    /// <summary>Gets the distinct compiled command and named projection routes.</summary>
    public static IReadOnlyList<RouteContractDeclaration> Routes =>
        RequireComplete(CompiledRouteCatalogDeclarations.Read(_owners));

    /// <summary>Gets the compiled operation schemas for the composed routes.</summary>
    public static IReadOnlyList<RouteOperationContract> Operations =>
        RequireComplete(CompiledRouteCatalogDeclarations.ReadOperations(_owners));

    /// <summary>Gets the receiver method for each composed route.</summary>
    public static IReadOnlyDictionary<string, string> ReceiverMethods
    {
        get
        {
            var methods = TimesheetsRouteCatalogDeclarations.ReceiverMethods
                .Concat(TimesheetsProjectionRouteCatalogDeclarations.ReceiverMethods).ToArray();
            if (!Routes.Select(route => route.RouteId).Order(StringComparer.Ordinal)
                .SequenceEqual(methods.Select(method => method.Key).Order(StringComparer.Ordinal), StringComparer.Ordinal))
            {
                throw new RouteCatalogUnavailableException("The composed Timesheets owner receiver inventory differs from its routes.");
            }

            return methods.ToDictionary(method => method.Key, method => method.Value, StringComparer.Ordinal);
        }
    }

    private static IReadOnlyList<T> RequireComplete<T>(IReadOnlyList<T> inventory)
    {
        if (inventory.Count != TimesheetsRouteCatalogDeclarations.Routes.Count + TimesheetsProjectionRouteCatalogDeclarations.Projections.Count)
        {
            throw new RouteCatalogUnavailableException("The composed Timesheets owner inventory is incomplete.");
        }

        return inventory;
    }
}
