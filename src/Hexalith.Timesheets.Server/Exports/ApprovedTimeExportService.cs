using System.Globalization;

using Hexalith.Timesheets.Contracts.Commands.Exports;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.Exports;

public sealed class ApprovedTimeExportService
{
    private const string DefaultFormatVersion = "approved-time-export.csv.v1";

    private readonly ITimesheetsAccessGuard _accessGuard;
    private readonly ApprovedTimeLedgerQueryService _ledgerQueryService;

    public ApprovedTimeExportService(
        ITimesheetsAccessGuard accessGuard,
        ApprovedTimeLedgerQueryService ledgerQueryService)
    {
        ArgumentNullException.ThrowIfNull(accessGuard);
        ArgumentNullException.ThrowIfNull(ledgerQueryService);

        _accessGuard = accessGuard;
        _ledgerQueryService = ledgerQueryService;
    }

    public async ValueTask<ApprovedTimeExportResult> GenerateAsync(
        TimesheetsRequestContext context,
        GenerateApprovedTimeExport command,
        DateTimeOffset requestedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);

        TimesheetsAuthorizationDecision exportDecision = await _accessGuard.AuthorizeAsync(
            new(context, TimesheetsOperation.Export),
            cancellationToken).ConfigureAwait(false);

        if (!exportDecision.IsAuthorized)
        {
            return ApprovedTimeExportResult.NotFoundOrDenied(exportDecision);
        }

        string? contractBlock = ValidateContract(command);
        if (contractBlock is not null)
        {
            ApprovedTimeExportReadModel blocked = CreateBlockedExport(
                context,
                command,
                requestedAtUtc,
                ProjectionFreshnessMetadata.Unavailable(contractBlock),
                [],
                contractBlock);

            return ApprovedTimeExportResult.Blocked(blocked);
        }

        (ApprovedTimeLedgerReadModel? ledger, TimesheetsAuthorizationDecision? denial) =
            await LoadDisclosedLedgerAsync(context, command.LedgerQuery, cancellationToken).ConfigureAwait(false);

        if (ledger is null)
        {
            return ApprovedTimeExportResult.NotFoundOrDenied(denial!);
        }

        string? readinessBlock = ValidateReadiness(ledger);
        if (readinessBlock is not null)
        {
            ApprovedTimeExportReadModel blocked = CreateBlockedExport(
                context,
                command,
                requestedAtUtc,
                ledger.ProjectionFreshness,
                ledger.Items,
                readinessBlock);

            return ApprovedTimeExportResult.Blocked(blocked);
        }

        IReadOnlyList<ApprovedTimeExportRowReadModel> rows = ApplyExportOrdering(
            ledger.Items.Select(ToExportRow),
            command.LedgerQuery);

        string csv = ApprovedTimeExportCsvWriter.Write(rows);
        ApprovedTimeExportScope scope = Scope(command.LedgerQuery, rows.Count);
        ApprovedTimeExportReadModel export = new(
            ApprovedTimeExportReadinessState.Ready,
            "Approved ledger rows are fresh enough for export.",
            scope,
            ledger.ProjectionFreshness,
            Audit(
                context,
                command,
                requestedAtUtc,
                requestedAtUtc,
                scope,
                ledger.ProjectionFreshness.State,
                rows.Count,
                null),
            command.Format,
            ResolveFormatVersion(command),
            rows)
        {
            CsvContent = csv
        };

        return ApprovedTimeExportResult.Generated(export);
    }

    private static string? ValidateContract(GenerateApprovedTimeExport command)
    {
        if (command.Format != ApprovedTimeExportFormat.Csv)
        {
            return "Only CSV approved-ledger export format is supported.";
        }

        if (!string.Equals(ResolveFormatVersion(command), DefaultFormatVersion, StringComparison.Ordinal))
        {
            return "Unsupported approved-ledger export format version.";
        }

        return command.LedgerQuery.BillableState == BillableState.Billable
            ? null
            : "Approved billable ledger evidence is required for export.";
    }

    private static string? ValidateReadiness(ApprovedTimeLedgerReadModel ledger)
    {
        if (ledger.ProjectionFreshness.State != ProjectionFreshnessState.Fresh)
        {
            return "Projection freshness does not allow export preview.";
        }

        if (!ledger.CanUseForExport)
        {
            return ledger.ExportReadinessDetail;
        }

        if (ledger.Items.Count == 0)
        {
            return "No approved ledger rows are available for export preview.";
        }

        return ledger.Items.Any(static row => row.BillableState != BillableState.Billable)
            ? "Approved billable ledger evidence is required for export."
            : null;
    }

    private static ApprovedTimeExportReadModel CreateBlockedExport(
        TimesheetsRequestContext context,
        GenerateApprovedTimeExport command,
        DateTimeOffset requestedAtUtc,
        ProjectionFreshnessMetadata freshness,
        IReadOnlyList<ApprovedTimeLedgerRowReadModel> sourceRows,
        string reason)
    {
        ApprovedTimeExportScope scope = Scope(command.LedgerQuery, sourceRows.Count);

        return new(
            ApprovedTimeExportReadinessState.Blocked,
            reason,
            scope,
            freshness,
            Audit(
                context,
                command,
                requestedAtUtc,
                null,
                scope,
                freshness.State,
                0,
                reason),
            command.Format,
            ResolveFormatVersion(command),
            []);
    }

    private static ApprovedTimeExportRowReadModel ToExportRow(ApprovedTimeLedgerRowReadModel row)
    {
        TimesheetsCommentPolicyDecision exportDecision = row.Comment?.Policy.ExportInclusion
            ?? TimesheetsCommentPolicyDecision.Excluded;

        return new(
            row.TimeEntryId,
            row.Contributor,
            row.Target,
            row.ServiceDate,
            row.DurationMinutes,
            row.ActivityTypeId,
            row.ActivityTypeScope,
            row.BillableState,
            row.ApprovalDecision,
            row.RowState)
        {
            ApprovedCorrection = row.ApprovedCorrection,
            Correction = row.Correction,
            EventLineage = [.. row.EventLineage],
            AiMetrics = row.AiMetrics,
            CommentExportState = exportDecision,
            Comment = exportDecision == TimesheetsCommentPolicyDecision.Allowed
                ? row.Comment
                : null
        };
    }

    private async ValueTask<(ApprovedTimeLedgerReadModel? Ledger, TimesheetsAuthorizationDecision? Denial)> LoadDisclosedLedgerAsync(
        TimesheetsRequestContext context,
        QueryApprovedTimeLedger baseQuery,
        CancellationToken cancellationToken)
    {
        // Export must cover the full filtered scope, not a single page, otherwise finance
        // evidence is silently truncated when more approved rows exist than the page size.
        List<ApprovedTimeLedgerRowReadModel> items = [];
        ProjectionFreshnessMetadata freshness = ProjectionFreshnessMetadata.Fresh;
        bool freshnessCaptured = false;
        bool allFresh = true;
        string? cursor = baseQuery.Cursor;

        do
        {
            QueryApprovedTimeLedger pageQuery = baseQuery with { Cursor = cursor };
            ApprovedTimeLedgerQueryResult ledgerResult = await _ledgerQueryService
                .QueryAsync(context, pageQuery, cancellationToken)
                .ConfigureAwait(false);

            if (!ledgerResult.WasDisclosed)
            {
                return (null, ledgerResult.Authorization);
            }

            ApprovedTimeLedgerReadModel page = ledgerResult.Page
                ?? throw new InvalidOperationException("A disclosed approved-time ledger result must include a page.");

            items.AddRange(page.Items);

            // Once any page is not fresh, keep that non-fresh state so readiness fails closed.
            if (!freshnessCaptured || page.ProjectionFreshness.State != ProjectionFreshnessState.Fresh)
            {
                freshness = page.ProjectionFreshness;
                freshnessCaptured = true;
            }

            allFresh &= page.ProjectionFreshness.State == ProjectionFreshnessState.Fresh;
            cursor = page.NextCursor;
        }
        while (!string.IsNullOrEmpty(cursor));

        bool canUseForExport = allFresh && items.Count > 0;
        ApprovedTimeLedgerReadModel aggregate = new(
            items,
            null,
            freshness,
            canUseForExport,
            ResolveAggregateReadinessDetail(canUseForExport, allFresh));

        return (aggregate, null);
    }

    private static string ResolveAggregateReadinessDetail(bool canUseForExport, bool allFresh)
    {
        if (canUseForExport)
        {
            return "Approved ledger rows are fresh enough for export preview.";
        }

        return allFresh
            ? "No approved ledger rows are available for export preview."
            : "Projection freshness does not allow export preview.";
    }

    private static IReadOnlyList<ApprovedTimeExportRowReadModel> ApplyExportOrdering(
        IEnumerable<ApprovedTimeExportRowReadModel> rows,
        QueryApprovedTimeLedger query)
    {
        IOrderedEnumerable<ApprovedTimeExportRowReadModel> ordered =
            query.SortDirection == TimeEntryQuerySortDirection.Descending
                ? rows.OrderByDescending(row => PrimarySortKey(row, query), StringComparer.Ordinal)
                : rows.OrderBy(row => PrimarySortKey(row, query), StringComparer.Ordinal);

        return ordered
            .ThenBy(static row => row.TimeEntryId.Value, StringComparer.Ordinal)
            .ThenBy(static row => row.RowState)
            .ToArray();
    }

    private static string PrimarySortKey(
        ApprovedTimeExportRowReadModel row,
        QueryApprovedTimeLedger query)
        => query.SortBy switch
        {
            TimeEntryQuerySortBy.TimeEntryId => row.TimeEntryId.Value,
            TimeEntryQuerySortBy.DurationMinutes => row.DurationMinutes.ToString("D10", CultureInfo.InvariantCulture),
            _ => row.ServiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };

    private static ApprovedTimeExportScope Scope(QueryApprovedTimeLedger query, int rowCount)
        => new(query, rowCount, query.IncludeSupersededRows, query.CurrentRowsOnly);

    private static ApprovedTimeExportAuditMetadata Audit(
        TimesheetsRequestContext context,
        GenerateApprovedTimeExport command,
        DateTimeOffset requestedAtUtc,
        DateTimeOffset? generatedAtUtc,
        ApprovedTimeExportScope scope,
        ProjectionFreshnessState freshnessState,
        int rowCount,
        string? blockedReason)
        => new(
            context.Actor,
            command.LedgerQuery,
            requestedAtUtc,
            generatedAtUtc,
            context.CorrelationId,
            scope,
            command.Format,
            ResolveFormatVersion(command),
            freshnessState,
            rowCount,
            blockedReason);

    private static string ResolveFormatVersion(GenerateApprovedTimeExport command)
        => string.IsNullOrWhiteSpace(command.FormatVersion)
            ? DefaultFormatVersion
            : command.FormatVersion;
}
