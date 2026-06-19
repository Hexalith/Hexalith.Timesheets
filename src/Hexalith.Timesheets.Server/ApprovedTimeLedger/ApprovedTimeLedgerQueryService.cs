using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.TimeEntries;

namespace Hexalith.Timesheets.Server.ApprovedTimeLedger;

public sealed class ApprovedTimeLedgerQueryService
{
    private readonly ITimesheetsAccessGuard _accessGuard;
    private readonly ITimeEntryDisplayHydrator _displayHydrator;
    private readonly IApprovedTimeLedgerProjectionReader _projectionReader;

    public ApprovedTimeLedgerQueryService(
        ITimesheetsAccessGuard accessGuard,
        IApprovedTimeLedgerProjectionReader projectionReader,
        ITimeEntryDisplayHydrator displayHydrator)
    {
        ArgumentNullException.ThrowIfNull(accessGuard);
        ArgumentNullException.ThrowIfNull(projectionReader);
        ArgumentNullException.ThrowIfNull(displayHydrator);

        _accessGuard = accessGuard;
        _projectionReader = projectionReader;
        _displayHydrator = displayHydrator;
    }

    public async ValueTask<ApprovedTimeLedgerQueryResult> QueryAsync(
        TimesheetsRequestContext context,
        QueryApprovedTimeLedger query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);

        TimesheetsAuthorizationDecision tenantDecision = await _accessGuard.AuthorizeAsync(
            new(context, TimesheetsOperation.ProjectionRead),
            cancellationToken).ConfigureAwait(false);

        if (!tenantDecision.IsAuthorized)
        {
            return ApprovedTimeLedgerQueryResult.NotFoundOrDenied(tenantDecision);
        }

        ApprovedTimeLedgerReadModel? page = await _projectionReader
            .QueryAsync(context, query, cancellationToken)
            .ConfigureAwait(false);

        if (page is null)
        {
            return ApprovedTimeLedgerQueryResult.NotFoundOrDenied(tenantDecision);
        }

        List<ApprovedTimeLedgerRowReadModel> disclosedRows = [];
        foreach (ApprovedTimeLedgerRowReadModel row in page.Items)
        {
            TimesheetsAuthorizationDecision rowDecision = await _accessGuard.AuthorizeAsync(
                CreateRowAuthorizationRequest(context, row),
                cancellationToken).ConfigureAwait(false);

            if (!rowDecision.IsAuthorized)
            {
                if (CanFilterRow(rowDecision))
                {
                    continue;
                }

                return ApprovedTimeLedgerQueryResult.NotFoundOrDenied(rowDecision);
            }

            TimeEntryDisplayHydration hydration = await _displayHydrator
                .HydrateAsync(context, ToEvidence(row), cancellationToken)
                .ConfigureAwait(false);

            disclosedRows.Add(row with
            {
                DisplayHydration = hydration
            });
        }

        bool freshEnoughForExport = page.ProjectionFreshness.State == ProjectionFreshnessState.Fresh;
        bool canUseForExport = disclosedRows.Count > 0 && freshEnoughForExport;

        return ApprovedTimeLedgerQueryResult.Disclosed(page with
        {
            Items = disclosedRows,
            CanUseForExport = canUseForExport,
            ExportReadinessDetail = ResolveExportReadinessDetail(canUseForExport, freshEnoughForExport)
        });
    }

    private static string ResolveExportReadinessDetail(bool canUseForExport, bool freshEnoughForExport)
    {
        if (canUseForExport)
        {
            return "Approved ledger rows are fresh enough for export preview.";
        }

        return freshEnoughForExport
            ? "No approved ledger rows are available for export preview."
            : "Projection freshness does not allow export preview.";
    }

    private static bool CanFilterRow(TimesheetsAuthorizationDecision decision)
        => decision.DenialCategory == TimesheetsDenialCategory.InsufficientRole;

    private static TimesheetsAuthorizationRequest CreateRowAuthorizationRequest(
        TimesheetsRequestContext context,
        ApprovedTimeLedgerRowReadModel row)
    {
        TimesheetsAuthorizationRequest request = new(context, TimesheetsOperation.ProjectionRead)
        {
            Contributor = row.Contributor
        };

        if (row.Target.TargetKind == TimeEntryTargetKind.Project)
        {
            return request with { Project = new ProjectReference(row.Target.TargetId) };
        }

        if (row.Target.TargetKind == TimeEntryTargetKind.Work)
        {
            return request with { Work = new WorkReference(row.Target.TargetId) };
        }

        return request;
    }

    private static TimeEntryEvidenceReadModel ToEvidence(ApprovedTimeLedgerRowReadModel row)
        => new(
            row.TimeEntryId,
            row.Target,
            row.Contributor,
            row.ActivityTypeId,
            row.ActivityTypeScope,
            row.ServiceDate,
            row.DurationMinutes,
            row.BillableState,
            TimeEntryApprovalState.Approved,
            row.ContributorCategory,
            null,
            ResolveCorrectionState(row),
            row.ProjectionFreshness)
        {
            ApprovalDecision = row.ApprovalDecision,
            ApprovedCorrection = row.ApprovedCorrection,
            Correction = row.Correction,
            Comment = row.Comment,
            EventLineage = row.EventLineage,
            LockEvidence = row.LockEvidence,
            DisplayHydration = row.DisplayHydration
        };

    private static TimeEntryCorrectionState ResolveCorrectionState(ApprovedTimeLedgerRowReadModel row)
        => row.RowState == ApprovedTimeLedgerRowState.Superseded
            ? TimeEntryCorrectionState.Superseded
            : row.ApprovedCorrection is null && row.Correction is null
                ? TimeEntryCorrectionState.None
                : TimeEntryCorrectionState.Corrected;
}
