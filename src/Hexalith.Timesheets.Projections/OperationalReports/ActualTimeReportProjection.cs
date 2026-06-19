using System.Globalization;
using System.Text;

using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.Queries.TimeEntries;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections.ApprovedTimeLedger;
using Hexalith.Timesheets.Projections.TimeEntries;

namespace Hexalith.Timesheets.Projections.OperationalReports;

public sealed class ActualTimeReportProjection
{
    public const string ProjectionName = "actual-time-report";

    private const int DefaultPageSize = 50;

    private const int MaxPageSize = 500;

    private readonly ApprovedTimeLedgerProjection _ledgerProjection = new();
    private readonly TimeEntryEvidenceListProjection _evidenceListProjection = new();

    public ActualTimeReportReadModel ProjectByProject(
        string tenantId,
        IEnumerable<TimeEntryProjectionEvent> events,
        TimesheetsProjectionCheckpoint checkpoint,
        QueryProjectActualTimeReport query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(query);

        TimeEntryProjectionEvent[] eventList = events
            .OrderBy(static projectionEvent => projectionEvent.SequenceNumber)
            .ToArray();

        return Project(
            tenantId,
            eventList,
            checkpoint,
            new ProjectQueryAdapter(query),
            TimeEntryTargetKind.Project);
    }

    public ActualTimeReportReadModel ProjectByWork(
        string tenantId,
        IEnumerable<TimeEntryProjectionEvent> events,
        TimesheetsProjectionCheckpoint checkpoint,
        QueryWorkActualTimeReport query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(query);

        TimeEntryProjectionEvent[] eventList = events
            .OrderBy(static projectionEvent => projectionEvent.SequenceNumber)
            .ToArray();

        return Project(
            tenantId,
            eventList,
            checkpoint,
            new WorkQueryAdapter(query),
            TimeEntryTargetKind.Work);
    }

    private ActualTimeReportReadModel Project(
        string tenantId,
        IReadOnlyList<TimeEntryProjectionEvent> events,
        TimesheetsProjectionCheckpoint checkpoint,
        IActualTimeReportQuery query,
        TimeEntryTargetKind targetKind)
    {
        Dictionary<ReportGroupKey, ReportGroupAccumulator> groups = [];
        foreach (ReportSourceRow row in SourceRows(tenantId, events, checkpoint, query, targetKind))
        {
            ReportGroupKey key = ReportGroupKey.From(row);
            if (!groups.TryGetValue(key, out ReportGroupAccumulator? accumulator))
            {
                accumulator = new(key);
                groups.Add(key, accumulator);
            }

            accumulator.Add(row);
        }

        IReadOnlyList<ActualTimeReportRowReadModel> sortedRows = Sort(
            groups.Values.Select(accumulator => accumulator.ToReadModel(checkpoint)).ToArray(),
            query);
        int pageSize = NormalizePageSize(query.PageSize);
        int offset = DecodeCursor(query.Cursor);
        ActualTimeReportRowReadModel[] pageItems = sortedRows
            .Skip(offset)
            .Take(pageSize)
            .ToArray();
        string? nextCursor = offset + pageItems.Length < sortedRows.Count
            ? EncodeCursor(offset + pageItems.Length)
            : null;

        return new(
            pageItems,
            nextCursor,
            ProjectionFreshnessMetadataMapper.ToMetadata(checkpoint));
    }

    private IEnumerable<ReportSourceRow> SourceRows(
        string tenantId,
        IReadOnlyList<TimeEntryProjectionEvent> events,
        TimesheetsProjectionCheckpoint checkpoint,
        IActualTimeReportQuery query,
        TimeEntryTargetKind targetKind)
    {
        if (query.ApprovalState is null || query.ApprovalState == TimeEntryApprovalState.Approved)
        {
            foreach (ApprovedTimeLedgerRowReadModel row in ApprovedRows(tenantId, events, checkpoint, query, targetKind))
            {
                if (MatchesContributorCategory(row.ContributorCategory, query.ContributorCategory))
                {
                    yield return ReportSourceRow.FromLedger(row);
                }
            }
        }

        if (query.ApprovalState == TimeEntryApprovalState.Approved)
        {
            yield break;
        }

        foreach (TimeEntryQueryRowReadModel row in EvidenceRows(tenantId, events, checkpoint, query, targetKind))
        {
            yield return ReportSourceRow.FromEvidence(row);
        }
    }

    private IEnumerable<ApprovedTimeLedgerRowReadModel> ApprovedRows(
        string tenantId,
        IReadOnlyList<TimeEntryProjectionEvent> events,
        TimesheetsProjectionCheckpoint checkpoint,
        IActualTimeReportQuery query,
        TimeEntryTargetKind targetKind)
    {
        string? cursor = null;
        do
        {
            ApprovedTimeLedgerReadModel page = _ledgerProjection.Project(
                tenantId,
                events,
                checkpoint,
                new QueryApprovedTimeLedger
                {
                    Project = targetKind == TimeEntryTargetKind.Project ? query.Project : null,
                    Work = targetKind == TimeEntryTargetKind.Work ? query.Work : null,
                    Contributor = query.Contributor,
                    ActivityTypeId = query.ActivityTypeId,
                    TenantLocalPeriodKey = query.TenantLocalPeriodKey,
                    ServiceDateFrom = query.ServiceDateFrom,
                    ServiceDateTo = query.ServiceDateTo,
                    BillableState = query.BillableState,
                    CurrentRowsOnly = query.CurrentRowsOnly,
                    IncludeSupersededRows = query.IncludeSupersededRows,
                    SortBy = TimeEntryQuerySortBy.TimeEntryId,
                    PageSize = MaxPageSize,
                    Cursor = cursor
                });

            foreach (ApprovedTimeLedgerRowReadModel row in page.Items)
            {
                if (row.Target.TargetKind == targetKind)
                {
                    yield return row;
                }
            }

            cursor = page.NextCursor;
        }
        while (cursor is not null);
    }

    private IEnumerable<TimeEntryQueryRowReadModel> EvidenceRows(
        string tenantId,
        IReadOnlyList<TimeEntryProjectionEvent> events,
        TimesheetsProjectionCheckpoint checkpoint,
        IActualTimeReportQuery query,
        TimeEntryTargetKind targetKind)
    {
        IReadOnlyList<TimeEntryApprovalState> approvalStates = query.ApprovalState is null
            ? [TimeEntryApprovalState.Draft, TimeEntryApprovalState.Submitted, TimeEntryApprovalState.Rejected]
            : [query.ApprovalState.Value];
        string? cursor = null;
        do
        {
            TimeEntryQueryReadModel page = _evidenceListProjection.Project(
                tenantId,
                events,
                checkpoint,
                new QueryTimeEntries
                {
                    Project = targetKind == TimeEntryTargetKind.Project ? query.Project : null,
                    Work = targetKind == TimeEntryTargetKind.Work ? query.Work : null,
                    Contributor = query.Contributor,
                    ActivityTypeId = query.ActivityTypeId,
                    TenantLocalPeriodKey = query.TenantLocalPeriodKey,
                    ServiceDateFrom = query.ServiceDateFrom,
                    ServiceDateTo = query.ServiceDateTo,
                    BillableState = query.BillableState,
                    ApprovalStates = approvalStates,
                    ContributorCategories = query.ContributorCategory is null ? [] : [query.ContributorCategory.Value],
                    CurrentEntriesOnly = query.CurrentRowsOnly,
                    IncludeNonCurrentStates = query.IncludeSupersededRows,
                    SortBy = TimeEntryQuerySortBy.TimeEntryId,
                    PageSize = MaxPageSize,
                    Cursor = cursor
                });

            foreach (TimeEntryQueryRowReadModel row in page.Items)
            {
                if (row.Target.TargetKind == targetKind)
                {
                    yield return row;
                }
            }

            cursor = page.NextCursor;
        }
        while (cursor is not null);
    }

    private static bool MatchesContributorCategory(ContributorCategory actual, ContributorCategory? expected)
        => expected is null || actual == expected.Value;

    private static IReadOnlyList<ActualTimeReportRowReadModel> Sort(
        IReadOnlyList<ActualTimeReportRowReadModel> rows,
        IActualTimeReportQuery query)
    {
        bool descending = query.SortDirection == TimeEntryQuerySortDirection.Descending;
        IOrderedEnumerable<ActualTimeReportRowReadModel> ordered = query.SortBy switch
        {
            ActualTimeReportSortBy.TargetReference => OrderByPrimary(
                rows, static row => row.Target.TargetId, StringComparer.Ordinal, descending),
            ActualTimeReportSortBy.Contributor => OrderByPrimary(
                rows, static row => row.Contributor.PartyId, StringComparer.Ordinal, descending),
            ActualTimeReportSortBy.ActivityType => OrderByPrimary(
                rows, static row => row.ActivityTypeId.Value, StringComparer.Ordinal, descending),
            ActualTimeReportSortBy.ActualMinutes => OrderByPrimary(
                rows, static row => row.ActualMinutes, Comparer<int>.Default, descending),
            ActualTimeReportSortBy.SourceRowCount => OrderByPrimary(
                rows, static row => row.SourceRowCount, Comparer<int>.Default, descending),
            _ => OrderByPrimary(
                rows, static row => row.PeriodStart, Comparer<DateOnly>.Default, descending)
        };

        // Tie-breakers must cover every grouping dimension so ordering and cursor paging stay stable
        // across replay/rebuild. ActivityTypeScope is part of the group key (the same Activity Type id
        // can be recorded at Tenant and Project scope), so it must participate here too.
        return ordered
            .ThenBy(static row => row.Target.TargetKind)
            .ThenBy(static row => row.Target.TargetId, StringComparer.Ordinal)
            .ThenBy(static row => row.TenantLocalPeriodKey, StringComparer.Ordinal)
            .ThenBy(static row => row.Contributor.PartyId, StringComparer.Ordinal)
            .ThenBy(static row => row.ActivityTypeId.Value, StringComparer.Ordinal)
            .ThenBy(static row => row.ActivityTypeScope)
            .ThenBy(static row => row.BillableState)
            .ThenBy(static row => row.ApprovalState)
            .ThenBy(static row => row.ContributorCategory)
            .ToArray();
    }

    private static IOrderedEnumerable<ActualTimeReportRowReadModel> OrderByPrimary<TKey>(
        IReadOnlyList<ActualTimeReportRowReadModel> rows,
        Func<ActualTimeReportRowReadModel, TKey> keySelector,
        IComparer<TKey> comparer,
        bool descending)
        => descending
            ? rows.OrderByDescending(keySelector, comparer)
            : rows.OrderBy(keySelector, comparer);

    private static int NormalizePageSize(int requestedPageSize)
        => requestedPageSize <= 0
            ? DefaultPageSize
            : Math.Min(requestedPageSize, MaxPageSize);

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

    private interface IActualTimeReportQuery
    {
        ProjectReference? Project { get; }

        WorkReference? Work { get; }

        PartyReference? Contributor { get; }

        ActivityTypeId? ActivityTypeId { get; }

        string? TenantLocalPeriodKey { get; }

        DateOnly? ServiceDateFrom { get; }

        DateOnly? ServiceDateTo { get; }

        BillableState? BillableState { get; }

        TimeEntryApprovalState? ApprovalState { get; }

        ContributorCategory? ContributorCategory { get; }

        bool CurrentRowsOnly { get; }

        bool IncludeSupersededRows { get; }

        ActualTimeReportSortBy SortBy { get; }

        TimeEntryQuerySortDirection SortDirection { get; }

        int PageSize { get; }

        string? Cursor { get; }
    }

    private sealed record ProjectQueryAdapter(QueryProjectActualTimeReport Query) : IActualTimeReportQuery
    {
        public ProjectReference? Project => Query.Project;

        public WorkReference? Work => null;

        public PartyReference? Contributor => Query.Contributor;

        public ActivityTypeId? ActivityTypeId => Query.ActivityTypeId;

        public string? TenantLocalPeriodKey => Query.TenantLocalPeriodKey;

        public DateOnly? ServiceDateFrom => Query.ServiceDateFrom;

        public DateOnly? ServiceDateTo => Query.ServiceDateTo;

        public BillableState? BillableState => Query.BillableState;

        public TimeEntryApprovalState? ApprovalState => Query.ApprovalState;

        public ContributorCategory? ContributorCategory => Query.ContributorCategory;

        public bool CurrentRowsOnly => Query.CurrentRowsOnly;

        public bool IncludeSupersededRows => Query.IncludeSupersededRows;

        public ActualTimeReportSortBy SortBy => Query.SortBy;

        public TimeEntryQuerySortDirection SortDirection => Query.SortDirection;

        public int PageSize => Query.PageSize;

        public string? Cursor => Query.Cursor;
    }

    private sealed record WorkQueryAdapter(QueryWorkActualTimeReport Query) : IActualTimeReportQuery
    {
        public ProjectReference? Project => null;

        public WorkReference? Work => Query.Work;

        public PartyReference? Contributor => Query.Contributor;

        public ActivityTypeId? ActivityTypeId => Query.ActivityTypeId;

        public string? TenantLocalPeriodKey => Query.TenantLocalPeriodKey;

        public DateOnly? ServiceDateFrom => Query.ServiceDateFrom;

        public DateOnly? ServiceDateTo => Query.ServiceDateTo;

        public BillableState? BillableState => Query.BillableState;

        public TimeEntryApprovalState? ApprovalState => Query.ApprovalState;

        public ContributorCategory? ContributorCategory => Query.ContributorCategory;

        public bool CurrentRowsOnly => Query.CurrentRowsOnly;

        public bool IncludeSupersededRows => Query.IncludeSupersededRows;

        public ActualTimeReportSortBy SortBy => Query.SortBy;

        public TimeEntryQuerySortDirection SortDirection => Query.SortDirection;

        public int PageSize => Query.PageSize;

        public string? Cursor => Query.Cursor;
    }

    private sealed record ReportGroupKey(
        TimeEntryTargetReference Target,
        string TenantLocalPeriodKey,
        DateOnly PeriodStart,
        DateOnly PeriodEnd,
        PartyReference Contributor,
        ActivityTypeId ActivityTypeId,
        ActivityTypeScope ActivityTypeScope,
        BillableState BillableState,
        TimeEntryApprovalState ApprovalState,
        ContributorCategory ContributorCategory)
    {
        public static ReportGroupKey From(ReportSourceRow row)
        {
            // Report rollups bucket by calendar month derived from the service date. Weekly tenant
            // periods are not bucketed here; callers that need weekly windows filter with an explicit
            // ServiceDateFrom/ServiceDateTo range. This monthly assumption is recorded as quality
            // evidence rather than hidden, matching the launch report sizing in docs/performance-evidence.md.
            DateOnly periodStart = new(row.ServiceDate.Year, row.ServiceDate.Month, 1);
            DateOnly periodEnd = periodStart.AddMonths(1).AddDays(-1);
            string periodKey = string.Create(
                CultureInfo.InvariantCulture,
                $"{periodStart.Year:D4}-{periodStart.Month:D2}");

            return new(
                row.Target,
                periodKey,
                periodStart,
                periodEnd,
                row.Contributor,
                row.ActivityTypeId,
                row.ActivityTypeScope,
                row.BillableState,
                row.ApprovalState,
                row.ContributorCategory);
        }
    }

    private sealed class ReportGroupAccumulator(ReportGroupKey key)
    {
        private int _actualMinutes;
        private int _sourceRowCount;
        private int _correctionCount;
        private int _supersededCount;

        public void Add(ReportSourceRow row)
        {
            _actualMinutes += row.DurationMinutes;
            _sourceRowCount++;
            if (row.IsCorrected)
            {
                _correctionCount++;
            }

            if (row.IsSuperseded)
            {
                _supersededCount++;
            }
        }

        public ActualTimeReportRowReadModel ToReadModel(TimesheetsProjectionCheckpoint checkpoint)
        {
            ProjectionFreshnessMetadata freshness = ProjectionFreshnessMetadataMapper.ToMetadata(checkpoint);
            ActualTimeReportRowState rowState = _supersededCount > 0
                ? ActualTimeReportRowState.IncludesSuperseded
                : ActualTimeReportRowState.Current;

            return new(
                key.Target,
                key.TenantLocalPeriodKey,
                key.PeriodStart,
                key.PeriodEnd,
                key.Contributor,
                key.ActivityTypeId,
                key.ActivityTypeScope,
                key.BillableState,
                key.ApprovalState,
                key.ContributorCategory,
                _actualMinutes,
                _sourceRowCount,
                _correctionCount,
                _supersededCount,
                rowState,
                ReferenceStateFrom(freshness),
                freshness)
            {
                WorkPlannedEffort = key.Target.TargetKind == TimeEntryTargetKind.Work
                    ? WorkPlannedEffortReadModel.NotSupplied()
                    : null
            };
        }

        private static ActualTimeReferenceStateMetadata ReferenceStateFrom(ProjectionFreshnessMetadata freshness)
            => freshness.State switch
            {
                ProjectionFreshnessState.Rebuilding => ActualTimeReferenceStateMetadata.Rebuilding(),
                ProjectionFreshnessState.Stale => new(
                    ActualTimeReferenceState.Stale,
                    freshness,
                    "Reference state may be stale."),
                ProjectionFreshnessState.Unavailable => ActualTimeReferenceStateMetadata.Unavailable(),
                _ => ActualTimeReferenceStateMetadata.Current
            };
    }

    private sealed record ReportSourceRow(
        TimeEntryTargetReference Target,
        PartyReference Contributor,
        ActivityTypeId ActivityTypeId,
        ActivityTypeScope ActivityTypeScope,
        DateOnly ServiceDate,
        int DurationMinutes,
        BillableState BillableState,
        TimeEntryApprovalState ApprovalState,
        ContributorCategory ContributorCategory,
        bool IsCorrected,
        bool IsSuperseded)
    {
        public static ReportSourceRow FromLedger(ApprovedTimeLedgerRowReadModel row)
            => new(
                row.Target,
                row.Contributor,
                row.ActivityTypeId,
                row.ActivityTypeScope,
                row.ServiceDate,
                row.DurationMinutes,
                row.BillableState,
                TimeEntryApprovalState.Approved,
                row.ContributorCategory,
                row.ApprovedCorrection is not null || row.Correction is not null,
                row.RowState == ApprovedTimeLedgerRowState.Superseded);

        public static ReportSourceRow FromEvidence(TimeEntryQueryRowReadModel row)
            => new(
                row.Target,
                row.Contributor,
                row.ActivityTypeId,
                row.ActivityTypeScope,
                row.ServiceDate,
                row.DurationMinutes,
                row.BillableState,
                row.ApprovalState,
                row.ContributorCategory,
                row.CorrectionState == TimeEntryCorrectionState.Corrected,
                row.CorrectionState == TimeEntryCorrectionState.Superseded);
    }
}
