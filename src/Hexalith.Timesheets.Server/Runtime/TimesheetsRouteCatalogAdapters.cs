using Hexalith.EventStore.DomainService;

[assembly: RouteCatalogAdapterSource(typeof(Hexalith.Timesheets.Server.Runtime.TimesheetsRouteCatalogAdapters))]

namespace Hexalith.Timesheets.Server.Runtime;

/// <summary>Exposes the exact owner adapters to the SDK run-scoped catalog loader.</summary>
public static class TimesheetsRouteCatalogAdapters
{
    /// <summary>Creates the four declared Timesheets semantic intent adapters.</summary>
    public static IReadOnlyList<IDeclaredIdempotencyIntentAdapter> Create() =>
        TimesheetsRouteCatalogDeclarations.CreateAdapters();
}
