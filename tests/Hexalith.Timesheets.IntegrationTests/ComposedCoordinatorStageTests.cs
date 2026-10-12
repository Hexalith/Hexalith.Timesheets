using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
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
    public async Task CoordinatorProducedTimeEntryCommitRetriesAfterHostRestartWithoutPartialPublication()
    {
        var store = new InMemoryReadModelStore();
        var entryId = new TimeEntryId("coordinator-retry-entry");
        var owner = new AggregateIdentity(Tenant().TenantId, "timesheets", entryId.Value);
        var recorded = new TimeEntryRecorded(entryId,
            TimeEntryTargetReference.ForProject(Project()), Contributor(), ActivityId(),
            ActivityTypeScope.Tenant, new DateOnly(2026, 6, 19), 75,
            BillableState.Billable, TimeEntryApprovalState.Draft,
            ContributorCategory.ExternalContributor, null);
        var source = new ProjectionEventDto(recorded.GetType().FullName!,
            JsonSerializer.SerializeToUtf8Bytes(recorded, JsonOptions), "json", 1, ObservedAtUtc,
            "coordinator-retry-correlation", "coordinator-retry-message", Operator().PartyId, 1);
        string key = TimeEntryEvidenceReadModelAddress.StateKey(owner.TenantId, entryId);
        ProjectionDispatchRequest header;
        string appId;
        string serviceVersion;
        string fingerprint;

        using (MagicLinkHttpBoundaryFactory firstFactory = new(batchStore: store))
        using (HttpClient firstHost = firstFactory.CreateClient())
        using (IServiceScope firstScope = firstFactory.Services.CreateScope())
        {
            IServiceProvider services = firstScope.ServiceProvider;
            DomainProjectionIdentityOptions identity = services
                .GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
            appId = identity.AppId;
            serviceVersion = identity.ServiceVersion;
            ProjectionDispatchRoute[] routes = [.. services.GetServices<IAsyncDomainProjectionHandler>()
                .Where(static handler => handler.Domain == "timesheets")
                .Select(static handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))];
            fingerprint = ProjectionRouteCatalogFingerprint.Compute(appId, serviceVersion, routes);
            NamedProjectionRouteCatalog catalog = CoordinatorCatalog(appId, serviceVersion, fingerprint,
                owner.Domain);
            var forwarder = new FailFirstCommitForwarder(firstFactory, firstHost);
            using var forwarded = new HttpClient(forwarder);
            IHttpClientFactory clients = Substitute.For<IHttpClientFactory>();
            clients.CreateClient(Arg.Any<string>()).Returns(forwarded);
            NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(catalog, clients);
            var registration = new DomainServiceRegistration(appId, "process", owner.TenantId,
                owner.Domain, serviceVersion);
            async Task<string?> WriteSource(Stream output, CancellationToken token)
            {
                await JsonSerializer.SerializeAsync(output, new[] { source }, JsonOptions, token);
                return null;
            }

            ProjectionMappedReplayStageResult staged = await coordinator.StageMappedOnlyStreamedRebuildAsync(
                owner, registration, "coordinator-retry", 1, WriteSource,
                TestContext.Current.CancellationToken);
            staged.Result.Succeeded.ShouldBeTrue();
            header = staged.Header.ShouldNotBeNull();
            NamedProjectionRebuildResult first = await coordinator.InvokeMappedOnlyStreamedRebuildAsync(
                owner, registration, header, ProjectionMappedReplayAction.Commit,
                TestContext.Current.CancellationToken);
            first.Succeeded.ShouldBeFalse();
            forwarder.FailedCommits.ShouldBe(1);
            (await store.GetAsync<TimeEntryEvidenceReadModel>(
                TimeEntryEvidenceReadModelAddress.StateStoreName, key,
                TestContext.Current.CancellationToken)).Value.ShouldBeNull();
        }

        using (MagicLinkHttpBoundaryFactory restartedFactory = new(batchStore: store))
        using (HttpClient restartedHost = restartedFactory.CreateClient())
        using (IServiceScope restartedScope = restartedFactory.Services.CreateScope())
        {
            DomainProjectionIdentityOptions restartedIdentity = restartedScope.ServiceProvider
                .GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
            restartedIdentity.AppId.ShouldBe(appId);
            restartedIdentity.ServiceVersion.ShouldBe(serviceVersion);
            using var forwarded = new HttpClient(new CoordinatorHostForwarder(restartedFactory, restartedHost));
            IHttpClientFactory clients = Substitute.For<IHttpClientFactory>();
            clients.CreateClient(Arg.Any<string>()).Returns(forwarded);
            NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
                CoordinatorCatalog(appId, serviceVersion, fingerprint, owner.Domain), clients);
            var registration = new DomainServiceRegistration(appId, "process", owner.TenantId,
                owner.Domain, serviceVersion);
            NamedProjectionRebuildResult retried = await coordinator.InvokeMappedOnlyStreamedRebuildAsync(
                owner, registration, header, ProjectionMappedReplayAction.Commit,
                TestContext.Current.CancellationToken);
            retried.Succeeded.ShouldBeTrue();
        }

        TimeEntryEvidenceReadModel selected = (await store.GetAsync<TimeEntryEvidenceReadModel>(
            TimeEntryEvidenceReadModelAddress.StateStoreName, key,
            TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        selected.TimeEntryId.ShouldBe(entryId);
        selected.Target.ShouldBe(recorded.Target);
        selected.Contributor.ShouldBe(recorded.Contributor);
        selected.DurationMinutes.ShouldBe(75);
        selected.ApprovalState.ShouldBe(TimeEntryApprovalState.Draft);
        selected.EventLineage.ShouldBe([new TimeEntryEventLineageItem("TimeEntryRecorded", 1,
            TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents)]);
        selected.EventPayloadLineage.ShouldBe([
            Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(source)))
        ]);
    }

    private static NamedProjectionRouteCatalog CoordinatorCatalog(string appId,
        string serviceVersion, string fingerprint, string domain)
    {
        var catalog = new NamedProjectionRouteCatalog();
        catalog.Replace(new NamedProjectionRouteCatalogSnapshot([
            new NamedProjectionRouteCatalogEntry(appId, serviceVersion, domain,
                ProjectionDispatchProtocol.Version, ProjectionDispatchProtocol.Capability,
                fingerprint,
                [TimeEntryEvidenceProjection.ProjectionName,
                    MagicLinkConfirmationCapabilityProjection.ProjectionName,
                    MagicLinkTokenHashCapabilityIndexProjection.ProjectionName],
                [TimeEntryEvidenceProjection.ProjectionName],
                [TimeEntryEvidenceProjection.ProjectionName],
                [MagicLinkConfirmationCapabilityProjection.ProjectionName],
                [MagicLinkTokenHashCapabilityIndexProjection.ProjectionName])
        ]));
        return catalog;
    }

    private static NamedProjectionDispatchCoordinator CreateCoordinator(
        NamedProjectionRouteCatalog catalog, IHttpClientFactory clients)
    {
        var lifecycle = Substitute.For<IProjectionLifecycleGateway>();
        lifecycle.BeginRebuildAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        return new NamedProjectionDispatchCoordinator(catalog,
            Substitute.For<IProjectionDeliveryCheckpointStore>(), lifecycle,
            Substitute.For<IActorProxyFactory>(), new DaprClientBuilder().Build(), clients,
            Options.Create(new ProjectionDispatchOptions()),
            NullLogger<NamedProjectionDispatchCoordinator>.Instance);
    }

    [Fact]
    public async Task CoordinatorProducedStreamedStageCommitsThroughComposedHost()
    {
        var store = new InMemoryReadModelStore();
        using MagicLinkHttpBoundaryFactory factory = new(batchStore: store);
        using HttpClient host = factory.CreateClient();
        using IServiceScope serviceScope = factory.Services.CreateScope();
        IServiceProvider services = serviceScope.ServiceProvider;
        DomainProjectionIdentityOptions domainIdentity = services
            .GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
        ProjectionDispatchRoute[] routes = [.. services.GetServices<IAsyncDomainProjectionHandler>()
            .Where(static handler => handler.Domain == "timesheets")
            .Select(static handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))];
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(
            domainIdentity.AppId, domainIdentity.ServiceVersion, routes);
        var entryId = new TimeEntryId("coordinator-stage-entry");
        var owner = new AggregateIdentity(Tenant().TenantId, "timesheets", entryId.Value);
        var recorded = new TimeEntryRecorded(
            entryId, TimeEntryTargetReference.ForProject(Project()), Contributor(), ActivityId(),
            ActivityTypeScope.Tenant, new DateOnly(2026, 6, 19), 60, BillableState.Billable,
            TimeEntryApprovalState.Draft, ContributorCategory.ExternalContributor, null);
        var source = new ProjectionEventDto(recorded.GetType().FullName!,
            JsonSerializer.SerializeToUtf8Bytes(recorded, JsonOptions), "json", 1, ObservedAtUtc,
            "coordinator-stage-correlation", "coordinator-stage-message", Operator().PartyId, 1);
        var catalog = new NamedProjectionRouteCatalog();
        catalog.Replace(new NamedProjectionRouteCatalogSnapshot([
            new NamedProjectionRouteCatalogEntry(domainIdentity.AppId, domainIdentity.ServiceVersion,
                owner.Domain, ProjectionDispatchProtocol.Version, ProjectionDispatchProtocol.Capability,
                fingerprint,
                [TimeEntryEvidenceProjection.ProjectionName,
                    MagicLinkConfirmationCapabilityProjection.ProjectionName,
                    MagicLinkTokenHashCapabilityIndexProjection.ProjectionName],
                [TimeEntryEvidenceProjection.ProjectionName],
                [TimeEntryEvidenceProjection.ProjectionName],
                [MagicLinkConfirmationCapabilityProjection.ProjectionName],
                [MagicLinkTokenHashCapabilityIndexProjection.ProjectionName])
        ]));
        var clientFactory = Substitute.For<IHttpClientFactory>();
        using var forwardingClient = new HttpClient(new CoordinatorHostForwarder(factory, host));
        clientFactory.CreateClient(Arg.Any<string>()).Returns(forwardingClient);
        var lifecycle = Substitute.For<IProjectionLifecycleGateway>();
        lifecycle.BeginRebuildAsync(owner, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(true);
        var coordinator = new NamedProjectionDispatchCoordinator(catalog,
            Substitute.For<IProjectionDeliveryCheckpointStore>(), lifecycle,
            Substitute.For<IActorProxyFactory>(), new DaprClientBuilder().Build(), clientFactory,
            Options.Create(new ProjectionDispatchOptions()),
            NullLogger<NamedProjectionDispatchCoordinator>.Instance);
        var registration = new DomainServiceRegistration(domainIdentity.AppId, "process",
            owner.TenantId, owner.Domain, domainIdentity.ServiceVersion);
        async Task<string?> WriteSource(Stream output, CancellationToken token)
        {
            await JsonSerializer.SerializeAsync(output, new[] { source }, JsonOptions, token);
            return null;
        }

        (await coordinator.SupportsMappedOnlyStreamedRebuildAsync(owner, registration,
            TimeEntryEvidenceProjection.ProjectionName, TestContext.Current.CancellationToken)).ShouldBeTrue();
        ProjectionMappedReplayStageResult staged = await coordinator.StageMappedOnlyStreamedRebuildAsync(
            owner, registration, "coordinator-stage", 1, WriteSource,
            TestContext.Current.CancellationToken);
        staged.Result.Succeeded.ShouldBeTrue();
        ProjectionDispatchRequest header = staged.Header.ShouldNotBeNull();
        header.RebuildTransport.ShouldNotBeNull().PartCount.ShouldBe(1);
        header.Request.Events.ShouldBeEmpty();
        var key = TimeEntryEvidenceReadModelAddress.StateKey(owner.TenantId, entryId);
        (await store.GetAsync<TimeEntryEvidenceReadModel>(TimeEntryEvidenceReadModelAddress.StateStoreName,
            key, TestContext.Current.CancellationToken)).Value.ShouldBeNull();

        NamedProjectionRebuildResult committed = await coordinator.InvokeMappedOnlyStreamedRebuildAsync(
            owner, registration, header, ProjectionMappedReplayAction.Commit,
            TestContext.Current.CancellationToken);
        committed.Succeeded.ShouldBeTrue();
        TimeEntryEvidenceReadModel selected = (await store.GetAsync<TimeEntryEvidenceReadModel>(
            TimeEntryEvidenceReadModelAddress.StateStoreName, key, TestContext.Current.CancellationToken))
            .Value.ShouldNotBeNull();
        selected.TimeEntryId.ShouldBe(entryId);
        selected.EventLineage.ShouldHaveSingleItem().EventName.ShouldBe("TimeEntryRecorded");

        var capabilityId = new MagicLinkCapabilityId("coordinator-stage-capability");
        var issued = new MagicLinkConfirmationCapabilityIssued(capabilityId, Tenant(), Contributor(),
            TimeEntryTargetReference.ForProject(Project()), ActivityId(), entryId,
            MagicLinkTargetKind.ProposedTimeEntry, MagicLinkAllowedAction.Confirm,
            new MagicLinkTokenHash("coordinator-stage-private-hash"), ObservedAtUtc.AddDays(1),
            Operator(), ObservedAtUtc.AddHours(-1),
            new MagicLinkAuditMetadata("timesheets", "coordinator-stage"), true);
        var issueEvent = new ProjectionEventDto(issued.GetType().FullName!,
            JsonSerializer.SerializeToUtf8Bytes(issued, JsonOptions), "json", 1, ObservedAtUtc,
            "coordinator-stage-issue-correlation", "coordinator-stage-issue-message",
            Operator().PartyId, 2);
        var issueRequest = new ProjectionDispatchRequest(
            new ProjectionRequest(owner.TenantId, owner.Domain, capabilityId.Value, [issueEvent]),
            [MagicLinkConfirmationCapabilityProjection.ProjectionName], "coordinator-stage-issue",
            fingerprint);
        using HttpResponseMessage issueResponse = await factory.PostProjectionAsync(host,
            "/project/v2", issueRequest);
        issueResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await issueResponse.Content.ReadFromJsonAsync<ProjectionDispatchResponse>(JsonOptions,
            TestContext.Current.CancellationToken)).ShouldNotBeNull().Outcomes.ShouldHaveSingleItem()
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        MagicLinkConfirmationCapabilityProjectionHandler capability = services
            .GetServices<IAsyncDomainProjectionHandler>()
            .OfType<MagicLinkConfirmationCapabilityProjectionHandler>().Single();
        SharedProjectionEpochCoordinator epoch = services.GetRequiredService<SharedProjectionEpochCoordinator>();
        SharedProjectionReadResult<MagicLinkConfirmationCapabilityReadModel> selectedCapability =
            await capability.ReadObservedAsync(owner.TenantId, capabilityId, ObservedAtUtc, epoch,
                TestContext.Current.CancellationToken);
        selectedCapability.IsAvailable.ShouldBeTrue();
        selectedCapability.Value.ShouldNotBeNull().CapabilityId.ShouldBe(capabilityId);
        selectedCapability.Value.TimeEntryId.ShouldBe(entryId);
        selectedCapability.Value.State.ShouldBe(
            Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Issued);
    }

    private sealed class CoordinatorHostForwarder(MagicLinkHttpBoundaryFactory factory,
        HttpClient host) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
            => factory.ForwardCoordinatorProjectionAsync(host, request, cancellationToken);
    }
}
