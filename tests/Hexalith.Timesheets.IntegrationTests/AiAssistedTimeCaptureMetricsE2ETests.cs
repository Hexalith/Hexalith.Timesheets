using System.Text.Json;

using Hexalith.Timesheets.Contracts.Commands.TimeEntries;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Projections.TimeEntries;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.TimeEntries;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed class AiAssistedTimeCaptureMetricsE2ETests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Ai_agent_time_capture_workflow_records_projects_and_discloses_separated_metrics()
    {
        AllowAllAccessGuard accessGuard = new();
        TimeEntryCommandService commandService = new(accessGuard);
        RecordTimeEntry command = AiCommand(NotReportedTokenMetrics());

        TimeEntryCommandResult commandResult = await commandService.RecordAsync(
            Context(),
            command,
            null,
            FreshCatalog(),
            TestContext.Current.CancellationToken);

        commandResult.WasDispatched.ShouldBeTrue();
        TimeEntryRecorded recorded = commandResult.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryRecorded>();
        recorded.DurationMinutes.ShouldBe(60);
        recorded.AiMetrics.ShouldNotBeNull();
        recorded.AiMetrics.BillableEffortMinutes.ShouldBe(2);
        recorded.AiMetrics.TokenAvailability.ShouldBe(AiTokenMetricAvailability.NotReported);
        recorded.AiMetrics.ProviderTotalTokenCount.ShouldBeNull();

        TimeEntryEvidenceReadModel projected = new TimeEntryEvidenceProjection().Project(
            "tenant-1",
            TimeEntryId(),
            [
                new("message-2", 2, RecordedOtherEntry()),
                new("message-1", 1, recorded),
                new("message-1", 1, recorded)
            ],
            new("tenant-1", TimeEntryEvidenceProjection.ProjectionName, 2, ProjectionFreshness.Fresh))
            .ShouldNotBeNull();

        TimeEntryEvidenceQueryService queryService = new(
            accessGuard,
            new FixedProjectionReader(projected),
            new FixedDisplayHydrator());

        TimeEntryEvidenceQueryResult queryResult = await queryService.ReadAsync(
            Context(),
            TimeEntryId(),
            TestContext.Current.CancellationToken);

        queryResult.WasDisclosed.ShouldBeTrue();
        TimeEntryEvidenceReadModel evidence = queryResult.Evidence.ShouldNotBeNull();
        evidence.ContributorCategory.ShouldBe(ContributorCategory.AutomatedAgent);
        evidence.DurationMinutes.ShouldBe(60);
        evidence.AiMetrics.ShouldNotBeNull();
        evidence.AiMetrics.WallClockDurationMilliseconds.ShouldBe(90000);
        evidence.AiMetrics.ModelRuntimeMilliseconds.ShouldBe(75000);
        evidence.AiMetrics.BillableEffortMinutes.ShouldBe(2);
        evidence.AiMetrics.ProviderInputTokenCount.ShouldBeNull();
        evidence.AiMetrics.ProviderOutputTokenCount.ShouldBeNull();
        evidence.AiMetrics.ProviderTotalTokenCount.ShouldBeNull();
        evidence.AiMetrics.TokenAvailability.ShouldBe(AiTokenMetricAvailability.NotReported);
        evidence.AiMetrics.Source.ShouldNotBeNull().SourceCategory.ShouldBe(AiEffortMetricSourceCategory.Provider);
        evidence.EventLineage.Count.ShouldBe(1);
        evidence.DisplayHydration.Contributor.Label.ShouldBe("AI agent");
        evidence.DisplayHydration.Target.Label.ShouldBe("Project");
        accessGuard.Requests.Select(static request => request.Operation).ShouldBe(
        [
            TimesheetsOperation.Command,
            TimesheetsOperation.ProjectionRead,
            TimesheetsOperation.ProjectionRead
        ]);

        string json = JsonSerializer.Serialize(evidence, JsonOptions);

        json.ShouldContain("\"durationMinutes\":60");
        json.ShouldContain("\"wallClockDurationMilliseconds\":90000");
        json.ShouldContain("\"modelRuntimeMilliseconds\":75000");
        json.ShouldContain("\"billableEffortMinutes\":2");
        json.ShouldContain("\"tokenAvailability\":\"NotReported\"");
        json.ShouldContain("\"providerInputTokenCount\":null");
        json.ShouldContain("\"providerOutputTokenCount\":null");
        json.ShouldContain("\"providerTotalTokenCount\":null");
        json.ShouldNotContain("\"providerTotalTokenCount\":0");
        AssertJsonOmitsCallerAuthority(json);
    }

    [Fact]
    public async Task Human_time_capture_workflow_rejects_provider_reported_ai_metrics()
    {
        TimeEntryCommandResult commandResult = await new TimeEntryCommandService(new AllowAllAccessGuard()).RecordAsync(
            Context(),
            HumanCommand(ProviderReportedTokenMetrics()),
            null,
            FreshCatalog(),
            TestContext.Current.CancellationToken);

        commandResult.WasDispatched.ShouldBeTrue();
        TimesheetsRejection rejection = commandResult.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimesheetsRejection>();

        rejection.Code.ShouldBe(TimesheetsRejectionCode.ValidationFailed);
        rejection.FieldErrors.ShouldContain(static error =>
            error.Field == "aiMetrics" && error.Code == "automated-agent-required");
    }

    private static RecordTimeEntry AiCommand(AiEffortMetrics metrics)
        => BaseCommand(ContributorCategory.AutomatedAgent, metrics);

    private static RecordTimeEntry HumanCommand(AiEffortMetrics metrics)
        => BaseCommand(ContributorCategory.Employee, metrics);

    private static RecordTimeEntry BaseCommand(
        ContributorCategory contributorCategory,
        AiEffortMetrics metrics)
        => new(
            TimeEntryId(),
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            ActivityId(),
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            contributorCategory,
            metrics);

    private static TimeEntryRecorded RecordedOtherEntry()
        => new(
            new TimeEntryId("other-entry"),
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            ActivityId(),
            ActivityTypeScope.Tenant,
            new DateOnly(2026, 6, 19),
            15,
            BillableState.Billable,
            TimeEntryApprovalState.Draft,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable);

    private static AiEffortMetrics NotReportedTokenMetrics()
        => new(
            AiMetricAvailability.ProviderReported,
            90000,
            75000,
            2,
            null,
            null,
            null,
            AiEffortMetricSourceMetadata.Provider("generic-provider", "capture-tool", "work-execution-1"),
            AiTokenMetricAvailability.NotReported);

    private static AiEffortMetrics ProviderReportedTokenMetrics()
        => new(
            AiMetricAvailability.ProviderReported,
            90000,
            75000,
            2,
            1000,
            250,
            1250,
            AiEffortMetricSourceMetadata.Provider("generic-provider", "capture-tool", "work-execution-1"),
            AiTokenMetricAvailability.ProviderReported);

    private static ActivityTypeCatalogReadModel FreshCatalog()
        => new(
            [
                new(
                    ActivityId(),
                    ActivityTypeScope.Tenant,
                    null,
                    "Delivery",
                    true,
                    BillableState.Billable)
            ],
            ProjectionFreshnessMetadata.Fresh);

    private static TimesheetsRequestContext Context()
        => new(
            new TenantReference("tenant-1"),
            new PartyReference("operator-1"),
            "correlation-1");

    private static ProjectReference Project() => new("project-1");

    private static PartyReference Contributor() => new("party-1");

    private static ActivityTypeId ActivityId() => new("activity-type-1");

    private static TimeEntryId TimeEntryId() => new("time-entry-1");

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

    private sealed class AllowAllAccessGuard : ITimesheetsAccessGuard
    {
        public List<TimesheetsAuthorizationRequest> Requests { get; } = [];

        public ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
            TimesheetsAuthorizationRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);

            return ValueTask.FromResult(TimesheetsAuthorizationDecision.Allowed());
        }

        public async ValueTask<TimesheetsAuthorizationDecision> ExecuteIfAuthorizedAsync(
            TimesheetsAuthorizationRequest request,
            Func<CancellationToken, ValueTask> trustedWork,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            await trustedWork(cancellationToken).ConfigureAwait(false);

            return TimesheetsAuthorizationDecision.Allowed();
        }

        public ValueTask<TimesheetsUiActionPolicyOutcome> EvaluateUiActionAsync(
            TimesheetsAuthorizationRequest request,
            TimesheetsUiActionVisibility deniedVisibility,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);

            return ValueTask.FromResult(TimesheetsUiActionPolicyOutcome.Allowed(
                request.UiAction ?? TimesheetsUiAction.Capture));
        }
    }

    private sealed class FixedProjectionReader : ITimeEntryEvidenceProjectionReader
    {
        private readonly TimeEntryEvidenceReadModel _model;

        public FixedProjectionReader(TimeEntryEvidenceReadModel model)
        {
            ArgumentNullException.ThrowIfNull(model);
            _model = model;
        }

        public ValueTask<TimeEntryEvidenceReadModel?> ReadAsync(
            TimesheetsRequestContext context,
            TimeEntryId timeEntryId,
            CancellationToken cancellationToken)
            => ValueTask.FromResult<TimeEntryEvidenceReadModel?>(_model);
    }

    private sealed class FixedDisplayHydrator : ITimeEntryDisplayHydrator
    {
        public ValueTask<TimeEntryDisplayHydration> HydrateAsync(
            TimesheetsRequestContext context,
            TimeEntryEvidenceReadModel evidence,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(new TimeEntryDisplayHydration(
                TimeEntryHydratedDisplayLabel.Fresh("AI agent"),
                TimeEntryHydratedDisplayLabel.Fresh("Project"),
                TimeEntryHydratedDisplayLabel.Fresh("Delivery")));
    }
}
