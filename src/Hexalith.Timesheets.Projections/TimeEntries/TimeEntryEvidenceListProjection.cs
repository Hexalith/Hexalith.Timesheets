using System.Globalization;
using System.Text;

using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.TimeEntries;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Projections.TimeEntries;

public sealed class TimeEntryEvidenceListProjection
{
    public const string ProjectionName = "time-entry-evidence-list";

    private const int DefaultPageSize = 50;

    private const int MaxPageSize = 500;

    private readonly TimeEntryEvidenceProjection _evidenceProjection = new();

    public TimeEntryQueryReadModel Project(
        string tenantId,
        IEnumerable<TimeEntryProjectionEvent> events,
        TimesheetsProjectionCheckpoint checkpoint,
        QueryTimeEntries query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(query);

        TimeEntryProjectionEvent[] eventList = events
            .OrderBy(static projectionEvent => projectionEvent.SequenceNumber)
            .ToArray();

        List<TimeEntryQueryRowReadModel> rows = [];
        foreach (TimeEntryId timeEntryId in CandidateIds(eventList))
        {
            TimeEntryEvidenceReadModel? evidence = _evidenceProjection.Project(
                tenantId,
                timeEntryId,
                eventList,
                checkpoint);

            if (evidence is null)
            {
                continue;
            }

            TimeEntryQueryRowReadModel row = TimeEntryQueryRowReadModel.FromEvidence(evidence);
            if (Matches(row, query))
            {
                rows.Add(row);
            }
        }

        IReadOnlyList<TimeEntryQueryRowReadModel> sortedRows = Sort(rows, query);
        int pageSize = NormalizePageSize(query.PageSize);
        int offset = DecodeCursor(query.Cursor);
        TimeEntryQueryRowReadModel[] pageItems = sortedRows
            .Skip(offset)
            .Take(pageSize)
            .ToArray();
        string? nextCursor = offset + pageItems.Length < sortedRows.Count
            ? EncodeCursor(offset + pageItems.Length)
            : null;

        return new(pageItems, nextCursor, ToFreshnessMetadata(checkpoint));
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

    private static bool Matches(TimeEntryQueryRowReadModel row, QueryTimeEntries query)
    {
        if (query.CurrentEntriesOnly
            && !query.IncludeNonCurrentStates
            && row.CorrectionState == TimeEntryCorrectionState.Superseded)
        {
            return false;
        }

        return MatchesContributor(row, query.Contributor)
            && MatchesProject(row, query.Project)
            && MatchesWork(row, query.Work)
            && MatchesTenantLocalPeriod(row, query.TenantLocalPeriodKey)
            && MatchesDateRange(row, query.ServiceDateFrom, query.ServiceDateTo)
            && MatchesValue(row.ActivityTypeId, query.ActivityTypeId)
            && MatchesNullableEnum(row.BillableState, query.BillableState)
            && MatchesSet(row.ApprovalState, query.ApprovalStates)
            && MatchesSet(row.CorrectionState, query.CorrectionStates)
            && MatchesSet(row.ContributorCategory, query.ContributorCategories)
            && MatchesSet(row.SourceType, query.SourceTypes);
    }

    private static bool MatchesContributor(TimeEntryQueryRowReadModel row, PartyReference? contributor)
        => contributor is null || row.Contributor == contributor;

    private static bool MatchesProject(TimeEntryQueryRowReadModel row, ProjectReference? project)
        => project is null
            || (row.Target.TargetKind == TimeEntryTargetKind.Project
                && string.Equals(row.Target.TargetId, project.ProjectId, StringComparison.Ordinal));

    private static bool MatchesWork(TimeEntryQueryRowReadModel row, WorkReference? work)
        => work is null
            || (row.Target.TargetKind == TimeEntryTargetKind.Work
                && string.Equals(row.Target.TargetId, work.WorkId, StringComparison.Ordinal));

    private static bool MatchesTenantLocalPeriod(TimeEntryQueryRowReadModel row, string? periodKey)
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

    private static bool MatchesDateRange(TimeEntryQueryRowReadModel row, DateOnly? from, DateOnly? to)
        => (from is null || row.ServiceDate >= from.Value)
            && (to is null || row.ServiceDate <= to.Value);

    private static bool MatchesValue<T>(T actual, T? expected)
        where T : class
        => expected is null || EqualityComparer<T>.Default.Equals(actual, expected);

    private static bool MatchesNullableEnum<T>(T actual, T? expected)
        where T : struct, Enum
        => expected is null || EqualityComparer<T>.Default.Equals(actual, expected.Value);

    private static bool MatchesSet<T>(T actual, IReadOnlyList<T> expected)
        where T : struct, Enum
        => expected.Count == 0 || expected.Contains(actual);

    private static IReadOnlyList<TimeEntryQueryRowReadModel> Sort(
        IReadOnlyList<TimeEntryQueryRowReadModel> rows,
        QueryTimeEntries query)
    {
        IEnumerable<TimeEntryQueryRowReadModel> ordered = query.SortBy switch
        {
            TimeEntryQuerySortBy.TimeEntryId => rows.OrderBy(static row => row.TimeEntryId.Value, StringComparer.Ordinal),
            TimeEntryQuerySortBy.DurationMinutes => rows.OrderBy(static row => row.DurationMinutes)
                .ThenBy(static row => row.TimeEntryId.Value, StringComparer.Ordinal),
            _ => rows.OrderBy(static row => row.ServiceDate)
                .ThenBy(static row => row.TimeEntryId.Value, StringComparer.Ordinal)
        };

        if (query.SortDirection == TimeEntryQuerySortDirection.Descending)
        {
            ordered = query.SortBy switch
            {
                TimeEntryQuerySortBy.TimeEntryId => rows.OrderByDescending(static row => row.TimeEntryId.Value, StringComparer.Ordinal),
                TimeEntryQuerySortBy.DurationMinutes => rows.OrderByDescending(static row => row.DurationMinutes)
                    .ThenBy(static row => row.TimeEntryId.Value, StringComparer.Ordinal),
                _ => rows.OrderByDescending(static row => row.ServiceDate)
                    .ThenBy(static row => row.TimeEntryId.Value, StringComparer.Ordinal)
            };
        }

        return ordered.ToArray();
    }

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

    private static ProjectionFreshnessMetadata ToFreshnessMetadata(TimesheetsProjectionCheckpoint checkpoint)
        => checkpoint.Freshness switch
        {
            ProjectionFreshness.Fresh => new(
                ProjectionFreshnessState.Fresh,
                checkpoint.SequenceNumber.ToString(CultureInfo.InvariantCulture),
                null,
                null),
            ProjectionFreshness.Rebuilding => ProjectionFreshnessMetadata.Rebuilding(),
            ProjectionFreshness.Stale => ProjectionFreshnessMetadata.Stale(
                checkpoint.SequenceNumber.ToString(CultureInfo.InvariantCulture)),
            ProjectionFreshness.Unavailable => ProjectionFreshnessMetadata.Unavailable(),
            ProjectionFreshness.Degraded => ProjectionFreshnessMetadata.Degraded(),
            _ => new(ProjectionFreshnessState.Unknown, null, null, "Projection freshness is unknown.")
        };
}
