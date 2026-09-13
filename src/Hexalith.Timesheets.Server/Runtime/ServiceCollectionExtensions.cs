using System.Globalization;

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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.Timesheets.Server.Runtime;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTimesheetsServerKernel(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ITimesheetsAccessGuard, TimesheetsAccessGuard>();
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
        if (!services.Any(static descriptor => descriptor.ServiceType == typeof(IEventStoreGatewayClient)))
        {
            services.AddEventStoreGatewayClient(options => options.BaseAddress = ResolveDaprHttpEndpoint())
                .AddEventStoreDaprServiceInvocation(
                    "eventstore",
                    Environment.GetEnvironmentVariable("DAPR_API_TOKEN"));
        }

        services.AddEventStoreReadModelStore();
        services.TryAddSingleton<IMagicLinkTokenGenerator, CryptographicMagicLinkTokenGenerator>();
        services.TryAddScoped<IMagicLinkConfirmationCapabilityStateLoader, EventStoreMagicLinkConfirmationCapabilityStateLoader>();
        services.TryAddSingleton<MagicLinkConfirmationCapabilityCommandService>();
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

    private static Uri ResolveDaprHttpEndpoint()
    {
        string? endpoint = Environment.GetEnvironmentVariable("DAPR_HTTP_ENDPOINT");
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? configured)
            && (configured.Scheme == Uri.UriSchemeHttp || configured.Scheme == Uri.UriSchemeHttps)
            && string.IsNullOrEmpty(configured.UserInfo)
            && configured.AbsolutePath == "/"
            && string.IsNullOrEmpty(configured.Query)
            && string.IsNullOrEmpty(configured.Fragment))
        {
            return new Uri(configured.GetLeftPart(UriPartial.Authority), UriKind.Absolute);
        }

        string? portText = Environment.GetEnvironmentVariable("DAPR_HTTP_PORT");
        return int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out int port)
            && port is > 0 and <= 65535
                ? new UriBuilder(Uri.UriSchemeHttp, "localhost", port).Uri
                : new Uri("http://localhost:3500", UriKind.Absolute);
    }
}
