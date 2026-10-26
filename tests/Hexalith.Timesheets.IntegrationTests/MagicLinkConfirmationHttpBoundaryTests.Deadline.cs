using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

using Polly.Timeout;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed partial class MagicLinkConfirmationHttpBoundaryTests
{
    [Fact]
    public async Task GatewayDefaultHttpDeadlineCancelsMappedBootstrapAndColdRetryDrainsPersistedPendingWork()
    {
        var inner = new InMemoryReadModelStore();
        var blocking = new DeadlineBootstrapStore(inner);
        var capabilityId = new MagicLinkCapabilityId("http-deadline-capability");
        var issued = new MagicLinkConfirmationCapabilityIssued(capabilityId, Tenant(), Contributor(),
            TimeEntryTargetReference.ForProject(Project()), ActivityId(), new("http-deadline-entry"),
            MagicLinkTargetKind.ProposedTimeEntry, MagicLinkAllowedAction.Confirm, new("http-deadline-hash"),
            ObservedAtUtc.AddDays(1), Operator(), ObservedAtUtc, new("timesheets", "http-deadline"), true);
        var projectionEvent = new ProjectionEventDto(typeof(StoredMagicLinkIssued).FullName!,
            JsonSerializer.SerializeToUtf8Bytes(new StoredMagicLinkIssued(issued), JsonOptions), "json", 1,
            ObservedAtUtc, "http-deadline-correlation", "http-deadline-message", Operator().PartyId, 1);
        SharedProjectionCheckpoint? acceptedCheckpoint = null;
        using (MagicLinkHttpBoundaryFactory factory = new(batchStore: inner, readModelStore: blocking))
        using (HttpClient host = factory.CreateClient())
        {
            ProjectionDispatchRequest delivery = DeadlineDelivery(factory, capabilityId, projectionEvent);
            HostApplicationBuilder sender = Host.CreateApplicationBuilder();
            Hexalith.EventStore.ServiceDefaults.Extensions.AddServiceDefaults(sender);
            sender.Services.AddHttpClient(Options.DefaultName).ConfigurePrimaryHttpMessageHandler(() => new CoordinatorHostForwarder(factory, host));
            using IHost gateway = sender.Build();
            HttpClient client = gateway.Services.GetRequiredService<IHttpClientFactory>().CreateClient();
            // This is the unnamed client and standard pipeline installed by the actual gateway defaults.
            HttpStandardResilienceOptions effective = gateway.Services
                .GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>().Get("-standard");
            effective.AttemptTimeout.Timeout.ShouldBe(TimeSpan.FromSeconds(10));
            effective.TotalRequestTimeout.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
            using var dapr = new DaprClientBuilder().Build();
            using HttpRequestMessage request = dapr.CreateInvokeMethodRequest("timesheets", "project/v2", delivery);
            await Should.ThrowAsync<TimeoutRejectedException>(() => client.SendAsync(request, TestContext.Current.CancellationToken));
            blocking.CancelledReads.ShouldBeGreaterThan(0);
            blocking.Tokens.ShouldAllBe(token => token.IsCancellationRequested);
            var epoch = new SharedProjectionEpochCoordinator(inner, inner);
            (await epoch.GetStatusAsync(MagicLinkTokenHashCapabilityIndexProjection.GlobalScope(), TestContext.Current.CancellationToken))
                .PendingDeliveryCount.ShouldBe(1);
            acceptedCheckpoint = (await epoch.GetCheckpointAsync(MagicLinkTokenHashCapabilityIndexProjection.GlobalScope(),
                new AggregateIdentity(Tenant().TenantId, "timesheets", capabilityId.Value).ActorId, TestContext.Current.CancellationToken)).ShouldNotBeNull();
            acceptedCheckpoint.OwnerSequenceNumber.ShouldBe(1);
            (await epoch.ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(
                MagicLinkTokenHashCapabilityIndexProjection.GlobalScope(), 0,
                MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, TestContext.Current.CancellationToken)).Value.ShouldBeNull();
        }
        using (MagicLinkHttpBoundaryFactory cold = new(batchStore: inner))
        using (HttpClient host = cold.CreateClient())
        {
            using HttpResponseMessage retry = await cold.PostProjectionAsync(host, "/project/v2", DeadlineDelivery(cold, capabilityId, projectionEvent));
            retry.StatusCode.ShouldBe(HttpStatusCode.OK);
            var response = (await retry.Content.ReadFromJsonAsync<ProjectionDispatchResponse>(JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull();
            response.Outcomes.ShouldHaveSingleItem().Status.ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
            var epoch = new SharedProjectionEpochCoordinator(inner, inner);
            (await epoch.GetStatusAsync(MagicLinkTokenHashCapabilityIndexProjection.GlobalScope(), TestContext.Current.CancellationToken)).PendingDeliveryCount.ShouldBe(0);
            SharedProjectionCheckpoint recoveredCheckpoint = (await epoch.GetCheckpointAsync(MagicLinkTokenHashCapabilityIndexProjection.GlobalScope(), new AggregateIdentity(Tenant().TenantId, "timesheets", capabilityId.Value).ActorId,
                TestContext.Current.CancellationToken)).ShouldNotBeNull();
            recoveredCheckpoint.OwnerSequenceNumber.ShouldBe(acceptedCheckpoint!.OwnerSequenceNumber);
            recoveredCheckpoint.Position.ShouldBe(acceptedCheckpoint.Position);
            recoveredCheckpoint.Token.ShouldBe(acceptedCheckpoint.Token);
            var root = (await epoch.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(MagicLinkTokenHashCapabilityIndexProjection.GlobalScope(),
                MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
            root.StorageVersion.ShouldBe(7);
        }
    }

    private static ProjectionDispatchRequest DeadlineDelivery(MagicLinkHttpBoundaryFactory factory,
        MagicLinkCapabilityId capabilityId, ProjectionEventDto projectionEvent)
    {
        using IServiceScope serviceScope = factory.Services.CreateScope();
        IServiceProvider services = serviceScope.ServiceProvider;
        DomainProjectionIdentityOptions identity = services.GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
        ProjectionDispatchRoute[] routes = services.GetServices<IAsyncDomainProjectionHandler>()
            .Where(handler => handler.Domain == "timesheets")
            .Select(handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType)).ToArray();
        return new(new ProjectionRequest(Tenant().TenantId, "timesheets", capabilityId.Value, [projectionEvent]),
            [MagicLinkTokenHashCapabilityIndexProjection.ProjectionName], "http-deadline-delivery",
            ProjectionRouteCatalogFingerprint.Compute(identity.AppId, identity.ServiceVersion, routes));
    }

}
