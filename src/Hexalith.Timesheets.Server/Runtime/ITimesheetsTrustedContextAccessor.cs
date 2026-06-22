using Hexalith.Timesheets.Contracts.References;

namespace Hexalith.Timesheets.Server.Runtime;

public interface ITimesheetsTrustedContextAccessor
{
    TenantReference? CurrentTenant { get; }

    PartyReference? CurrentActor { get; }

    string? CurrentCorrelationId { get; }
}
