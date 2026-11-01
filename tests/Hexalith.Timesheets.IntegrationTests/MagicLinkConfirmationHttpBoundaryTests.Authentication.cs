using System.Net;
using System.Security.Claims;
using System.Text.Json;

using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Timesheets.Contracts;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Runtime;

using Microsoft.AspNetCore.Http;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed partial class MagicLinkConfirmationHttpBoundaryTests
{
    [Fact]
    public async Task ComposedHostInventoryNamesExactlyOneCredentialSchemePerSurface()
    {
        using MagicLinkHttpBoundaryFactory factory = new();
        using HttpClient client = factory.CreateClient();
        EndpointDataSource source = factory.Services.GetRequiredService<EndpointDataSource>();
        (await EventStoreHttpEndpointInventory.ValidateAsync(source.Endpoints, factory.Services)).ShouldBeEmpty();
        IAuthorizationPolicyProvider policies = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        string[] routes =
        [
            "/metadata/timesheets",
            "/api/timesheets/external-contributions/",
            "/api/timesheets/external-contributions/{timeEntryId}/confirm",
            "/admin/timesheets/magic-link-index/migrate-v6",
            "/admin/timesheets/magic-link-index/migrate-v1",
            "/api/timesheets/magic-links/confirm",
            "/api/timesheets/magic-links/confirm/submit",
            "/api/timesheets/magic-links/adjust",
            "/api/timesheets/magic-links/adjust/submit"
        ];
        foreach (string route in routes)
        {
            RouteEndpoint endpoint = source.Endpoints.OfType<RouteEndpoint>()
                .Single(endpoint => endpoint.RoutePattern.RawText == route);
            endpoint.Metadata.GetMetadata<IAllowAnonymous>().ShouldBeNull();
            string policyName = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .ShouldHaveSingleItem().Policy.ShouldNotBeNull();
            bool capability = route.StartsWith("/api/timesheets/magic-links/", StringComparison.Ordinal);
            bool contribution = route.StartsWith("/api/timesheets/external-contributions/", StringComparison.Ordinal);
            policyName.ShouldBe(capability ? "TimesheetsMagicLinkCapability"
                : contribution ? "EventStoreWorkload:any" : "EventStoreHuman");
            (await policies.GetPolicyAsync(policyName)).ShouldNotBeNull().AuthenticationSchemes
                .ShouldBe([capability ? "TimesheetsMagicLinkCapability"
                    : contribution ? "EventStoreWorkload" : "Bearer"]);
        }
        (await factory.Services.GetRequiredService<IAuthenticationSchemeProvider>().GetDefaultAuthenticateSchemeAsync())
            .ShouldBeNull();
        AuthenticationOptions authentication = factory.Services.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        authentication.DefaultScheme.ShouldBeNull();
        authentication.DefaultChallengeScheme.ShouldBeNull();
        factory.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value.DefaultPolicy
            .AuthenticationSchemes.ShouldBe(["Bearer"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CapabilityAuthenticationReusesOneCanonicalLoadWithoutInventingAnActor(bool adjust)
    {
        string token = adjust ? ValidAdjustToken() : ValidConfirmToken();
        IMagicLinkConfirmationCapabilityStateLoader loader = Substitute.For<IMagicLinkConfirmationCapabilityStateLoader>();
        loader.LoadTokenStateAsync(token, Arg.Any<CancellationToken>()).Returns(new MagicLinkEndpointTokenState(
            IssuedState(token, adjust ? MagicLinkAllowedAction.Adjust : MagicLinkAllowedAction.Confirm),
            RecordedExternalState(), FreshCatalog()));
        using MagicLinkHttpBoundaryFactory factory = new(stateLoader: loader);
        using HttpClient client = factory.CreateClient();
        string route = adjust ? "adjust" : "confirm";
        using HttpResponseMessage response = await client.GetAsync(
            $"/api/timesheets/magic-links/{route}?t={token}", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ClaimsPrincipal principal = factory.AuthenticatedPrincipal.ShouldNotBeNull();
        System.Security.Principal.IIdentity identity = principal.Identity.ShouldNotBeNull();
        identity.IsAuthenticated.ShouldBeTrue();
        identity.AuthenticationType.ShouldBe("TimesheetsMagicLinkCapability");
        principal.Claims.ShouldBeEmpty();
        await loader.Received(1).LoadTokenStateAsync(token, Arg.Any<CancellationToken>());
        // Domain authorization retains the canonical contributor context. Only the HTTP
        // principal supplies outbound actor claims, so capability authentication cannot mint one.
        factory.AccessGuard.LastAuthorizationRequest.ShouldNotBeNull().Context.Actor.ShouldBe(Contributor());
        IWorkloadAssertionIssuer issuer = Substitute.For<IWorkloadAssertionIssuer>();
        issuer.CanBindResources.Returns(true);
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };
        await new EventStoreGatewayWorkloadAssertionSource(issuer, accessor).IssueAsync(
            "test-operation", Tenant().TenantId, TestContext.Current.CancellationToken);
        await issuer.Received(1).IssueAsync(Arg.Is<WorkloadAssertionRequest>(request => request.Bindings != null
            && !request.Bindings.ContainsKey(EventStoreWorkloadAuthenticationDefaults.ActorBindingClaimType)),
            Arg.Any<CancellationToken>());
        factory.Gateway.SubmissionCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(false, "application/json", HttpStatusCode.BadRequest)]
    [InlineData(true, "application/json", HttpStatusCode.BadRequest)]
    [InlineData(false, "text/plain", HttpStatusCode.UnsupportedMediaType)]
    [InlineData(true, "text/plain", HttpStatusCode.UnsupportedMediaType)]
    public async Task SubmitBindingRefusesInvalidBodiesBeforeAnyCapabilityLookup(
        bool adjust, string contentType, HttpStatusCode expected)
    {
        IMagicLinkConfirmationCapabilityStateLoader loader = Substitute.For<IMagicLinkConfirmationCapabilityStateLoader>();
        using MagicLinkHttpBoundaryFactory factory = new(stateLoader: loader);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.PostAsync(
            $"/api/timesheets/magic-links/{(adjust ? "adjust" : "confirm")}/submit?t=unknown-query-secret",
            new StringContent("{ invalid json", System.Text.Encoding.UTF8, contentType), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(expected);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldNotContain("unknown-query-secret");
        await loader.DidNotReceive().LoadTokenStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        factory.Gateway.SubmissionCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HumanCredentialsCannotSubstituteForAnOpaqueCapability(bool adjust)
    {
        using MagicLinkHttpBoundaryFactory factory = new();
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", HttpBoundaryHumanCredential.Create("human-reader"));
        string route = adjust ? "adjust" : "confirm";
        using HttpResponseMessage response = await client.GetAsync(
            $"/api/timesheets/magic-links/{route}", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
        response.Headers.GetValues("Referrer-Policy").ShouldBe(["no-referrer"]);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument problem = JsonDocument.Parse(body);
        problem.RootElement.GetProperty("title").GetString().ShouldBe(
            Hexalith.Timesheets.Contracts.Models.MagicLinks.MagicLinkInvalidLinkDenial.Default.Title);
        problem.RootElement.GetProperty("detail").GetString().ShouldBe(
            Hexalith.Timesheets.Contracts.Models.MagicLinks.MagicLinkInvalidLinkDenial.Default.Detail);
        body.ShouldNotContain("human-reader");
        factory.AccessGuard.AuthorizationCount.ShouldBe(0);
        factory.Gateway.SubmissionCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MetadataRequiresTheExistingHumanCredentialAndPreservesTheDescriptor(bool workload)
    {
        using MagicLinkHttpBoundaryFactory factory = new();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage anonymous = await client.GetAsync("/metadata/timesheets", TestContext.Current.CancellationToken);
        anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldNotContain("Hexalith.Timesheets");
        using HttpResponseMessage capability = await client.GetAsync(
            $"/metadata/timesheets?t={ValidConfirmToken()}", TestContext.Current.CancellationToken);
        capability.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        client.DefaultRequestHeaders.Authorization = new("Bearer", HttpBoundaryHumanCredential.Create("metadata-reader", workload));
        using HttpResponseMessage authenticated = await client.GetAsync("/metadata/timesheets", TestContext.Current.CancellationToken);
        authenticated.StatusCode.ShouldBe(workload ? HttpStatusCode.Forbidden : HttpStatusCode.OK);
        string body = await authenticated.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        if (workload)
        {
            body.ShouldNotContain("Hexalith.Timesheets");
            return;
        }
        using JsonDocument json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("module").GetString().ShouldBe("Hexalith.Timesheets");
        json.RootElement.GetProperty("contractVersion").GetString().ShouldBe("1.0");
        json.RootElement.GetProperty("capabilities").EnumerateArray().Select(item => item.GetString())
            .ShouldBe(TimesheetsMetadataCatalog.Descriptors.Select(descriptor => descriptor.Capability).Distinct(StringComparer.Ordinal));
        json.RootElement.GetProperty("metadataDescriptors").EnumerateArray().Select(item => item.GetString())
            .ShouldBe(TimesheetsMetadataCatalog.Descriptors.Select(descriptor => descriptor.Name));
    }

    [Theory]
    [InlineData("/api/timesheets/external-contributions/")]
    [InlineData("/api/timesheets/external-contributions/entry/confirm")]
    public async Task ExternalContributionRequiresWorkloadCredentialsBeforeItsDomainGate(string route)
    {
        using MagicLinkHttpBoundaryFactory factory = new();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage anonymous = await client.PostAsync(route,
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);
        anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        client.DefaultRequestHeaders.Authorization = new("Bearer", HttpBoundaryHumanCredential.Create("human-contributor"));
        using HttpResponseMessage human = await client.PostAsync(route,
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);
        human.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        factory.AccessGuard.AuthorizationCount.ShouldBe(0);
        factory.Gateway.SubmissionCount.ShouldBe(0);
    }
}
