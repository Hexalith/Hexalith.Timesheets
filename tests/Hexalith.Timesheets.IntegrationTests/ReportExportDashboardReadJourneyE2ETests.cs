using Hexalith.Timesheets.Contracts.Commands.Exports;
using Hexalith.Timesheets.Contracts.Events.Exports;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Exports;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.Queries.TimeEntries;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Projections.ApprovedTimeLedger;
using Hexalith.Timesheets.Projections.OperationalReports;
using Hexalith.Timesheets.Projections.TimeEntries;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.Dashboard;
using Hexalith.Timesheets.Server.Exports;
using Hexalith.Timesheets.Server.OperationalReports;
using Hexalith.Timesheets.Server.TimeEntries;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

/// <summary>
/// Fast-baseline functional E2E coverage for the NFR11 report/export/dashboard read journey.
/// The opt-in <see cref="ReportExportDashboardQueryPerformanceLaneTests"/> only asserts these
/// composed read paths behind <c>TIMESHEETS_PERF=1</c>; this suite exercises the same seeded
/// composition once (no timing loop) so the report/ledger/export/preview/dashboard correctness
/// and the fail-closed authorization boundary stay guarded in the default CI baseline.
/// </summary>
public sealed class ReportExportDashboardReadJourneyE2ETests
{
    [Fact]
    public async Task Project_report_discloses_expected_rows_across_tenant_project_and_period_filters()
    {
        QueryFixture fixture = QueryFixture.CreateAllowed();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await AssertProjectReportAsync(
            fixture,
            new QueryProjectActualTimeReport { SortBy = ActualTimeReportSortBy.TargetReference, PageSize = 50 },
            expectedRowCount: 6,
            cancellationToken);

        await AssertProjectReportAsync(
            fixture,
            new QueryProjectActualTimeReport
            {
                Project = new ProjectReference("project-1"),
                SortBy = ActualTimeReportSortBy.TargetReference,
                PageSize = 50
            },
            expectedRowCount: 2,
            cancellationToken);

        await AssertProjectReportAsync(
            fixture,
            new QueryProjectActualTimeReport
            {
                TenantLocalPeriodKey = "2026-06",
                SortBy = ActualTimeReportSortBy.TargetReference,
                PageSize = 50
            },
            expectedRowCount: 3,
            cancellationToken);

        await AssertProjectReportAsync(
            fixture,
            new QueryProjectActualTimeReport
            {
                Project = new ProjectReference("project-1"),
                TenantLocalPeriodKey = "2026-06",
                SortBy = ActualTimeReportSortBy.TargetReference,
                PageSize = 50
            },
            expectedRowCount: 1,
            cancellationToken);
    }

    [Fact]
    public async Task Work_report_discloses_supplied_works_planned_effort()
    {
        QueryFixture fixture = QueryFixture.CreateAllowed();

        ActualTimeReportQueryResult result = await fixture.ReportService.QueryWorkAsync(
            Context(),
            new QueryWorkActualTimeReport
            {
                Work = new WorkReference("work-1"),
                TenantLocalPeriodKey = "2026-06",
                SortBy = ActualTimeReportSortBy.TargetReference,
                PageSize = 50
            },
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeTrue();
        ActualTimeReportReadModel page = result.Page.ShouldNotBeNull();
        page.Items.Count.ShouldBe(1);
        page.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        foreach (ActualTimeReportRowReadModel row in page.Items)
        {
            row.WorkPlannedEffort.ShouldNotBeNull().Availability.ShouldBe(WorkPlannedEffortAvailability.Supplied);
        }
    }

    [Fact]
    public async Task Approved_time_ledger_discloses_export_ready_multi_page_source()
    {
        QueryFixture fixture = QueryFixture.CreateAllowed();

        ApprovedTimeLedgerQueryResult result = await fixture.LedgerService.QueryAsync(
            Context(),
            LedgerQuery() with { PageSize = 25 },
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeTrue();
        ApprovedTimeLedgerReadModel page = result.Page.ShouldNotBeNull();
        page.Items.Count.ShouldBe(25);
        page.NextCursor.ShouldNotBeNull("a 60-row ledger paged at 25 must expose a next cursor.");
        page.CanUseForExport.ShouldBeTrue();
        page.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
    }

    [Fact]
    public async Task Approved_time_export_generates_full_cursor_scope_while_preview_stays_side_effect_free()
    {
        QueryFixture fixture = QueryFixture.CreateAllowed();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        ApprovedTimeExportResult export = await fixture.ExportService.GenerateAsync(
            Context(),
            new GenerateApprovedTimeExport { LedgerQuery = LedgerQuery() with { PageSize = 25 } },
            RequestedAtUtc(),
            cancellationToken);

        export.WasGenerated.ShouldBeTrue();
        ApprovedTimeExportReadModel generated = export.Export.ShouldNotBeNull();
        generated.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Ready);
        generated.Scope.RowCount.ShouldBe(60, "export must accumulate the full disclosed scope across all ledger pages.");
        generated.Rows.Count.ShouldBe(60);
        generated.CsvContent.ShouldNotBeNull().Length.ShouldBeGreaterThan(0);
        generated.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);

        ApprovedTimePreviewResult preview = await fixture.ExportService.PreviewAsync(
            Context(),
            new PreviewApprovedTimeExport { LedgerQuery = LedgerQuery() with { PageSize = 25 } },
            RequestedAtUtc(),
            cancellationToken);

        preview.WasDisclosed.ShouldBeTrue();
        ApprovedTimeExportPreviewReadModel previewed = preview.Preview.ShouldNotBeNull();
        previewed.IsReady.ShouldBeTrue();
        previewed.Scope.RowCount.ShouldBe(60, "preview must measure the same full-cursor scope as generation.");
        previewed.Audit.GeneratedAtUtc.ShouldBeNull("preview is side-effect-free: no export audit timestamp.");
        previewed.Audit.OutputContentHashSha256.ShouldBeNull("preview is side-effect-free: no CSV output hash.");
        previewed.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
    }

    [Fact]
    public async Task Dashboard_overview_composes_all_read_sections()
    {
        QueryFixture fixture = QueryFixture.CreateAllowed();

        TimesheetsDashboardOverviewQueryResult result = await fixture.DashboardService.QueryAsync(
            Context(),
            new QueryTimesheetsDashboardOverview
            {
                TenantLocalPeriodKey = "2026-06",
                ServiceDateFrom = new DateOnly(2026, 6, 1),
                ServiceDateTo = new DateOnly(2026, 6, 30)
            },
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeTrue();
        TimesheetsDashboardOverviewReadModel overview = result.Overview.ShouldNotBeNull();
        overview.CurrentPeriod.EntryCount.ShouldBe(30);
        overview.ApprovalWorkload.SubmittedEntryCount.ShouldBe(0);
        overview.LedgerReadiness.DisclosedApprovedRowCount.ShouldBe(30);
        overview.LedgerReadiness.ExportReadiness.ShouldBe(ApprovedTimeExportReadinessState.Ready);
        overview.ReportShortcuts.Count.ShouldBe(3);
        overview.ProjectionStatuses.ShouldAllBe(static status =>
            status.Freshness.State == ProjectionFreshnessState.Fresh);
    }

    [Fact]
    public async Task Report_query_fails_closed_when_tenant_projection_read_is_denied()
    {
        // NFR8: the lane uses an allow-all guard only to reach the read path. With the real
        // tenant-first ProjectionRead gate denied, the composed report path must short-circuit
        // to a non-disclosed result rather than leaking a seeded page.
        QueryFixture fixture = QueryFixture.CreateDenied();

        ActualTimeReportQueryResult result = await fixture.ReportService.QueryProjectAsync(
            Context(),
            new QueryProjectActualTimeReport { SortBy = ActualTimeReportSortBy.TargetReference, PageSize = 50 },
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeFalse("a denied projection read must not disclose report rows.");
        result.Page.ShouldBeNull();
    }

    private static async Task AssertProjectReportAsync(
        QueryFixture fixture,
        QueryProjectActualTimeReport query,
        int expectedRowCount,
        CancellationToken cancellationToken)
    {
        ActualTimeReportQueryResult result = await fixture.ReportService
            .QueryProjectAsync(Context(), query, cancellationToken);

        result.WasDisclosed.ShouldBeTrue();
        ActualTimeReportReadModel page = result.Page.ShouldNotBeNull();
        page.Items.Count.ShouldBe(expectedRowCount);
        page.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
    }

    private static QueryApprovedTimeLedger LedgerQuery()
        => new()
        {
            BillableState = BillableState.Billable,
            CurrentRowsOnly = true,
            SortBy = TimeEntryQuerySortBy.TimeEntryId
        };

    private static IReadOnlyList<TimeEntryProjectionEvent> SeedEvents()
    {
        List<TimeEntryProjectionEvent> events = [];
        long sequence = 0;

        for (int index = 0; index < 60; index++)
        {
            string id = $"time-entry-{index + 1:D3}";
            bool projectTarget = index % 2 == 0;
            int targetNumber = (index % 3) + 1;
            DateOnly serviceDate = index % 4 < 2
                ? new DateOnly(2026, 6, 10 + (index % 10))
                : new DateOnly(2026, 7, 10 + (index % 10));
            TimeEntryTargetReference target = projectTarget
                ? TimeEntryTargetReference.ForProject(new ProjectReference($"project-{targetNumber}"))
                : TimeEntryTargetReference.ForWork(new WorkReference($"work-{targetNumber}"));

            events.Add(Event($"m{++sequence}", sequence, Recorded(id, target, serviceDate, 30 + (index % 6) * 15)));
            events.Add(Event($"m{++sequence}", sequence, Submitted(id)));
            events.Add(Event($"m{++sequence}", sequence, Approved(id, $"decision-{index + 1:D3}")));
        }

        return events;
    }

    private static TimeEntryProjectionEvent Event(string messageId, long sequenceNumber, object payload)
        => new(messageId, sequenceNumber, payload);

    private static TimeEntryRecorded Recorded(
        string id,
        TimeEntryTargetReference target,
        DateOnly serviceDate,
        int durationMinutes)
        => new(
            new TimeEntryId(id),
            target,
            new PartyReference("party-1"),
            new ActivityTypeId("activity-type-1"),
            ActivityTypeScope.Tenant,
            serviceDate,
            durationMinutes,
            BillableState.Billable,
            TimeEntryApprovalState.Draft,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable);

    private static TimeEntrySubmitted Submitted(string id)
        => new(
            new TimeEntryId(id),
            new PartyReference("submitter-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero),
            new TimeEntrySubmissionId("submission-" + id),
            TimeEntrySubmissionScope.SelectedEntries,
            TimeEntryApprovalState.Submitted);

    private static TimeEntryApproved Approved(string id, string decisionId)
        => new(
            new TimeEntryId(id),
            new PartyReference("approver-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            new TimeEntryApprovalDecisionId(decisionId),
            TimeEntryApprovalState.Approved,
            Authority(ApprovalAuthorityAction.EntryApproval),
            TimeEntryApprovalScope.IndividualEntry);

    private static ApprovalAuthoritySourceAttribution Authority(ApprovalAuthorityAction action)
        => new(
            action,
            ApprovalAuthoritySource.ProjectApprover,
            ApprovalAuthorityDecisionState.Allowed,
            "timesheets.approval-authority.v1",
            "v1",
            ProjectionFreshnessMetadata.Fresh);

    private static TimesheetsRequestContext Context()
        => new(new TenantReference("tenant-1"), new PartyReference("operator-1"), "correlation-1");

    private static DateTimeOffset RequestedAtUtc()
        => new(2026, 6, 19, 15, 0, 0, TimeSpan.Zero);

    private sealed class QueryFixture
    {
        private QueryFixture(
            ActualTimeReportQueryService reportService,
            ApprovedTimeLedgerQueryService ledgerService,
            ApprovedTimeExportService exportService,
            TimesheetsDashboardOverviewQueryService dashboardService)
        {
            ReportService = reportService;
            LedgerService = ledgerService;
            ExportService = exportService;
            DashboardService = dashboardService;
        }

        public ActualTimeReportQueryService ReportService { get; }

        public ApprovedTimeLedgerQueryService LedgerService { get; }

        public ApprovedTimeExportService ExportService { get; }

        public TimesheetsDashboardOverviewQueryService DashboardService { get; }

        public static QueryFixture CreateAllowed()
            => Create(new ConfigurableAccessGuard(TimesheetsAuthorizationDecision.Allowed()));

        public static QueryFixture CreateDenied()
            => Create(new ConfigurableAccessGuard(
                TimesheetsAuthorizationDecision.Denied("Projection read denied for this tenant.")));

        private static QueryFixture Create(ConfigurableAccessGuard guard)
        {
            IReadOnlyList<TimeEntryProjectionEvent> events = SeedEvents();
            TimesheetsProjectionCheckpoint ledgerCheckpoint = new(
                "tenant-1",
                ApprovedTimeLedgerProjection.ProjectionName,
                events.Count,
                ProjectionFreshness.Fresh);
            TimesheetsProjectionCheckpoint reportCheckpoint = new(
                "tenant-1",
                ActualTimeReportProjection.ProjectionName,
                events.Count,
                ProjectionFreshness.Fresh);
            TimesheetsProjectionCheckpoint listCheckpoint = new(
                "tenant-1",
                TimeEntryEvidenceListProjection.ProjectionName,
                events.Count,
                ProjectionFreshness.Fresh);

            StaticHydrationProvider hydration = new();
            ActualTimeReportQueryService reportService = new(
                guard,
                new SeededReportReader(new ActualTimeReportProjection(), events, reportCheckpoint),
                hydration,
                hydration,
                hydration,
                hydration,
                new StaticPlannedEffortProvider());
            ApprovedTimeLedgerQueryService ledgerService = new(
                guard,
                new SeededLedgerReader(new ApprovedTimeLedgerProjection(), events, ledgerCheckpoint),
                hydration);
            ApprovedTimeExportService exportService = new(
                guard,
                ledgerService,
                new NoOpAuditRecorder());
            TimeEntryEvidenceListQueryService timeEntryService = new(
                guard,
                new SeededListReader(new TimeEntryEvidenceListProjection(), events, listCheckpoint),
                hydration);
            TimesheetsDashboardOverviewQueryService dashboardService = new(
                guard,
                timeEntryService,
                ledgerService,
                reportService);

            return new QueryFixture(reportService, ledgerService, exportService, dashboardService);
        }
    }

    private sealed class SeededReportReader(
        ActualTimeReportProjection projection,
        IReadOnlyList<TimeEntryProjectionEvent> events,
        TimesheetsProjectionCheckpoint checkpoint) : IActualTimeReportProjectionReader
    {
        public ValueTask<ActualTimeReportReadModel?> QueryProjectAsync(
            TimesheetsRequestContext context,
            QueryProjectActualTimeReport query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context.Tenant);

            return ValueTask.FromResult<ActualTimeReportReadModel?>(
                projection.ProjectByProject(context.Tenant.TenantId, events, checkpoint, query));
        }

        public ValueTask<ActualTimeReportReadModel?> QueryWorkAsync(
            TimesheetsRequestContext context,
            QueryWorkActualTimeReport query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context.Tenant);

            return ValueTask.FromResult<ActualTimeReportReadModel?>(
                projection.ProjectByWork(context.Tenant.TenantId, events, checkpoint, query));
        }
    }

    private sealed class SeededLedgerReader(
        ApprovedTimeLedgerProjection projection,
        IReadOnlyList<TimeEntryProjectionEvent> events,
        TimesheetsProjectionCheckpoint checkpoint) : IApprovedTimeLedgerProjectionReader
    {
        public ValueTask<ApprovedTimeLedgerReadModel?> QueryAsync(
            TimesheetsRequestContext context,
            QueryApprovedTimeLedger query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context.Tenant);

            return ValueTask.FromResult<ApprovedTimeLedgerReadModel?>(
                projection.Project(context.Tenant.TenantId, events, checkpoint, query));
        }
    }

    private sealed class SeededListReader(
        TimeEntryEvidenceListProjection projection,
        IReadOnlyList<TimeEntryProjectionEvent> events,
        TimesheetsProjectionCheckpoint checkpoint) : ITimeEntryEvidenceListProjectionReader
    {
        public ValueTask<TimeEntryQueryReadModel?> QueryAsync(
            TimesheetsRequestContext context,
            QueryTimeEntries query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context.Tenant);

            return ValueTask.FromResult<TimeEntryQueryReadModel?>(
                projection.Project(context.Tenant.TenantId, events, checkpoint, query));
        }
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
            => ValueTask.FromResult(new TimeEntryDisplayHydration(
                TimeEntryHydratedDisplayLabel.Fresh("Contributor"),
                TimeEntryHydratedDisplayLabel.Fresh("Target"),
                TimeEntryHydratedDisplayLabel.Fresh("Activity Type")));

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
            => ValueTask.FromResult(WorkPlannedEffortReadModel.Supplied(
                160,
                40,
                120,
                "minutes",
                ProjectionFreshnessMetadata.Fresh));
    }

    private sealed class NoOpAuditRecorder : IApprovedTimeExportAuditRecorder
    {
        public ValueTask<TimesheetsDomainResult> RecordAcceptedExportAsync(
            ApprovedTimeExported evidence,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(TimesheetsDomainResult.Success([evidence]));
    }

    private sealed class ConfigurableAccessGuard(TimesheetsAuthorizationDecision decision) : ITimesheetsAccessGuard
    {
        public ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
            TimesheetsAuthorizationRequest request,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(decision);

        public async ValueTask<TimesheetsAuthorizationDecision> ExecuteIfAuthorizedAsync(
            TimesheetsAuthorizationRequest request,
            Func<CancellationToken, ValueTask> trustedWork,
            CancellationToken cancellationToken)
        {
            if (!decision.IsAuthorized)
            {
                return decision;
            }

            await trustedWork(cancellationToken).ConfigureAwait(false);
            return decision;
        }

        public ValueTask<TimesheetsUiActionPolicyOutcome> EvaluateUiActionAsync(
            TimesheetsAuthorizationRequest request,
            TimesheetsUiActionVisibility deniedVisibility,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(TimesheetsUiActionPolicyOutcome.FromDecision(
                request.UiAction.GetValueOrDefault(),
                decision,
                deniedVisibility));
    }
}
