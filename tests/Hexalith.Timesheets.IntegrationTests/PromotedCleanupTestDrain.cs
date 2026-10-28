using System.Reflection;

using Hexalith.EventStore.Client.Projections;

namespace Hexalith.Timesheets.IntegrationTests;

/// <summary>Drains the pinned SDK's actual cleanup task without changing its scheduling or budget.</summary>
/// <remarks>
/// The SDK's phase-advance drain is internal. This test-only bridge binds its exact signature and
/// fails loudly on SDK changes; it neither substitutes cleanup nor changes production visibility.
/// </remarks>
internal static class PromotedCleanupTestDrain
{
    /// <summary>Waits for the owning coordinator's entire scheduled cleanup, including during teardown.</summary>
    /// <param name="coordinator">The exact coordinator that scheduled the work.</param>
    /// <param name="scope">The owning shared projection scope.</param>
    /// <param name="cancellationToken">Cancellation of the wait, without cancelling SDK cleanup.</param>
    /// <returns>The SDK's own drain task.</returns>
    internal static Task WaitAsync(SharedProjectionEpochCoordinator coordinator, SharedProjectionScope scope,
        CancellationToken cancellationToken)
    {
        MethodInfo method = typeof(SharedProjectionEpochCoordinator).GetMethod("WaitForPromotedCleanupAsync",
            BindingFlags.Instance | BindingFlags.NonPublic, [typeof(SharedProjectionScope), typeof(CancellationToken)])
            ?? throw new MissingMethodException("The pinned SDK promoted-cleanup drain changed.");
        return method.CreateDelegate<Func<SharedProjectionScope, CancellationToken, Task>>(coordinator)(scope, cancellationToken);
    }
}
