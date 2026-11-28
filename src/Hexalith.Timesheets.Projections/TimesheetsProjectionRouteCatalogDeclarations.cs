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

/// <summary>Declares the named projections owned by the Timesheets Projections assembly.</summary>
/// <remarks>
/// Starting in Timesheets 3.0.0, discover both the Server and Projections declaration assemblies
/// or use the composed HTTP host declaration assembly to obtain the complete Timesheets inventory.
/// </remarks>
public static class TimesheetsProjectionRouteCatalogDeclarations
{
    /// <summary>Gets the empty command inventory owned by Projections.</summary>
    /// <remarks>Starting in Timesheets 3.0.0, discover command policies from the Server or composed HTTP host declaration source.</remarks>
    public static IReadOnlyList<CommandIntentDeclaration> Commands => [];

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

    /// <summary>Gets the compiled named projection routes.</summary>
    public static IReadOnlyList<RouteContractDeclaration> Routes => Projections;

    /// <summary>Gets the value-free operation schemas for named projection receivers.</summary>
    public static IReadOnlyList<RouteOperationContract> Operations =>
        [.. Projections.Select(route => RouteOperationSchema.Create(route,
            typeof(ProjectionDispatchRequest), typeof(ProjectionDispatchResponse),
            "internal", "UI-only", JsonSerializerOptions.Web))];

    /// <summary>Gets the canonical receiver method for every named projection route.</summary>
    public static IReadOnlyDictionary<string, string> ReceiverMethods =>
        Projections.ToDictionary(route => route.RouteId, _ => "project/v2", StringComparer.Ordinal);

    private static RouteContractDeclaration Projection<THandler>(string slug, string projectionName) =>
        new($"timesheets-{slug}", "projection", TimesheetsDomain.Name, null, projectionName,
            "v1", null, "workload", EventStoreOperationIdentifiers.DomainServiceProject, null, [],
            new(typeof(THandler).FullName!, "exact", [projectionName], "domain-service", null, [projectionName]));
}
