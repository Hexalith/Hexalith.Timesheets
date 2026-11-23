using System.Globalization;
using System.Text;

using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections.TimeEntries;
using Hexalith.Timesheets.Server.Policies;

namespace Hexalith.Timesheets.Projections.ApprovedTimeLedger;

public sealed class ApprovedTimeLedgerProjection
{
    public const string ProjectionName = "approved-time-ledger";

    private const int DefaultPageSize = 50;

    private const int MaxPageSize = 500;

    private readonly TimeEntryEvidenceProjection _evidenceProjection = new();

    private readonly bool _trustedPublicationAllowed;

    public ApprovedTimeLedgerProjection()
    {
    }

    public ApprovedTimeLedgerProjection(TimesheetsEvidencePolicyOptions evidencePolicyOptions)
    {
        ArgumentNullException.ThrowIfNull(evidencePolicyOptions);
        _trustedPublicationAllowed = evidencePolicyOptions.AllowsProjectionPublication();
    }

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

        checkpoint.ValidateTenant(tenantId);

        TimeEntryProjectionEvent[] eventList = [.. TimeEntryStoredEventNormalizer.Normalize(events)];

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

            rows.Add(ApprovedTimeLedgerRowReadModel.CurrentFromEvidence(evidence, _trustedPublicationAllowed));

            ApprovedTimeLedgerRowReadModel? superseded = ApprovedTimeLedgerRowReadModel
                .SupersededFromApprovedCorrection(evidence, _trustedPublicationAllowed);
            if (superseded is not null)
            {
                rows.Add(superseded);
            }
        }

        IReadOnlyList<ApprovedTimeLedgerRowReadModel> filteredRows = rows
            .Where(query.Includes)
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
        HashSet<TimeEntryId> seenTimeEntryIds = [];

        foreach (TimeEntryProjectionEvent projectionEvent in events)
        {
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
