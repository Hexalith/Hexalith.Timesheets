using System.Net;
using System.Text;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections.MagicLinks;
using Hexalith.Timesheets.Projections.TimeEntries;
using Hexalith.Timesheets.Server.MagicLinks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;


namespace Hexalith.Timesheets.IntegrationTests;

public sealed partial class MagicLinkConfirmationHttpBoundaryTests
{
    [Fact]
    public async Task OrchestratorDeliversAllMappedHandlersThroughComposedHostAfterRestart()
    {
        var store = new InMemoryReadModelStore();
        using MagicLinkHttpBoundaryFactory factory = new(batchStore: store);
        using HttpClient host = factory.CreateClient();
        using IServiceScope scope = factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        DomainProjectionIdentityOptions options = services
            .GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
        ProjectionDispatchRoute[] routes = [.. services.GetServices<IAsyncDomainProjectionHandler>()
            .Where(static handler => handler.Domain == "timesheets")
            .Select(static handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))];
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(options.AppId,
            options.ServiceVersion, routes);
        var catalog = new NamedProjectionRouteCatalog();
        catalog.Replace(new NamedProjectionRouteCatalogSnapshot([
            new NamedProjectionRouteCatalogEntry(options.AppId, options.ServiceVersion, "timesheets",
                ProjectionDispatchProtocol.Version, ProjectionDispatchProtocol.Capability, fingerprint,
                [TimeEntryEvidenceProjection.ProjectionName,
                    MagicLinkConfirmationCapabilityProjection.ProjectionName,
                    MagicLinkTokenHashCapabilityIndexProjection.ProjectionName],
                [TimeEntryEvidenceProjection.ProjectionName],
                [TimeEntryEvidenceProjection.ProjectionName],
                [MagicLinkConfirmationCapabilityProjection.ProjectionName,
                    MagicLinkTokenHashCapabilityIndexProjection.ProjectionName], [],
                [TimeEntryEvidenceProjection.ProjectionName]),
        ]));
        var capabilityId = new MagicLinkCapabilityId("composed-orchestrator-capability");
        var entryId = new TimeEntryId("composed-orchestrator-entry");
        var capabilityOwner = new AggregateIdentity(Tenant().TenantId, "timesheets", capabilityId.Value);
        var entryOwner = new AggregateIdentity(Tenant().TenantId, "timesheets", entryId.Value);
        var issued = new MagicLinkConfirmationCapabilityIssued(capabilityId, Tenant(), Contributor(),
            TimeEntryTargetReference.ForProject(Project()), ActivityId(), entryId,
            MagicLinkTargetKind.ProposedTimeEntry, MagicLinkAllowedAction.Confirm,
            new MagicLinkTokenHash("composed-orchestrator-private-hash"), ObservedAtUtc.AddDays(1),
            Operator(), ObservedAtUtc.AddHours(-1),
            new MagicLinkAuditMetadata("timesheets", "orchestrator-issue"), true);
        var recorded = new TimeEntryRecorded(entryId, TimeEntryTargetReference.ForProject(Project()),
            Contributor(), ActivityId(), ActivityTypeScope.Tenant, new DateOnly(2026, 6, 19), 60,
            BillableState.Billable, TimeEntryApprovalState.Draft,
            ContributorCategory.ExternalContributor, null);
        var used = new MagicLinkConfirmationCapabilityUsed(capabilityId, Tenant(), Contributor(),
            entryId, ObservedAtUtc, new MagicLinkAuditMetadata("timesheets", "orchestrator-use"));
        EventEnvelope Envelope(AggregateIdentity owner, long sequence, long position, object payload)
            => new($"01J{position:D23}", owner.AggregateId,
                "timesheets", owner.TenantId, owner.Domain, sequence, position, ObservedAtUtc,
                "orchestrator-correlation", "orchestrator-causation", Operator().PartyId,
                options.ServiceVersion, payload.GetType().FullName!, 1, "json",
                JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions), null);
        var histories = new Dictionary<AggregateIdentity, EventEnvelope[]>
        {
            [capabilityOwner] = [Envelope(capabilityOwner, 1, 1, new StoredMagicLinkIssued(issued))],
            [entryOwner] = [Envelope(entryOwner, 1, 2, recorded),
                Envelope(entryOwner, 2, 3, new StoredMagicLinkUsed(used))]
        };
        IActorProxyFactory proxies = Substitute.For<IActorProxyFactory>();
        foreach ((AggregateIdentity owner, EventEnvelope[] events) in histories)
        {
            IAggregateActor actor = Substitute.For<IAggregateActor>();
            actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(true, events.Length));
            actor.ReadEventsRangeAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<int>())
                .Returns(call => events.Where(item => item.SequenceNumber > call.ArgAt<long>(0)
                    && item.SequenceNumber <= call.ArgAt<long>(1))
                    .Take(call.ArgAt<int>(2)).ToArray());
            proxies.CreateActorProxy<IAggregateActor>(new ActorId(owner.ActorId), nameof(AggregateActor))
                .Returns(actor);
        }
        var reader = new EventStoreProjectionDeliveryHistoryReader(proxies,
            new NoOpEventPayloadProtectionService(), Options.Create(new EventStoreActorOptions()));
        var states = new Dictionary<string, (ProjectionDeliveryState State, int Revision)>(StringComparer.Ordinal);
        IProjectionDeliveryStateStore deliveryStore = Substitute.For<IProjectionDeliveryStateStore>();
        _ = deliveryStore.ReadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>()).Returns(call =>
            {
                string key = call.ArgAt<AggregateIdentity>(0).ActorId + ":" + call.ArgAt<string>(1);
                bool found = states.TryGetValue(key, out (ProjectionDeliveryState State, int Revision) entry);
                ProjectionDeliveryState? state = found ? entry.State : null;
                return new ProjectionDeliveryStateReadResult(state,
                    found ? entry.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty,
                    ProjectionDeliveryStateClassifier.Classify(state, protocolV2Active: true), true);
            });
        _ = deliveryStore.TrySaveAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(),
                Arg.Any<ProjectionDeliveryState>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                string key = call.ArgAt<AggregateIdentity>(0).ActorId + ":" + call.ArgAt<string>(1);
                int revision = states.TryGetValue(key, out (ProjectionDeliveryState State, int Revision) entry)
                    ? entry.Revision : 0;
                if (!string.Equals(call.ArgAt<string>(3), revision == 0 ? string.Empty :
                    revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    StringComparison.Ordinal)) return false;
                states[key] = (call.ArgAt<ProjectionDeliveryState>(2), revision + 1);
                return true;
            });
        var idempotency = new ProjectionDeliveryIdempotencyCoordinator(deliveryStore,
            Options.Create(new ProjectionDeliveryIdempotencyOptions()), TimeProvider.System);
        var retry = new ComposedRetryScheduler();
        var lifecycle = Substitute.For<IProjectionLifecycleGateway>();
        lifecycle.BeginDeliveryWriteAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        lifecycle.CompleteDeliveryWriteAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var forwarder = new FailFirstCommitForwarder(factory, host);
        using var forwardingClient = new HttpClient(forwarder);
        IHttpClientFactory clients = Substitute.For<IHttpClientFactory>();
        clients.CreateClient(Arg.Any<string>()).Returns(forwardingClient);
        IDomainServiceResolver resolver = Substitute.For<IDomainServiceResolver>();
        resolver.ResolveAsync(Arg.Any<string>(), "timesheets", "v1", Arg.Any<CancellationToken>())
            .Returns(call => new DomainServiceRegistration(options.AppId, "process",
                call.ArgAt<string>(0), "timesheets", options.ServiceVersion));
        IProjectionCheckpointTracker tracker = Substitute.For<IProjectionCheckpointTracker>();
        tracker.ReadLastDeliveredSequenceAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>())
            .Returns(0);
        ProjectionUpdateOrchestrator CreateOrchestrator()
        {
            var coordinator = new NamedProjectionDispatchCoordinator(catalog,
                Substitute.For<IProjectionDeliveryCheckpointStore>(), lifecycle, proxies,
                new DaprClientBuilder().Build(), clients, Options.Create(new ProjectionDispatchOptions()),
                NullLogger<NamedProjectionDispatchCoordinator>.Instance, retry,
                idempotencyCoordinator: idempotency, historyReader: reader);
            return new ProjectionUpdateOrchestrator(proxies, new DaprClientBuilder().Build(), clients,
                resolver, tracker, Options.Create(new ProjectionOptions()),
                NullLogger<ProjectionUpdateOrchestrator>.Instance,
                namedProjectionDispatchCoordinator: coordinator);
        }

        await CreateOrchestrator().DeliverProjectionAsync(capabilityOwner,
            TestContext.Current.CancellationToken);
        retry.Work.ShouldNotBeNull().PendingRoutes.ShouldBe([TimeEntryEvidenceProjection.ProjectionName]);
        forwarder.FailedCommits.ShouldBe(1);
        forwarder.ProjectionCalls.ShouldBe(1);
        await CreateOrchestrator().DeliverProjectionAsync(capabilityOwner,
            TestContext.Current.CancellationToken);
        retry.Work.ShouldBeNull();
        forwarder.ProjectionCalls.ShouldBe(1);
        await CreateOrchestrator().DeliverProjectionAsync(entryOwner,
            TestContext.Current.CancellationToken);
        retry.Work.ShouldBeNull();
        forwarder.ProjectionCalls.ShouldBe(3);

        TimeEntryEvidenceReadModel evidence = (await store.GetAsync<TimeEntryEvidenceReadModel>(
            TimeEntryEvidenceReadModelAddress.StateStoreName,
            TimeEntryEvidenceReadModelAddress.StateKey(entryOwner.TenantId, entryId),
            TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        evidence.TimeEntryId.ShouldBe(entryId);
        evidence.EventLineage.ShouldHaveSingleItem().EventName.ShouldBe("TimeEntryRecorded");
        MagicLinkConfirmationCapabilityProjectionHandler capability = services
            .GetServices<IAsyncDomainProjectionHandler>()
            .OfType<MagicLinkConfirmationCapabilityProjectionHandler>().Single();
        SharedProjectionEpochCoordinator epoch = services.GetRequiredService<SharedProjectionEpochCoordinator>();
        SharedProjectionReadResult<MagicLinkConfirmationCapabilityReadModel> selected =
            await capability.ReadObservedAsync(Tenant().TenantId, capabilityId, ObservedAtUtc, epoch,
                TestContext.Current.CancellationToken);
        selected.IsAvailable.ShouldBeTrue();
        selected.Value.ShouldNotBeNull().State.ShouldBe(
            Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Used);
        SharedProjectionScope indexScope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        SharedProjectionReadResult<MagicLinkTokenHashCapabilityIndexTreeRoot> selectedIndex =
            await epoch.ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(indexScope,
                MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
                TestContext.Current.CancellationToken);
        selectedIndex.IsAvailable.ShouldBeTrue();
        (await new MagicLinkTokenHashCapabilityIndexTreeReader(epoch, indexScope)
            .FindCandidatesAsync(selectedIndex.Value.ShouldNotBeNull().TokenRoot,
                issued.TokenHash.Value, TestContext.Current.CancellationToken))
            .ShouldHaveSingleItem().CapabilityId.ShouldBe(capabilityId);
        states[capabilityOwner.ActorId + ":" + MagicLinkConfirmationCapabilityProjection.ProjectionName]
            .State.LastDeliveredSequence.ShouldBe(1);
        states[entryOwner.ActorId + ":" + TimeEntryEvidenceProjection.ProjectionName]
            .State.LastDeliveredSequence.ShouldBe(2);
    }

    private sealed class FailFirstCommitForwarder(MagicLinkHttpBoundaryFactory factory,
        HttpClient host) : HttpMessageHandler
    {
        public int FailedCommits { get; private set; }

        public int ProjectionCalls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/project/v2", StringComparison.Ordinal) == true)
                ProjectionCalls++;
            if (FailedCommits == 0 && request.RequestUri?.AbsolutePath.EndsWith(
                    "/project/rebuild/commit/v1", StringComparison.Ordinal) == true)
            {
                FailedCommits++;
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                };
            }
            return await factory.ForwardCoordinatorProjectionAsync(host, request, cancellationToken);
        }
    }

    private sealed class ComposedRetryScheduler : IProjectionDeliveryRetryScheduler
    {
        public ProjectionDeliveryRetryWorkItem? Work { get; private set; }

        public Task<ProjectionDeliveryRetryWorkItem> ScheduleAsync(ProjectionDeliveryRetryWorkItem item,
            CancellationToken cancellationToken = default)
        {
            Work ??= item;
            return Task.FromResult(Work);
        }

        public Task<ProjectionDeliveryRetryWorkItem?> TryAcquireAsync(ProjectionDeliveryRetryWorkItem item,
            string leaseOwner, DateTimeOffset nowUtc, TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            Work = Work is null ? null : Work with { Revision = Work.Revision + 1,
                LeaseOwner = leaseOwner, LeaseExpiresUtc = nowUtc + leaseDuration };
            return Task.FromResult(Work);
        }

        public Task<bool> TryUpdateAsync(ProjectionDeliveryRetryWorkItem item,
            CancellationToken cancellationToken = default)
        {
            Work = item;
            return Task.FromResult(true);
        }

        public Task<bool> TryDeleteAsync(ProjectionDeliveryRetryWorkItem item,
            CancellationToken cancellationToken = default)
        {
            Work = null;
            return Task.FromResult(true);
        }

        public Task UpdateAsync(ProjectionDeliveryRetryWorkItem item,
            CancellationToken cancellationToken = default)
        {
            Work = item;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string workId, CancellationToken cancellationToken = default)
        {
            Work = null;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ProjectionDeliveryRetryWorkItem>> GetDueAsync(DateTimeOffset dueUtc,
            int maximumCount, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectionDeliveryRetryWorkItem>>(
                Work is null ? [] : [Work]);
    }
}
