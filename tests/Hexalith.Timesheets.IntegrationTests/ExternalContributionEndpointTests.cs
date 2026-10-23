using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed class ExternalContributionEndpointTests
{
    [Fact]
    public void HostMapsNarrowExternalContributionRoutesWithoutAuthorityBodyFields()
    {
        string program = File.ReadAllText(TestRepositoryRoot.PathTo("src", "Hexalith.Timesheets", "Program.cs"));
        string endpoint = File.ReadAllText(TestRepositoryRoot.PathTo(
            "src",
            "Hexalith.Timesheets",
            "Endpoints",
            "ExternalContributionEndpoints.cs"));

        program.ShouldContain("MapTimesheetsExternalContributionEndpoints");
        endpoint.ShouldContain("/api/timesheets/external-contributions");
        endpoint.ShouldContain("/api/timesheets/external-contributions");
        endpoint.ShouldContain("/{timeEntryId}/confirm");
        endpoint.ShouldContain("SubmitExternalTimeEntry");
        endpoint.ShouldContain("ConfirmExternalTimeEntry");
        endpoint.ShouldContain("TimesheetsServerRequestContext");
        endpoint.ShouldNotContain("command.Tenant");
        endpoint.ShouldNotContain("command.Actor");
        endpoint.ShouldNotContain("command.CorrelationId");
        endpoint.ShouldNotContain("EventStore");
        endpoint.ShouldNotContain("bearer", Case.Insensitive);
        endpoint.ShouldNotContain("rawToken", Case.Insensitive);
    }

    [Fact]
    public void ExternalContributionEndpointDenialCopyIsOpaque()
    {
        string endpoint = File.ReadAllText(TestRepositoryRoot.PathTo(
            "src",
            "Hexalith.Timesheets",
            "Endpoints",
            "ExternalContributionEndpoints.cs"));

        endpoint.ShouldContain("External contribution request was not accepted.");
        endpoint.ShouldNotContain("Project authority cannot be resolved.");
        endpoint.ShouldNotContain("Contributor authority cannot be resolved.");
        endpoint.ShouldNotContain("Party display", Case.Insensitive);
        endpoint.ShouldNotContain("Project name", Case.Insensitive);
        endpoint.ShouldNotContain("Work name", Case.Insensitive);
    }
}
