using System.Globalization;

using Hexalith.Timesheets.Contracts.Models;
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

    /// <summary>Determines membership in the ledger filter scope independently of paging and sorting.</summary>
    public bool Includes(ApprovedTimeLedgerRowReadModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (CurrentRowsOnly
            && !IncludeSupersededRows
            && row.RowState == ApprovedTimeLedgerRowState.Superseded)
        {
            return false;
        }

        return MatchesContributor(row, Contributor)
            && MatchesProject(row, Project)
            && MatchesWork(row, Work)
            && MatchesTenantLocalPeriod(row, TenantLocalPeriodKey)
            && MatchesDateRange(row, ServiceDateFrom, ServiceDateTo)
            && MatchesValue(row.ActivityTypeId, ActivityTypeId)
            && MatchesNullableEnum(row.BillableState, BillableState);
    }

    private static bool MatchesContributor(ApprovedTimeLedgerRowReadModel row, PartyReference? contributor)
        => contributor is null || row.Contributor == contributor;

    private static bool MatchesProject(ApprovedTimeLedgerRowReadModel row, ProjectReference? project)
        => project is null
            || (row.Target.TargetKind == TimeEntryTargetKind.Project
                && string.Equals(row.Target.TargetId, project.ProjectId, StringComparison.Ordinal));

    private static bool MatchesWork(ApprovedTimeLedgerRowReadModel row, WorkReference? work)
        => work is null
            || (row.Target.TargetKind == TimeEntryTargetKind.Work
                && string.Equals(row.Target.TargetId, work.WorkId, StringComparison.Ordinal));

    private static bool MatchesTenantLocalPeriod(ApprovedTimeLedgerRowReadModel row, string? periodKey)
    {
        if (string.IsNullOrWhiteSpace(periodKey))
        {
            return true;
        }

        if (TryParsePeriodKey(periodKey, out DateOnly start, out DateOnly end))
        {
            return row.ServiceDate >= start && row.ServiceDate <= end;
        }

        return false;
    }

    private static bool MatchesDateRange(ApprovedTimeLedgerRowReadModel row, DateOnly? from, DateOnly? to)
        => (from is null || row.ServiceDate >= from.Value)
            && (to is null || row.ServiceDate <= to.Value);

    private static bool MatchesValue<T>(T actual, T? expected)
        where T : class
        => expected is null || EqualityComparer<T>.Default.Equals(actual, expected);

    private static bool MatchesNullableEnum<T>(T actual, T? expected)
        where T : struct, Enum
        => expected is null || EqualityComparer<T>.Default.Equals(actual, expected.Value);

    private static bool TryParsePeriodKey(string periodKey, out DateOnly start, out DateOnly end)
    {
        string normalized = periodKey.Trim();

        if (normalized.Length == 7
            && DateOnly.TryParseExact(
                normalized + "-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out start))
        {
            end = new(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month));
            return true;
        }

        string[] parts = normalized.Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2
            && DateOnly.TryParseExact(
                parts[0],
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out start)
            && DateOnly.TryParseExact(
                parts[1],
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out end)
            && start <= end)
        {
            return true;
        }

        start = default;
        end = default;
        return false;
    }
}
