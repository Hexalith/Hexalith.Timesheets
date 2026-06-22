using Hexalith.Timesheets.Contracts.Commands.Exports;
using Hexalith.Timesheets.Contracts.Events.Exports;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Exports;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Projections.ApprovedTimeLedger;
using Hexalith.Timesheets.Projections.TimeEntries;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.Exports;
using Hexalith.Timesheets.Server.TimeEntries;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed class ApprovedTimeExportPreviewIntegrationTests
{
    [Fact]
    public async Task Seeded_approved_billable_project_and_work_rows_preview_ready_with_scope_freshness_and_no_audit_event()
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
        TrackingAuditRecorder auditRecorder = new();
        ApprovedTimeExportService service = Service(events, 7, ProjectionFreshness.Fresh, auditRecorder);

        PreviewApprovedTimeExport query = new()
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

        ApprovedTimePreviewResult result = await service.PreviewAsync(
            Context(),
            query,
            new DateTimeOffset(2026, 6, 19, 15, 0, 0, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeTrue();
        ApprovedTimeExportPreviewReadModel preview = result.Preview.ShouldNotBeNull();
        preview.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Ready);
        preview.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        preview.Scope.RowCount.ShouldBe(3);
        preview.Audit.Requester.ShouldBe(new PartyReference("operator-1"));
        preview.Audit.CorrelationId.ShouldBe("correlation-1");
        preview.Audit.GeneratedAtUtc.ShouldBeNull();
        preview.Audit.OutputContentHashSha256.ShouldBeNull();
        // End-to-end: preview emits no audit evidence.
        auditRecorder.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task No_results_filter_previews_blocked_without_file_or_audit_event()
    {
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded("time-entry-1", TimeEntryTargetReference.ForProject(Project()), 30)),
            Event("m2", 2, Submitted("time-entry-1")),
            Event("m3", 3, Approved("time-entry-1", "decision-1"))
        ];
        TrackingAuditRecorder auditRecorder = new();
        ApprovedTimeExportService service = Service(events, 3, ProjectionFreshness.Fresh, auditRecorder);

        PreviewApprovedTimeExport query = new()
        {
            LedgerQuery = new QueryApprovedTimeLedger
            {
                Contributor = new PartyReference("missing-party"),
                BillableState = BillableState.Billable,
                SortBy = TimeEntryQuerySortBy.TimeEntryId,
                PageSize = 50
            }
        };

        ApprovedTimePreviewResult result = await service.PreviewAsync(
            Context(),
            query,
            new DateTimeOffset(2026, 6, 19, 15, 0, 0, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        ApprovedTimeExportPreviewReadModel preview = result.Preview.ShouldNotBeNull();
        preview.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Blocked);
        preview.ReadinessDetail.ShouldBe("No approved ledger rows are available for export preview.");
        preview.Scope.RowCount.ShouldBe(0);
        auditRecorder.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task Stale_projection_previews_blocked_without_file_or_audit_event()
    {
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded("time-entry-1", TimeEntryTargetReference.ForProject(Project()), 30)),
            Event("m2", 2, Submitted("time-entry-1")),
            Event("m3", 3, Approved("time-entry-1", "decision-1"))
        ];
        TrackingAuditRecorder auditRecorder = new();
        ApprovedTimeExportService service = Service(events, 3, ProjectionFreshness.Stale, auditRecorder);

        PreviewApprovedTimeExport query = new()
        {
            LedgerQuery = new QueryApprovedTimeLedger
            {
                BillableState = BillableState.Billable,
                SortBy = TimeEntryQuerySortBy.TimeEntryId,
                PageSize = 50
            }
        };

        ApprovedTimePreviewResult result = await service.PreviewAsync(
            Context(),
            query,
            new DateTimeOffset(2026, 6, 19, 15, 0, 0, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        ApprovedTimeExportPreviewReadModel preview = result.Preview.ShouldNotBeNull();
        preview.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Blocked);
        preview.ReadinessDetail.ShouldBe("Projection freshness does not allow export preview.");
        preview.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Stale);
        auditRecorder.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task Preview_and_generate_agree_over_seeded_projection_while_only_generate_produces_file_and_audit()
    {
        // AC3 (NFR9): preview and generation share one readiness core, so over the SAME real seeded projection
        // they must agree on readiness, scope, and freshness — proven end-to-end, not just with a fake reader.
        TimeEntryProjectionEvent[] events = SeededCorrectedLedger();
        QueryApprovedTimeLedger ledgerFilters = CurrentRowsOnlyBillableFilters();

        TrackingAuditRecorder previewAudit = new();
        ApprovedTimeExportService previewService = Service(events, 7, ProjectionFreshness.Fresh, previewAudit);
        TrackingAuditRecorder generateAudit = new();
        ApprovedTimeExportService generateService = Service(events, 7, ProjectionFreshness.Fresh, generateAudit);

        ApprovedTimePreviewResult preview = await previewService.PreviewAsync(
            Context(),
            new PreviewApprovedTimeExport { LedgerQuery = ledgerFilters },
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);
        ApprovedTimeExportResult generate = await generateService.GenerateAsync(
            Context(),
            new GenerateApprovedTimeExport { LedgerQuery = ledgerFilters },
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);

        ApprovedTimeExportPreviewReadModel previewModel = preview.Preview.ShouldNotBeNull();
        ApprovedTimeExportReadModel generateModel = generate.Export.ShouldNotBeNull();

        previewModel.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Ready);
        previewModel.Readiness.ShouldBe(generateModel.Readiness);
        // Default current-rows-only scope counts the corrected current row, not the superseded original.
        previewModel.Scope.RowCount.ShouldBe(2);
        previewModel.Scope.RowCount.ShouldBe(generateModel.Scope.RowCount);
        previewModel.ProjectionFreshness.State.ShouldBe(generateModel.ProjectionFreshness.State);
        previewModel.Audit.BlockedReason.ShouldBe(generateModel.Audit.BlockedReason);

        // Only generation produces a CSV file and emits export audit evidence; preview produces neither.
        generate.WasGenerated.ShouldBeTrue();
        generateModel.CsvContent.ShouldNotBeNull();
        generateAudit.Events.ShouldHaveSingleItem().ShouldBeOfType<ApprovedTimeExported>();
        previewAudit.Events.ShouldBeEmpty();
        previewModel.Audit.GeneratedAtUtc.ShouldBeNull();
        previewModel.Audit.OutputContentHashSha256.ShouldBeNull();
    }

    [Fact]
    public async Task Preview_current_rows_only_corrected_scope_is_deterministic_and_excludes_superseded_rows()
    {
        // AC5 (NFR9): seeded corrected/superseded rows must yield a deterministic readiness scope across repeated
        // runs, and the current-rows-only filter must exclude the superseded original from the count.
        TimeEntryProjectionEvent[] events = SeededCorrectedLedger();
        TrackingAuditRecorder auditRecorder = new();
        ApprovedTimeExportService service = Service(events, 7, ProjectionFreshness.Fresh, auditRecorder);

        PreviewApprovedTimeExport currentRowsOnly = new() { LedgerQuery = CurrentRowsOnlyBillableFilters() };
        PreviewApprovedTimeExport includingSuperseded = new()
        {
            LedgerQuery = CurrentRowsOnlyBillableFilters() with
            {
                CurrentRowsOnly = false,
                IncludeSupersededRows = true
            }
        };

        ApprovedTimePreviewResult first = await service.PreviewAsync(
            Context(), currentRowsOnly, RequestedAtUtc(), TestContext.Current.CancellationToken);
        ApprovedTimePreviewResult second = await service.PreviewAsync(
            Context(), currentRowsOnly, RequestedAtUtc(), TestContext.Current.CancellationToken);
        ApprovedTimePreviewResult superseded = await service.PreviewAsync(
            Context(), includingSuperseded, RequestedAtUtc(), TestContext.Current.CancellationToken);

        ApprovedTimeExportPreviewReadModel firstModel = first.Preview.ShouldNotBeNull();
        ApprovedTimeExportPreviewReadModel secondModel = second.Preview.ShouldNotBeNull();
        ApprovedTimeExportPreviewReadModel supersededModel = superseded.Preview.ShouldNotBeNull();

        // Deterministic across repeated runs: the corrected current row is counted, the superseded original is not.
        firstModel.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Ready);
        firstModel.Scope.RowCount.ShouldBe(2);
        secondModel.Readiness.ShouldBe(firstModel.Readiness);
        secondModel.Scope.RowCount.ShouldBe(firstModel.Scope.RowCount);
        secondModel.ProjectionFreshness.State.ShouldBe(firstModel.ProjectionFreshness.State);

        // Including superseded rows deterministically widens the scope to the original plus its correction.
        supersededModel.Scope.RowCount.ShouldBe(3);
        supersededModel.Scope.RowCount.ShouldBeGreaterThan(firstModel.Scope.RowCount);

        // No preview run produces audit evidence.
        auditRecorder.Events.ShouldBeEmpty();
    }

    private static TimeEntryProjectionEvent[] SeededCorrectedLedger()
        =>
        [
            Event("m1", 1, Recorded("time-entry-1", TimeEntryTargetReference.ForProject(Project()), 30)),
            Event("m2", 2, Submitted("time-entry-1")),
            Event("m3", 3, Approved("time-entry-1", "decision-1")),
            Event("m4", 4, Recorded("time-entry-2", TimeEntryTargetReference.ForWork(Work()), 45)),
            Event("m5", 5, Submitted("time-entry-2")),
            Event("m6", 6, Approved("time-entry-2", "decision-2")),
            Event("m7", 7, ApprovedCorrected("time-entry-2", TimeEntryTargetReference.ForWork(Work()), 60))
        ];

    private static QueryApprovedTimeLedger CurrentRowsOnlyBillableFilters()
        => new()
        {
            BillableState = BillableState.Billable,
            CurrentRowsOnly = true,
            IncludeSupersededRows = false,
            SortBy = TimeEntryQuerySortBy.TimeEntryId,
            PageSize = 50
        };

    private static DateTimeOffset RequestedAtUtc() => new(2026, 6, 19, 15, 0, 0, TimeSpan.Zero);

    private static ApprovedTimeExportService Service(
        IReadOnlyList<TimeEntryProjectionEvent> events,
        long sequenceNumber,
        ProjectionFreshness freshness,
        IApprovedTimeExportAuditRecorder auditRecorder)
    {
        ApprovedTimeLedgerProjection ledgerProjection = new();
        TimesheetsProjectionCheckpoint checkpoint = new(
            "tenant-1",
            ApprovedTimeLedgerProjection.ProjectionName,
            sequenceNumber,
            freshness);

        return new(
            new AllowAllAccessGuard(),
            new ApprovedTimeLedgerQueryService(
                new AllowAllAccessGuard(),
                new SeededLedgerReader(ledgerProjection, events, checkpoint),
                new StaticHydrator()),
            auditRecorder);
    }

    private static TimeEntryProjectionEvent Event(string messageId, long sequenceNumber, object payload)
        => new(messageId, sequenceNumber, payload);

    private static TimeEntryRecorded Recorded(
        string id,
        TimeEntryTargetReference target,
        int durationMinutes)
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

    private sealed class TrackingAuditRecorder : IApprovedTimeExportAuditRecorder
    {
        public List<ApprovedTimeExported> Events { get; } = [];

        public ValueTask<TimesheetsDomainResult> RecordAcceptedExportAsync(
            ApprovedTimeExported evidence,
            CancellationToken cancellationToken)
        {
            Events.Add(evidence);
            return ValueTask.FromResult(TimesheetsDomainResult.Success([evidence]));
        }
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
