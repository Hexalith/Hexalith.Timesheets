using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

#if TIMESHEETS_COMPILED_GATEWAY
using Hexalith.EventStore.Aspire;
#endif

namespace Hexalith.Timesheets.AppHost;

/// <summary>Selects the private isolated gateway launch available to a Debug/source AppHost.</summary>
internal static class IsolatedGatewayLaunch
{
    /// <summary>Refuses private launch settings when this build cannot use the source SDK helper.</summary>
    /// <param name="executable">The optional private executable setting.</param>
    /// <param name="workingDirectory">The optional private working-directory setting.</param>
    internal static void ValidateBuildMode(string? executable, string? workingDirectory)
    {
#if !TIMESHEETS_COMPILED_GATEWAY
        if (executable is not null || workingDirectory is not null)
        {
            throw new InvalidOperationException("Compiled gateway launch requires a Debug/source AppHost build.");
        }
#endif
    }

    /// <summary>Delegates validated isolated launch selection to the EventStore hosting SDK.</summary>
    /// <param name="gateway">The existing gateway project resource.</param>
    /// <param name="executable">The SDK-resolved native executable.</param>
    /// <param name="workingDirectory">The SDK-resolved working directory.</param>
    internal static void Apply(IResourceBuilder<ProjectResource> gateway, string executable, string workingDirectory)
    {
#if TIMESHEETS_COMPILED_GATEWAY
        _ = gateway.WithCompiledProjectExecutable(executable, workingDirectory);
#else
        throw new InvalidOperationException("Compiled gateway launch requires a Debug/source AppHost build.");
#endif
    }
}
