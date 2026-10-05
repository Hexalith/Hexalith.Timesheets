using Hexalith.Timesheets.Contracts.References;

namespace Hexalith.Timesheets.Server.Runtime;

/// <summary>
/// Provides server-established request context, or null when the context is unavailable.
/// </summary>
/// <remarks>
/// Values, including any claim-derived identifiers, are evidence for server-side authorization;
/// this accessor does not grant tenant or resource authority.
/// </remarks>
public interface ITimesheetsTrustedContextAccessor
{
    /// <summary>Gets the current tenant evidence, or null when unavailable.</summary>
    TenantReference? CurrentTenant { get; }

    /// <summary>Gets the current actor evidence, or null when unavailable.</summary>
    PartyReference? CurrentActor { get; }

    /// <summary>Gets the server-established correlation identifier, or null when unavailable.</summary>
    string? CurrentCorrelationId { get; }
}
