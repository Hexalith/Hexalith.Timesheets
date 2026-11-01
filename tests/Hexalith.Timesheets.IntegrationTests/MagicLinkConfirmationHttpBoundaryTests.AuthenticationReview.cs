using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;

using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed partial class MagicLinkConfirmationHttpBoundaryTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task SubmitVariantsCommitTheCorrectCommandWithOneCanonicalCredentialLoad(
        bool adjust, bool upperCase, bool trailingSlash)
    {
        IMagicLinkConfirmationCapabilityStateLoader loader = Substitute.For<IMagicLinkConfirmationCapabilityStateLoader>();
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, stateLoader: loader);
        async Task<MagicLinkEndpointTokenState> LoadAsync(string token, CancellationToken cancellationToken)
        {
            using IServiceScope scope = factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<EventStoreMagicLinkConfirmationCapabilityStateLoader>()
                .LoadTokenStateAsync(token, cancellationToken);
        }
        loader.LoadTokenStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
            new ValueTask<MagicLinkEndpointTokenState>(LoadAsync(call.Arg<string>(), call.Arg<CancellationToken>())));
        using HttpClient client = factory.CreateClient();
        string token = adjust ? ValidAdjustToken() : ValidConfirmToken();
        var entry = new TimeEntryId("variant-entry");
        await factory.ProjectValidStateAsync(client, token,
            adjust ? MagicLinkAllowedAction.Adjust : MagicLinkAllowedAction.Confirm,
            new MagicLinkCapabilityId("variant-capability"), entry);
        string path = $"/api/timesheets/magic-links/{(adjust ? "adjust" : "confirm")}/submit";
        if (upperCase) path = path.ToUpperInvariant();
        if (trailingSlash) path += "/";
        using HttpResponseMessage response = await client.PostAsJsonAsync(path + "?t=" + token,
            adjust ? (object)AdjustCommand() : new ConfirmTimeThroughMagicLink(), JsonOptions,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
        response.Headers.GetValues("Referrer-Policy").ShouldBe(["no-referrer"]);
        factory.AuthenticatedPrincipal.ShouldNotBeNull().Claims.ShouldBeEmpty();
        await loader.Received(1).LoadTokenStateAsync(token, Arg.Any<CancellationToken>());
        factory.Gateway.SubmissionCount.ShouldBe(1);
        StreamReadEvent[] stored = factory.Gateway.StoredEvents(Tenant().TenantId, entry.Value);
        stored.Length.ShouldBe(3);
        stored[^1].EventTypeName.ShouldBe(adjust ? typeof(StoredTimeEntryAdjusted).FullName : typeof(StoredTimeEntryConfirmed).FullName);
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(false, "application/json")]
    [InlineData(false, "text/plain")]
    [InlineData(true, null)]
    [InlineData(true, "application/json")]
    [InlineData(true, "text/plain")]
    public async Task BodylessSubmitIsBadRequestBeforeMediaValidationOrCapabilityLookup(bool adjust, string? contentType)
    {
        IMagicLinkConfirmationCapabilityStateLoader loader = Substitute.For<IMagicLinkConfirmationCapabilityStateLoader>();
        using MagicLinkHttpBoundaryFactory factory = new(stateLoader: loader);
        using HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/timesheets/magic-links/{(adjust ? "adjust" : "confirm")}/submit?t=query-secret-probe");
        if (contentType is not null)
        {
            request.Content = new ByteArrayContent([]);
            request.Content.Headers.ContentType = new(contentType);
        }
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldNotContain("query-secret-probe");
        await loader.DidNotReceive().LoadTokenStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        factory.Gateway.SubmissionCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SubmitBodySizeFailureRetainsPlatformStatusWithoutCapabilityLookup(bool adjust)
    {
        IMagicLinkConfirmationCapabilityStateLoader loader = Substitute.For<IMagicLinkConfirmationCapabilityStateLoader>();
        using MagicLinkHttpBoundaryFactory factory = new(stateLoader: loader);
        using var body = new HttpBoundaryReadFailureStream(new BadHttpRequestException("private-body-material", 413));
        HttpContext context = await factory.Server.SendAsync(context =>
        {
            context.Request.Method = "POST";
            context.Request.Path = $"/api/timesheets/magic-links/{(adjust ? "adjust" : "confirm")}/submit";
            context.Request.QueryString = new("?t=query-secret-probe");
            context.Request.ContentType = "application/json";
            context.Request.ContentLength = 1;
            context.Request.Body = body;
            context.Features.Set<IHttpRequestBodyDetectionFeature>(new HttpBoundaryRequestBodyDetectionFeature());
        }, TestContext.Current.CancellationToken);
        context.Response.StatusCode.ShouldBe(StatusCodes.Status413PayloadTooLarge);
        foreach (LogRecord record in factory.Logs.Records)
        {
            AssertSensitiveDiagnosticsAbsent(record, "query-secret-probe");
            record.Message.ShouldNotContain("private-body-material");
            record.Exception?.ShouldNotContain("private-body-material");
        }
        await loader.DidNotReceive().LoadTokenStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        factory.Gateway.SubmissionCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SubmitBodyCancellationPropagatesBeforeCapabilityLookup(bool adjust)
    {
        IMagicLinkConfirmationCapabilityStateLoader loader = Substitute.For<IMagicLinkConfirmationCapabilityStateLoader>();
        using var body = new HttpBoundaryReadFailureStream();
        using MagicLinkHttpBoundaryFactory original = new(stateLoader: loader);
        using var factory = original.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IStartupFilter>(new HttpBoundaryBodyOverrideStartupFilter(body))));
        using HttpClient client = factory.CreateClient();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Task<HttpResponseMessage> pending = client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/{(adjust ? "adjust" : "confirm")}/submit?t=query-secret-probe",
            new ConfirmTimeThroughMagicLink(), cancellation.Token);
        await body.Started.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => pending);
        await body.Canceled.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        await loader.DidNotReceive().LoadTokenStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        original.Logs.Records.Where(record => record.Level >= Microsoft.Extensions.Logging.LogLevel.Warning
            && record.Category.Contains("MagicLink", StringComparison.Ordinal)).ShouldBeEmpty();
        original.Gateway.SubmissionCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("confirm", "confirm-display", false)]
    [InlineData("confirm/submit", "confirm-submit", true)]
    [InlineData("adjust", "adjust-display", false)]
    [InlineData("adjust/submit", "adjust-submit", true)]
    public async Task VariantCredentialFaultUsesCanonicalOperationAndOpaqueResponse(string route, string operation, bool post)
    {
        IMagicLinkConfirmationCapabilityStateLoader loader = Substitute.For<IMagicLinkConfirmationCapabilityStateLoader>();
        loader.LoadTokenStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException<MagicLinkEndpointTokenState>(new IOException("private-fault-material")));
        using MagicLinkHttpBoundaryFactory factory = new(stateLoader: loader);
        using HttpClient client = factory.CreateClient();
        string path = ("/api/timesheets/magic-links/" + route).ToUpperInvariant() + "/?t=query-secret-probe";
        using HttpResponseMessage response = post
            ? await client.PostAsJsonAsync(path, route.StartsWith("confirm", StringComparison.Ordinal)
                ? (object)new ConfirmTimeThroughMagicLink() : AdjustCommand(), JsonOptions, TestContext.Current.CancellationToken)
            : await client.GetAsync(path, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
        response.Headers.GetValues("Referrer-Policy").ShouldBe(["no-referrer"]);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        AssertProtectedResponseMaterialAbsent(body, "query-secret-probe");
        using JsonDocument problem = JsonDocument.Parse(body);
        problem.RootElement.GetProperty("title").GetString().ShouldBe("Unable to process link.");
        problem.RootElement.GetProperty("detail").GetString().ShouldBe("The link request could not be completed.");
        LogRecord fault = factory.Logs.Records.Single(record => record.Level == Microsoft.Extensions.Logging.LogLevel.Error);
        fault.State.ShouldContain("Operation=" + operation);
        fault.Exception.ShouldBeNull();
        AssertSensitiveDiagnosticsAbsent(fault, "query-secret-probe");
        factory.Gateway.SubmissionCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SubmissionDiscoveryRetainsTheRequiredCommandSchemaWithoutMediaRouteReplacement(bool adjust)
    {
        using MagicLinkHttpBoundaryFactory original = new();
        using var factory = original.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddEndpointsApiExplorer()));
        using HttpClient client = factory.CreateClient();
        string path = $"/api/timesheets/magic-links/{(adjust ? "adjust" : "confirm")}/submit";
        Type expectedType = adjust ? typeof(AdjustTimeThroughMagicLink) : typeof(ConfirmTimeThroughMagicLink);
        RouteEndpoint endpoint = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>().Single(item => item.RoutePattern.RawText == path);
        IAcceptsMetadata accepts = endpoint.Metadata.GetMetadata<IAcceptsMetadata>().ShouldNotBeNull();
        accepts.RequestType.ShouldBe(expectedType);
        accepts.IsOptional.ShouldBeFalse();
        accepts.ContentTypes.ShouldBeEmpty();
        var description = factory.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups.Items.SelectMany(group => group.Items).Single(item => item.RelativePath == path.TrimStart('/'));
        var body = description.ParameterDescriptions.Single(item => item.Source == BindingSource.Body);
        body.Type.ShouldBe(expectedType);
        body.IsRequired.ShouldBeTrue();
        // The framework's JSON OpenAPI body generator supplies application/json when
        // the discovered body has no explicit request formats, without media-type routing.
        description.SupportedRequestFormats.ShouldBeEmpty();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        string schema = options.GetJsonSchemaAsNode(body.Type).ToJsonString();
        if (adjust)
        {
            schema.ShouldContain("durationMinutes");
            schema.ShouldContain("activityTypeId");
        }
        else
        {
            schema.ShouldNotContain("durationMinutes");
            schema.ShouldNotContain("activityTypeId");
        }
    }

    [Theory]
    [InlineData("Development", true, false, true)]
    [InlineData("Development", false, false, false)]
    [InlineData("Staging", true, false, false)]
    [InlineData("Staging", true, true, true)]
    [InlineData("Production", true, true, false)]
    public void HumanStartupValidationIsIndependentOfValidDedicatedWorkloadConfiguration(
        string environment, bool humanConfigured, bool allowInsecure, bool starts)
    {
        using MagicLinkHttpBoundaryFactory original = new();
        using var factory = original.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            var settings = new Dictionary<string, string>
            {
                ["Authentication:JwtBearer:SigningKey"] = humanConfigured ? Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(new string('k', 32))) : "",
                ["Authentication:JwtBearer:Issuer"] = humanConfigured ? "timesheets-test-issuer" : "",
                ["Authentication:JwtBearer:Audience"] = humanConfigured ? "hexalith-eventstore" : "",
                ["Authentication:JwtBearer:AllowInsecureSymmetricKey"] = allowInsecure.ToString(),
                ["Authentication:WorkloadJwtBearer:Authority"] = "https://workload.example.invalid/",
                ["Authentication:WorkloadJwtBearer:Issuer"] = "https://workload.example.invalid/",
                ["Authentication:WorkloadJwtBearer:Audience"] = "timesheets",
                ["Authentication:WorkloadJwtBearer:RequireHttpsMetadata"] = "true",
                ["Authentication:WorkloadJwtBearer:AllowedAlgorithms:0"] = "RS256"
            };
            foreach ((string key, string value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });
        if (starts)
        {
            using HttpClient client = factory.CreateClient();
            factory.Services.GetRequiredService<IOptions<Hexalith.EventStore.ServiceDefaults.Authentication.JwtBearerAuthenticationOptions>>()
                .Value.Issuer.ShouldBe("timesheets-test-issuer");
            factory.Services.GetRequiredService<IOptions<AuthenticationOptions>>().Value.DefaultScheme.ShouldBeNull();
        }
        else
        {
            OptionsValidationException error = Should.Throw<OptionsValidationException>(() => factory.CreateClient());
            error.Message.ShouldContain("Authentication:JwtBearer");
            error.Message.ShouldNotContain("workload.example.invalid");
        }
    }
}
