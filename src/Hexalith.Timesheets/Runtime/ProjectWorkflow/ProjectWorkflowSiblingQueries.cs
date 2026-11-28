using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Queries;
using Hexalith.Projects.Contracts.Queries;
using Hexalith.Tenants.Contracts.Identity;
using Hexalith.Tenants.Contracts.Queries;

using Microsoft.Extensions.Options;

namespace Hexalith.Timesheets.Runtime.ProjectWorkflow;

/// <summary>Queries sibling owners with the authority credential enrolled for this host.</summary>
internal sealed class ProjectWorkflowSiblingQueries(IHttpClientFactory clients)
{
    /// <summary>The separate authority-authenticated EventStore query client.</summary>
    internal const string ClientName = "TimesheetsProjectWorkflowAuthority";

    private static readonly JsonSerializerOptions s_projectJson = new(JsonSerializerDefaults.Web) { AllowDuplicateProperties = false };

    /// <summary>Reads exactly one current membership in the workflow tenant for the selected actor.</summary>
    internal async Task<UserTenantMembership?> ReadTenantMembershipAsync(string tenant, string actor, CancellationToken cancellationToken)
    {
        // The authority principal owns just the enrolled tenant. Request enough rows to detect
        // unexpected visible memberships and require both owner paging markers to prove completeness.
        const int pageSize = 2;
        var request = new SubmitQueryRequest(TenantPlatformScopes.Tenants, GetUserTenantsQuery.Domain,
            "index", GetUserTenantsQuery.QueryType, GetUserTenantsQuery.ProjectionType,
            JsonSerializer.SerializeToElement(new GetUserTenantsQuery { UserId = actor }), EntityId: actor)
        {
            Paging = new QueryPagingOptions(PageSize: pageSize)
        };
        EventStoreQueryResult<PaginatedResult<UserTenantMembership>>? result =
            await ReadAsync<PaginatedResult<UserTenantMembership>>(request, cancellationToken).ConfigureAwait(false);
        if (result is not
            {
                Metadata:
                {
                    Provenance: QueryResponseProvenance.ProjectionBacked,
                    Lifecycle: ProjectionLifecycleState.Current,
                    Paging: { PageSize: pageSize, Offset: null, HasMore: false, NextCursor: null, TotalCount: null or 1 }
                },
                Payload: { Items: [var membership], HasMore: false, Cursor: null }
            } || membership?.TenantId != tenant)
        {
            return null;
        }

        return membership;
    }

    /// <summary>Reads a Party from the exactly scoped tenant envelope.</summary>
    internal async Task<PartyDetail?> ReadPartyAsync(string tenant, string party, CancellationToken cancellationToken)
    {
        EventStoreQueryResult<PartyDetail>? result = await ReadAsync<PartyDetail>(new(tenant, "party", party,
            "PartyDetail", "party-detail",
            JsonSerializer.SerializeToElement(new GetParty { PartyId = party }, JsonSerializerOptions.Web)),
            cancellationToken).ConfigureAwait(false);
        return result?.Payload;
    }

    /// <summary>Reads safe Project lifecycle and authorization evidence from Projects.</summary>
    internal async Task<GetProjectResponse?> ReadProjectAsync(string tenant, string project, CancellationToken cancellationToken)
    {
        EventStoreQueryResult<JsonElement>? result = await ReadAsync<JsonElement>(new(tenant, "projects", project,
            GetProjectQuery.QueryType, Payload: JsonSerializer.SerializeToElement(new GetProjectQuery(), JsonSerializerOptions.Web)),
            cancellationToken).ConfigureAwait(false);
        if (result?.Payload is not { ValueKind: JsonValueKind.Object } payload
            || !HasString(payload, "projectId") || !HasString(payload, "lifecycleState")
            || !payload.TryGetProperty("snapshot", out JsonElement snapshot) || snapshot.ValueKind != JsonValueKind.Object
            || !HasString(snapshot, "responseState") || !HasString(snapshot, "asOf")
            || !snapshot.TryGetProperty("projectVersion", out JsonElement version) || version.ValueKind != JsonValueKind.Number
            || !snapshot.TryGetProperty("components", out JsonElement components) || components.ValueKind != JsonValueKind.Array
            || !snapshot.TryGetProperty("recoveryActions", out JsonElement actions) || actions.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        try
        {
            return payload.Deserialize<GetProjectResponse>(s_projectJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool HasString(JsonElement value, string property)
        => value.TryGetProperty(property, out JsonElement member) && member.ValueKind == JsonValueKind.String;

    private async Task<EventStoreQueryResult<T>?> ReadAsync<T>(SubmitQueryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using HttpClient http = clients.CreateClient(ClientName);
            var gateway = new EventStoreGatewayClient(http, Options.Create(new EventStoreGatewayClientOptions()));
            EventStoreQueryResult<T> result = await gateway.SubmitQueryAsync<T>(request,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return result.IsNotModified || result.Metadata is { IsStale: true } or { IsDegraded: true }
                || result.Metadata?.Lifecycle is ProjectionLifecycleState.Rebuilding or ProjectionLifecycleState.Unavailable
                    or ProjectionLifecycleState.LocalOnly or ProjectionLifecycleState.Degraded or ProjectionLifecycleState.Stale
                ? null : result;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}
