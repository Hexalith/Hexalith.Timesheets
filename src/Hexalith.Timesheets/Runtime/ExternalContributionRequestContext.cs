using System.Security.Claims;

using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Timesheets.Server.Runtime;

namespace Hexalith.Timesheets.Runtime;

/// <summary>Resolves identity evidence for the workload-only external contribution routes.</summary>
internal static class ExternalContributionRequestContext
{
    /// <summary>Normalizes complete signed bindings and rejects conflicting or invalid binding evidence.</summary>
    internal static (string? TenantId, string? ActorId)? Resolve(HttpContext httpContext)
    {
        ClaimsPrincipal user = httpContext.User;
        ClaimsIdentity[] workloads = user.Identities.Where(identity => identity.IsAuthenticated
            && identity.AuthenticationType == EventStoreWorkloadAuthenticationDefaults.WorkloadScheme).ToArray();
        if (workloads.Length > 1)
        {
            return null;
        }

        string? tenant;
        string? actor;
        ClaimsIdentity? workload = workloads.SingleOrDefault();
        if (workload?.Claims.Any(claim => claim.Type.StartsWith(
            EventStoreWorkloadAuthenticationDefaults.BindingClaimPrefix, StringComparison.Ordinal)) == true)
        {
            tenant = ReadBinding(workload, EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType);
            actor = ReadBinding(workload, EventStoreWorkloadAuthenticationDefaults.ActorBindingClaimType);
            string? domain = ReadBinding(workload, EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType);
            if (tenant is null || actor is null || IsWorkloadSubject(actor)
                || domain != TimesheetsEventStoreIntegration.DomainName
                || user.Claims.Any(claim => ConflictsWithBinding(claim, tenant, actor)))
            {
                return null;
            }

            // Downstream gateway assertions resolve Party evidence from this same request principal.
            AddNormalizedClaims(httpContext, new Claim("tenant_id", tenant), new Claim("party_id", actor));
        }
        else
        {
            string[] tenants = ReadEvidence(user.Claims.Where(claim => claim.Type is "tenant_id" or "tenant"));
            string[] actors = ReadEvidence(user.FindAll("party_id"));
            if (actors.Length == 0)
            {
                actors = ReadEvidence(user.FindAll(ClaimTypes.NameIdentifier).Where(claim => !IsWorkloadSubject(claim.Value)));
            }

            if (tenants.Length > 1 || actors.Length > 1)
            {
                return null;
            }

            tenant = tenants.SingleOrDefault();
            actor = actors.SingleOrDefault();
            if (actor is not null && !IsWorkloadSubject(actor)
                && HttpContextTimesheetsTrustedContextAccessor.ResolveActorId(user) != actor)
            {
                AddNormalizedClaims(httpContext, new Claim("party_id", actor));
            }
        }

        return (tenant, actor is not null && !IsWorkloadSubject(actor)
            ? HttpContextTimesheetsTrustedContextAccessor.ResolveActorId(httpContext.User) : null);
    }

    private static void AddNormalizedClaims(HttpContext httpContext, params Claim[] claims)
        => httpContext.User = new ClaimsPrincipal(httpContext.User.Identities.Append(new ClaimsIdentity(claims)));

    private static string[] ReadEvidence(IEnumerable<Claim> claims)
        => claims.Select(claim => claim.Value).Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal).Take(2).ToArray();

    private static string? ReadBinding(ClaimsIdentity workload, string type)
    {
        Claim[] matches = workload.FindAll(type).ToArray();
        return matches.Length == 1 && !string.IsNullOrWhiteSpace(matches[0].Value) ? matches[0].Value : null;
    }

    private static bool ConflictsWithBinding(Claim claim, string tenant, string actor)
        => claim.Type switch
        {
            "tenant_id" or "tenant" => claim.Value != tenant,
            "party_id" => claim.Value != actor,
            ClaimTypes.NameIdentifier => !IsWorkloadSubject(claim.Value) && claim.Value != actor,
            _ => false
        };

    private static bool IsWorkloadSubject(string value) => value.StartsWith("workload:", StringComparison.Ordinal);
}
