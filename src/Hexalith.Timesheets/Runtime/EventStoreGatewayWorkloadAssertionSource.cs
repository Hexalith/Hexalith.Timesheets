using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Timesheets.Server.Runtime;
using System.Security.Claims;

namespace Hexalith.Timesheets.Runtime;

/// <summary>Requests a signed EventStore gateway assertion for the Timesheets workload.</summary>
public sealed class EventStoreGatewayWorkloadAssertionSource(
    IWorkloadAssertionIssuer issuer,
    IHttpContextAccessor httpContextAccessor)
    : IEventStoreGatewayWorkloadAssertionSource
{
    /// <inheritdoc />
    public ValueTask<string?> IssueAsync(string operation, string tenant, CancellationToken cancellationToken)
    {
        if (!issuer.CanBindResources || string.IsNullOrWhiteSpace(tenant))
        {
            return ValueTask.FromResult<string?>(null);
        }

        var bindings = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = tenant,
            [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = TimesheetsEventStoreIntegration.DomainName
        };
        ClaimsPrincipal? user = httpContextAccessor.HttpContext?.User;
        string? actor = user?.FindFirst("party_id")?.Value;
        if (string.IsNullOrWhiteSpace(actor))
        {
            actor = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
        if (user?.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(actor))
        {
            bindings[EventStoreWorkloadAuthenticationDefaults.ActorBindingClaimType] = actor;
        }

        return issuer.IssueAsync(new WorkloadAssertionRequest("eventstore", operation, bindings), cancellationToken);
    }
}
