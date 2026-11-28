using System.Text.Json;

using Hexalith.EventStore.Client.Attributes;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.DomainService;
using Hexalith.Timesheets.Contracts;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Server.Authorization;

using Microsoft.AspNetCore.Http;

namespace Hexalith.Timesheets.Server.ApprovedTimeLedger;

/// <summary>Serves the approved ledger through the SDK's authenticated query dispatcher.</summary>
[EventStoreDomain(TimesheetsDomain.Name)]
public sealed class ApprovedTimeLedgerQueryHandler(
    ApprovedTimeLedgerQueryService service, IHttpContextAccessor? httpContextAccessor = null) : IDomainQueryHandler
{
    /// <inheritdoc />
    public string Domain => TimesheetsDomain.Name;

    /// <inheritdoc />
    public string QueryType => nameof(QueryApprovedTimeLedger);

    /// <inheritdoc />
    public async Task<QueryResult> ExecuteAsync(QueryEnvelope query, CancellationToken cancellationToken)
    {
        if (query.Domain != Domain || query.QueryType != QueryType || query.EntityId is not null
            || VerifiedQueryCaller.Get(httpContextAccessor?.HttpContext) is not { } caller
            || !ReferenceEquals(caller.Query, query) || query.OriginalActorId != query.UserId)
        {
            return QueryResult.Failure(QueryAdapterFailureReason.Forbidden);
        }

        try
        {
            QueryApprovedTimeLedger? request = JsonSerializer.Deserialize<QueryApprovedTimeLedger>(query.Payload, JsonSerializerOptions.Web);
            var project = new ProjectReference(query.AggregateId);
            if (request is null || request.Project is not null && request.Project != project)
            {
                return QueryResult.Failure(QueryAdapterFailureReason.Forbidden);
            }

            request = request with { Project = project };
            if (query.Paging is { } paging)
            {
                request = request with { PageSize = paging.PageSize ?? request.PageSize, Cursor = paging.Cursor ?? request.Cursor };
            }

            var context = TimesheetsRequestContext.ForProjectWorkflow(new(query.TenantId), new(query.UserId), query.CorrelationId);
            ApprovedTimeLedgerQueryResult result = await service.QueryAsync(context, request, cancellationToken).ConfigureAwait(false);
            return result.WasDisclosed
                ? QueryResult.FromPayload(JsonSerializer.SerializeToElement(result.Page, JsonSerializerOptions.Web))
                : QueryResult.Failure(QueryAdapterFailureReason.Forbidden);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return QueryResult.Failure(QueryAdapterFailureReason.Forbidden);
        }
    }
}
