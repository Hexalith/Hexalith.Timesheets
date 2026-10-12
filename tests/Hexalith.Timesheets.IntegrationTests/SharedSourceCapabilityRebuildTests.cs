using System.Security.Cryptography;
using System.Net.Http.Json;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed partial class MagicLinkConfirmationHttpBoundaryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(9_998)]
    public async Task ActorGroundedSharedSourceProducerCommitsComposedCapabilityAndSelectedReadback(int extraOwners)
    {
        var store = new InMemoryReadModelStore();
        using MagicLinkHttpBoundaryFactory factory = new(batchStore: store);
        using HttpClient client = factory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(15);
        using IServiceScope serviceScope = factory.Services.CreateScope();
        IServiceProvider services = serviceScope.ServiceProvider;
        DomainProjectionIdentityOptions domainIdentity = services
            .GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
        ProjectionDispatchRoute[] routes = [.. services.GetServices<IAsyncDomainProjectionHandler>()
            .Where(static item => item.Domain == "timesheets")
            .Select(static item => new ProjectionDispatchRoute(item.Domain, item.ProjectionType))];
        string catalog = ProjectionRouteCatalogFingerprint.Compute(
            domainIdentity.AppId, domainIdentity.ServiceVersion, routes);
        var identity = new DomainSharedProjectionRebuildIdentity(Tenant().TenantId, "timesheets",
            MagicLinkConfirmationCapabilityProjection.ProjectionName, "actor-grounded-composed", catalog);
        var scope = new SharedProjectionSourceScope(identity.TenantId, identity.Domain);
        var capabilityId = new MagicLinkCapabilityId("actor-grounded-capability");
        var entryId = new TimeEntryId("actor-grounded-entry");
        var issued = new MagicLinkConfirmationCapabilityIssued(
            capabilityId, Tenant(), Contributor(), TimeEntryTargetReference.ForProject(Project()),
            ActivityId(), entryId, MagicLinkTargetKind.ProposedTimeEntry, MagicLinkAllowedAction.Confirm,
            new MagicLinkTokenHash("private-actor-grounded-hash"), ObservedAtUtc.AddDays(1), Operator(),
            ObservedAtUtc.AddHours(-1), new MagicLinkAuditMetadata("timesheets", "source-issue"), true);
        var used = new MagicLinkConfirmationCapabilityUsed(capabilityId, Tenant(), Contributor(), entryId,
            ObservedAtUtc, new MagicLinkAuditMetadata("timesheets", "source-use"));
        ProjectionEventDto Event(long position, object payload) => new(payload.GetType().FullName!,
            JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions), "json", 1, ObservedAtUtc,
            "source-correlation", "source-message-" + position, Operator().PartyId, position);
        var ownerList = new List<AggregateIdentity> {
            new(identity.TenantId, identity.Domain, capabilityId.Value),
            new(identity.TenantId, identity.Domain, entryId.Value) };
        var sourceEvents = new Dictionary<AggregateIdentity, ProjectionEventDto[]>
        {
            [ownerList[0]] = [Event(1, new StoredMagicLinkIssued(issued))],
            [ownerList[1]] = [Event(2, new StoredMagicLinkUsed(used))]
        };
        for (int index = 0; index < extraOwners; index++)
        {
            string id = "cap-" + index.ToString("D5", System.Globalization.CultureInfo.InvariantCulture)
                + "-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var owner = new AggregateIdentity(identity.TenantId, identity.Domain, id);
            MagicLinkConfirmationCapabilityIssued extra = issued with
            {
                CapabilityId = new MagicLinkCapabilityId(id),
                TokenHash = new MagicLinkTokenHash(Convert.ToHexString(RandomNumberGenerator.GetBytes(32))),
                Source = new MagicLinkAuditMetadata("timesheets", "source-" + id)
            };
            ownerList.Add(owner);
            sourceEvents.Add(owner, [Event(index + 3, new StoredMagicLinkIssued(extra))]);
        }
        AggregateIdentity[] owners = [.. ownerList];
        AggregateIdentity[][] pages = [.. Enumerable.Range(0, 64).Select(partition =>
            owners.Where((_, ordinal) => ordinal % 64 == partition).ToArray())];
        SharedProjectionCapturedSourceOwner[][] capturedPages = new SharedProjectionCapturedSourceOwner[64][];
        var roster = Substitute.For<ISharedProjectionSourceRosterActor>();
        bool begun = false;
        bool sealedCut = false;
        bool released = false;
        bool accumulated = false;
        string? sealedFingerprint = null;
        roster.ReadCaptureProgressAsync(scope, identity.OperationId).Returns(_ => begun
            ? new SharedProjectionSourceCaptureProgress(9, sealedCut, !released, accumulated,
                sealedFingerprint, owners.Length, false) : null);
        roster.BeginCaptureAsync(scope, identity.OperationId).Returns(_ => { begun = true; return 9L; });
        roster.ReadPageAsync(scope, identity.OperationId, Arg.Any<int>()).Returns(call =>
            new SharedProjectionSourceRosterPage(9, pages[call.Arg<int>()]));
        roster.WriteCapturedPageAsync(scope, identity.OperationId, Arg.Any<int>(),
            Arg.Any<IReadOnlyList<SharedProjectionCapturedSourceOwner>>()).Returns(call =>
        {
            capturedPages[call.ArgAt<int>(2)] = [.. call.ArgAt<IReadOnlyList<SharedProjectionCapturedSourceOwner>>(3)];
            return true;
        });
        roster.SealCaptureAsync(scope, identity.OperationId, Arg.Any<string>(),
            identity.ProjectionType, identity.CatalogFingerprint).Returns(call =>
        {
            sealedCut = true;
            sealedFingerprint = call.ArgAt<string>(2);
            return true;
        });
        roster.ReadCapturedPageAsync(scope, identity.OperationId, Arg.Any<int>()).Returns(call =>
            new SharedProjectionCapturedSourcePage(9, capturedPages[call.Arg<int>()]));
        roster.ReleaseAppendFenceAsync(scope, identity.OperationId).Returns(_ => { released = true; return true; });
        roster.MarkOwnersAccumulatedAsync(scope, identity.OperationId).Returns(_ =>
        { accumulated = true; return true; });
        roster.CompleteCaptureAsync(scope, identity.OperationId).Returns(_ => accumulated);
        var proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<ISharedProjectionSourceRosterActor>(new ActorId(scope.ActorId),
            SharedProjectionSourceRosterActor.ActorTypeName).Returns(roster);
        var aggregate = Substitute.For<IAggregateActor>();
        aggregate.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(true, 1));
        proxies.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor").Returns(aggregate);
        var histories = Substitute.For<IProjectionDeliveryHistoryReader>();
        histories.ReadAsync(Arg.Any<AggregateIdentity>(), 1, Arg.Any<CancellationToken>()).Returns(call =>
            sourceEvents[call.ArgAt<AggregateIdentity>(0)]);
        var handler = services.GetServices<IAsyncDomainProjectionHandler>()
            .OfType<MagicLinkConfirmationCapabilityProjectionHandler>().Single();
        SharedProjectionEpochCoordinator coordinator = services.GetRequiredService<SharedProjectionEpochCoordinator>();
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope(identity.TenantId), handler.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        var gateway = new ComposedRebuildGateway(factory, client);
        var producer = new SharedProjectionSourceBeginProducer(proxies, histories, gateway,
            Options.Create(new EventStoreActorOptions()), Options.Create(new SharedProjectionSourceCaptureOptions
                { EnabledDomains = ["timesheets"] }));
        DomainSharedProjectionRebuildResponse result = await producer.RebuildAsync(identity,
            TestContext.Current.CancellationToken);
        result.Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Committed);
        released.ShouldBeTrue();
        accumulated.ShouldBeTrue();
        SharedProjectionReadResult<MagicLinkConfirmationCapabilityReadModel> selected =
            await handler.ReadObservedAsync(identity.TenantId, capabilityId, ObservedAtUtc, coordinator,
                TestContext.Current.CancellationToken);
        selected.IsAvailable.ShouldBeTrue();
        selected.Value.ShouldNotBeNull().State.ShouldBe(
            Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Used);
        if (extraOwners == 0)
        {
            DomainSharedProjectionRebuildIdentity erasedIdentity = identity with
                { OperationId = "actor-grounded-erasure" };
            var erasedRoster = Substitute.For<ISharedProjectionSourceRosterActor>();
            bool erasureBegun = false;
            bool erasureSealed = false;
            bool erasureReleased = false;
            bool erasureAccumulated = false;
            string? erasureFingerprint = null;
            erasedRoster.ReadCaptureProgressAsync(scope, erasedIdentity.OperationId).Returns(_ => erasureBegun
                ? new SharedProjectionSourceCaptureProgress(12, erasureSealed, !erasureReleased,
                    erasureAccumulated, erasureFingerprint, owners.Length, false) : null);
            erasedRoster.BeginCaptureAsync(scope, erasedIdentity.OperationId)
                .Returns(_ => { erasureBegun = true; return 12L; });
            erasedRoster.ReadPageAsync(scope, erasedIdentity.OperationId, Arg.Any<int>()).Returns(call =>
                new SharedProjectionSourceRosterPage(12, pages[call.Arg<int>()]));
            SharedProjectionCapturedSourceOwner[][] erasedCapturedPages = new SharedProjectionCapturedSourceOwner[64][];
            erasedRoster.WriteCapturedPageAsync(scope, erasedIdentity.OperationId, Arg.Any<int>(),
                Arg.Any<IReadOnlyList<SharedProjectionCapturedSourceOwner>>()).Returns(call =>
            {
                erasedCapturedPages[call.ArgAt<int>(2)] =
                    [.. call.ArgAt<IReadOnlyList<SharedProjectionCapturedSourceOwner>>(3)];
                return true;
            });
            erasedRoster.SealCaptureAsync(scope, erasedIdentity.OperationId, Arg.Any<string>(),
                erasedIdentity.ProjectionType, erasedIdentity.CatalogFingerprint).Returns(call =>
            {
                erasureSealed = true;
                erasureFingerprint = call.ArgAt<string>(2);
                return true;
            });
            erasedRoster.ReadCapturedPageAsync(scope, erasedIdentity.OperationId, Arg.Any<int>()).Returns(call =>
                new SharedProjectionCapturedSourcePage(12, erasedCapturedPages[call.Arg<int>()]));
            erasedRoster.ReleaseAppendFenceAsync(scope, erasedIdentity.OperationId)
                .Returns(_ => { erasureReleased = true; return true; });
            erasedRoster.MarkOwnersAccumulatedAsync(scope, erasedIdentity.OperationId)
                .Returns(_ => { erasureAccumulated = true; return true; });
            erasedRoster.CompleteCaptureAsync(scope, erasedIdentity.OperationId).Returns(_ => erasureAccumulated);
            proxies.CreateActorProxy<ISharedProjectionSourceRosterActor>(new ActorId(scope.ActorId),
                SharedProjectionSourceRosterActor.ActorTypeName).Returns(erasedRoster);
            foreach (AggregateIdentity owner in owners)
            {
                var erasedActor = Substitute.For<IAggregateActor>();
                erasedActor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(false, 0, true,
                    sourceEvents[owner][0].GlobalPosition));
                proxies.CreateActorProxy<IAggregateActor>(new ActorId(owner.ActorId), "AggregateActor")
                    .Returns(erasedActor);
            }
            var erasedProducer = new SharedProjectionSourceBeginProducer(proxies, histories, gateway,
                Options.Create(new EventStoreActorOptions()), Options.Create(new SharedProjectionSourceCaptureOptions
                    { EnabledDomains = ["timesheets"] }));
            (await erasedProducer.RebuildAsync(erasedIdentity, TestContext.Current.CancellationToken))
                .Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Committed);
            SharedProjectionReadResult<MagicLinkConfirmationCapabilityReadModel> pruned =
                await handler.ReadObservedAsync(identity.TenantId, capabilityId, ObservedAtUtc, coordinator,
                    TestContext.Current.CancellationToken);
            pruned.IsAvailable.ShouldBeTrue();
            pruned.Value.ShouldBeNull();
        }
        else
        {
            SharedProjectionReadResult<MagicLinkConfirmationCapabilityReadModel> last =
                await handler.ReadObservedAsync(identity.TenantId,
                    new MagicLinkCapabilityId(owners[^1].AggregateId), ObservedAtUtc, coordinator,
                    TestContext.Current.CancellationToken);
            last.IsAvailable.ShouldBeTrue();
            last.Value.ShouldNotBeNull().State.ShouldBe(
                Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Issued);
        }
    }

    private sealed class ComposedRebuildGateway(MagicLinkHttpBoundaryFactory factory,
        HttpClient client) : ISharedProjectionRebuildGateway
    {
        public async Task<DomainSharedProjectionRebuildResponse> SendAsync(
            DomainSharedProjectionRebuildRequest request, CancellationToken cancellationToken = default)
        {
            using HttpResponseMessage response = await factory.PostSharedProjectionAsync(client, request,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<DomainSharedProjectionRebuildResponse>(
                JsonOptions, cancellationToken)).ShouldNotBeNull();
        }
    }
}
