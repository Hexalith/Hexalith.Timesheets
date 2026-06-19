using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Queries.Reporting;

public sealed record QueryTimesheetsDashboardOverview
{
    public string? TenantLocalPeriodKey { get; init; }

    public DateOnly? ServiceDateFrom { get; init; }

    public DateOnly? ServiceDateTo { get; init; }

    public ProjectReference? Project { get; init; }

    public WorkReference? Work { get; init; }

    public ActivityTypeId? ActivityTypeId { get; init; }

    public BillableState? BillableState { get; init; }

    public bool CurrentRowsOnly { get; init; } = true;
}
