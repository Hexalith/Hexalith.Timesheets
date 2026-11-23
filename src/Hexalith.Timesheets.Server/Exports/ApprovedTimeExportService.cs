using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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

    private readonly TimeProvider? _clock;
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

    /// <summary>Captures generation time from the host clock after writing the final file.</summary>
    public ApprovedTimeExportService(
        ITimesheetsAccessGuard accessGuard,
        ApprovedTimeLedgerQueryService ledgerQueryService,
        IApprovedTimeExportAuditRecorder auditRecorder,
        TimesheetsEvidencePolicyOptions evidencePolicyOptions,
        TimeProvider clock)
        : this(accessGuard, ledgerQueryService, auditRecorder, evidencePolicyOptions)
    {
        _clock = clock;
    }

    public async ValueTask<ApprovedTimeExportResult> GenerateAsync(
        TimesheetsRequestContext context,
        GenerateApprovedTimeExport command,
        DateTimeOffset requestedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);
        requestedAtUtc = requestedAtUtc.ToUniversalTime();

        ExportEvaluation evaluation = await EvaluateAsync(
            context,
            command.LedgerQuery,
            command.Format,
            command.FormatVersion,
            command.RequestId,
            cancellationToken).ConfigureAwait(false);

        if (!evaluation.Authorization.IsAuthorized)
        {
            return ApprovedTimeExportResult.NotFoundOrDenied(evaluation.Authorization);
        }

        if (evaluation.Readiness == ApprovedTimeExportReadinessState.Blocked)
        {
            return Blocked(evaluation.ReadinessDetail);
        }

        // EvaluateAsync only returns Ready after a non-null tenant check; capture it so the audit event
        // (which requires a non-null tenant) keeps its nullable-flow guarantee after the extraction.
        TenantReference tenant = context.Tenant
            ?? throw new InvalidOperationException("A ready approved-time export requires a tenant context.");

        IReadOnlyList<ApprovedTimeExportRowReadModel> rows = ApplyExportOrdering(
            evaluation.Rows.Select(row => ToExportRow(row, evaluation.TenantCommentPolicy)),
            evaluation.Scope.Filters);

        string csv = ApprovedTimeExportCsvWriter.Write(rows);
        ApprovedTimeExportScope scope = evaluation.Scope;
        string outputContentHash = ComputeSha256(csv);
        DateTimeOffset generatedAtUtc = _clock?.GetUtcNow().ToUniversalTime() ?? requestedAtUtc;
        ApprovedTimeExported evidence = new(
            context.Actor,
            tenant,
            scope.Filters,
            requestedAtUtc,
            generatedAtUtc,
            context.CorrelationId,
            scope,
            command.Format,
            ResolveFormatVersion(command.FormatVersion),
            evaluation.Freshness.State,
            rows.Count,
            outputContentHash)
        {
            RequestId = command.RequestId,
            OutputRowsHashSha256 = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(rows, JsonSerializerOptions.Web)))
        };
        TimesheetsDomainResult? auditResult;
        try
        {
            auditResult = await _auditRecorder.RecordAcceptedExportAsync(evidence, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Blocked("Export audit completion could not be verified.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (auditResult?.Events is not [ApprovedTimeExported acknowledged]
            || acknowledged.RequestedAtUtc.Offset != TimeSpan.Zero || acknowledged.GeneratedAtUtc.Offset != TimeSpan.Zero)
        {
            return Blocked("Export audit completion could not be verified.");
        }

        // Only the owner-stream recorder can attest the original instants of a recovered request.
        if (_auditRecorder is EventStoreApprovedTimeExportAuditRecorder)
        {
            evidence = evidence with
            {
                RequestedAtUtc = acknowledged.RequestedAtUtc,
                GeneratedAtUtc = acknowledged.GeneratedAtUtc,
                CorrelationId = command.RequestId is null ? evidence.CorrelationId : acknowledged.CorrelationId
            };
        }

        if (acknowledged != evidence)
        {
            return Blocked("Export audit completion could not be verified.");
        }

        var finalDecision = await _accessGuard.AuthorizeAsync(
            ApprovedTimeExportReceipt.Authority(context, scope.Filters), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!finalDecision.IsAuthorized)
        {
            return ApprovedTimeExportResult.NotFoundOrDenied(finalDecision);
        }

        foreach (ApprovedTimeLedgerRowReadModel row in evaluation.Rows)
        {
            var rowDecision = await _ledgerQueryService.AuthorizeRowAsync(context, row, cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!rowDecision.IsAuthorized)
            {
                return ApprovedTimeExportResult.NotFoundOrDenied(rowDecision);
            }
        }

        ApprovedTimeExportAuditMetadata audit = Audit(
            context, acknowledged.Filters, acknowledged.Format, acknowledged.FormatVersion, acknowledged.RequestedAtUtc,
            acknowledged.GeneratedAtUtc, scope, acknowledged.FreshnessState, rows.Count, null, outputContentHash)
            with { RequestId = acknowledged.RequestId, CorrelationId = acknowledged.CorrelationId, OutputRowsHashSha256 = acknowledged.OutputRowsHashSha256 };
        ApprovedTimeExportReadModel export = new(
            ApprovedTimeExportReadinessState.Ready, "Approved ledger rows are fresh enough for export.",
            scope, evaluation.Freshness, audit, acknowledged.Format, acknowledged.FormatVersion, rows)
        {
            CsvContent = csv
        };
        return ApprovedTimeExportResult.Generated(export, auditResult);

        ApprovedTimeExportResult Blocked(string reason)
        {
            ApprovedTimeExportScope emptyScope = evaluation.Scope with { RowCount = 0 };
            return ApprovedTimeExportResult.Blocked(new(
                ApprovedTimeExportReadinessState.Blocked,
                reason,
                emptyScope,
                evaluation.Freshness,
                Audit(context, emptyScope.Filters, command.Format, command.FormatVersion, requestedAtUtc, null,
                    emptyScope, evaluation.Freshness.State, 0, reason, null) with
                {
                    RequestId = ApprovedTimeExportReceipt.ValidRequestId(command.RequestId) ? command.RequestId : null
                },
                command.Format,
                ResolveFormatVersion(command.FormatVersion),
                []));
        }
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
        requestedAtUtc = requestedAtUtc.ToUniversalTime();

        ExportEvaluation evaluation = await EvaluateAsync(
            context,
            query.LedgerQuery,
            query.Format,
            query.FormatVersion,
            null,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (evaluation.Authorization.IsBlockedByMissingEvidencePolicy)
        {
            evaluation = ExportEvaluation.MissingPolicy(
                Scope(ApprovedTimeExportReceipt.Normalize(query.LedgerQuery ?? new()), 0));
        }

        if (!evaluation.Authorization.IsAuthorized)
        {
            return ApprovedTimePreviewResult.NotFoundOrDenied(evaluation.Authorization);
        }

        bool ready = evaluation.Readiness == ApprovedTimeExportReadinessState.Ready;

        (TimesheetsCommentPolicyDecision commentPolicy, string detail) = SummarizePublishedComments(evaluation);
        ApprovedTimeExportPreviewReadModel preview = new(
            evaluation.Readiness,
            detail,
            evaluation.Scope,
            commentPolicy,
            evaluation.Freshness,
            Audit(
                context,
                evaluation.Scope.Filters,
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
        QueryApprovedTimeLedger? requestedFilters,
        ApprovedTimeExportFormat format,
        string formatVersion,
        string? requestId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TimesheetsAuthorizationDecision exportDecision = await _accessGuard.AuthorizeAsync(
            ApprovedTimeExportReceipt.Authority(context, requestedFilters), cancellationToken).ConfigureAwait(false);

        if (!exportDecision.IsAuthorized)
        {
            return ExportEvaluation.Denied(exportDecision, Scope(requestedFilters ?? new(), 0));
        }

        QueryApprovedTimeLedger ledgerQuery = ApprovedTimeExportReceipt.Normalize(requestedFilters ?? new());
        if (context.Tenant is null)
        {
            return ExportEvaluation.Denied(
                TimesheetsAuthorizationDecision.Denied(
                    TimesheetsDenialCategory.MissingTenant,
                    "Tenant context is required for approved-time export."),
                Scope(ledgerQuery, 0));
        }

        string? contractBlock = requestedFilters is null ? "Ledger filters are required for export."
            : !ApprovedTimeExportReceipt.ValidRequestId(requestId) ? "The export request identifier is invalid."
            : ValidateContract(format, formatVersion, ledgerQuery);
        if (contractBlock is not null)
        {
            return ExportEvaluation.Blocked(
                contractBlock,
                ProjectionFreshnessMetadata.Unavailable(contractBlock),
                Scope(ledgerQuery, 0));
        }

        if (!_evidencePolicyOptions.CommentSensitivityPolicyConfigured
            || !_evidencePolicyOptions.LegalHoldPolicyConfigured
            || !_evidencePolicyOptions.TenantRetentionOverridePolicyConfigured
            || !_evidencePolicyOptions.ExportCommentsAllowedByTenant.TryGetValue(context.Tenant.TenantId, out bool tenantAllows))
        {
            return ExportEvaluation.MissingPolicy(Scope(ledgerQuery, 0));
        }

        (ApprovedTimeLedgerReadModel? ledger, TimesheetsAuthorizationDecision? denial) =
            await LoadDisclosedLedgerAsync(context, ledgerQuery, cancellationToken).ConfigureAwait(false);

        if (ledger is null)
        {
            return ExportEvaluation.Denied(denial!, Scope(ledgerQuery, 0));
        }

        if (!ledger.CanUseForExport)
        {
            return ExportEvaluation.Blocked(
                ledger.ExportReadinessDetail,
                ledger.ProjectionFreshness,
                Scope(ledgerQuery, ledger.Items.Count));
        }

        return ExportEvaluation.Ready(
            ledger.ExportReadinessDetail,
            ledger.ProjectionFreshness,
            Scope(ledgerQuery, ledger.Items.Count) with
            {
                LedgerCheckpoint = ledger.ExportSnapshotId,
                LedgerCursor = ledger.ProjectionFreshness.Cursor,
                LedgerAsOfUtc = ledger.ProjectionFreshness.AsOfUtc?.ToUniversalTime()
            },
            ledger.Items,
            tenantAllows && _evidencePolicyOptions.ExportCommentsAllowed
                ? TimesheetsCommentPolicyDecision.Allowed : TimesheetsCommentPolicyDecision.Excluded);
    }

    private static (TimesheetsCommentPolicyDecision Policy, string Detail) SummarizePublishedComments(ExportEvaluation evaluation)
    {
        if (evaluation.TenantCommentPolicy != TimesheetsCommentPolicyDecision.Allowed)
        {
            return (evaluation.TenantCommentPolicy, evaluation.ReadinessDetail);
        }

        bool anyAllowed = false;
        bool anyExcluded = false;
        foreach (ApprovedTimeLedgerRowReadModel row in evaluation.Rows)
        {
            Observe(row.Comment);
            Observe(row.ApprovedCorrection?.PreviousValues.Comment);
            Observe(row.ApprovedCorrection?.CorrectedValues.Comment);
            Observe(row.Correction?.PreviousValues.Comment);
            Observe(row.Correction?.CorrectedValues.Comment);
            if (anyAllowed && anyExcluded)
            {
                return (TimesheetsCommentPolicyDecision.Excluded, "Comments are permitted for some values only.");
            }
        }

        return (anyAllowed ? TimesheetsCommentPolicyDecision.Allowed : TimesheetsCommentPolicyDecision.Excluded,
            evaluation.ReadinessDetail);

        void Observe(TimeEntryComment? comment)
        {
            if (comment is null)
            {
                return;
            }

            bool allowed = ExportDecision(comment, evaluation.TenantCommentPolicy) == TimesheetsCommentPolicyDecision.Allowed;
            anyAllowed |= allowed;
            anyExcluded |= !allowed;
        }
    }

    private static TimesheetsCommentPolicyDecision ExportDecision(TimeEntryComment? comment, TimesheetsCommentPolicyDecision tenantPolicy)
        => tenantPolicy == TimesheetsCommentPolicyDecision.Allowed
            && comment?.Policy.RedactionRequirement is TimesheetsCommentRedactionRequirement.NotRequired
                or TimesheetsCommentRedactionRequirement.RequiredBeforeExternalDisclosure
            ? comment.Policy.ExportInclusion : TimesheetsCommentPolicyDecision.Excluded;

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

        if (ledgerQuery.Cursor is not null)
        {
            return "A full approved-time export must start without a browsing cursor.";
        }

        return ledgerQuery.BillableState == BillableState.Billable
            ? null
            : "Approved billable ledger evidence is required for export.";
    }

    private static ApprovedTimeExportRowReadModel ToExportRow(
        ApprovedTimeLedgerRowReadModel row, TimesheetsCommentPolicyDecision tenantPolicy)
    {
        TimesheetsCommentPolicyDecision currentDecision = ExportDecision(row.Comment, tenantPolicy);
        row = row.SelectComments(comment => ExportDecision(comment, tenantPolicy) == TimesheetsCommentPolicyDecision.Allowed
            ? comment : null);
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
            CommentExportState = currentDecision,
            Comment = row.Comment
        };
    }

    private async ValueTask<(ApprovedTimeLedgerReadModel? Ledger, TimesheetsAuthorizationDecision? Denial)> LoadDisclosedLedgerAsync(
        TimesheetsRequestContext context,
        QueryApprovedTimeLedger baseQuery,
        CancellationToken cancellationToken)
    {
        List<ApprovedTimeLedgerRowReadModel> items = [];
        HashSet<(TimeEntryId Id, ApprovedTimeLedgerRowState State)> identities = [];
        HashSet<string> cursors = new(StringComparer.Ordinal);
        ProjectionFreshnessMetadata? first = null;
        string? snapshotId = null;
        string? cursor = null;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            ApprovedTimeLedgerQueryResult result = await _ledgerQueryService
                .QueryAsync(context, baseQuery with { Cursor = cursor }, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Authorization.IsAuthorized)
            {
                return (null, result.Authorization);
            }

            if (result.Page is not { } page)
            {
                return (null, TimesheetsAuthorizationDecision.Denied(
                    TimesheetsDenialCategory.UnavailableSiblingAuthority, "The authorized ledger is unavailable."));
            }

            ProjectionFreshnessMetadata freshness = page.ProjectionFreshness;
            if (freshness.State != ProjectionFreshnessState.Fresh)
            {
                return (new([], null, freshness, false, "Projection freshness does not allow export preview."), null);
            }

            if (first is null)
            {
                first = freshness;
                snapshotId = page.ExportSnapshotId;
            }

            if ((page.NextCursor is not null && string.IsNullOrWhiteSpace(snapshotId))
                || !string.Equals(snapshotId, page.ExportSnapshotId, StringComparison.Ordinal)
                || !string.Equals(first.Cursor, freshness.Cursor, StringComparison.Ordinal)
                || first.AsOfUtc != freshness.AsOfUtc)
            {
                return Unverified("The ledger snapshot could not be verified. Retry the export.");
            }

            if (page.Items.Count > 0 && !page.CanUseForExport)
            {
                return (new([], null, freshness, false, page.ExportReadinessDetail), null);
            }

            foreach (ApprovedTimeLedgerRowReadModel row in page.Items)
            {
                if (row.BillableState != BillableState.Billable)
                {
                    return Unverified("Approved billable ledger evidence is required for export.");
                }

                if (row.RowState is not (ApprovedTimeLedgerRowState.Current or ApprovedTimeLedgerRowState.Superseded)
                    || !baseQuery.Includes(row) || !identities.Add((row.TimeEntryId, row.RowState))
                    || row.ProjectionFreshness.State != ProjectionFreshnessState.Fresh
                    || (row.ProjectionFreshness.AsOfUtc is { } rowAsOf && rowAsOf != freshness.AsOfUtc)
                    || (row.ProjectionFreshness.Cursor is { } rowVersion
                        && !string.Equals(freshness.Cursor, rowVersion, StringComparison.Ordinal)))
                {
                    return Unverified("The approved ledger scope could not be verified. Retry the export.");
                }
                items.Add(row);
            }

            cursor = page.NextCursor;
            if (cursor is not null && (string.IsNullOrWhiteSpace(cursor) || !cursors.Add(cursor)))
            {
                return Unverified("The ledger continuation did not advance. Retry the export.");
            }
        }
        while (cursor is not null);

        if (cursors.Count > 0)
        {
            TimesheetsAuthorizationDecision finalDecision = await _accessGuard.AuthorizeAsync(
                ApprovedTimeExportReceipt.Authority(context, baseQuery), cancellationToken).ConfigureAwait(false);
            if (!finalDecision.IsAuthorized)
            {
                return (null, finalDecision);
            }
        }

        return (new(items, null, first, items.Count > 0, items.Count > 0
            ? "Approved ledger rows are fresh enough for export preview."
            : "No approved ledger rows are available for export preview.") { ExportSnapshotId = snapshotId }, null);

        static (ApprovedTimeLedgerReadModel, TimesheetsAuthorizationDecision?) Unverified(string detail)
            => (new([], null, ProjectionFreshnessMetadata.Unavailable(detail), false, detail), null);
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
        IReadOnlyList<ApprovedTimeLedgerRowReadModel> Rows,
        TimesheetsCommentPolicyDecision TenantCommentPolicy)
    {
        public static ExportEvaluation MissingPolicy(ApprovedTimeExportScope scope)
        {
            const string Detail = "The tenant's approved-time export policy is unavailable.";
            return Blocked(Detail, ProjectionFreshnessMetadata.Unavailable(Detail), scope,
                TimesheetsCommentPolicyDecision.PolicyRequired);
        }

        public static ExportEvaluation Denied(
            TimesheetsAuthorizationDecision authorization,
            ApprovedTimeExportScope scope)
            => new(
                authorization,
                ApprovedTimeExportReadinessState.Unknown,
                string.Empty,
                ProjectionFreshnessMetadata.Unavailable("Approved-time export was not authorized."),
                scope,
                [], TimesheetsCommentPolicyDecision.Excluded);

        public static ExportEvaluation Blocked(
            string reason,
            ProjectionFreshnessMetadata freshness,
            ApprovedTimeExportScope scope,
            TimesheetsCommentPolicyDecision policy = TimesheetsCommentPolicyDecision.Excluded)
            => new(
                TimesheetsAuthorizationDecision.Allowed(),
                ApprovedTimeExportReadinessState.Blocked,
                reason,
                freshness,
                scope,
                [], policy);

        public static ExportEvaluation Ready(
            string readinessDetail,
            ProjectionFreshnessMetadata freshness,
            ApprovedTimeExportScope scope,
            IReadOnlyList<ApprovedTimeLedgerRowReadModel> rows,
            TimesheetsCommentPolicyDecision policy)
            => new(
                TimesheetsAuthorizationDecision.Allowed(),
                ApprovedTimeExportReadinessState.Ready,
                readinessDetail,
                freshness,
                scope,
                rows, policy);
    }
}
