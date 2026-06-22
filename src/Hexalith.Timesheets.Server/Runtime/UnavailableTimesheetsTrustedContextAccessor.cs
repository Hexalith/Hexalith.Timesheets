using Hexalith.Timesheets.Contracts.References;

namespace Hexalith.Timesheets.Server.Runtime;

public sealed class UnavailableTimesheetsTrustedContextAccessor : ITimesheetsTrustedContextAccessor
{
    public TenantReference? CurrentTenant => null;

    public PartyReference? CurrentActor => null;

    public string? CurrentCorrelationId => null;
}
