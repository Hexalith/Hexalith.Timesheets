using Aspire.Hosting.ApplicationModel;

using Hexalith.EventStore.Aspire;

namespace Hexalith.Timesheets.AppHost;

/// <summary>Consumes the owning SDK's new Development transport in the explicitly supported local source build.</summary>
/// <param name="ownership">The owning SDK lifecycle.</param>
/// <param name="enroll">The SDK enrollment action for the existing sidecar.</param>
internal sealed class IsolatedCatalogTransport(
    IAsyncDisposable ownership,
    Action<IResourceBuilder<ProjectResource>> enroll) : IAsyncDisposable
{
    /// <summary>Starts the SDK lifecycle without upgrading the ordinary published-package graph.</summary>
    internal static async Task<IsolatedCatalogTransport> StartAsync(IDistributedApplicationBuilder builder,
        string custody, string runtime, string? placement, string? scheduler)
    {
#if TIMESHEETS_COMPILED_GATEWAY
        if (!OperatingSystem.IsLinux()) throw new InvalidOperationException("Owned Development transport requires Linux.");
        var transport = await builder.AddEventStoreDevelopmentRouteCatalogTransportAsync(new(custody, runtime, placement, scheduler));
        return new(transport, project =>
        {
            if (!OperatingSystem.IsLinux()) throw new InvalidOperationException("Owned Development transport requires Linux.");
            project.WithEventStoreDevelopmentRouteCatalogTransport(transport);
        });
#else
        return await Task.FromException<IsolatedCatalogTransport>(new InvalidOperationException(
            "Owned Development transport requires the validated local Debug/source-reference build."));
#endif
    }

    /// <summary>Binds one existing sidecar through the owner capability before catalog composition.</summary>
    internal void Enroll(IResourceBuilder<ProjectResource> project) => enroll(project);

    /// <summary>Disposes SDK ownership after the application and catalog lifecycles finish.</summary>
    public ValueTask DisposeAsync() => ownership.DisposeAsync();
}
