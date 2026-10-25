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
            string? actorId = ResolveActorId(_httpContextAccessor.HttpContext?.User);
            return string.IsNullOrWhiteSpace(actorId) ? null : new PartyReference(actorId);
        }
    }

    /// <inheritdoc/>
    public string? CurrentCorrelationId => _httpContextAccessor.HttpContext?.TraceIdentifier;

    /// <summary>Resolves existing Party evidence without treating the SDK's synthetic workload subject as a Party.</summary>
    internal static string? ResolveActorId(ClaimsPrincipal? user)
    {
        string? partyId = user?.FindFirst("party_id")?.Value;
        if (!string.IsNullOrWhiteSpace(partyId))
        {
            return partyId;
        }

        string? subject = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return string.IsNullOrWhiteSpace(subject) || subject.StartsWith("workload:", StringComparison.Ordinal)
            ? null : subject;
    }

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
