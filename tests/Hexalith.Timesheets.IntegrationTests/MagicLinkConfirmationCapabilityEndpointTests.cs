using System.Text.RegularExpressions;

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
        // Commented-out wiring must not satisfy the source fitness assertions.
        appHost = RemoveComments(appHost);
        host = RemoveComments(host);

        // Count configuration-key prefixes across separators and command-line arguments.
        Regex.Matches(appHost, @"Timesheets(?:__|:)InternalSurface(?:__|:)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count.ShouldBe(1);
        appHost.ShouldNotContain("AllowOnAnyPort", Case.Insensitive);
        host.ShouldNotContain("AllowOnAnyPort", Case.Insensitive);

        InternalSurfaceOptions.SectionName.ShouldBe("Timesheets:InternalSurface");
        string environmentKey = InternalSurfaceOptions.SectionName.Replace(":", "__", StringComparison.Ordinal) + "__Port";
        environmentKey.ShouldBe("Timesheets__InternalSurface__Port");

        // Keep the listener values aligned with the configured-port HTTP fixture.
        appHost.ShouldNotMatch(@"(?m)^\s*#\s*(?:if|elif|else|endif)\b");
        host.ShouldNotMatch(@"(?m)^\s*#\s*(?:if|elif|else|endif)\b");
        appHost.ShouldMatch(@"(?m)^\s*const\s+int\s+PublicPort\s*=\s*8080\s*;");
        appHost.ShouldMatch(@"(?m)^\s*const\s+int\s+InternalPort\s*=\s*8081\s*;");
        Match resource = Regex.Match(appHost, @"\.AddProject<Projects\.Hexalith_Timesheets>\s*\(\s*""timesheets""\s*\)(?<chain>[^;]*);");
        resource.Success.ShouldBeTrue("The Timesheets resource must declare its listener configuration.");
        string chain = resource.Groups["chain"].Value;
        chain.ShouldMatch(@"\.WithHttpEndpoint\s*\(\s*name\s*:\s*""public""\s*,\s*port\s*:\s*PublicPort\s*,\s*isProxied\s*:\s*false\s*\)");
        chain.ShouldMatch(@"\.WithHttpEndpoint\s*\(\s*name\s*:\s*""internal""\s*,\s*port\s*:\s*InternalPort\s*,\s*isProxied\s*:\s*false\s*\)");
        chain.ShouldMatch($@"\.WithEnvironment\s*\(\s*""{Regex.Escape(environmentKey)}""\s*,\s*InternalPort\.ToString\s*\(\s*System\.Globalization\.CultureInfo\.InvariantCulture\s*\)\s*\)");
        host.ShouldMatch(@"\.Configure<InternalSurfaceOptions>\s*\(\s*builder\.Configuration\.GetSection\s*\(\s*InternalSurfaceOptions\.SectionName\s*\)\s*\)");
    }

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
    public void Magic_link_external_routes_share_one_denial_helper()
    {
        string endpoint = File.ReadAllText(TestRepositoryRoot.PathTo(
            "src",
            "Hexalith.Timesheets",
            "Endpoints",
            "MagicLinks",
            "MagicLinkConfirmationCapabilityEndpoints.cs"));

        endpoint.Split("private static IResult Denied()", StringSplitOptions.None).Length.ShouldBe(2);
        endpoint.Split("Results.Problem(", StringSplitOptions.None).Length.ShouldBe(2);
    }

    private static string RemoveComments(string source)
    {
        // Match literals first so URLs and comment markers inside them remain source text.
        return Regex.Replace(
            source,
            @"(?<raw>""{3,})[\s\S]*?\k<raw>|@\$*""(?:""""|[^""])*""|""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])'|//[^\r\n]*|/\*[\s\S]*?\*/",
            static match => match.Value.StartsWith("//", StringComparison.Ordinal) || match.Value.StartsWith("/*", StringComparison.Ordinal)
                ? string.Empty
                : match.Value);
    }
}
