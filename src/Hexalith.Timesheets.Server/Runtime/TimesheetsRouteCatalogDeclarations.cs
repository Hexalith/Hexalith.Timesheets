using System.Text.Json;

using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.DomainService;
using Hexalith.Timesheets.Server.MagicLinks.Commands;
using Hexalith.Timesheets.Server.TimeEntries.Commands;

[assembly: RouteCatalogDeclarationSource(typeof(Hexalith.Timesheets.Server.Runtime.TimesheetsRouteCatalogDeclarations))]

namespace Hexalith.Timesheets.Server.Runtime;

/// <summary>Exact internal Timesheets command inventory and owner intent policies.</summary>
public static class TimesheetsRouteCatalogDeclarations
{
    /// <summary>Gets the four payloads accepted by the keyed domain processor.</summary>
    public static IReadOnlyList<Type> PayloadTypes { get; } =
        [typeof(CommitMagicLinkIssue), typeof(CommitMagicLinkTransition), typeof(CommitMagicLinkUse), typeof(CommitTimeEntryRecord)];

    /// <summary>Gets the complete internal command policies.</summary>
    public static IReadOnlyList<CommandIntentDeclaration> Commands { get; } =
        [Command<CommitMagicLinkIssue>("commit-magic-link-issue"),
            Command<CommitMagicLinkTransition>("commit-magic-link-transition"),
            Command<CommitMagicLinkUse>("commit-magic-link-use"),
            Command<CommitTimeEntryRecord>("commit-time-entry-record")];

    /// <summary>Gets the compiled owner routes.</summary>
    public static IReadOnlyList<RouteContractDeclaration> Routes => [.. Commands.Select(command => command.Route)];

    /// <summary>Gets the value-free schemas for the four internal workload operations.</summary>
    public static IReadOnlyList<RouteOperationContract> Operations =>
        [.. Routes.Select(route => RouteOperationSchema.Create(route,
            PayloadTypes.Single(type => type.FullName == route.Execution!.HandlerType),
            typeof(DomainServiceWireResult), "internal", "UI-only", JsonSerializerOptions.Web))];

    /// <summary>Gets the exact receiver methods for these routes.</summary>
    public static IReadOnlyDictionary<string, string> ReceiverMethods =>
        Routes.ToDictionary(route => route.RouteId, _ => "process", StringComparer.Ordinal);

    /// <summary>Creates adapters for the complete payload, including every authority-bearing field.</summary>
    public static IReadOnlyList<IDeclaredIdempotencyIntentAdapter> CreateAdapters() =>
        [Adapter<CommitMagicLinkIssue>(), Adapter<CommitMagicLinkTransition>(),
            Adapter<CommitMagicLinkUse>(), Adapter<CommitTimeEntryRecord>()];

    private static CommandIntentDeclaration Command<T>(string slug)
    {
        return new(new($"timesheets-{slug}", "command", TimesheetsEventStoreIntegration.DomainName,
                typeof(T).FullName!, null, "v1", 1, "workload", EventStoreOperationIdentifiers.DomainServiceProcess,
                null, [], new(typeof(T).FullName!, "exact", [typeof(T).FullName!], "domain-service", null, [])),
            $"timesheets:{slug}:intent-v1", $"timesheets:{slug}", $"timesheets:{slug}:policy-v1",
            new($"timesheets:{slug}:descriptor-v1", 1, [new("target", "string"), new("payload", "string")]),
            IdempotencyReplayRetentionTier.Mutation, false);
    }

    private static IDeclaredIdempotencyIntentAdapter Adapter<T>() =>
        new DeclaredIdempotencyIntentAdapter<T>(
            Commands.Single(command => command.Route.Execution!.HandlerType == typeof(T).FullName),
            payload => new Dictionary<string, object?> { ["payload"] = JsonSerializer.Serialize(payload, JsonSerializerOptions.Web) });
}
