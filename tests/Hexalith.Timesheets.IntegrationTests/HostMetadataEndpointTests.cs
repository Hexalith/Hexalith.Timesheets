using System.Text.Json;

using Hexalith.Timesheets.Contracts;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed class HostMetadataEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Host_metadata_api_contract_declares_successful_module_descriptor_response()
    {
        string program = File.ReadAllText(TestRepositoryRoot.PathTo("src", "Hexalith.Timesheets", "Program.cs"));

        program.ShouldContain("MapGet");
        program.ShouldContain("/metadata/timesheets");
        program.ShouldContain("Results.Ok");
        program.ShouldContain("Module = \"Hexalith.Timesheets\"");
        program.ShouldContain("ContractVersion = \"1.0\"");
        program.ShouldContain("Capabilities = TimesheetsMetadataCatalog.Descriptors");
        program.ShouldContain("MetadataDescriptors = TimesheetsMetadataCatalog.Descriptors");
        program.ShouldNotContain("TenantId");
        program.ShouldNotContain("Token");
        program.ShouldNotContain("TimeEntryId");
        program.ShouldNotContain("PartyId");
        program.ShouldNotContain("ProjectId");
        program.ShouldNotContain("WorkId");
    }

    [Fact]
    public void Host_metadata_api_contract_exports_activity_type_catalog_descriptors()
    {
        string[] capabilities = TimesheetsMetadataCatalog.Descriptors
            .Select(static descriptor => descriptor.Capability)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string[] metadataDescriptors = TimesheetsMetadataCatalog.Descriptors
            .Select(static descriptor => descriptor.Name)
            .ToArray();

        capabilities.ShouldContain("catalog");
        metadataDescriptors.ShouldContain("timesheets.command.activity-type-catalog");
        metadataDescriptors.ShouldContain("timesheets.projection.activity-type-catalog");
    }

    [Fact]
    public void Host_metadata_api_contract_exports_actual_time_report_descriptors()
    {
        string[] metadataDescriptors = TimesheetsMetadataCatalog.Descriptors
            .Select(static descriptor => descriptor.Name)
            .ToArray();

        metadataDescriptors.ShouldContain("timesheets.projection.project-actual-time-report");
        metadataDescriptors.ShouldContain("timesheets.projection.work-actual-time-report");
        metadataDescriptors.ShouldContain("timesheets.dashboard.overview");

        string json = JsonSerializer.Serialize(
            TimesheetsMetadataCatalog.Descriptors
                .Where(static descriptor => descriptor.Name.Contains("actual-time-report", StringComparison.Ordinal)),
            JsonOptions);

        json.ShouldContain("filter", Case.Insensitive);
        json.ShouldContain("Actual minutes");
        json.ShouldContain("Projection freshness");
        json.ShouldNotContain("EventStore");
        json.ShouldNotContain("invoice", Case.Insensitive);
        json.ShouldNotContain("payroll", Case.Insensitive);
    }

    [Fact]
    public void Host_metadata_api_contract_exports_submit_time_entries_descriptor()
    {
        var submitDescriptor = TimesheetsMetadataCatalog.Descriptors
            .Single(static descriptor => descriptor.Name == "timesheets.command.submit-time-entries");

        submitDescriptor.Capability.ShouldBe("submission");
        submitDescriptor.Fields.Select(static field => field.Name).ShouldBe(
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
        submitDescriptor.Actions.Select(static action => action.Label).ShouldContain("Submit entries");
        submitDescriptor.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain("TimeEntryApprovalState");
        submitDescriptor.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain("ProjectionFreshnessState");

        string json = JsonSerializer.Serialize(submitDescriptor, JsonOptions);

        json.ShouldContain("Submit entries");
        json.ShouldContain("persistentMessageBarState");
        AssertJsonOmitsCallerAuthority(json);
    }

    [Fact]
    public void Host_metadata_api_contract_exports_external_contribution_descriptor()
    {
        var descriptor = TimesheetsMetadataCatalog.Descriptors
            .Single(static descriptor => descriptor.Name == "timesheets.command.external-contribution");

        descriptor.Capability.ShouldBe("external-contribution");
        descriptor.Fields.Select(static field => field.Name).ShouldBe(
        [
            "source",
            "timeEntry",
            "target",
            "contributor",
            "activityType",
            "serviceDate",
            "durationMinutes",
            "billableState",
            "confirmation",
            "approvalState",
            "projectionFreshness"
        ]);
        descriptor.Actions.Select(static action => action.Label).ShouldBe(["Submit external time", "Confirm time"]);
        descriptor.StateBadges.Select(static badge => badge.Label).ShouldContain("External contributor");

        string json = JsonSerializer.Serialize(descriptor, JsonOptions);

        json.ShouldContain("Confirmation recorded");
        AssertJsonOmitsCallerAuthority(json);
        json.ShouldNotContain("invoice", Case.Insensitive);
        json.ShouldNotContain("payroll", Case.Insensitive);
    }

    [Fact]
    public void Host_metadata_api_contract_exports_correct_rejected_entry_descriptor()
    {
        var correctionDescriptor = TimesheetsMetadataCatalog.Descriptors
            .Single(static descriptor => descriptor.Name == "timesheets.command.correct-rejected-time-entry");

        correctionDescriptor.Capability.ShouldBe("correction");
        correctionDescriptor.Fields.Select(static field => field.Name).ShouldContain("rejectionReason");
        correctionDescriptor.Fields.Select(static field => field.Name).ShouldContain("correctionState");
        correctionDescriptor.Actions.Select(static action => action.Label).ShouldContain("Correct entry");
        correctionDescriptor.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain("TimeEntryCorrectionState");

        string json = JsonSerializer.Serialize(correctionDescriptor, JsonOptions);

        json.ShouldContain("Correct entry");
        json.ShouldContain("rejectionReason");
        AssertJsonOmitsCallerAuthority(json);
    }

    [Fact]
    public void Host_metadata_api_contract_exports_operational_time_entry_query_descriptor()
    {
        var queryDescriptor = TimesheetsMetadataCatalog.Descriptors
            .Single(static descriptor => descriptor.Name == "timesheets.projection.time-entry-query");

        queryDescriptor.Capability.ShouldBe("evidence");
        queryDescriptor.Pattern.ToString().ShouldBe("FrontComposerProjectionView");
        queryDescriptor.Fields.Select(static field => field.Name).ShouldContain("periodFilter");
        queryDescriptor.Fields.Select(static field => field.Name).ShouldContain("projectionFreshness");
        queryDescriptor.Actions.Select(static action => action.Label).ShouldContain("Open evidence");
        queryDescriptor.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain("ProjectionFreshnessState");
        queryDescriptor.StateBadges.Select(static badge => badge.StateVocabulary).ShouldContain("TimeEntrySourceType");

        string json = JsonSerializer.Serialize(queryDescriptor, JsonOptions);

        json.ShouldContain("Query entries");
        json.ShouldContain("Tenant-local period or date range.");
        AssertJsonOmitsCallerAuthority(json);
        json.ShouldNotContain("EventStore");
        json.ShouldNotContain("stream");
    }

    [Fact]
    public void Host_metadata_api_catalog_response_omits_authority_and_payload_identifiers()
    {
        var response = new
        {
            Module = "Hexalith.Timesheets",
            ContractVersion = "1.0",
            Capabilities = TimesheetsMetadataCatalog.Descriptors
                .Select(static descriptor => descriptor.Capability)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            MetadataDescriptors = TimesheetsMetadataCatalog.Descriptors
                .Select(static descriptor => descriptor.Name)
                .ToArray()
        };

        string json = JsonSerializer.Serialize(response, JsonOptions);

        json.ShouldContain("Hexalith.Timesheets");
        json.ShouldContain("timesheets.command.activity-type-catalog");
        json.ShouldContain("timesheets.projection.activity-type-catalog");
        json.ShouldContain("timesheets.command.correct-rejected-time-entry");
        json.ShouldContain("timesheets.projection.time-entry-query");
        json.ShouldContain("timesheets.projection.project-actual-time-report");
        json.ShouldContain("timesheets.projection.work-actual-time-report");
        AssertJsonOmitsCallerAuthority(json);
        json.ShouldNotContain("timeEntryId");
        json.ShouldNotContain("activityTypeId");
        json.ShouldNotContain("partyId");
        json.ShouldNotContain("projectId");
        json.ShouldNotContain("workId");
    }

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
}
