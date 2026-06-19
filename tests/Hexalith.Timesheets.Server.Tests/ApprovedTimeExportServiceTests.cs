using Hexalith.Timesheets.Contracts.Commands.Exports;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
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
        result.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.InsufficientRole);
        result.Export.ShouldBeNull();
        reader.Calls.ShouldBe(0);
        hydrator.Calls.ShouldBe(0);
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
        ApprovedTimeExportService service = Service(
            new SequencedAccessGuard([
                TimesheetsAuthorizationDecision.Allowed(),
                TimesheetsAuthorizationDecision.Allowed(),
                TimesheetsAuthorizationDecision.Allowed(),
                TimesheetsAuthorizationDecision.Allowed()
            ]),
            new TrackingLedgerReader(page),
            new TrackingHydrator());

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
        ITimeEntryDisplayHydrator hydrator)
        => new(
            accessGuard,
            new ApprovedTimeLedgerQueryService(accessGuard, reader, hydrator));

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

    private static TimeEntryComment AllowedExportComment(string text)
        => new(
            text,
            new(
                TimesheetsCommentPolicyDecision.Allowed,
                TimesheetsCommentPolicyDecision.Excluded,
                TimesheetsCommentPolicyDecision.Allowed,
                TimesheetsCommentPolicyDecision.Allowed,
                TimesheetsCommentPolicyDecision.Excluded,
                TimesheetsCommentRedactionRequirement.NotRequired,
                TimesheetsEvidenceRetentionCategory.CommentText));

    private static TimeEntryTargetReference ProjectTarget(string projectId = "project-1")
        => TimeEntryTargetReference.ForProject(new ProjectReference(projectId));

    private static TimeEntryTargetReference WorkTarget() => TimeEntryTargetReference.ForWork(new WorkReference("work-1"));

    private static TimesheetsRequestContext Context()
        => new(new TenantReference("tenant-1"), new PartyReference("operator-1"), "correlation-1");

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
