using Hexalith.Timesheets.Server.ApprovalAuthority;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Dashboard;
using Hexalith.Timesheets.Server.Exports;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.OperationalReports;
using Hexalith.Timesheets.Server.Policies;
using Hexalith.Timesheets.Server.References;
using Hexalith.Timesheets.Server.TimeEntries;
using Hexalith.Timesheets.Server.TimesheetPeriods;

using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Handlers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.Timesheets.Server.Runtime;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTimesheetsServerKernel(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ITimesheetsAccessGuard, TimesheetsAccessGuard>();
        services.TryAddSingleton<ITimeEntryCommentPublicationAuthority, DenyAllTimeEntryCommentPublicationAuthority>();
        services.TryAddSingleton<TimeEntryCommentPublicationIssuer>();
        services.TryAddSingleton(TimesheetsApprovalAuthorityPolicyOptions.Default);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IApprovalAuthoritySourceProvider, DefaultProjectApprovalAuthoritySourceProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IApprovalAuthoritySourceProvider, DefaultWorkApprovalAuthoritySourceProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IApprovalAuthoritySourceProvider, DefaultTenantApprovalAuthoritySourceProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IApprovalAuthoritySourceProvider, DefaultFinanceApprovalAuthoritySourceProvider>());
        services.TryAddSingleton<ITimesheetsApprovalAuthorityResolver>(static provider => new TimesheetsApprovalAuthorityResolver(
            provider.GetRequiredService<TimesheetsApprovalAuthorityPolicyOptions>(),
            provider.GetServices<IApprovalAuthoritySourceProvider>(),
            provider.GetRequiredService<ITimesheetsAccessGuard>()));
        services.TryAddSingleton<TenantActivityTypeCommandService>();
        services.TryAddSingleton<ProjectActivityTypeCommandService>();
        services.TryAddSingleton<TimeEntryCommandService>();
        services.TryAddSingleton<TimeEntrySubmissionCommandService>();
        services.TryAddSingleton(ExternalContributionPolicyOptions.Default);
        services.TryAddSingleton<ExternalContributionCommandService>();
        services.TryAddSingleton<ITimesheetsTrustedContextAccessor, UnavailableTimesheetsTrustedContextAccessor>();
        // Reuse the SDK's internal fail-closed Location implementation without changing a caller's
        // gateway transport. Inspect registration descriptors only; never build a temporary provider.
        IServiceCollection gatewayDefaults = new ServiceCollection();
        gatewayDefaults.AddEventStoreGatewayClient();
        services.AddOptions<EventStoreGatewayClientOptions>();
        services.AddHttpClient();
        services.TryAdd(gatewayDefaults.Single(static descriptor =>
            descriptor.ServiceType == typeof(ICommandStatusLocationBuilder)));
        if (!services.Any(static descriptor => !descriptor.IsKeyedService
            && descriptor.ServiceType == typeof(IEventStoreGatewayClient)))
        {
            services.TryAddSingleton<IConfiguration>(_ => new ConfigurationBuilder().Build());
            services.AddOptions<TimesheetsEventStoreGatewayOptions>()
                .Configure<IConfiguration>((options, configuration) =>
                {
                    options.Endpoint = configuration["DAPR_HTTP_ENDPOINT"];
                    options.HttpPort = configuration["DAPR_HTTP_PORT"];
                    options.ApiToken = configuration["DAPR_API_TOKEN"];
                })
                .BindConfiguration("Timesheets:EventStoreGateway");
            services.AddEventStoreGatewayClient()
                .ConfigureHttpClient((provider, client) => client.BaseAddress = provider
                    .GetRequiredService<IOptionsMonitor<TimesheetsEventStoreGatewayOptions>>().CurrentValue.BaseAddress)
                .AddEventStoreDaprServiceInvocation(
                    provider => provider.GetRequiredService<IOptionsMonitor<TimesheetsEventStoreGatewayOptions>>().CurrentValue,
                    options => options.AppId,
                    options => options.ApiToken);
        }

        services.AddEventStoreReadModelStore();
        services.TryAddSingleton<IMagicLinkTokenGenerator, CryptographicMagicLinkTokenGenerator>();
        services.TryAddScoped<EventStoreMagicLinkConfirmationCapabilityStateLoader>();
        services.TryAddScoped<IMagicLinkConfirmationCapabilityStateLoader>(
            static provider => provider.GetRequiredService<EventStoreMagicLinkConfirmationCapabilityStateLoader>());
        services.TryAddSingleton<MagicLinkConfirmationCapabilityCommandService>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<MagicLinkSubmissionPollingOptions>()
            .BindConfiguration("Timesheets:MagicLinks:SubmissionPolling")
            .Validate(static options => options.IsValid(), "Magic-link submission polling options must be bounded.")
            .ValidateOnStart();
        services.TryAddScoped<MagicLinkDurableSubmissionService>();
        services.TryAddScoped<TimeEntryRecordSubmissionService>();
        services.TryAddSingleton<TimeEntryApprovalCommandService>();
        services.TryAddSingleton<TimeEntryCorrectionCommandService>();
        services.TryAddSingleton<TimeEntryEvidenceQueryService>();
        services.TryAddSingleton<TimeEntryEvidenceListQueryService>();
        services.TryAddSingleton<ApprovedTimeLedgerQueryService>();
        services.TryAddSingleton<TimesheetsDashboardOverviewQueryService>();
        services.TryAddSingleton<IApprovedTimeExportAuditRecorder, DomainEventApprovedTimeExportAuditRecorder>();
        services.TryAddSingleton<ApprovedTimeExportService>();
        services.TryAddSingleton<ActualTimeReportQueryService>();
        services.TryAddSingleton<TimesheetPeriodSubmissionCommandService>();
        services.TryAddSingleton<TimesheetPeriodApprovalCommandService>();
        services.TryAddSingleton<TimesheetPeriodSummaryQueryService>();
        services.TryAddSingleton<ITimesheetsAuthorizationGate, DenyAllTimesheetsAuthorizationGate>();
        services.TryAddSingleton<ITimesheetsTenantAccessValidator, DenyAllTimesheetsTenantAccessValidator>();
        services.TryAddSingleton(TimesheetsEvidencePolicyOptions.FailClosedDefault);
        services.TryAddSingleton<ITimesheetsPolicyEvaluator, TimesheetsEvidencePolicyEvaluator>();
        services.TryAddSingleton<IProjectReferenceValidator, DenyAllProjectReferenceValidator>();
        services.TryAddSingleton<IWorkReferenceValidator, DenyAllWorkReferenceValidator>();
        services.TryAddSingleton<IContributorPartyValidator, DenyAllContributorPartyValidator>();
        services.TryAddSingleton<ITimeEntryEvidenceProjectionReader, UnavailableTimeEntryEvidenceProjectionReader>();
        services.TryAddSingleton<ITimeEntryEvidenceListProjectionReader, UnavailableTimeEntryEvidenceListProjectionReader>();
        services.TryAddSingleton<IApprovedTimeLedgerProjectionReader, UnavailableApprovedTimeLedgerProjectionReader>();
        services.TryAddSingleton<IActualTimeReportProjectionReader, UnavailableActualTimeReportProjectionReader>();
        services.TryAddSingleton<IWorkPlannedEffortProvider, UnavailableWorkPlannedEffortProvider>();
        services.TryAddSingleton<ITimesheetPeriodSummaryProjectionReader, UnavailableTimesheetPeriodSummaryProjectionReader>();
        services.TryAddSingleton<UnavailableDisplayHydrationProvider>();
        services.TryAddSingleton<IPartyDisplayHydrationProvider>(static provider =>
            provider.GetRequiredService<UnavailableDisplayHydrationProvider>());
        services.TryAddSingleton<IProjectDisplayHydrationProvider>(static provider =>
            provider.GetRequiredService<UnavailableDisplayHydrationProvider>());
        services.TryAddSingleton<IWorkDisplayHydrationProvider>(static provider =>
            provider.GetRequiredService<UnavailableDisplayHydrationProvider>());
        services.TryAddSingleton<IActivityTypeDisplayHydrationProvider>(static provider =>
            provider.GetRequiredService<UnavailableDisplayHydrationProvider>());
        services.TryAddSingleton<ITimeEntryDisplayHydrator, UnavailableTimeEntryDisplayHydrator>();

        return services;
    }

    /// <summary>Opts the host into the EventStore magic-link command processor.</summary>
    public static IServiceCollection AddTimesheetsMagicLinkEventStoreProcessing(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<MagicLinkEventStoreDomainProcessor>();
        services.AddKeyedScoped<IDomainProcessor>(
            TimesheetsEventStoreIntegration.DomainName,
            static (provider, _) => provider.GetRequiredService<MagicLinkEventStoreDomainProcessor>());
        services.AddKeyedScoped<IAsyncDomainProcessor>(
            TimesheetsEventStoreIntegration.DomainName,
            static (provider, _) => provider.GetRequiredService<MagicLinkEventStoreDomainProcessor>());
        return services;
    }

}
