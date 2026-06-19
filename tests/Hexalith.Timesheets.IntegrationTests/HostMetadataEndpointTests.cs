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
