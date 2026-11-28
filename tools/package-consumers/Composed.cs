using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Projections.ActivityTypes;
using Hexalith.Timesheets.Projections.ApprovedTimeLedger;
using Hexalith.Timesheets.Projections.MagicLinks;
using Hexalith.Timesheets.Projections.TimeEntries;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.Exports;
using Hexalith.Timesheets.Server.MagicLinks.Commands;
using Hexalith.Timesheets.Server.Runtime;
using Hexalith.Timesheets.Server.TimeEntries.Commands;

Assembly[] owners = [typeof(TimesheetsRouteCatalogDeclarations).Assembly, typeof(TimesheetsProjectionRouteCatalogDeclarations).Assembly];
var routes = CompiledRouteCatalogDeclarations.Read(owners);
var operations = CompiledRouteCatalogDeclarations.ReadOperations(owners);
var methods = owners.SelectMany(owner => owner.GetCustomAttributes<RouteCatalogDeclarationSourceAttribute>())
    .SelectMany(source =>
        (IReadOnlyDictionary<string, string>)source.SourceType.GetProperty("ReceiverMethods", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!)
    .ToDictionary(method => method.Key, method => method.Value, StringComparer.Ordinal);

var commands = new Dictionary<string, Type>(StringComparer.Ordinal)
{
    ["commit-magic-link-issue"] = typeof(CommitMagicLinkIssue),
    ["commit-magic-link-transition"] = typeof(CommitMagicLinkTransition),
    ["commit-magic-link-use"] = typeof(CommitMagicLinkUse),
    ["commit-time-entry-record"] = typeof(CommitTimeEntryRecord),
    ["commit-time-entry-confirmation"] = typeof(CommitTimeEntryConfirmation),
    ["commit-approved-time-export"] = typeof(CommitApprovedTimeExport),
    ["commit-project-time-entry-record"] = typeof(CommitProjectTimeEntryRecord),
    ["commit-project-time-entry-submit"] = typeof(CommitProjectTimeEntrySubmit),
    ["commit-project-time-entry-approve"] = typeof(CommitProjectTimeEntryApprove),
    ["commit-project-time-entry-correct"] = typeof(CommitProjectTimeEntryCorrect)
};
var projections = new Dictionary<string, Type>(StringComparer.Ordinal)
{
    ["approved-time-ledger"] = typeof(ApprovedTimeLedgerProjectionHandler),
    ["magic-link-token-hash-capability-index"] = typeof(MagicLinkTokenHashCapabilityIndexProjectionHandler),
    ["magic-link-confirmation-capabilities"] = typeof(MagicLinkConfirmationCapabilityProjectionHandler),
    ["tenant-activity-type-catalog"] = typeof(TenantActivityTypeCatalogProjectionHandler),
    ["time-entry-evidence"] = typeof(TimeEntryEvidenceProjectionHandler)
};
var commandTypes = TimesheetsRouteCatalogDeclarations.PayloadTypes.Select(type => type.FullName!);
var projectionTypes = owners[1].GetTypes().Where(type =>
    type.IsClass && !type.IsAbstract && typeof(IAsyncDomainProjectionHandler).IsAssignableFrom(type)).Select(type => type.FullName!);
var queryTypes = owners[0].GetTypes().Where(type =>
    type.IsClass && !type.IsAbstract && typeof(IDomainQueryHandler).IsAssignableFrom(type));
int expectedCount = commands.Count + projections.Count + 1;
if (routes.Count != expectedCount || operations.Count != expectedCount || methods.Count != expectedCount
    || !Matches(commands.Values.Select(type => type.FullName!), commandTypes)
    || !Matches(projections.Values.Select(type => type.FullName!), projectionTypes)
    || queryTypes.SingleOrDefault() != typeof(ApprovedTimeLedgerQueryHandler))
{
    throw new InvalidOperationException("The composed catalog must match every installed command, query and named projection exactly once.");
}

foreach (var (slug, payload) in commands)
{
    VerifyBinding(slug, payload, "command", payload.FullName, null, "process", payload, typeof(DomainServiceWireResult));
}

foreach (var (name, handler) in projections)
{
    VerifyBinding(name, handler, "projection", null, name, "project/v2", typeof(ProjectionDispatchRequest), typeof(ProjectionDispatchResponse));
}

VerifyBinding("query-approved-time-ledger", typeof(ApprovedTimeLedgerQueryHandler), "query",
    nameof(QueryApprovedTimeLedger), null, "query", typeof(QueryApprovedTimeLedger), typeof(ApprovedTimeLedgerReadModel));

void VerifyBinding(string slug, Type handler, string kind, string? message, string? projection, string method, Type request, Type result)
{
    string routeId = "timesheets-" + slug;
    var route = routes.Single(candidate => candidate.RouteId == routeId);
    if (route.Kind != kind || route.Execution?.HandlerType != handler.FullName
        || route.MessageType != message || route.ProjectionType != projection || methods[routeId] != method)
    {
        throw new InvalidOperationException($"The composed catalog binds {routeId} to the wrong receiver.");
    }

    var expected = RouteOperationSchema.Create(route, request, result, "internal", "UI-only", JsonSerializerOptions.Web);
    if (operations.Single(operation => operation.RouteId == routeId).Digest != expected.Digest)
    {
        throw new InvalidOperationException($"The composed catalog binds {routeId} to the wrong operation schema.");
    }
}

static bool Matches(IEnumerable<string> actual, IEnumerable<string> expected) =>
    actual.Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal), StringComparer.Ordinal);
