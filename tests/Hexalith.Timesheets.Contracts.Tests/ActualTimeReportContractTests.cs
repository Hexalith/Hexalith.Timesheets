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

public sealed class ActualTimeReportContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Project_and_work_report_queries_round_trip_without_server_controlled_authority_fields()
    {
        QueryProjectActualTimeReport projectQuery = new()
        {
            Project = new ProjectReference("project-1"),
            Contributor = new PartyReference("party-1"),
            AiAgent = new PartyReference("ai-agent-1"),
            ActivityTypeId = new ActivityTypeId("activity-type-1"),
            TenantLocalPeriodKey = "2026-06",
            BillableState = BillableState.Billable,
            ApprovalState = TimeEntryApprovalState.Approved,
            ContributorCategory = ContributorCategory.AutomatedAgent,
            AiMetricAvailability = AiMetricAvailability.ProviderReported,
            AiTokenAvailability = AiTokenMetricAvailability.NotReported,
            AiSourceCategory = AiEffortMetricSourceCategory.Provider,
            IncludeSupersededRows = true,
            SortBy = ActualTimeReportSortBy.ActualMinutes,
            PageSize = 25,
            Cursor = "opaque"
        };
        QueryWorkActualTimeReport workQuery = new()
        {
            Work = new WorkReference("work-1"),
            Contributor = new PartyReference("party-1"),
            ApprovalState = TimeEntryApprovalState.Submitted,
            ContributorCategory = ContributorCategory.ExternalContributor
        };

        string projectJson = JsonSerializer.Serialize(projectQuery, JsonOptions);
        string workJson = JsonSerializer.Serialize(workQuery, JsonOptions);

        AssertJsonOmitsCallerAuthority(projectJson);
        AssertJsonOmitsCallerAuthority(workJson);
        projectJson.ShouldNotContain("planned", Case.Insensitive);
        workJson.ShouldNotContain("planned", Case.Insensitive);
        projectJson.ShouldNotContain("converted", Case.Insensitive);
        projectJson.ShouldNotContain("tokenHours", Case.Insensitive);

        JsonSerializer.Deserialize<QueryProjectActualTimeReport>(projectJson, JsonOptions)
            .ShouldNotBeNull()
            .Project.ShouldBe(new ProjectReference("project-1"));
        JsonSerializer.Deserialize<QueryWorkActualTimeReport>(workJson, JsonOptions)
            .ShouldNotBeNull()
            .Work.ShouldBe(new WorkReference("work-1"));
    }

    [Fact]
    public void Actual_time_report_read_model_round_trips_reference_state_planned_effort_cursor_and_freshness()
    {
        ActualTimeReportReadModel model = new(
            [
                new(
                    TimeEntryTargetReference.ForWork(new WorkReference("work-1")),
                    "2026-06",
                    new DateOnly(2026, 6, 1),
                    new DateOnly(2026, 6, 30),
                    new PartyReference("party-1"),
                    new ActivityTypeId("activity-type-1"),
                    ActivityTypeScope.Tenant,
                    BillableState.Billable,
                    TimeEntryApprovalState.Approved,
                    ContributorCategory.Employee,
                    120,
                    2,
                    1,
                    0,
                    ActualTimeReportRowState.Current,
                    ActualTimeReferenceStateMetadata.Current,
                    ProjectionFreshnessMetadata.Degraded())
                {
                    WorkPlannedEffort = WorkPlannedEffortReadModel.Supplied(
                        160,
                        40,
                        120,
                        "minutes",
                        ProjectionFreshnessMetadata.Stale("12"))
                }
            ],
            "cursor-2",
            ProjectionFreshnessMetadata.Degraded());

        string json = JsonSerializer.Serialize(model, JsonOptions);

        json.ShouldContain("\"availability\":\"Supplied\"");
        json.ShouldContain("\"aiWallClockDurationMilliseconds\":null");
        json.ShouldContain("\"aiTokenAvailability\":\"Unavailable\"");
        json.ShouldContain("\"sourceModuleName\":\"Works\"");
        json.ShouldContain("\"nextCursor\":\"cursor-2\"");
        json.ShouldContain("\"state\":\"Degraded\"");

        ActualTimeReportReadModel? roundTripped = JsonSerializer.Deserialize<ActualTimeReportReadModel>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        ActualTimeReportRowReadModel row = roundTripped.Items.ShouldHaveSingleItem();
        row.WorkPlannedEffort.ShouldNotBeNull().Availability.ShouldBe(WorkPlannedEffortAvailability.Supplied);
        row.ReferenceState.State.ShouldBe(ActualTimeReferenceState.Current);
        row.ActualMinutes.ShouldBe(120);
        row.AiProviderTotalTokenCount.ShouldBeNull();
        row.AiTokenAvailability.ShouldBe(AiTokenMetricAvailability.Unavailable);
    }

    [Fact]
    public void Actual_time_report_read_model_round_trips_ai_effort_units_and_provider_reported_zero_tokens()
    {
        ActualTimeReportReadModel model = new(
            [
                new(
                    TimeEntryTargetReference.ForWork(new WorkReference("work-1")),
                    "2026-06",
                    new DateOnly(2026, 6, 1),
                    new DateOnly(2026, 6, 30),
                    new PartyReference("ai-agent-1"),
                    new ActivityTypeId("activity-type-1"),
                    ActivityTypeScope.Project,
                    BillableState.Billable,
                    TimeEntryApprovalState.Approved,
                    ContributorCategory.AutomatedAgent,
                    0,
                    1,
                    0,
                    0,
                    ActualTimeReportRowState.Current,
                    ActualTimeReferenceStateMetadata.Current,
                    ProjectionFreshnessMetadata.Fresh)
                {
                    AiWallClockDurationMilliseconds = 90_000,
                    AiModelRuntimeMilliseconds = 75_000,
                    AiBillableEffortMinutes = 2,
                    AiProviderInputTokenCount = 0,
                    AiProviderOutputTokenCount = 0,
                    AiProviderTotalTokenCount = 0,
                    AiMetricAvailability = AiMetricAvailability.ProviderReported,
                    AiTokenAvailability = AiTokenMetricAvailability.ProviderReported,
                    AiMetricSourceMetadata = AiEffortMetricSourceMetadata.Provider("provider-a", "tool-a", "run-1")
                }
            ],
            null,
            ProjectionFreshnessMetadata.Fresh);

        string json = JsonSerializer.Serialize(model, JsonOptions);

        json.ShouldContain("\"actualMinutes\":0");
        json.ShouldContain("\"aiBillableEffortMinutes\":2");
        json.ShouldContain("\"aiProviderTotalTokenCount\":0");
        json.ShouldContain("\"aiTokenAvailability\":\"ProviderReported\"");
        json.ShouldNotContain("tokenHours", Case.Insensitive);

        ActualTimeReportRowReadModel row = JsonSerializer.Deserialize<ActualTimeReportReadModel>(json, JsonOptions)
            .ShouldNotBeNull()
            .Items
            .ShouldHaveSingleItem();
        row.ActualMinutes.ShouldBe(0);
        row.AiProviderTotalTokenCount.ShouldBe(0);
        row.AiMetricSourceMetadata.ShouldNotBeNull().ProviderName.ShouldBe("provider-a");
    }

    [Fact]
    public void Work_planned_effort_unavailable_states_never_fabricate_planned_values()
    {
        WorkPlannedEffortReadModel[] unavailableStates =
        [
            WorkPlannedEffortReadModel.NotSupplied(),
            WorkPlannedEffortReadModel.Unavailable(),
            WorkPlannedEffortReadModel.Unauthorized()
        ];

        foreach (WorkPlannedEffortReadModel state in unavailableStates)
        {
            state.SourceModuleName.ShouldBe("Works");
            state.Estimated.ShouldBeNull();
            state.Done.ShouldBeNull();
            state.Remaining.ShouldBeNull();
            state.Unit.ShouldBeNull();
            state.Availability.ShouldNotBe(WorkPlannedEffortAvailability.Supplied);
        }

        WorkPlannedEffortReadModel stale = WorkPlannedEffortReadModel.Stale(
            160,
            40,
            120,
            "minutes",
            ProjectionFreshnessMetadata.Stale("44"));

        stale.Availability.ShouldBe(WorkPlannedEffortAvailability.Stale);
        stale.SourceFreshness.State.ShouldBe(ProjectionFreshnessState.Stale);
        stale.SourceModuleName.ShouldBe("Works");
    }

    [Fact]
    public void Actual_time_report_metadata_declares_required_filters_fields_badges_and_no_forbidden_language()
    {
        TimesheetsMetadataDescriptor project = Descriptor("timesheets.projection.project-actual-time-report");
        TimesheetsMetadataDescriptor work = Descriptor("timesheets.projection.work-actual-time-report");

        project.Pattern.ShouldBe(TimesheetsCompositionPattern.FrontComposerProjectionView);
        work.Pattern.ShouldBe(TimesheetsCompositionPattern.FrontComposerProjectionView);
        project.Fields.Select(static field => field.Name).ShouldContain("projectFilter");
        project.Fields.Select(static field => field.Name).ShouldContain("includeSupersededRows");
        project.Fields.Select(static field => field.Name).ShouldContain("actualMinutes");
        project.Fields.Select(static field => field.Name).ShouldContain("aiAgentFilter");
        project.Fields.Select(static field => field.Name).ShouldContain("aiWallClockDurationMilliseconds");
        project.Fields.Select(static field => field.Name).ShouldContain("aiProviderTotalTokenCount");
        project.Fields.Select(static field => field.Name).ShouldContain("aiTokenAvailability");
        project.Fields.Select(static field => field.Name).ShouldContain("referenceState");
        work.Fields.Select(static field => field.Name).ShouldContain("workFilter");
        work.Fields.Select(static field => field.Name).ShouldContain("aiMetricAvailabilityFilter");
        work.Fields.Select(static field => field.Name).ShouldContain("aiMetricSourceMetadata");
        work.Fields.Select(static field => field.Name).ShouldContain("plannedEffortAvailability");
        work.Fields.Select(static field => field.Name).ShouldContain("plannedSourceReferenceState");
        work.Fields.Select(static field => field.Name).ShouldContain("plannedSourceFreshness");
        work.Actions.Select(static action => action.Intent).ShouldContain("Timesheets.QueryProjectActualTimeReport");
        project.Actions.Select(static action => action.Intent).ShouldContain("Timesheets.QueryWorkActualTimeReport");
        project.Actions.Select(static action => action.Intent).ShouldContain("Timesheets.ReadTimeEntryEvidence");
        work.Actions.Select(static action => action.Intent).ShouldContain("Timesheets.ReadTimeEntryEvidence");

        string[] stateBadges = project.StateBadges.Concat(work.StateBadges)
            .Select(static badge => badge.StateVocabulary)
            .ToArray();
        stateBadges.ShouldContain(nameof(TimeEntryApprovalState));
        stateBadges.ShouldContain(nameof(BillableState));
        stateBadges.ShouldContain(nameof(ContributorCategory));
        stateBadges.ShouldContain(nameof(ActualTimeReportRowState));
        stateBadges.ShouldContain(nameof(ActualTimeReferenceState));
        stateBadges.ShouldContain(nameof(WorkPlannedEffortAvailability));
        stateBadges.ShouldContain(nameof(AiMetricAvailability));
        stateBadges.ShouldContain(nameof(AiTokenMetricAvailability));
        stateBadges.ShouldContain(nameof(AiEffortMetricSourceCategory));
        stateBadges.ShouldContain(nameof(ProjectionFreshnessState));

        string metadata = JsonSerializer.Serialize(new[] { project, work }, JsonOptions);
        metadata.ShouldContain("Unavailable");
        metadata.ShouldContain("Not reported by provider");
        metadata.ShouldNotContain("EventStore");
        metadata.ShouldNotContain("Project lifecycle", Case.Insensitive);
        metadata.ShouldNotContain("Project ownership", Case.Insensitive);
        metadata.ShouldNotContain("Work lifecycle", Case.Insensitive);
        metadata.ShouldNotContain("Work ownership", Case.Insensitive);
        metadata.ShouldNotContain("raw", Case.Insensitive);
        AssertNoFinanceOwnershipLanguage(metadata);
    }

    private static TimesheetsMetadataDescriptor Descriptor(string name)
        => TimesheetsMetadataCatalog.Descriptors.Single(descriptor => descriptor.Name == name);

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
