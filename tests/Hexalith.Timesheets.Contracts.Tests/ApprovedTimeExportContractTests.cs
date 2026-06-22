using System.Text.Json;
using System.Text.RegularExpressions;

using Hexalith.Timesheets.Contracts.Commands.Exports;
using Hexalith.Timesheets.Contracts.Events.Exports;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.Queries.Exports;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

using Shouldly;

namespace Hexalith.Timesheets.Contracts.Tests;

public sealed class ApprovedTimeExportContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Export_request_contracts_reuse_ledger_filters_and_omit_server_controlled_authority()
    {
        GenerateApprovedTimeExport command = new()
        {
            LedgerQuery = Filters(),
            Format = ApprovedTimeExportFormat.Csv,
            FormatVersion = "approved-time-export.csv.v1"
        };
        PreviewApprovedTimeExport preview = new()
        {
            LedgerQuery = Filters(),
            Format = ApprovedTimeExportFormat.Csv,
            FormatVersion = "approved-time-export.csv.v1"
        };

        string commandJson = JsonSerializer.Serialize(command, JsonOptions);
        string previewJson = JsonSerializer.Serialize(preview, JsonOptions);

        commandJson.ShouldContain("\"format\":\"Csv\"");
        commandJson.ShouldContain("\"includeSupersededRows\":true");
        previewJson.ShouldContain("\"ledgerQuery\"");
        AssertJsonOmitsCallerAuthority(commandJson);
        AssertJsonOmitsCallerAuthority(previewJson);

        GenerateApprovedTimeExport? roundTripped = JsonSerializer.Deserialize<GenerateApprovedTimeExport>(
            commandJson,
            JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.LedgerQuery.Project.ShouldBe(new ProjectReference("project-1"));
        roundTripped.LedgerQuery.BillableState.ShouldBe(BillableState.Billable);
        roundTripped.FormatVersion.ShouldBe("approved-time-export.csv.v1");
    }

    [Fact]
    public void Export_read_model_round_trips_audit_scope_lineage_comment_policy_and_csv_v1_output()
    {
        ApprovedTimeExportReadModel model = new(
            ApprovedTimeExportReadinessState.Ready,
            "Approved ledger rows are fresh enough for export.",
            new ApprovedTimeExportScope(Filters(), 1, true, false),
            ProjectionFreshnessMetadata.Fresh,
            new ApprovedTimeExportAuditMetadata(
                new PartyReference("operator-1"),
                Filters(),
                new DateTimeOffset(2026, 6, 19, 14, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 19, 14, 0, 1, TimeSpan.Zero),
                "correlation-1",
                new ApprovedTimeExportScope(Filters(), 1, true, false),
                ApprovedTimeExportFormat.Csv,
                "approved-time-export.csv.v1",
                ProjectionFreshnessState.Fresh,
                1,
                null)
            {
                Tenant = new TenantReference("tenant-1"),
                OutputContentHashSha256 = "abcdef"
            },
            ApprovedTimeExportFormat.Csv,
            "approved-time-export.csv.v1",
            [Row()])
        {
            CsvContent = "time_entry_id,contributor_party_id,target_kind,target_reference,service_date,duration_minutes"
        };

        string json = JsonSerializer.Serialize(model, JsonOptions);

        json.ShouldContain("\"readiness\":\"Ready\"");
        json.ShouldContain("\"format\":\"Csv\"");
        json.ShouldContain("\"formatVersion\":\"approved-time-export.csv.v1\"");
        json.ShouldContain("\"correlationId\":\"correlation-1\"");
        json.ShouldContain("\"commentExportState\":\"Excluded\"");
        json.ShouldContain("\"eventLineage\"");
        json.ShouldNotContain("displayHydration");
        AssertNoFinanceOwnershipLanguage(json);

        ApprovedTimeExportReadModel? roundTripped = JsonSerializer.Deserialize<ApprovedTimeExportReadModel>(
            json,
            JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.HasOutput.ShouldBeTrue();
        roundTripped.Rows.ShouldHaveSingleItem().Comment.ShouldBeNull();
        roundTripped.Audit.Requester.ShouldBe(new PartyReference("operator-1"));
        roundTripped.Audit.Tenant.ShouldBe(new TenantReference("tenant-1"));
        roundTripped.Audit.OutputContentHashSha256.ShouldBe("abcdef");
    }

    [Fact]
    public void Export_preview_read_model_round_trips_readiness_scope_comment_policy_and_audit_without_generated_fields()
    {
        ApprovedTimeExportPreviewReadModel model = new(
            ApprovedTimeExportReadinessState.Ready,
            "Approved ledger rows are fresh enough for export preview.",
            new ApprovedTimeExportScope(Filters(), 2, true, false),
            TimesheetsCommentPolicyDecision.Excluded,
            ProjectionFreshnessMetadata.Fresh,
            new ApprovedTimeExportAuditMetadata(
                new PartyReference("operator-1"),
                Filters(),
                new DateTimeOffset(2026, 6, 19, 14, 0, 0, TimeSpan.Zero),
                null,
                "correlation-1",
                new ApprovedTimeExportScope(Filters(), 2, true, false),
                ApprovedTimeExportFormat.Csv,
                "approved-time-export.csv.v1",
                ProjectionFreshnessState.Fresh,
                2,
                null)
            {
                Tenant = new TenantReference("tenant-1"),
                OutputContentHashSha256 = null
            },
            ApprovedTimeExportFormat.Csv,
            "approved-time-export.csv.v1");

        string json = JsonSerializer.Serialize(model, JsonOptions);

        json.ShouldContain("\"readiness\":\"Ready\"");
        json.ShouldContain("\"commentExportPolicy\":\"Excluded\"");
        json.ShouldContain("\"format\":\"Csv\"");
        json.ShouldContain("\"formatVersion\":\"approved-time-export.csv.v1\"");
        json.ShouldContain("\"generatedAtUtc\":null");
        // Preview is structurally rows-free: it can carry no CSV output and no evidence rows.
        json.ShouldNotContain("csvContent", Case.Insensitive);
        AssertNoFinanceOwnershipLanguage(json);

        ApprovedTimeExportPreviewReadModel? roundTripped = JsonSerializer.Deserialize<ApprovedTimeExportPreviewReadModel>(
            json,
            JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.IsReady.ShouldBeTrue();
        roundTripped.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Ready);
        roundTripped.Scope.RowCount.ShouldBe(2);
        roundTripped.CommentExportPolicy.ShouldBe(TimesheetsCommentPolicyDecision.Excluded);
        roundTripped.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        roundTripped.Audit.GeneratedAtUtc.ShouldBeNull();
        roundTripped.Audit.OutputContentHashSha256.ShouldBeNull();
        roundTripped.Audit.Tenant.ShouldBe(new TenantReference("tenant-1"));
        roundTripped.FormatVersion.ShouldBe("approved-time-export.csv.v1");
    }

    [Fact]
    public void Approved_time_exported_event_round_trips_safe_audit_evidence_without_output_payloads()
    {
        ApprovedTimeExported exported = new(
            new PartyReference("operator-1"),
            new TenantReference("tenant-1"),
            Filters(),
            new DateTimeOffset(2026, 6, 19, 14, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 19, 14, 0, 1, TimeSpan.Zero),
            "correlation-1",
            new ApprovedTimeExportScope(Filters(), 1, true, false),
            ApprovedTimeExportFormat.Csv,
            "approved-time-export.csv.v1",
            ProjectionFreshnessState.Fresh,
            1,
            "e3b0c44298fc1c149afbf4c8996fb924");

        string json = JsonSerializer.Serialize(exported, JsonOptions);

        json.ShouldContain("\"tenant\":{\"tenantId\":\"tenant-1\"}");
        json.ShouldContain("\"correlationId\":\"correlation-1\"");
        json.ShouldContain("\"outputContentHashSha256\":\"e3b0c44298fc1c149afbf4c8996fb924\"");
        json.ShouldNotContain("csvContent", Case.Insensitive);
        json.ShouldNotContain("comment", Case.Insensitive);
        json.ShouldNotContain("display", Case.Insensitive);
        json.ShouldNotContain("claims", Case.Insensitive);
        json.ShouldNotContain("body", Case.Insensitive);
        json.ShouldNotContain("envelope", Case.Insensitive);
        AssertNoFinanceOwnershipLanguage(json);

        ApprovedTimeExported? roundTripped = JsonSerializer.Deserialize<ApprovedTimeExported>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.Requester.ShouldBe(new PartyReference("operator-1"));
        roundTripped.Tenant.ShouldBe(new TenantReference("tenant-1"));
        roundTripped.OutputScope.RowCount.ShouldBe(1);
        roundTripped.OutputContentHashSha256.ShouldBe("e3b0c44298fc1c149afbf4c8996fb924");
    }

    private static QueryApprovedTimeLedger Filters()
        => new()
        {
            Project = new ProjectReference("project-1"),
            Work = new WorkReference("work-1"),
            Contributor = new PartyReference("party-1"),
            ActivityTypeId = new ActivityTypeId("activity-type-1"),
            TenantLocalPeriodKey = "2026-06",
            ServiceDateFrom = new DateOnly(2026, 6, 1),
            ServiceDateTo = new DateOnly(2026, 6, 30),
            BillableState = BillableState.Billable,
            CurrentRowsOnly = false,
            IncludeSupersededRows = true,
            SortBy = TimeEntryQuerySortBy.TimeEntryId,
            PageSize = 500
        };

    private static ApprovedTimeExportRowReadModel Row()
        => new(
            new TimeEntryId("time-entry-1"),
            new PartyReference("party-1"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            new DateOnly(2026, 6, 19),
            60,
            new ActivityTypeId("activity-type-1"),
            ActivityTypeScope.Tenant,
            BillableState.Billable,
            Approval(),
            ApprovedTimeLedgerRowState.Current)
        {
            EventLineage = [new("TimeEntryApproved", 3, TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents)],
            CommentExportState = TimesheetsCommentPolicyDecision.Excluded,
            AiMetrics = AiEffortMetrics.Unavailable
        };

    private static TimeEntryApprovalDecisionEvidence Approval()
        => new(
            new TimeEntryId("time-entry-1"),
            new TimeEntryApprovalDecisionId("decision-1"),
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

    private static void AssertJsonOmitsCallerAuthority(string json)
    {
        string normalizedJson = json.ToLowerInvariant();
        string[] forbiddenPropertyNames =
        [
            "tenantId",
            "userId",
            "correlationId",
            "messageId",
            "causationId",
            "authorization",
            "claimsPrincipal",
            "jwt",
            "token",
            "stream",
            "sequence"
        ];

        foreach (string forbiddenPropertyName in forbiddenPropertyNames)
        {
            normalizedJson.Contains(
                $"\"{forbiddenPropertyName.ToLowerInvariant()}\"",
                StringComparison.Ordinal).ShouldBeFalse(forbiddenPropertyName);
        }
    }

    private static void AssertNoFinanceOwnershipLanguage(string content)
    {
        foreach (string forbiddenWord in new[] { "invoice", "payroll", "rate", "tax", "revenue" })
        {
            Regex.IsMatch(
                content,
                $@"\b{forbiddenWord}\b",
                RegexOptions.IgnoreCase,
                TimeSpan.FromSeconds(1)).ShouldBeFalse(forbiddenWord);
        }
    }
}
