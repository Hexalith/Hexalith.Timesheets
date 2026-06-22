using System.Diagnostics;

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
/// Opt-in, infrastructure-free query/report/export/dashboard performance lane for NFR11.
/// </summary>
public sealed class ReportExportDashboardQueryPerformanceLaneTests
{
    private const string OptInVariable = "TIMESHEETS_PERF";
    private const int WarmupIterations = 100;
    private const int MeasuredIterations = 500;
    private const double Nfr11TargetMilliseconds = 2000.0;

    private readonly ITestOutputHelper _output;

    public ReportExportDashboardQueryPerformanceLaneTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Report_export_and_dashboard_query_latency_records_nfr11_p95_evidence()
    {
        if (Environment.GetEnvironmentVariable(OptInVariable) != "1")
        {
            Assert.Skip($"Set {OptInVariable}=1 to run the report/export/dashboard performance lane.");
        }

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        QueryFixture fixture = QueryFixture.Create();

        List<ScenarioMeasurement> measurements =
        [
            await MeasureProjectReportAsync(
                "report: project actuals tenant filter",
                fixture,
                new QueryProjectActualTimeReport
                {
                    SortBy = ActualTimeReportSortBy.TargetReference,
                    PageSize = 50
                },
                expectedRowCount: 6,
                cancellationToken),
            await MeasureProjectReportAsync(
                "report: project actuals tenant + project filter",
                fixture,
                new QueryProjectActualTimeReport
                {
                    Project = Project("project-1"),
                    SortBy = ActualTimeReportSortBy.TargetReference,
                    PageSize = 50
                },
                expectedRowCount: 2,
                cancellationToken),
            await MeasureProjectReportAsync(
                "report: project actuals tenant + period filter",
                fixture,
                new QueryProjectActualTimeReport
                {
                    TenantLocalPeriodKey = "2026-06",
                    SortBy = ActualTimeReportSortBy.TargetReference,
                    PageSize = 50
                },
                expectedRowCount: 3,
                cancellationToken),
            await MeasureProjectReportAsync(
                "report: project actuals tenant + project + period filter",
                fixture,
                new QueryProjectActualTimeReport
                {
                    Project = Project("project-1"),
                    TenantLocalPeriodKey = "2026-06",
                    SortBy = ActualTimeReportSortBy.TargetReference,
                    PageSize = 50
                },
                expectedRowCount: 1,
                cancellationToken),
            await MeasureWorkReportAsync(
                "report: work actuals with supplied Works effort",
                fixture,
                new QueryWorkActualTimeReport
                {
                    Work = Work("work-1"),
                    TenantLocalPeriodKey = "2026-06",
                    SortBy = ActualTimeReportSortBy.TargetReference,
                    PageSize = 50
                },
                expectedRowCount: 1,
                cancellationToken),
            await MeasureLedgerAsync(fixture, cancellationToken),
            await MeasureExportAsync(fixture, cancellationToken),
            await MeasurePreviewAsync(fixture, cancellationToken),
            await MeasureDashboardAsync(fixture, cancellationToken)
        ];

        ReportEvidence(measurements);

        foreach (ScenarioMeasurement measurement in measurements)
        {
            measurement.P95Milliseconds.ShouldBeLessThan(
                Nfr11TargetMilliseconds,
                $"{measurement.Scenario} p95 exceeded the NFR11 {Nfr11TargetMilliseconds} ms query sanity bound.");
        }
    }

    private async Task<ScenarioMeasurement> MeasureProjectReportAsync(
        string scenario,
        QueryFixture fixture,
        QueryProjectActualTimeReport query,
        int expectedRowCount,
        CancellationToken cancellationToken)
        => await MeasureAsync(
            scenario,
            async ct =>
            {
                ActualTimeReportQueryResult result = await fixture.ReportService
                    .QueryProjectAsync(Context(), query, ct)
                    .ConfigureAwait(false);

                result.WasDisclosed.ShouldBeTrue($"{scenario} should disclose a project report.");
                ActualTimeReportReadModel page = result.Page.ShouldNotBeNull();
                page.Items.Count.ShouldBe(expectedRowCount, $"{scenario} row count changed.");
                page.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
                return true;
            },
            cancellationToken);

    private async Task<ScenarioMeasurement> MeasureWorkReportAsync(
        string scenario,
        QueryFixture fixture,
        QueryWorkActualTimeReport query,
        int expectedRowCount,
        CancellationToken cancellationToken)
        => await MeasureAsync(
            scenario,
            async ct =>
            {
                ActualTimeReportQueryResult result = await fixture.ReportService
                    .QueryWorkAsync(Context(), query, ct)
                    .ConfigureAwait(false);

                result.WasDisclosed.ShouldBeTrue($"{scenario} should disclose a work report.");
                ActualTimeReportReadModel page = result.Page.ShouldNotBeNull();
                page.Items.Count.ShouldBe(expectedRowCount, $"{scenario} row count changed.");
                foreach (ActualTimeReportRowReadModel row in page.Items)
                {
                    row.WorkPlannedEffort.ShouldNotBeNull().Availability.ShouldBe(WorkPlannedEffortAvailability.Supplied);
                }
                page.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
                return true;
            },
            cancellationToken);

    private async Task<ScenarioMeasurement> MeasureLedgerAsync(QueryFixture fixture, CancellationToken cancellationToken)
        => await MeasureAsync(
            "ledger: approved-time ledger multi-page source",
            async ct =>
            {
                ApprovedTimeLedgerQueryResult result = await fixture.LedgerService
                    .QueryAsync(Context(), LedgerQuery() with { PageSize = 25 }, ct)
                    .ConfigureAwait(false);

                result.WasDisclosed.ShouldBeTrue("ledger query should disclose a page.");
                ApprovedTimeLedgerReadModel page = result.Page.ShouldNotBeNull();
                page.Items.Count.ShouldBe(25);
                page.NextCursor.ShouldNotBeNull();
                page.CanUseForExport.ShouldBeTrue();
                page.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
                return true;
            },
            cancellationToken);

    private async Task<ScenarioMeasurement> MeasureExportAsync(QueryFixture fixture, CancellationToken cancellationToken)
        => await MeasureAsync(
            "export: approved-time CSV generation full cursor",
            async ct =>
            {
                ApprovedTimeExportResult result = await fixture.ExportService
                    .GenerateAsync(
                        Context(),
                        new GenerateApprovedTimeExport { LedgerQuery = LedgerQuery() with { PageSize = 25 } },
                        RequestedAtUtc(),
                        ct)
                    .ConfigureAwait(false);

                result.WasGenerated.ShouldBeTrue("export generation should be ready.");
                ApprovedTimeExportReadModel export = result.Export.ShouldNotBeNull();
                export.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Ready);
                export.Scope.RowCount.ShouldBe(60);
                export.Rows.Count.ShouldBe(60);
                export.CsvContent.ShouldNotBeNull().Length.ShouldBeGreaterThan(0);
                export.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
                return true;
            },
            cancellationToken);

    private async Task<ScenarioMeasurement> MeasurePreviewAsync(QueryFixture fixture, CancellationToken cancellationToken)
        => await MeasureAsync(
            "preview: approved-time export readiness full cursor",
            async ct =>
            {
                ApprovedTimePreviewResult result = await fixture.ExportService
                    .PreviewAsync(
                        Context(),
                        new PreviewApprovedTimeExport { LedgerQuery = LedgerQuery() with { PageSize = 25 } },
                        RequestedAtUtc(),
                        ct)
                    .ConfigureAwait(false);

                result.WasDisclosed.ShouldBeTrue("export preview should disclose readiness.");
                ApprovedTimeExportPreviewReadModel preview = result.Preview.ShouldNotBeNull();
                preview.IsReady.ShouldBeTrue("export preview should be ready.");
                preview.Scope.RowCount.ShouldBe(60);
                preview.Audit.GeneratedAtUtc.ShouldBeNull();
                preview.Audit.OutputContentHashSha256.ShouldBeNull();
                preview.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
                return true;
            },
            cancellationToken);

    private async Task<ScenarioMeasurement> MeasureDashboardAsync(QueryFixture fixture, CancellationToken cancellationToken)
        => await MeasureAsync(
            "dashboard: overview composed read fan-out",
            async ct =>
            {
                TimesheetsDashboardOverviewQueryResult result = await fixture.DashboardService
                    .QueryAsync(
                        Context(),
                        new QueryTimesheetsDashboardOverview
                        {
                            TenantLocalPeriodKey = "2026-06",
                            ServiceDateFrom = new DateOnly(2026, 6, 1),
                            ServiceDateTo = new DateOnly(2026, 6, 30)
                        },
                        ct)
                    .ConfigureAwait(false);

                result.WasDisclosed.ShouldBeTrue("dashboard should disclose the overview.");
                TimesheetsDashboardOverviewReadModel overview = result.Overview.ShouldNotBeNull();
                overview.CurrentPeriod.EntryCount.ShouldBe(30);
                overview.ApprovalWorkload.SubmittedEntryCount.ShouldBe(0);
                overview.LedgerReadiness.DisclosedApprovedRowCount.ShouldBe(30);
                overview.LedgerReadiness.ExportReadiness.ShouldBe(ApprovedTimeExportReadinessState.Ready);
                overview.ReportShortcuts.Count.ShouldBe(3);
                overview.ProjectionStatuses.ShouldAllBe(static status =>
                    status.Freshness.State == ProjectionFreshnessState.Fresh);
                return true;
            },
            cancellationToken);

    private async Task<ScenarioMeasurement> MeasureAsync(
        string scenario,
        Func<CancellationToken, ValueTask<bool>> query,
        CancellationToken cancellationToken)
    {
        for (int iteration = 0; iteration < WarmupIterations; iteration++)
        {
            (await query(cancellationToken).ConfigureAwait(false)).ShouldBeTrue(
                $"{scenario} warm-up result was not disclosed.");
        }

        double[] durations = new double[MeasuredIterations];
        for (int iteration = 0; iteration < MeasuredIterations; iteration++)
        {
            long start = Stopwatch.GetTimestamp();
            bool disclosed = await query(cancellationToken).ConfigureAwait(false);
            double elapsedMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

            disclosed.ShouldBeTrue($"{scenario} measured result was not disclosed.");
            durations[iteration] = elapsedMilliseconds;
        }

        Array.Sort(durations);

        return new ScenarioMeasurement(
            scenario,
            PerformanceStatistics.NearestRankPercentile(durations, 0.95),
            durations[0],
            PerformanceStatistics.NearestRankPercentile(durations, 0.50),
            durations[^1]);
    }

    private void ReportEvidence(IReadOnlyList<ScenarioMeasurement> measurements)
    {
        _output.WriteLine(
            $"Report/export/dashboard performance lane (warm-up {WarmupIterations}, measured {MeasuredIterations} iterations per scenario).");
        _output.WriteLine("NFR11 target: 2s p95 in a warmed in-process query/report/export/dashboard path.");
        _output.WriteLine("Scenario | p95 (ms) | min (ms) | median (ms) | max (ms)");

        foreach (ScenarioMeasurement measurement in measurements)
        {
            _output.WriteLine(
                $"{measurement.Scenario} | {measurement.P95Milliseconds:F4} | {measurement.MinMilliseconds:F4} | {measurement.MedianMilliseconds:F4} | {measurement.MaxMilliseconds:F4}");
        }

        ScenarioMeasurement worst = measurements.MaxBy(static measurement => measurement.P95Milliseconds)!;
        _output.WriteLine(
            $"Worst-case report/export/dashboard query p95: {worst.Scenario} at {worst.P95Milliseconds:F4} ms (NFR11 target 2s p95).");
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
                ? TimeEntryTargetReference.ForProject(Project($"project-{targetNumber}"))
                : TimeEntryTargetReference.ForWork(Work($"work-{targetNumber}"));

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
            Contributor(),
            ActivityType(),
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

    private static ProjectReference Project(string value = "project-1") => new(value);

    private static WorkReference Work(string value = "work-1") => new(value);

    private static PartyReference Contributor() => new("party-1");

    private static ActivityTypeId ActivityType() => new("activity-type-1");

    private sealed record ScenarioMeasurement(
        string Scenario,
        double P95Milliseconds,
        double MinMilliseconds,
        double MedianMilliseconds,
        double MaxMilliseconds);

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

        public static QueryFixture Create()
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

            AllowAllAccessGuard guard = new();
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
