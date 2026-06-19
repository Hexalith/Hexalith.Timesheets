using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.Timesheets.Contracts.Commands.ActivityTypes;
using Hexalith.Timesheets.Contracts.Commands.TimeEntries;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.Queries.TimeEntries;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.Ui;
using Hexalith.Timesheets.Contracts.ValueObjects;

using Shouldly;

namespace Hexalith.Timesheets.Contracts.Tests;

public sealed class TimeCaptureContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Record_time_entry_models_exactly_one_target_reference()
    {
        TimeEntryTargetReference projectTarget = TimeEntryTargetReference.ForProject(new ProjectReference("project-42"));
        TimeEntryTargetReference workTarget = TimeEntryTargetReference.ForWork(new WorkReference("work-42"));

        projectTarget.TargetKind.ShouldBe(TimeEntryTargetKind.Project);
        projectTarget.TargetId.ShouldBe("project-42");
        workTarget.TargetKind.ShouldBe(TimeEntryTargetKind.Work);
        workTarget.TargetId.ShouldBe("work-42");

        Type commandType = typeof(RecordTimeEntry);
        commandType.GetProperty("Target").ShouldNotBeNull();
        commandType.GetProperty("TargetKind").ShouldBeNull();
        commandType.GetProperty("TargetId").ShouldBeNull();
        commandType.GetProperty("ProjectReference").ShouldBeNull();
        commandType.GetProperty("WorkReference").ShouldBeNull();
    }

    [Fact]
    public void Record_time_entry_contract_round_trips_through_web_json_without_authority_fields()
    {
        RecordTimeEntry command = new(
            new TimeEntryId("time-entry-123"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-123")),
            new PartyReference("party-123"),
            new ActivityTypeId("activity-type-123"),
            new DateOnly(2026, 6, 19),
            45,
            BillableState.Billable,
            ContributorCategory.AutomatedAgent,
            new(
                AiMetricAvailability.ProviderReported,
                90000,
                75000,
                2,
                1000,
                250,
                1250,
                AiEffortMetricSourceMetadata.Provider("generic-provider", "capture-tool", "work-execution-123"),
                AiTokenMetricAvailability.ProviderReported));

        string json = JsonSerializer.Serialize(command, JsonOptions);

        json.ShouldContain("\"timeEntryId\"");
        json.ShouldContain("\"targetKind\":\"Project\"");
        json.ShouldContain("\"durationMinutes\":45");
        json.ShouldContain("\"providerTotalTokenCount\":1250");
        AssertJsonOmitsCallerAuthority(json);

        RecordTimeEntry? roundTripped = JsonSerializer.Deserialize<RecordTimeEntry>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.TimeEntryId.Value.ShouldBe("time-entry-123");
        roundTripped.Target.TargetKind.ShouldBe(TimeEntryTargetKind.Project);
        roundTripped.Target.TargetId.ShouldBe("project-123");
        roundTripped.Contributor.PartyId.ShouldBe("party-123");
        roundTripped.AiMetrics.ShouldNotBeNull();
        roundTripped.AiMetrics.ProviderTotalTokenCount.ShouldBe(1250);
        roundTripped.AiMetrics.Source.ShouldNotBeNull();
        roundTripped.AiMetrics.Source.SourceCategory.ShouldBe(AiEffortMetricSourceCategory.Provider);
        roundTripped.AiMetrics.TokenAvailability.ShouldBe(AiTokenMetricAvailability.ProviderReported);
    }

    [Fact]
    public void Time_entry_evidence_read_model_round_trips_projection_and_state_metadata()
    {
        TimeEntryEvidenceReadModel readModel = new(
            new TimeEntryId("time-entry-456"),
            TimeEntryTargetReference.ForWork(new WorkReference("work-456")),
            new PartyReference("party-456"),
            new ActivityTypeId("activity-type-456"),
            ActivityTypeScope.Project,
            new DateOnly(2026, 6, 18),
            30,
            BillableState.NonBillable,
            TimeEntryApprovalState.Submitted,
            ContributorCategory.AutomatedAgent,
            AiEffortMetrics.Unavailable,
            TimeEntryCorrectionState.None,
            ProjectionFreshnessMetadata.Rebuilding());

        string json = JsonSerializer.Serialize(readModel, JsonOptions);

        json.ShouldContain("\"targetKind\":\"Work\"");
        json.ShouldContain("\"approvalState\":\"Submitted\"");
        json.ShouldContain("\"projectionFreshness\"");
        json.ShouldContain("Projection is rebuilding.");
        AssertJsonOmitsCallerAuthority(json, allowTenantId: true);

        TimeEntryEvidenceReadModel? roundTripped = JsonSerializer.Deserialize<TimeEntryEvidenceReadModel>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.Target.TargetKind.ShouldBe(TimeEntryTargetKind.Work);
        roundTripped.Target.TargetId.ShouldBe("work-456");
        roundTripped.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Rebuilding);
        roundTripped.AiMetrics.ShouldNotBeNull();
        roundTripped.AiMetrics.ProviderInputTokenCount.ShouldBeNull();
    }

    [Fact]
    public void Submit_time_entries_contract_round_trips_without_authority_fields()
    {
        SubmitTimeEntriesForApproval command = new(
            new TimeEntrySubmissionId("submission-123"),
            [
                new("time-entry-123"),
                new("time-entry-456")
            ],
            TimeEntrySubmissionScope.SelectedEntries);

        string json = JsonSerializer.Serialize(command, JsonOptions);

        json.ShouldContain("\"timeEntrySubmissionId\"");
        json.ShouldContain("\"timeEntryIds\"");
        json.ShouldContain("\"submissionScope\":\"SelectedEntries\"");
        AssertJsonOmitsCallerAuthority(json, allowTenantId: true);

        SubmitTimeEntriesForApproval? roundTripped = JsonSerializer.Deserialize<SubmitTimeEntriesForApproval>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.TimeEntrySubmissionId.Value.ShouldBe("submission-123");
        roundTripped.TimeEntryIds.Select(static id => id.Value).ShouldBe(["time-entry-123", "time-entry-456"]);
        roundTripped.SubmissionScope.ShouldBe(TimeEntrySubmissionScope.SelectedEntries);
    }

    [Fact]
    public void Time_entry_submitted_event_records_submission_evidence_and_resulting_state()
    {
        TimeEntrySubmitted submitted = new(
            new TimeEntryId("time-entry-123"),
            new PartyReference("party-submitter"),
            new TenantReference("tenant-123"),
            new DateTimeOffset(2026, 6, 19, 12, 30, 0, TimeSpan.Zero),
            new TimeEntrySubmissionId("submission-123"),
            TimeEntrySubmissionScope.SelectedEntries,
            TimeEntryApprovalState.Submitted);

        string json = JsonSerializer.Serialize(submitted, JsonOptions);

        json.ShouldContain("\"approvalState\":\"Submitted\"");
        json.ShouldContain("\"submissionScope\":\"SelectedEntries\"");
        json.ShouldContain("\"submittedAtUtc\"");
        AssertJsonOmitsCallerAuthority(json, allowTenantId: true);

        TimeEntrySubmitted? roundTripped = JsonSerializer.Deserialize<TimeEntrySubmitted>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.TimeEntryId.Value.ShouldBe("time-entry-123");
        roundTripped.Submitter.PartyId.ShouldBe("party-submitter");
        roundTripped.Tenant.TenantId.ShouldBe("tenant-123");
        roundTripped.TimeEntrySubmissionId.Value.ShouldBe("submission-123");
        roundTripped.ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
    }

    [Fact]
    public void Time_entry_evidence_read_model_round_trips_source_lineage_and_display_hydration_without_raw_envelope_fields()
    {
        TimeEntryEvidenceReadModel readModel = new(
            new TimeEntryId("time-entry-789"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-789")),
            new PartyReference("party-789"),
            new ActivityTypeId("activity-type-789"),
            ActivityTypeScope.Project,
            new DateOnly(2026, 6, 19),
            75,
            BillableState.Billable,
            TimeEntryApprovalState.Draft,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable,
            TimeEntryCorrectionState.None,
            ProjectionFreshnessMetadata.Fresh)
        {
            SourceAuthority = TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents,
            EventLineage =
            [
                new("TimeEntryRecorded", 7, TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents)
            ],
            DisplayHydration = new(
                TimeEntryHydratedDisplayLabel.Fresh("Contributor label"),
                TimeEntryHydratedDisplayLabel.Stale("Project label"),
                TimeEntryHydratedDisplayLabel.Unavailable())
        };

        string json = JsonSerializer.Serialize(readModel, JsonOptions);

        json.ShouldContain("\"sourceAuthority\":\"TimesheetsDomainEvents\"");
        json.ShouldContain("\"eventLineage\"");
        json.ShouldContain("\"eventName\":\"TimeEntryRecorded\"");
        json.ShouldContain("\"ordinal\":7");
        json.ShouldContain("\"displayHydration\"");
        json.ShouldContain("\"state\":\"Stale\"");
        AssertJsonOmitsCallerAuthority(json);
        json.ShouldNotContain("\"payload\"", Case.Insensitive);
        json.ShouldNotContain("\"envelope\"", Case.Insensitive);

        TimeEntryEvidenceReadModel? roundTripped = JsonSerializer.Deserialize<TimeEntryEvidenceReadModel>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.SourceAuthority.ShouldBe(TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents);
        roundTripped.EventLineage.ShouldHaveSingleItem().Ordinal.ShouldBe(7);
        roundTripped.DisplayHydration.Target.State.ShouldBe(DisplayHydrationState.Stale);
    }

    [Fact]
    public void Rejection_contract_round_trips_policy_failures_without_disclosing_protected_context()
    {
        TimesheetsRejection rejection = new(
            TimesheetsRejectionCode.AuthorityCannotBeResolved,
            "Authority cannot be resolved.",
            [
                new("target", "authority-unresolved", "Authority cannot be resolved.")
            ]);

        string json = JsonSerializer.Serialize(rejection, JsonOptions);

        json.ShouldContain("\"code\":\"AuthorityCannotBeResolved\"");
        json.ShouldContain("\"fieldErrors\"");
        json.ShouldContain("\"field\":\"target\"");
        AssertJsonOmitsCallerAuthority(json);

        TimesheetsRejection? roundTripped = JsonSerializer.Deserialize<TimesheetsRejection>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.Code.ShouldBe(TimesheetsRejectionCode.AuthorityCannotBeResolved);
        roundTripped.FieldErrors.Single().Code.ShouldBe("authority-unresolved");
    }

    [Fact]
    public void Public_commands_do_not_accept_server_controlled_authority_or_envelope_fields()
    {
        Type[] commandTypes =
        [
            typeof(RecordTimeEntry),
            typeof(SubmitTimeEntriesForApproval),
            typeof(CreateTenantActivityType),
            typeof(CreateProjectActivityType),
            typeof(RenameProjectActivityType),
            typeof(UpdateProjectActivityTypeMetadata),
            typeof(DeactivateProjectActivityType),
            typeof(ReactivateProjectActivityType),
            typeof(ConfigureProjectActivityTypeCatalogRestriction),
            typeof(RenameActivityType),
            typeof(UpdateActivityTypeMetadata),
            typeof(DeactivateActivityType),
            typeof(ReactivateActivityType),
            typeof(ReadTimeEntryEvidence)
        ];

        string[] forbiddenNames =
        [
            "TenantId",
            "UserId",
            "CorrelationId",
            "MessageId",
            "CausationId",
            "Sequence",
            "Stream",
            "ClaimsPrincipal",
            "Authorization",
            "Jwt",
            "Token",
            "Roles"
        ];

        foreach (Type commandType in commandTypes)
        {
            string[] propertyNames = commandType.GetProperties().Select(static property => property.Name).ToArray();

            foreach (string forbiddenName in forbiddenNames)
            {
                propertyNames.ShouldNotContain(forbiddenName, commandType.Name);
            }
        }
    }

    [Fact]
    public void Value_enums_expose_unknown_zero_sentinels()
    {
        Enum.GetName((TimeEntryTargetKind)0).ShouldBe("Unknown");
        Enum.GetName((ContributorCategory)0).ShouldBe("Unknown");
        Enum.GetName((TimeEntryApprovalState)0).ShouldBe("Unknown");
        Enum.GetName((TimeEntrySubmissionScope)0).ShouldBe("Unknown");
        Enum.GetName((BillableState)0).ShouldBe("Unknown");
        Enum.GetName((ActivityTypeScope)0).ShouldBe("Unknown");
        Enum.GetName((ActivityTypeActiveState)0).ShouldBe("Unknown");
        Enum.GetName((ProjectionFreshnessState)0).ShouldBe("Unknown");
        Enum.GetName((AiMetricAvailability)0).ShouldBe("Unknown");
        Enum.GetName((AiEffortMetricSourceCategory)0).ShouldBe("Unknown");
        Enum.GetName((AiTokenMetricAvailability)0).ShouldBe("Unknown");
        Enum.GetName((TimeEntryEvidenceSourceAuthority)0).ShouldBe("Unknown");
        Enum.GetName((DisplayHydrationState)0).ShouldBe("Unknown");
    }

    [Fact]
    public void Ai_metrics_keep_units_source_metadata_and_unavailable_provider_counts_explicit()
    {
        AiEffortMetrics unavailable = AiEffortMetrics.Unavailable;

        unavailable.Availability.ShouldBe(AiMetricAvailability.Unavailable);
        unavailable.Source.ShouldBe(AiEffortMetricSourceMetadata.Unavailable);
        unavailable.TokenAvailability.ShouldBe(AiTokenMetricAvailability.Unavailable);
        unavailable.WallClockDurationMilliseconds.ShouldBeNull();
        unavailable.ModelRuntimeMilliseconds.ShouldBeNull();
        unavailable.BillableEffortMinutes.ShouldBeNull();
        unavailable.ProviderInputTokenCount.ShouldBeNull();
        unavailable.ProviderOutputTokenCount.ShouldBeNull();
        unavailable.ProviderTotalTokenCount.ShouldBeNull();

        typeof(AiEffortMetrics).GetProperty("Duration").ShouldBeNull();
        typeof(AiEffortMetrics).GetProperty("Tokens").ShouldBeNull();
        typeof(AiEffortMetricSourceMetadata).GetProperty("Prompt").ShouldBeNull();
        typeof(AiEffortMetricSourceMetadata).GetProperty("Response").ShouldBeNull();
        typeof(AiEffortMetricSourceMetadata).GetProperty("Secret").ShouldBeNull();
        typeof(AiEffortMetricSourceMetadata).GetProperty("BearerToken").ShouldBeNull();
        typeof(AiEffortMetricSourceMetadata).GetProperty("RequestBody").ShouldBeNull();
        typeof(AiEffortMetricSourceMetadata).GetProperty("ResponseBody").ShouldBeNull();
        typeof(AiEffortMetricSourceMetadata).GetProperty("PartyDisplayName").ShouldBeNull();
    }

    [Fact]
    public void Ai_metrics_distinguish_provider_reported_zero_tokens_from_unreported_tokens()
    {
        AiEffortMetrics reportedZeroTokens = new(
            AiMetricAvailability.ProviderReported,
            100,
            90,
            1,
            0,
            0,
            0,
            AiEffortMetricSourceMetadata.Provider("generic-provider", "capture-tool", "work-execution-456"),
            AiTokenMetricAvailability.ProviderReported);
        AiEffortMetrics notReportedTokens = new(
            AiMetricAvailability.ProviderReported,
            100,
            90,
            1,
            null,
            null,
            null,
            AiEffortMetricSourceMetadata.Provider("generic-provider", "capture-tool", "work-execution-789"),
            AiTokenMetricAvailability.NotReported);

        reportedZeroTokens.ProviderInputTokenCount.ShouldBe(0);
        reportedZeroTokens.ProviderOutputTokenCount.ShouldBe(0);
        reportedZeroTokens.ProviderTotalTokenCount.ShouldBe(0);
        reportedZeroTokens.TokenAvailability.ShouldBe(AiTokenMetricAvailability.ProviderReported);

        notReportedTokens.ProviderInputTokenCount.ShouldBeNull();
        notReportedTokens.ProviderOutputTokenCount.ShouldBeNull();
        notReportedTokens.ProviderTotalTokenCount.ShouldBeNull();
        notReportedTokens.TokenAvailability.ShouldBe(AiTokenMetricAvailability.NotReported);
    }

    [Fact]
    public void Time_entry_evidence_read_model_keeps_sibling_references_stable_and_projection_freshness_explicit()
    {
        TimeEntryEvidenceReadModel model = new(
            new TimeEntryId("time-entry-1"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            new PartyReference("party-1"),
            new ActivityTypeId("activity-type-1"),
            ActivityTypeScope.Tenant,
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            TimeEntryApprovalState.Draft,
            ContributorCategory.ExternalContributor,
            AiEffortMetrics.Unavailable,
            TimeEntryCorrectionState.None,
            ProjectionFreshnessMetadata.Fresh);

        model.Target.TargetId.ShouldBe("project-1");
        model.Contributor.PartyId.ShouldBe("party-1");
        model.DurationMinutes.ShouldBe(60);
        model.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
    }

    [Fact]
    public void Metadata_catalog_declares_foundational_frontcomposer_surfaces_without_runtime_dependencies()
    {
        TimesheetsMetadataCatalog.Descriptors.Select(static descriptor => descriptor.Name)
            .ShouldBe(
            [
                "timesheets.command.record-time",
                "timesheets.command.submit-time-entries",
                "timesheets.command.activity-type-catalog",
                "timesheets.projection.activity-type-catalog",
                "timesheets.projection.time-entry-evidence",
                "timesheets.review.export-policy"
            ]);

        TimesheetsMetadataCatalog.Descriptors
            .Select(static descriptor => descriptor.Pattern)
            .ShouldContain(TimesheetsCompositionPattern.FrontComposerGeneratedForm);
        TimesheetsMetadataCatalog.Descriptors
            .Select(static descriptor => descriptor.Pattern)
            .ShouldContain(TimesheetsCompositionPattern.FrontComposerProjectionView);

        string serializedMetadata = string.Join(
            " ",
            TimesheetsMetadataCatalog.Descriptors.Select(static descriptor => descriptor.ToString()));

        serializedMetadata.ShouldNotContain("Microsoft.FluentUI");
        serializedMetadata.ShouldNotContain("Microsoft.AspNetCore");
        serializedMetadata.ShouldNotContain("Dapr");
        serializedMetadata.ShouldNotContain("EventStore");
    }

    [Fact]
    public void Metadata_catalog_declares_required_status_badge_vocabularies()
    {
        string[] badgeVocabularies = TimesheetsMetadataCatalog.Descriptors
            .SelectMany(static descriptor => descriptor.StateBadges)
            .Select(static badge => badge.StateVocabulary)
            .ToArray();

        badgeVocabularies.ShouldContain(nameof(TimeEntryApprovalState));
        badgeVocabularies.ShouldContain(nameof(BillableState));
        badgeVocabularies.ShouldContain(nameof(ContributorCategory));
        badgeVocabularies.ShouldContain(nameof(ActivityTypeActiveState));
        badgeVocabularies.ShouldContain(nameof(TimeEntryCorrectionState));
        badgeVocabularies.ShouldContain(nameof(ProjectionFreshnessState));
        badgeVocabularies.ShouldContain(nameof(TimeEntryEvidenceSourceAuthority));
        badgeVocabularies.ShouldContain(nameof(DisplayHydrationState));
    }

    [Fact]
    public void Record_time_entry_e2e_metadata_exposes_expected_capture_workflow()
    {
        TimesheetsMetadataDescriptor command = Descriptor("timesheets.command.record-time");
        TimesheetsMetadataDescriptor submission = Descriptor("timesheets.command.submit-time-entries");
        TimesheetsMetadataDescriptor evidence = Descriptor("timesheets.projection.time-entry-evidence");

        command.Pattern.ShouldBe(TimesheetsCompositionPattern.FrontComposerGeneratedForm);
        command.Fields.Select(static field => field.Name).ShouldBe(
        [
            "serviceDate",
            "target",
            "contributor",
            "activityType",
            "durationMinutes",
            "billableState",
            "contributorCategory",
            "aiMetrics",
            "aiWallClockDurationMilliseconds",
            "aiModelRuntimeMilliseconds",
            "aiBillableEffortMinutes",
            "aiTokenAvailability",
            "aiMetricSource",
            "comment"
        ]);
        command.Fields.Single(static field => field.Name == "durationMinutes")
            .ContractType.ShouldBe("WholeMinutes");
        command.Fields.Single(static field => field.Name == "aiMetrics")
            .HelpText.ShouldBe("AI metrics keep runtime, effort, and token units explicit.");
        command.Actions.Select(static action => action.Name).ShouldBe(
        [
            "record-time",
            "record-project-time",
            "record-work-time"
        ]);
        submission.Pattern.ShouldBe(TimesheetsCompositionPattern.FrontComposerGeneratedForm);
        submission.Fields.Select(static field => field.Name).ShouldBe(
        [
            "timeEntryIds",
            "timeEntrySubmissionId",
            "submissionScope",
            "approvalState",
            "blockingFields",
            "projectionFreshness",
            "partialSubmission",
            "persistentMessageBarState"
        ]);
        submission.Actions.Select(static action => action.Label).ShouldContain("Submit entries");
        submission.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(TimeEntryApprovalState));
        submission.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(ProjectionFreshnessState));
        command.StateBadges.Select(static badge => badge.StateVocabulary).ShouldBe(
        [
            nameof(TimeEntryApprovalState),
            nameof(BillableState),
            nameof(ContributorCategory),
            nameof(AiMetricAvailability),
            nameof(AiTokenMetricAvailability),
            nameof(AiEffortMetricSourceCategory),
            nameof(TimesheetsEvidenceRetentionCategory)
        ]);

        command.Fields.Select(static field => field.Name).ShouldContain("aiMetricSource");
        command.Fields.Select(static field => field.Name).ShouldContain("aiTokenAvailability");
        evidence.Fields.Select(static field => field.Name).ShouldContain("projectionFreshness");
        evidence.Fields.Select(static field => field.Name).ShouldContain("correctionState");
        evidence.Fields.Select(static field => field.Name).ShouldContain("sourceAuthority");
        evidence.Fields.Select(static field => field.Name).ShouldContain("eventLineage");
        evidence.Fields.Select(static field => field.Name).ShouldContain("displayHydration");
        evidence.Fields.Select(static field => field.Name).ShouldContain("aiWallClockDurationMilliseconds");
        evidence.Fields.Select(static field => field.Name).ShouldContain("aiModelRuntimeMilliseconds");
        evidence.Fields.Select(static field => field.Name).ShouldContain("aiBillableEffortMinutes");
        evidence.Fields.Select(static field => field.Name).ShouldContain("aiTokenAvailability");
        evidence.Fields.Select(static field => field.Name).ShouldContain("aiMetricSource");
        evidence.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(ProjectionFreshnessState));
        evidence.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(TimeEntryCorrectionState));
        evidence.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(TimeEntryEvidenceSourceAuthority));
        evidence.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(DisplayHydrationState));
        evidence.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(AiTokenMetricAvailability));
        evidence.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain(nameof(AiEffortMetricSourceCategory));
    }

    [Fact]
    public void Activity_type_catalog_read_model_exposes_text_status_and_selection_metadata()
    {
        ActivityTypeCatalogReadModel model = new(
            [
                new(
                    new ActivityTypeId("activity-type-1"),
                    ActivityTypeScope.Tenant,
                    null,
                    "Discovery",
                    false,
                    BillableState.Billable)
            ],
            ProjectionFreshnessMetadata.Stale("42"));

        string json = JsonSerializer.Serialize(model, JsonOptions);

        json.ShouldContain("\"statusText\":\"Inactive\"");
        json.ShouldContain("\"activeState\":\"Inactive\"");
        json.ShouldContain("\"isAvailableForCapture\":false");
        json.ShouldContain("\"state\":\"Stale\"");
        AssertJsonOmitsCallerAuthority(json);
    }

    [Fact]
    public void Openapi_ready_artifact_documents_safe_contract_surface_without_product_endpoints()
    {
        string artifactPath = RepositoryPath(
            "src",
            "Hexalith.Timesheets.Contracts",
            "openapi",
            "timesheets-capture-contracts.v1.json");
        JsonNode artifact = JsonNode.Parse(File.ReadAllText(artifactPath))
            ?? throw new InvalidOperationException("OpenAPI artifact could not be parsed.");

        artifact["openapi"]?.GetValue<string>().ShouldBe("3.1.0");
        artifact["info"]?["title"]?.GetValue<string>().ShouldBe("Hexalith Timesheets Capture Contracts");

        JsonObject paths = artifact["paths"]?.AsObject()
            ?? throw new InvalidOperationException("OpenAPI paths node is missing.");
        paths.Count.ShouldBe(0);

        JsonArray serverDerivedContext = artifact["x-hexalith-boundaries"]?["serverDerivedContext"]?.AsArray()
            ?? throw new InvalidOperationException("serverDerivedContext boundary metadata is missing.");
        serverDerivedContext.Select(static value => value?.GetValue<string>()).ShouldBe(
        [
            "tenant",
            "user",
            "correlation",
            "authorization"
        ]);

        JsonObject schemas = artifact["components"]?["schemas"]?.AsObject()
            ?? throw new InvalidOperationException("OpenAPI schemas node is missing.");

        schemas.ContainsKey("RecordTimeEntry").ShouldBeTrue();
        schemas.ContainsKey("SubmitTimeEntriesForApproval").ShouldBeTrue();
        schemas.ContainsKey("TimeEntrySubmitted").ShouldBeTrue();
        schemas.ContainsKey("TimeEntrySubmissionId").ShouldBeTrue();
        schemas.ContainsKey("TimeEntrySubmissionScope").ShouldBeTrue();
        schemas.ContainsKey("TimeEntryTargetReference").ShouldBeTrue();
        schemas.ContainsKey("AiEffortMetrics").ShouldBeTrue();
        schemas.ContainsKey("AiEffortMetricSourceMetadata").ShouldBeTrue();
        schemas.ContainsKey("ActivityTypeCatalogCommand").ShouldBeTrue();
        schemas.ContainsKey("CreateProjectActivityType").ShouldBeTrue();
        schemas.ContainsKey("RenameProjectActivityType").ShouldBeTrue();
        schemas.ContainsKey("UpdateProjectActivityTypeMetadata").ShouldBeTrue();
        schemas.ContainsKey("ConfigureProjectActivityTypeCatalogRestriction").ShouldBeTrue();
        schemas.ContainsKey("ActivityTypeCatalogReadModel").ShouldBeTrue();
        schemas.ContainsKey("TimeEntryEvidenceReadModel").ShouldBeTrue();
        schemas.ContainsKey("TimesheetsMetadataDescriptor").ShouldBeTrue();

        string schemaJson = schemas.ToJsonString();
        AssertJsonOmitsCallerAuthority(schemaJson, allowTenantId: true);
        schemaJson.ShouldContain("wall-clock execution time in milliseconds");
        schemaJson.ShouldContain("model or tool runtime in milliseconds");
        schemaJson.ShouldContain("Provider token counts are nullable when not reported");
        schemaJson.ShouldContain("AiTokenMetricAvailability");
        schemaJson.ShouldContain("AiEffortMetricSourceMetadata");
        schemaJson.ShouldContain("SubmitTimeEntriesForApproval");
        schemaJson.ShouldContain("TimeEntrySubmitted");
        schemaJson.ShouldNotContain("EventStore");
        schemaJson.ShouldNotContain("invoice", Case.Insensitive);
        schemaJson.ShouldNotContain("payroll", Case.Insensitive);
        schemaJson.ShouldNotContain("revenue", Case.Insensitive);
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

    private static TimesheetsMetadataDescriptor Descriptor(string name)
        => TimesheetsMetadataCatalog.Descriptors.Single(descriptor => descriptor.Name == name);

    private static string RepositoryPath(params string[] segments)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.Timesheets.slnx")))
            {
                return Path.Combine([directory.FullName, .. segments]);
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the Timesheets repository root.");
    }
}
