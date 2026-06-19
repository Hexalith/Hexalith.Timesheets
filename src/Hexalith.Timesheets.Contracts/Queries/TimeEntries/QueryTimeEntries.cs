using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Queries.TimeEntries;

public sealed record QueryTimeEntries
{
    public PartyReference? Contributor { get; init; }

    public ProjectReference? Project { get; init; }

    public WorkReference? Work { get; init; }

    public string? TenantLocalPeriodKey { get; init; }

    public DateOnly? ServiceDateFrom { get; init; }

    public DateOnly? ServiceDateTo { get; init; }

    public ActivityTypeId? ActivityTypeId { get; init; }

    public BillableState? BillableState { get; init; }

    public IReadOnlyList<TimeEntryApprovalState> ApprovalStates { get; init; } = [];

    public IReadOnlyList<TimeEntryCorrectionState> CorrectionStates { get; init; } = [];

    public IReadOnlyList<ContributorCategory> ContributorCategories { get; init; } = [];

    public IReadOnlyList<TimeEntrySourceType> SourceTypes { get; init; } = [];

    public bool CurrentEntriesOnly { get; init; } = true;

    public bool IncludeNonCurrentStates { get; init; }

    public TimeEntryQuerySortBy SortBy { get; init; } = TimeEntryQuerySortBy.ServiceDate;

    public TimeEntryQuerySortDirection SortDirection { get; init; } = TimeEntryQuerySortDirection.Ascending;

    public int PageSize { get; init; } = 50;

    public string? Cursor { get; init; }
}
