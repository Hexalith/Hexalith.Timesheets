using System.Text.Json;
using System.Text.RegularExpressions;

using Hexalith.Timesheets.Contracts;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.Ui;
using Hexalith.Timesheets.Contracts.ValueObjects;

using Shouldly;

namespace Hexalith.Timesheets.Contracts.Tests;

public sealed class DashboardContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Dashboard_query_round_trips_without_server_controlled_authority_or_freshness_fields()
    {
        QueryTimesheetsDashboardOverview query = new()
        {
            TenantLocalPeriodKey = "2026-06",
            ServiceDateFrom = new DateOnly(2026, 6, 1),
            ServiceDateTo = new DateOnly(2026, 6, 30),
            Project = new ProjectReference("project-1"),
            Work = new WorkReference("work-1"),
            ActivityTypeId = new ActivityTypeId("activity-type-1"),
            BillableState = BillableState.Billable
        };

        string json = JsonSerializer.Serialize(query, JsonOptions);

        json.ShouldContain("\"tenantLocalPeriodKey\":\"2026-06\"");
        json.ShouldContain("\"project\"");
        json.ShouldContain("\"work\"");
        json.ShouldNotContain("freshness", Case.Insensitive);
        AssertJsonOmitsCallerAuthority(json);

        QueryTimesheetsDashboardOverview? roundTripped = JsonSerializer.Deserialize<QueryTimesheetsDashboardOverview>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.Project.ShouldBe(new ProjectReference("project-1"));
        roundTripped.Work.ShouldBe(new WorkReference("work-1"));
        roundTripped.BillableState.ShouldBe(BillableState.Billable);
    }

    [Fact]
    public void Dashboard_read_model_round_trips_status_messages_shortcuts_and_preserved_context()
    {
        TimesheetsDashboardContextReadModel context = new(
            "2026-06",
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            new ProjectReference("project-1"),
            null,
            new ActivityTypeId("activity-type-1"),
            BillableState.Billable);
        TimesheetsDashboardShortcutReadModel recordTime = new(
            "record-time",
            "Record time",
            "Timesheets.RecordTime",
            TimesheetsDashboardShortcutVisibility.Visible,
            TimesheetsDashboardShortcutState.Ready,
            "Shortcut is available.",
            context);
        TimesheetsDashboardOverviewReadModel model = new(
            context,
            new(
                TimesheetsDashboardCurrentPeriodState.Mixed,
                3,
                1,
                1,
                1,
                0,
                ProjectionFreshnessMetadata.Stale("cursor-1"),
                "Current period requires freshness review."),
            new(
                1,
                0,
                recordTime,
                ProjectionFreshnessMetadata.Stale("cursor-1"),
                "Pending action counts require freshness review."),
            new(
                TimesheetsDashboardAuthorityState.Unresolved,
                TimesheetsDashboardShortcutVisibility.Hidden,
                0,
                ProjectionFreshnessMetadata.Unavailable("Authority cannot be resolved."),
                "Authority cannot be resolved.",
                null),
            [
                new(
                    "open-project-report",
                    "Project report",
                    "Timesheets.QueryProjectActualTimeReport",
                    TimesheetsDashboardShortcutVisibility.Visible,
                    TimesheetsDashboardShortcutState.BlockedByFreshness,
                    "Projection freshness requires attention before this shortcut is decision authority.",
                    context)
            ],
            new(
                TimesheetsDashboardShortcutVisibility.Visible,
                TimesheetsDashboardShortcutVisibility.Disabled,
                ApprovedTimeExportReadinessState.Blocked,
                0,
                ProjectionFreshnessMetadata.Degraded("Ledger shard is delayed."),
                "Projection freshness does not allow export preview.",
                null,
                null),
            [
                new(
                    "current-period",
                    ProjectionFreshnessMetadata.Stale("cursor-1"),
                    TimesheetsDashboardProjectionDecisionAuthority.StatusOnly,
                    "Projection is stale.")
            ],
            recordTime);

        string json = JsonSerializer.Serialize(model, JsonOptions);

        json.ShouldContain("\"currentPeriod\"");
        json.ShouldContain("\"state\":\"Mixed\"");
        json.ShouldContain("\"projectionStatuses\"");
        json.ShouldContain("\"decisionAuthority\":\"StatusOnly\"");
        json.ShouldContain("\"tenantLocalPeriodKey\":\"2026-06\"");
        json.ShouldContain("\"visibility\":\"Hidden\"");
        AssertJsonOmitsCallerAuthority(json, allowTenantId: true);
        AssertNoForbiddenBusinessLanguage(json);

        TimesheetsDashboardOverviewReadModel? roundTripped = JsonSerializer.Deserialize<TimesheetsDashboardOverviewReadModel>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.Context.Project.ShouldBe(new ProjectReference("project-1"));
        roundTripped.CurrentPeriod.State.ShouldBe(TimesheetsDashboardCurrentPeriodState.Mixed);
        roundTripped.ApprovalWorkload.AuthorityState.ShouldBe(TimesheetsDashboardAuthorityState.Unresolved);
        roundTripped.ProjectionStatuses.ShouldHaveSingleItem().DecisionAuthority.ShouldBe(TimesheetsDashboardProjectionDecisionAuthority.StatusOnly);
    }

    [Fact]
    public void Dashboard_metadata_declares_operational_fields_actions_badges_and_no_runtime_dependency_leakage()
    {
        TimesheetsMetadataDescriptor descriptor = TimesheetsMetadataCatalog.Descriptors
            .Single(static descriptor => descriptor.Name == "timesheets.dashboard.overview");

        descriptor.SurfaceKind.ShouldBe(TimesheetsSurfaceKind.Projection);
        descriptor.Pattern.ShouldBe(TimesheetsCompositionPattern.FrontComposerProjectionView);
        descriptor.Fields.Select(static field => field.Name).ShouldContain("currentPeriodState");
        descriptor.Fields.Select(static field => field.Name).ShouldContain("pendingSubmissionCount");
        descriptor.Fields.Select(static field => field.Name).ShouldContain("pendingCorrectionCount");
        descriptor.Fields.Select(static field => field.Name).ShouldContain("approvalWorkloadState");
        descriptor.Fields.Select(static field => field.Name).ShouldContain("reportShortcuts");
        descriptor.Fields.Select(static field => field.Name).ShouldContain("ledgerReadiness");
        descriptor.Fields.Select(static field => field.Name).ShouldContain("projectionStatuses");
        descriptor.Fields.Select(static field => field.Name).ShouldContain("emptyStateAction");
        descriptor.Actions.Select(static action => action.Intent).ShouldContain("Timesheets.RecordTime");
        descriptor.Actions.Select(static action => action.Intent).ShouldContain("Timesheets.OpenApprovalsQueue");
        descriptor.Actions.Select(static action => action.Intent).ShouldContain("Timesheets.QueryApprovedTimeLedger");
        descriptor.Actions.Select(static action => action.Intent).ShouldContain("Timesheets.GenerateApprovedLedgerExport");
        descriptor.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(ProjectionFreshnessState));
        descriptor.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(TimesheetsDashboardShortcutVisibility));
        descriptor.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(ApprovedTimeExportReadinessState));

        string json = JsonSerializer.Serialize(descriptor, JsonOptions);

        json.ShouldContain("FluentMessageBar");
        json.ShouldNotContain("Microsoft.FluentUI");
        json.ShouldNotContain("EventStore");
        AssertJsonOmitsCallerAuthority(json, allowTenantId: true);
        AssertNoForbiddenBusinessLanguage(json);
    }

    private static void AssertJsonOmitsCallerAuthority(string json, bool allowTenantId = false)
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
            if (allowTenantId && forbiddenPropertyName == "tenantId")
            {
                continue;
            }

            normalizedJson.Contains(
                $"\"{forbiddenPropertyName.ToLowerInvariant()}\"",
                StringComparison.Ordinal).ShouldBeFalse(forbiddenPropertyName);
        }
    }

    private static void AssertNoForbiddenBusinessLanguage(string content)
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
