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

namespace Hexalith.Timesheets.Server.Tests;

public sealed class TimesheetsDashboardOverviewQueryServiceTests
{
    [Fact]
    public async Task Dashboard_denies_tenant_before_any_projection_lookup()
    {
        DashboardFixture fixture = new(new DashboardAccessGuard
        {
            TenantDecision = TimesheetsAuthorizationDecision.Denied(
                TimesheetsDenialCategory.MissingTenant,
                "Tenant is missing.")
        });

        TimesheetsDashboardOverviewQueryResult result = await fixture.Service.QueryAsync(
            Context(),
            new QueryTimesheetsDashboardOverview(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeFalse();
        result.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.MissingTenant);
        fixture.TimeReader.Calls.ShouldBe(0);
        fixture.LedgerReader.Calls.ShouldBe(0);
        fixture.ReportReader.ProjectCalls.ShouldBe(0);
        fixture.ReportReader.WorkCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Dashboard_hides_approval_and_ledger_when_policy_denies_without_disclosing_protected_rows()
    {
        DashboardAccessGuard guard = new();
        guard.SetAction(TimesheetsUiAction.Approval, TimesheetsAuthorizationDecision.Denied(TimesheetsDenialCategory.InsufficientRole, "No approver role."));
        guard.SetAction(TimesheetsUiAction.Ledger, TimesheetsAuthorizationDecision.Denied(TimesheetsDenialCategory.InsufficientRole, "No ledger role."));
        guard.SetAction(TimesheetsUiAction.Export, TimesheetsAuthorizationDecision.Denied(TimesheetsDenialCategory.InsufficientRole, "No export role."));
        guard.SetAction(TimesheetsUiAction.Report, TimesheetsAuthorizationDecision.Denied(TimesheetsDenialCategory.InsufficientRole, "No report role."));
        DashboardFixture fixture = new(guard);
        fixture.TimeReader.Enqueue(TimePage([
            TimeRow("time-entry-1", TimeEntryApprovalState.Draft)
        ]));

        TimesheetsDashboardOverviewQueryResult result = await fixture.Service.QueryAsync(
            Context(),
            new QueryTimesheetsDashboardOverview
            {
                TenantLocalPeriodKey = "2026-06"
            },
            TestContext.Current.CancellationToken);

        TimesheetsDashboardOverviewReadModel overview = result.Overview.ShouldNotBeNull();
        overview.ApprovalWorkload.Visibility.ShouldBe(TimesheetsDashboardShortcutVisibility.Hidden);
        overview.ApprovalWorkload.SubmittedEntryCount.ShouldBe(0);
        overview.ApprovalWorkload.Shortcut.ShouldBeNull();
        overview.LedgerReadiness.LedgerVisibility.ShouldBe(TimesheetsDashboardShortcutVisibility.Hidden);
        overview.LedgerReadiness.DisclosedApprovedRowCount.ShouldBe(0);
        overview.LedgerReadiness.LedgerShortcut.ShouldBeNull();
        overview.EmptyStateAction.Label.ShouldBe("Record time");
        overview.CurrentPeriod.DraftEntryCount.ShouldBe(1);
        fixture.TimeReader.Calls.ShouldBe(1);
        fixture.LedgerReader.Calls.ShouldBe(0);
        fixture.ReportReader.ProjectCalls.ShouldBe(0);
        fixture.ReportReader.WorkCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Dashboard_surfaces_stale_and_degraded_projection_status_without_enabling_export()
    {
        DashboardFixture fixture = new(new DashboardAccessGuard());
        fixture.TimeReader.Enqueue(TimePage(
            [TimeRow("time-entry-1", TimeEntryApprovalState.Draft)],
            ProjectionFreshnessMetadata.Stale("cursor-1", detail: "Current period projection is stale.")));
        fixture.TimeReader.Enqueue(TimePage(
            [TimeRow("time-entry-2", TimeEntryApprovalState.Submitted)],
            ProjectionFreshnessMetadata.Fresh));
        fixture.LedgerReader.Page = new(
            [],
            null,
            ProjectionFreshnessMetadata.Degraded("Ledger projection is degraded."),
            false,
            "Projection freshness does not allow export preview.");
        fixture.ReportReader.ProjectPage = ReportPage(ProjectionFreshnessMetadata.Stale("report-cursor"));
        fixture.ReportReader.WorkPage = ReportPage(ProjectionFreshnessMetadata.Fresh);

        TimesheetsDashboardOverviewQueryResult result = await fixture.Service.QueryAsync(
            Context(),
            new QueryTimesheetsDashboardOverview
            {
                TenantLocalPeriodKey = "2026-06",
                Project = new ProjectReference("project-1"),
                ActivityTypeId = new ActivityTypeId("activity-type-1")
            },
            TestContext.Current.CancellationToken);

        TimesheetsDashboardOverviewReadModel overview = result.Overview.ShouldNotBeNull();
        overview.CurrentPeriod.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Stale);

        // Record time is a write action: it stays Ready when authorized even though the
        // current-period read projection is stale (read freshness must not gate capture).
        overview.PendingActions.PrimaryAction.State.ShouldBe(TimesheetsDashboardShortcutState.Ready);
        overview.ApprovalWorkload.AuthorityState.ShouldBe(TimesheetsDashboardAuthorityState.Allowed);
        overview.ApprovalWorkload.SubmittedEntryCount.ShouldBe(1);
        overview.LedgerReadiness.ExportReadiness.ShouldBe(ApprovedTimeExportReadinessState.Blocked);
        overview.LedgerReadiness.ExportVisibility.ShouldBe(TimesheetsDashboardShortcutVisibility.Disabled);
        overview.LedgerReadiness.ExportShortcut.ShouldNotBeNull().State.ShouldBe(TimesheetsDashboardShortcutState.BlockedByFreshness);
        overview.ProjectionStatuses.Single(status => status.Name == "current-period")
            .DecisionAuthority.ShouldBe(TimesheetsDashboardProjectionDecisionAuthority.StatusOnly);
        overview.ProjectionStatuses.Single(status => status.Name == "approved-ledger")
            .DecisionAuthority.ShouldBe(TimesheetsDashboardProjectionDecisionAuthority.StatusOnly);
        overview.ReportShortcuts.Single(shortcut => shortcut.Name == "open-project-report")
            .PreservedContext.Project.ShouldBe(new ProjectReference("project-1"));
    }

    private static TimesheetsRequestContext Context()
        => new(new TenantReference("tenant-1"), new PartyReference("operator-1"), "correlation-1");

    private static TimeEntryQueryReadModel TimePage(
        IReadOnlyList<TimeEntryQueryRowReadModel> rows,
        ProjectionFreshnessMetadata? freshness = null)
        => new(rows, null, freshness ?? ProjectionFreshnessMetadata.Fresh);

    private static TimeEntryQueryRowReadModel TimeRow(
        string id,
        TimeEntryApprovalState approvalState,
        TimeEntryTargetReference? target = null)
        => new(
            new TimeEntryId(id),
            target ?? TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
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

    private static ApprovedTimeLedgerReadModel LedgerPage(IReadOnlyList<ApprovedTimeLedgerRowReadModel> rows)
        => new(rows, null, ProjectionFreshnessMetadata.Fresh, rows.Count > 0, "Approved ledger rows are fresh enough for export preview.");

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

    private static ActualTimeReportReadModel ReportPage(ProjectionFreshnessMetadata freshness)
        => new([], null, freshness);

    private sealed class DashboardFixture
    {
        public DashboardFixture(DashboardAccessGuard guard)
        {
            TimeReader = new TrackingTimeReader();
            LedgerReader = new TrackingLedgerReader();
            ReportReader = new TrackingReportReader();
            TrackingHydrationProvider hydration = new();
            TimeEntryEvidenceListQueryService timeService = new(guard, TimeReader, hydration);
            ApprovedTimeLedgerQueryService ledgerService = new(guard, LedgerReader, hydration);
            ActualTimeReportQueryService reportService = new(
                guard,
                ReportReader,
                hydration,
                hydration,
                hydration,
                hydration,
                new TrackingPlannedEffortProvider());

            Service = new(guard, timeService, ledgerService, reportService);
        }

        public TimesheetsDashboardOverviewQueryService Service { get; }

        public TrackingTimeReader TimeReader { get; }

        public TrackingLedgerReader LedgerReader { get; }

        public TrackingReportReader ReportReader { get; }
    }

    private sealed class TrackingTimeReader : ITimeEntryEvidenceListProjectionReader
    {
        private readonly Queue<TimeEntryQueryReadModel> _pages = [];

        public int Calls { get; private set; }

        public void Enqueue(TimeEntryQueryReadModel page) => _pages.Enqueue(page);

        public ValueTask<TimeEntryQueryReadModel?> QueryAsync(
            TimesheetsRequestContext context,
            QueryTimeEntries query,
            CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult<TimeEntryQueryReadModel?>(_pages.Count == 0
                ? TimePage([])
                : _pages.Dequeue());
        }
    }

    private sealed class TrackingLedgerReader : IApprovedTimeLedgerProjectionReader
    {
        public int Calls { get; private set; }

        public ApprovedTimeLedgerReadModel Page { get; set; } = LedgerPage([LedgerRow("time-entry-ledger")]);

        public ValueTask<ApprovedTimeLedgerReadModel?> QueryAsync(
            TimesheetsRequestContext context,
            QueryApprovedTimeLedger query,
            CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult<ApprovedTimeLedgerReadModel?>(Page);
        }
    }

    private sealed class TrackingReportReader : IActualTimeReportProjectionReader
    {
        public int ProjectCalls { get; private set; }

        public int WorkCalls { get; private set; }

        public ActualTimeReportReadModel ProjectPage { get; set; } = ReportPage(ProjectionFreshnessMetadata.Fresh);

        public ActualTimeReportReadModel WorkPage { get; set; } = ReportPage(ProjectionFreshnessMetadata.Fresh);

        public ValueTask<ActualTimeReportReadModel?> QueryProjectAsync(
            TimesheetsRequestContext context,
            QueryProjectActualTimeReport query,
            CancellationToken cancellationToken)
        {
            ProjectCalls++;
            return ValueTask.FromResult<ActualTimeReportReadModel?>(ProjectPage);
        }

        public ValueTask<ActualTimeReportReadModel?> QueryWorkAsync(
            TimesheetsRequestContext context,
            QueryWorkActualTimeReport query,
            CancellationToken cancellationToken)
        {
            WorkCalls++;
            return ValueTask.FromResult<ActualTimeReportReadModel?>(WorkPage);
        }
    }

    private sealed class TrackingHydrationProvider :
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

    private sealed class TrackingPlannedEffortProvider : IWorkPlannedEffortProvider
    {
        public ValueTask<WorkPlannedEffortReadModel> GetPlannedEffortAsync(
            TimesheetsRequestContext context,
            WorkReference work,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(WorkPlannedEffortReadModel.NotSupplied());
    }

    private sealed class DashboardAccessGuard : ITimesheetsAccessGuard
    {
        private readonly Dictionary<TimesheetsUiAction, TimesheetsAuthorizationDecision> _actions = new();
        private bool _tenantDecisionReturned;

        public TimesheetsAuthorizationDecision TenantDecision { get; set; } = TimesheetsAuthorizationDecision.Allowed();

        public void SetAction(TimesheetsUiAction action, TimesheetsAuthorizationDecision decision)
            => _actions[action] = decision;

        public ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
            TimesheetsAuthorizationRequest request,
            CancellationToken cancellationToken)
        {
            if (!_tenantDecisionReturned)
            {
                _tenantDecisionReturned = true;
                return ValueTask.FromResult(TenantDecision);
            }

            return ValueTask.FromResult(TimesheetsAuthorizationDecision.Allowed());
        }

        public async ValueTask<TimesheetsAuthorizationDecision> ExecuteIfAuthorizedAsync(
            TimesheetsAuthorizationRequest request,
            Func<CancellationToken, ValueTask> trustedWork,
            CancellationToken cancellationToken)
        {
            TimesheetsAuthorizationDecision decision = await AuthorizeAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (decision.IsAuthorized)
            {
                await trustedWork(cancellationToken).ConfigureAwait(false);
            }

            return decision;
        }

        public ValueTask<TimesheetsUiActionPolicyOutcome> EvaluateUiActionAsync(
            TimesheetsAuthorizationRequest request,
            TimesheetsUiActionVisibility deniedVisibility,
            CancellationToken cancellationToken)
        {
            TimesheetsUiAction action = request.UiAction.GetValueOrDefault();
            TimesheetsAuthorizationDecision decision = _actions.GetValueOrDefault(action, TimesheetsAuthorizationDecision.Allowed());
            return ValueTask.FromResult(TimesheetsUiActionPolicyOutcome.FromDecision(action, decision, deniedVisibility));
        }
    }
}
