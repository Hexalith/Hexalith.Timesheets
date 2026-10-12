using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed partial class MagicLinkConfirmationHttpBoundaryTests
{
    [Fact]
    public async Task GlobalIndexNormalRouteParksMalformedIssuanceAndContinues()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        using IServiceScope scope = factory.Services.CreateScope();
        DomainProjectionIdentityOptions identity = scope.ServiceProvider
            .GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
        ProjectionDispatchRoute[] routes = [.. scope.ServiceProvider.GetServices<IAsyncDomainProjectionHandler>()
            .Where(static handler => handler.Domain == "timesheets")
            .Select(static handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))];
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(identity.AppId,
            identity.ServiceVersion, routes);

        async Task<ProjectionDispatchStatus> Deliver(string owner, ProjectionEventDto item, string id)
        {
            var request = new ProjectionDispatchRequest(
                new ProjectionRequest(Tenant().TenantId, "timesheets", owner, [item]),
                [MagicLinkTokenHashCapabilityIndexProjection.ProjectionName], id, fingerprint);
            using HttpResponseMessage response = await factory.PostProjectionAsync(client, "/project/v2", request);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            return (await response.Content.ReadFromJsonAsync<ProjectionDispatchResponse>(
                JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull()
                .Outcomes.ShouldHaveSingleItem().Status;
        }

        var malformed = new ProjectionEventDto(typeof(StoredMagicLinkIssued).FullName!,
            JsonSerializer.SerializeToUtf8Bytes(new { Event = (object?)null }, JsonOptions), "json",
            1, ObservedAtUtc, "index-poison-correlation", "index-poison-message",
            Operator().PartyId, 1);
        (await Deliver("index-poison", malformed, "index-poison-first"))
            .ShouldBe(ProjectionDispatchStatus.Retryable);
        (await Deliver("index-poison", malformed, "index-poison-retry"))
            .ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);

        var issued = new MagicLinkConfirmationCapabilityIssued(
            new MagicLinkCapabilityId("index-after-poison"), Tenant(), Contributor(),
            TimeEntryTargetReference.ForProject(Project()), ActivityId(), new TimeEntryId("index-entry"),
            MagicLinkTargetKind.ProposedTimeEntry, MagicLinkAllowedAction.Confirm,
            new MagicLinkTokenHash("index-after-poison-hash"), ObservedAtUtc.AddDays(1),
            Operator(), ObservedAtUtc.AddHours(-1), new MagicLinkAuditMetadata("timesheets", "index"), true);
        var valid = new ProjectionEventDto(typeof(StoredMagicLinkIssued).FullName!,
            JsonSerializer.SerializeToUtf8Bytes(new StoredMagicLinkIssued(issued), JsonOptions), "json",
            1, ObservedAtUtc, "index-valid-correlation", "index-valid-message",
            Operator().PartyId, 2);
        ProjectionDispatchStatus later = ProjectionDispatchStatus.Retryable;
        for (int attempt = 0; attempt < 3 && later == ProjectionDispatchStatus.Retryable; attempt++)
            later = await Deliver("index-after-poison", valid, "index-valid-" + attempt);
        later.ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        factory.Store.GetSelectedIndex().Entries["index-after-poison-hash"].CapabilityId
            .ShouldBe(new MagicLinkCapabilityId("index-after-poison"));
    }

    [Fact]
    public async Task GlobalIndexNormalDeliverySelectsTenThousandHighEntropyCandidates()
    {
        const int count = 10_000;
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(15);
        using IServiceScope scope = factory.Services.CreateScope();
        DomainProjectionIdentityOptions identity = scope.ServiceProvider
            .GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
        ProjectionDispatchRoute[] routes = [.. scope.ServiceProvider.GetServices<IAsyncDomainProjectionHandler>()
            .Where(static handler => handler.Domain == "timesheets")
            .Select(static handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))];
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(identity.AppId,
            identity.ServiceVersion, routes);
        for (int index = 0; index < count; index++)
        {
            string owner = "normal-capability-" + index.ToString("D5",
                System.Globalization.CultureInfo.InvariantCulture);
            string hash = Convert.ToHexString(SHA256.HashData(BitConverter.GetBytes(index)));
            var issued = new MagicLinkConfirmationCapabilityIssued(
                new MagicLinkCapabilityId(owner), Tenant(), Contributor(),
                TimeEntryTargetReference.ForProject(Project()), ActivityId(),
                new TimeEntryId("normal-capacity-entry"), MagicLinkTargetKind.ProposedTimeEntry,
                MagicLinkAllowedAction.Confirm, new MagicLinkTokenHash(hash),
                ObservedAtUtc.AddDays(1), Operator(), ObservedAtUtc.AddHours(-1),
                new MagicLinkAuditMetadata("timesheets", "normal-capacity"), true);
            var item = new ProjectionEventDto(issued.GetType().FullName!,
                JsonSerializer.SerializeToUtf8Bytes(issued, JsonOptions), "json", 1,
                ObservedAtUtc, "normal-capacity-correlation", "normal-capacity-message-" + owner,
                Operator().PartyId, index + 1);
            var request = new ProjectionDispatchRequest(
                new ProjectionRequest(Tenant().TenantId, "timesheets", owner, [item]),
                [MagicLinkTokenHashCapabilityIndexProjection.ProjectionName],
                "normal-capacity-dispatch-" + owner, fingerprint);
            using HttpResponseMessage response = await factory.PostProjectionAsync(client, "/project/v2", request);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await response.Content.ReadFromJsonAsync<ProjectionDispatchResponse>(JsonOptions,
                TestContext.Current.CancellationToken)).ShouldNotBeNull().Outcomes.ShouldHaveSingleItem()
                .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        }

        MagicLinkTokenHashCapabilityIndexReadModel selected = factory.Store.GetSelectedIndex();
        selected.Entries.Count.ShouldBe(count);
        string sampleHash = Convert.ToHexString(SHA256.HashData(BitConverter.GetBytes(count - 1)));
        selected.Entries[sampleHash].CapabilityId.ShouldBe(
            new MagicLinkCapabilityId("normal-capability-09999"));
    }

    [Fact]
    public async Task NormalDirectoryCandidateRemainsResolvableAfterTenantRebuild()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        using IServiceScope serviceScope = factory.Services.CreateScope();
        IServiceProvider services = serviceScope.ServiceProvider;
        string[] tokens = Enumerable.Range(0, 50_000)
            .Select(index => $"directory-token-{index:D5}")
            .Where(token => MagicLinkTokenHashCapabilityIndexProjection.ShardOrdinal(Hash(token))
                == MagicLinkTokenHashCapabilityIndexProjection.ShardOrdinal(Hash("directory-token-00000")))
            .Take(65).OrderBy(Hash, StringComparer.Ordinal).ToArray();
        tokens.Length.ShouldBe(65);
        var histories = new List<(string Owner, ProjectionEventDto Event)>();
        for (int index = 0; index < tokens.Length; index++)
        {
            string owner = $"directory-capability-{index:D3}";
            var capabilityId = new MagicLinkCapabilityId(owner);
            var entryId = new TimeEntryId($"directory-entry-{index:D3}");
            await factory.ProjectValidStateAsync(client, tokens[index], MagicLinkAllowedAction.Confirm,
                capabilityId, entryId);
            var issued = new MagicLinkConfirmationCapabilityIssued(capabilityId, Tenant(), Contributor(),
                TimeEntryTargetReference.ForProject(Project()), ActivityId(), entryId,
                MagicLinkTargetKind.ProposedTimeEntry, MagicLinkAllowedAction.Confirm,
                new MagicLinkTokenHash(Hash(tokens[index])), ObservedAtUtc.AddDays(1),
                Operator(), ObservedAtUtc.AddHours(-1),
                new MagicLinkAuditMetadata("timesheets", "issue-1"), true);
            histories.Add((owner, new ProjectionEventDto(issued.GetType().FullName!,
                JsonSerializer.SerializeToUtf8Bytes(issued, JsonOptions), "json", 1, ObservedAtUtc,
                "correlation-1", "projection-1", Operator().PartyId, 1)));
        }

        var loader = services.GetRequiredService<IMagicLinkConfirmationCapabilityStateLoader>();
        MagicLinkEndpointTokenState ordinary = await loader.LoadTokenStateAsync(tokens[^1],
            TestContext.Current.CancellationToken);
        ordinary.CapabilityState.ShouldNotBeNull().CapabilityId
            .ShouldBe(new MagicLinkCapabilityId("directory-capability-064"));
        var coordinator = services.GetRequiredService<SharedProjectionEpochCoordinator>();
        SharedProjectionReadResult<MagicLinkTokenHashCapabilityIndexTreeRoot> selected = await coordinator
            .ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(
                MagicLinkTokenHashCapabilityIndexProjection.GlobalScope(),
                MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
                TestContext.Current.CancellationToken);
        selected.Value.ShouldNotBeNull().TokenRoot.ShouldNotBeNull();

        DomainProjectionIdentityOptions domainIdentity = services
            .GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
        ProjectionDispatchRoute[] routes = [.. services.GetServices<IAsyncDomainProjectionHandler>()
            .Where(static handler => handler.Domain == "timesheets")
            .Select(static handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))];
        string catalog = ProjectionRouteCatalogFingerprint.Compute(domainIdentity.AppId,
            domainIdentity.ServiceVersion, routes);
        var identity = new DomainSharedProjectionRebuildIdentity(Tenant().TenantId, "timesheets",
            MagicLinkTokenHashCapabilityIndexProjection.ProjectionName, "directory-rebuild", catalog);
        string inventory = DomainSharedProjectionRebuildFingerprint.EmptyInventory;
        for (int index = 0; index < histories.Count; index++)
            inventory = DomainSharedProjectionRebuildFingerprint.AppendInventory(inventory, index,
                DomainSharedProjectionRebuildFingerprint.ComputeHistory(
                    histories[index].Owner, false, [histories[index].Event]));
        async Task<DomainSharedProjectionRebuildResponse> Rebuild(DomainSharedProjectionRebuildRequest request)
        {
            using HttpResponseMessage response = await factory.PostSharedProjectionAsync(client, request,
                TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            return (await response.Content.ReadFromJsonAsync<DomainSharedProjectionRebuildResponse>(
                JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        }
        DomainSharedProjectionRebuildResponse current = await Rebuild(new(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
            identity, CaptureInventoryFingerprint: inventory,
            SourceHighWatermarks: histories.ToDictionary(static item => item.Owner,
                static _ => 1L, StringComparer.Ordinal)));
        current.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        for (int index = 0; index < histories.Count; index++)
        {
            current = await Rebuild(new(DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildAction.Accumulate, identity,
                AggregateOrdinal: index, AggregateId: histories[index].Owner,
                Events: [histories[index].Event]));
            current.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        }
        current = await Rebuild(new(DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Finalize, identity,
            ExpectedAggregateCount: histories.Count, ExpectedInventoryFingerprint: current.InventoryFingerprint));
        current.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await Rebuild(new(DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Stage, identity))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await Rebuild(new(DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Commit, identity))).Status.ShouldBe(ProjectionDispatchStatus.Completed);

        MagicLinkEndpointTokenState rebuilt = await loader.LoadTokenStateAsync(tokens[^1],
            TestContext.Current.CancellationToken);
        rebuilt.CapabilityState.ShouldNotBeNull().CapabilityId.ShouldBe(ordinary.CapabilityState.CapabilityId);
    }

    [Fact]
    public async Task GlobalIndexTenantRebuildPrunesItsSliceAndRetainsAnotherTenant()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        using IServiceScope scope = factory.Services.CreateScope();
        DomainProjectionIdentityOptions domainIdentity = scope.ServiceProvider
            .GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
        ProjectionDispatchRoute[] routes = [.. scope.ServiceProvider.GetServices<IAsyncDomainProjectionHandler>()
            .Where(static handler => handler.Domain == "timesheets")
            .Select(static handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))];
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(domainIdentity.AppId,
            domainIdentity.ServiceVersion, routes);
        async Task Issue(string tenant, string owner, string hash)
        {
            var issued = new MagicLinkConfirmationCapabilityIssued(
                new MagicLinkCapabilityId(owner), new TenantReference(tenant), Contributor(),
                TimeEntryTargetReference.ForProject(Project()), ActivityId(),
                new TimeEntryId("global-index-prune-entry"), MagicLinkTargetKind.ProposedTimeEntry,
                MagicLinkAllowedAction.Confirm, new MagicLinkTokenHash(hash), ObservedAtUtc.AddDays(1),
                Operator(), ObservedAtUtc.AddHours(-1),
                new MagicLinkAuditMetadata("timesheets", "global-index-prune"), true);
            var projectionEvent = new ProjectionEventDto(issued.GetType().FullName!,
                JsonSerializer.SerializeToUtf8Bytes(issued, JsonOptions), "json", 1, ObservedAtUtc,
                "prune-correlation", "prune-message-" + owner, Operator().PartyId, 1);
            var dispatch = new ProjectionDispatchRequest(
                new ProjectionRequest(tenant, "timesheets", owner, [projectionEvent]),
                [MagicLinkTokenHashCapabilityIndexProjection.ProjectionName], "prune-" + owner,
                fingerprint);
            using HttpResponseMessage response = await factory.PostProjectionAsync(client, "/project/v2", dispatch);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await response.Content.ReadFromJsonAsync<ProjectionDispatchResponse>(JsonOptions,
                TestContext.Current.CancellationToken)).ShouldNotBeNull().Outcomes.ShouldHaveSingleItem()
                .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        }
        await Issue(Tenant().TenantId, "prune-tenant-a", "prune-hash-a");
        await Issue(OtherTenant().TenantId, "prune-tenant-b", "prune-hash-b");
        factory.Store.GetSelectedIndex().Entries.Count.ShouldBe(2);

        var identity = new DomainSharedProjectionRebuildIdentity(Tenant().TenantId, "timesheets",
            MagicLinkTokenHashCapabilityIndexProjection.ProjectionName, "prune-index-a", fingerprint);
        async Task<DomainSharedProjectionRebuildResponse> Rebuild(DomainSharedProjectionRebuildRequest request)
        {
            using HttpResponseMessage response = await factory.PostSharedProjectionAsync(client, request,
                TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            return (await response.Content.ReadFromJsonAsync<DomainSharedProjectionRebuildResponse>(
                JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        }
        DomainSharedProjectionRebuildResponse begun = await Rebuild(new(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
            identity, CaptureInventoryFingerprint: DomainSharedProjectionRebuildFingerprint.EmptyInventory,
            SourceHighWatermarks: new Dictionary<string, long>()));
        begun.Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Accumulating);
        DomainSharedProjectionRebuildResponse finalized = await Rebuild(new(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Finalize,
            identity, ExpectedAggregateCount: 0,
            ExpectedInventoryFingerprint: DomainSharedProjectionRebuildFingerprint.EmptyInventory));
        finalized.Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Finalized);
        (await Rebuild(new(DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Stage, identity))).Phase
            .ShouldBe(DomainSharedProjectionRebuildPhase.Prepared);
        (await Rebuild(new(DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Commit, identity))).Phase
            .ShouldBe(DomainSharedProjectionRebuildPhase.Committed);
        MagicLinkTokenHashCapabilityIndexReadModel selected = factory.Store.GetSelectedIndex();
        selected.Entries.ContainsKey("prune-hash-a").ShouldBeFalse();
        selected.Entries["prune-hash-b"].CapabilityId.ShouldBe(new MagicLinkCapabilityId("prune-tenant-b"));
    }

    [Fact]
    public async Task GlobalIndexAcceptsLaterIssuanceAndSuppressesCrossTenantCollision()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        string token = "global-index-later-issuance";
        string hash = Hash(token);
        var firstId = new MagicLinkCapabilityId("global-index-first");
        var secondId = new MagicLinkCapabilityId("global-index-second");
        using IServiceScope scope = factory.Services.CreateScope();
        DomainProjectionIdentityOptions identity = scope.ServiceProvider
            .GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
        ProjectionDispatchRoute[] routes = [.. scope.ServiceProvider.GetServices<IAsyncDomainProjectionHandler>()
            .Where(static handler => handler.Domain == "timesheets")
            .Select(static handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))];
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(identity.AppId,
            identity.ServiceVersion, routes);
        MagicLinkConfirmationCapabilityIssued Issued(MagicLinkCapabilityId id, TenantReference tenant,
            string? tokenHash = null)
            => new(id, tenant, Contributor(), TimeEntryTargetReference.ForProject(Project()),
                ActivityId(), new TimeEntryId("global-index-entry"), MagicLinkTargetKind.ProposedTimeEntry,
                MagicLinkAllowedAction.Confirm, new MagicLinkTokenHash(tokenHash ?? hash), ObservedAtUtc.AddDays(1),
                Operator(), ObservedAtUtc.AddHours(-1),
                new MagicLinkAuditMetadata("timesheets", "global-index-issue"), true);
        ProjectionEventDto Event(long sequence, object payload)
            => new(payload.GetType().FullName!, JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions),
                "json", sequence, ObservedAtUtc, "global-index-correlation",
                $"global-index-message-{sequence}", Operator().PartyId, sequence);
        async Task<ProjectionDispatchOutcome> Dispatch(string tenant, string owner,
            ProjectionEventDto[] events, string dispatchId)
        {
            var request = new ProjectionDispatchRequest(
                new ProjectionRequest(tenant, "timesheets", owner, events),
                [MagicLinkTokenHashCapabilityIndexProjection.ProjectionName], dispatchId, fingerprint);
            using HttpResponseMessage response = await factory.PostProjectionAsync(client, "/project/v2", request);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            return (await response.Content.ReadFromJsonAsync<ProjectionDispatchResponse>(
                JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull()
                .Outcomes.ShouldHaveSingleItem();
        }

        ProjectionEventDto[] firstEvents = [.. Enumerable.Range(1, 6)
            .Select(sequence => Event(sequence, new { Harmless = sequence }))];
        firstEvents = [.. firstEvents, Event(7, Issued(firstId, Tenant()))];
        ProjectionDispatchOutcome firstOutcome = await Dispatch(Tenant().TenantId, firstId.Value, firstEvents, "global-first");
        firstOutcome.Status.ShouldBe(ProjectionDispatchStatus.Completed, firstOutcome.ReasonCode);
        factory.Store.GetSelectedIndex().Entries[hash].CapabilityId.ShouldBe(firstId);

        (await Dispatch(OtherTenant().TenantId, secondId.Value,
            [Event(1, Issued(secondId, OtherTenant()))], "global-second"))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        MagicLinkTokenHashCapabilityIndexReadModel selected = factory.Store.GetSelectedIndex();
        selected.Entries.ShouldNotContainKey(hash);
        selected.Contributions[hash].Count.ShouldBe(2);

        for (int index = 0; index < 63; index++)
        {
            var id = new MagicLinkCapabilityId($"global-index-collision-{index:D2}");
            (await Dispatch(Tenant().TenantId, id.Value,
                [Event(1, Issued(id, Tenant()))], $"global-collision-{index:D2}"))
                .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        }
        selected = factory.Store.GetSelectedIndex();
        selected.Entries.ShouldNotContainKey(hash);
        selected.Contributions[hash].Count.ShouldBe(65);

        int shard = MagicLinkTokenHashCapabilityIndexProjection.ShardOrdinal(hash);
        string neighborHash = Enumerable.Range(0, 10_000)
            .Select(index => Hash($"same-shard-{index:D5}"))
            .First(candidate => candidate != hash
                && MagicLinkTokenHashCapabilityIndexProjection.ShardOrdinal(candidate) == shard);
        for (int index = 0; index < 65; index++)
        {
            var id = new MagicLinkCapabilityId($"same-shard-neighbor-{index:D2}");
            (await Dispatch(Tenant().TenantId, id.Value,
                [Event(1, Issued(id, Tenant(), neighborHash))], $"same-shard-neighbor-{index:D2}"))
                .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        }
        for (int index = 0; index < 64; index++)
        {
            var id = new MagicLinkCapabilityId($"same-shard-original-{index:D2}");
            (await Dispatch(Tenant().TenantId, id.Value,
                [Event(1, Issued(id, Tenant()))], $"same-shard-original-{index:D2}"))
                .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        }
        selected = factory.Store.GetSelectedIndex();
        selected.Contributions[hash].Count.ShouldBe(129);
        selected.Contributions[neighborHash].Count.ShouldBe(65);

        var earlyOwner = new MagicLinkCapabilityId("global-index-collision-00");
        ProjectionDispatchOutcome replay = await Dispatch(Tenant().TenantId, earlyOwner.Value,
            [Event(2, Issued(earlyOwner, Tenant()))], "global-earlier-page-replay");
        replay.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        replay = await Dispatch(Tenant().TenantId, earlyOwner.Value,
            [Event(2, Issued(earlyOwner, Tenant()))], "global-earlier-page-replay-retry");
        replay.Status.ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        ProjectionDispatchOutcome conflict = await Dispatch(Tenant().TenantId, earlyOwner.Value,
            [Event(3, Issued(earlyOwner, Tenant(), neighborHash))], "global-earlier-page-conflict");
        conflict.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        conflict = await Dispatch(Tenant().TenantId, earlyOwner.Value,
            [Event(3, Issued(earlyOwner, Tenant(), neighborHash))], "global-earlier-page-conflict-retry");
        conflict.Status.ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        selected = factory.Store.GetSelectedIndex();
        selected.Contributions[hash].Count.ShouldBe(129);
        selected.Contributions[neighborHash].Count.ShouldBe(65);
    }
}
