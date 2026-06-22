using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Hexalith.Timesheets.Contracts.Commands.Exports;
using Hexalith.Timesheets.Contracts.Events.Exports;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.Queries.Exports;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.Policies;

namespace Hexalith.Timesheets.Server.Exports;

public sealed class ApprovedTimeExportService
{
    private const string DefaultFormatVersion = "approved-time-export.csv.v1";

    private readonly ITimesheetsAccessGuard _accessGuard;
    private readonly IApprovedTimeExportAuditRecorder _auditRecorder;
    private readonly ApprovedTimeLedgerQueryService _ledgerQueryService;
    private readonly TimesheetsEvidencePolicyOptions _evidencePolicyOptions;

    public ApprovedTimeExportService(
        ITimesheetsAccessGuard accessGuard,
        ApprovedTimeLedgerQueryService ledgerQueryService,
        IApprovedTimeExportAuditRecorder? auditRecorder = null,
        TimesheetsEvidencePolicyOptions? evidencePolicyOptions = null)
    {
        ArgumentNullException.ThrowIfNull(accessGuard);
        ArgumentNullException.ThrowIfNull(ledgerQueryService);

        _accessGuard = accessGuard;
        _auditRecorder = auditRecorder ?? new DomainEventApprovedTimeExportAuditRecorder();
        _ledgerQueryService = ledgerQueryService;
        _evidencePolicyOptions = evidencePolicyOptions ?? TimesheetsEvidencePolicyOptions.FailClosedDefault;
    }

    public async ValueTask<ApprovedTimeExportResult> GenerateAsync(
        TimesheetsRequestContext context,
        GenerateApprovedTimeExport command,
        DateTimeOffset requestedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);

        ExportEvaluation evaluation = await EvaluateAsync(
            context,
            command.LedgerQuery,
            command.Format,
            command.FormatVersion,
            cancellationToken).ConfigureAwait(false);

        if (!evaluation.Authorization.IsAuthorized)
        {
            return ApprovedTimeExportResult.NotFoundOrDenied(evaluation.Authorization);
        }

        if (evaluation.Readiness == ApprovedTimeExportReadinessState.Blocked)
        {
            ApprovedTimeExportReadModel blocked = new(
                ApprovedTimeExportReadinessState.Blocked,
                evaluation.ReadinessDetail,
                evaluation.Scope,
                evaluation.Freshness,
                Audit(
                    context,
                    command.LedgerQuery,
                    command.Format,
                    command.FormatVersion,
                    requestedAtUtc,
                    null,
                    evaluation.Scope,
                    evaluation.Freshness.State,
                    0,
                    evaluation.ReadinessDetail,
                    null),
                command.Format,
                ResolveFormatVersion(command.FormatVersion),
                []);

            return ApprovedTimeExportResult.Blocked(blocked);
        }

        // EvaluateAsync only returns Ready after a non-null tenant check; capture it so the audit event
        // (which requires a non-null tenant) keeps its nullable-flow guarantee after the extraction.
        TenantReference tenant = context.Tenant
            ?? throw new InvalidOperationException("A ready approved-time export requires a tenant context.");

        IReadOnlyList<ApprovedTimeExportRowReadModel> rows = ApplyExportOrdering(
            evaluation.DisclosedItems.Select(ToExportRow),
            command.LedgerQuery);

        string csv = ApprovedTimeExportCsvWriter.Write(rows);
        ApprovedTimeExportScope scope = Scope(command.LedgerQuery, rows.Count);
        string outputContentHash = ComputeSha256(csv);
        ApprovedTimeExportAuditMetadata audit = Audit(
            context,
            command.LedgerQuery,
            command.Format,
            command.FormatVersion,
            requestedAtUtc,
            requestedAtUtc,
            scope,
            evaluation.Freshness.State,
            rows.Count,
            null,
            outputContentHash);
        ApprovedTimeExportReadModel export = new(
            ApprovedTimeExportReadinessState.Ready,
            "Approved ledger rows are fresh enough for export.",
            scope,
            evaluation.Freshness,
            audit,
            command.Format,
            ResolveFormatVersion(command.FormatVersion),
            rows)
        {
            CsvContent = csv
        };
        ApprovedTimeExported evidence = new(
            context.Actor,
            tenant,
            command.LedgerQuery,
            requestedAtUtc,
            requestedAtUtc,
            context.CorrelationId,
            scope,
            command.Format,
            ResolveFormatVersion(command.FormatVersion),
            evaluation.Freshness.State,
            rows.Count,
            outputContentHash);
        TimesheetsDomainResult auditResult = await _auditRecorder
            .RecordAcceptedExportAsync(evidence, cancellationToken)
            .ConfigureAwait(false);

        return ApprovedTimeExportResult.Generated(export, auditResult);
    }

    /// <summary>
    /// Evaluates approved-time export readiness over the requested ledger scope without producing any output or
    /// emitting audit evidence. Mirrors generation gating exactly so preview and generation cannot drift.
    /// </summary>
    /// <param name="context">The authorization context for the preview request.</param>
    /// <param name="query">The dedicated, side-effect-free export preview query.</param>
    /// <param name="requestedAtUtc">The UTC instant the preview was requested.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The disclosed readiness, or a fail-closed not-found-or-denied result.</returns>
    public async ValueTask<ApprovedTimePreviewResult> PreviewAsync(
        TimesheetsRequestContext context,
        PreviewApprovedTimeExport query,
        DateTimeOffset requestedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);

        ExportEvaluation evaluation = await EvaluateAsync(
            context,
            query.LedgerQuery,
            query.Format,
            query.FormatVersion,
            cancellationToken).ConfigureAwait(false);

        if (!evaluation.Authorization.IsAuthorized)
        {
            return ApprovedTimePreviewResult.NotFoundOrDenied(evaluation.Authorization);
        }

        bool ready = evaluation.Readiness == ApprovedTimeExportReadinessState.Ready;

        // Preview never produces a file and never records audit evidence: GeneratedAtUtc and the output content
        // hash stay null, and _auditRecorder is never called. The audit row count mirrors generation's audit
        // semantics — the count of rows in the (would-be) output, which is zero for a blocked result — so the
        // shared readiness core cannot drift between preview and generation (AC3).
        ApprovedTimeExportPreviewReadModel preview = new(
            evaluation.Readiness,
            evaluation.ReadinessDetail,
            evaluation.Scope,
            _evidencePolicyOptions.ExportCommentsAllowed
                ? TimesheetsCommentPolicyDecision.Allowed
                : TimesheetsCommentPolicyDecision.Excluded,
            evaluation.Freshness,
            Audit(
                context,
                query.LedgerQuery,
                query.Format,
                query.FormatVersion,
                requestedAtUtc,
                null,
                evaluation.Scope,
                evaluation.Freshness.State,
                ready ? evaluation.Scope.RowCount : 0,
                ready ? null : evaluation.ReadinessDetail,
                null),
            query.Format,
            ResolveFormatVersion(query.FormatVersion));

        return ApprovedTimePreviewResult.Evaluated(preview);
    }

    private async ValueTask<ExportEvaluation> EvaluateAsync(
        TimesheetsRequestContext context,
        QueryApprovedTimeLedger ledgerQuery,
        ApprovedTimeExportFormat format,
        string formatVersion,
        CancellationToken cancellationToken)
    {
        TimesheetsAuthorizationDecision exportDecision = await _accessGuard.AuthorizeAsync(
            new(context, TimesheetsOperation.Export),
            cancellationToken).ConfigureAwait(false);

        if (!exportDecision.IsAuthorized)
        {
            return ExportEvaluation.Denied(exportDecision, Scope(ledgerQuery, 0));
        }

        if (context.Tenant is null)
        {
            return ExportEvaluation.Denied(
                TimesheetsAuthorizationDecision.Denied(
                    TimesheetsDenialCategory.MissingTenant,
                    "Tenant context is required for approved-time export."),
                Scope(ledgerQuery, 0));
        }

        string? contractBlock = ValidateContract(format, formatVersion, ledgerQuery);
        if (contractBlock is not null)
        {
            return ExportEvaluation.Blocked(
                contractBlock,
                ProjectionFreshnessMetadata.Unavailable(contractBlock),
                Scope(ledgerQuery, 0));
        }

        (ApprovedTimeLedgerReadModel? ledger, TimesheetsAuthorizationDecision? denial) =
            await LoadDisclosedLedgerAsync(context, ledgerQuery, cancellationToken).ConfigureAwait(false);

        if (ledger is null)
        {
            return ExportEvaluation.Denied(denial!, Scope(ledgerQuery, 0));
        }

        string? readinessBlock = ValidateReadiness(ledger);
        if (readinessBlock is not null)
        {
            return ExportEvaluation.Blocked(
                readinessBlock,
                ledger.ProjectionFreshness,
                Scope(ledgerQuery, ledger.Items.Count));
        }

        return ExportEvaluation.Ready(
            ledger.ExportReadinessDetail,
            ledger.ProjectionFreshness,
            Scope(ledgerQuery, ledger.Items.Count),
            ledger.Items);
    }

    private static string? ValidateContract(
        ApprovedTimeExportFormat format,
        string formatVersion,
        QueryApprovedTimeLedger ledgerQuery)
    {
        if (format != ApprovedTimeExportFormat.Csv)
        {
            return "Only CSV approved-ledger export format is supported.";
        }

        if (!string.Equals(ResolveFormatVersion(formatVersion), DefaultFormatVersion, StringComparison.Ordinal))
        {
            return "Unsupported approved-ledger export format version.";
        }

        return ledgerQuery.BillableState == BillableState.Billable
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
        QueryApprovedTimeLedger ledgerQuery,
        ApprovedTimeExportFormat format,
        string formatVersion,
        DateTimeOffset requestedAtUtc,
        DateTimeOffset? generatedAtUtc,
        ApprovedTimeExportScope scope,
        ProjectionFreshnessState freshnessState,
        int rowCount,
        string? blockedReason,
        string? outputContentHashSha256)
        => new(
            context.Actor,
            ledgerQuery,
            requestedAtUtc,
            generatedAtUtc,
            context.CorrelationId,
            scope,
            format,
            ResolveFormatVersion(formatVersion),
            freshnessState,
            rowCount,
            blockedReason)
        {
            Tenant = context.Tenant,
            OutputContentHashSha256 = outputContentHashSha256
        };

    private static string ComputeSha256(string content)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ResolveFormatVersion(string? formatVersion)
        => string.IsNullOrWhiteSpace(formatVersion)
            ? DefaultFormatVersion
            : formatVersion;

    private sealed record ExportEvaluation(
        TimesheetsAuthorizationDecision Authorization,
        ApprovedTimeExportReadinessState Readiness,
        string ReadinessDetail,
        ProjectionFreshnessMetadata Freshness,
        ApprovedTimeExportScope Scope,
        IReadOnlyList<ApprovedTimeLedgerRowReadModel> DisclosedItems)
    {
        public static ExportEvaluation Denied(
            TimesheetsAuthorizationDecision authorization,
            ApprovedTimeExportScope scope)
            => new(
                authorization,
                ApprovedTimeExportReadinessState.Unknown,
                string.Empty,
                ProjectionFreshnessMetadata.Unavailable("Approved-time export was not authorized."),
                scope,
                []);

        public static ExportEvaluation Blocked(
            string reason,
            ProjectionFreshnessMetadata freshness,
            ApprovedTimeExportScope scope)
            => new(
                TimesheetsAuthorizationDecision.Allowed(),
                ApprovedTimeExportReadinessState.Blocked,
                reason,
                freshness,
                scope,
                []);

        public static ExportEvaluation Ready(
            string readinessDetail,
            ProjectionFreshnessMetadata freshness,
            ApprovedTimeExportScope scope,
            IReadOnlyList<ApprovedTimeLedgerRowReadModel> disclosedItems)
            => new(
                TimesheetsAuthorizationDecision.Allowed(),
                ApprovedTimeExportReadinessState.Ready,
                readinessDetail,
                freshness,
                scope,
                disclosedItems);
    }
}
