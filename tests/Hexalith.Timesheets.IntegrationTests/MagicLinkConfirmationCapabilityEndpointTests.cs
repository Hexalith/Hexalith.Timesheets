using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed class MagicLinkConfirmationCapabilityEndpointTests
{
    [Fact]
    public void Host_maps_narrow_magic_link_confirmation_routes_without_authority_body_fields()
    {
        string program = File.ReadAllText(TestRepositoryRoot.PathTo("src", "Hexalith.Timesheets", "Program.cs"));
        string endpoint = File.ReadAllText(TestRepositoryRoot.PathTo(
            "src",
            "Hexalith.Timesheets",
            "Endpoints",
            "MagicLinks",
            "MagicLinkConfirmationCapabilityEndpoints.cs"));

        program.ShouldContain("MapTimesheetsMagicLinkConfirmationCapabilityEndpoints");
        endpoint.ShouldContain("/api/timesheets/magic-links/confirmation-capabilities");
        endpoint.ShouldContain("/{capabilityId}/revoke");
        endpoint.ShouldContain("/{capabilityId}/expire");
        endpoint.ShouldContain("IssueMagicLinkConfirmationCapability");
        endpoint.ShouldContain("RevokeMagicLinkConfirmationCapability");
        endpoint.ShouldContain("ExpireMagicLinkConfirmationCapability");
        endpoint.ShouldContain("TimesheetsServerRequestContext");
        endpoint.ShouldNotContain("command.Tenant");
        endpoint.ShouldNotContain("command.Actor");
        endpoint.ShouldNotContain("command.CorrelationId");
        endpoint.ShouldNotContain("EventStore");
        endpoint.ShouldNotContain("inspect", Case.Insensitive);
        endpoint.ShouldNotContain("bearer", Case.Insensitive);
        endpoint.ShouldNotContain("rawToken", Case.Insensitive);
        endpoint.ShouldNotContain("tokenHash", Case.Insensitive);
    }

    [Fact]
    public void Magic_link_endpoint_denial_copy_is_opaque()
    {
        string endpoint = File.ReadAllText(TestRepositoryRoot.PathTo(
            "src",
            "Hexalith.Timesheets",
            "Endpoints",
            "MagicLinks",
            "MagicLinkConfirmationCapabilityEndpoints.cs"));

        endpoint.ShouldContain("Magic-link confirmation request was not accepted.");
        endpoint.ShouldNotContain("Project authority cannot be resolved.");
        endpoint.ShouldNotContain("Contributor authority cannot be resolved.");
        endpoint.ShouldNotContain("Activity Type was not found");
        endpoint.ShouldNotContain("expired-at-issue");
        endpoint.ShouldNotContain("Party display", Case.Insensitive);
        endpoint.ShouldNotContain("Project name", Case.Insensitive);
        endpoint.ShouldNotContain("Work name", Case.Insensitive);
    }
}
