using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Queries.Reporting;

public sealed record QueryApprovedTimeLedger
{
    public ProjectReference? Project { get; init; }

    public WorkReference? Work { get; init; }

    public PartyReference? Contributor { get; init; }

    public ActivityTypeId? ActivityTypeId { get; init; }

    public string? TenantLocalPeriodKey { get; init; }

    public DateOnly? ServiceDateFrom { get; init; }

    public DateOnly? ServiceDateTo { get; init; }

    public BillableState? BillableState { get; init; }

    public bool CurrentRowsOnly { get; init; } = true;

    public bool IncludeSupersededRows { get; init; }

    public TimeEntryQuerySortBy SortBy { get; init; } = TimeEntryQuerySortBy.ServiceDate;

    public TimeEntryQuerySortDirection SortDirection { get; init; } = TimeEntryQuerySortDirection.Ascending;

    public int PageSize { get; init; } = 50;

    public string? Cursor { get; init; }
}
