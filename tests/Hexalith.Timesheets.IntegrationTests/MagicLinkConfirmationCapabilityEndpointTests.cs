using Hexalith.Timesheets.Runtime;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed class MagicLinkConfirmationCapabilityEndpointTests
{
    [Fact]
    public void AppHostExportsTheInternalListenerPortToTheHostOptionsSection()
    {
        string appHost = File.ReadAllText(TestRepositoryRoot.PathTo("src", "Hexalith.Timesheets.AppHost", "Program.cs"));
        string host = File.ReadAllText(TestRepositoryRoot.PathTo("src", "Hexalith.Timesheets", "Program.cs"));
        InternalSurfaceConfigurationSyntax.Validate(appHost, host).ShouldBeEmpty();

        InternalSurfaceOptions.SectionName.ShouldBe("Timesheets:InternalSurface");
        new InternalSurfaceOptions().AllowOnAnyPort.ShouldBeFalse();
        new InternalSurfaceOptions().Port.ShouldBeNull();
        string environmentKey = InternalSurfaceOptions.SectionName.Replace(":", "__", StringComparison.Ordinal) + "__Port";
        environmentKey.ShouldBe("Timesheets__InternalSurface__Port");
    }

    [Fact]
    public void HostMapsNarrowMagicLinkConfirmationRoutesWithoutAuthorityBodyFields()
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
        endpoint.ShouldContain("/api/timesheets/magic-links/confirm");
        endpoint.ShouldContain("/api/timesheets/magic-links/confirm/submit");
        endpoint.ShouldContain("/api/timesheets/magic-links/adjust");
        endpoint.ShouldContain("/api/timesheets/magic-links/adjust/submit");
        endpoint.ShouldContain("IssueMagicLinkConfirmationCapability");
        endpoint.ShouldContain("RevokeMagicLinkConfirmationCapability");
        endpoint.ShouldContain("ExpireMagicLinkConfirmationCapability");
        endpoint.ShouldContain("ConfirmTimeThroughMagicLink");
        endpoint.ShouldContain("AdjustTimeThroughMagicLink");
        endpoint.ShouldContain("IMagicLinkConfirmationCapabilityStateLoader");
        endpoint.ShouldContain("LoadTokenStateAsync");
        endpoint.ShouldContain("TimesheetsServerRequestContext");
        endpoint.ShouldNotContain("command.Tenant");
        endpoint.ShouldNotContain("command.Actor");
        endpoint.ShouldNotContain("command.CorrelationId");
        endpoint.ShouldContain(".RequireAuthorization(EventStoreDomainServicePolicies.Process)");
        string withoutSdkRouteContracts = endpoint
            .Replace("using Hexalith.EventStore.DomainService;", string.Empty, StringComparison.Ordinal)
            .Replace("AllowEventStorePublicEndpoint", string.Empty, StringComparison.Ordinal)
            .Replace(".RequireAuthorization(EventStoreDomainServicePolicies.Process)", string.Empty, StringComparison.Ordinal);
        withoutSdkRouteContracts.ShouldNotContain("EventStore");
        endpoint.ShouldNotContain("inspect", Case.Insensitive);
        endpoint.ShouldNotContain("bearer", Case.Insensitive);
        endpoint.ShouldNotContain("rawToken", Case.Insensitive);
        endpoint.ShouldNotContain("tokenHash", Case.Insensitive);
    }

    [Fact]
    public void MagicLinkEndpointDenialCopyIsOpaque()
    {
        string endpoint = File.ReadAllText(TestRepositoryRoot.PathTo(
            "src",
            "Hexalith.Timesheets",
            "Endpoints",
            "MagicLinks",
            "MagicLinkConfirmationCapabilityEndpoints.cs"));

        endpoint.ShouldContain("MagicLinkInvalidLinkDenial.Default.Title");
        endpoint.ShouldContain("MagicLinkInvalidLinkDenial.Default.Detail");
        endpoint.ShouldContain("string? t");
        endpoint.ShouldNotContain("Magic-link adjustment request was not accepted.");
        endpoint.ShouldNotContain("Magic-link confirmation request was not accepted.");
        endpoint.ShouldNotContain("Project authority cannot be resolved.");
        endpoint.ShouldNotContain("Contributor authority cannot be resolved.");
        endpoint.ShouldNotContain("Activity Type was not found");
        endpoint.ShouldNotContain("expired-at-issue");
        endpoint.ShouldNotContain("Party display", Case.Insensitive);
        endpoint.ShouldNotContain("Project name", Case.Insensitive);
        endpoint.ShouldNotContain("Work name", Case.Insensitive);
    }

    [Fact]
    public void MagicLinkExternalRoutesShareOneDenialHelper()
    {
        string endpoint = File.ReadAllText(TestRepositoryRoot.PathTo(
            "src",
            "Hexalith.Timesheets",
            "Endpoints",
            "MagicLinks",
            "MagicLinkConfirmationCapabilityEndpoints.cs"));

        string[] denialParts = endpoint.Split("private static IResult Denied()", StringSplitOptions.None);
        denialParts.Length.ShouldBe(2);
        string denial = denialParts[1].Split("private static void SetPrivateLinkResponseHeaders", StringSplitOptions.None)[0];
        denial.Split("Results.Problem(", StringSplitOptions.None).Length.ShouldBe(2);
        denial.ShouldContain("MagicLinkInvalidLinkDenial.Default.Title");
        denial.ShouldContain("MagicLinkInvalidLinkDenial.Default.Detail");
        denial.ShouldContain("StatusCodes.Status403Forbidden");
        // Route delegates share the fixed helpers; they must not emit route-specific problem copy.
        denialParts[0].ShouldNotContain("Results.Problem(");
        string[] faultParts = endpoint.Split("private static IResult Faulted(", StringSplitOptions.None);
        faultParts.Length.ShouldBe(2);
        string fault = faultParts[1].Split("[LoggerMessage", StringSplitOptions.None)[0];
        fault.Split("Results.Problem(", StringSplitOptions.None).Length.ShouldBe(2);
        fault.ShouldContain("StatusCodes.Status500InternalServerError");
        fault.ShouldContain("Unable to process link.");
        fault.ShouldContain("The link request could not be completed.");
    }

}
