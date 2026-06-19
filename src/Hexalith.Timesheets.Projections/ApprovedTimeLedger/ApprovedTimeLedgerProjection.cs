using System.Globalization;
using System.Text;

using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections.TimeEntries;

namespace Hexalith.Timesheets.Projections.ApprovedTimeLedger;

public sealed class ApprovedTimeLedgerProjection
{
    public const string ProjectionName = "approved-time-ledger";

    private const int DefaultPageSize = 50;

    private const int MaxPageSize = 500;

    private readonly TimeEntryEvidenceProjection _evidenceProjection = new();

    public ApprovedTimeLedgerReadModel Project(
        string tenantId,
        IEnumerable<TimeEntryProjectionEvent> events,
        TimesheetsProjectionCheckpoint checkpoint,
        QueryApprovedTimeLedger query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(query);

        TimeEntryProjectionEvent[] eventList = events
            .OrderBy(static projectionEvent => projectionEvent.SequenceNumber)
            .ToArray();

        List<ApprovedTimeLedgerRowReadModel> rows = [];
        foreach (TimeEntryId timeEntryId in CandidateIds(eventList))
        {
            TimeEntryEvidenceReadModel? evidence = _evidenceProjection.Project(
                tenantId,
                timeEntryId,
                eventList,
                checkpoint);

            if (evidence?.ApprovalState != TimeEntryApprovalState.Approved
                || evidence.ApprovalDecision is null)
            {
                continue;
            }

            rows.Add(ApprovedTimeLedgerRowReadModel.CurrentFromEvidence(evidence));

            ApprovedTimeLedgerRowReadModel? superseded = ApprovedTimeLedgerRowReadModel
                .SupersededFromApprovedCorrection(evidence);
            if (superseded is not null)
            {
                rows.Add(superseded);
            }
        }

        IReadOnlyList<ApprovedTimeLedgerRowReadModel> filteredRows = rows
            .Where(row => Matches(row, query))
            .ToArray();
        IReadOnlyList<ApprovedTimeLedgerRowReadModel> sortedRows = Sort(filteredRows, query);
        int pageSize = NormalizePageSize(query.PageSize);
        int offset = DecodeCursor(query.Cursor);
        ApprovedTimeLedgerRowReadModel[] pageItems = sortedRows
            .Skip(offset)
            .Take(pageSize)
            .ToArray();
        string? nextCursor = offset + pageItems.Length < sortedRows.Count
            ? EncodeCursor(offset + pageItems.Length)
            : null;
        ProjectionFreshnessMetadata freshness = ProjectionFreshnessMetadataMapper.ToMetadata(checkpoint);

        return new(
            pageItems,
            nextCursor,
            freshness,
            pageItems.Length > 0 && checkpoint.CanServeReads,
            checkpoint.CanServeReads
                ? "Approved ledger rows are fresh enough for export preview."
                : "Projection freshness does not allow export preview.");
    }

    private static IEnumerable<TimeEntryId> CandidateIds(IEnumerable<TimeEntryProjectionEvent> events)
    {
        HashSet<string> seenMessageIds = new(StringComparer.Ordinal);
        HashSet<TimeEntryId> seenTimeEntryIds = [];

        foreach (TimeEntryProjectionEvent projectionEvent in events)
        {
            if (string.IsNullOrWhiteSpace(projectionEvent.MessageId)
                || !seenMessageIds.Add(projectionEvent.MessageId))
            {
                continue;
            }

            TimeEntryId? timeEntryId = projectionEvent.Payload switch
            {
                TimeEntryRecorded recorded => recorded.TimeEntryId,
                TimeEntrySubmitted submitted => submitted.TimeEntryId,
                TimeEntryContributorConfirmed confirmed => confirmed.TimeEntryId,
                TimeEntryAdjustedThroughMagicLink adjusted => adjusted.TimeEntryId,
                TimeEntryApproved approved => approved.TimeEntryId,
                TimeEntryRejected rejected => rejected.TimeEntryId,
                TimeEntryCorrected corrected => corrected.TimeEntryId,
                TimeEntryApprovedCorrected approvedCorrected => approvedCorrected.TimeEntryId,
                _ => null
            };

            if (timeEntryId is not null && seenTimeEntryIds.Add(timeEntryId))
            {
                yield return timeEntryId;
            }
        }
    }

    private static bool Matches(ApprovedTimeLedgerRowReadModel row, QueryApprovedTimeLedger query)
    {
        if (query.CurrentRowsOnly
            && !query.IncludeSupersededRows
            && row.RowState == ApprovedTimeLedgerRowState.Superseded)
        {
            return false;
        }

        return MatchesContributor(row, query.Contributor)
            && MatchesProject(row, query.Project)
            && MatchesWork(row, query.Work)
            && MatchesTenantLocalPeriod(row, query.TenantLocalPeriodKey)
            && MatchesDateRange(row, query.ServiceDateFrom, query.ServiceDateTo)
            && MatchesValue(row.ActivityTypeId, query.ActivityTypeId)
            && MatchesNullableEnum(row.BillableState, query.BillableState);
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

    private static IReadOnlyList<ApprovedTimeLedgerRowReadModel> Sort(
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

    private static int NormalizePageSize(int requestedPageSize)
        => requestedPageSize <= 0
            ? DefaultPageSize
            : Math.Min(requestedPageSize, MaxPageSize);

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
            end = start.AddMonths(1).AddDays(-1);
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

    private static string EncodeCursor(int offset)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(
            string.Create(CultureInfo.InvariantCulture, $"offset:{offset}")));

    private static int DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return 0;
        }

        try
        {
            string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            const string Prefix = "offset:";
            return decoded.StartsWith(Prefix, StringComparison.Ordinal)
                && int.TryParse(decoded[Prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out int offset)
                && offset > 0
                    ? offset
                    : 0;
        }
        catch (FormatException)
        {
            return 0;
        }
    }
}
