using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.Policies;
using Hexalith.Timesheets.Server.TimeEntries;

namespace Hexalith.Timesheets.Server.ApprovedTimeLedger;

public sealed class ApprovedTimeLedgerQueryService
{
    private readonly ITimesheetsAccessGuard _accessGuard;
    private readonly ITimeEntryDisplayHydrator _displayHydrator;
    private readonly IApprovedTimeLedgerProjectionReader _projectionReader;
    private readonly bool _trustedPublicationAllowed;

    public ApprovedTimeLedgerQueryService(
        ITimesheetsAccessGuard accessGuard,
        IApprovedTimeLedgerProjectionReader projectionReader,
        ITimeEntryDisplayHydrator displayHydrator)
        : this(accessGuard, projectionReader, displayHydrator, TimesheetsEvidencePolicyOptions.FailClosedDefault)
    {
    }

    public ApprovedTimeLedgerQueryService(
        ITimesheetsAccessGuard accessGuard,
        IApprovedTimeLedgerProjectionReader projectionReader,
        ITimeEntryDisplayHydrator displayHydrator,
        TimesheetsEvidencePolicyOptions evidencePolicyOptions)
    {
        ArgumentNullException.ThrowIfNull(accessGuard);
        ArgumentNullException.ThrowIfNull(projectionReader);
        ArgumentNullException.ThrowIfNull(displayHydrator);
        ArgumentNullException.ThrowIfNull(evidencePolicyOptions);

        _trustedPublicationAllowed = evidencePolicyOptions.AllowsProjectionPublication();
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
            TimesheetsAuthorizationDecision rowDecision = await AuthorizeRowAsync(context, row, cancellationToken)
                .ConfigureAwait(false);

            if (!rowDecision.IsAuthorized)
            {
                if (CanFilterRow(rowDecision))
                {
                    continue;
                }

                return ApprovedTimeLedgerQueryResult.NotFoundOrDenied(rowDecision);
            }

            ApprovedTimeLedgerRowReadModel published = row.ApplyPublicationPolicy(_trustedPublicationAllowed);
            TimeEntryDisplayHydration hydration = await _displayHydrator
                .HydrateAsync(context, ToEvidence(published), cancellationToken)
                .ConfigureAwait(false);

            disclosedRows.Add(published with
            {
                DisplayHydration = hydration
            });
        }

        bool freshEnoughForExport = page.ProjectionFreshness.State == ProjectionFreshnessState.Fresh;
        bool canUseForExport = page.CanUseForExport && disclosedRows.Count > 0 && freshEnoughForExport;

        return ApprovedTimeLedgerQueryResult.Disclosed(page with
        {
            Items = disclosedRows,
            CanUseForExport = canUseForExport,
            ExportReadinessDetail = ResolveExportReadinessDetail(canUseForExport, freshEnoughForExport, disclosedRows.Count)
        });
    }

    private static string ResolveExportReadinessDetail(bool canUseForExport, bool freshEnoughForExport, int rowCount)
    {
        if (canUseForExport)
        {
            return "Approved ledger rows are fresh enough for export preview.";
        }

        if (!freshEnoughForExport)
        {
            return "Projection freshness does not allow export preview.";
        }

        return rowCount == 0 ? "No approved ledger rows are available for export preview."
            : "The ledger reader has withheld these rows from export.";
    }

    private static bool CanFilterRow(TimesheetsAuthorizationDecision decision)
        => decision.DenialCategory == TimesheetsDenialCategory.InsufficientRole;

    /// <summary>Authorizes the current and historical evidence scopes of a ledger row.</summary>
    internal async ValueTask<TimesheetsAuthorizationDecision> AuthorizeRowAsync(
        TimesheetsRequestContext context, ApprovedTimeLedgerRowReadModel row, CancellationToken cancellationToken)
    {
        if (context.Tenant is null || row.TimeEntryId is null
            || row.ApprovalDecision is not { ApprovalState: TimeEntryApprovalState.Approved } approval
            || approval.TimeEntryId != row.TimeEntryId || approval.Tenant != context.Tenant
            || (row.ApprovedCorrection is { } approved
                && (approved.TimeEntryId != row.TimeEntryId || approved.Tenant != context.Tenant))
            || (row.Correction is { } correction
                && (correction.TimeEntryId != row.TimeEntryId || correction.Tenant != context.Tenant)))
        {
            return TimesheetsAuthorizationDecision.Denied(TimesheetsDenialCategory.AmbiguousAuthority,
                "The approved ledger evidence could not be verified.");
        }

        foreach ((TimeEntryTargetReference? target, PartyReference? contributor) in DisclosureScopes(row).Distinct())
        {
            if (target is null || contributor is null)
            {
                return TimesheetsAuthorizationDecision.Denied(TimesheetsDenialCategory.AmbiguousAuthority,
                    "The approved ledger evidence could not be verified.");
            }

            TimesheetsAuthorizationRequest request = new(context, TimesheetsOperation.ProjectionRead)
            {
                Contributor = contributor,
                Project = target.TargetKind == TimeEntryTargetKind.Project ? new(target.TargetId) : null,
                Work = target.TargetKind == TimeEntryTargetKind.Work ? new(target.TargetId) : null
            };
            TimesheetsAuthorizationDecision decision = await _accessGuard.AuthorizeAsync(request, cancellationToken)
                .ConfigureAwait(false);
            if (!decision.IsAuthorized)
            {
                return decision;
            }
        }
        return TimesheetsAuthorizationDecision.Allowed();
    }

    private static IEnumerable<(TimeEntryTargetReference?, PartyReference?)> DisclosureScopes(ApprovedTimeLedgerRowReadModel row)
    {
        yield return (row.Target, row.Contributor);
        if (row.ApprovedCorrection is { } approved)
        {
            yield return (approved.PreviousValues?.Target, approved.PreviousValues?.Contributor);
            yield return (approved.CorrectedValues?.Target, approved.CorrectedValues?.Contributor);
        }
        if (row.Correction is { } correction)
        {
            yield return (correction.PreviousValues?.Target, correction.PreviousValues?.Contributor);
            yield return (correction.CorrectedValues?.Target, correction.CorrectedValues?.Contributor);
        }
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
