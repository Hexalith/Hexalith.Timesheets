using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.ReadModels.ApprovedTimeLedger;

/// <summary>Defines ledger ordering across projection and read paths.</summary>
internal static class ApprovedTimeLedgerOrdering
{
    /// <summary>Orders filtered ledger rows with stable owner and row-state tie-breakers.</summary>
    internal static IReadOnlyList<ApprovedTimeLedgerRowReadModel> Sort(
        IReadOnlyList<ApprovedTimeLedgerRowReadModel> rows,
        QueryApprovedTimeLedger query)
    {
        bool descending = query.SortDirection == TimeEntryQuerySortDirection.Descending;

        IOrderedEnumerable<ApprovedTimeLedgerRowReadModel> ordered = query.SortBy switch
        {
            TimeEntryQuerySortBy.TimeEntryId => OrderByPrimary(
                rows, static row => row.TimeEntryId.Value, StringComparer.Ordinal, descending),
            TimeEntryQuerySortBy.DurationMinutes => OrderByPrimary(
                rows, static row => row.DurationMinutes, Comparer<int>.Default, descending),
            _ => OrderByPrimary(
                rows, static row => row.ServiceDate, Comparer<DateOnly>.Default, descending)
        };

        // Deterministic tie-breakers always remain ascending so paging is stable across replay/rebuild.
        return ordered
            .ThenBy(static row => row.TimeEntryId.Value, StringComparer.Ordinal)
            .ThenBy(static row => row.RowState)
            .ToArray();
    }

    private static IOrderedEnumerable<ApprovedTimeLedgerRowReadModel> OrderByPrimary<TKey>(
        IReadOnlyList<ApprovedTimeLedgerRowReadModel> rows,
        Func<ApprovedTimeLedgerRowReadModel, TKey> keySelector,
        IComparer<TKey> comparer,
        bool descending)
        => descending
            ? rows.OrderByDescending(keySelector, comparer)
            : rows.OrderBy(keySelector, comparer);
}
