using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.Queries.TimeEntries;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.Dashboard;
using Hexalith.Timesheets.Server.OperationalReports;
using Hexalith.Timesheets.Server.TimeEntries;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed class TimesheetsDashboardOverviewIntegrationTests
{
    [Fact]
    public async Task Dashboard_composes_period_pending_approval_reports_ledger_and_empty_action_from_projection_readers()
    {
        AllowAllAccessGuard guard = new();
        QueueTimeReader timeReader = new(
            new(
                [
                    TimeRow("draft-entry", TimeEntryApprovalState.Draft),
                    TimeRow("rejected-entry", TimeEntryApprovalState.Rejected),
                    TimeRow("approved-entry", TimeEntryApprovalState.Approved)
                ],
                null,
                ProjectionFreshnessMetadata.Fresh),
            new(
                [TimeRow("submitted-entry", TimeEntryApprovalState.Submitted)],
                null,
                ProjectionFreshnessMetadata.Fresh));
        StaticHydrationProvider hydration = new();
        TimesheetsDashboardOverviewQueryService service = new(
            guard,
            new TimeEntryEvidenceListQueryService(guard, timeReader, hydration),
            new ApprovedTimeLedgerQueryService(
                guard,
                new StaticLedgerReader(new(
                    [LedgerRow("approved-entry")],
                    null,
                    ProjectionFreshnessMetadata.Fresh,
                    true,
                    "Approved ledger rows are fresh enough for export preview.")),
                hydration),
            new ActualTimeReportQueryService(
                guard,
                new StaticReportReader(),
                hydration,
                hydration,
                hydration,
                hydration,
                new StaticPlannedEffortProvider()));

        TimesheetsDashboardOverviewQueryResult result = await service.QueryAsync(
            Context(),
            new QueryTimesheetsDashboardOverview
            {
                TenantLocalPeriodKey = "2026-06",
                ServiceDateFrom = new DateOnly(2026, 6, 1),
                ServiceDateTo = new DateOnly(2026, 6, 30),
                Project = new ProjectReference("project-1")
            },
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeTrue();
        TimesheetsDashboardOverviewReadModel overview = result.Overview.ShouldNotBeNull();
        overview.CurrentPeriod.State.ShouldBe(TimesheetsDashboardCurrentPeriodState.NeedsCorrection);
        overview.CurrentPeriod.EntryCount.ShouldBe(3);
        overview.PendingActions.PendingSubmissionCount.ShouldBe(1);
        overview.PendingActions.PendingCorrectionCount.ShouldBe(1);
        overview.ApprovalWorkload.SubmittedEntryCount.ShouldBe(1);
        overview.ApprovalWorkload.Visibility.ShouldBe(TimesheetsDashboardShortcutVisibility.Visible);
        overview.ReportShortcuts.Select(static shortcut => shortcut.Intent)
            .ShouldContain("Timesheets.QueryProjectActualTimeReport");
        overview.ReportShortcuts.Select(static shortcut => shortcut.Intent)
            .ShouldContain("Timesheets.QueryWorkActualTimeReport");
        overview.LedgerReadiness.DisclosedApprovedRowCount.ShouldBe(1);
        overview.LedgerReadiness.ExportReadiness.ShouldBe(ApprovedTimeExportReadinessState.Ready);
        overview.EmptyStateAction.Name.ShouldBe("record-time");
        overview.EmptyStateAction.PreservedContext.TenantLocalPeriodKey.ShouldBe("2026-06");
    }

    [Fact]
    public async Task Dashboard_empty_unavailable_projections_keep_record_time_as_only_ready_empty_state_action()
    {
        AllowAllAccessGuard guard = new();
        QueueTimeReader timeReader = new(
            new(
                [],
                null,
                ProjectionFreshnessMetadata.Unavailable("Current-period projection is unavailable.")),
            new(
                [],
                null,
                ProjectionFreshnessMetadata.Unavailable("Approval workload is unavailable.")));
        StaticHydrationProvider hydration = new();
        TimesheetsDashboardOverviewQueryService service = new(
            guard,
            new TimeEntryEvidenceListQueryService(guard, timeReader, hydration),
            new ApprovedTimeLedgerQueryService(
                guard,
                new StaticLedgerReader(new(
                    [],
                    null,
                    ProjectionFreshnessMetadata.Unavailable("Approved-Time Ledger is unavailable."),
                    false,
                    "Approved-Time Ledger is unavailable.")),
                hydration),
            new ActualTimeReportQueryService(
                guard,
                new StaticReportReader(ProjectionFreshnessMetadata.Unavailable("Reports are unavailable.")),
                hydration,
                hydration,
                hydration,
                hydration,
                new StaticPlannedEffortProvider()));

        TimesheetsDashboardOverviewQueryResult result = await service.QueryAsync(
            Context(),
            new QueryTimesheetsDashboardOverview
            {
                TenantLocalPeriodKey = "2026-06",
                ServiceDateFrom = new DateOnly(2026, 6, 1),
                ServiceDateTo = new DateOnly(2026, 6, 30)
            },
            TestContext.Current.CancellationToken);

        TimesheetsDashboardOverviewReadModel overview = result.Overview.ShouldNotBeNull();
        overview.CurrentPeriod.State.ShouldBe(TimesheetsDashboardCurrentPeriodState.NoEntries);
        overview.CurrentPeriod.EntryCount.ShouldBe(0);
        overview.PendingActions.PendingSubmissionCount.ShouldBe(0);
        overview.PendingActions.PendingCorrectionCount.ShouldBe(0);
        overview.EmptyStateAction.Name.ShouldBe("record-time");
        overview.EmptyStateAction.Label.ShouldBe("Record time");
        overview.EmptyStateAction.Intent.ShouldBe("Timesheets.RecordTime");
        overview.EmptyStateAction.State.ShouldBe(TimesheetsDashboardShortcutState.Ready);
        overview.EmptyStateAction.PreservedContext.TenantLocalPeriodKey.ShouldBe("2026-06");
        overview.ReportShortcuts.ShouldAllBe(static shortcut => shortcut.State == TimesheetsDashboardShortcutState.Unavailable);
        overview.LedgerReadiness.DisclosedApprovedRowCount.ShouldBe(0);
        overview.LedgerReadiness.ExportReadiness.ShouldBe(ApprovedTimeExportReadinessState.Blocked);
        overview.LedgerReadiness.ExportVisibility.ShouldBe(TimesheetsDashboardShortcutVisibility.Disabled);
        overview.LedgerReadiness.ExportShortcut.ShouldNotBeNull().State.ShouldBe(TimesheetsDashboardShortcutState.BlockedByFreshness);
        overview.ProjectionStatuses.ShouldAllBe(static status => status.DecisionAuthority == TimesheetsDashboardProjectionDecisionAuthority.Unavailable);
    }

    private static TimesheetsRequestContext Context()
        => new(new TenantReference("tenant-1"), new PartyReference("operator-1"), "correlation-1");

    private static TimeEntryQueryRowReadModel TimeRow(string id, TimeEntryApprovalState approvalState)
        => new(
            new TimeEntryId(id),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            new PartyReference("party-1"),
            new ActivityTypeId("activity-type-1"),
            ActivityTypeScope.Tenant,
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            approvalState,
            TimeEntryCorrectionState.None,
            ContributorCategory.Employee,
            TimeEntrySourceType.Employee,
            ProjectionFreshnessMetadata.Fresh);

    private static ApprovedTimeLedgerRowReadModel LedgerRow(string id)
        => new(
            new TimeEntryId(id),
            new PartyReference("party-1"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            new DateOnly(2026, 6, 19),
            60,
            new ActivityTypeId("activity-type-1"),
            ActivityTypeScope.Tenant,
            BillableState.Billable,
            ContributorCategory.Employee,
            Approval(id),
            TimeEntryLockEvidence.Approved(
                new TimeEntryApprovalDecisionId("decision-" + id),
                TimeEntryApprovalScope.IndividualEntry,
                new PartyReference("approver-1"),
                new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero)),
            ApprovedTimeLedgerRowState.Current,
            ProjectionFreshnessMetadata.Fresh);

    private static TimeEntryApprovalDecisionEvidence Approval(string id)
        => new(
            new TimeEntryId(id),
            new TimeEntryApprovalDecisionId("decision-" + id),
            new PartyReference("approver-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            TimeEntryApprovalState.Approved,
            TimeEntryApprovalScope.IndividualEntry,
            new(
                ApprovalAuthorityAction.EntryApproval,
                ApprovalAuthoritySource.ProjectApprover,
                ApprovalAuthorityDecisionState.Allowed,
                "timesheets.approval-authority.v1",
                "v1",
                ProjectionFreshnessMetadata.Fresh),
            null);

    private sealed class QueueTimeReader : ITimeEntryEvidenceListProjectionReader
    {
        private readonly Queue<TimeEntryQueryReadModel> _pages;

        public QueueTimeReader(params TimeEntryQueryReadModel[] pages)
        {
            _pages = new(pages);
        }

        public ValueTask<TimeEntryQueryReadModel?> QueryAsync(
            TimesheetsRequestContext context,
            QueryTimeEntries query,
            CancellationToken cancellationToken)
            => ValueTask.FromResult<TimeEntryQueryReadModel?>(_pages.Count == 0
                ? new([], null, ProjectionFreshnessMetadata.Fresh)
                : _pages.Dequeue());
    }

    private sealed class StaticLedgerReader(ApprovedTimeLedgerReadModel page) : IApprovedTimeLedgerProjectionReader
    {
        public ValueTask<ApprovedTimeLedgerReadModel?> QueryAsync(
            TimesheetsRequestContext context,
            QueryApprovedTimeLedger query,
            CancellationToken cancellationToken)
            => ValueTask.FromResult<ApprovedTimeLedgerReadModel?>(page);
    }

    private sealed class StaticReportReader : IActualTimeReportProjectionReader
    {
        private readonly ProjectionFreshnessMetadata _freshness;

        public StaticReportReader(ProjectionFreshnessMetadata? freshness = null)
        {
            _freshness = freshness ?? ProjectionFreshnessMetadata.Fresh;
        }

        public ValueTask<ActualTimeReportReadModel?> QueryProjectAsync(
            TimesheetsRequestContext context,
            QueryProjectActualTimeReport query,
            CancellationToken cancellationToken)
            => ValueTask.FromResult<ActualTimeReportReadModel?>(new([], null, _freshness));

        public ValueTask<ActualTimeReportReadModel?> QueryWorkAsync(
            TimesheetsRequestContext context,
            QueryWorkActualTimeReport query,
            CancellationToken cancellationToken)
            => ValueTask.FromResult<ActualTimeReportReadModel?>(new([], null, _freshness));
    }

    private sealed class StaticHydrationProvider :
        ITimeEntryDisplayHydrator,
        IPartyDisplayHydrationProvider,
        IProjectDisplayHydrationProvider,
        IWorkDisplayHydrationProvider,
        IActivityTypeDisplayHydrationProvider
    {
        public ValueTask<TimeEntryDisplayHydration> HydrateAsync(
            TimesheetsRequestContext context,
            TimeEntryEvidenceReadModel evidence,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(TimeEntryDisplayHydration.Unavailable());

        public ValueTask<TimeEntryHydratedDisplayLabel> HydrateContributorAsync(
            TimesheetsRequestContext context,
            PartyReference contributor,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(TimeEntryHydratedDisplayLabel.Fresh("Contributor"));

        public ValueTask<TimeEntryHydratedDisplayLabel> HydrateProjectAsync(
            TimesheetsRequestContext context,
            ProjectReference project,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(TimeEntryHydratedDisplayLabel.Fresh("Project"));

        public ValueTask<TimeEntryHydratedDisplayLabel> HydrateWorkAsync(
            TimesheetsRequestContext context,
            WorkReference work,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(TimeEntryHydratedDisplayLabel.Fresh("Work"));

        public ValueTask<TimeEntryHydratedDisplayLabel> HydrateActivityTypeAsync(
            TimesheetsRequestContext context,
            ActivityTypeId activityTypeId,
            ActivityTypeScope activityTypeScope,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(TimeEntryHydratedDisplayLabel.Fresh("Activity Type"));
    }

    private sealed class StaticPlannedEffortProvider : IWorkPlannedEffortProvider
    {
        public ValueTask<WorkPlannedEffortReadModel> GetPlannedEffortAsync(
            TimesheetsRequestContext context,
            WorkReference work,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(WorkPlannedEffortReadModel.NotSupplied());
    }

    private sealed class AllowAllAccessGuard : ITimesheetsAccessGuard
    {
        public ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
            TimesheetsAuthorizationRequest request,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(TimesheetsAuthorizationDecision.Allowed());

        public async ValueTask<TimesheetsAuthorizationDecision> ExecuteIfAuthorizedAsync(
            TimesheetsAuthorizationRequest request,
            Func<CancellationToken, ValueTask> trustedWork,
            CancellationToken cancellationToken)
        {
            await trustedWork(cancellationToken).ConfigureAwait(false);
            return TimesheetsAuthorizationDecision.Allowed();
        }

        public ValueTask<TimesheetsUiActionPolicyOutcome> EvaluateUiActionAsync(
            TimesheetsAuthorizationRequest request,
            TimesheetsUiActionVisibility deniedVisibility,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(TimesheetsUiActionPolicyOutcome.Allowed(request.UiAction.GetValueOrDefault()));
    }
}
