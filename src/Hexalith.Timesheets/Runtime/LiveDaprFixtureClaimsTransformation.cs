using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;

namespace Hexalith.Timesheets.Runtime;

/// <summary>Maps verified local workload bindings to fixture identity claims.</summary>
internal sealed class LiveDaprFixtureClaimsTransformation : IClaimsTransformation
{
    /// <inheritdoc/>
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (!principal.HasClaim("eventstore:workload", "eventstore")
            || !principal.HasClaim("eventstore:bound-domain", "timesheets")
            || !principal.HasClaim("eventstore:operation", "domain-service:process"))
        {
            return Task.FromResult(principal);
        }

        string? tenant = principal.FindFirst("eventstore:bound-tenant")?.Value;
        string? actor = principal.FindFirst("eventstore:bound-actor")?.Value;
        if (string.IsNullOrWhiteSpace(tenant)
            || string.IsNullOrWhiteSpace(actor)
            || principal.HasClaim(static claim => claim.Type is "tenant_id" or "party_id"))
        {
            return Task.FromResult(principal);
        }

        var copy = new ClaimsPrincipal(principal);
        copy.AddIdentity(new ClaimsIdentity(
        [
            new Claim("tenant_id", tenant),
            new Claim("party_id", actor)
        ], "LiveDaprFixture"));
        return Task.FromResult(copy);
    }
}
