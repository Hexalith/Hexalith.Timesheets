using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Server.Runtime;

namespace Hexalith.Timesheets.Projections.Tests;

/// <summary>Provides a fixed trusted context for selected-index loader tests.</summary>
internal sealed class FixedIndexTrustedContextAccessor : ITimesheetsTrustedContextAccessor
{
    public TenantReference? CurrentTenant => new("tenant-1");

    public PartyReference? CurrentActor => new("operator-1");

    public string? CurrentCorrelationId => "selected-index-test";
}
