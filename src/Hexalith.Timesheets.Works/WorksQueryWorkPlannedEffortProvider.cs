using System.Globalization;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.OperationalReports;
using Hexalith.Works.Contracts.Models;

namespace Hexalith.Timesheets.Works;

/// <summary>
/// Concrete <see cref="IWorkPlannedEffortProvider"/> that reads a Work item's planned effort by consuming
/// the Hexalith.Works <c>get-work-item</c> consumer query (domain <c>work</c>) over the EventStore
/// domain-service query channel (<see cref="IWorksQueryChannel"/>, bound to <c>IDomainQueryInvoker</c>
/// by the composed host).
/// <para>
/// Unlike the sibling authority gate (<c>WorksQueryWorkReferenceValidator</c>), this provider's legitimate
/// payload is the planned/estimated effort itself: it reads <c>Estimated</c>/<c>Done</c>/<c>Remaining</c>/
/// <c>Unit</c> from the consumer view and surfaces them exclusively through the typed, source-attributed
/// <see cref="WorkPlannedEffortReadModel"/> (<c>SourceModuleName == "Works"</c>). It never reads Works
/// internal roll-up structure or parent lineage, never converts the Works unit into a Timesheets unit, and
/// never copies, persists, or serializes Works lifecycle state, names, descriptions, or ownership outward.
/// </para>
/// <para>
/// It is fail-closed: missing tenant context, a non-success or empty <see cref="QueryResult"/>, a
/// deserialization failure, or any other non-cancellation exception resolves to
/// <see cref="WorkPlannedEffortReadModel.Unavailable(string?)"/>; a cross-tenant view resolves to
/// <see cref="WorkPlannedEffortReadModel.Unauthorized(string?)"/>. A planned value is supplied only when the
/// Work is found in-tenant and carries both an estimate and a unit. The report consumer calls this provider
/// only for already-authorized Work rows.
/// </para>
/// </summary>
public sealed class WorksQueryWorkPlannedEffortProvider : IWorkPlannedEffortProvider
{
    // Mirrors Hexalith.Works.Queries.GetWorkItemQueryHandler.DomainName / .GetWorkItemQueryType.
    // Hardcoded so the adapter depends on Hexalith.Works.Contracts only, never the Works server project.
    private const string WorkDomainName = "work";
    private const string GetWorkItemQueryType = "get-work-item";

    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IWorksQueryChannel _queryChannel;

    /// <summary>Initializes a new instance of the <see cref="WorksQueryWorkPlannedEffortProvider"/> class.</summary>
    /// <param name="queryChannel">The Works query channel used to invoke the Works consumer query.</param>
    public WorksQueryWorkPlannedEffortProvider(IWorksQueryChannel queryChannel)
    {
        ArgumentNullException.ThrowIfNull(queryChannel);
        _queryChannel = queryChannel;
    }

    /// <inheritdoc/>
    public async ValueTask<WorkPlannedEffortReadModel> GetPlannedEffortAsync(
        TimesheetsRequestContext context,
        WorkReference work,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(work);

        // Tenant authority comes from the request context, never from a caller-supplied payload field.
        // Without it the Works source cannot be scoped or consulted — mark planned effort unavailable.
        if (context.Tenant is null)
        {
            return WorkPlannedEffortReadModel.Unavailable(
                "Works planned effort could not be read without tenant context.");
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
                return WorkPlannedEffortReadModel.Unavailable();
            }

            JsonElement payload = result.GetPayload();
            if (payload.ValueKind == JsonValueKind.Undefined)
            {
                return WorkPlannedEffortReadModel.Unavailable();
            }

            view = payload.Deserialize<WorkItemView>(s_jsonOptions);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is the caller's intent, not a Works-source failure: let it propagate.
            throw;
        }
#pragma warning disable CA1031 // Fail closed: any transport/serialization failure yields Unavailable, never a fabricated value.
        catch (Exception)
        {
            return WorkPlannedEffortReadModel.Unavailable();
        }
#pragma warning restore CA1031

        if (view is null)
        {
            return WorkPlannedEffortReadModel.Unavailable();
        }

        return Map(context.Tenant, view);
    }

    private static QueryEnvelope BuildEnvelope(TimesheetsRequestContext context, WorkReference work)
    {
        // userId is propagated for audit only; the query is tenant-scoped by the Works roll-up key.
        // Fall back to the correlation id so a missing actor never throws out of the provider.
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

    private static WorkPlannedEffortReadModel Map(TenantReference tenant, WorkItemView view)
    {
        // Defensive cross-tenant guard. The tenant-scoped roll-up key already makes a cross-tenant id
        // resolve to NotFound; this re-asserts the request tenant authority on the returned view so a
        // mismatched view never discloses another tenant's planned effort.
        string expectedTenant = tenant.TenantId.ToLowerInvariant();
        if (!string.Equals(view.TenantId.Value, expectedTenant, StringComparison.Ordinal))
        {
            return WorkPlannedEffortReadModel.Unauthorized();
        }

        // The row is already authorized; an absent or unestimated Work is "not supplied", not an authority
        // failure. Supply a planned value only when the Work is found AND carries both an estimate and a unit
        // (the Works effort fields are all-null-or-all-present together).
        if (!view.Found || view.Estimated is null || view.Unit is null)
        {
            return WorkPlannedEffortReadModel.NotSupplied();
        }

        // Record the monotonic Works source sequence as the freshness cursor. WorkItemView exposes no
        // degraded/rebuilding flag or as-of timestamp, so Works-side staleness is not positively detectable
        // from the consumer view; never fabricate a Stale result (see behavior note Q-C).
        ProjectionFreshnessMetadata freshness = new(
            ProjectionFreshnessState.Fresh,
            view.LatestAcceptedSourceSequence.ToString(CultureInfo.InvariantCulture),
            null,
            null);

        // Pass the Works unit string straight through — never convert it into a Timesheets/human duration.
        return WorkPlannedEffortReadModel.Supplied(
            view.Estimated,
            view.Done,
            view.Remaining,
            view.Unit!.Value,
            freshness);
    }
}
