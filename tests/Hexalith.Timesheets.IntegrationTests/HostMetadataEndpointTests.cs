using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed class HostMetadataEndpointTests
{
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
}
