using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Hexalith.Timesheets.IntegrationTests;

/// <summary>
/// Supplies the fixture's administrator claims after workload authentication on protected management requests.
/// </summary>
internal sealed class MagicLinkFixtureClaimsTransformation(
    IHttpContextAccessor contextAccessor,
    IReadOnlyList<Claim> claims) : IClaimsTransformation
{
    /// <inheritdoc />
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (contextAccessor.HttpContext?.Request.Path.StartsWithSegments(
                "/api/timesheets/magic-links/confirmation-capabilities", StringComparison.OrdinalIgnoreCase) != true)
        {
            return Task.FromResult(principal);
        }

        var transformed = new ClaimsPrincipal(
            new[] { new ClaimsIdentity(claims) }.Concat(principal.Identities));
        return Task.FromResult(transformed);
    }
}
