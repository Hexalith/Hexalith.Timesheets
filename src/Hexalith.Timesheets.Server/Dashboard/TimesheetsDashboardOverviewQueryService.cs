using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.Queries.TimeEntries;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.OperationalReports;
using Hexalith.Timesheets.Server.TimeEntries;

namespace Hexalith.Timesheets.Server.Dashboard;

public sealed class TimesheetsDashboardOverviewQueryService
{
    private readonly ITimesheetsAccessGuard _accessGuard;
    private readonly ActualTimeReportQueryService _actualTimeReportQueryService;
    private readonly ApprovedTimeLedgerQueryService _approvedTimeLedgerQueryService;
    private readonly TimeEntryEvidenceListQueryService _timeEntryQueryService;

    public TimesheetsDashboardOverviewQueryService(
        ITimesheetsAccessGuard accessGuard,
        TimeEntryEvidenceListQueryService timeEntryQueryService,
        ApprovedTimeLedgerQueryService approvedTimeLedgerQueryService,
        ActualTimeReportQueryService actualTimeReportQueryService)
    {
        ArgumentNullException.ThrowIfNull(accessGuard);
        ArgumentNullException.ThrowIfNull(timeEntryQueryService);
        ArgumentNullException.ThrowIfNull(approvedTimeLedgerQueryService);
        ArgumentNullException.ThrowIfNull(actualTimeReportQueryService);

        _accessGuard = accessGuard;
        _timeEntryQueryService = timeEntryQueryService;
        _approvedTimeLedgerQueryService = approvedTimeLedgerQueryService;
        _actualTimeReportQueryService = actualTimeReportQueryService;
    }

    public async ValueTask<TimesheetsDashboardOverviewQueryResult> QueryAsync(
        TimesheetsRequestContext context,
        QueryTimesheetsDashboardOverview query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);

        TimesheetsAuthorizationDecision tenantDecision = await _accessGuard.AuthorizeAsync(
            new(context, TimesheetsOperation.ProjectionRead),
            cancellationToken).ConfigureAwait(false);

        if (!tenantDecision.IsAuthorized)
        {
            return TimesheetsDashboardOverviewQueryResult.NotFoundOrDenied(tenantDecision);
        }

        TimesheetsDashboardContextReadModel preservedContext = ToDashboardContext(query);
        TimesheetsUiActionPolicyOutcome capture = await EvaluateActionAsync(
            context,
            TimesheetsUiAction.Capture,
            TimesheetsUiActionVisibility.Disabled,
            cancellationToken).ConfigureAwait(false);
        TimesheetsUiActionPolicyOutcome approval = await EvaluateActionAsync(
            context,
            TimesheetsUiAction.Approval,
            TimesheetsUiActionVisibility.Hidden,
            cancellationToken).ConfigureAwait(false);
        TimesheetsUiActionPolicyOutcome report = await EvaluateActionAsync(
            context,
            TimesheetsUiAction.Report,
            TimesheetsUiActionVisibility.Disabled,
            cancellationToken).ConfigureAwait(false);
        TimesheetsUiActionPolicyOutcome ledger = await EvaluateActionAsync(
            context,
            TimesheetsUiAction.Ledger,
            TimesheetsUiActionVisibility.Hidden,
            cancellationToken).ConfigureAwait(false);
        TimesheetsUiActionPolicyOutcome export = await EvaluateActionAsync(
            context,
            TimesheetsUiAction.Export,
            TimesheetsUiActionVisibility.Disabled,
            cancellationToken).ConfigureAwait(false);

        TimeEntryEvidenceListQueryResult currentEntries = await _timeEntryQueryService.QueryAsync(
            context,
            ToTimeEntryQuery(query),
            cancellationToken).ConfigureAwait(false);
        TimeEntryQueryReadModel currentPage = currentEntries.Page
            ?? new([], null, ProjectionFreshnessMetadata.Unavailable("Current-period projection is unavailable."));

        TimeEntryQueryReadModel approvalPage = new([], null, ProjectionFreshnessMetadata.Unavailable("Approval workload is unavailable."));
        if (approval.Visibility == TimesheetsUiActionVisibility.Allowed)
        {
            TimeEntryEvidenceListQueryResult approvalEntries = await _timeEntryQueryService.QueryAsync(
                context,
                ToApprovalQuery(query),
                cancellationToken).ConfigureAwait(false);
            approvalPage = approvalEntries.Page
                ?? approvalPage;
        }

        ActualTimeReportReadModel projectReport = await QueryProjectReportAsync(
            context,
            query,
            report,
            cancellationToken).ConfigureAwait(false);
        ActualTimeReportReadModel workReport = await QueryWorkReportAsync(
            context,
            query,
            report,
            cancellationToken).ConfigureAwait(false);

        ApprovedTimeLedgerReadModel ledgerPage = new(
            [],
            null,
            ProjectionFreshnessMetadata.Unavailable("Approved-Time Ledger is unavailable."),
            false,
            "Approved-Time Ledger is unavailable.");
        if (ledger.Visibility == TimesheetsUiActionVisibility.Allowed)
        {
            ApprovedTimeLedgerQueryResult ledgerResult = await _approvedTimeLedgerQueryService.QueryAsync(
                context,
                ToLedgerQuery(query),
                cancellationToken).ConfigureAwait(false);
            ledgerPage = ledgerResult.Page
                ?? ledgerPage;
        }

        TimesheetsDashboardCurrentPeriodSummary currentPeriod = ToCurrentPeriodSummary(currentPage);
        // Record time is a capture/write action whose availability follows capture
        // authority only. It must not be gated by read-projection freshness, otherwise
        // the AC5 safe empty-state action becomes BlockedByFreshness exactly when the
        // current-period projection is empty/unavailable.
        TimesheetsDashboardShortcutReadModel recordTime = Shortcut(
            "record-time",
            "Record time",
            "Timesheets.RecordTime",
            capture,
            TimesheetsDashboardShortcutState.Ready,
            preservedContext);

        TimesheetsDashboardOverviewReadModel overview = new(
            preservedContext,
            currentPeriod,
            new(
                currentPage.Items.Count(static row => row.ApprovalState == TimeEntryApprovalState.Draft),
                currentPage.Items.Count(static row => row.ApprovalState == TimeEntryApprovalState.Rejected),
                recordTime,
                currentPage.ProjectionFreshness,
                ResolvePendingMessage(currentPage)),
            ToApprovalSummary(approval, approvalPage, preservedContext),
            ToReportShortcuts(report, projectReport, workReport, preservedContext),
            ToLedgerSummary(ledger, export, ledgerPage, preservedContext),
            [
                ToProjectionStatus("current-period", currentPage.ProjectionFreshness),
                ToProjectionStatus("approval-workload", approvalPage.ProjectionFreshness),
                ToProjectionStatus("project-report", projectReport.ProjectionFreshness),
                ToProjectionStatus("work-report", workReport.ProjectionFreshness),
                ToProjectionStatus("approved-ledger", ledgerPage.ProjectionFreshness)
            ],
            recordTime);

        return TimesheetsDashboardOverviewQueryResult.Disclosed(overview);
    }

    private async ValueTask<TimesheetsUiActionPolicyOutcome> EvaluateActionAsync(
        TimesheetsRequestContext context,
        TimesheetsUiAction action,
        TimesheetsUiActionVisibility deniedVisibility,
        CancellationToken cancellationToken)
        => await _accessGuard.EvaluateUiActionAsync(
            new TimesheetsAuthorizationRequest(context, TimesheetsOperation.UiActionVisibility)
            {
                UiAction = action
            },
            deniedVisibility,
            cancellationToken).ConfigureAwait(false);

    private async ValueTask<ActualTimeReportReadModel> QueryProjectReportAsync(
        TimesheetsRequestContext context,
        QueryTimesheetsDashboardOverview query,
        TimesheetsUiActionPolicyOutcome report,
        CancellationToken cancellationToken)
    {
        if (report.Visibility != TimesheetsUiActionVisibility.Allowed)
        {
            return EmptyReport("Project report is blocked by policy.");
        }

        ActualTimeReportQueryResult result = await _actualTimeReportQueryService.QueryProjectAsync(
            context,
            ToProjectReportQuery(query),
            cancellationToken).ConfigureAwait(false);

        return result.Page ?? EmptyReport("Project report projection is unavailable.");
    }

    private async ValueTask<ActualTimeReportReadModel> QueryWorkReportAsync(
        TimesheetsRequestContext context,
        QueryTimesheetsDashboardOverview query,
        TimesheetsUiActionPolicyOutcome report,
        CancellationToken cancellationToken)
    {
        if (report.Visibility != TimesheetsUiActionVisibility.Allowed)
        {
            return EmptyReport("Work report is blocked by policy.");
        }

        ActualTimeReportQueryResult result = await _actualTimeReportQueryService.QueryWorkAsync(
            context,
            ToWorkReportQuery(query),
            cancellationToken).ConfigureAwait(false);

        return result.Page ?? EmptyReport("Work report projection is unavailable.");
    }

    private static TimesheetsDashboardContextReadModel ToDashboardContext(QueryTimesheetsDashboardOverview query)
        => new(
            query.TenantLocalPeriodKey,
            query.ServiceDateFrom,
            query.ServiceDateTo,
            query.Project,
            query.Work,
            query.ActivityTypeId,
            query.BillableState);

    private static QueryTimeEntries ToTimeEntryQuery(QueryTimesheetsDashboardOverview query)
        => new()
        {
            Project = query.Project,
            Work = query.Work,
            TenantLocalPeriodKey = query.TenantLocalPeriodKey,
            ServiceDateFrom = query.ServiceDateFrom,
            ServiceDateTo = query.ServiceDateTo,
            ActivityTypeId = query.ActivityTypeId,
            BillableState = query.BillableState,
            CurrentEntriesOnly = query.CurrentRowsOnly,
            PageSize = 200
        };

    private static QueryTimeEntries ToApprovalQuery(QueryTimesheetsDashboardOverview query)
        => ToTimeEntryQuery(query) with
        {
            ApprovalStates = [TimeEntryApprovalState.Submitted]
        };

    private static QueryApprovedTimeLedger ToLedgerQuery(QueryTimesheetsDashboardOverview query)
        => new()
        {
            Project = query.Project,
            Work = query.Work,
            TenantLocalPeriodKey = query.TenantLocalPeriodKey,
            ServiceDateFrom = query.ServiceDateFrom,
            ServiceDateTo = query.ServiceDateTo,
            ActivityTypeId = query.ActivityTypeId,
            BillableState = query.BillableState,
            CurrentRowsOnly = query.CurrentRowsOnly,
            PageSize = 200
        };

    private static QueryProjectActualTimeReport ToProjectReportQuery(QueryTimesheetsDashboardOverview query)
        => new()
        {
            Project = query.Project,
            TenantLocalPeriodKey = query.TenantLocalPeriodKey,
            ServiceDateFrom = query.ServiceDateFrom,
            ServiceDateTo = query.ServiceDateTo,
            ActivityTypeId = query.ActivityTypeId,
            BillableState = query.BillableState,
            CurrentRowsOnly = query.CurrentRowsOnly,
            PageSize = 50
        };

    private static QueryWorkActualTimeReport ToWorkReportQuery(QueryTimesheetsDashboardOverview query)
        => new()
        {
            Work = query.Work,
            TenantLocalPeriodKey = query.TenantLocalPeriodKey,
            ServiceDateFrom = query.ServiceDateFrom,
            ServiceDateTo = query.ServiceDateTo,
            ActivityTypeId = query.ActivityTypeId,
            BillableState = query.BillableState,
            CurrentRowsOnly = query.CurrentRowsOnly,
            PageSize = 50
        };

    private static TimesheetsDashboardCurrentPeriodSummary ToCurrentPeriodSummary(TimeEntryQueryReadModel page)
    {
        int draft = page.Items.Count(static row => row.ApprovalState == TimeEntryApprovalState.Draft);
        int submitted = page.Items.Count(static row => row.ApprovalState == TimeEntryApprovalState.Submitted);
        int approved = page.Items.Count(static row => row.ApprovalState == TimeEntryApprovalState.Approved);
        int rejected = page.Items.Count(static row => row.ApprovalState == TimeEntryApprovalState.Rejected);

        return new(
            ResolveCurrentPeriodState(page.Items.Count, draft, submitted, approved, rejected),
            page.Items.Count,
            draft,
            submitted,
            approved,
            rejected,
            page.ProjectionFreshness,
            page.Items.Count == 0
                ? "No current entries match these filters."
                : "Current period status is composed from disclosed time-entry projections.");
    }

    private static TimesheetsDashboardCurrentPeriodState ResolveCurrentPeriodState(
        int total,
        int draft,
        int submitted,
        int approved,
        int rejected)
    {
        if (total == 0)
        {
            return TimesheetsDashboardCurrentPeriodState.NoEntries;
        }

        if (rejected > 0)
        {
            return TimesheetsDashboardCurrentPeriodState.NeedsCorrection;
        }

        int nonZeroStates = new[] { draft, submitted, approved }.Count(static count => count > 0);
        if (nonZeroStates > 1)
        {
            return TimesheetsDashboardCurrentPeriodState.Mixed;
        }

        if (draft > 0)
        {
            return TimesheetsDashboardCurrentPeriodState.Draft;
        }

        if (submitted > 0)
        {
            return TimesheetsDashboardCurrentPeriodState.Submitted;
        }

        return TimesheetsDashboardCurrentPeriodState.Approved;
    }

    private static TimesheetsDashboardApprovalWorkloadSummary ToApprovalSummary(
        TimesheetsUiActionPolicyOutcome approval,
        TimeEntryQueryReadModel approvalPage,
        TimesheetsDashboardContextReadModel context)
    {
        TimesheetsDashboardShortcutReadModel? shortcut = approval.Visibility == TimesheetsUiActionVisibility.Hidden
            ? null
            : Shortcut(
                "open-approvals",
                "Approvals Queue",
                "Timesheets.OpenApprovalsQueue",
                approval,
                approvalPage.ProjectionFreshness.State == ProjectionFreshnessState.Fresh
                    ? TimesheetsDashboardShortcutState.Ready
                    : TimesheetsDashboardShortcutState.BlockedByFreshness,
                context);

        return new(
            MapAuthorityState(approval),
            MapVisibility(approval.Visibility),
            approval.Visibility == TimesheetsUiActionVisibility.Allowed ? approvalPage.Items.Count : 0,
            approvalPage.ProjectionFreshness,
            approval.Visibility == TimesheetsUiActionVisibility.Allowed
                ? "Approval workload is composed from disclosed submitted entries."
                : approval.SafeMessage,
            shortcut);
    }

    private static IReadOnlyList<TimesheetsDashboardShortcutReadModel> ToReportShortcuts(
        TimesheetsUiActionPolicyOutcome report,
        ActualTimeReportReadModel projectReport,
        ActualTimeReportReadModel workReport,
        TimesheetsDashboardContextReadModel context)
        =>
        [
            Shortcut(
                "open-project-report",
                "Project report",
                "Timesheets.QueryProjectActualTimeReport",
                report,
                ResolveShortcutState(projectReport.ProjectionFreshness, projectReport.Items.Count),
                context),
            Shortcut(
                "open-work-report",
                "Work report",
                "Timesheets.QueryWorkActualTimeReport",
                report,
                ResolveShortcutState(workReport.ProjectionFreshness, workReport.Items.Count),
                context),
            Shortcut(
                "open-ai-effort-report",
                "AI effort report",
                "Timesheets.QueryAiEffortReport",
                report,
                ResolveShortcutState(projectReport.ProjectionFreshness, projectReport.Items.Count),
                context)
        ];

    private static TimesheetsDashboardLedgerReadinessSummary ToLedgerSummary(
        TimesheetsUiActionPolicyOutcome ledger,
        TimesheetsUiActionPolicyOutcome export,
        ApprovedTimeLedgerReadModel ledgerPage,
        TimesheetsDashboardContextReadModel context)
    {
        TimesheetsDashboardShortcutReadModel? ledgerShortcut = ledger.Visibility == TimesheetsUiActionVisibility.Hidden
            ? null
            : Shortcut(
                "open-approved-ledger",
                "Approved-Time Ledger",
                "Timesheets.QueryApprovedTimeLedger",
                ledger,
                ResolveShortcutState(ledgerPage.ProjectionFreshness, ledgerPage.Items.Count),
                context);
        TimesheetsDashboardShortcutState exportState = ledgerPage.CanUseForExport
            ? TimesheetsDashboardShortcutState.Ready
            : ledgerPage.ProjectionFreshness.State == ProjectionFreshnessState.Fresh
                ? TimesheetsDashboardShortcutState.Empty
                : TimesheetsDashboardShortcutState.BlockedByFreshness;
        TimesheetsDashboardShortcutReadModel? exportShortcut = export.Visibility == TimesheetsUiActionVisibility.Hidden
            ? null
            : Shortcut(
                "export-approved-ledger",
                "Export approved ledger",
                "Timesheets.GenerateApprovedLedgerExport",
                export,
                exportState,
                context);

        return new(
            MapVisibility(ledger.Visibility),
            ledgerPage.CanUseForExport && export.Visibility == TimesheetsUiActionVisibility.Allowed
                ? TimesheetsDashboardShortcutVisibility.Visible
                : MapVisibility(export.Visibility == TimesheetsUiActionVisibility.Allowed ? TimesheetsUiActionVisibility.Disabled : export.Visibility),
            ledgerPage.CanUseForExport && export.Visibility == TimesheetsUiActionVisibility.Allowed
                ? ApprovedTimeExportReadinessState.Ready
                : ApprovedTimeExportReadinessState.Blocked,
            ledger.Visibility == TimesheetsUiActionVisibility.Allowed ? ledgerPage.Items.Count : 0,
            ledgerPage.ProjectionFreshness,
            ledger.Visibility == TimesheetsUiActionVisibility.Allowed
                ? ledgerPage.ExportReadinessDetail
                : ledger.SafeMessage,
            ledgerShortcut,
            exportShortcut);
    }

    private static TimesheetsDashboardShortcutReadModel Shortcut(
        string name,
        string label,
        string intent,
        TimesheetsUiActionPolicyOutcome outcome,
        TimesheetsDashboardShortcutState state,
        TimesheetsDashboardContextReadModel context)
        => new(
            name,
            label,
            intent,
            MapVisibility(outcome.Visibility),
            outcome.Visibility == TimesheetsUiActionVisibility.Allowed ? state : TimesheetsDashboardShortcutState.DisabledByPolicy,
            outcome.Visibility == TimesheetsUiActionVisibility.Allowed ? ResolveShortcutMessage(state) : outcome.SafeMessage,
            context);

    private static TimesheetsDashboardShortcutState ResolveShortcutState(
        ProjectionFreshnessMetadata freshness,
        int itemCount)
    {
        if (freshness.State == ProjectionFreshnessState.Unavailable)
        {
            return TimesheetsDashboardShortcutState.Unavailable;
        }

        if (freshness.State != ProjectionFreshnessState.Fresh)
        {
            return TimesheetsDashboardShortcutState.BlockedByFreshness;
        }

        return itemCount == 0
            ? TimesheetsDashboardShortcutState.Empty
            : TimesheetsDashboardShortcutState.Ready;
    }

    private static string ResolveShortcutMessage(TimesheetsDashboardShortcutState state)
        => state switch
        {
            TimesheetsDashboardShortcutState.Ready => "Shortcut is available.",
            TimesheetsDashboardShortcutState.Empty => "No matching data is available.",
            TimesheetsDashboardShortcutState.BlockedByFreshness => "Projection freshness requires attention before this shortcut is decision authority.",
            TimesheetsDashboardShortcutState.Unavailable => "Projection is unavailable.",
            TimesheetsDashboardShortcutState.DisabledByPolicy => "Access denied for this action.",
            _ => "Shortcut status is unknown."
        };

    private static string ResolvePendingMessage(TimeEntryQueryReadModel page)
        => page.ProjectionFreshness.State == ProjectionFreshnessState.Fresh
            ? "Pending actions are composed from disclosed current entries."
            : "Pending action counts require freshness review.";

    private static TimesheetsDashboardProjectionStatusReadModel ToProjectionStatus(
        string name,
        ProjectionFreshnessMetadata freshness)
        => new(
            name,
            freshness,
            freshness.State == ProjectionFreshnessState.Fresh
                ? TimesheetsDashboardProjectionDecisionAuthority.FreshDecisionAuthority
                : freshness.State == ProjectionFreshnessState.Unavailable
                    ? TimesheetsDashboardProjectionDecisionAuthority.Unavailable
                    : TimesheetsDashboardProjectionDecisionAuthority.StatusOnly,
            freshness.State == ProjectionFreshnessState.Fresh
                ? "Projection is fresh."
                : freshness.Detail ?? "Projection status requires attention.");

    private static TimesheetsDashboardShortcutVisibility MapVisibility(TimesheetsUiActionVisibility visibility)
        => visibility switch
        {
            TimesheetsUiActionVisibility.Allowed => TimesheetsDashboardShortcutVisibility.Visible,
            TimesheetsUiActionVisibility.Hidden => TimesheetsDashboardShortcutVisibility.Hidden,
            TimesheetsUiActionVisibility.Disabled => TimesheetsDashboardShortcutVisibility.Disabled,
            _ => TimesheetsDashboardShortcutVisibility.Unknown
        };

    private static TimesheetsDashboardAuthorityState MapAuthorityState(TimesheetsUiActionPolicyOutcome outcome)
        => outcome.Visibility == TimesheetsUiActionVisibility.Allowed
            ? TimesheetsDashboardAuthorityState.Allowed
            : outcome.SafeMessage.Equals(TimesheetsUiActionPolicyOutcome.AuthorityUnresolvedMessage, StringComparison.Ordinal)
                ? TimesheetsDashboardAuthorityState.Unresolved
                : TimesheetsDashboardAuthorityState.Denied;

    private static ActualTimeReportReadModel EmptyReport(string detail)
        => new([], null, ProjectionFreshnessMetadata.Unavailable(detail));
}
