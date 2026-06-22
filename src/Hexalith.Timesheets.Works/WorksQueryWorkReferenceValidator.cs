using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.References;
using Hexalith.Works.Contracts.Models;
using Hexalith.Works.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Works;

/// <summary>
/// Concrete <see cref="IWorkReferenceValidator"/> that validates a Work reference by consuming the
/// Hexalith.Works <c>get-work-item</c> consumer query (domain <c>work</c>) over the EventStore
/// domain-service query channel (<see cref="IWorksQueryChannel"/>, bound to <c>IDomainQueryInvoker</c>
/// by the composed host).
/// <para>
/// It is an authority gate, not a data source: it returns only a typed <see cref="ReferenceValidationResult"/>
/// and never copies, persists, logs, or surfaces any Works-owned field (status, names, descriptions,
/// ownership, effort, parent). It is fail-closed by default — any unavailable, missing, cross-tenant,
/// ambiguous, or disabled/archived Work, and any transport or deserialization failure, denies the
/// trust-bearing write and never resolves to <see cref="ReferenceValidationState.Valid"/>.
/// </para>
/// </summary>
public sealed class WorksQueryWorkReferenceValidator : IWorkReferenceValidator
{
    // Mirrors Hexalith.Works.Queries.GetWorkItemQueryHandler.DomainName / .GetWorkItemQueryType.
    // Hardcoded so the adapter depends on Hexalith.Works.Contracts only, never the Works server project.
    private const string WorkDomainName = "work";
    private const string GetWorkItemQueryType = "get-work-item";

    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IWorksQueryChannel _queryChannel;

    /// <summary>Initializes a new instance of the <see cref="WorksQueryWorkReferenceValidator"/> class.</summary>
    /// <param name="queryChannel">The Works query channel used to invoke the Works consumer query.</param>
    public WorksQueryWorkReferenceValidator(IWorksQueryChannel queryChannel)
    {
        ArgumentNullException.ThrowIfNull(queryChannel);
        _queryChannel = queryChannel;
    }

    /// <inheritdoc/>
    public async ValueTask<ReferenceValidationResult> ValidateAsync(
        TimesheetsRequestContext context,
        WorkReference work,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(work);

        // Tenant authority comes from the request context, never from a caller-supplied payload field.
        // Without it the sibling authority cannot be consulted — fail closed.
        if (context.Tenant is null)
        {
            return ReferenceValidationResult.Denied(
                ReferenceValidationState.Unavailable,
                "Work reference authority could not be consulted without tenant context.");
        }

        WorkItemView? view;
        try
        {
            QueryEnvelope envelope = BuildEnvelope(context, work);
            QueryResult result = await _queryChannel
                .InvokeAsync(envelope, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                return Unavailable();
            }

            JsonElement payload = result.GetPayload();
            if (payload.ValueKind == JsonValueKind.Undefined)
            {
                return Unavailable();
            }

            view = payload.Deserialize<WorkItemView>(s_jsonOptions);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is the caller's intent, not a Works-authority failure: let it propagate.
            throw;
        }
#pragma warning disable CA1031 // Fail closed: any transport/serialization failure denies, never validates.
        catch (Exception)
        {
            return Unavailable();
        }
#pragma warning restore CA1031

        if (view is null)
        {
            return Unavailable();
        }

        return Map(context.Tenant, view);
    }

    private static QueryEnvelope BuildEnvelope(TimesheetsRequestContext context, WorkReference work)
    {
        // userId is propagated for audit only; the query is tenant-scoped by the Works roll-up key.
        // Fall back to the correlation id so a missing actor never throws out of the validator.
        string userId = string.IsNullOrWhiteSpace(context.Actor?.PartyId)
            ? context.CorrelationId
            : context.Actor!.PartyId;

        return new QueryEnvelope(
            tenantId: context.Tenant!.TenantId,
            domain: WorkDomainName,
            aggregateId: work.WorkId,
            queryType: GetWorkItemQueryType,
            payload: [],
            correlationId: context.CorrelationId,
            userId: userId);
    }

    private static ReferenceValidationResult Map(TenantReference tenant, WorkItemView view)
    {
        if (!view.Found)
        {
            // NotFound conflates "does not exist" and "not yet projected"; both deny a trust-bearing
            // write (Open Question Q1 default: InvalidReference). Reason avoids existence disclosure.
            return ReferenceValidationResult.Denied(
                ReferenceValidationState.InvalidReference,
                "Work reference could not be validated.");
        }

        // Defensive cross-tenant guard. The tenant-scoped roll-up key already makes a cross-tenant id
        // resolve to NotFound; this re-asserts the request tenant authority on the returned view.
        string expectedTenant = tenant.TenantId.ToLowerInvariant();
        if (!string.Equals(view.TenantId.Value, expectedTenant, StringComparison.Ordinal))
        {
            return ReferenceValidationResult.Denied(
                ReferenceValidationState.TenantMismatch,
                "Work reference is not accessible in the current tenant.");
        }

        return view.Status switch
        {
            WorkItemStatus.Created
                or WorkItemStatus.Assigned
                or WorkItemStatus.Queued
                or WorkItemStatus.InProgress => ReferenceValidationResult.Valid(),

            // Open Question Q2 default: historical capture against completed work is allowed.
            WorkItemStatus.Completed => ReferenceValidationResult.Valid(),

            // Open Question Q2 default: suspended work is closed to trust-bearing time capture.
            WorkItemStatus.Suspended => NotOpen(),

            WorkItemStatus.Cancelled
                or WorkItemStatus.Rejected
                or WorkItemStatus.Expired => NotOpen(),

            // Found work reporting an Unknown status should not occur; treat as a safe denial.
            WorkItemStatus.Unknown => ReferenceValidationResult.Denied(
                ReferenceValidationState.Ambiguous,
                "Work reference is in an indeterminate state."),

            _ => ReferenceValidationResult.Denied(
                ReferenceValidationState.InvalidReference,
                "Work reference could not be validated."),
        };
    }

    private static ReferenceValidationResult Unavailable()
        => ReferenceValidationResult.Denied(
            ReferenceValidationState.Unavailable,
            "Work reference authority is unavailable.");

    private static ReferenceValidationResult NotOpen()
        => ReferenceValidationResult.Denied(
            ReferenceValidationState.DisabledOrArchived,
            "Work reference is not open for time capture.");
}
