using Hexalith.Timesheets.Contracts.Commands.Exports;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Projections.ApprovedTimeLedger;
using Hexalith.Timesheets.Projections.TimeEntries;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.Exports;
using Hexalith.Timesheets.Server.TimeEntries;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed class ApprovedTimeExportIntegrationTests
{
    [Fact]
    public async Task Seeded_approved_billable_project_and_work_rows_export_with_freshness_lineage_and_no_empty_file()
    {
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded("time-entry-1", TimeEntryTargetReference.ForProject(Project()), 30)),
            Event("m2", 2, Submitted("time-entry-1")),
            Event("m3", 3, Approved("time-entry-1", "decision-1")),
            Event("m4", 4, Recorded("time-entry-2", TimeEntryTargetReference.ForWork(Work()), 45)),
            Event("m5", 5, Submitted("time-entry-2")),
            Event("m6", 6, Approved("time-entry-2", "decision-2")),
            Event("m7", 7, ApprovedCorrected("time-entry-2", TimeEntryTargetReference.ForWork(Work()), 60))
        ];
        ApprovedTimeLedgerProjection ledgerProjection = new();
        TimesheetsProjectionCheckpoint checkpoint = new(
            "tenant-1",
            ApprovedTimeLedgerProjection.ProjectionName,
            7,
            ProjectionFreshness.Fresh);
        ApprovedTimeExportService service = new(
            new AllowAllAccessGuard(),
            new ApprovedTimeLedgerQueryService(
                new AllowAllAccessGuard(),
                new SeededLedgerReader(ledgerProjection, events, checkpoint),
                new StaticHydrator()));

        GenerateApprovedTimeExport command = new()
        {
            LedgerQuery = new QueryApprovedTimeLedger
            {
                BillableState = BillableState.Billable,
                CurrentRowsOnly = false,
                IncludeSupersededRows = true,
                SortBy = TimeEntryQuerySortBy.TimeEntryId,
                PageSize = 50
            }
        };

        ApprovedTimeExportResult result = await service.GenerateAsync(
            Context(),
            command,
            new DateTimeOffset(2026, 6, 19, 15, 0, 0, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        result.WasGenerated.ShouldBeTrue();
        ApprovedTimeExportReadModel export = result.Export.ShouldNotBeNull();
        export.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        export.Audit.Requester.ShouldBe(new PartyReference("operator-1"));
        export.Audit.CorrelationId.ShouldBe("correlation-1");
        export.Scope.RowCount.ShouldBe(3);
        export.Rows.Select(static row => row.TimeEntryId.Value).ShouldBe(["time-entry-1", "time-entry-2", "time-entry-2"]);
        export.Rows.Count(static row => row.RowState == ApprovedTimeLedgerRowState.Superseded).ShouldBe(1);
        export.Rows.Any(static row => row.ApprovedCorrection is not null).ShouldBeTrue();
        export.CsvContent.ShouldNotBeNull().ShouldContain("time-entry-1");
        export.CsvContent.ShouldNotBeNull().ShouldContain("time-entry-2");
        export.CsvContent.ShouldNotBeNull().ShouldContain("TimeEntryApproved");

        GenerateApprovedTimeExport noResultsCommand = command with
        {
            LedgerQuery = command.LedgerQuery with
            {
                Contributor = new PartyReference("missing-party")
            }
        };

        ApprovedTimeExportResult noResults = await service.GenerateAsync(
            Context(),
            noResultsCommand,
            new DateTimeOffset(2026, 6, 19, 15, 0, 0, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        noResults.WasGenerated.ShouldBeFalse();
        ApprovedTimeExportReadModel blocked = noResults.Export.ShouldNotBeNull();
        blocked.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Blocked);
        blocked.CsvContent.ShouldBeNull();
        blocked.Audit.BlockedReason.ShouldBe("No approved ledger rows are available for export preview.");
    }

    [Fact]
    public async Task Export_scope_respects_work_contributor_activity_period_date_and_billable_filters()
    {
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded("project-entry", TimeEntryTargetReference.ForProject(Project()), 30)),
            Event("m2", 2, Submitted("project-entry")),
            Event("m3", 3, Approved("project-entry", "decision-1")),
            Event(
                "m4",
                4,
                Recorded(
                    "matching-work-entry",
                    TimeEntryTargetReference.ForWork(Work()),
                    45,
                    new PartyReference("party-2"),
                    new ActivityTypeId("activity-type-2"),
                    new DateOnly(2026, 7, 15),
                    BillableState.Billable)),
            Event("m5", 5, Submitted("matching-work-entry")),
            Event("m6", 6, Approved("matching-work-entry", "decision-2")),
            Event(
                "m7",
                7,
                Recorded(
                    "wrong-contributor-entry",
                    TimeEntryTargetReference.ForWork(Work()),
                    60,
                    new PartyReference("party-3"),
                    new ActivityTypeId("activity-type-2"),
                    new DateOnly(2026, 7, 16),
                    BillableState.Billable)),
            Event("m8", 8, Submitted("wrong-contributor-entry")),
            Event("m9", 9, Approved("wrong-contributor-entry", "decision-3")),
            Event(
                "m10",
                10,
                Recorded(
                    "wrong-activity-entry",
                    TimeEntryTargetReference.ForWork(Work()),
                    75,
                    new PartyReference("party-2"),
                    new ActivityTypeId("activity-type-3"),
                    new DateOnly(2026, 7, 17),
                    BillableState.Billable)),
            Event("m11", 11, Submitted("wrong-activity-entry")),
            Event("m12", 12, Approved("wrong-activity-entry", "decision-4")),
            Event(
                "m13",
                13,
                Recorded(
                    "non-billable-entry",
                    TimeEntryTargetReference.ForWork(Work()),
                    90,
                    new PartyReference("party-2"),
                    new ActivityTypeId("activity-type-2"),
                    new DateOnly(2026, 7, 18),
                    BillableState.NonBillable)),
            Event("m14", 14, Submitted("non-billable-entry")),
            Event("m15", 15, Approved("non-billable-entry", "decision-5")),
            Event(
                "m16",
                16,
                Recorded(
                    "out-of-period-entry",
                    TimeEntryTargetReference.ForWork(Work()),
                    105,
                    new PartyReference("party-2"),
                    new ActivityTypeId("activity-type-2"),
                    new DateOnly(2026, 8, 1),
                    BillableState.Billable)),
            Event("m17", 17, Submitted("out-of-period-entry")),
            Event("m18", 18, Approved("out-of-period-entry", "decision-6"))
        ];
        ApprovedTimeLedgerProjection ledgerProjection = new();
        ApprovedTimeExportService service = new(
            new AllowAllAccessGuard(),
            new ApprovedTimeLedgerQueryService(
                new AllowAllAccessGuard(),
                new SeededLedgerReader(
                    ledgerProjection,
                    events,
                    new("tenant-1", ApprovedTimeLedgerProjection.ProjectionName, 18, ProjectionFreshness.Fresh)),
                new StaticHydrator()));

        GenerateApprovedTimeExport command = new()
        {
            LedgerQuery = new QueryApprovedTimeLedger
            {
                Work = Work(),
                Contributor = new PartyReference("party-2"),
                ActivityTypeId = new ActivityTypeId("activity-type-2"),
                TenantLocalPeriodKey = "2026-07",
                ServiceDateFrom = new DateOnly(2026, 7, 1),
                ServiceDateTo = new DateOnly(2026, 7, 31),
                BillableState = BillableState.Billable,
                SortBy = TimeEntryQuerySortBy.TimeEntryId,
                PageSize = 50
            }
        };

        ApprovedTimeExportResult result = await service.GenerateAsync(
            Context(),
            command,
            new DateTimeOffset(2026, 6, 19, 15, 0, 0, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        result.WasGenerated.ShouldBeTrue();
        ApprovedTimeExportReadModel export = result.Export.ShouldNotBeNull();
        ApprovedTimeExportRowReadModel row = export.Rows.ShouldHaveSingleItem();
        row.TimeEntryId.ShouldBe(new TimeEntryId("matching-work-entry"));
        row.Target.TargetKind.ShouldBe(TimeEntryTargetKind.Work);
        row.Target.TargetId.ShouldBe("work-1");
        row.Contributor.ShouldBe(new PartyReference("party-2"));
        row.ActivityTypeId.ShouldBe(new ActivityTypeId("activity-type-2"));
        row.ServiceDate.ShouldBe(new DateOnly(2026, 7, 15));
        row.BillableState.ShouldBe(BillableState.Billable);
        export.Scope.Filters.Work.ShouldBe(Work());
        export.Audit.Filters.TenantLocalPeriodKey.ShouldBe("2026-07");
        export.CsvContent.ShouldNotBeNull().ShouldContain("matching-work-entry");
        export.CsvContent.ShouldNotBeNull().ShouldNotContain("project-entry");
        export.CsvContent.ShouldNotBeNull().ShouldNotContain("wrong-contributor-entry");
        export.CsvContent.ShouldNotBeNull().ShouldNotContain("wrong-activity-entry");
        export.CsvContent.ShouldNotBeNull().ShouldNotContain("non-billable-entry");
        export.CsvContent.ShouldNotBeNull().ShouldNotContain("out-of-period-entry");
    }

    private static TimeEntryProjectionEvent Event(string messageId, long sequenceNumber, object payload)
        => new(messageId, sequenceNumber, payload);

    private static TimeEntryRecorded Recorded(
        string id,
        TimeEntryTargetReference target,
        int durationMinutes,
        PartyReference? contributor = null,
        ActivityTypeId? activityType = null,
        DateOnly? serviceDate = null,
        BillableState billableState = BillableState.Billable)
        => new(
            new TimeEntryId(id),
            target,
            contributor ?? Contributor(),
            activityType ?? ActivityType(),
            ActivityTypeScope.Tenant,
            serviceDate ?? new DateOnly(2026, 6, 19),
            durationMinutes,
            billableState,
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

    private static TimeEntryApprovedCorrected ApprovedCorrected(
        string id,
        TimeEntryTargetReference target,
        int durationMinutes)
        => new(
            new TimeEntryId(id),
            new TimeEntryCorrectionId("approved-correction-1"),
            new TenantReference("tenant-1"),
            new PartyReference("operator-1"),
            new DateTimeOffset(2026, 6, 20, 9, 30, 0, TimeSpan.Zero),
            Values(target, 45),
            Values(target, durationMinutes),
            new TimeEntryCorrectionReason("Correct approved duration after audit review."),
            new TimeEntryApprovalDecisionId("decision-2"),
            TimeEntryApprovalScope.IndividualEntry,
            TimeEntryApprovalState.Approved,
            TimeEntryCorrectionState.Corrected);

    private static TimeEntryCorrectionValues Values(TimeEntryTargetReference target, int durationMinutes)
        => new(
            target,
            Contributor(),
            ActivityType(),
            new DateOnly(2026, 6, 19),
            durationMinutes,
            BillableState.Billable,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable);

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

    private static ProjectReference Project() => new("project-1");

    private static WorkReference Work() => new("work-1");

    private static PartyReference Contributor() => new("party-1");

    private static ActivityTypeId ActivityType() => new("activity-type-1");

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

    private sealed class StaticHydrator : ITimeEntryDisplayHydrator
    {
        public ValueTask<TimeEntryDisplayHydration> HydrateAsync(
            TimesheetsRequestContext context,
            TimeEntryEvidenceReadModel evidence,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(new TimeEntryDisplayHydration(
                TimeEntryHydratedDisplayLabel.Fresh("Contributor"),
                TimeEntryHydratedDisplayLabel.Fresh("Target"),
                TimeEntryHydratedDisplayLabel.Fresh("Activity Type")));
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
