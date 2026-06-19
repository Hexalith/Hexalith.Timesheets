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
                ReportSourceRow sourceRow = ReportSourceRow.FromLedger(row);
                if (MatchesSourceRow(sourceRow, query))
                {
                    yield return sourceRow;
                }
            }
        }

        if (query.ApprovalState == TimeEntryApprovalState.Approved)
        {
            yield break;
        }

        foreach (TimeEntryQueryRowReadModel row in EvidenceRows(tenantId, events, checkpoint, query, targetKind))
        {
            ReportSourceRow sourceRow = ReportSourceRow.FromEvidence(row);
            if (MatchesSourceRow(sourceRow, query))
            {
                yield return sourceRow;
            }
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
                    Contributor = EffectiveContributor(query),
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
                    Contributor = EffectiveContributor(query),
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

    private static bool MatchesSourceRow(
        ReportSourceRow row,
        IActualTimeReportQuery query)
        => MatchesContributorCategory(row.ContributorCategory, query.ContributorCategory)
            && MatchesAiAgent(row, query.AiAgent)
            && MatchesAiMetricAvailability(row.AiMetrics, query.AiMetricAvailability)
            && MatchesAiTokenAvailability(row.AiMetrics, query.AiTokenAvailability)
            && MatchesAiSourceCategory(row.AiMetrics, query.AiSourceCategory);

    private static PartyReference? EffectiveContributor(IActualTimeReportQuery query)
        => query.Contributor is not null && query.AiAgent is not null && query.Contributor != query.AiAgent
            ? new PartyReference("__no-matching-contributor__")
            : query.Contributor ?? query.AiAgent;

    private static bool MatchesContributorCategory(ContributorCategory actual, ContributorCategory? expected)
        => expected is null || actual == expected.Value;

    private static bool MatchesAiAgent(
        ReportSourceRow row,
        PartyReference? expected)
        => expected is null
            || (row.ContributorCategory == ContributorCategory.AutomatedAgent
                && EqualityComparer<PartyReference>.Default.Equals(row.Contributor, expected));

    private static bool MatchesAiMetricAvailability(
        AiEffortMetrics? metrics,
        AiMetricAvailability? expected)
        => expected is null
            || (metrics?.Availability ?? AiMetricAvailability.Unavailable) == expected.Value;

    private static bool MatchesAiTokenAvailability(
        AiEffortMetrics? metrics,
        AiTokenMetricAvailability? expected)
        => expected is null
            || (metrics?.TokenAvailability ?? AiTokenMetricAvailability.Unavailable) == expected.Value;

    private static bool MatchesAiSourceCategory(
        AiEffortMetrics? metrics,
        AiEffortMetricSourceCategory? expected)
        => expected is null
            || (metrics?.Source?.SourceCategory ?? AiEffortMetricSourceCategory.Unavailable) == expected.Value;

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
            ActualTimeReportSortBy.AiWallClockDurationMilliseconds => OrderByPrimary(
                rows, static row => row.AiWallClockDurationMilliseconds.GetValueOrDefault(), Comparer<int>.Default, descending),
            ActualTimeReportSortBy.AiModelRuntimeMilliseconds => OrderByPrimary(
                rows, static row => row.AiModelRuntimeMilliseconds.GetValueOrDefault(), Comparer<int>.Default, descending),
            ActualTimeReportSortBy.AiBillableEffortMinutes => OrderByPrimary(
                rows, static row => row.AiBillableEffortMinutes.GetValueOrDefault(), Comparer<int>.Default, descending),
            ActualTimeReportSortBy.AiProviderTotalTokenCount => OrderByPrimary(
                rows, static row => row.AiProviderTotalTokenCount.GetValueOrDefault(), Comparer<long>.Default, descending),
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

        PartyReference? AiAgent { get; }

        ActivityTypeId? ActivityTypeId { get; }

        string? TenantLocalPeriodKey { get; }

        DateOnly? ServiceDateFrom { get; }

        DateOnly? ServiceDateTo { get; }

        BillableState? BillableState { get; }

        TimeEntryApprovalState? ApprovalState { get; }

        ContributorCategory? ContributorCategory { get; }

        AiMetricAvailability? AiMetricAvailability { get; }

        AiTokenMetricAvailability? AiTokenAvailability { get; }

        AiEffortMetricSourceCategory? AiSourceCategory { get; }

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

        public PartyReference? AiAgent => Query.AiAgent;

        public ActivityTypeId? ActivityTypeId => Query.ActivityTypeId;

        public string? TenantLocalPeriodKey => Query.TenantLocalPeriodKey;

        public DateOnly? ServiceDateFrom => Query.ServiceDateFrom;

        public DateOnly? ServiceDateTo => Query.ServiceDateTo;

        public BillableState? BillableState => Query.BillableState;

        public TimeEntryApprovalState? ApprovalState => Query.ApprovalState;

        public ContributorCategory? ContributorCategory => Query.ContributorCategory;

        public AiMetricAvailability? AiMetricAvailability => Query.AiMetricAvailability;

        public AiTokenMetricAvailability? AiTokenAvailability => Query.AiTokenAvailability;

        public AiEffortMetricSourceCategory? AiSourceCategory => Query.AiSourceCategory;

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

        public PartyReference? AiAgent => Query.AiAgent;

        public ActivityTypeId? ActivityTypeId => Query.ActivityTypeId;

        public string? TenantLocalPeriodKey => Query.TenantLocalPeriodKey;

        public DateOnly? ServiceDateFrom => Query.ServiceDateFrom;

        public DateOnly? ServiceDateTo => Query.ServiceDateTo;

        public BillableState? BillableState => Query.BillableState;

        public TimeEntryApprovalState? ApprovalState => Query.ApprovalState;

        public ContributorCategory? ContributorCategory => Query.ContributorCategory;

        public AiMetricAvailability? AiMetricAvailability => Query.AiMetricAvailability;

        public AiTokenMetricAvailability? AiTokenAvailability => Query.AiTokenAvailability;

        public AiEffortMetricSourceCategory? AiSourceCategory => Query.AiSourceCategory;

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
        private int _aiWallClockDurationMilliseconds;
        private int _aiModelRuntimeMilliseconds;
        private int _aiBillableEffortMinutes;
        private long _aiProviderInputTokenCount;
        private long _aiProviderOutputTokenCount;
        private long _aiProviderTotalTokenCount;
        private int _sourceRowCount;
        private int _correctionCount;
        private int _supersededCount;
        private bool _hasAiWallClockDurationMilliseconds;
        private bool _hasAiModelRuntimeMilliseconds;
        private bool _hasAiBillableEffortMinutes;
        private bool _hasAiProviderInputTokenCount;
        private bool _hasAiProviderOutputTokenCount;
        private bool _hasAiProviderTotalTokenCount;
        private AiMetricAvailability _aiMetricAvailability = AiMetricAvailability.Unavailable;
        private AiTokenMetricAvailability _aiTokenAvailability = AiTokenMetricAvailability.Unavailable;
        private AiEffortMetricSourceMetadata? _aiMetricSourceMetadata;

        public void Add(ReportSourceRow row)
        {
            if (row.ContributorCategory != ContributorCategory.AutomatedAgent)
            {
                _actualMinutes += row.DurationMinutes;
            }

            AddAiMetrics(row.AiMetrics);
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
                AiWallClockDurationMilliseconds = _hasAiWallClockDurationMilliseconds
                    ? _aiWallClockDurationMilliseconds
                    : null,
                AiModelRuntimeMilliseconds = _hasAiModelRuntimeMilliseconds
                    ? _aiModelRuntimeMilliseconds
                    : null,
                AiBillableEffortMinutes = _hasAiBillableEffortMinutes
                    ? _aiBillableEffortMinutes
                    : null,
                AiProviderInputTokenCount = _hasAiProviderInputTokenCount
                    ? _aiProviderInputTokenCount
                    : null,
                AiProviderOutputTokenCount = _hasAiProviderOutputTokenCount
                    ? _aiProviderOutputTokenCount
                    : null,
                AiProviderTotalTokenCount = _hasAiProviderTotalTokenCount
                    ? _aiProviderTotalTokenCount
                    : null,
                AiMetricAvailability = _aiMetricAvailability,
                AiTokenAvailability = _aiTokenAvailability,
                AiMetricSourceMetadata = _aiMetricSourceMetadata,
                WorkPlannedEffort = key.Target.TargetKind == TimeEntryTargetKind.Work
                    ? WorkPlannedEffortReadModel.NotSupplied()
                    : null
            };
        }

        private void AddAiMetrics(AiEffortMetrics? metrics)
        {
            if (metrics is null || metrics.Availability == AiMetricAvailability.Unavailable)
            {
                return;
            }

            _aiMetricAvailability = MergeMetricAvailability(_aiMetricAvailability, metrics.Availability);
            _aiTokenAvailability = MergeTokenAvailability(_aiTokenAvailability, metrics.TokenAvailability);
            _aiMetricSourceMetadata ??= metrics.Source;

            AddNullable(metrics.WallClockDurationMilliseconds, ref _aiWallClockDurationMilliseconds, ref _hasAiWallClockDurationMilliseconds);
            AddNullable(metrics.ModelRuntimeMilliseconds, ref _aiModelRuntimeMilliseconds, ref _hasAiModelRuntimeMilliseconds);
            AddNullable(metrics.BillableEffortMinutes, ref _aiBillableEffortMinutes, ref _hasAiBillableEffortMinutes);
            AddNullable(metrics.ProviderInputTokenCount, ref _aiProviderInputTokenCount, ref _hasAiProviderInputTokenCount);
            AddNullable(metrics.ProviderOutputTokenCount, ref _aiProviderOutputTokenCount, ref _hasAiProviderOutputTokenCount);
            AddNullable(metrics.ProviderTotalTokenCount, ref _aiProviderTotalTokenCount, ref _hasAiProviderTotalTokenCount);
        }

        private static AiMetricAvailability MergeMetricAvailability(
            AiMetricAvailability current,
            AiMetricAvailability next)
            => (current, next) switch
            {
                (_, AiMetricAvailability.Unknown) => AiMetricAvailability.Unknown,
                (AiMetricAvailability.Unknown, _) => AiMetricAvailability.Unknown,
                (_, AiMetricAvailability.Estimated) => AiMetricAvailability.Estimated,
                (AiMetricAvailability.Estimated, _) => AiMetricAvailability.Estimated,
                (_, AiMetricAvailability.ProviderReported) => AiMetricAvailability.ProviderReported,
                _ => current
            };

        // Conservative, order-independent precedence: Unknown > NotReported > ProviderReported.
        // A group is only ProviderReported when every contributing row was provider-reported; any
        // not-reported sibling keeps the aggregate "Not reported by provider" regardless of the
        // order events are replayed in. Mirrors MergeMetricAvailability so both stay deterministic.
        private static AiTokenMetricAvailability MergeTokenAvailability(
            AiTokenMetricAvailability current,
            AiTokenMetricAvailability next)
            => (current, next) switch
            {
                (_, AiTokenMetricAvailability.Unknown) => AiTokenMetricAvailability.Unknown,
                (AiTokenMetricAvailability.Unknown, _) => AiTokenMetricAvailability.Unknown,
                (_, AiTokenMetricAvailability.NotReported) => AiTokenMetricAvailability.NotReported,
                (AiTokenMetricAvailability.NotReported, _) => AiTokenMetricAvailability.NotReported,
                (_, AiTokenMetricAvailability.ProviderReported) => AiTokenMetricAvailability.ProviderReported,
                _ => current
            };

        private static void AddNullable(int? value, ref int total, ref bool hasValue)
        {
            if (value is null)
            {
                return;
            }

            total += value.Value;
            hasValue = true;
        }

        private static void AddNullable(long? value, ref long total, ref bool hasValue)
        {
            if (value is null)
            {
                return;
            }

            total += value.Value;
            hasValue = true;
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
        AiEffortMetrics? AiMetrics,
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
                row.AiMetrics,
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
                row.AiMetrics,
                row.CorrectionState == TimeEntryCorrectionState.Corrected,
                row.CorrectionState == TimeEntryCorrectionState.Superseded);
    }
}
