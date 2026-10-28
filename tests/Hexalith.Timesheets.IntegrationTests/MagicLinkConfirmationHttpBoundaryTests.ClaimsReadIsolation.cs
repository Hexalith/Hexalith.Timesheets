using Hexalith.EventStore.Client.Projections;
using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.ReadModels.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.MagicLinks;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed partial class MagicLinkConfirmationHttpBoundaryTests
{
    /// <summary>Replays the setup cleanup that can enter the old global administrator read window.</summary>
    /// <param name="injectForeignRequestRead">Also prove the same key is rejected when read inside the administrator request.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminRequestReadSetExcludesDemonstratedPromotedCatalogCleanup(bool injectForeignRequestRead)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, useMismatchedClaims: true);
        using HttpClient client = factory.CreateClient();
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            new MagicLinkCapabilityId("capability-existing"), new TimeEntryId("time-entry-existing"));
        SharedProjectionScope setupScope = TenantActivityTypeCatalogEpoch.CreateScope(Tenant().TenantId);
        string setupEpochKey = "shared-epoch:" + setupScope.ComputeHash();
        string requestEpochKey = "shared-epoch:" + TenantActivityTypeCatalogEpoch.CreateScope(OtherTenant().TenantId).ComputeHash();
        await PromotedCleanupTestDrain.WaitAsync(factory.CatalogSetupCoordinator.ShouldNotBeNull(), setupScope,
            TestContext.Current.CancellationToken);
        var coordinator = new SharedProjectionEpochCoordinator(factory.Store, factory.Store);
        var blocked = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recorded = new TaskCompletionSource<HttpBoundaryReadObservation>(TaskCreationOptions.RunContinuationsAsynchronously);
        int claimed = 0;
        CancellationToken testCancellation = TestContext.Current.CancellationToken;
        HttpBoundaryReadObservation? blockedRead = null;
        factory.Store.BeforeReadAsync = async (read, cancellationToken) =>
        {
            if (injectForeignRequestRead && read.Key == requestEpochKey && read.RequestId is not null)
            {
                // Fault injection performs a real store read in the HTTP execution context.
                // The audit must reject it even though this key is legitimate for setup cleanup.
                await factory.Store.GetAsync<object>(setupScope.StoreName, setupEpochKey, cancellationToken);
            }

            // Commit's foreground read uses the test token. The scheduled cleanup owns
            // a cancellable SDK budget and runs outside HTTP. Verify its stack below as well.
            if (read.Key == setupEpochKey && cancellationToken.CanBeCanceled && cancellationToken != testCancellation
                && read.RequestId is null)
            {
                string stack = Environment.StackTrace;
                // Verify the operation before claiming the barrier; a metadata-only read
                // must never steal it from the demonstrated chunk-cleanup operation.
                if (stack.Contains("SchedulePromotedCleanup", StringComparison.Ordinal)
                    && stack.Contains("ReconcileChunkCleanupAsync", StringComparison.Ordinal)
                    && Interlocked.CompareExchange(ref claimed, 1, 0) == 0)
                {
                    blockedRead = read;
                    blocked.SetResult(stack);
                    await release.Task.WaitAsync(cancellationToken);
                }
            }
        };
        factory.Store.ReadObserved = read =>
        {
            if (ReferenceEquals(read, blockedRead))
            {
                recorded.TrySetResult(read);
            }
        };

        try
        {
            // The real SDK's already-promoted branch schedules cleanup on a fresh coordinator.
            // No thread-pool settings, sleeps or production timeouts are changed.
            await coordinator.CommitAsync(setupScope, "fixture-catalog-rebuild", testCancellation);
            string stack = await blocked.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            stack.ShouldContain("SchedulePromotedCleanup");
            stack.ShouldContain("ReconcileChunkCleanupAsync");
            int windowStart = factory.Store.Reads.Count;
            factory.Gateway.Requests.Clear();
            var capabilityId = new MagicLinkCapabilityId("capability-admin-new");
            var command = new IssueMagicLinkConfirmationCapability(
                capabilityId,
                new MagicLinkConfirmationScope(Contributor(), TimeEntryTargetReference.ForProject(Project()),
                    ActivityId(), new TimeEntryId("time-entry-admin-new"), MagicLinkTargetKind.ProposedTimeEntry),
                MagicLinkAllowedAction.Confirm, ObservedAtUtc.AddDays(1), new MagicLinkAuditMetadata("timesheets", "admin-issue"));
            using HttpResponseMessage response = await factory.PostProtectedManagementAsync(
                client, "/api/timesheets/magic-links/confirmation-capabilities", command);
            TimesheetsRequestContext context = factory.AccessGuard.LastAuthorizationRequest.ShouldNotBeNull().Context;
            context.Tenant.ShouldBe(OtherTenant());
            context.Actor.ShouldBe(OtherContributor());
            context.CorrelationId.ShouldBe(factory.ObservedTrustedContext.CorrelationId);
            factory.Gateway.Requests.ShouldHaveSingleItem().Tenant.ShouldBe(OtherTenant().TenantId);
            factory.Gateway.Requests[0].AggregateId.ShouldBe(capabilityId.Value);
            response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Forbidden);

            // Release after the HTTP response, before the original global read-set assertion.
            release.TrySetResult();
            HttpBoundaryReadObservation cleanup = await recorded.Task.WaitAsync(
                TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            cleanup.RequestId.ShouldBeNull();
            HttpBoundaryReadObservation[] globalWindow = factory.Store.Reads.Skip(windowStart).ToArray();
            globalWindow.Select(read => read.Key).ShouldContain(setupEpochKey);
            globalWindow.Select(read => read.Key).ShouldContain(requestEpochKey);
            Should.Throw<ShouldAssertException>(() => AssertAdminCatalogReadSet(globalWindow, OtherTenant(), 0))
                .Message.ShouldContain(setupEpochKey);
            HttpBoundaryReadObservation[] requestReads = factory.Store.ReadsFor(context.CorrelationId);
            if (injectForeignRequestRead)
            {
                requestReads.Select(read => read.Key).ShouldContain(setupEpochKey);
                Should.Throw<ShouldAssertException>(() => AssertAdminCatalogReadSet(requestReads, OtherTenant(), 0))
                    .Message.ShouldContain(setupEpochKey);
            }
            else
            {
                AssertAdminCatalogReadSet(requestReads, OtherTenant(), 0);
                requestReads.Select(read => read.Key).ShouldBe([requestEpochKey]);
            }

            // Persist only synthetic key/context and caller evidence in the test result output.
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"Cleanup read: {cleanup.Key}; HTTP request: <none>; injected HTTP fault: {injectForeignRequestRead}; request read set: {string.Join(",", requestReads.Select(read => read.Key))}\n{stack}");
        }
        finally
        {
            release.TrySetResult();
            // The pinned SDK exposes its own drain internally. Use it only for test lifetime,
            // including assertion failures, and wait for the entire explicitly scheduled task.
            await PromotedCleanupTestDrain.WaitAsync(coordinator, setupScope, CancellationToken.None);
            factory.Store.BeforeReadAsync = null;
            factory.Store.ReadObserved = null;
        }
    }

    private static void AssertAdminCatalogReadSet(
        IReadOnlyList<HttpBoundaryReadObservation> reads, TenantReference tenant, int expectedPhysicalReads)
    {
        SharedProjectionScope scope = TenantActivityTypeCatalogEpoch.CreateScope(tenant.TenantId);
        string epochKey = "shared-epoch:" + scope.ComputeHash();
        string physicalPrefix = "shared-generation:" + scope.ComputeHash() + ":";
        reads.ShouldAllBe(read => read.StoreName == scope.StoreName);
        string[] keys = reads.Select(read => read.Key).ToArray();
        keys.ShouldNotContain(MagicLinkActivityTypeCatalogReadModelAddress.StateKey(tenant));
        keys.ShouldContain(epochKey);
        keys.Count(key => key.StartsWith(physicalPrefix, StringComparison.Ordinal)).ShouldBe(expectedPhysicalReads);
        keys.ShouldAllBe(key => key == epochKey || key.StartsWith(physicalPrefix, StringComparison.Ordinal));
    }

}
