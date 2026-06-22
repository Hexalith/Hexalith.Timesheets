using Hexalith.Timesheets.Server.OperationalReports;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.Timesheets.Works;

/// <summary>
/// Host/adapter wiring that replaces the fail-closed <see cref="UnavailableWorkPlannedEffortProvider"/>
/// default with the concrete <see cref="WorksQueryWorkPlannedEffortProvider"/> once Works integration is
/// composed.
/// </summary>
public static class WorksPlannedEffortReportingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the concrete Works planned-effort provider, overriding the kernel's fail-closed default.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="ServiceCollectionDescriptorExtensions.Replace"/> so the concrete registration wins
    /// regardless of whether <c>AddTimesheetsServerKernel</c> (which registers
    /// <see cref="UnavailableWorkPlannedEffortProvider"/> via <c>TryAddSingleton</c>) ran before or after
    /// this call. When this extension is NOT invoked, the kernel default keeps reporting Work planned effort
    /// as unavailable, so planned-vs-actual claims stay fail-closed until a host composes Works integration.
    /// The concrete provider depends on <see cref="IWorksQueryChannel"/>, which the composed host binds to
    /// <c>Hexalith.EventStore.Queries.IDomainQueryInvoker</c> when the EventStore domain-service query
    /// channel is wired.
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddTimesheetsWorksPlannedEffortReporting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Replace(ServiceDescriptor.Singleton<IWorkPlannedEffortProvider, WorksQueryWorkPlannedEffortProvider>());

        return services;
    }
}
