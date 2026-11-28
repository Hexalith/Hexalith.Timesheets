using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.DomainService;
using Hexalith.Timesheets.Server.ApprovalAuthority;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.References;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.Timesheets.Runtime.ProjectWorkflow;

/// <summary>Opts the existing host into one explicitly configured Project workflow.</summary>
internal static class ProjectWorkflowServiceCollectionExtensions
{
    /// <summary>Binds the seven Project workflow authorities when the owning host enables them.</summary>
    internal static IServiceCollection AddTimesheetsProjectWorkflow(this IServiceCollection services, IConfiguration configuration)
    {
        ProjectWorkflowOptions? options = configuration.GetSection(ProjectWorkflowOptions.SectionName).Get<ProjectWorkflowOptions>();
        if (options?.Enabled != true)
        {
            return services;
        }

        services.AddSingleton(options);
        services.AddHttpClient(ProjectWorkflowSiblingQueries.ClientName, client =>
            {
                client.BaseAddress = options.AuthorityGatewayOrigin;
                client.Timeout = TimeSpan.FromSeconds(5);
            })
            .ConfigurePrimaryHttpMessageHandler(static () => new HttpClientHandler { AllowAutoRedirect = false })
            .AddEventStoreGatewayAuthorityTransport(configuration);

        services.AddSingleton<ProjectWorkflowSiblingQueries>();
        services.AddSingleton<GatewayProjectWorkflowReferences>();
        services.Replace(ServiceDescriptor.Singleton<ITimesheetsTenantAccessValidator>(
            provider => provider.GetRequiredService<GatewayProjectWorkflowReferences>()));
        services.Replace(ServiceDescriptor.Singleton<IContributorPartyValidator>(
            provider => provider.GetRequiredService<GatewayProjectWorkflowReferences>()));
        services.Replace(ServiceDescriptor.Singleton<IProjectReferenceValidator>(
            provider => provider.GetRequiredService<GatewayProjectWorkflowReferences>()));
        foreach (ServiceDescriptor descriptor in services.Where(descriptor => !descriptor.IsKeyedService
            && descriptor.ServiceType == typeof(IApprovalAuthoritySourceProvider)
            && (descriptor.ImplementationType == typeof(DefaultProjectApprovalAuthoritySourceProvider)
                || descriptor.ImplementationType == typeof(DefaultWorkApprovalAuthoritySourceProvider))).ToArray())
        {
            services.Remove(descriptor);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IApprovalAuthoritySourceProvider, ConfiguredProjectApprovalAuthority>());
        // The workflow policy evaluator uses its scoped options. Projection handlers continue
        // to use the fail-closed publication policy registered by the reusable kernel.
        services.Replace(ServiceDescriptor.Singleton<ITimesheetsPolicyEvaluator, ProjectWorkflowEvidencePolicy>());
        services.AddEventStoreQueryCursorCodec("Hexalith.Timesheets.ApprovedTimeLedger.v1");
        services.AddSingleton<ProjectionBackedApprovedTimeLedgerReader>();
        services.Replace(ServiceDescriptor.Singleton<IApprovedTimeLedgerProjectionReader, ProjectWorkflowLedgerReader>());
        return services;
    }
}
