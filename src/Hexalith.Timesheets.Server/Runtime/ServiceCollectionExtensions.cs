using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Policies;
using Hexalith.Timesheets.Server.References;
using Hexalith.Timesheets.Server.TimeEntries;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.Timesheets.Server.Runtime;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTimesheetsServerKernel(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ITimesheetsAccessGuard, TimesheetsAccessGuard>();
        services.TryAddSingleton<TenantActivityTypeCommandService>();
        services.TryAddSingleton<ProjectActivityTypeCommandService>();
        services.TryAddSingleton<TimeEntryCommandService>();
        services.TryAddSingleton<TimeEntryEvidenceQueryService>();
        services.TryAddSingleton<ITimesheetsAuthorizationGate, DenyAllTimesheetsAuthorizationGate>();
        services.TryAddSingleton<ITimesheetsTenantAccessValidator, DenyAllTimesheetsTenantAccessValidator>();
        services.TryAddSingleton(TimesheetsEvidencePolicyOptions.FailClosedDefault);
        services.TryAddSingleton<ITimesheetsPolicyEvaluator, TimesheetsEvidencePolicyEvaluator>();
        services.TryAddSingleton<IProjectReferenceValidator, DenyAllProjectReferenceValidator>();
        services.TryAddSingleton<IWorkReferenceValidator, DenyAllWorkReferenceValidator>();
        services.TryAddSingleton<IContributorPartyValidator, DenyAllContributorPartyValidator>();
        services.TryAddSingleton<ITimeEntryEvidenceProjectionReader, UnavailableTimeEntryEvidenceProjectionReader>();
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
