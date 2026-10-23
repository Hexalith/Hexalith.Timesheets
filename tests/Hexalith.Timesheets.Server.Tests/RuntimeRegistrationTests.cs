using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ApprovalAuthority;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.Dashboard;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.OperationalReports;
using Hexalith.Timesheets.Server.Policies;
using Hexalith.Timesheets.Server.References;
using Hexalith.Timesheets.Server.Runtime;
using Hexalith.Timesheets.Server.TimeEntries;
using Hexalith.Timesheets.Server.TimesheetPeriods;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class RuntimeRegistrationTests
{
    [Fact]
    public void StateLoaderRetainsItsOriginalSevenParameterPublicConstructor()
    {
        typeof(EventStoreMagicLinkConfirmationCapabilityStateLoader).GetConstructor([
            typeof(IEventStoreGatewayClient), typeof(IReadModelStore), typeof(IMagicLinkTokenGenerator),
            typeof(ITimesheetsTrustedContextAccessor), typeof(SharedProjectionEpochCoordinator),
            typeof(bool), typeof(ILoggerFactory)]).ShouldNotBeNull();
        typeof(EventStoreMagicLinkConfirmationCapabilityStateLoader).GetConstructor([
            typeof(IEventStoreGatewayClient), typeof(IReadModelStore), typeof(IMagicLinkTokenGenerator),
            typeof(ITimesheetsTrustedContextAccessor), typeof(SharedProjectionEpochCoordinator),
            typeof(bool), typeof(ILoggerFactory), typeof(IOptions<MagicLinkStateLoadingOptions>)]).ShouldNotBeNull();
    }

    [Fact]
    public void KernelSelectsTheOptionsAwareLoaderConstructor()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IEventStoreGatewayClient>());
        services.AddSingleton(Substitute.For<IReadModelStore>());
        services.AddSingleton(Substitute.For<IReadModelBatchStore>());
        services.AddTimesheetsServerKernel();
        services.Configure<MagicLinkStateLoadingOptions>(options => options.MaxPages = 0);
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        Should.Throw<OptionsValidationException>(() => scope.ServiceProvider
            .GetRequiredService<EventStoreMagicLinkConfirmationCapabilityStateLoader>());
    }

    [Theory]
    [InlineData("MaxPages", "0")]
    [InlineData("MaxPages", "257")]
    [InlineData("MaxEvents", "0")]
    [InlineData("MaxEvents", "100001")]
    [InlineData("MaxPayloadBytes", "0")]
    [InlineData("MaxPayloadBytes", "134217729")]
    [InlineData("Timeout", "00:00:00")]
    [InlineData("Timeout", "00:00:31")]
    public void StateLoadingOptionsBindAndRejectUnboundedValues(string key, string value)
    {
        var values = new Dictionary<string, string?>
        {
            ["Timesheets:MagicLinks:StateLoading:MaxPages"] = "256",
            ["Timesheets:MagicLinks:StateLoading:MaxEvents"] = "100000",
            ["Timesheets:MagicLinks:StateLoading:MaxPayloadBytes"] = "134217728",
            ["Timesheets:MagicLinks:StateLoading:Timeout"] = "00:00:30"
        };
        ServiceProvider Provider() => new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(values).Build())
            .AddTimesheetsServerKernel().BuildServiceProvider();
        using (ServiceProvider valid = Provider())
        {
            valid.GetRequiredService<IStartupValidator>().Validate();
            MagicLinkStateLoadingOptions options = valid.GetRequiredService<IOptions<MagicLinkStateLoadingOptions>>().Value;
            options.MaxPages.ShouldBe(256); options.MaxEvents.ShouldBe(100000);
            options.MaxPayloadBytes.ShouldBe(134217728); options.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
        }
        values["Timesheets:MagicLinks:StateLoading:" + key] = value;
        using ServiceProvider invalid = Provider();
        Should.Throw<OptionsValidationException>(() => invalid.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void PollingOptionsBindFromConfigurationAndInvalidValuesFailStartupValidation()
    {
        using (ServiceProvider valid = BuildPollingProvider("00:00:02", "00:00:00.025"))
        {
            valid.GetRequiredService<IStartupValidator>().Validate();
            MagicLinkSubmissionPollingOptions options = valid
                .GetRequiredService<IOptions<MagicLinkSubmissionPollingOptions>>().Value;
            options.Timeout.ShouldBe(TimeSpan.FromSeconds(2));
            options.PollInterval.ShouldBe(TimeSpan.FromMilliseconds(25));
            options.ReadbackTimeout.ShouldBe(TimeSpan.FromMilliseconds(75));
        }

        using ServiceProvider invalid = BuildPollingProvider("00:00:02", "00:00:00.001");
        Should.Throw<OptionsValidationException>(() => invalid.GetRequiredService<IStartupValidator>().Validate());
        using ServiceProvider invalidReadback = BuildPollingProvider("00:00:02", "00:00:00.025", "00:00:00");
        Should.Throw<OptionsValidationException>(() => invalidReadback.GetRequiredService<IStartupValidator>().Validate());
    }

    private static ServiceProvider BuildPollingProvider(string timeout, string interval, string readback = "00:00:00.075")
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Timesheets:MagicLinks:SubmissionPolling:Timeout"] = timeout,
                ["Timesheets:MagicLinks:SubmissionPolling:PollInterval"] = interval,
                ["Timesheets:MagicLinks:SubmissionPolling:ReadbackTimeout"] = readback
            }).Build());
        services.AddTimesheetsServerKernel();
        return services.BuildServiceProvider();
    }

    /// <summary>Resolves the kernel's default loader with supplied EventStore read seams.</summary>
    [Fact]
    public void ServerKernelResolvesConcreteMagicLinkLoaderWithoutLoaderOverride()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IEventStoreGatewayClient>());
        services.AddSingleton(Substitute.For<IReadModelStore>());
        services.AddSingleton(Substitute.For<IReadModelBatchStore>());
        services.AddTimesheetsServerKernel();

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IMagicLinkConfirmationCapabilityStateLoader>()
            .ShouldBeOfType<EventStoreMagicLinkConfirmationCapabilityStateLoader>();
    }

    [Fact]
    public void ServerKernelPreservesAPreRegisteredEventStoreGatewayOverride()
    {
        IServiceCollection services = new ServiceCollection();
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        services.AddSingleton(gateway);

        services.AddTimesheetsServerKernel();

        using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<IEventStoreGatewayClient>().ShouldBeSameAs(gateway);
    }

    /// <summary>A caller's scoped gateway does not suppress the SDK's supporting services.</summary>
    [Fact]
    public async Task GatewayOverridePreservesSupportingRegistrationsAndCallerDescriptors()
    {
        IServiceCollection services = new ServiceCollection();
        IEventStoreGatewayClient first = Substitute.For<IEventStoreGatewayClient>();
        IEventStoreGatewayClient second = Substitute.For<IEventStoreGatewayClient>();
        services.AddSingleton(first);
        services.AddScoped<IEventStoreGatewayClient>(_ => second);
        ServiceDescriptor[] overrides = services.Where(static descriptor =>
            descriptor.ServiceType == typeof(IEventStoreGatewayClient)).ToArray();
        var recorder = new RecordingHandler();
        services.AddTimesheetsServerKernel();
        services.Configure<HttpClientFactoryOptions>(nameof(IEventStoreGatewayClient),
            options => options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = recorder));

        services.Where(static descriptor => descriptor.ServiceType == typeof(IEventStoreGatewayClient))
            .ShouldBe(overrides);
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IEventStoreGatewayClient>().ShouldBeSameAs(second);
        scope.ServiceProvider.GetServices<IEventStoreGatewayClient>().ShouldBe([first, second]);
        provider.GetRequiredService<ICommandStatusLocationBuilder>()
            .TryBuild("tracking", out string? location).ShouldBeFalse();
        location.ShouldBeNull();
        Uri? origin = provider.GetRequiredService<IOptions<EventStoreGatewayClientOptions>>().Value.BaseAddress;
        origin.ShouldBeNull();
        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(nameof(IEventStoreGatewayClient));
        client.BaseAddress.ShouldBe(origin);
        using HttpResponseMessage response = await client.GetAsync("https://caller-owned.test/api/v1/streams/read",
            TestContext.Current.CancellationToken);
        recorder.AppId.ShouldBeNull();
    }

    /// <summary>The SDK retains the host's command-status URL policy alongside a custom gateway.</summary>
    [Fact]
    public void GatewayOverridePreservesCustomCommandStatusLocationBuilder()
    {
        IServiceCollection services = new ServiceCollection();
        ICommandStatusLocationBuilder locations = Substitute.For<ICommandStatusLocationBuilder>();
        services.AddSingleton(locations);
        services.AddSingleton(Substitute.For<IEventStoreGatewayClient>());
        services.AddTimesheetsServerKernel();

        using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<ICommandStatusLocationBuilder>().ShouldBeSameAs(locations);
    }

    /// <summary>Repeated kernel composition keeps a caller's real SDK gateway transport intact.</summary>
    [Fact]
    public async Task PreconfiguredSdkGatewayRetainsItsOptionsAndNamedClientRouting()
    {
        IServiceCollection services = new ServiceCollection();
        var recorder = new RecordingHandler();
        services.AddEventStoreGatewayClient(options => options.BaseAddress = new Uri("https://configured-options.test/"))
            .AddEventStoreDaprServiceInvocation("configured-target", "configured-token")
            .ConfigureHttpClient(client => client.BaseAddress = new Uri("https://configured-client.test/"));
        services.AddTimesheetsServerKernel();
        services.AddTimesheetsServerKernel();
        services.Configure<HttpClientFactoryOptions>(nameof(IEventStoreGatewayClient),
            options => options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = recorder));
        using ServiceProvider provider = services.BuildServiceProvider();

        _ = await provider.GetRequiredService<IEventStoreGatewayClient>().ReadStreamAsync(
            new StreamReadRequest("tenant-1", "timesheets", "capability-1"),
            TestContext.Current.CancellationToken);

        provider.GetRequiredService<IOptions<EventStoreGatewayClientOptions>>().Value.BaseAddress
            .ShouldBe(new Uri("https://configured-options.test/"));
        recorder.RequestUri.ShouldNotBeNull().Host.ShouldBe("configured-client.test");
        recorder.AppId.ShouldBe("configured-target");
        recorder.ApiToken.ShouldBe("configured-token");
        provider.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>()
            .Get(nameof(IEventStoreGatewayClient)).HttpMessageHandlerBuilderActions.Count.ShouldBe(2);
    }

    /// <summary>Repeated default composition adds only one sidecar routing handler.</summary>
    [Fact]
    public void DefaultGatewaySupportingRegistrationIsIdempotent()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddTimesheetsServerKernel();
        services.AddTimesheetsServerKernel();
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetServices<IEventStoreGatewayClient>().ShouldHaveSingleItem();
        provider.GetServices<ICommandStatusLocationBuilder>().ShouldHaveSingleItem();
        provider.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>()
            .Get(nameof(IEventStoreGatewayClient)).HttpMessageHandlerBuilderActions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GatewayConfigurationReloadUpdatesExistingCredentialsAndNewClientEndpoints()
    {
        string? environmentToken = Environment.GetEnvironmentVariable("DAPR_API_TOKEN");
        string? environmentEndpoint = Environment.GetEnvironmentVariable("DAPR_HTTP_ENDPOINT");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DAPR_HTTP_ENDPOINT"] = "https://fallback.test/", ["DAPR_API_TOKEN"] = "fallback-token",
            ["Timesheets:EventStoreGateway:Endpoint"] = "https://first.test/",
            ["Timesheets:EventStoreGateway:AppId"] = "first-target",
            ["Timesheets:EventStoreGateway:ApiToken"] = "first-token"
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddTimesheetsServerKernel();
        services.AddTimesheetsServerKernel();
        var recorder = new RecordingHandler();
        services.Configure<HttpClientFactoryOptions>(nameof(IEventStoreGatewayClient), options =>
            options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = recorder));
        using ServiceProvider provider = services.BuildServiceProvider();
        IEventStoreGatewayClient existing = provider.GetRequiredService<IEventStoreGatewayClient>();
        var request = new StreamReadRequest("tenant-1", "timesheets", "capability-1");
        _ = await existing.ReadStreamAsync(request, TestContext.Current.CancellationToken);
        recorder.RequestUri.ShouldNotBeNull().Host.ShouldBe("first.test");
        recorder.AppId.ShouldBe("first-target"); recorder.ApiToken.ShouldBe("first-token");
        configuration["Timesheets:EventStoreGateway:Endpoint"] = "https://second.test/";
        configuration["Timesheets:EventStoreGateway:AppId"] = "second-target";
        configuration["Timesheets:EventStoreGateway:ApiToken"] = "rotated-token";
        configuration.Reload();
        _ = await existing.ReadStreamAsync(request, TestContext.Current.CancellationToken);
        recorder.RequestUri.Host.ShouldBe("first.test");
        recorder.AppId.ShouldBe("second-target"); recorder.ApiToken.ShouldBe("rotated-token");
        _ = await provider.GetRequiredService<IEventStoreGatewayClient>().ReadStreamAsync(request, TestContext.Current.CancellationToken);
        recorder.RequestUri.Host.ShouldBe("second.test");
        // Empty explicitly removes the credential, including a configured fallback token.
        configuration["Timesheets:EventStoreGateway:ApiToken"] = "";
        configuration.Reload();
        _ = await existing.ReadStreamAsync(request, TestContext.Current.CancellationToken);
        recorder.ApiToken.ShouldBeNull();
        Environment.GetEnvironmentVariable("DAPR_API_TOKEN").ShouldBe(environmentToken);
        Environment.GetEnvironmentVariable("DAPR_HTTP_ENDPOINT").ShouldBe(environmentEndpoint);
    }

    [Fact]
    public async Task GatewayUsesConfigurationDaprFallbacksWithoutProcessMutation()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DAPR_HTTP_ENDPOINT"] = "https://fallback.test/", ["DAPR_API_TOKEN"] = "configured-token"
        }).Build();
        var services = new ServiceCollection(); services.AddSingleton<IConfiguration>(configuration);
        services.AddTimesheetsServerKernel(); var recorder = new RecordingHandler();
        services.Configure<HttpClientFactoryOptions>(nameof(IEventStoreGatewayClient), options =>
            options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = recorder));
        using ServiceProvider provider = services.BuildServiceProvider();
        _ = await provider.GetRequiredService<IEventStoreGatewayClient>().ReadStreamAsync(
            new("tenant-1", "timesheets", "capability-1"), TestContext.Current.CancellationToken);
        recorder.RequestUri.ShouldNotBeNull().Host.ShouldBe("fallback.test");
        recorder.AppId.ShouldBe("eventstore"); recorder.ApiToken.ShouldBe("configured-token");
        configuration["DAPR_HTTP_ENDPOINT"] = "https://invalid.test/path";
        configuration["DAPR_HTTP_PORT"] = "4555";
        configuration["DAPR_API_TOKEN"] = null;
        configuration.Reload();
        _ = await provider.GetRequiredService<IEventStoreGatewayClient>().ReadStreamAsync(
            new("tenant-1", "timesheets", "capability-1"), TestContext.Current.CancellationToken);
        recorder.RequestUri.Port.ShouldBe(4555); recorder.RequestUri.Host.ShouldBe("localhost");
        recorder.ApiToken.ShouldBeNull();
    }

    [Fact]
    public async Task ServerKernelDefaultGatewayRoutesThroughConfiguredDaprOriginAndHeaders()
    {
        var recorder = new RecordingHandler();
        IServiceCollection services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DAPR_HTTP_ENDPOINT"] = "https://127.0.0.1:4545/", ["DAPR_API_TOKEN"] = "routing-test-token"
        }).Build());
        services.AddTimesheetsServerKernel();
        services.Configure<HttpClientFactoryOptions>(nameof(IEventStoreGatewayClient), options =>
            options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = recorder));
        using ServiceProvider provider = services.BuildServiceProvider();
        _ = await provider.GetRequiredService<IEventStoreGatewayClient>().ReadStreamAsync(
            new("tenant-1", "timesheets", "capability-1"), TestContext.Current.CancellationToken);
        recorder.RequestUri.ShouldNotBeNull().ToString().ShouldStartWith("https://127.0.0.1:4545/api/v1/streams/read");
        recorder.AppId.ShouldBe("eventstore"); recorder.ApiToken.ShouldBe("routing-test-token");
        recorder.RequestUri.ToString().ShouldNotContain("routing-test-token");
    }

    [Fact]
    public async Task ServerKernelWorkloadGatewayUsesSignedRoutesForSubmitStatusAndRead()
    {
        var recorder = new RecordingHandler();
        IEventStoreGatewayWorkloadAssertionSource assertions = Substitute.For<IEventStoreGatewayWorkloadAssertionSource>();
        assertions.IssueAsync(Arg.Any<string>(), "tenant-1", Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string?>("signed-assertion"));
        IServiceCollection services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(assertions);
        services.AddTimesheetsServerKernel();
        services.Configure<HttpClientFactoryOptions>(nameof(IEventStoreGatewayClient),
            options => options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = recorder));
        using ServiceProvider provider = services.BuildServiceProvider();
        IEventStoreGatewayClient gateway = provider.GetRequiredService<IEventStoreGatewayClient>();

        _ = await gateway.ReadWorkloadStreamAsync(
            new StreamReadRequest("tenant-1", "timesheets", "capability-1"), TestContext.Current.CancellationToken);
        _ = await gateway.GetWorkloadCommandStatusAsync(
            "tenant-1", "message", TestContext.Current.CancellationToken);
        _ = await gateway.SubmitWorkloadCommandAsync(new SubmitCommandRequest(
            "message", "tenant-1", "timesheets", "capability-1", "test.command",
            JsonSerializer.SerializeToElement(new { value = 1 }), CorrelationId: "message"),
            TestContext.Current.CancellationToken);

        recorder.Routes.ShouldBe([
            "/api/v1/streams/read/workload",
            "/api/v1/commands/status/workload/tenant-1/message",
            "/api/v1/commands/workload"
        ]);
        recorder.Assertions.ShouldBe(["signed-assertion", "signed-assertion", "signed-assertion"]);
        await assertions.Received(1).IssueAsync(EventStoreGatewayWorkloadOperations.StreamRead,
            "tenant-1", Arg.Any<CancellationToken>());
        await assertions.Received(1).IssueAsync(EventStoreGatewayWorkloadOperations.CommandStatus,
            "tenant-1", Arg.Any<CancellationToken>());
        await assertions.Received(1).IssueAsync(EventStoreGatewayWorkloadOperations.CommandSubmit,
            "tenant-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ServerKernelFallsBackToTheDaprHttpPortWhenNoEndpointIsConfigured()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["DAPR_HTTP_PORT"] = "3501" }).Build());
        services.AddTimesheetsServerKernel();
        using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptionsMonitor<TimesheetsEventStoreGatewayOptions>>()
            .CurrentValue.BaseAddress.ShouldBe(new Uri("http://localhost:3501"));
    }

    [Theory]
    [InlineData("https://example.test/path")]
    [InlineData("https://example.test/?query=value")]
    [InlineData("https://example.test/#fragment")]
    [InlineData("https://user@example.test/")]
    [InlineData("ftp://example.test/")]
    public void GatewayRejectsNonOriginEndpointsAndNonNumericPorts(string endpoint)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["DAPR_HTTP_ENDPOINT"] = endpoint, ["DAPR_HTTP_PORT"] = "3501@off-host.example" }).Build());
        services.AddTimesheetsServerKernel();
        using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptionsMonitor<TimesheetsEventStoreGatewayOptions>>()
            .CurrentValue.BaseAddress.ShouldBe(new Uri("http://localhost:3500"));
    }

    [Fact]
    public void ServerKernelRegistersFailClosedDefaultsUntilTrustAdaptersExist()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddTimesheetsServerKernel();

        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<ITimesheetsAuthorizationGate>()
            .ShouldBeOfType<DenyAllTimesheetsAuthorizationGate>();
        provider.GetRequiredService<ITimesheetsAccessGuard>()
            .ShouldBeOfType<TimesheetsAccessGuard>();
        provider.GetRequiredService<ITimesheetsApprovalAuthorityResolver>()
            .ShouldBeOfType<TimesheetsApprovalAuthorityResolver>();
        provider.GetServices<IApprovalAuthoritySourceProvider>()
            .Select(static provider => provider.Source)
            .ShouldBe([
                ApprovalAuthoritySource.ProjectApprover,
                ApprovalAuthoritySource.WorkOwner,
                ApprovalAuthoritySource.TenantAdministrator,
                ApprovalAuthoritySource.FinanceReviewer
            ]);
        provider.GetRequiredService<TenantActivityTypeCommandService>()
            .ShouldNotBeNull();
        provider.GetRequiredService<ProjectActivityTypeCommandService>()
            .ShouldNotBeNull();
        provider.GetRequiredService<TimeEntryCommandService>()
            .ShouldNotBeNull();
        provider.GetRequiredService<TimeEntrySubmissionCommandService>()
            .ShouldNotBeNull();
        provider.GetRequiredService<ExternalContributionCommandService>()
            .ShouldNotBeNull();
        provider.GetRequiredService<ExternalContributionPolicyOptions>()
            .ShouldBe(ExternalContributionPolicyOptions.Default);
        provider.GetRequiredService<TimeEntryApprovalCommandService>()
            .ShouldNotBeNull();
        provider.GetRequiredService<TimeEntryCorrectionCommandService>()
            .ShouldNotBeNull();
        provider.GetRequiredService<TimeEntryEvidenceQueryService>()
            .ShouldNotBeNull();
        provider.GetRequiredService<TimeEntryEvidenceListQueryService>()
            .ShouldNotBeNull();
        provider.GetRequiredService<ActualTimeReportQueryService>()
            .ShouldNotBeNull();
        provider.GetRequiredService<TimesheetsDashboardOverviewQueryService>()
            .ShouldNotBeNull();
        provider.GetRequiredService<TimesheetPeriodSubmissionCommandService>()
            .ShouldNotBeNull();
        provider.GetRequiredService<TimesheetPeriodApprovalCommandService>()
            .ShouldNotBeNull();
        provider.GetRequiredService<TimesheetPeriodSummaryQueryService>()
            .ShouldNotBeNull();
        provider.GetRequiredService<ITimesheetsTenantAccessValidator>()
            .ShouldBeOfType<DenyAllTimesheetsTenantAccessValidator>();
        provider.GetRequiredService<ITimesheetsPolicyEvaluator>()
            .ShouldBeOfType<TimesheetsEvidencePolicyEvaluator>();
        provider.GetRequiredService<TimesheetsEvidencePolicyOptions>()
            .ShouldBe(TimesheetsEvidencePolicyOptions.FailClosedDefault);
        provider.GetRequiredService<IProjectReferenceValidator>()
            .ShouldBeOfType<DenyAllProjectReferenceValidator>();
        provider.GetRequiredService<IWorkReferenceValidator>()
            .ShouldBeOfType<DenyAllWorkReferenceValidator>();
        provider.GetRequiredService<IContributorPartyValidator>()
            .ShouldBeOfType<DenyAllContributorPartyValidator>();
        provider.GetRequiredService<ITimeEntryEvidenceProjectionReader>()
            .ShouldBeOfType<UnavailableTimeEntryEvidenceProjectionReader>();
        provider.GetRequiredService<ITimeEntryEvidenceListProjectionReader>()
            .ShouldBeOfType<UnavailableTimeEntryEvidenceListProjectionReader>();
        provider.GetRequiredService<IActualTimeReportProjectionReader>()
            .ShouldBeOfType<UnavailableActualTimeReportProjectionReader>();
        provider.GetRequiredService<IWorkPlannedEffortProvider>()
            .ShouldBeOfType<UnavailableWorkPlannedEffortProvider>();
        provider.GetRequiredService<ITimeEntryDisplayHydrator>()
            .ShouldBeOfType<UnavailableTimeEntryDisplayHydrator>();
        provider.GetRequiredService<IPartyDisplayHydrationProvider>()
            .ShouldBeOfType<UnavailableDisplayHydrationProvider>();
        provider.GetRequiredService<IProjectDisplayHydrationProvider>()
            .ShouldBeOfType<UnavailableDisplayHydrationProvider>();
        provider.GetRequiredService<IWorkDisplayHydrationProvider>()
            .ShouldBeOfType<UnavailableDisplayHydrationProvider>();
        provider.GetRequiredService<IActivityTypeDisplayHydrationProvider>()
            .ShouldBeOfType<UnavailableDisplayHydrationProvider>();
    }

    [Fact]
    public async Task ComposedAccessGuardFailsClosedWithUnconfiguredDefaults()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddTimesheetsServerKernel();

        using ServiceProvider provider = services.BuildServiceProvider();

        ITimesheetsAccessGuard guard = provider.GetRequiredService<ITimesheetsAccessGuard>();

        TimesheetsAuthorizationRequest request = new(
            new TimesheetsRequestContext(
                new TenantReference("tenant_01"),
                new PartyReference("party_01"),
                "correlation_01"),
            TimesheetsOperation.Command)
        {
            Project = new ProjectReference("project_01")
        };

        TimesheetsAuthorizationDecision decision = await guard.AuthorizeAsync(
            request,
            TestContext.Current.CancellationToken);

        decision.IsAuthorized.ShouldBeFalse();
        decision.DenialCategory.ShouldBe(TimesheetsDenialCategory.UnconfiguredPolicy);
        decision.Reason.ShouldBe("Authority cannot be resolved.");
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> Routes { get; } = [];

        public List<string?> Assertions { get; } = [];
        public Uri? RequestUri { get; private set; }

        public string? AppId { get; private set; }

        public string? ApiToken { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Routes.Add(request.RequestUri?.AbsolutePath ?? string.Empty);
            Assertions.Add(request.Headers.TryGetValues("X-Hexalith-Workload-Assertion", out IEnumerable<string>? values)
                ? values.Single() : null);
            AppId = request.Headers.TryGetValues("dapr-app-id", out IEnumerable<string>? appIds)
                ? appIds.Single()
                : null;
            ApiToken = request.Headers.TryGetValues("dapr-api-token", out IEnumerable<string>? apiTokens)
                ? apiTokens.Single()
                : null;
            if (request.RequestUri?.AbsolutePath.Contains("/commands/status/workload/", StringComparison.Ordinal) == true)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            if (request.RequestUri?.AbsolutePath.EndsWith("/commands/workload", StringComparison.Ordinal) == true)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new SubmitCommandResponse("message", MessageId: "message"))
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new StreamReadPage(
                    "tenant-1",
                    "timesheets",
                    "capability-1",
                    [],
                    new StreamReadMetadata(0, null, null, 0, 0, false, null)))
            });
        }
    }
}
