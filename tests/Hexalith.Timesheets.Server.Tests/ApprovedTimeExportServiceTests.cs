using Hexalith.Timesheets.Contracts.Commands.Exports;
using Hexalith.Timesheets.Contracts.Events.Exports;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.Exports;
using Hexalith.Timesheets.Server.TimeEntries;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class ApprovedTimeExportServiceTests
{
    [Fact]
    public async Task Export_denial_prevents_ledger_lookup()
    {
        TrackingLedgerReader reader = new(Page([Row("time-entry-1", ProjectTarget())]));
        TrackingHydrator hydrator = new();
        ApprovedTimeExportService service = Service(
            new SequencedAccessGuard([
                TimesheetsAuthorizationDecision.Denied(TimesheetsDenialCategory.InsufficientRole, "No export role.")
            ]),
            reader,
            hydrator);

        ApprovedTimeExportResult result = await service.GenerateAsync(
            Context(),
            Command(),
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasGenerated.ShouldBeFalse();
        result.AuditResult.ShouldBeNull();
        result.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.InsufficientRole);
        result.Export.ShouldBeNull();
        reader.Calls.ShouldBe(0);
        hydrator.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Export_missing_tenant_context_prevents_ledger_lookup_and_audit_recording()
    {
        TrackingLedgerReader reader = new(Page([Row("time-entry-1", ProjectTarget())]));
        TrackingAuditRecorder auditRecorder = new();
        ApprovedTimeExportService service = Service(
            new SequencedAccessGuard([TimesheetsAuthorizationDecision.Allowed()]),
            reader,
            new TrackingHydrator(),
            auditRecorder);

        ApprovedTimeExportResult result = await service.GenerateAsync(
            MissingTenantContext(),
            Command(),
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasGenerated.ShouldBeFalse();
        result.AuditResult.ShouldBeNull();
        result.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.MissingTenant);
        result.Export.ShouldBeNull();
        reader.Calls.ShouldBe(0);
        auditRecorder.Events.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(TimesheetsDenialCategory.RetentionPolicyMissing)]
    [InlineData(TimesheetsDenialCategory.CommentPolicyMissing)]
    public async Task Export_policy_gaps_fail_closed_before_ledger_lookup(TimesheetsDenialCategory category)
    {
        TrackingLedgerReader reader = new(Page([Row("time-entry-1", ProjectTarget())]));
        ApprovedTimeExportService service = Service(
            new SequencedAccessGuard([
                TimesheetsAuthorizationDecision.Denied(category, "Policy gap.")
            ]),
            reader,
            new TrackingHydrator());

        ApprovedTimeExportResult result = await service.GenerateAsync(
            Context(),
            Command(),
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasGenerated.ShouldBeFalse();
        result.AuditResult.ShouldBeNull();
        result.Authorization.DenialCategory.ShouldBe(category);
        reader.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Export_blocks_stale_projection_without_output()
    {
        ApprovedTimeLedgerReadModel stalePage = Page([Row("time-entry-1", ProjectTarget())]) with
        {
            ProjectionFreshness = ProjectionFreshnessMetadata.Stale(),
            CanUseForExport = false,
            ExportReadinessDetail = "Projection freshness does not allow export preview."
        };
        ApprovedTimeExportService service = Service(
            new SequencedAccessGuard([TimesheetsAuthorizationDecision.Allowed(), TimesheetsAuthorizationDecision.Allowed()]),
            new TrackingLedgerReader(stalePage),
            new TrackingHydrator());

        ApprovedTimeExportResult result = await service.GenerateAsync(
            Context(),
            Command(),
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasGenerated.ShouldBeFalse();
        result.AuditResult.ShouldBeNull();
        ApprovedTimeExportReadModel export = result.Export.ShouldNotBeNull();
        export.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Blocked);
        export.ReadinessDetail.ShouldBe("Projection freshness does not allow export preview.");
        export.HasOutput.ShouldBeFalse();
        export.Rows.ShouldBeEmpty();
        export.Audit.BlockedReason.ShouldBe("Projection freshness does not allow export preview.");
    }

    [Fact]
    public async Task Export_blocks_empty_ledger_without_empty_file()
    {
        ApprovedTimeExportService service = Service(
            new SequencedAccessGuard([TimesheetsAuthorizationDecision.Allowed(), TimesheetsAuthorizationDecision.Allowed()]),
            new TrackingLedgerReader(Page([])),
            new TrackingHydrator());

        ApprovedTimeExportResult result = await service.GenerateAsync(
            Context(),
            Command(),
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasGenerated.ShouldBeFalse();
        result.AuditResult.ShouldBeNull();
        ApprovedTimeExportReadModel export = result.Export.ShouldNotBeNull();
        export.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Blocked);
        export.ReadinessDetail.ShouldBe("No approved ledger rows are available for export preview.");
        export.CsvContent.ShouldBeNull();
    }

    [Fact]
    public async Task Export_blocks_invalid_contract_requests_before_ledger_lookup()
    {
        (GenerateApprovedTimeExport Command, string Reason)[] cases =
        [
            (Command() with { Format = ApprovedTimeExportFormat.Unknown }, "Only CSV approved-ledger export format is supported."),
            (Command() with { FormatVersion = "approved-time-export.csv.v2" }, "Unsupported approved-ledger export format version."),
            (
                Command() with
                {
                    LedgerQuery = Command().LedgerQuery with
                    {
                        BillableState = BillableState.NonBillable
                    }
                },
                "Approved billable ledger evidence is required for export.")
        ];

        foreach ((GenerateApprovedTimeExport command, string reason) in cases)
        {
            TrackingLedgerReader reader = new(Page([Row("time-entry-1", ProjectTarget())]));
            ApprovedTimeExportService service = Service(
                new SequencedAccessGuard([TimesheetsAuthorizationDecision.Allowed()]),
                reader,
                new TrackingHydrator());

            ApprovedTimeExportResult result = await service.GenerateAsync(
                Context(),
                command,
                RequestedAtUtc(),
                TestContext.Current.CancellationToken);

            result.WasGenerated.ShouldBeFalse(reason);
            result.AuditResult.ShouldBeNull();
            ApprovedTimeExportReadModel export = result.Export.ShouldNotBeNull();
            export.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Blocked);
            export.ReadinessDetail.ShouldBe(reason);
            export.CsvContent.ShouldBeNull();
            export.Audit.BlockedReason.ShouldBe(reason);
            reader.Calls.ShouldBe(0);
        }
    }

    [Fact]
    public async Task Export_blocks_non_billable_ledger_rows_without_csv_output()
    {
        ApprovedTimeExportService service = Service(
            new SequencedAccessGuard([TimesheetsAuthorizationDecision.Allowed(), TimesheetsAuthorizationDecision.Allowed()]),
            new TrackingLedgerReader(Page([
                Row("time-entry-1", ProjectTarget()) with
                {
                    BillableState = BillableState.NonBillable
                }
            ])),
            new TrackingHydrator());

        ApprovedTimeExportResult result = await service.GenerateAsync(
            Context(),
            Command(),
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasGenerated.ShouldBeFalse();
        result.AuditResult.ShouldBeNull();
        ApprovedTimeExportReadModel export = result.Export.ShouldNotBeNull();
        export.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Blocked);
        export.ReadinessDetail.ShouldBe("Approved billable ledger evidence is required for export.");
        export.CsvContent.ShouldBeNull();
    }

    [Fact]
    public async Task Export_generates_deterministic_csv_from_authorized_billable_ledger_rows()
    {
        ApprovedTimeLedgerReadModel page = Page([
            Row("time-entry-2", WorkTarget(), ApprovedTimeLedgerRowState.Superseded) with
            {
                Comment = AllowedExportComment("Needs, \"quotes\"\nand newline"),
                CommentProjectionState = TimesheetsCommentPolicyDecision.Allowed
            },
            Row("time-entry-1", ProjectTarget()) with
            {
                Comment = new("Comment that must not be exported", TimeEntryCommentPolicy.SensitiveDefault),
                CommentProjectionState = TimesheetsCommentPolicyDecision.PolicyRequired
            }
        ]);
        TrackingAuditRecorder auditRecorder = new();
        ApprovedTimeExportService service = Service(
            new SequencedAccessGuard([
                TimesheetsAuthorizationDecision.Allowed(),
                TimesheetsAuthorizationDecision.Allowed(),
                TimesheetsAuthorizationDecision.Allowed(),
                TimesheetsAuthorizationDecision.Allowed()
            ]),
            new TrackingLedgerReader(page),
            new TrackingHydrator(),
            auditRecorder);

        GenerateApprovedTimeExport command = Command() with
        {
            LedgerQuery = Command().LedgerQuery with
            {
                CurrentRowsOnly = false,
                IncludeSupersededRows = true,
                SortBy = TimeEntryQuerySortBy.DurationMinutes
            }
        };

        ApprovedTimeExportResult first = await service.GenerateAsync(
            Context(),
            command,
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);
        ApprovedTimeExportResult second = await service.GenerateAsync(
            Context(),
            command,
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);

        first.WasGenerated.ShouldBeTrue();
        second.WasGenerated.ShouldBeTrue();
        first.AuditResult.ShouldNotBeNull().Events.ShouldHaveSingleItem().ShouldBeOfType<ApprovedTimeExported>();
        auditRecorder.Events.Count.ShouldBe(2);
        first.Export.ShouldNotBeNull().CsvContent.ShouldBe(second.Export.ShouldNotBeNull().CsvContent);
        string csv = first.Export.ShouldNotBeNull().CsvContent.ShouldNotBeNull();
        csv.Split('\n')[0].ShouldBe(
            "time_entry_id,contributor_party_id,target_kind,target_reference,service_date,duration_minutes,activity_type_id,activity_type_scope,billable_flag,approval_decision_id,approval_state,approval_scope,approver_party_id,approved_at_utc,row_state,approved_correction_id,correction_id,event_lineage,ai_availability,ai_wall_clock_ms,ai_model_runtime_ms,ai_billable_effort_minutes,ai_input_tokens,ai_output_tokens,ai_total_tokens,comment");
        csv.ShouldContain("time-entry-1");
        csv.ShouldContain("time-entry-2");
        csv.ShouldContain("\"Needs, \"\"quotes\"\"\nand newline\"");
        csv.ShouldNotContain("Comment that must not be exported");
        csv.IndexOf("time-entry-1", StringComparison.Ordinal).ShouldBeLessThan(
            csv.IndexOf("time-entry-2", StringComparison.Ordinal));
        ApprovedTimeExported auditEvent = auditRecorder.Events[0];
        auditEvent.Requester.ShouldBe(new PartyReference("operator-1"));
        auditEvent.Tenant.ShouldBe(new TenantReference("tenant-1"));
        auditEvent.CorrelationId.ShouldBe("correlation-1");
        auditEvent.OutputScope.RowCount.ShouldBe(2);
        auditEvent.OutputContentHashSha256.ShouldBe(first.Export.ShouldNotBeNull().Audit.OutputContentHashSha256);
        auditEvent.OutputContentHashSha256.Length.ShouldBe(64);
        auditEvent.OutputContentHashSha256.ShouldBe(second.Export.ShouldNotBeNull().Audit.OutputContentHashSha256);
    }

    [Fact]
    public async Task Accepted_export_audit_evidence_records_filter_snapshot_timestamps_format_freshness_and_scope()
    {
        TrackingAuditRecorder auditRecorder = new();
        ApprovedTimeExportService service = Service(
            new SequencedAccessGuard([]),
            new TrackingLedgerReader(Page([Row("time-entry-1", WorkTarget())])),
            new TrackingHydrator(),
            auditRecorder);
        GenerateApprovedTimeExport command = Command() with
        {
            FormatVersion = "approved-time-export.csv.v1",
            LedgerQuery = Command().LedgerQuery with
            {
                Project = null,
                Work = new WorkReference("work-1"),
                Contributor = new PartyReference("party-2"),
                ActivityTypeId = new ActivityTypeId("activity-type-2"),
                TenantLocalPeriodKey = "2026-06",
                ServiceDateFrom = new DateOnly(2026, 6, 1),
                ServiceDateTo = new DateOnly(2026, 6, 30),
                IncludeSupersededRows = true,
                CurrentRowsOnly = false,
                SortBy = TimeEntryQuerySortBy.TimeEntryId
            }
        };
        DateTimeOffset requestedAtUtc = RequestedAtUtc();

        ApprovedTimeExportResult result = await service.GenerateAsync(
            Context(),
            command,
            requestedAtUtc,
            TestContext.Current.CancellationToken);

        result.WasGenerated.ShouldBeTrue();
        ApprovedTimeExportReadModel export = result.Export.ShouldNotBeNull();
        ApprovedTimeExported auditEvent = auditRecorder.Events.ShouldHaveSingleItem();
        auditEvent.Filters.ShouldBe(command.LedgerQuery);
        auditEvent.RequestedAtUtc.ShouldBe(requestedAtUtc);
        auditEvent.GeneratedAtUtc.ShouldBe(requestedAtUtc);
        auditEvent.Format.ShouldBe(ApprovedTimeExportFormat.Csv);
        auditEvent.FormatVersion.ShouldBe("approved-time-export.csv.v1");
        auditEvent.FreshnessState.ShouldBe(ProjectionFreshnessState.Fresh);
        auditEvent.RowCount.ShouldBe(1);
        auditEvent.OutputScope.ShouldBe(export.Scope);
        auditEvent.OutputScope.Filters.ShouldBe(command.LedgerQuery);
        auditEvent.OutputScope.IncludesSupersededRows.ShouldBeTrue();
        auditEvent.OutputScope.CurrentRowsOnly.ShouldBeFalse();
        auditEvent.OutputContentHashSha256.ShouldBe(export.Audit.OutputContentHashSha256);
        export.Audit.Filters.ShouldBe(command.LedgerQuery);
        export.Audit.RequestedAtUtc.ShouldBe(requestedAtUtc);
        export.Audit.GeneratedAtUtc.ShouldBe(requestedAtUtc);
        export.Audit.Format.ShouldBe(ApprovedTimeExportFormat.Csv);
        export.Audit.FormatVersion.ShouldBe("approved-time-export.csv.v1");
        export.Audit.FreshnessState.ShouldBe(ProjectionFreshnessState.Fresh);
        export.Audit.RowCount.ShouldBe(1);
    }

    [Fact]
    public void Csv_v1_uses_lf_line_endings_and_neutralizes_formula_leading_comments()
    {
        string csv = ApprovedTimeExportCsvWriter.Write([
            ExportRow("time-entry-1", ProjectTarget()) with
            {
                Comment = AllowedExportComment("=SUM(A1:A2)"),
                CommentExportState = TimesheetsCommentPolicyDecision.Allowed
            }
        ]);

        csv.ShouldNotContain("\r\n");
        csv.Split('\n').Length.ShouldBe(2);
        csv.ShouldContain("'=SUM(A1:A2)");
    }

    [Fact]
    public void Csv_v1_quotes_carriage_returns_commas_quotes_and_unicode_without_mutating_plain_text()
    {
        string csv = ApprovedTimeExportCsvWriter.Write([
            ExportRow("time-entry-1", ProjectTarget()) with
            {
                Comment = AllowedExportComment("Carriage\rReturn, \"quoted\" café"),
                CommentExportState = TimesheetsCommentPolicyDecision.Allowed
            }
        ]);

        // The whole comment field is quoted, the embedded quote is doubled, the carriage return
        // and Unicode text survive verbatim, and plain (non-formula) text is not apostrophe-prefixed.
        csv.ShouldContain("\"Carriage\rReturn, \"\"quoted\"\" café\"");
        csv.ShouldNotContain("'Carriage");
        csv.Split('\n').Length.ShouldBe(2);
    }

    [Fact]
    public void Csv_v1_neutralizes_formula_leading_values_in_any_field_not_only_comments()
    {
        // The formula-injection policy protects every column, so a structured field whose value
        // begins with a formula trigger is neutralized just like a free-text comment would be.
        string csv = ApprovedTimeExportCsvWriter.Write([
            ExportRow("=cmd-injection", ProjectTarget())
        ]);

        csv.ShouldContain("\n'=cmd-injection");
        csv.ShouldNotContain("\n=cmd-injection");
    }

    [Fact]
    public async Task Export_omits_comment_text_unless_export_policy_allows_it()
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
                },
                Row("time-entry-policy-required", ProjectTarget()) with
                {
                    Comment = CommentWithExportDecision("Policy required export comment", TimesheetsCommentPolicyDecision.PolicyRequired)
                }
            ])),
            new TrackingHydrator());

        ApprovedTimeExportResult result = await service.GenerateAsync(
            Context(),
            Command(),
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasGenerated.ShouldBeTrue();
        ApprovedTimeExportReadModel export = result.Export.ShouldNotBeNull();
        export.Rows.Single(static row => row.TimeEntryId.Value == "time-entry-allowed")
            .CommentExportState.ShouldBe(TimesheetsCommentPolicyDecision.Allowed);
        export.Rows.Single(static row => row.TimeEntryId.Value == "time-entry-excluded")
            .Comment.ShouldBeNull();
        export.Rows.Single(static row => row.TimeEntryId.Value == "time-entry-redacted")
            .Comment.ShouldBeNull();
        export.Rows.Single(static row => row.TimeEntryId.Value == "time-entry-policy-required")
            .Comment.ShouldBeNull();
        string csv = export.CsvContent.ShouldNotBeNull();
        csv.ShouldContain("Allowed export comment");
        csv.ShouldNotContain("Excluded export comment");
        csv.ShouldNotContain("Redacted export comment");
        csv.ShouldNotContain("Policy required export comment");
        export.Audit.BlockedReason.ShouldBeNull();
        export.Audit.OutputContentHashSha256.ShouldNotBeNull();
    }

    [Fact]
    public async Task Export_filters_insufficient_role_rows_before_output()
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

        ApprovedTimeExportResult result = await service.GenerateAsync(
            Context(),
            Command(),
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasGenerated.ShouldBeTrue();
        ApprovedTimeExportReadModel export = result.Export.ShouldNotBeNull();
        export.Rows.ShouldHaveSingleItem().TimeEntryId.ShouldBe(new TimeEntryId("time-entry-1"));
        export.CsvContent.ShouldNotBeNull().ShouldContain("time-entry-1");
        export.CsvContent.ShouldNotBeNull().ShouldNotContain("time-entry-2");
        hydrator.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Export_fails_closed_when_ledger_row_authorization_is_unsafe()
    {
        ApprovedTimeExportService service = Service(
            new SequencedAccessGuard([
                TimesheetsAuthorizationDecision.Allowed(),
                TimesheetsAuthorizationDecision.Allowed(),
                TimesheetsAuthorizationDecision.Denied(TimesheetsDenialCategory.CrossTenantTarget, "Unsafe row.")
            ]),
            new TrackingLedgerReader(Page([Row("time-entry-1", ProjectTarget())])),
            new TrackingHydrator());

        ApprovedTimeExportResult result = await service.GenerateAsync(
            Context(),
            Command(),
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasGenerated.ShouldBeFalse();
        result.AuditResult.ShouldBeNull();
        result.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.CrossTenantTarget);
        result.Export.ShouldBeNull();
    }

    [Fact]
    public async Task Export_accumulates_all_pages_when_ledger_is_cursor_paged()
    {
        SequentialPagedLedgerReader reader = new([
            Page([Row("time-entry-1", ProjectTarget())]) with { NextCursor = "cursor-2" },
            Page([Row("time-entry-2", ProjectTarget())])
        ]);
        ApprovedTimeExportService service = Service(
            new SequencedAccessGuard([]),
            reader,
            new TrackingHydrator());

        ApprovedTimeExportResult result = await service.GenerateAsync(
            Context(),
            Command(),
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasGenerated.ShouldBeTrue();
        ApprovedTimeExportReadModel export = result.Export.ShouldNotBeNull();
        reader.Calls.ShouldBe(2);
        reader.ObservedCursors.ShouldBe([null, "cursor-2"]);
        export.Scope.RowCount.ShouldBe(2);
        export.Audit.RowCount.ShouldBe(2);
        export.Audit.Tenant.ShouldBe(new TenantReference("tenant-1"));
        export.Audit.OutputContentHashSha256.ShouldNotBeNull().Length.ShouldBe(64);
        result.AuditResult.ShouldNotBeNull().Events.ShouldHaveSingleItem().ShouldBeOfType<ApprovedTimeExported>();
        export.Rows.Select(static row => row.TimeEntryId.Value).ShouldBe(["time-entry-1", "time-entry-2"]);
        export.CsvContent.ShouldNotBeNull().ShouldContain("time-entry-1");
        export.CsvContent.ShouldNotBeNull().ShouldContain("time-entry-2");
    }

    [Fact]
    public async Task Export_orders_descending_time_entry_ids_with_prefix_relationship()
    {
        ApprovedTimeExportService service = Service(
            new SequencedAccessGuard([]),
            new TrackingLedgerReader(Page([
                Row("time-entry-2", ProjectTarget()),
                Row("time-entry-20", ProjectTarget())
            ])),
            new TrackingHydrator());

        GenerateApprovedTimeExport command = Command() with
        {
            LedgerQuery = Command().LedgerQuery with
            {
                SortBy = TimeEntryQuerySortBy.TimeEntryId,
                SortDirection = TimeEntryQuerySortDirection.Descending
            }
        };

        ApprovedTimeExportResult result = await service.GenerateAsync(
            Context(),
            command,
            RequestedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasGenerated.ShouldBeTrue();
        ApprovedTimeExportReadModel export = result.Export.ShouldNotBeNull();
        export.Rows.Select(static row => row.TimeEntryId.Value).ShouldBe(["time-entry-20", "time-entry-2"]);
    }

    private static ApprovedTimeExportService Service(
        ITimesheetsAccessGuard accessGuard,
        IApprovedTimeLedgerProjectionReader reader,
        ITimeEntryDisplayHydrator hydrator,
        IApprovedTimeExportAuditRecorder? auditRecorder = null)
        => new(
            accessGuard,
            new ApprovedTimeLedgerQueryService(accessGuard, reader, hydrator),
            auditRecorder);

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

    private static ApprovedTimeExportRowReadModel ExportRow(string id, TimeEntryTargetReference target)
        => new(
            new TimeEntryId(id),
            new PartyReference("party-1"),
            target,
            new DateOnly(2026, 6, 19),
            60,
            new ActivityTypeId("activity-type-1"),
            ActivityTypeScope.Tenant,
            BillableState.Billable,
            Approval(id),
            ApprovedTimeLedgerRowState.Current)
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

    private static TimeEntryComment AllowedExportComment(string text)
        => CommentWithExportDecision(text, TimesheetsCommentPolicyDecision.Allowed);

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
