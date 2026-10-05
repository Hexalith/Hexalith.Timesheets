using Hexalith.Timesheets.Contracts.References;

namespace Hexalith.Timesheets.Server.Runtime;

/// <summary>Provides the fail-closed default when no server-established request context is available.</summary>
public sealed class UnavailableTimesheetsTrustedContextAccessor : ITimesheetsTrustedContextAccessor
{
    /// <inheritdoc/>
    public TenantReference? CurrentTenant => null;

    /// <inheritdoc/>
    public PartyReference? CurrentActor => null;

    /// <inheritdoc/>
    public string? CurrentCorrelationId => null;
}
