using System.Net;
using System.Net.Http.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ApprovalAuthority;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.Dashboard;
using Hexalith.Timesheets.Server.OperationalReports;
using Hexalith.Timesheets.Server.Policies;
using Hexalith.Timesheets.Server.References;
using Hexalith.Timesheets.Server.Runtime;
using Hexalith.Timesheets.Server.TimeEntries;
using Hexalith.Timesheets.Server.TimesheetPeriods;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class RuntimeRegistrationTests
{
    [Fact]
    public void Server_kernel_preserves_a_pre_registered_event_store_gateway_override()
    {
        IServiceCollection services = new ServiceCollection();
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        services.AddSingleton(gateway);

        services.AddTimesheetsServerKernel();

        using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<IEventStoreGatewayClient>().ShouldBeSameAs(gateway);
    }

    [Fact]
    public async Task Server_kernel_default_gateway_routes_through_configured_dapr_origin_and_headers()
    {
        string? previousEndpoint = Environment.GetEnvironmentVariable("DAPR_HTTP_ENDPOINT");
        string? previousPort = Environment.GetEnvironmentVariable("DAPR_HTTP_PORT");
        string? previousToken = Environment.GetEnvironmentVariable("DAPR_API_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("DAPR_HTTP_ENDPOINT", "https://127.0.0.1:4545/");
            Environment.SetEnvironmentVariable("DAPR_HTTP_PORT", null);
            Environment.SetEnvironmentVariable("DAPR_API_TOKEN", "routing-test-token");
            var recorder = new RecordingHandler();
            IServiceCollection services = new ServiceCollection();
            services.AddLogging();
            services.AddTimesheetsServerKernel();
            services.Configure<HttpClientFactoryOptions>(
                nameof(IEventStoreGatewayClient),
                options => options.HttpMessageHandlerBuilderActions.Add(
                    builder => builder.PrimaryHandler = recorder));

            using ServiceProvider provider = services.BuildServiceProvider();
            IEventStoreGatewayClient gateway = provider.GetRequiredService<IEventStoreGatewayClient>();
            _ = await gateway.ReadStreamAsync(
                new StreamReadRequest("tenant-1", "timesheets", "capability-1"),
                TestContext.Current.CancellationToken);

            recorder.RequestUri.ShouldNotBeNull().ToString()
                .ShouldStartWith("https://127.0.0.1:4545/api/v1/streams/read");
            recorder.AppId.ShouldBe("eventstore");
            recorder.ApiToken.ShouldBe("routing-test-token");
            recorder.RequestUri.ToString().ShouldNotContain("routing-test-token");
        }
        finally
        {
            Environment.SetEnvironmentVariable("DAPR_HTTP_ENDPOINT", previousEndpoint);
            Environment.SetEnvironmentVariable("DAPR_HTTP_PORT", previousPort);
            Environment.SetEnvironmentVariable("DAPR_API_TOKEN", previousToken);
        }
    }

    [Fact]
    public void Server_kernel_rejects_non_origin_endpoints_and_non_numeric_ports()
    {
        string? previousEndpoint = Environment.GetEnvironmentVariable("DAPR_HTTP_ENDPOINT");
        string? previousPort = Environment.GetEnvironmentVariable("DAPR_HTTP_PORT");
        try
        {
            string[] invalidEndpoints =
            [
                "https://example.test/path",
                "https://example.test/?query=value",
                "https://user@example.test/"
            ];
            foreach (string invalidEndpoint in invalidEndpoints)
            {
                Environment.SetEnvironmentVariable("DAPR_HTTP_ENDPOINT", invalidEndpoint);
                Environment.SetEnvironmentVariable("DAPR_HTTP_PORT", "3500@off-host.example");
                IServiceCollection services = new ServiceCollection();
                services.AddTimesheetsServerKernel();

                using ServiceProvider provider = services.BuildServiceProvider();
                provider.GetRequiredService<IOptions<EventStoreGatewayClientOptions>>()
                    .Value.BaseAddress.ShouldBe(new Uri("http://localhost:3500"));
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("DAPR_HTTP_ENDPOINT", previousEndpoint);
            Environment.SetEnvironmentVariable("DAPR_HTTP_PORT", previousPort);
        }
    }

    [Fact]
    public void Server_kernel_registers_fail_closed_defaults_until_trust_adapters_exist()
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
    public async Task Composed_access_guard_fails_closed_with_unconfigured_defaults()
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
        public Uri? RequestUri { get; private set; }

        public string? AppId { get; private set; }

        public string? ApiToken { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            AppId = request.Headers.TryGetValues("dapr-app-id", out IEnumerable<string>? appIds)
                ? appIds.Single()
                : null;
            ApiToken = request.Headers.TryGetValues("dapr-api-token", out IEnumerable<string>? apiTokens)
                ? apiTokens.Single()
                : null;
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
