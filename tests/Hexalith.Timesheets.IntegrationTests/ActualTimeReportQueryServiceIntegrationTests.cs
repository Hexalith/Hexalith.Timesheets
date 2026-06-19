using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Projections.OperationalReports;
using Hexalith.Timesheets.Projections.TimeEntries;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.OperationalReports;
using Hexalith.Timesheets.Server.TimeEntries;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed class ActualTimeReportQueryServiceIntegrationTests
{
    [Fact]
    public async Task Seeded_project_report_can_be_queried_authorized_hydrated_and_drilled_into_evidence()
    {
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded("time-entry-1", 60, ProjectTarget("project-1"))),
            Event("m2", 2, Submitted("time-entry-1")),
            Event("m3", 3, Approved("time-entry-1", "decision-1"))
        ];
        TimesheetsProjectionCheckpoint checkpoint = new(
            "tenant-1",
            ActualTimeReportProjection.ProjectionName,
            3,
            ProjectionFreshness.Fresh);
        ActualTimeReportQueryService service = new(
            new AllowAllAccessGuard(),
            new SeededReportReader(new ActualTimeReportProjection(), events, checkpoint),
            new StaticHydrationProvider(),
            new StaticHydrationProvider(),
            new StaticHydrationProvider(),
            new StaticHydrationProvider(),
            new StaticPlannedEffortProvider());

        ActualTimeReportQueryResult result = await service.QueryProjectAsync(
            Context(),
            new QueryProjectActualTimeReport
            {
                Project = new ProjectReference("project-1"),
                TenantLocalPeriodKey = "2026-06",
                ApprovalState = TimeEntryApprovalState.Approved
            },
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeTrue();
        ActualTimeReportRowReadModel row = result.Page.ShouldNotBeNull().Items.ShouldHaveSingleItem();
        row.Target.TargetKind.ShouldBe(TimeEntryTargetKind.Project);
        row.Target.TargetId.ShouldBe("project-1");
        row.ActualMinutes.ShouldBe(60);
        row.WorkPlannedEffort.ShouldBeNull();
        row.DisplayHydration.Target.Label.ShouldBe("Target");
        row.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);

        TimeEntryEvidenceReadModel? drillIn = new TimeEntryEvidenceProjection().Project(
            "tenant-1",
            new TimeEntryId("time-entry-1"),
            events,
            new("tenant-1", TimeEntryEvidenceProjection.ProjectionName, 3, ProjectionFreshness.Fresh));

        drillIn.ShouldNotBeNull();
        drillIn.Target.TargetKind.ShouldBe(TimeEntryTargetKind.Project);
        drillIn.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved);
    }

    [Fact]
    public async Task Seeded_work_report_can_be_queried_authorized_hydrated_paged_and_attributed_to_works_effort()
    {
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded("time-entry-1", 30, WorkTarget("work-1"))),
            Event("m2", 2, Submitted("time-entry-1")),
            Event("m3", 3, Approved("time-entry-1", "decision-1")),
            Event("m4", 4, Recorded("time-entry-2", 45, WorkTarget("work-2"))),
            Event("m5", 5, Submitted("time-entry-2")),
            Event("m6", 6, Approved("time-entry-2", "decision-2"))
        ];
        TimesheetsProjectionCheckpoint checkpoint = new(
            "tenant-1",
            ActualTimeReportProjection.ProjectionName,
            6,
            ProjectionFreshness.Fresh);
        ActualTimeReportQueryService service = new(
            new AllowAllAccessGuard(),
            new SeededReportReader(new ActualTimeReportProjection(), events, checkpoint),
            new StaticHydrationProvider(),
            new StaticHydrationProvider(),
            new StaticHydrationProvider(),
            new StaticHydrationProvider(),
            new StaticPlannedEffortProvider());

        ActualTimeReportQueryResult firstPage = await service.QueryWorkAsync(
            Context(),
            new QueryWorkActualTimeReport
            {
                SortBy = ActualTimeReportSortBy.TargetReference,
                PageSize = 1
            },
            TestContext.Current.CancellationToken);

        firstPage.WasDisclosed.ShouldBeTrue();
        ActualTimeReportReadModel first = firstPage.Page.ShouldNotBeNull();
        ActualTimeReportRowReadModel firstRow = first.Items.ShouldHaveSingleItem();
        firstRow.Target.TargetId.ShouldBe("work-1");
        firstRow.ActualMinutes.ShouldBe(30);
        firstRow.DisplayHydration.Target.Label.ShouldBe("Target");
        firstRow.WorkPlannedEffort.ShouldNotBeNull().SourceModuleName.ShouldBe("Works");
        firstRow.WorkPlannedEffort.Availability.ShouldBe(WorkPlannedEffortAvailability.Supplied);
        first.NextCursor.ShouldNotBeNull();

        ActualTimeReportQueryResult secondPage = await service.QueryWorkAsync(
            Context(),
            new QueryWorkActualTimeReport
            {
                SortBy = ActualTimeReportSortBy.TargetReference,
                PageSize = 1,
                Cursor = first.NextCursor
            },
            TestContext.Current.CancellationToken);

        secondPage.WasDisclosed.ShouldBeTrue();
        ActualTimeReportRowReadModel secondRow = secondPage.Page.ShouldNotBeNull().Items.ShouldHaveSingleItem();
        secondRow.Target.TargetId.ShouldBe("work-2");
        secondRow.ActualMinutes.ShouldBe(45);

        TimeEntryEvidenceReadModel? drillIn = new TimeEntryEvidenceProjection().Project(
            "tenant-1",
            new TimeEntryId("time-entry-1"),
            events,
            new("tenant-1", TimeEntryEvidenceProjection.ProjectionName, 6, ProjectionFreshness.Fresh));

        drillIn.ShouldNotBeNull();
        drillIn.Target.TargetKind.ShouldBe(TimeEntryTargetKind.Work);
        drillIn.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved);
    }

    [Fact]
    public async Task Seeded_ai_work_report_preserves_units_authorization_hydration_freshness_and_unavailable_tokens()
    {
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded("human-entry", 45, WorkTarget("work-1"))),
            Event("m2", 2, Submitted("human-entry")),
            Event("m3", 3, Approved("human-entry", "decision-human")),
            Event("m4", 4, Recorded(
                "ai-entry-1",
                15,
                WorkTarget("work-1"),
                ContributorCategory.AutomatedAgent,
                NotReportedTokenMetrics())),
            Event("m5", 5, Submitted("ai-entry-1")),
            Event("m6", 6, Approved("ai-entry-1", "decision-1"))
        ];
        TimesheetsProjectionCheckpoint checkpoint = new(
            "tenant-1",
            ActualTimeReportProjection.ProjectionName,
            6,
            ProjectionFreshness.Fresh);
        ActualTimeReportQueryService service = new(
            new AllowAllAccessGuard(),
            new SeededReportReader(new ActualTimeReportProjection(), events, checkpoint),
            new StaticHydrationProvider(),
            new StaticHydrationProvider(),
            new StaticHydrationProvider(),
            new StaticHydrationProvider(),
            new StaticPlannedEffortProvider());

        ActualTimeReportQueryResult result = await service.QueryWorkAsync(
            Context(),
            new QueryWorkActualTimeReport
            {
                Work = new WorkReference("work-1"),
                AiAgent = new PartyReference("party-1"),
                AiTokenAvailability = AiTokenMetricAvailability.NotReported,
                PageSize = 10
            },
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeTrue();
        ActualTimeReportRowReadModel row = result.Page.ShouldNotBeNull().Items.ShouldHaveSingleItem();
        row.ContributorCategory.ShouldBe(ContributorCategory.AutomatedAgent);
        row.ActualMinutes.ShouldBe(0);
        row.AiWallClockDurationMilliseconds.ShouldBe(90_000);
        row.AiModelRuntimeMilliseconds.ShouldBe(75_000);
        row.AiBillableEffortMinutes.ShouldBe(2);
        row.AiProviderTotalTokenCount.ShouldBeNull();
        row.AiTokenAvailability.ShouldBe(AiTokenMetricAvailability.NotReported);
        row.DisplayHydration.Contributor.Label.ShouldBe("Contributor");
        row.WorkPlannedEffort.ShouldNotBeNull().SourceModuleName.ShouldBe("Works");
        row.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        result.Page.NextCursor.ShouldBeNull();

        TimeEntryEvidenceReadModel? drillIn = new TimeEntryEvidenceProjection().Project(
            "tenant-1",
            new TimeEntryId("ai-entry-1"),
            events,
            new("tenant-1", TimeEntryEvidenceProjection.ProjectionName, 6, ProjectionFreshness.Fresh));

        drillIn.ShouldNotBeNull();
        drillIn.AiMetrics.ShouldNotBeNull().TokenAvailability.ShouldBe(AiTokenMetricAvailability.NotReported);
        drillIn.AiMetrics.ProviderTotalTokenCount.ShouldBeNull();
    }

    private static TimeEntryProjectionEvent Event(string messageId, long sequenceNumber, object payload)
        => new(messageId, sequenceNumber, payload);

    private static TimeEntryRecorded Recorded(
        string id,
        int durationMinutes,
        TimeEntryTargetReference target,
        ContributorCategory contributorCategory = ContributorCategory.Employee,
        AiEffortMetrics? aiMetrics = null)
        => new(
            new TimeEntryId(id),
            target,
            Contributor(),
            ActivityType(),
            ActivityTypeScope.Tenant,
            new DateOnly(2026, 6, 19),
            durationMinutes,
            BillableState.Billable,
            TimeEntryApprovalState.Draft,
            contributorCategory,
            aiMetrics ?? AiEffortMetrics.Unavailable);

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
            ApprovalAuthoritySource.WorkOwner,
            ApprovalAuthorityDecisionState.Allowed,
            "timesheets.approval-authority.v1",
            "v1",
            ProjectionFreshnessMetadata.Fresh);

    private static TimeEntryTargetReference WorkTarget(string workId)
        => TimeEntryTargetReference.ForWork(new WorkReference(workId));

    private static TimeEntryTargetReference ProjectTarget(string projectId)
        => TimeEntryTargetReference.ForProject(new ProjectReference(projectId));

    private static PartyReference Contributor() => new("party-1");

    private static ActivityTypeId ActivityType() => new("activity-type-1");

    private static AiEffortMetrics NotReportedTokenMetrics()
        => new(
            AiMetricAvailability.ProviderReported,
            90_000,
            75_000,
            2,
            null,
            null,
            null,
            AiEffortMetricSourceMetadata.Provider("provider-a", "tool-a", "run-1"),
            AiTokenMetricAvailability.NotReported);

    private static TimesheetsRequestContext Context()
        => new(new TenantReference("tenant-1"), new PartyReference("operator-1"), "correlation-1");

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

    private sealed class StaticHydrationProvider :
        IPartyDisplayHydrationProvider,
        IProjectDisplayHydrationProvider,
        IWorkDisplayHydrationProvider,
        IActivityTypeDisplayHydrationProvider
    {
        public ValueTask<TimeEntryHydratedDisplayLabel> HydrateContributorAsync(
            TimesheetsRequestContext context,
            PartyReference contributor,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(TimeEntryHydratedDisplayLabel.Fresh("Contributor"));

        public ValueTask<TimeEntryHydratedDisplayLabel> HydrateProjectAsync(
            TimesheetsRequestContext context,
            ProjectReference project,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(TimeEntryHydratedDisplayLabel.Fresh("Target"));

        public ValueTask<TimeEntryHydratedDisplayLabel> HydrateWorkAsync(
            TimesheetsRequestContext context,
            WorkReference work,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(TimeEntryHydratedDisplayLabel.Fresh("Target"));

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
            => ValueTask.FromResult(TimesheetsUiActionPolicyOutcome.FromDecision(
                request.UiAction.GetValueOrDefault(),
                TimesheetsAuthorizationDecision.Allowed(),
                deniedVisibility));
    }
}
