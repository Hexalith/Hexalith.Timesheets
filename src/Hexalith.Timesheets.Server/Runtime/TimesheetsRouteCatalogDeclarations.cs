using System.Text.Json;

using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.DomainService;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.Exports;
using Hexalith.Timesheets.Server.TimeEntries;
using Hexalith.Timesheets.Server.MagicLinks.Commands;
using Hexalith.Timesheets.Server.TimeEntries.Commands;

[assembly: RouteCatalogDeclarationSource(typeof(Hexalith.Timesheets.Server.Runtime.TimesheetsRouteCatalogDeclarations))]

namespace Hexalith.Timesheets.Server.Runtime;

/// <summary>Exact internal Timesheets command inventory and owner intent policies.</summary>
public static class TimesheetsRouteCatalogDeclarations
{
    /// <summary>Gets the payloads accepted by the keyed domain processor.</summary>
    public static IReadOnlyList<Type> PayloadTypes { get; } =
        [typeof(CommitMagicLinkIssue), typeof(CommitMagicLinkTransition), typeof(CommitMagicLinkUse), typeof(CommitTimeEntryRecord),
            typeof(CommitTimeEntryConfirmation), typeof(CommitApprovedTimeExport), .. ProjectTimeEntryEventStoreReceiver.PayloadTypes];

    /// <summary>Gets the complete internal command policies.</summary>
    public static IReadOnlyList<CommandIntentDeclaration> Commands { get; } =
        [Command<CommitMagicLinkIssue>("commit-magic-link-issue"),
            Command<CommitMagicLinkTransition>("commit-magic-link-transition"),
            Command<CommitMagicLinkUse>("commit-magic-link-use"),
            Command<CommitTimeEntryRecord>("commit-time-entry-record"),
            Command<CommitTimeEntryConfirmation>("commit-time-entry-confirmation"),
            Command<CommitApprovedTimeExport>("commit-approved-time-export"),
            Command<CommitProjectTimeEntryRecord>("commit-project-time-entry-record"),
            Command<CommitProjectTimeEntrySubmit>("commit-project-time-entry-submit"),
            Command<CommitProjectTimeEntryApprove>("commit-project-time-entry-approve"),
            Command<CommitProjectTimeEntryCorrect>("commit-project-time-entry-correct")];

    /// <summary>Gets the SDK query route owned by Timesheets.</summary>
    public static RouteContractDeclaration ApprovedLedgerQuery { get; } = new(
        "timesheets-query-approved-time-ledger", "query", TimesheetsEventStoreIntegration.DomainName,
        nameof(QueryApprovedTimeLedger), null, "v1", null, "workload",
        EventStoreOperationIdentifiers.DomainServiceQuery, null, [],
        new(typeof(ApprovedTimeLedgerQueryHandler).FullName!, "exact", [nameof(QueryApprovedTimeLedger)],
            "domain-service", "computed", [], RequiresDelegatedContext: true));

    /// <summary>Gets the compiled owner routes.</summary>
    public static IReadOnlyList<RouteContractDeclaration> Routes => [.. Commands.Select(command => command.Route), ApprovedLedgerQuery];

    /// <summary>Gets the value-free schemas for the internal workload operations.</summary>
    public static IReadOnlyList<RouteOperationContract> Operations =>
        [.. Commands.Select(command => RouteOperationSchema.Create(command.Route,
            PayloadTypes.Single(type => type.FullName == command.Route.Execution!.HandlerType),
            typeof(DomainServiceWireResult), "internal", "UI-only", JsonSerializerOptions.Web)),
            RouteOperationSchema.Create(ApprovedLedgerQuery, typeof(QueryApprovedTimeLedger), typeof(ApprovedTimeLedgerReadModel),
                "internal", "UI-only", JsonSerializerOptions.Web)];

    /// <summary>Gets the exact receiver methods for these routes.</summary>
    public static IReadOnlyDictionary<string, string> ReceiverMethods =>
        Routes.ToDictionary(route => route.RouteId, route => route == ApprovedLedgerQuery ? "query" : "process", StringComparer.Ordinal);

    /// <summary>Creates adapters that bind each command's semantic intent and authority-bearing fields.</summary>
    public static IReadOnlyList<IDeclaredIdempotencyIntentAdapter> CreateAdapters() =>
        [Adapter<CommitMagicLinkIssue>(), Adapter<CommitMagicLinkTransition>(),
            Adapter<CommitMagicLinkUse>(),
            // The server clock supplies audit evidence and can advance between identical retries.
            Adapter<CommitTimeEntryRecord>(payload => JsonSerializer.Serialize(
                new { payload.Command, payload.Tenant, payload.Actor }, JsonSerializerOptions.Web)),
            Adapter<CommitTimeEntryConfirmation>(payload => JsonSerializer.Serialize(
                new { payload.Command, payload.Tenant, payload.Actor }, JsonSerializerOptions.Web)),
            Adapter<CommitApprovedTimeExport>(),
            Adapter<CommitProjectTimeEntryRecord>(), Adapter<CommitProjectTimeEntrySubmit>(),
            Adapter<CommitProjectTimeEntryApprove>(), Adapter<CommitProjectTimeEntryCorrect>()];

    private static CommandIntentDeclaration Command<T>(string slug)
    {
        return new(new($"timesheets-{slug}", "command", TimesheetsEventStoreIntegration.DomainName,
                typeof(T).FullName!, null, "v1", 1, "workload", EventStoreOperationIdentifiers.DomainServiceProcess,
                null, [], new(typeof(T).FullName!, "exact", [typeof(T).FullName!], "domain-service", null, [])),
            $"timesheets:{slug}:intent-v1", $"timesheets:{slug}", $"timesheets:{slug}:policy-v1",
            new($"timesheets:{slug}:descriptor-v1", 1, [new("target", "string"), new("payload", "string")]),
            IdempotencyReplayRetentionTier.Mutation, false);
    }

    private static IDeclaredIdempotencyIntentAdapter Adapter<T>(Func<T, string>? serializeIntent = null) =>
        new DeclaredIdempotencyIntentAdapter<T>(
            Commands.Single(command => command.Route.Execution!.HandlerType == typeof(T).FullName),
            payload => new Dictionary<string, object?>
            {
                ["payload"] = serializeIntent?.Invoke(payload) ?? JsonSerializer.Serialize(payload, JsonSerializerOptions.Web)
            });
}
