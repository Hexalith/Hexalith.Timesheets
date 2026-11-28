using System.Security.Claims;

using Hexalith.Timesheets.Contracts.Commands.TimeEntries;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Runtime;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.Runtime;
using Hexalith.Timesheets.Server.TimeEntries;

namespace Hexalith.Timesheets.Endpoints;

/// <summary>Maps the authenticated human Project capture-to-ledger workflow.</summary>
internal static class ProjectWorkflowEndpoints
{
    /// <summary>Maps the Project command and approved ledger endpoints with the human policy.</summary>
    internal static IEndpointRouteBuilder MapTimesheetsProjectWorkflow(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/timesheets")
            .RequireAuthorization(TimesheetsAuthenticationDefaults.HumanPolicy);
        group.MapPost("/time-entries", (RecordTimeEntry body, HttpContext http, ITimesheetsTrustedContextAccessor trusted,
            ProjectTimeEntryWorkflowSubmission workflow, CancellationToken cancellationToken) =>
            SubmitAsync(body.TimeEntryId?.Value, body, http, trusted, workflow, cancellationToken));
        group.MapPost("/time-entries/{id}/submit", (string id, SubmitTimeEntriesForApproval body, HttpContext http,
            ITimesheetsTrustedContextAccessor trusted, ProjectTimeEntryWorkflowSubmission workflow, CancellationToken cancellationToken) =>
            SubmitAsync(id, body, http, trusted, workflow, cancellationToken));
        group.MapPost("/time-entries/{id}/approve", (string id, ApproveTimeEntry body, HttpContext http,
            ITimesheetsTrustedContextAccessor trusted, ProjectTimeEntryWorkflowSubmission workflow, CancellationToken cancellationToken) =>
            SubmitAsync(id, body, http, trusted, workflow, cancellationToken));
        group.MapPost("/time-entries/{id}/corrections", (string id, CorrectApprovedTimeEntry body, HttpContext http,
            ITimesheetsTrustedContextAccessor trusted, ProjectTimeEntryWorkflowSubmission workflow, CancellationToken cancellationToken) =>
            SubmitAsync(id, body, http, trusted, workflow, cancellationToken));
        group.MapPost("/approved-ledger", QueryAsync);
        return endpoints;
    }

    private static async Task<IResult> SubmitAsync(string? id, object body, HttpContext http,
        ITimesheetsTrustedContextAccessor trusted, ProjectTimeEntryWorkflowSubmission workflow, CancellationToken cancellationToken)
    {
        http.Response.Headers.CacheControl = "no-store";
        TimesheetsRequestContext? context = CreateContext(http, trusted);
        if (context is null || string.IsNullOrWhiteSpace(id)
            || !http.Request.Headers.TryGetValue("Idempotency-Key", out var keys) || keys.Count != 1
            || string.IsNullOrWhiteSpace(keys[0]))
        {
            return Results.NotFound();
        }

        ProjectTimeEntrySubmissionOutcome outcome = await workflow.SubmitAsync(
            context, new TimeEntryId(id), body, keys[0]!, cancellationToken).ConfigureAwait(false);
        if (outcome == ProjectTimeEntrySubmissionOutcome.Indeterminate)
        {
            http.Response.Headers.RetryAfter = "1";
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        return outcome == ProjectTimeEntrySubmissionOutcome.Committed ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> QueryAsync(QueryApprovedTimeLedger query, HttpContext http,
        ITimesheetsTrustedContextAccessor trusted, ApprovedTimeLedgerQueryService ledger, CancellationToken cancellationToken)
    {
        http.Response.Headers.CacheControl = "no-store";
        TimesheetsRequestContext? context = CreateContext(http, trusted);
        if (context is null)
        {
            return Results.NotFound();
        }

        ApprovedTimeLedgerQueryResult result = await ledger.QueryAsync(context, query, cancellationToken).ConfigureAwait(false);
        return result.WasDisclosed ? Results.Ok(result.Page) : Results.NotFound();
    }

    private static TimesheetsRequestContext? CreateContext(HttpContext http, ITimesheetsTrustedContextAccessor trusted)
    {
        if (http.User.Identity?.IsAuthenticated != true || trusted.CurrentTenant is not { } tenant
            || trusted.CurrentActor is not { } actor)
        {
            return null;
        }

        string? subject = http.User.FindFirst("sub")?.Value ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return subject == actor.PartyId
            ? TimesheetsRequestContext.ForProjectWorkflow(tenant, actor, trusted.CurrentCorrelationId ?? http.TraceIdentifier)
            : null;
    }
}
