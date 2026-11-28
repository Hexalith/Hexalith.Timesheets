using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Contracts.Identity;
using Hexalith.Tenants.Contracts.Queries;

namespace Hexalith.Timesheets.IntegrationTests;

/// <summary>Stands in for the three sibling owners and the authority credential endpoint.</summary>
internal sealed class ProjectWorkflowSiblingTransport(string? unavailable) : HttpMessageHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri?.Host == "workflow-authority.example")
        {
            return Response(new { access_token = "isolated-authority", token_type = "Bearer", expires_in = 120 });
        }

        if (request.Headers.Authorization is not { Scheme: "Bearer", Parameter: "isolated-authority" }
            || request.Content is null || request.RequestUri?.AbsolutePath != "/api/v1/queries")
        {
            return new(HttpStatusCode.Forbidden);
        }

        SubmitQueryRequest? scope = await request.Content.ReadFromJsonAsync<SubmitQueryRequest>(cancellationToken);
        if (scope is null || scope.Domain == unavailable)
        {
            return new(HttpStatusCode.ServiceUnavailable);
        }

        object payload;
        QueryPagingMetadata? paging = null;
        switch (scope.Domain, scope.QueryType)
        {
            case var (domain, queryType) when domain == GetUserTenantsQuery.Domain && queryType == GetUserTenantsQuery.QueryType:
                if (scope.Tenant != TenantPlatformScopes.Tenants || scope.AggregateId != "index"
                    || scope.ProjectionType != GetUserTenantsQuery.ProjectionType
                    || scope.Paging is not { PageSize: 2, Cursor: null, Offset: null }
                    || scope.Payload is not { ValueKind: JsonValueKind.Object } input
                    || !input.TryGetProperty("UserId", out JsonElement target)
                    || target.GetString() is not ("contributor" or "approver")
                    || scope.EntityId != target.GetString())
                {
                    return new(HttpStatusCode.BadRequest);
                }

                UserTenantMembership membership = new(
                    unavailable == "tenant-membership-wrong-scope" ? "different-tenant" : "workflow-tenant",
                    "Sibling tenant display value",
                    unavailable == "tenant-membership-disabled" ? TenantStatus.Disabled : TenantStatus.Active,
                    unavailable == "tenant-membership-unknown-role" ? TenantRole.Unknown : TenantRole.TenantContributor);
                bool hasMore = unavailable == "tenant-membership-incomplete";
                string? cursor = hasMore ? "owner-cursor" : null;
                payload = new PaginatedResult<UserTenantMembership>([membership], cursor, hasMore);
                paging = unavailable == "tenant-membership-paging-missing"
                    ? null : new QueryPagingMetadata(2, NextCursor: cursor, HasMore: hasMore);
                break;
            case ("party", "PartyDetail") when scope.Tenant == "workflow-tenant":
                payload = new { id = scope.AggregateId, type = "Person",
                    isActive = true, displayName = "Sibling Party display value", sortName = "Sibling Party sort value",
                    freshness = new { status = "Current" } };
                break;
            case ("projects", "Hexalith.Projects.Queries.GetProject.v1") when scope.Tenant == "workflow-tenant":
                payload = new { projectId = scope.AggregateId, name = "Sibling Project display value",
                    lifecycleState = "Active", createdAt = DateTimeOffset.UnixEpoch, updatedAt = DateTimeOffset.UnixEpoch,
                    references = Array.Empty<object>(), snapshot = new { responseState = "Complete",
                        asOf = DateTimeOffset.UtcNow, projectVersion = 1, components = Array.Empty<object>(),
                        recoveryActions = Array.Empty<string>() } };
                if (unavailable is "project-lifecycle-missing" or "project-admission-missing")
                {
                    JsonObject truncated = JsonSerializer.SerializeToNode(payload)!.AsObject();
                    if (unavailable == "project-lifecycle-missing")
                    {
                        truncated.Remove("lifecycleState");
                    }
                    else
                    {
                        truncated["snapshot"]!.AsObject().Remove("responseState");
                    }
                    payload = truncated;
                }
                break;
            default:
                return new(HttpStatusCode.BadRequest);
        }

        bool projected = scope.Domain != "projects";
        ProjectionLifecycleState lifecycle = scope.Domain == "tenants" && unavailable == "tenant-lifecycle-unknown"
            ? ProjectionLifecycleState.Unknown : ProjectionLifecycleState.Current;
        HttpResponseMessage result = Response(new
        {
            correlationId = "workflow-sibling",
            payload,
            success = true,
            metadata = projected
                ? new QueryResponseMetadata(IsStale: false, IsDegraded: false, Paging: paging)
                {
                    Provenance = QueryResponseProvenance.ProjectionBacked,
                    Lifecycle = lifecycle
                }
                : new QueryResponseMetadata { Provenance = QueryResponseProvenance.HandlerComputed }
        });
        if (scope.Domain != "projects")
        {
            result.Headers.Add(QueryResponseProvenancePolicy.HeaderName, QueryResponseProvenancePolicy.Format(QueryResponseProvenance.ProjectionBacked));
            if (lifecycle != ProjectionLifecycleState.Unknown)
            {
                result.Headers.Add(ProjectionLifecyclePolicy.HeaderName, lifecycle.ToString());
            }
        }

        return result;
    }

    private static HttpResponseMessage Response(object payload) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(payload)
    };
}
