using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Queries.Reporting;

public sealed record QueryProjectActualTimeReport
{
    public ProjectReference? Project { get; init; }

    public PartyReference? Contributor { get; init; }

    public PartyReference? AiAgent { get; init; }

    public ActivityTypeId? ActivityTypeId { get; init; }

    public string? TenantLocalPeriodKey { get; init; }

    public DateOnly? ServiceDateFrom { get; init; }

    public DateOnly? ServiceDateTo { get; init; }

    public BillableState? BillableState { get; init; }

    public TimeEntryApprovalState? ApprovalState { get; init; }

    public ContributorCategory? ContributorCategory { get; init; }

    public AiMetricAvailability? AiMetricAvailability { get; init; }

    public AiTokenMetricAvailability? AiTokenAvailability { get; init; }

    public AiEffortMetricSourceCategory? AiSourceCategory { get; init; }

    public bool CurrentRowsOnly { get; init; } = true;

    public bool IncludeSupersededRows { get; init; }

    public ActualTimeReportSortBy SortBy { get; init; } = ActualTimeReportSortBy.Period;

    public TimeEntryQuerySortDirection SortDirection { get; init; } = TimeEntryQuerySortDirection.Ascending;

    public int PageSize { get; init; } = 50;

    public string? Cursor { get; init; }
}
