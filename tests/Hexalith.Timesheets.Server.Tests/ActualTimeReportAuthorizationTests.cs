using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.OperationalReports;
using Hexalith.Timesheets.Server.TimeEntries;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class ActualTimeReportAuthorizationTests
{
    [Fact]
    public async Task Project_report_denies_tenant_before_projection_lookup()
    {
        TrackingReportReader reader = new(Page([Row(ProjectTarget())]));
        TrackingHydrationProvider hydration = new();
        TrackingPlannedEffortProvider plannedEffort = new();
        ActualTimeReportQueryService service = Service(
            new SequencedAccessGuard([
                TimesheetsAuthorizationDecision.Denied(TimesheetsDenialCategory.MissingTenant, "Tenant is missing.")
            ]),
            reader,
            hydration,
            plannedEffort);

        ActualTimeReportQueryResult result = await service.QueryProjectAsync(
            Context(),
            new QueryProjectActualTimeReport(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeFalse();
        result.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.MissingTenant);
        reader.ProjectCalls.ShouldBe(0);
        hydration.Calls.ShouldBe(0);
        plannedEffort.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Unavailable_report_reader_discloses_nothing_after_tenant_authorization()
    {
        ActualTimeReportQueryService service = Service(
            new SequencedAccessGuard([TimesheetsAuthorizationDecision.Allowed()]),
            new UnavailableActualTimeReportProjectionReader(),
            new TrackingHydrationProvider(),
            new TrackingPlannedEffortProvider());

        ActualTimeReportQueryResult result = await service.QueryWorkAsync(
            Context(),
            new QueryWorkActualTimeReport(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeFalse();
        result.Page.ShouldBeNull();
    }

    [Fact]
    public async Task Work_report_filters_insufficient_role_rows_without_hydration_or_planned_effort_lookup_for_denied_rows()
    {
        TrackingHydrationProvider hydration = new();
        TrackingPlannedEffortProvider plannedEffort = new();
        ActualTimeReportQueryService service = Service(
            new SequencedAccessGuard([
                TimesheetsAuthorizationDecision.Allowed(),
                TimesheetsAuthorizationDecision.Allowed(),
                TimesheetsAuthorizationDecision.Denied(TimesheetsDenialCategory.InsufficientRole, "No row role.")
            ]),
            new TrackingReportReader(Page([
                Row(WorkTarget("work-1")),
                Row(WorkTarget("work-2"))
            ])),
            hydration,
            plannedEffort);

        ActualTimeReportQueryResult result = await service.QueryWorkAsync(
            Context(),
            new QueryWorkActualTimeReport(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeTrue();
        ActualTimeReportReadModel page = result.Page.ShouldNotBeNull();
        ActualTimeReportRowReadModel row = page.Items.ShouldHaveSingleItem();
        row.Target.TargetId.ShouldBe("work-1");
        row.DisplayHydration.Target.Label.ShouldBe("target-label");
        row.WorkPlannedEffort.ShouldNotBeNull().Availability.ShouldBe(WorkPlannedEffortAvailability.Supplied);
        hydration.Calls.ShouldBe(3);
        plannedEffort.Calls.ShouldBe(1);
        plannedEffort.WorkIds.ShouldBe(["work-1"]);
    }

    [Theory]
    [InlineData(TimesheetsDenialCategory.CrossTenantTarget)]
    [InlineData(TimesheetsDenialCategory.StaleProjection)]
    [InlineData(TimesheetsDenialCategory.AmbiguousAuthority)]
    [InlineData(TimesheetsDenialCategory.UnavailableSiblingAuthority)]
    [InlineData(TimesheetsDenialCategory.InvalidReference)]
    [InlineData(TimesheetsDenialCategory.UnconfiguredPolicy)]
    public async Task Report_fails_closed_for_non_filterable_row_denials(TimesheetsDenialCategory category)
    {
        TrackingHydrationProvider hydration = new();
        TrackingPlannedEffortProvider plannedEffort = new();
        ActualTimeReportQueryService service = Service(
            new SequencedAccessGuard([
                TimesheetsAuthorizationDecision.Allowed(),
                TimesheetsAuthorizationDecision.Denied(category, "Unsafe row disclosure.")
            ]),
            new TrackingReportReader(Page([Row(ProjectTarget())])),
            hydration,
            plannedEffort);

        ActualTimeReportQueryResult result = await service.QueryProjectAsync(
            Context(),
            new QueryProjectActualTimeReport(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeFalse();
        result.Authorization.DenialCategory.ShouldBe(category);
        hydration.Calls.ShouldBe(0);
        plannedEffort.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Report_row_authorization_uses_project_for_project_rows_and_work_for_work_rows()
    {
        RecordingAccessGuard guard = new();
        ActualTimeReportQueryService service = Service(
            guard,
            new TrackingReportReader(
                Page([Row(ProjectTarget())]),
                Page([Row(WorkTarget())])),
            new TrackingHydrationProvider(),
            new TrackingPlannedEffortProvider());

        (await service.QueryProjectAsync(
            Context(),
            new QueryProjectActualTimeReport(),
            TestContext.Current.CancellationToken)).WasDisclosed.ShouldBeTrue();
        (await service.QueryWorkAsync(
            Context(),
            new QueryWorkActualTimeReport(),
            TestContext.Current.CancellationToken)).WasDisclosed.ShouldBeTrue();

        guard.Requests.Count.ShouldBe(4);
        guard.Requests[0].Project.ShouldBeNull();
        guard.Requests[0].Work.ShouldBeNull();
        guard.Requests[1].Project.ShouldBe(new ProjectReference("project-1"));
        guard.Requests[1].Work.ShouldBeNull();
        guard.Requests[2].Project.ShouldBeNull();
        guard.Requests[2].Work.ShouldBeNull();
        guard.Requests[3].Project.ShouldBeNull();
        guard.Requests[3].Work.ShouldBe(new WorkReference("work-1"));
    }

    [Fact]
    public async Task Ai_report_row_authorization_uses_exact_ai_agent_contributor_and_work_target()
    {
        RecordingAccessGuard guard = new();
        ActualTimeReportQueryService service = Service(
            guard,
            new TrackingReportReader(Page([Row(
                WorkTarget(),
                new PartyReference("ai-agent-1"),
                ContributorCategory.AutomatedAgent)])),
            new TrackingHydrationProvider(),
            new TrackingPlannedEffortProvider());

        ActualTimeReportQueryResult result = await service.QueryWorkAsync(
            Context(),
            new QueryWorkActualTimeReport
            {
                Work = new WorkReference("work-1"),
                AiAgent = new PartyReference("ai-agent-1")
            },
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeTrue();
        guard.Requests.Count.ShouldBe(2);
        guard.Requests[0].Contributor.ShouldBeNull();
        guard.Requests[0].Work.ShouldBeNull();
        guard.Requests[1].Contributor.ShouldBe(new PartyReference("ai-agent-1"));
        guard.Requests[1].Work.ShouldBe(new WorkReference("work-1"));
    }

    [Fact]
    public async Task Project_report_hydrates_authorized_rows_without_planned_effort_lookup()
    {
        TrackingHydrationProvider hydration = new();
        TrackingPlannedEffortProvider plannedEffort = new();
        ActualTimeReportQueryService service = Service(
            new SequencedAccessGuard([
                TimesheetsAuthorizationDecision.Allowed(),
                TimesheetsAuthorizationDecision.Allowed()
            ]),
            new TrackingReportReader(Page([Row(ProjectTarget())])),
            hydration,
            plannedEffort);

        ActualTimeReportQueryResult result = await service.QueryProjectAsync(
            Context(),
            new QueryProjectActualTimeReport(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeTrue();
        ActualTimeReportRowReadModel row = result.Page.ShouldNotBeNull().Items.ShouldHaveSingleItem();
        row.Target.TargetKind.ShouldBe(TimeEntryTargetKind.Project);
        row.WorkPlannedEffort.ShouldBeNull();
        row.DisplayHydration.Target.Label.ShouldBe("target-label");
        hydration.Calls.ShouldBe(3);
        plannedEffort.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Work_report_preserves_planned_effort_state_separately_from_actual_projection_freshness()
    {
        TrackingPlannedEffortProvider plannedEffort = new(WorkPlannedEffortReadModel.Unauthorized());
        ActualTimeReportQueryService service = Service(
            new SequencedAccessGuard([
                TimesheetsAuthorizationDecision.Allowed(),
                TimesheetsAuthorizationDecision.Allowed()
            ]),
            new TrackingReportReader(Page([Row(WorkTarget())])),
            new TrackingHydrationProvider(),
            plannedEffort);

        ActualTimeReportQueryResult result = await service.QueryWorkAsync(
            Context(),
            new QueryWorkActualTimeReport(),
            TestContext.Current.CancellationToken);

        ActualTimeReportRowReadModel row = result.Page.ShouldNotBeNull().Items.ShouldHaveSingleItem();
        row.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        row.WorkPlannedEffort.ShouldNotBeNull().Availability.ShouldBe(WorkPlannedEffortAvailability.Unauthorized);
        row.WorkPlannedEffort.SourceFreshness.State.ShouldBe(ProjectionFreshnessState.Unavailable);
        plannedEffort.Calls.ShouldBe(1);
    }

    private static ActualTimeReportQueryService Service(
        ITimesheetsAccessGuard accessGuard,
        IActualTimeReportProjectionReader reader,
        TrackingHydrationProvider hydration,
        IWorkPlannedEffortProvider plannedEffort)
        => new(
            accessGuard,
            reader,
            hydration,
            hydration,
            hydration,
            hydration,
            plannedEffort);

    private static ActualTimeReportReadModel Page(IReadOnlyList<ActualTimeReportRowReadModel> rows)
        => new(rows, null, ProjectionFreshnessMetadata.Fresh);

    private static ActualTimeReportRowReadModel Row(
        TimeEntryTargetReference target,
        PartyReference? contributor = null,
        ContributorCategory contributorCategory = ContributorCategory.Employee)
        => new(
            target,
            "2026-06",
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            contributor ?? new PartyReference("party-1"),
            new ActivityTypeId("activity-type-1"),
            ActivityTypeScope.Tenant,
            BillableState.Billable,
            TimeEntryApprovalState.Approved,
            contributorCategory,
            60,
            1,
            0,
            0,
            ActualTimeReportRowState.Current,
            ActualTimeReferenceStateMetadata.Current,
            ProjectionFreshnessMetadata.Fresh)
        {
            WorkPlannedEffort = target.TargetKind == TimeEntryTargetKind.Work
                ? WorkPlannedEffortReadModel.NotSupplied()
                : null
        };

    private static TimeEntryTargetReference ProjectTarget()
        => TimeEntryTargetReference.ForProject(new ProjectReference("project-1"));

    private static TimeEntryTargetReference WorkTarget(string workId = "work-1")
        => TimeEntryTargetReference.ForWork(new WorkReference(workId));

    private static TimesheetsRequestContext Context()
        => new(new TenantReference("tenant-1"), new PartyReference("operator-1"), "correlation-1");

    private sealed class TrackingReportReader(
        ActualTimeReportReadModel projectPage,
        ActualTimeReportReadModel? workPage = null) : IActualTimeReportProjectionReader
    {
        public int ProjectCalls { get; private set; }

        public int WorkCalls { get; private set; }

        public ValueTask<ActualTimeReportReadModel?> QueryProjectAsync(
            TimesheetsRequestContext context,
            QueryProjectActualTimeReport query,
            CancellationToken cancellationToken)
        {
            ProjectCalls++;
            return ValueTask.FromResult<ActualTimeReportReadModel?>(projectPage);
        }

        public ValueTask<ActualTimeReportReadModel?> QueryWorkAsync(
            TimesheetsRequestContext context,
            QueryWorkActualTimeReport query,
            CancellationToken cancellationToken)
        {
            WorkCalls++;
            return ValueTask.FromResult<ActualTimeReportReadModel?>(workPage ?? projectPage);
        }
    }

    private sealed class TrackingHydrationProvider :
        IPartyDisplayHydrationProvider,
        IProjectDisplayHydrationProvider,
        IWorkDisplayHydrationProvider,
        IActivityTypeDisplayHydrationProvider
    {
        public int Calls { get; private set; }

        public ValueTask<TimeEntryHydratedDisplayLabel> HydrateContributorAsync(
            TimesheetsRequestContext context,
            PartyReference contributor,
            CancellationToken cancellationToken)
            => Hydrated("contributor-label");

        public ValueTask<TimeEntryHydratedDisplayLabel> HydrateProjectAsync(
            TimesheetsRequestContext context,
            ProjectReference project,
            CancellationToken cancellationToken)
            => Hydrated("target-label");

        public ValueTask<TimeEntryHydratedDisplayLabel> HydrateWorkAsync(
            TimesheetsRequestContext context,
            WorkReference work,
            CancellationToken cancellationToken)
            => Hydrated("target-label");

        public ValueTask<TimeEntryHydratedDisplayLabel> HydrateActivityTypeAsync(
            TimesheetsRequestContext context,
            ActivityTypeId activityTypeId,
            ActivityTypeScope activityTypeScope,
            CancellationToken cancellationToken)
            => Hydrated("activity-label");

        private ValueTask<TimeEntryHydratedDisplayLabel> Hydrated(string label)
        {
            Calls++;
            return ValueTask.FromResult(TimeEntryHydratedDisplayLabel.Fresh(label));
        }
    }

    private sealed class TrackingPlannedEffortProvider(
        WorkPlannedEffortReadModel? result = null) : IWorkPlannedEffortProvider
    {
        public int Calls { get; private set; }

        public List<string> WorkIds { get; } = [];

        public ValueTask<WorkPlannedEffortReadModel> GetPlannedEffortAsync(
            TimesheetsRequestContext context,
            WorkReference work,
            CancellationToken cancellationToken)
        {
            Calls++;
            WorkIds.Add(work.WorkId);
            return ValueTask.FromResult(result ?? WorkPlannedEffortReadModel.Supplied(
                120,
                30,
                90,
                "minutes",
                ProjectionFreshnessMetadata.Fresh));
        }
    }

    private sealed class SequencedAccessGuard(IReadOnlyList<TimesheetsAuthorizationDecision> decisions) : ITimesheetsAccessGuard
    {
        private int _index;

        public ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
            TimesheetsAuthorizationRequest request,
            CancellationToken cancellationToken)
        {
            TimesheetsAuthorizationDecision decision = _index < decisions.Count
                ? decisions[_index]
                : TimesheetsAuthorizationDecision.Allowed();
            _index++;
            return ValueTask.FromResult(decision);
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
            => ValueTask.FromResult(TimesheetsUiActionPolicyOutcome.FromDecision(
                request.UiAction.GetValueOrDefault(),
                TimesheetsAuthorizationDecision.Allowed(),
                deniedVisibility));
    }

    private sealed class RecordingAccessGuard : ITimesheetsAccessGuard
    {
        public List<TimesheetsAuthorizationRequest> Requests { get; } = [];

        public ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
            TimesheetsAuthorizationRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return ValueTask.FromResult(TimesheetsAuthorizationDecision.Allowed());
        }

        public async ValueTask<TimesheetsAuthorizationDecision> ExecuteIfAuthorizedAsync(
            TimesheetsAuthorizationRequest request,
            Func<CancellationToken, ValueTask> trustedWork,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            await trustedWork(cancellationToken).ConfigureAwait(false);
            return TimesheetsAuthorizationDecision.Allowed();
        }

        public ValueTask<TimesheetsUiActionPolicyOutcome> EvaluateUiActionAsync(
            TimesheetsAuthorizationRequest request,
            TimesheetsUiActionVisibility deniedVisibility,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return ValueTask.FromResult(TimesheetsUiActionPolicyOutcome.FromDecision(
                request.UiAction.GetValueOrDefault(),
                TimesheetsAuthorizationDecision.Allowed(),
                deniedVisibility));
        }
    }
}
