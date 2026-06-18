using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.References;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.Timesheets.Server.Runtime;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTimesheetsServerKernel(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ITimesheetsAuthorizationGate, DenyAllTimesheetsAuthorizationGate>();
        services.TryAddSingleton<IProjectReferenceValidator, DenyAllProjectReferenceValidator>();
        services.TryAddSingleton<IWorkReferenceValidator, DenyAllWorkReferenceValidator>();
        services.TryAddSingleton<IContributorPartyValidator, DenyAllContributorPartyValidator>();

        return services;
    }
}
