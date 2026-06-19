using Hexalith.Timesheets.Server.ApprovalAuthority;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Exports;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.OperationalReports;
using Hexalith.Timesheets.Server.Policies;
using Hexalith.Timesheets.Server.References;
using Hexalith.Timesheets.Server.TimeEntries;
using Hexalith.Timesheets.Server.TimesheetPeriods;

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
        services.TryAddSingleton<IMagicLinkTokenGenerator, CryptographicMagicLinkTokenGenerator>();
        services.TryAddSingleton<IMagicLinkConfirmationCapabilityStateLoader, UnavailableMagicLinkConfirmationCapabilityStateLoader>();
        services.TryAddSingleton<MagicLinkConfirmationCapabilityCommandService>();
        services.TryAddSingleton<TimeEntryApprovalCommandService>();
        services.TryAddSingleton<TimeEntryCorrectionCommandService>();
        services.TryAddSingleton<TimeEntryEvidenceQueryService>();
        services.TryAddSingleton<TimeEntryEvidenceListQueryService>();
        services.TryAddSingleton<ApprovedTimeLedgerQueryService>();
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
}
