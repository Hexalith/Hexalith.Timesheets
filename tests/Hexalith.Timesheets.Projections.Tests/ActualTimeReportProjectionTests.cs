using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Projections.OperationalReports;
using Hexalith.Timesheets.Projections.TimeEntries;

using Shouldly;

namespace Hexalith.Timesheets.Projections.Tests;

public sealed class ActualTimeReportProjectionTests
{
    [Fact]
    public void Project_report_rolls_up_approved_actuals_by_required_dimensions_without_replayed_duplicates()
    {
        TimeEntryProjectionEvent duplicateApproval = Event("m6", 6, Approved("time-entry-2", "decision-2"));
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded("time-entry-1", 45, ProjectTarget())),
            Event("m2", 2, Submitted("time-entry-1")),
            Event("m3", 3, Approved("time-entry-1", "decision-1")),
            Event("m4", 4, Recorded("time-entry-2", 30, ProjectTarget())),
            Event("m5", 5, Submitted("time-entry-2")),
            duplicateApproval,
            duplicateApproval,
            Event("m7", 7, Recorded("draft-entry", 20, ProjectTarget()))
        ];

        ActualTimeReportReadModel page = Projector().ProjectByProject(
            "tenant-1",
            events,
            FreshCheckpoint(7),
            new QueryProjectActualTimeReport
            {
                Project = Project(),
                ApprovalState = TimeEntryApprovalState.Approved
            });

        ActualTimeReportRowReadModel row = page.Items.ShouldHaveSingleItem();
        row.Target.ShouldBe(ProjectTarget());
        row.TenantLocalPeriodKey.ShouldBe("2026-06");
        row.Contributor.ShouldBe(Contributor());
        row.ActivityTypeId.ShouldBe(Activity());
        row.BillableState.ShouldBe(BillableState.Billable);
        row.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved);
        row.ContributorCategory.ShouldBe(ContributorCategory.Employee);
        row.ActualMinutes.ShouldBe(75);
        row.SourceRowCount.ShouldBe(2);
        row.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        row.ReferenceState.State.ShouldBe(ActualTimeReferenceState.Current);
        row.WorkPlannedEffort.ShouldBeNull();
    }

    [Fact]
    public void Work_report_includes_planned_effort_placeholder_and_superseded_rows_only_when_requested()
    {
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded("time-entry-1", 45, WorkTarget())),
            Event("m2", 2, Submitted("time-entry-1")),
            Event("m3", 3, Approved("time-entry-1")),
            Event("m4", 4, ApprovedCorrected("time-entry-1", 75, WorkTarget()))
        ];

        ActualTimeReportReadModel currentOnly = Projector().ProjectByWork(
            "tenant-1",
            events,
            FreshCheckpoint(4),
            new QueryWorkActualTimeReport
            {
                Work = Work(),
                ApprovalState = TimeEntryApprovalState.Approved
            });

        ActualTimeReportRowReadModel current = currentOnly.Items.ShouldHaveSingleItem();
        current.ActualMinutes.ShouldBe(75);
        current.SourceRowCount.ShouldBe(1);
        current.SupersededCount.ShouldBe(0);
        current.WorkPlannedEffort.ShouldNotBeNull().Availability.ShouldBe(WorkPlannedEffortAvailability.NotSupplied);

        ActualTimeReportReadModel includingSuperseded = Projector().ProjectByWork(
            "tenant-1",
            events,
            FreshCheckpoint(4),
            new QueryWorkActualTimeReport
            {
                Work = Work(),
                ApprovalState = TimeEntryApprovalState.Approved,
                CurrentRowsOnly = false,
                IncludeSupersededRows = true
            });

        ActualTimeReportRowReadModel row = includingSuperseded.Items.ShouldHaveSingleItem();
        row.ActualMinutes.ShouldBe(120);
        row.SourceRowCount.ShouldBe(2);
        row.CorrectionCount.ShouldBe(2);
        row.SupersededCount.ShouldBe(1);
        row.RowState.ShouldBe(ActualTimeReportRowState.IncludesSuperseded);
    }

    [Fact]
    public void Report_combines_selected_non_approved_rows_with_approved_ledger_rows_when_no_approval_filter_is_set()
    {
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded("submitted-entry", 25, ProjectTarget())),
            Event("m2", 2, Submitted("submitted-entry")),
            Event("m3", 3, Recorded("approved-entry", 35, ProjectTarget())),
            Event("m4", 4, Submitted("approved-entry", "submission-2")),
            Event("m5", 5, Approved("approved-entry"))
        ];

        ActualTimeReportReadModel page = Projector().ProjectByProject(
            "tenant-1",
            events,
            FreshCheckpoint(5),
            new QueryProjectActualTimeReport
            {
                Project = Project(),
                SortBy = ActualTimeReportSortBy.ActualMinutes
            });

        page.Items.Select(static row => row.ApprovalState)
            .ShouldBe([TimeEntryApprovalState.Submitted, TimeEntryApprovalState.Approved]);
        page.Items.Select(static row => row.ActualMinutes).ShouldBe([25, 35]);
    }

    [Fact]
    public void Report_rolls_up_ai_effort_units_without_merging_them_into_actual_minutes()
    {
        TimeEntryProjectionEvent duplicateApproval = Event("m6", 6, Approved("ai-entry-1", "decision-2"));
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded("human-entry", 60, ProjectTarget())),
            Event("m2", 2, Submitted("human-entry")),
            Event("m3", 3, Approved("human-entry", "decision-human")),
            Event("m4", 4, Recorded(
                "ai-entry-1",
                15,
                ProjectTarget(),
                contributorCategory: ContributorCategory.AutomatedAgent,
                aiMetrics: ProviderMetrics(90_000, 75_000, 2, 10, 20, 30))),
            Event("m5", 5, Submitted("ai-entry-1", "submission-ai-1")),
            duplicateApproval,
            duplicateApproval
        ];

        ActualTimeReportReadModel page = Projector().ProjectByProject(
            "tenant-1",
            events,
            FreshCheckpoint(6),
            new QueryProjectActualTimeReport
            {
                Project = Project(),
                ApprovalState = TimeEntryApprovalState.Approved,
                SortBy = ActualTimeReportSortBy.ActualMinutes
            });

        page.Items.Count.ShouldBe(2);
        ActualTimeReportRowReadModel aiRow = page.Items.Single(static row => row.ContributorCategory == ContributorCategory.AutomatedAgent);
        aiRow.ActualMinutes.ShouldBe(0);
        aiRow.AiWallClockDurationMilliseconds.ShouldBe(90_000);
        aiRow.AiModelRuntimeMilliseconds.ShouldBe(75_000);
        aiRow.AiBillableEffortMinutes.ShouldBe(2);
        aiRow.AiProviderInputTokenCount.ShouldBe(10);
        aiRow.AiProviderOutputTokenCount.ShouldBe(20);
        aiRow.AiProviderTotalTokenCount.ShouldBe(30);
        aiRow.AiMetricAvailability.ShouldBe(AiMetricAvailability.ProviderReported);
        aiRow.AiTokenAvailability.ShouldBe(AiTokenMetricAvailability.ProviderReported);
        aiRow.AiMetricSourceMetadata.ShouldNotBeNull().SourceCategory.ShouldBe(AiEffortMetricSourceCategory.Provider);
        aiRow.SourceRowCount.ShouldBe(1);

        ActualTimeReportRowReadModel humanRow = page.Items.Single(static row => row.ContributorCategory == ContributorCategory.Employee);
        humanRow.ActualMinutes.ShouldBe(60);
        humanRow.AiProviderTotalTokenCount.ShouldBeNull();
        humanRow.AiMetricAvailability.ShouldBe(AiMetricAvailability.Unavailable);
    }

    [Fact]
    public void Ai_report_filters_agent_availability_source_and_keeps_not_reported_tokens_null()
    {
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded(
                "ai-entry-1",
                15,
                WorkTarget(),
                contributorCategory: ContributorCategory.AutomatedAgent,
                aiMetrics: NotReportedTokenMetrics())),
            Event("m2", 2, Submitted("ai-entry-1")),
            Event("m3", 3, Approved("ai-entry-1")),
            Event("m4", 4, Recorded(
                "human-entry",
                30,
                WorkTarget(),
                contributorCategory: ContributorCategory.Employee))
        ];

        ActualTimeReportReadModel page = Projector().ProjectByWork(
            "tenant-1",
            events,
            FreshCheckpoint(4),
            new QueryWorkActualTimeReport
            {
                Work = Work(),
                AiAgent = Contributor(),
                AiMetricAvailability = AiMetricAvailability.ProviderReported,
                AiTokenAvailability = AiTokenMetricAvailability.NotReported,
                AiSourceCategory = AiEffortMetricSourceCategory.Provider
            });

        ActualTimeReportRowReadModel row = page.Items.ShouldHaveSingleItem();
        row.ContributorCategory.ShouldBe(ContributorCategory.AutomatedAgent);
        row.ActualMinutes.ShouldBe(0);
        row.AiBillableEffortMinutes.ShouldBe(2);
        row.AiProviderInputTokenCount.ShouldBeNull();
        row.AiProviderOutputTokenCount.ShouldBeNull();
        row.AiProviderTotalTokenCount.ShouldBeNull();
        row.AiTokenAvailability.ShouldBe(AiTokenMetricAvailability.NotReported);
    }

    [Fact]
    public void Ai_agent_filter_excludes_human_rows_for_the_same_party_reference()
    {
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded("human-entry", 45, WorkTarget())),
            Event("m2", 2, Submitted("human-entry")),
            Event("m3", 3, Approved("human-entry", "decision-human")),
            Event("m4", 4, Recorded(
                "ai-entry-1",
                15,
                WorkTarget(),
                contributorCategory: ContributorCategory.AutomatedAgent,
                aiMetrics: ProviderMetrics(90_000, 75_000, 2, 10, 20, 30))),
            Event("m5", 5, Submitted("ai-entry-1", "submission-ai-1")),
            Event("m6", 6, Approved("ai-entry-1", "decision-ai"))
        ];

        ActualTimeReportReadModel page = Projector().ProjectByWork(
            "tenant-1",
            events,
            FreshCheckpoint(6),
            new QueryWorkActualTimeReport
            {
                Work = Work(),
                AiAgent = Contributor()
            });

        ActualTimeReportRowReadModel row = page.Items.ShouldHaveSingleItem();
        row.Contributor.ShouldBe(Contributor());
        row.ContributorCategory.ShouldBe(ContributorCategory.AutomatedAgent);
        row.ActualMinutes.ShouldBe(0);
        row.AiProviderTotalTokenCount.ShouldBe(30);
    }

    [Fact]
    public void Approved_ai_metric_corrections_use_current_values_by_default_and_superseded_values_when_requested()
    {
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded(
                "ai-entry-1",
                15,
                WorkTarget(),
                contributorCategory: ContributorCategory.AutomatedAgent,
                aiMetrics: ProviderMetrics(90_000, 75_000, 2, 10, 20, 30))),
            Event("m2", 2, Submitted("ai-entry-1")),
            Event("m3", 3, Approved("ai-entry-1")),
            Event("m4", 4, ApprovedCorrected(
                "ai-entry-1",
                25,
                WorkTarget(),
                ProviderMetrics(90_000, 75_000, 2, 10, 20, 30),
                ProviderMetrics(120_000, 100_000, 3, 15, 25, 40)))
        ];

        ActualTimeReportReadModel currentOnly = Projector().ProjectByWork(
            "tenant-1",
            events,
            FreshCheckpoint(4),
            new QueryWorkActualTimeReport
            {
                Work = Work(),
                ApprovalState = TimeEntryApprovalState.Approved
            });

        ActualTimeReportRowReadModel current = currentOnly.Items.ShouldHaveSingleItem();
        current.ActualMinutes.ShouldBe(0);
        current.AiWallClockDurationMilliseconds.ShouldBe(120_000);
        current.AiProviderTotalTokenCount.ShouldBe(40);
        current.SourceRowCount.ShouldBe(1);

        ActualTimeReportReadModel includingSuperseded = Projector().ProjectByWork(
            "tenant-1",
            events,
            FreshCheckpoint(4),
            new QueryWorkActualTimeReport
            {
                Work = Work(),
                ApprovalState = TimeEntryApprovalState.Approved,
                CurrentRowsOnly = false,
                IncludeSupersededRows = true
            });

        ActualTimeReportRowReadModel row = includingSuperseded.Items.ShouldHaveSingleItem();
        row.ActualMinutes.ShouldBe(0);
        row.AiWallClockDurationMilliseconds.ShouldBe(210_000);
        row.AiProviderTotalTokenCount.ShouldBe(70);
        row.SourceRowCount.ShouldBe(2);
        row.SupersededCount.ShouldBe(1);
        row.RowState.ShouldBe(ActualTimeReportRowState.IncludesSuperseded);
    }

    [Fact]
    public void Mixed_provider_reported_and_not_reported_token_rows_merge_to_not_reported_regardless_of_order()
    {
        // Two automated-agent entries in the same rollup group: one provider-reported,
        // one not-reported. The aggregate token availability must be conservative and
        // order-independent, otherwise a "Not reported by provider" group could be
        // silently upgraded to "ProviderReported" by event ordering.
        ActualTimeReportRowReadModel providerApprovedFirst = MergeTokenAvailabilityRow(providerFirst: true);
        ActualTimeReportRowReadModel notReportedApprovedFirst = MergeTokenAvailabilityRow(providerFirst: false);

        providerApprovedFirst.AiTokenAvailability.ShouldBe(AiTokenMetricAvailability.NotReported);
        notReportedApprovedFirst.AiTokenAvailability.ShouldBe(AiTokenMetricAvailability.NotReported);
        providerApprovedFirst.AiProviderTotalTokenCount.ShouldBe(30);
        notReportedApprovedFirst.AiProviderTotalTokenCount.ShouldBe(30);
        providerApprovedFirst.SourceRowCount.ShouldBe(2);
        notReportedApprovedFirst.SourceRowCount.ShouldBe(2);
    }

    private static ActualTimeReportRowReadModel MergeTokenAvailabilityRow(bool providerFirst)
    {
        AiEffortMetrics providerMetrics = ProviderMetrics(90_000, 75_000, 2, 10, 20, 30);
        AiEffortMetrics notReportedMetrics = NotReportedTokenMetrics();
        (string firstId, AiEffortMetrics firstMetrics, string secondId, AiEffortMetrics secondMetrics) = providerFirst
            ? ("ai-provider", providerMetrics, "ai-not-reported", notReportedMetrics)
            : ("ai-not-reported", notReportedMetrics, "ai-provider", providerMetrics);

        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded(firstId, 15, WorkTarget(), contributorCategory: ContributorCategory.AutomatedAgent, aiMetrics: firstMetrics)),
            Event("m2", 2, Submitted(firstId, "submission-first")),
            Event("m3", 3, Approved(firstId, "decision-first")),
            Event("m4", 4, Recorded(secondId, 15, WorkTarget(), contributorCategory: ContributorCategory.AutomatedAgent, aiMetrics: secondMetrics)),
            Event("m5", 5, Submitted(secondId, "submission-second")),
            Event("m6", 6, Approved(secondId, "decision-second"))
        ];

        return Projector().ProjectByWork(
            "tenant-1",
            events,
            FreshCheckpoint(6),
            new QueryWorkActualTimeReport
            {
                Work = Work(),
                ApprovalState = TimeEntryApprovalState.Approved
            }).Items.ShouldHaveSingleItem();
    }

    [Fact]
    public void Selected_non_approved_rows_preserve_activity_type_scope_as_a_grouping_dimension()
    {
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded("tenant-scope-entry", 20, ProjectTarget(), activityTypeScope: ActivityTypeScope.Tenant)),
            Event("m2", 2, Submitted("tenant-scope-entry")),
            Event("m3", 3, Recorded("project-scope-entry", 30, ProjectTarget(), activityTypeScope: ActivityTypeScope.Project)),
            Event("m4", 4, Submitted("project-scope-entry", "submission-2"))
        ];

        ActualTimeReportReadModel page = Projector().ProjectByProject(
            "tenant-1",
            events,
            FreshCheckpoint(4),
            new QueryProjectActualTimeReport
            {
                Project = Project(),
                ApprovalState = TimeEntryApprovalState.Submitted,
                SortBy = ActualTimeReportSortBy.ActualMinutes
            });

        page.Items.Select(static row => row.ActivityTypeScope)
            .ShouldBe([ActivityTypeScope.Tenant, ActivityTypeScope.Project]);
        page.Items.Select(static row => row.ActualMinutes).ShouldBe([20, 30]);
        page.Items.Select(static row => row.SourceRowCount).ShouldBe([1, 1]);
    }

    [Fact]
    public void Report_filters_period_boundaries_date_range_billable_state_and_contributor_category()
    {
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded("may-entry", 10, ProjectTarget(), new DateOnly(2026, 5, 31))),
            Event("m2", 2, Submitted("may-entry", "submission-may")),
            Event("m3", 3, Approved("may-entry", "decision-may")),
            Event("m4", 4, Recorded("june-start", 20, ProjectTarget(), new DateOnly(2026, 6, 1))),
            Event("m5", 5, Submitted("june-start", "submission-june-start")),
            Event("m6", 6, Approved("june-start", "decision-june-start")),
            Event(
                "m7",
                7,
                Recorded(
                    "june-end",
                    30,
                    ProjectTarget(),
                    new DateOnly(2026, 6, 30),
                    BillableState.NonBillable,
                    ContributorCategory.ExternalContributor)),
            Event("m8", 8, Submitted("june-end", "submission-june-end")),
            Event("m9", 9, Approved("june-end", "decision-june-end")),
            Event("m10", 10, Recorded("july-entry", 40, ProjectTarget(), new DateOnly(2026, 7, 1))),
            Event("m11", 11, Submitted("july-entry", "submission-july")),
            Event("m12", 12, Approved("july-entry", "decision-july"))
        ];

        ActualTimeReportReadModel junePage = Projector().ProjectByProject(
            "tenant-1",
            events,
            FreshCheckpoint(12),
            new QueryProjectActualTimeReport
            {
                Project = Project(),
                TenantLocalPeriodKey = "2026-06",
                ApprovalState = TimeEntryApprovalState.Approved,
                SortBy = ActualTimeReportSortBy.ActualMinutes
            });

        junePage.Items.Select(static row => row.ActualMinutes).ShouldBe([20, 30]);
        junePage.Items.Select(static row => row.PeriodStart).Distinct().ShouldBe([new DateOnly(2026, 6, 1)]);
        junePage.Items.Select(static row => row.PeriodEnd).Distinct().ShouldBe([new DateOnly(2026, 6, 30)]);

        ActualTimeReportReadModel filteredPage = Projector().ProjectByProject(
            "tenant-1",
            events,
            FreshCheckpoint(12),
            new QueryProjectActualTimeReport
            {
                Project = Project(),
                ServiceDateFrom = new DateOnly(2026, 6, 15),
                ServiceDateTo = new DateOnly(2026, 6, 30),
                BillableState = BillableState.NonBillable,
                ContributorCategory = ContributorCategory.ExternalContributor,
                ApprovalState = TimeEntryApprovalState.Approved
            });

        ActualTimeReportRowReadModel filtered = filteredPage.Items.ShouldHaveSingleItem();
        filtered.ActualMinutes.ShouldBe(30);
        filtered.SourceRowCount.ShouldBe(1);
        filtered.BillableState.ShouldBe(BillableState.NonBillable);
        filtered.ContributorCategory.ShouldBe(ContributorCategory.ExternalContributor);
    }

    [Fact]
    public void Report_sorting_and_cursor_paging_are_deterministic()
    {
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded("time-entry-2", 20, ProjectTarget("project-2"))),
            Event("m2", 2, Submitted("time-entry-2")),
            Event("m3", 3, Approved("time-entry-2", "decision-2")),
            Event("m4", 4, Recorded("time-entry-1", 10, ProjectTarget("project-1"))),
            Event("m5", 5, Submitted("time-entry-1")),
            Event("m6", 6, Approved("time-entry-1", "decision-1"))
        ];

        ActualTimeReportReadModel firstPage = Projector().ProjectByProject(
            "tenant-1",
            events,
            FreshCheckpoint(6),
            new QueryProjectActualTimeReport
            {
                SortBy = ActualTimeReportSortBy.TargetReference,
                PageSize = 1
            });

        firstPage.Items.ShouldHaveSingleItem().Target.TargetId.ShouldBe("project-1");
        firstPage.NextCursor.ShouldNotBeNull();

        ActualTimeReportReadModel secondPage = Projector().ProjectByProject(
            "tenant-1",
            events,
            FreshCheckpoint(6),
            new QueryProjectActualTimeReport
            {
                SortBy = ActualTimeReportSortBy.TargetReference,
                PageSize = 1,
                Cursor = firstPage.NextCursor
            });

        secondPage.Items.ShouldHaveSingleItem().Target.TargetId.ShouldBe("project-2");
        secondPage.NextCursor.ShouldBeNull();
    }

    [Theory]
    [InlineData(ProjectionFreshness.Rebuilding, ProjectionFreshnessState.Rebuilding, ActualTimeReferenceState.Rebuilding)]
    [InlineData(ProjectionFreshness.Stale, ProjectionFreshnessState.Stale, ActualTimeReferenceState.Stale)]
    [InlineData(ProjectionFreshness.Unavailable, ProjectionFreshnessState.Unavailable, ActualTimeReferenceState.Unavailable)]
    public void Report_maps_projection_freshness_to_row_and_reference_state(
        ProjectionFreshness freshness,
        ProjectionFreshnessState expectedFreshness,
        ActualTimeReferenceState expectedReferenceState)
    {
        ActualTimeReportReadModel page = Projector().ProjectByProject(
            "tenant-1",
            [
                Event("m1", 1, Recorded("time-entry-1", 30, ProjectTarget())),
                Event("m2", 2, Submitted("time-entry-1")),
                Event("m3", 3, Approved("time-entry-1"))
            ],
            new("tenant-1", ActualTimeReportProjection.ProjectionName, 3, freshness),
            new QueryProjectActualTimeReport { Project = Project() });

        ActualTimeReportRowReadModel row = page.Items.ShouldHaveSingleItem();
        row.ProjectionFreshness.State.ShouldBe(expectedFreshness);
        row.ReferenceState.State.ShouldBe(expectedReferenceState);
    }

    private static ActualTimeReportProjection Projector() => new();

    private static TimeEntryProjectionEvent Event(string messageId, long sequenceNumber, object payload)
        => new(messageId, sequenceNumber, payload);

    private static TimeEntryRecorded Recorded(
        string id,
        int durationMinutes,
        TimeEntryTargetReference target,
        DateOnly? serviceDate = null,
        BillableState billableState = BillableState.Billable,
        ContributorCategory contributorCategory = ContributorCategory.Employee,
        ActivityTypeScope activityTypeScope = ActivityTypeScope.Tenant,
        AiEffortMetrics? aiMetrics = null)
        => new(
            new TimeEntryId(id),
            target,
            Contributor(),
            Activity(),
            activityTypeScope,
            serviceDate ?? new DateOnly(2026, 6, 19),
            durationMinutes,
            billableState,
            TimeEntryApprovalState.Draft,
            contributorCategory,
            aiMetrics ?? AiEffortMetrics.Unavailable)
        {
            Comment = new("Internal context.", TimeEntryCommentPolicy.SensitiveDefault)
        };

    private static TimeEntrySubmitted Submitted(string id, string submissionId = "submission-1")
        => new(
            new TimeEntryId(id),
            new PartyReference("submitter-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero),
            new TimeEntrySubmissionId(submissionId),
            TimeEntrySubmissionScope.SelectedEntries,
            TimeEntryApprovalState.Submitted);

    private static TimeEntryApproved Approved(string id, string decisionId = "decision-1")
        => new(
            new TimeEntryId(id),
            new PartyReference("approver-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            new TimeEntryApprovalDecisionId(decisionId),
            TimeEntryApprovalState.Approved,
            Authority(ApprovalAuthorityAction.EntryApproval),
            TimeEntryApprovalScope.IndividualEntry);

    private static TimeEntryApprovedCorrected ApprovedCorrected(
        string id,
        int durationMinutes,
        TimeEntryTargetReference target,
        AiEffortMetrics? priorMetrics = null,
        AiEffortMetrics? correctedMetrics = null)
        => new(
            new TimeEntryId(id),
            new TimeEntryCorrectionId("approved-correction-1"),
            new TenantReference("tenant-1"),
            new PartyReference("operator-1"),
            new DateTimeOffset(2026, 6, 20, 9, 30, 0, TimeSpan.Zero),
            Values(
                45,
                target,
                priorMetrics,
                priorMetrics is null ? ContributorCategory.Employee : ContributorCategory.AutomatedAgent),
            Values(
                durationMinutes,
                target,
                correctedMetrics,
                correctedMetrics is null ? ContributorCategory.Employee : ContributorCategory.AutomatedAgent),
            new TimeEntryCorrectionReason("Correct approved duration after audit review."),
            new TimeEntryApprovalDecisionId("decision-1"),
            TimeEntryApprovalScope.IndividualEntry,
            TimeEntryApprovalState.Approved,
            TimeEntryCorrectionState.Corrected);

    private static TimeEntryCorrectionValues Values(
        int durationMinutes,
        TimeEntryTargetReference target,
        AiEffortMetrics? aiMetrics = null,
        ContributorCategory contributorCategory = ContributorCategory.Employee)
        => new(
            target,
            Contributor(),
            Activity(),
            new DateOnly(2026, 6, 19),
            durationMinutes,
            BillableState.Billable,
            contributorCategory,
            aiMetrics ?? AiEffortMetrics.Unavailable);

    private static ApprovalAuthoritySourceAttribution Authority(ApprovalAuthorityAction action)
        => new(
            action,
            ApprovalAuthoritySource.ProjectApprover,
            ApprovalAuthorityDecisionState.Allowed,
            "timesheets.approval-authority.v1",
            "v1",
            ProjectionFreshnessMetadata.Fresh);

    private static TimesheetsProjectionCheckpoint FreshCheckpoint(long sequenceNumber)
        => new("tenant-1", ActualTimeReportProjection.ProjectionName, sequenceNumber, ProjectionFreshness.Fresh);

    private static ProjectReference Project(string projectId = "project-1") => new(projectId);

    private static WorkReference Work() => new("work-1");

    private static TimeEntryTargetReference ProjectTarget(string projectId = "project-1")
        => TimeEntryTargetReference.ForProject(Project(projectId));

    private static TimeEntryTargetReference WorkTarget()
        => TimeEntryTargetReference.ForWork(Work());

    private static PartyReference Contributor() => new("party-1");

    private static ActivityTypeId Activity() => new("activity-type-1");

    private static AiEffortMetrics ProviderMetrics(
        int wallClockMilliseconds,
        int modelRuntimeMilliseconds,
        int billableEffortMinutes,
        long inputTokens,
        long outputTokens,
        long totalTokens)
        => new(
            AiMetricAvailability.ProviderReported,
            wallClockMilliseconds,
            modelRuntimeMilliseconds,
            billableEffortMinutes,
            inputTokens,
            outputTokens,
            totalTokens,
            AiEffortMetricSourceMetadata.Provider("provider-a", "tool-a", "run-1"),
            AiTokenMetricAvailability.ProviderReported);

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
}
