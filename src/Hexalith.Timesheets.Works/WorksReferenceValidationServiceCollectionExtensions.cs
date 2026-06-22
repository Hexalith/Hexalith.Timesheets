using Hexalith.Timesheets.Server.References;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.Timesheets.Works;

/// <summary>
/// Host/adapter wiring that replaces the fail-closed <see cref="DenyAllWorkReferenceValidator"/> default
/// with the concrete <see cref="WorksQueryWorkReferenceValidator"/> once Works integration is composed.
/// </summary>
public static class WorksReferenceValidationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the concrete Works reference validator, overriding the kernel's fail-closed default.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="ServiceCollectionDescriptorExtensions.Replace"/> so the concrete registration wins
    /// regardless of whether <c>AddTimesheetsServerKernel</c> (which registers
    /// <see cref="DenyAllWorkReferenceValidator"/> via <c>TryAddSingleton</c>) ran before or after this call.
    /// When this extension is NOT invoked, the kernel default keeps denying composed Work writes, preserving
    /// the Story 1.7 fail-closed guarantee. The concrete validator depends on <see cref="IWorksQueryChannel"/>,
    /// which the composed host binds to <c>Hexalith.EventStore.Queries.IDomainQueryInvoker</c> when the
    /// EventStore domain-service query channel is wired.
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddTimesheetsWorksReferenceValidation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Replace(ServiceDescriptor.Singleton<IWorkReferenceValidator, WorksQueryWorkReferenceValidator>());

        return services;
    }
}
