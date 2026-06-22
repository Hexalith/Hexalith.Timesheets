using System.Text.Json;

using Hexalith.Timesheets.Contracts.Commands.Exports;
using Hexalith.Timesheets.Contracts.Events.Exports;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.Queries.Exports;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.Exports;
using Hexalith.Timesheets.Server.Policies;
using Hexalith.Timesheets.Server.TimeEntries;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class ApprovedTimeExportPreviewServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Preview_denial_prevents_ledger_lookup_and_records_no_audit()
    {
        TrackingLedgerReader reader = new(Page([Row("time-entry-1", ProjectTarget())]));
        TrackingHydrator hydrator = new();
        TrackingAuditRecorder auditRecorder = new();
        ApprovedTimeExportService service = Service(
            new SequencedAccessGuard([
                TimesheetsAuthorizationDecision.Denied(TimesheetsDenialCategory.InsufficientRole, "No export role.")
            ]),
            reader,
            hydrator,
            auditRecorder);

        ApprovedTimePreviewResult result = await service.PreviewAsync(
            Context(),
            PreviewQuery(),
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeFalse();
        result.Preview.ShouldBeNull();
        result.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.InsufficientRole);
        reader.Calls.ShouldBe(0);
        hydrator.Calls.ShouldBe(0);
        auditRecorder.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task Preview_missing_tenant_context_prevents_ledger_lookup_and_audit_recording()
    {
        TrackingLedgerReader reader = new(Page([Row("time-entry-1", ProjectTarget())]));
        TrackingAuditRecorder auditRecorder = new();
        ApprovedTimeExportService service = Service(
            new SequencedAccessGuard([TimesheetsAuthorizationDecision.Allowed()]),
            reader,
            new TrackingHydrator(),
            auditRecorder);

        ApprovedTimePreviewResult result = await service.PreviewAsync(
            MissingTenantContext(),
            PreviewQuery(),
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeFalse();
        result.Preview.ShouldBeNull();
        result.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.MissingTenant);
        reader.Calls.ShouldBe(0);
        auditRecorder.Events.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(TimesheetsDenialCategory.RetentionPolicyMissing)]
    [InlineData(TimesheetsDenialCategory.CommentPolicyMissing)]
    public async Task Preview_policy_gaps_fail_closed_before_ledger_lookup(TimesheetsDenialCategory category)
    {
        TrackingLedgerReader reader = new(Page([Row("time-entry-1", ProjectTarget())]));
        TrackingAuditRecorder auditRecorder = new();
        ApprovedTimeExportService service = Service(
            new SequencedAccessGuard([
                TimesheetsAuthorizationDecision.Denied(category, "Policy gap.")
            ]),
            reader,
            new TrackingHydrator(),
            auditRecorder);

        ApprovedTimePreviewResult result = await service.PreviewAsync(
            Context(),
            PreviewQuery(),
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeFalse();
        result.Preview.ShouldBeNull();
        result.Authorization.DenialCategory.ShouldBe(category);
        reader.Calls.ShouldBe(0);
        auditRecorder.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task Preview_blocks_stale_projection_without_output_or_audit()
    {
        ApprovedTimeLedgerReadModel stalePage = Page([Row("time-entry-1", ProjectTarget())]) with
        {
            ProjectionFreshness = ProjectionFreshnessMetadata.Stale(),
            CanUseForExport = false,
            ExportReadinessDetail = "Projection freshness does not allow export preview."
        };
        TrackingAuditRecorder auditRecorder = new();
        ApprovedTimeExportService service = Service(
            new SequencedAccessGuard([TimesheetsAuthorizationDecision.Allowed(), TimesheetsAuthorizationDecision.Allowed()]),
            new TrackingLedgerReader(stalePage),
            new TrackingHydrator(),
            auditRecorder);

        ApprovedTimePreviewResult result = await service.PreviewAsync(
            Context(),
            PreviewQuery(),
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeTrue();
        ApprovedTimeExportPreviewReadModel preview = result.Preview.ShouldNotBeNull();
        preview.IsReady.ShouldBeFalse();
        preview.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Blocked);
        preview.ReadinessDetail.ShouldBe("Projection freshness does not allow export preview.");
        preview.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Stale);
        preview.Audit.BlockedReason.ShouldBe("Projection freshness does not allow export preview.");
        preview.Audit.GeneratedAtUtc.ShouldBeNull();
        preview.Audit.OutputContentHashSha256.ShouldBeNull();
        auditRecorder.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task Preview_blocks_empty_ledger_without_file_or_audit()
    {
        TrackingAuditRecorder auditRecorder = new();
        ApprovedTimeExportService service = Service(
            new SequencedAccessGuard([TimesheetsAuthorizationDecision.Allowed(), TimesheetsAuthorizationDecision.Allowed()]),
            new TrackingLedgerReader(Page([])),
            new TrackingHydrator(),
            auditRecorder);

        ApprovedTimePreviewResult result = await service.PreviewAsync(
            Context(),
            PreviewQuery(),
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);

        ApprovedTimeExportPreviewReadModel preview = result.Preview.ShouldNotBeNull();
        preview.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Blocked);
        preview.ReadinessDetail.ShouldBe("No approved ledger rows are available for export preview.");
        preview.Scope.RowCount.ShouldBe(0);
        auditRecorder.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task Preview_blocks_invalid_contract_requests_before_ledger_lookup()
    {
        (PreviewApprovedTimeExport Query, string Reason)[] cases =
        [
            (PreviewQuery() with { Format = ApprovedTimeExportFormat.Unknown }, "Only CSV approved-ledger export format is supported."),
            (PreviewQuery() with { FormatVersion = "approved-time-export.csv.v2" }, "Unsupported approved-ledger export format version."),
            (
                PreviewQuery() with
                {
                    LedgerQuery = PreviewQuery().LedgerQuery with
                    {
                        BillableState = BillableState.NonBillable
                    }
                },
                "Approved billable ledger evidence is required for export.")
        ];

        foreach ((PreviewApprovedTimeExport query, string reason) in cases)
        {
            TrackingLedgerReader reader = new(Page([Row("time-entry-1", ProjectTarget())]));
            TrackingAuditRecorder auditRecorder = new();
            ApprovedTimeExportService service = Service(
                new SequencedAccessGuard([TimesheetsAuthorizationDecision.Allowed()]),
                reader,
                new TrackingHydrator(),
                auditRecorder);

            ApprovedTimePreviewResult result = await service.PreviewAsync(
                Context(),
                query,
                RequestedAtUtc(),
                TestContext.Current.CancellationToken);

            ApprovedTimeExportPreviewReadModel preview = result.Preview.ShouldNotBeNull();
            preview.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Blocked, reason);
            preview.ReadinessDetail.ShouldBe(reason);
            preview.Audit.BlockedReason.ShouldBe(reason);
            reader.Calls.ShouldBe(0, reason);
            auditRecorder.Events.ShouldBeEmpty(reason);
        }
    }

    [Fact]
    public async Task Preview_blocks_non_billable_ledger_rows_without_audit()
    {
        TrackingAuditRecorder auditRecorder = new();
        ApprovedTimeExportService service = Service(
            new SequencedAccessGuard([TimesheetsAuthorizationDecision.Allowed(), TimesheetsAuthorizationDecision.Allowed()]),
            new TrackingLedgerReader(Page([
                Row("time-entry-1", ProjectTarget()) with
                {
                    BillableState = BillableState.NonBillable
                }
            ])),
            new TrackingHydrator(),
            auditRecorder);

        ApprovedTimePreviewResult result = await service.PreviewAsync(
            Context(),
            PreviewQuery(),
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);

        ApprovedTimeExportPreviewReadModel preview = result.Preview.ShouldNotBeNull();
        preview.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Blocked);
        preview.ReadinessDetail.ShouldBe("Approved billable ledger evidence is required for export.");
        auditRecorder.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task Preview_ready_scope_is_deterministic_and_records_no_file_or_audit()
    {
        ApprovedTimeLedgerReadModel page = Page([
            Row("time-entry-2", WorkTarget(), ApprovedTimeLedgerRowState.Superseded),
            Row("time-entry-1", ProjectTarget())
        ]);
        TrackingAuditRecorder auditRecorder = new();
        ApprovedTimeExportService service = Service(
            new SequencedAccessGuard([]),
            new TrackingLedgerReader(page),
            new TrackingHydrator(),
            auditRecorder);

        PreviewApprovedTimeExport query = PreviewQuery() with
        {
            LedgerQuery = PreviewQuery().LedgerQuery with
            {
                CurrentRowsOnly = false,
                IncludeSupersededRows = true
            }
        };

        ApprovedTimePreviewResult first = await service.PreviewAsync(
            Context(),
            query,
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);
        ApprovedTimePreviewResult second = await service.PreviewAsync(
            Context(),
            query,
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);

        ApprovedTimeExportPreviewReadModel firstPreview = first.Preview.ShouldNotBeNull();
        ApprovedTimeExportPreviewReadModel secondPreview = second.Preview.ShouldNotBeNull();
        firstPreview.IsReady.ShouldBeTrue();
        firstPreview.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Ready);
        firstPreview.Scope.RowCount.ShouldBe(2);
        firstPreview.Scope.IncludesSupersededRows.ShouldBeTrue();
        firstPreview.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        // Deterministic across repeated runs.
        secondPreview.Scope.RowCount.ShouldBe(firstPreview.Scope.RowCount);
        secondPreview.Readiness.ShouldBe(firstPreview.Readiness);
        secondPreview.ProjectionFreshness.State.ShouldBe(firstPreview.ProjectionFreshness.State);
        // No file, no audit evidence.
        firstPreview.Audit.GeneratedAtUtc.ShouldBeNull();
        firstPreview.Audit.OutputContentHashSha256.ShouldBeNull();
        firstPreview.Audit.BlockedReason.ShouldBeNull();
        auditRecorder.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task Preview_ready_audit_metadata_carries_filters_scope_without_generated_fields_or_audit_event()
    {
        TrackingAuditRecorder auditRecorder = new();
        ApprovedTimeExportService service = Service(
            new SequencedAccessGuard([]),
            new TrackingLedgerReader(Page([Row("time-entry-1", WorkTarget())])),
            new TrackingHydrator(),
            auditRecorder);
        PreviewApprovedTimeExport query = PreviewQuery() with
        {
            LedgerQuery = PreviewQuery().LedgerQuery with
            {
                Project = null,
                Work = new WorkReference("work-1"),
                Contributor = new PartyReference("party-2"),
                ActivityTypeId = new ActivityTypeId("activity-type-2"),
                TenantLocalPeriodKey = "2026-06",
                IncludeSupersededRows = true,
                CurrentRowsOnly = false
            }
        };
        DateTimeOffset requestedAtUtc = RequestedAtUtc();

        ApprovedTimePreviewResult result = await service.PreviewAsync(
            Context(),
            query,
            requestedAtUtc,
            TestContext.Current.CancellationToken);

        ApprovedTimeExportPreviewReadModel preview = result.Preview.ShouldNotBeNull();
        preview.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Ready);
        preview.Audit.Filters.ShouldBe(query.LedgerQuery);
        preview.Audit.RequestedAtUtc.ShouldBe(requestedAtUtc);
        preview.Audit.GeneratedAtUtc.ShouldBeNull();
        preview.Audit.OutputContentHashSha256.ShouldBeNull();
        preview.Audit.Format.ShouldBe(ApprovedTimeExportFormat.Csv);
        preview.Audit.FormatVersion.ShouldBe("approved-time-export.csv.v1");
        preview.Audit.FreshnessState.ShouldBe(ProjectionFreshnessState.Fresh);
        preview.Audit.RowCount.ShouldBe(1);
        preview.Audit.Requester.ShouldBe(new PartyReference("operator-1"));
        preview.Audit.Tenant.ShouldBe(new TenantReference("tenant-1"));
        preview.Audit.OutputScope.ShouldBe(preview.Scope);
        preview.Scope.Filters.ShouldBe(query.LedgerQuery);
        preview.Format.ShouldBe(ApprovedTimeExportFormat.Csv);
        preview.FormatVersion.ShouldBe("approved-time-export.csv.v1");
        auditRecorder.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task Preview_surfaces_comment_policy_and_discloses_no_comment_text()
    {
        ApprovedTimeExportService service = Service(
            new SequencedAccessGuard([]),
            new TrackingLedgerReader(Page([
                Row("time-entry-allowed", ProjectTarget()) with
                {
                    Comment = CommentWithExportDecision("Allowed export comment", TimesheetsCommentPolicyDecision.Allowed)
                },
                Row("time-entry-excluded", ProjectTarget()) with
                {
                    Comment = CommentWithExportDecision("Excluded export comment", TimesheetsCommentPolicyDecision.Excluded)
                },
                Row("time-entry-redacted", ProjectTarget()) with
                {
                    Comment = CommentWithExportDecision("Redacted export comment", TimesheetsCommentPolicyDecision.Redacted)
                }
            ])),
            new TrackingHydrator());

        ApprovedTimePreviewResult result = await service.PreviewAsync(
            Context(),
            PreviewQuery(),
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);

        ApprovedTimeExportPreviewReadModel preview = result.Preview.ShouldNotBeNull();
        preview.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Ready);
        // Fail-closed default policy: comments are excluded from export preview.
        preview.CommentExportPolicy.ShouldBe(TimesheetsCommentPolicyDecision.Excluded);

        // The preview carries no rows or CSV by construction, so no comment text can be disclosed anywhere.
        string previewJson = JsonSerializer.Serialize(preview, JsonOptions);
        previewJson.ShouldNotContain("Allowed export comment");
        previewJson.ShouldNotContain("Excluded export comment");
        previewJson.ShouldNotContain("Redacted export comment");
    }

    [Fact]
    public async Task Preview_comment_policy_reflects_evidence_options_when_comments_allowed()
    {
        ApprovedTimeExportService service = Service(
            new SequencedAccessGuard([]),
            new TrackingLedgerReader(Page([Row("time-entry-1", ProjectTarget())])),
            new TrackingHydrator(),
            evidencePolicyOptions: new TimesheetsEvidencePolicyOptions { ExportCommentsAllowed = true });

        ApprovedTimePreviewResult result = await service.PreviewAsync(
            Context(),
            PreviewQuery(),
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);

        ApprovedTimeExportPreviewReadModel preview = result.Preview.ShouldNotBeNull();
        preview.CommentExportPolicy.ShouldBe(TimesheetsCommentPolicyDecision.Allowed);
    }

    [Fact]
    public async Task Preview_filters_insufficient_role_rows_from_scope()
    {
        TrackingHydrator hydrator = new();
        ApprovedTimeExportService service = Service(
            new SequencedAccessGuard([
                TimesheetsAuthorizationDecision.Allowed(),
                TimesheetsAuthorizationDecision.Allowed(),
                TimesheetsAuthorizationDecision.Allowed(),
                TimesheetsAuthorizationDecision.Denied(TimesheetsDenialCategory.InsufficientRole, "No row role.")
            ]),
            new TrackingLedgerReader(Page([
                Row("time-entry-1", ProjectTarget()),
                Row("time-entry-2", ProjectTarget("project-2"))
            ])),
            hydrator);

        ApprovedTimePreviewResult result = await service.PreviewAsync(
            Context(),
            PreviewQuery(),
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);

        ApprovedTimeExportPreviewReadModel preview = result.Preview.ShouldNotBeNull();
        preview.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Ready);
        // The insufficient-role row is silently filtered before the scope count.
        preview.Scope.RowCount.ShouldBe(1);
        string previewJson = JsonSerializer.Serialize(preview, JsonOptions);
        previewJson.ShouldNotContain("time-entry-2");
        hydrator.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Preview_fails_closed_when_ledger_row_authorization_is_unsafe()
    {
        TrackingAuditRecorder auditRecorder = new();
        ApprovedTimeExportService service = Service(
            new SequencedAccessGuard([
                TimesheetsAuthorizationDecision.Allowed(),
                TimesheetsAuthorizationDecision.Allowed(),
                TimesheetsAuthorizationDecision.Denied(TimesheetsDenialCategory.CrossTenantTarget, "Unsafe row.")
            ]),
            new TrackingLedgerReader(Page([Row("time-entry-1", ProjectTarget())])),
            new TrackingHydrator(),
            auditRecorder);

        ApprovedTimePreviewResult result = await service.PreviewAsync(
            Context(),
            PreviewQuery(),
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeFalse();
        result.Preview.ShouldBeNull();
        result.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.CrossTenantTarget);
        auditRecorder.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task Preview_accumulates_all_pages_when_ledger_is_cursor_paged()
    {
        SequentialPagedLedgerReader reader = new([
            Page([Row("time-entry-1", ProjectTarget())]) with { NextCursor = "cursor-2" },
            Page([Row("time-entry-2", ProjectTarget())])
        ]);
        TrackingAuditRecorder auditRecorder = new();
        ApprovedTimeExportService service = Service(
            new SequencedAccessGuard([]),
            reader,
            new TrackingHydrator(),
            auditRecorder);

        ApprovedTimePreviewResult result = await service.PreviewAsync(
            Context(),
            PreviewQuery(),
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);

        ApprovedTimeExportPreviewReadModel preview = result.Preview.ShouldNotBeNull();
        reader.Calls.ShouldBe(2);
        reader.ObservedCursors.ShouldBe([null, "cursor-2"]);
        preview.Scope.RowCount.ShouldBe(2);
        preview.Audit.RowCount.ShouldBe(2);
        auditRecorder.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task Preview_and_generate_share_one_readiness_core_on_ready_scope()
    {
        ApprovedTimeLedgerReadModel page = Page([
            Row("time-entry-1", ProjectTarget()),
            Row("time-entry-2", ProjectTarget("project-2"))
        ]);
        TrackingAuditRecorder previewAudit = new();
        ApprovedTimeExportService previewService = Service(
            new SequencedAccessGuard([]),
            new TrackingLedgerReader(page),
            new TrackingHydrator(),
            previewAudit);
        TrackingAuditRecorder generateAudit = new();
        ApprovedTimeExportService generateService = Service(
            new SequencedAccessGuard([]),
            new TrackingLedgerReader(page),
            new TrackingHydrator(),
            generateAudit);

        ApprovedTimePreviewResult preview = await previewService.PreviewAsync(
            Context(),
            PreviewQuery(),
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);
        ApprovedTimeExportResult generate = await generateService.GenerateAsync(
            Context(),
            Command(),
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);

        ApprovedTimeExportPreviewReadModel previewModel = preview.Preview.ShouldNotBeNull();
        ApprovedTimeExportReadModel generateModel = generate.Export.ShouldNotBeNull();

        // AC3: preview and generation agree on readiness, freshness, and scope row count.
        previewModel.Readiness.ShouldBe(generateModel.Readiness);
        previewModel.ProjectionFreshness.State.ShouldBe(generateModel.ProjectionFreshness.State);
        previewModel.Scope.RowCount.ShouldBe(generateModel.Scope.RowCount);
        previewModel.Audit.BlockedReason.ShouldBe(generateModel.Audit.BlockedReason);

        // On Ready, generation produces CSV + audit evidence; preview produces neither.
        generate.WasGenerated.ShouldBeTrue();
        generateModel.CsvContent.ShouldNotBeNull();
        generateAudit.Events.ShouldHaveSingleItem().ShouldBeOfType<ApprovedTimeExported>();
        previewAudit.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task Preview_and_generate_share_one_readiness_core_on_blocked_reason()
    {
        ApprovedTimeLedgerReadModel stalePage = Page([Row("time-entry-1", ProjectTarget())]) with
        {
            ProjectionFreshness = ProjectionFreshnessMetadata.Stale(),
            CanUseForExport = false,
            ExportReadinessDetail = "Projection freshness does not allow export preview."
        };
        TrackingAuditRecorder previewAudit = new();
        ApprovedTimeExportService previewService = Service(
            new SequencedAccessGuard([TimesheetsAuthorizationDecision.Allowed(), TimesheetsAuthorizationDecision.Allowed()]),
            new TrackingLedgerReader(stalePage),
            new TrackingHydrator(),
            previewAudit);
        TrackingAuditRecorder generateAudit = new();
        ApprovedTimeExportService generateService = Service(
            new SequencedAccessGuard([TimesheetsAuthorizationDecision.Allowed(), TimesheetsAuthorizationDecision.Allowed()]),
            new TrackingLedgerReader(stalePage),
            new TrackingHydrator(),
            generateAudit);

        ApprovedTimePreviewResult preview = await previewService.PreviewAsync(
            Context(),
            PreviewQuery(),
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);
        ApprovedTimeExportResult generate = await generateService.GenerateAsync(
            Context(),
            Command(),
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);

        ApprovedTimeExportPreviewReadModel previewModel = preview.Preview.ShouldNotBeNull();
        ApprovedTimeExportReadModel generateModel = generate.Export.ShouldNotBeNull();

        previewModel.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Blocked);
        previewModel.Readiness.ShouldBe(generateModel.Readiness);
        previewModel.ReadinessDetail.ShouldBe(generateModel.ReadinessDetail);
        previewModel.Audit.BlockedReason.ShouldBe(generateModel.Audit.BlockedReason);
        previewModel.ProjectionFreshness.State.ShouldBe(generateModel.ProjectionFreshness.State);
        // AC3 (cannot drift): output scope and audit row count agree on a blocked result — both report zero
        // output rows because neither produces a file.
        previewModel.Scope.RowCount.ShouldBe(generateModel.Scope.RowCount);
        previewModel.Audit.RowCount.ShouldBe(generateModel.Audit.RowCount);
        previewModel.Audit.RowCount.ShouldBe(0);
        generate.WasGenerated.ShouldBeFalse();
        previewAudit.Events.ShouldBeEmpty();
        generateAudit.Events.ShouldBeEmpty();
    }

    private static ApprovedTimeExportService Service(
        ITimesheetsAccessGuard accessGuard,
        IApprovedTimeLedgerProjectionReader reader,
        ITimeEntryDisplayHydrator hydrator,
        IApprovedTimeExportAuditRecorder? auditRecorder = null,
        TimesheetsEvidencePolicyOptions? evidencePolicyOptions = null)
        => new(
            accessGuard,
            new ApprovedTimeLedgerQueryService(accessGuard, reader, hydrator),
            auditRecorder,
            evidencePolicyOptions);

    private static PreviewApprovedTimeExport PreviewQuery()
        => new()
        {
            LedgerQuery = new QueryApprovedTimeLedger
            {
                Project = new ProjectReference("project-1"),
                BillableState = BillableState.Billable,
                SortBy = TimeEntryQuerySortBy.TimeEntryId,
                PageSize = 500
            }
        };

    private static GenerateApprovedTimeExport Command()
        => new()
        {
            LedgerQuery = new QueryApprovedTimeLedger
            {
                Project = new ProjectReference("project-1"),
                BillableState = BillableState.Billable,
                SortBy = TimeEntryQuerySortBy.TimeEntryId,
                PageSize = 500
            }
        };

    private static DateTimeOffset RequestedAtUtc() => new(2026, 6, 19, 14, 0, 0, TimeSpan.Zero);

    private static ApprovedTimeLedgerReadModel Page(IReadOnlyList<ApprovedTimeLedgerRowReadModel> rows)
        => new(
            rows,
            null,
            ProjectionFreshnessMetadata.Fresh,
            rows.Count > 0,
            rows.Count > 0
                ? "Approved ledger rows are fresh enough for export preview."
                : "No approved ledger rows are available for export preview.");

    private static ApprovedTimeLedgerRowReadModel Row(
        string id,
        TimeEntryTargetReference target,
        ApprovedTimeLedgerRowState rowState = ApprovedTimeLedgerRowState.Current)
        => new(
            new TimeEntryId(id),
            new PartyReference("party-1"),
            target,
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
            rowState,
            ProjectionFreshnessMetadata.Fresh)
        {
            EventLineage = [
                new("TimeEntryRecorded", 1, TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents),
                new("TimeEntryApproved", 3, TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents)
            ],
            AiMetrics = AiEffortMetrics.Unavailable
        };

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

    private static TimeEntryComment CommentWithExportDecision(
        string text,
        TimesheetsCommentPolicyDecision exportDecision)
        => new(
            text,
            new(
                TimesheetsCommentPolicyDecision.Allowed,
                TimesheetsCommentPolicyDecision.Excluded,
                TimesheetsCommentPolicyDecision.Allowed,
                exportDecision,
                TimesheetsCommentPolicyDecision.Excluded,
                TimesheetsCommentRedactionRequirement.NotRequired,
                TimesheetsEvidenceRetentionCategory.CommentText));

    private static TimeEntryTargetReference ProjectTarget(string projectId = "project-1")
        => TimeEntryTargetReference.ForProject(new ProjectReference(projectId));

    private static TimeEntryTargetReference WorkTarget() => TimeEntryTargetReference.ForWork(new WorkReference("work-1"));

    private static TimesheetsRequestContext Context()
        => new(new TenantReference("tenant-1"), new PartyReference("operator-1"), "correlation-1");

    private static TimesheetsRequestContext MissingTenantContext()
        => new(null, new PartyReference("operator-1"), "correlation-1");

    private sealed class TrackingLedgerReader(ApprovedTimeLedgerReadModel page) : IApprovedTimeLedgerProjectionReader
    {
        public int Calls { get; private set; }

        public ValueTask<ApprovedTimeLedgerReadModel?> QueryAsync(
            TimesheetsRequestContext context,
            QueryApprovedTimeLedger query,
            CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult<ApprovedTimeLedgerReadModel?>(page);
        }
    }

    private sealed class SequentialPagedLedgerReader(IReadOnlyList<ApprovedTimeLedgerReadModel> pages)
        : IApprovedTimeLedgerProjectionReader
    {
        public int Calls { get; private set; }

        public List<string?> ObservedCursors { get; } = [];

        public ValueTask<ApprovedTimeLedgerReadModel?> QueryAsync(
            TimesheetsRequestContext context,
            QueryApprovedTimeLedger query,
            CancellationToken cancellationToken)
        {
            ObservedCursors.Add(query.Cursor);
            ApprovedTimeLedgerReadModel page = pages[Calls];
            Calls++;
            return ValueTask.FromResult<ApprovedTimeLedgerReadModel?>(page);
        }
    }

    private sealed class TrackingHydrator : ITimeEntryDisplayHydrator
    {
        public int Calls { get; private set; }

        public ValueTask<TimeEntryDisplayHydration> HydrateAsync(
            TimesheetsRequestContext context,
            TimeEntryEvidenceReadModel evidence,
            CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult(TimeEntryDisplayHydration.Unavailable());
        }
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
}
