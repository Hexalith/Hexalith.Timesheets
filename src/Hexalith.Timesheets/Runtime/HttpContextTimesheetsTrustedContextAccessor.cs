using System.Security.Claims;

using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Server.Runtime;

namespace Hexalith.Timesheets.Runtime;

/// <summary>Reads request claims and correlation context for the host's server-side authorization gates.</summary>
/// <remarks>Claim-derived identifiers are evidence and do not grant tenant or resource authority.</remarks>
/// <param name="httpContextAccessor">The accessor for the current server-established HTTP context.</param>
public sealed class HttpContextTimesheetsTrustedContextAccessor(
    IHttpContextAccessor httpContextAccessor) : ITimesheetsTrustedContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));

    /// <inheritdoc/>
    public TenantReference? CurrentTenant
    {
        get
        {
            string? tenantId = FirstClaimValue("tenant_id", "tenant");
            return string.IsNullOrWhiteSpace(tenantId) ? null : new TenantReference(tenantId);
        }
    }

    /// <inheritdoc/>
    public PartyReference? CurrentActor
    {
        get
        {
            string? actorId = FirstClaimValue("party_id", ClaimTypes.NameIdentifier);
            return string.IsNullOrWhiteSpace(actorId) ? null : new PartyReference(actorId);
        }
    }

    /// <inheritdoc/>
    public string? CurrentCorrelationId => _httpContextAccessor.HttpContext?.TraceIdentifier;

    private string? FirstClaimValue(params string[] claimTypes)
    {
        ClaimsPrincipal? user = _httpContextAccessor.HttpContext?.User;
        if (user is null)
        {
            return null;
        }

        foreach (string claimType in claimTypes)
        {
            string? value = user.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
