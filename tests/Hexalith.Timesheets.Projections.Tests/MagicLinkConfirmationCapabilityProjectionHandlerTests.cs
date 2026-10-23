using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.Timesheets.Projections.Tests;

public sealed class MagicLinkConfirmationCapabilityProjectionHandlerTests
{
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);
    private static readonly DomainProjectionIdentityOptions s_identity = new() { AppId = "timesheets-test", ServiceVersion = "v1" };
    private static readonly string s_catalog = ProjectionRouteCatalogFingerprint.Compute(
        "timesheets-test", "v1", [new ProjectionDispatchRoute("timesheets", MagicLinkConfirmationCapabilityProjection.ProjectionName)]);

    [Fact]
    public void CaseVariantsOfTenantIdentitySelectTheSameEpochScope()
    {
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(new InMemoryReadModelStore());
        SharedProjectionScope lower = handler.CreateScope("tenant-1");
        SharedProjectionScope mixed = handler.CreateScope("Tenant-1");
        mixed.TenantId.ShouldBe("tenant-1");
        mixed.ComputeHash().ShouldBe(lower.ComputeHash());
    }

    [Fact]
    public async Task SelectedIssuanceCannotClaimUsedWithoutMatchingTerminalFact()
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        (await Deliver(provider, "cap-1", Event(1, 1, new StoredMagicLinkIssued(Issued("cap-1")))))
            .ShouldBe(ProjectionDispatchStatus.Completed);
        MagicLinkCapabilityProjectionView original = (await Read(coordinator, handler, "cap-1")).ShouldNotBeNull();
        MagicLinkCapabilityProjectionView invalid = original with
        {
            Issuance = original.Issuance.ShouldNotBeNull() with
            {
                State = Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Used
            }
        };
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        MagicLinkCapabilityId id = new("cap-1");
        string key = Physical(scope, MagicLinkCapabilityReadModelAddress.StateKey("tenant-1", id));
        ReadModelEntry<MagicLinkCapabilityProjectionStorage> selected = await store
            .GetAsync<MagicLinkCapabilityProjectionStorage>(scope.StoreName, key, TestContext.Current.CancellationToken);
        (await store.TrySaveAsync(scope.StoreName, key,
            new MagicLinkCapabilityProjectionStorage(invalid, null, 0, 0), selected.ETag!,
            TestContext.Current.CancellationToken)).ShouldBeTrue();

        _ = await Should.ThrowAsync<InvalidOperationException>(() => handler.ReadViewAsync(
            "tenant-1", id, coordinator, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SelectedTerminalCursorWithoutTerminalFactIsRejected()
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        (await Deliver(provider, "cap-1", Event(1, 1, new StoredMagicLinkIssued(Issued("cap-1")))))
            .ShouldBe(ProjectionDispatchStatus.Completed);
        MagicLinkCapabilityProjectionView original = (await Read(coordinator, handler, "cap-1")).ShouldNotBeNull();
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        MagicLinkCapabilityId id = new("cap-1");
        string key = Physical(scope, MagicLinkCapabilityReadModelAddress.StateKey("tenant-1", id));
        ReadModelEntry<MagicLinkCapabilityProjectionStorage> selected = await store
            .GetAsync<MagicLinkCapabilityProjectionStorage>(scope.StoreName, key, TestContext.Current.CancellationToken);
        (await store.TrySaveAsync(scope.StoreName, key,
            new MagicLinkCapabilityProjectionStorage(original with { TerminalPosition = 2 }, null, 0, 0),
            selected.ETag!, TestContext.Current.CancellationToken)).ShouldBeTrue();

        _ = await Should.ThrowAsync<InvalidOperationException>(() => handler.ReadViewAsync(
            "tenant-1", id, coordinator, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("action")]
    [InlineData("expiry")]
    [InlineData("badge")]
    [InlineData("terminal-residue")]
    public async Task SelectedIssuanceWithCorruptFieldIsRejectedBeforeFreshReadback(string corruption)
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        (await Deliver(provider, "cap-1", Event(1, 1, new StoredMagicLinkIssued(Issued("cap-1")))))
            .ShouldBe(ProjectionDispatchStatus.Completed);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        MagicLinkCapabilityId id = new("cap-1");
        string key = Physical(scope, MagicLinkCapabilityReadModelAddress.StateKey("tenant-1", id));
        ReadModelEntry<MagicLinkCapabilityProjectionStorage> selected = await store
            .GetAsync<MagicLinkCapabilityProjectionStorage>(scope.StoreName, key, TestContext.Current.CancellationToken);
        MagicLinkCapabilityProjectionView original = selected.Value.ShouldNotBeNull().Inline.ShouldNotBeNull();
        MagicLinkConfirmationCapabilityReadModel issue = original.Issuance.ShouldNotBeNull();
        MagicLinkConfirmationCapabilityReadModel changed = corruption switch
        {
            "action" => issue with { AllowedAction = (MagicLinkAllowedAction)999 },
            "expiry" => issue with { ExpiryState = MagicLinkExpiryState.Unknown },
            "badge" => issue with { StateBadgeText = "Used" },
            "terminal-residue" => issue with { RevokedBy = issue.Issuer },
            _ => throw new InvalidOperationException("Unknown corruption fixture.")
        };
        (await store.TrySaveAsync(scope.StoreName, key,
            new MagicLinkCapabilityProjectionStorage(original with
            {
                Issuance = changed,
                Published = changed
            }, null, 0, 0), selected.ETag!, TestContext.Current.CancellationToken)).ShouldBeTrue();

        _ = await Should.ThrowAsync<InvalidOperationException>(() => handler.ReadViewAsync(
            "tenant-1", id, coordinator, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CorruptLegacyTerminalFlagDoesNotParkLaterValidIssuance()
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        ProjectionEventDto terminal = Event(1, 2, new StoredMagicLinkUsed(Used("cap-1")));
        ProjectionEventDto issued = Event(1, 1, new StoredMagicLinkIssued(Issued("cap-1")));
        (await Deliver(provider, "entry-1", terminal)).ShouldBe(ProjectionDispatchStatus.Completed);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        MagicLinkCapabilityId id = new("cap-1");
        string key = Physical(scope, MagicLinkCapabilityReadModelAddress.StateKey("tenant-1", id));
        ReadModelEntry<MagicLinkCapabilityProjectionStorage> selected = await store
            .GetAsync<MagicLinkCapabilityProjectionStorage>(scope.StoreName, key, TestContext.Current.CancellationToken);
        MagicLinkCapabilityProjectionView pending = selected.Value.ShouldNotBeNull().Inline.ShouldNotBeNull();
        (await store.TrySaveAsync(scope.StoreName, key,
            new MagicLinkCapabilityProjectionStorage(pending with { TerminalInCapabilityStream = true }, null, 0, 0),
            selected.ETag!, TestContext.Current.CancellationToken)).ShouldBeTrue();

        (await Deliver(provider, "cap-1", issued)).ShouldBe(ProjectionDispatchStatus.Retryable);
        await Rebuild(provider, "repair-legacy-flag", [("cap-1", issued), ("entry-1", terminal)]);
        (await Read(coordinator, handler, "cap-1")).ShouldNotBeNull().Published.ShouldNotBeNull()
            .State.ShouldBe(Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Used);
    }

    [Theory]
    [InlineData(18)]
    [InlineData(21)]
    public async Task UseOutsideIssuanceWindowIsParkedWithoutAUsedRow(int day)
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        (await Deliver(provider, "cap-1", Event(1, 1, new StoredMagicLinkIssued(Issued("cap-1")))))
            .ShouldBe(ProjectionDispatchStatus.Completed);
        DateTimeOffset usedAt = day == 21
            ? Issued("cap-1").ExpiresAtUtc
            : new DateTimeOffset(2026, 6, day, 15, 0, 0, TimeSpan.Zero);
        ProjectionEventDto invalid = Event(1, 2, new StoredMagicLinkUsed(Used("cap-1") with { UsedAtUtc = usedAt }));

        (await Deliver(provider, "entry-1", invalid)).ShouldBe(ProjectionDispatchStatus.Retryable);
        (await Deliver(provider, "entry-1", invalid)).ShouldBeOneOf(
            ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        (await Read(coordinator, handler, "cap-1")).ShouldNotBeNull().Published.ShouldNotBeNull()
            .State.ShouldBe(Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Issued);
    }

    [Theory]
    [InlineData(MagicLinkAllowedAction.Confirm, "adjusted")]
    [InlineData(MagicLinkAllowedAction.Adjust, "confirmed")]
    [InlineData(MagicLinkAllowedAction.ConfirmOrAdjust, "unexpected")]
    public async Task UseOutcomeMustMatchIssuedActionOrItIsParked(
        MagicLinkAllowedAction action, string outcome)
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        (await Deliver(provider, "cap-1", Event(1, 1,
            new StoredMagicLinkIssued(Issued("cap-1") with { AllowedAction = action }))))
            .ShouldBe(ProjectionDispatchStatus.Completed);
        ProjectionEventDto invalid = Event(1, 2,
            new StoredMagicLinkUsed(Used("cap-1") with { OutcomeCategory = outcome }));

        (await Deliver(provider, "entry-1", invalid)).ShouldBe(ProjectionDispatchStatus.Retryable);
        (await Deliver(provider, "entry-1", invalid)).ShouldBeOneOf(
            ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        (await Read(coordinator, handler, "cap-1")).ShouldNotBeNull().Published.ShouldNotBeNull()
            .State.ShouldBe(Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Issued);
    }

    [Fact]
    public async Task ExpiryBeforeIssuedDeadlineIsParkedWithoutAnExpiredRow()
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        (await Deliver(provider, "cap-1", Event(1, 1, new StoredMagicLinkIssued(Issued("cap-1")))))
            .ShouldBe(ProjectionDispatchStatus.Completed);
        ProjectionEventDto invalid = Event(1, 2, new StoredMagicLinkExpired(
            new MagicLinkConfirmationCapabilityExpired(new MagicLinkCapabilityId("cap-1"),
                new TenantReference("tenant-1"), Issued("cap-1").ExpiresAtUtc.AddTicks(-1),
                new MagicLinkAuditMetadata("timesheets", "early-expiry"))
            { TimeEntryId = new TimeEntryId("entry-1") }));

        (await Deliver(provider, "entry-1", invalid)).ShouldBe(ProjectionDispatchStatus.Retryable);
        (await Deliver(provider, "entry-1", invalid)).ShouldBeOneOf(
            ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        (await Read(coordinator, handler, "cap-1")).ShouldNotBeNull().Published.ShouldNotBeNull()
            .State.ShouldBe(Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Issued);
    }

    [Fact]
    public async Task NormalEpochDeliveryRetainsTwoCapabilitiesAndJoinsTerminalFirstWithoutTokenData()
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);

        (await Deliver(provider, "entry-1", Event(1, 2, new StoredMagicLinkUsed(Used("cap-1"))))).ShouldBe(ProjectionDispatchStatus.Completed);
        MagicLinkCapabilityProjectionView pending = (await Read(coordinator, handler, "cap-1")).ShouldNotBeNull();
        pending.Issuance.ShouldBeNull();
        pending.Published.ShouldBeNull();

        (await Deliver(provider, "cap-1", Event(1, 1, new StoredMagicLinkIssued(Issued("cap-1"))))).ShouldBe(ProjectionDispatchStatus.Completed);
        (await Deliver(provider, "cap-2", Event(1, 3, new StoredMagicLinkIssued(Issued("cap-2"))))).ShouldBe(ProjectionDispatchStatus.Completed);
        MagicLinkCapabilityProjectionView first = (await Read(coordinator, handler, "cap-1")).ShouldNotBeNull();
        first.Published.ShouldNotBeNull().State.ShouldBe(Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Used);
        (await Read(coordinator, handler, "cap-2")).ShouldNotBeNull().Published.ShouldNotBeNull();
        string json = JsonSerializer.Serialize(first, s_json);
        json.ShouldNotContain("hash-cap-1");
        json.ShouldNotContain("tokenHash", Case.Insensitive);
        SharedProjectionReadResult<MagicLinkConfirmationCapabilityReadModel> observed = await handler.ReadObservedAsync(
            "tenant-1", new MagicLinkCapabilityId("cap-1"),
            new DateTimeOffset(2026, 6, 22, 0, 0, 0, TimeSpan.Zero), coordinator,
            TestContext.Current.CancellationToken);
        observed.Value.ShouldNotBeNull().ExpiryState.ShouldBe(MagicLinkExpiryState.Expired);
        observed.Value.ProjectionFreshness.AsOfUtc.ShouldBe(new DateTimeOffset(2026, 6, 22, 0, 0, 0, TimeSpan.Zero));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NormalEpochDeliveryPublishesRevocationOrExpiry(bool expiry)
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        (await Deliver(provider, "cap-1", Event(1, 1, new StoredMagicLinkIssued(Issued("cap-1")))))
            .ShouldBe(ProjectionDispatchStatus.Completed);

        object terminal = expiry
            ? new StoredMagicLinkExpired(new MagicLinkConfirmationCapabilityExpired(
                new MagicLinkCapabilityId("cap-1"), new TenantReference("tenant-1"),
                new DateTimeOffset(2026, 6, 21, 12, 0, 0, TimeSpan.Zero),
                new MagicLinkAuditMetadata("timesheets", "expiry-cap-1"))
                { TimeEntryId = new TimeEntryId("entry-1") })
            : new StoredMagicLinkRevoked(new MagicLinkConfirmationCapabilityRevoked(
                new MagicLinkCapabilityId("cap-1"), new TenantReference("tenant-1"),
                new PartyReference("issuer-1"),
                new DateTimeOffset(2026, 6, 19, 15, 0, 0, TimeSpan.Zero),
                new MagicLinkAuditMetadata("timesheets", "revoke-cap-1"))
                { TimeEntryId = new TimeEntryId("entry-1") });
        (await Deliver(provider, "entry-1", Event(1, 2, terminal))).ShouldBe(ProjectionDispatchStatus.Completed);

        MagicLinkConfirmationCapabilityReadModel published = (await Read(coordinator, handler, "cap-1"))
            .ShouldNotBeNull().Published.ShouldNotBeNull();
        published.State.ShouldBe(expiry
            ? Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Expired
            : Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Revoked);
        if (expiry)
        {
            published.ExpiredAtUtc.ShouldNotBeNull();
            SharedProjectionReadResult<MagicLinkConfirmationCapabilityReadModel> observed = await handler.ReadObservedAsync(
                "tenant-1", new MagicLinkCapabilityId("cap-1"),
                new DateTimeOffset(2026, 6, 19, 15, 30, 0, TimeSpan.Zero), coordinator,
                TestContext.Current.CancellationToken);
            observed.Value.ShouldNotBeNull().ExpiryState.ShouldBe(MagicLinkExpiryState.Expired);
            observed.Value.ExpiryBadgeText.ShouldBe("Expired");
        }
        else published.RevokedAtUtc.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("used")]
    [InlineData("revoked")]
    [InlineData("expired")]
    public async Task DefaultTerminalTimestampIsParkedWithoutPublishing(string kind)
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        object terminal = kind switch
        {
            "used" => new StoredMagicLinkUsed(Used("cap-1") with { UsedAtUtc = default }),
            "revoked" => new StoredMagicLinkRevoked(new MagicLinkConfirmationCapabilityRevoked(
                new MagicLinkCapabilityId("cap-1"), new TenantReference("tenant-1"),
                new PartyReference("issuer-1"), default,
                new MagicLinkAuditMetadata("timesheets", "revoke-cap-1"))
                { TimeEntryId = new TimeEntryId("entry-1") }),
            "expired" => new StoredMagicLinkExpired(new MagicLinkConfirmationCapabilityExpired(
                new MagicLinkCapabilityId("cap-1"), new TenantReference("tenant-1"), default,
                new MagicLinkAuditMetadata("timesheets", "expiry-cap-1"))
                { TimeEntryId = new TimeEntryId("entry-1") }),
            _ => throw new InvalidOperationException("Unknown terminal case.")
        };
        ProjectionEventDto malformed = Event(1, 1, terminal);
        (await Deliver(provider, "entry-1", malformed)).ShouldBe(ProjectionDispatchStatus.Retryable);
        (await Deliver(provider, "entry-1", malformed)).ShouldBeOneOf(
            ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        (await Read(coordinator, handler, "cap-1")).ShouldBeNull();
    }

    [Fact]
    public async Task NormalEpochDeliveryParksMalformedHeadThenProcessesLaterValidEvent()
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        ProjectionEventDto malformed = Event(1, 1, new StoredMagicLinkIssued(Issued("cap-1"))) with
        {
            Payload = JsonSerializer.SerializeToUtf8Bytes(new { @event = (object?)null })
        };
        ProjectionDispatchStatus first = await Deliver(provider, "cap-1", malformed);
        first.ShouldBe(ProjectionDispatchStatus.Retryable);
        ProjectionDispatchStatus parked = await Deliver(provider, "cap-1", malformed);
        parked.ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        (await Deliver(provider, "cap-2", Event(1, 2, new StoredMagicLinkIssued(Issued("cap-2")))))
            .ShouldBe(ProjectionDispatchStatus.Completed);
        (await Read(coordinator, handler, "cap-2")).ShouldNotBeNull().Published.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("target-kind")]
    [InlineData("allowed-action")]
    [InlineData("contributor")]
    [InlineData("target-id")]
    [InlineData("activity-type")]
    [InlineData("time-entry")]
    [InlineData("issuer")]
    [InlineData("source-system")]
    [InlineData("source-reference")]
    public async Task NormalEpochDeliveryParksIncompleteOrUnknownIssuance(string field)
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        ProjectionEventDto valid = Event(1, 1, new StoredMagicLinkIssued(Issued("cap-1")));
        JsonNode payload = JsonNode.Parse(valid.Payload)!.AsObject();
        JsonNode issue = payload["event"]!;
        switch (field)
        {
            case "target-kind": issue["targetKind"] = "Unknown"; break;
            case "allowed-action": issue["allowedAction"] = "Unknown"; break;
            case "contributor": issue["contributor"]!["partyId"] = " "; break;
            case "target-id": issue["target"]!["targetId"] = " "; break;
            case "activity-type": issue["activityTypeId"]!["value"] = " "; break;
            case "time-entry": issue["timeEntryId"]!["value"] = " "; break;
            case "issuer": issue["issuer"]!["partyId"] = " "; break;
            case "source-system": issue["source"]!["sourceSystem"] = " "; break;
            case "source-reference": issue["source"]!["sourceReference"] = " "; break;
            default: throw new InvalidOperationException("Unknown test case.");
        }

        ProjectionEventDto malformed = valid with { Payload = JsonSerializer.SerializeToUtf8Bytes(payload, s_json) };
        (await Deliver(provider, "cap-1", malformed)).ShouldBe(ProjectionDispatchStatus.Retryable);
        ProjectionDispatchStatus parked = await Deliver(provider, "cap-1", malformed);
        parked.ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        string key = MagicLinkCapabilityReadModelAddress.StateKey("tenant-1", new MagicLinkCapabilityId("cap-1"));
        (await coordinator.ReadGenerationAsync<MagicLinkCapabilityProjectionStorage>(
            handler.CreateScope("tenant-1"), 0, key, TestContext.Current.CancellationToken)).Value.ShouldBeNull();
    }

    [Fact]
    public async Task SelectedGenerationWriteOutageIsRetryableAndPreservesPreviousCompleteView()
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        (await Deliver(provider, "cap-1", Event(1, 1, new StoredMagicLinkIssued(Issued("cap-1")))))
            .ShouldBe(ProjectionDispatchStatus.Completed);
        bool failNextWrite = true;
        store.BatchFaultHook = (phase, _, _) =>
        {
            if (failNextWrite && phase == ReadModelBatchPhase.BeforeDispatch)
            {
                failNextWrite = false;
                throw new HttpRequestException("Injected batch write outage.");
            }

            return Task.CompletedTask;
        };

        ProjectionEventDto used = Event(1, 2, new StoredMagicLinkUsed(Used("cap-1")));
        (await Deliver(provider, "entry-1", used)).ShouldBe(ProjectionDispatchStatus.Retryable);
        string key = MagicLinkCapabilityReadModelAddress.StateKey("tenant-1", new MagicLinkCapabilityId("cap-1"));
        MagicLinkCapabilityProjectionView prior = (await coordinator.ReadGenerationAsync<MagicLinkCapabilityProjectionStorage>(
            handler.CreateScope("tenant-1"), 0, key, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull()
            .Inline.ShouldNotBeNull();
        prior.Published.ShouldNotBeNull().State.ShouldBe(Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Issued);
        store.BatchFaultHook = null;
        (await Deliver(provider, "entry-1", used))
            .ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        (await Read(coordinator, handler, "cap-1")).ShouldNotBeNull().Published.ShouldNotBeNull()
            .State.ShouldBe(Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Used);
    }

    [Fact]
    public async Task LargeValidIssuanceCrossesLiveJournalLimitAndPublishesCompleteView()
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        string target = new('p', 40_000);
        MagicLinkConfirmationCapabilityIssued issued = Issued("cap-large") with
        {
            Target = TimeEntryTargetReference.ForProject(new ProjectReference(target))
        };
        ProjectionEventDto item = Event(1, 1, new StoredMagicLinkIssued(issued));
        (await Deliver(provider, "cap-large", item)).ShouldBe(ProjectionDispatchStatus.Completed);
        MagicLinkCapabilityProjectionView selected = (await Read(coordinator, handler, "cap-large")).ShouldNotBeNull();
        selected.Issuance.ShouldNotBeNull().Target.TargetId.ShouldBe(target);
        selected.Published.ShouldNotBeNull().Target.TargetId.ShouldBe(target);
        JsonSerializer.SerializeToUtf8Bytes(selected, s_json).Length.ShouldBeGreaterThan(64 * 1024);
    }

    [Fact]
    public async Task NearOneMiBIssuanceCompletesOrdinaryDeliveryAndRebuildPromotion()
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        string target = new('p', 950_000);
        ProjectionEventDto item = Event(1, 1, new StoredMagicLinkIssued(Issued("cap-near-limit") with
        {
            Target = TimeEntryTargetReference.ForProject(new ProjectReference(target))
        }));
        item.Payload.Length.ShouldBeLessThan(1_048_576);
        (await Deliver(provider, "cap-near-limit", item)).ShouldBe(ProjectionDispatchStatus.Completed);
        (await Read(coordinator, handler, "cap-near-limit")).ShouldNotBeNull()
            .Published.ShouldNotBeNull().Target.TargetId.ShouldBe(target);

        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-1"), handler.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        await Rebuild(provider, "near-limit-rebuild", [("cap-near-limit", item)]);
        (await Read(coordinator, handler, "cap-near-limit")).ShouldNotBeNull()
            .Published.ShouldNotBeNull().Target.TargetId.ShouldBe(target);
    }

    [Fact]
    public async Task NearOneMiBIssuanceAndTerminalPublishCompleteViewWithinPhysicalAndJournalLimits()
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        string target = new('p', 950_000);
        string outcome = "confirmed";
        ProjectionEventDto issue = Event(1, 1, new StoredMagicLinkIssued(Issued("cap-double-large") with
        {
            Target = TimeEntryTargetReference.ForProject(new ProjectReference(target))
        }));
        ProjectionEventDto terminal = Event(1, 2, new StoredMagicLinkUsed(Used("cap-double-large") with
        {
            OutcomeCategory = outcome
        }));
        issue.Payload.Length.ShouldBeLessThan(1_048_576);
        terminal.Payload.Length.ShouldBeLessThan(1_048_576);

        (await Deliver(provider, "cap-double-large", issue)).ShouldBe(ProjectionDispatchStatus.Completed);
        (await Deliver(provider, "entry-1", terminal)).ShouldBe(ProjectionDispatchStatus.Completed);
        MagicLinkCapabilityProjectionView selected = (await Read(coordinator, handler, "cap-double-large"))
            .ShouldNotBeNull();
        selected.Published.ShouldNotBeNull().Target.TargetId.ShouldBe(target);
        selected.Published.UseOutcomeCategory.ShouldBe(outcome);
        JsonSerializer.SerializeToUtf8Bytes(selected, s_json).Length.ShouldBeGreaterThan(1_048_576);

        string key = MagicLinkCapabilityReadModelAddress.StateKey("tenant-1",
            new MagicLinkCapabilityId("cap-double-large"));
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        MagicLinkCapabilityProjectionStorage storage = (await coordinator.ReadGenerationAsync<MagicLinkCapabilityProjectionStorage>(
            scope, 0, key, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        storage.Inline.ShouldBeNull();
        storage.FragmentCount.ShouldBeInRange(1, 127);
        for (int ordinal = 0; ordinal < storage.FragmentCount; ordinal++)
        {
            string fragmentKey = key + ":fragment:" + storage.Digest + ":" + ordinal;
            MagicLinkCapabilityProjectionFragment fragment = (await coordinator
                .ReadGenerationAsync<MagicLinkCapabilityProjectionFragment>(scope, 0, fragmentKey,
                    TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
            JsonSerializer.SerializeToUtf8Bytes(fragment, s_json).Length.ShouldBeLessThanOrEqualTo(64 * 1024);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReplacementRetiresOnlyValidatedPriorFragmentsAfterPublishingPointer(bool inlineReplacement)
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        var id = new MagicLinkCapabilityId("cap-retirement");
        MagicLinkConfirmationCapabilityIssued issue = Issued(id.Value) with
        {
            Target = TimeEntryTargetReference.ForProject(new ProjectReference(
                inlineReplacement ? "project-1" : new string('p', 40_000)))
        };
        (await Deliver(provider, id.Value, Event(1, 1, new StoredMagicLinkIssued(issue))))
            .ShouldBe(ProjectionDispatchStatus.Completed);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        string key = MagicLinkCapabilityReadModelAddress.StateKey("tenant-1", id);
        MagicLinkCapabilityProjectionStorage previous = (await coordinator
            .ReadGenerationAsync<MagicLinkCapabilityProjectionStorage>(scope, 0, key,
                TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        if (inlineReplacement)
        {
            // A valid older encoding may contain whitespace and exceed the current inline limit.
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(previous.Inline, s_json)
                + new string(' ', 50_000));
            string digest = Convert.ToHexString(SHA256.HashData(bytes));
            int count = (bytes.Length + 24 * 1024 - 1) / (24 * 1024);
            for (int ordinal = 0; ordinal < count; ordinal++)
                store.SeedRaw(scope.StoreName, Physical(scope, key + ":fragment:" + digest + ":" + ordinal),
                    new MagicLinkCapabilityProjectionFragment(bytes.AsSpan(ordinal * 24 * 1024,
                        Math.Min(24 * 1024, bytes.Length - ordinal * 24 * 1024)).ToArray()));
            previous = new(null, digest, bytes.Length, count);
            store.SeedRaw(scope.StoreName, Physical(scope, key), previous);
        }
        previous.FragmentCount.ShouldBeGreaterThan(0);
        string[] retired = Enumerable.Range(0, previous.FragmentCount)
            .Select(ordinal => key + ":fragment:" + previous.Digest + ":" + ordinal).ToArray();
        SharedProjectionScope foreignScope = handler.CreateScope("tenant-foreign");
        foreach (string fragment in retired)
            store.SeedRaw(scope.StoreName, Physical(foreignScope, fragment), new MagicLinkCapabilityProjectionFragment([42]));
        ProjectionEventDto terminal = Event(1, 2, new StoredMagicLinkUsed(Used(id.Value)));
        var delivery = new SharedProjectionDelivery("entry-1", 2,
            JsonSerializer.SerializeToUtf8Bytes(new ProjectionRequest("tenant-1", "timesheets", "entry-1", [terminal]), s_json), []);
        IReadOnlyList<ReadModelBatchOperation> operations = await handler.FoldCatchUpAsync(delivery, 0,
            coordinator, TestContext.Current.CancellationToken);
        ReadModelBatchOperation[] deletes = operations.Where(operation => operation.Kind == ReadModelBatchOperationKind.Delete).ToArray();
        deletes.Select(operation => operation.Key).ShouldBe(retired);
        deletes.ShouldAllBe(operation => operation.Concurrency.Mode == ReadModelBatchConcurrencyMode.IdempotentAbsent);
        Array.FindIndex(operations.ToArray(), operation => operation.Key == key)
            .ShouldBeLessThan(Array.FindIndex(operations.ToArray(), operation => operation.Kind == ReadModelBatchOperationKind.Delete));

        (await Deliver(provider, "entry-1", terminal)).ShouldBe(ProjectionDispatchStatus.Completed);
        (await Deliver(provider, "entry-1", terminal)).ShouldBeOneOf(
            ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        foreach (string fragment in retired)
        {
            (await store.GetAsync<MagicLinkCapabilityProjectionFragment>(scope.StoreName, Physical(scope, fragment),
                TestContext.Current.CancellationToken)).Value.ShouldBeNull();
            (await store.GetAsync<MagicLinkCapabilityProjectionFragment>(scope.StoreName, Physical(foreignScope, fragment),
                TestContext.Current.CancellationToken)).Value.ShouldNotBeNull().Data.ShouldBe(new byte[] { 42 });
        }
        MagicLinkCapabilityProjectionStorage selected = (await coordinator.ReadGenerationAsync<MagicLinkCapabilityProjectionStorage>(
            scope, 0, key, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        (selected.Inline is not null).ShouldBe(inlineReplacement);
        (await Read(coordinator, handler, id.Value)).ShouldNotBeNull().Published.ShouldNotBeNull()
            .State.ShouldBe(Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Used);
    }

    [Fact]
    public async Task InterruptedRetirementResumesPreparedDeletesAndCrossingReaderSeesReplacement()
    {
        var inner = new InMemoryReadModelStore();
        var store = new ReadFaultStore(inner);
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, inner);
        using ServiceProvider provider = Provider(inner, coordinator, handler);
        var id = new MagicLinkCapabilityId("cap-retire-retry");
        var issue = Issued(id.Value) with
        {
            Target = TimeEntryTargetReference.ForProject(new ProjectReference(new string('p', 950_000)))
        };
        (await Deliver(provider, id.Value, Event(1, 1, new StoredMagicLinkIssued(issue))))
            .ShouldBe(ProjectionDispatchStatus.Completed);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        string key = MagicLinkCapabilityReadModelAddress.StateKey("tenant-1", id);
        var previous = (await coordinator.ReadGenerationAsync<MagicLinkCapabilityProjectionStorage>(scope, 0, key,
            TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        bool interrupted = false;
        inner.BatchFaultHook = async (phase, _, token) =>
        {
            if (phase == ReadModelBatchPhase.BeforeCommit)
            {
                var selected = await inner.GetAsync<MagicLinkCapabilityProjectionStorage>(scope.StoreName,
                    Physical(scope, key), token);
                if (selected.Value?.Digest != previous.Digest)
                {
                    interrupted = true;
                    throw new IOException("Injected retirement interruption after publication.");
                }
            }
        };
        ProjectionEventDto terminal = Event(1, 2, new StoredMagicLinkUsed(Used(id.Value)));
        (await Deliver(provider, "entry-1", terminal)).ShouldBe(ProjectionDispatchStatus.Retryable);
        interrupted.ShouldBeTrue();
        inner.BatchFaultHook = null;
        (await Deliver(provider, "entry-1", terminal)).ShouldBeOneOf(
            ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        for (int ordinal = 0; ordinal < previous.FragmentCount; ordinal++)
            (await coordinator.ReadGenerationAsync<MagicLinkCapabilityProjectionFragment>(scope, 0,
                key + ":fragment:" + previous.Digest + ":" + ordinal, TestContext.Current.CancellationToken))
                .Value.ShouldBeNull();

        // A reader holding the old pointer must retry even when retirement removes its first fragment.
        var otherId = new MagicLinkCapabilityId("cap-cross-retirement");
        (await Deliver(provider, otherId.Value, Event(1, 3, new StoredMagicLinkIssued(issue with { CapabilityId = otherId }))))
            .ShouldBe(ProjectionDispatchStatus.Completed);
        store.OnFragmentReadAsync = async () =>
            (await Deliver(provider, "entry-1", Event(2, 4, new StoredMagicLinkUsed(Used(otherId.Value)))))
                .ShouldBe(ProjectionDispatchStatus.Completed);
        (await Read(coordinator, handler, otherId.Value)).ShouldNotBeNull().Published.ShouldNotBeNull()
            .State.ShouldBe(Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Used);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("truncated")]
    [InlineData("substituted")]
    [InlineData("digest-invalid")]
    [InlineData("zero-fact")]
    [InlineData("multiple-fact")]
    public async Task PublicReadRejectsMalformedTransitionFragmentsAndFactSets(string damage)
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        var id = new MagicLinkCapabilityId("cap-transition-validation");
        (await Deliver(provider, id.Value, Event(1, 1, new StoredMagicLinkIssued(Issued(id.Value)))))
            .ShouldBe(ProjectionDispatchStatus.Completed);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        string key = MagicLinkCapabilityReadModelAddress.StateKey("tenant-1", id);
        MagicLinkCapabilityProjectionStorage baseStorage = (await coordinator.ReadGenerationAsync<MagicLinkCapabilityProjectionStorage>(
            scope, 0, key, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        (await Deliver(provider, "entry-1", Event(1, 2, new StoredMagicLinkUsed(Used(id.Value)))))
            .ShouldBe(ProjectionDispatchStatus.Completed);
        MagicLinkCapabilityProjectionView complete = (await Read(coordinator, handler, id.Value)).ShouldNotBeNull();
        var transition = new MagicLinkCapabilityProjectionTransition(null, complete.Used, null, null, false, 2);
        byte[] originalBytes = JsonSerializer.SerializeToUtf8Bytes(transition, s_json);
        byte[] completeBytes = JsonSerializer.SerializeToUtf8Bytes(complete, s_json);
        string originalDigest = Convert.ToHexString(SHA256.HashData(originalBytes));
        var continuation = new MagicLinkCapabilityProjectionContinuation("tenant-1", id.Value, baseStorage,
            originalDigest, originalBytes.Length, 1, Convert.ToHexString(SHA256.HashData(completeBytes)), completeBytes.Length);
        string fragmentKey = key + ":transition:" + originalDigest + ":0";
        store.SeedRaw(scope.StoreName, Physical(scope, fragmentKey), new MagicLinkCapabilityProjectionFragment(originalBytes));
        store.SeedRaw(scope.StoreName, Physical(scope, key), new MagicLinkCapabilityProjectionStorage(null, null, 0, 0)
            { Continuation = continuation });
        (await Read(coordinator, handler, id.Value)).ShouldBe(complete);

        byte[] malformedBytes = originalBytes;
        if (damage == "missing")
        {
            var fragment = await store.GetAsync<MagicLinkCapabilityProjectionFragment>(scope.StoreName, Physical(scope, fragmentKey),
                TestContext.Current.CancellationToken);
            (await store.TryEraseAsync(scope.StoreName, Physical(scope, fragmentKey), fragment.ETag!,
                TestContext.Current.CancellationToken)).ShouldBeTrue();
        }
        else if (damage == "truncated") malformedBytes = originalBytes[..^1];
        else if (damage == "substituted")
        {
            // Match the substituted complete view, isolating the transition fragment's own digest check.
            var changedUsed = complete.Used.ShouldNotBeNull() with { UsedAtUtc = complete.Used.UsedAtUtc.AddMinutes(1) };
            malformedBytes = JsonSerializer.SerializeToUtf8Bytes(transition with { Used = changedUsed }, s_json);
            malformedBytes.Length.ShouldBe(originalBytes.Length);
            byte[] changedView = JsonSerializer.SerializeToUtf8Bytes(complete with
                { Used = changedUsed, Published = complete.Published! with { UsedAtUtc = changedUsed.UsedAtUtc } }, s_json);
            continuation = continuation with { ViewDigest = Convert.ToHexString(SHA256.HashData(changedView)), ViewByteLength = changedView.Length };
        }
        else if (damage == "digest-invalid")
        {
            continuation = continuation with { TransitionDigest = new string('0', 64) };
            fragmentKey = key + ":transition:" + continuation.TransitionDigest + ":0";
        }
        else
        {
            MagicLinkCapabilityProjectionTransition malformed = damage == "zero-fact"
                ? transition with { Used = null }
                : transition with { Revoked = new(id, new("tenant-1"), new("issuer-1"),
                    complete.Used!.UsedAtUtc, new("timesheets", "multiple-fact")) { TimeEntryId = new("entry-1") } };
            malformedBytes = JsonSerializer.SerializeToUtf8Bytes(malformed, s_json);
            continuation = continuation with { TransitionDigest = Convert.ToHexString(SHA256.HashData(malformedBytes)),
                TransitionByteLength = malformedBytes.Length };
            fragmentKey = key + ":transition:" + continuation.TransitionDigest + ":0";
            // The Used+Revoked case deliberately retains the valid Used complete-view digest above.
            // If the single-fact check is removed, selecting Used would otherwise pass every later check.
        }
        if (damage != "missing")
            store.SeedRaw(scope.StoreName, Physical(scope, fragmentKey), new MagicLinkCapabilityProjectionFragment(malformedBytes));
        store.SeedRaw(scope.StoreName, Physical(scope, key), new MagicLinkCapabilityProjectionStorage(null, null, 0, 0)
            { Continuation = continuation });
        _ = await Should.ThrowAsync<InvalidOperationException>(() => handler.ReadViewAsync("tenant-1", id, coordinator,
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(128)]
    [InlineData(129)]
    public async Task ExactFoldCapacityBoundaryRetiresOrRetainsTheValidatedFlatBase(int requiredOperations)
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        var id = new MagicLinkCapabilityId("cap-capacity-boundary");
        var issued = Issued(id.Value) with { Target = TimeEntryTargetReference.ForProject(new ProjectReference(new string('p', 30_000))) };
        (await Deliver(provider, id.Value, Event(1, 1, new StoredMagicLinkIssued(issued)))).ShouldBe(ProjectionDispatchStatus.Completed);
        MagicLinkCapabilityProjectionView baseView = (await Read(coordinator, handler, id.Value)).ShouldNotBeNull();
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        string key = MagicLinkCapabilityReadModelAddress.StateKey("tenant-1", id);
        ProjectionEventDto terminal = Event(1, 2, new StoredMagicLinkUsed(Used(id.Value)));
        var delivery = new SharedProjectionDelivery("entry-1", 2,
            JsonSerializer.SerializeToUtf8Bytes(new ProjectionRequest("tenant-1", "timesheets", "entry-1", [terminal]), s_json), []);
        IReadOnlyList<ReadModelBatchOperation> initial = await handler.FoldCatchUpAsync(delivery, 0, coordinator,
            TestContext.Current.CancellationToken);
        int replacementWrites = initial.Count(operation => operation.Kind == ReadModelBatchOperationKind.Write);
        replacementWrites.ShouldBe(3);
        int priorCount = requiredOperations - replacementWrites;
        const int fragmentBytes = 24 * 1024;
        byte[] canonical = JsonSerializer.SerializeToUtf8Bytes(baseView, s_json);
        byte[] padded = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(canonical)
            + new string(' ', (priorCount - 1) * fragmentBytes + 1 - canonical.Length));
        string digest = Convert.ToHexString(SHA256.HashData(padded));
        for (int ordinal = 0; ordinal < priorCount; ordinal++)
            store.SeedRaw(scope.StoreName, Physical(scope, key + ":fragment:" + digest + ":" + ordinal),
                new MagicLinkCapabilityProjectionFragment(padded.AsSpan(ordinal * fragmentBytes,
                    Math.Min(fragmentBytes, padded.Length - ordinal * fragmentBytes)).ToArray()));
        var previous = new MagicLinkCapabilityProjectionStorage(null, digest, padded.Length, priorCount);
        store.SeedRaw(scope.StoreName, Physical(scope, key), previous);
        (await Read(coordinator, handler, id.Value)).ShouldBe(baseView);
        (replacementWrites + previous.FragmentCount).ShouldBe(requiredOperations);

        IReadOnlyList<ReadModelBatchOperation> operations = await handler.FoldCatchUpAsync(delivery, 0, coordinator,
            TestContext.Current.CancellationToken);
        ReadModelBatchOperation[] deletes = operations.Where(operation => operation.Kind == ReadModelBatchOperationKind.Delete).ToArray();
        if (requiredOperations == 128)
        {
            operations.Count.ShouldBe(128);
            deletes.Select(operation => operation.Key).ShouldBe(Enumerable.Range(0, priorCount)
                .Select(ordinal => key + ":fragment:" + digest + ":" + ordinal));
            deletes.ShouldAllBe(operation => operation.Concurrency.Mode == ReadModelBatchConcurrencyMode.IdempotentAbsent);
            operations[replacementWrites - 1].Key.ShouldBe(key);
        }
        else
        {
            operations.Count.ShouldBeInRange(2, 128);
            deletes.ShouldBeEmpty();
            var pointer = JsonSerializer.Deserialize<MagicLinkCapabilityProjectionStorage>(operations.Last().CanonicalValue.Span, s_json)
                .ShouldNotBeNull();
            pointer.Continuation.ShouldNotBeNull().Base.ShouldBe(previous);
        }
        (await Deliver(provider, "entry-1", terminal)).ShouldBe(ProjectionDispatchStatus.Completed);
        var selected = (await coordinator.ReadGenerationAsync<MagicLinkCapabilityProjectionStorage>(scope, 0, key,
            TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        (selected.Continuation is not null).ShouldBe(requiredOperations == 129);
        for (int ordinal = 0; ordinal < priorCount; ordinal++)
        {
            var fragment = (await coordinator.ReadGenerationAsync<MagicLinkCapabilityProjectionFragment>(scope, 0,
                key + ":fragment:" + digest + ":" + ordinal, TestContext.Current.CancellationToken)).Value;
            if (requiredOperations == 128) fragment.ShouldBeNull();
            else fragment.ShouldNotBeNull();
        }
        (await Read(coordinator, handler, id.Value)).ShouldNotBeNull().Published.ShouldNotBeNull().State
            .ShouldBe(Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Used);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnsupportedCompleteViewCapacityPreservesPointerFragmentsAndJournalHead(bool emptyBase)
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        var id = new MagicLinkCapabilityId("cap-oversized-completion");
        (await Deliver(provider, id.Value, Event(1, 1, new StoredMagicLinkIssued(Issued(id.Value)))))
            .ShouldBe(ProjectionDispatchStatus.Completed);
        MagicLinkCapabilityProjectionView oldView = (await Read(coordinator, handler, id.Value)).ShouldNotBeNull();
        // An admitted historical flat view can be larger than a current single-event delivery.
        var largeRow = oldView.Issuance.ShouldNotBeNull() with
        {
            Target = TimeEntryTargetReference.ForProject(new ProjectReference(new string('p', 2_200_000)))
        };
        byte[] bytes = emptyBase
            ? Encoding.UTF8.GetBytes(JsonSerializer.Serialize(MagicLinkCapabilityProjectionView.Empty, s_json) + new string(' ', 5_100_000))
            : JsonSerializer.SerializeToUtf8Bytes(oldView with { Issuance = largeRow, Published = largeRow }, s_json);
        string digest = Convert.ToHexString(SHA256.HashData(bytes));
        int count = (bytes.Length + 40 * 1024 - 1) / (40 * 1024);
        count.ShouldBeInRange(1, 127);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        string key = MagicLinkCapabilityReadModelAddress.StateKey("tenant-1", id);
        for (int ordinal = 0; ordinal < count; ordinal++)
            store.SeedRaw(scope.StoreName, Physical(scope, key + ":fragment:" + digest + ":" + ordinal),
                new MagicLinkCapabilityProjectionFragment(bytes.AsSpan(ordinal * 40 * 1024,
                    Math.Min(40 * 1024, bytes.Length - ordinal * 40 * 1024)).ToArray()));
        var pointer = new MagicLinkCapabilityProjectionStorage(null, digest, bytes.Length, count, 40 * 1024);
        store.SeedRaw(scope.StoreName, Physical(scope, key), pointer);
        var before = await store.GetAsync<MagicLinkCapabilityProjectionStorage>(scope.StoreName, Physical(scope, key),
            TestContext.Current.CancellationToken);
        var revoked = new MagicLinkConfirmationCapabilityRevoked(id, new("tenant-1"), new(new string('r', 950_000)),
            largeRow.IssuedAtUtc.AddHours(1), new("timesheets", "capacity-refusal")) { TimeEntryId = largeRow.TimeEntryId };
        ProjectionEventDto terminal = Event(1, 2, new StoredMagicLinkRevoked(revoked));
        terminal.Payload.Length.ShouldBeLessThan(1_048_576);
        (await Deliver(provider, "entry-1", terminal)).ShouldBe(ProjectionDispatchStatus.Retryable);
        (await Deliver(provider, "entry-1", terminal)).ShouldBe(ProjectionDispatchStatus.Retryable);
        var after = await store.GetAsync<MagicLinkCapabilityProjectionStorage>(scope.StoreName, Physical(scope, key),
            TestContext.Current.CancellationToken);
        after.Value.ShouldBe(pointer); after.ETag.ShouldBe(before.ETag);
        for (int ordinal = 0; ordinal < count; ordinal++)
            (await coordinator.ReadGenerationAsync<MagicLinkCapabilityProjectionFragment>(scope, 0,
                key + ":fragment:" + digest + ":" + ordinal, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        (await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken)).PendingDeliveryCount.ShouldBe(1);
        MagicLinkCapabilityProjectionView retained = (await Read(coordinator, handler, id.Value)).ShouldNotBeNull();
        if (emptyBase) retained.ShouldBe(MagicLinkCapabilityProjectionView.Empty);
        else retained.Published.ShouldNotBeNull().State.ShouldBe(Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Issued);
    }

    [Theory]
    [InlineData("used", false)]
    [InlineData("revoked", false)]
    [InlineData("expired", false)]
    [InlineData("legacy-used", false)]
    [InlineData("legacy-revoked", false)]
    [InlineData("legacy-expired", false)]
    [InlineData("used", true)]
    [InlineData("revoked", true)]
    [InlineData("expired", true)]
    public async Task MaximalEntropyTransitionsUseBoundedContinuationAndRetainReachableFlatBase(string kind, bool terminalFirst)
    {
        var inner = new InMemoryReadModelStore();
        var store = new ReadFaultStore(inner);
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, inner);
        using ServiceProvider provider = Provider(inner, coordinator, handler);
        var id = new MagicLinkCapabilityId("cap-continuation");
        string entropy = Convert.ToHexString(RandomNumberGenerator.GetBytes(475_000));
        MagicLinkConfirmationCapabilityIssued issuance = Issued(id.Value) with
        {
            Target = TimeEntryTargetReference.ForProject(new ProjectReference(entropy))
        };
        bool legacyTerminal = kind.StartsWith("legacy-", StringComparison.Ordinal);
        kind = kind.Replace("legacy-", string.Empty, StringComparison.Ordinal);
        var metadata = new MagicLinkAuditMetadata("timesheets", entropy[..120]);
        object terminal = kind switch
        {
            "used" => new StoredMagicLinkUsed(Used(id.Value) with { Source = metadata }),
            "revoked" => new StoredMagicLinkRevoked(new(id, issuance.Tenant, new PartyReference(entropy),
                issuance.IssuedAtUtc.AddHours(1), metadata) { TimeEntryId = issuance.TimeEntryId }),
            _ => new StoredMagicLinkExpired(new(id, issuance.Tenant, issuance.ExpiresAtUtc, metadata)
                { TimeEntryId = issuance.TimeEntryId })
        };
        if (legacyTerminal)
            terminal = terminal switch
            {
                StoredMagicLinkUsed used => used.Event,
                StoredMagicLinkRevoked revoked => revoked.Event,
                StoredMagicLinkExpired expired => expired.Event,
                _ => throw new InvalidOperationException()
            };
        string terminalOwner = legacyTerminal ? id.Value : "entry-1";
        ProjectionEventDto issue = Event(1, 1, new StoredMagicLinkIssued(issuance));
        ProjectionEventDto fact = Event(legacyTerminal ? 2 : 1, 2, terminal);
        issue.Payload.Length.ShouldBeLessThan(1_048_576);
        fact.Payload.Length.ShouldBeLessThan(1_048_576);
        (await Deliver(provider, terminalFirst ? terminalOwner : id.Value, terminalFirst ? fact : issue))
            .ShouldBe(ProjectionDispatchStatus.Completed);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        string key = MagicLinkCapabilityReadModelAddress.StateKey("tenant-1", id);
        MagicLinkCapabilityProjectionStorage previous = (await coordinator.ReadGenerationAsync<MagicLinkCapabilityProjectionStorage>(
            scope, 0, key, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        if (terminalFirst || kind != "revoked")
        {
            // Legacy flat fragments can have a larger physical footprint than today's encoding.
            MagicLinkCapabilityProjectionView pending = (await Read(coordinator, handler, id.Value)).ShouldNotBeNull();
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(pending, s_json) + new string(' ', terminalFirst ? 2_000_000 : 500_000));
            string digest = Convert.ToHexString(SHA256.HashData(bytes));
            int count = (bytes.Length + 24 * 1024 - 1) / (24 * 1024);
            for (int ordinal = 0; ordinal < count; ordinal++)
                inner.SeedRaw(scope.StoreName, Physical(scope, key + ":fragment:" + digest + ":" + ordinal),
                    new MagicLinkCapabilityProjectionFragment(bytes.AsSpan(ordinal * 24 * 1024,
                        Math.Min(24 * 1024, bytes.Length - ordinal * 24 * 1024)).ToArray()));
            previous = new(null, digest, bytes.Length, count);
            inner.SeedRaw(scope.StoreName, Physical(scope, key), previous);
        }
        string owner = terminalFirst ? id.Value : terminalOwner;
        ProjectionEventDto completion = terminalFirst ? issue : fact;
        var delivery = new SharedProjectionDelivery(owner, completion.GlobalPosition,
            JsonSerializer.SerializeToUtf8Bytes(new ProjectionRequest("tenant-1", "timesheets", owner, [completion]), s_json), []);
        IReadOnlyList<ReadModelBatchOperation> operations = await handler.FoldCatchUpAsync(delivery, 0, coordinator,
            TestContext.Current.CancellationToken);
        operations.Count.ShouldBeInRange(2, 128);
        operations.ShouldAllBe(operation => operation.Kind == ReadModelBatchOperationKind.Write
            && operation.CanonicalValue.Length <= 64 * 1024);
        operations.Last().Key.ShouldBe(key);
        MagicLinkCapabilityProjectionStorage pointer = JsonSerializer.Deserialize<MagicLinkCapabilityProjectionStorage>(
            operations.Last().CanonicalValue.Span, s_json).ShouldNotBeNull();
        pointer.Continuation.ShouldNotBeNull().Base.ShouldBe(previous);
        using (var transitionBytes = new MemoryStream())
        {
            foreach (ReadModelBatchOperation operation in operations.SkipLast(1))
                transitionBytes.Write(JsonSerializer.Deserialize<MagicLinkCapabilityProjectionFragment>(
                    operation.CanonicalValue.Span, s_json).ShouldNotBeNull().Data);
            string json = Encoding.UTF8.GetString(transitionBytes.ToArray());
            json.ShouldNotContain("tokenHash", Case.Insensitive);
            json.ShouldNotContain(issuance.TokenHash.Value);
        }
        // Interrupt after preparing fragments but before publishing their pointer.
        bool interrupted = false;
        inner.BatchFaultHook = (phase, _, _) =>
        {
            if (phase == ReadModelBatchPhase.BeforeCommit && !interrupted)
            {
                interrupted = true;
                throw new IOException("Interrupted continuation publication.");
            }
            return Task.CompletedTask;
        };
        (await Deliver(provider, owner, completion)).ShouldBe(ProjectionDispatchStatus.Retryable);
        inner.BatchFaultHook = null;
        (await coordinator.ReadGenerationAsync<MagicLinkCapabilityProjectionStorage>(scope, 0, key,
            TestContext.Current.CancellationToken)).Value.ShouldBe(previous);
        // A reader crosses the same-generation pointer change while restoring its flat base.
        store.OnFragmentReadAsync = async () =>
            (await Deliver(provider, owner, completion)).ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        MagicLinkCapabilityProjectionView selected = (await Read(coordinator, handler, id.Value)).ShouldNotBeNull();
        selected.Published.ShouldNotBeNull().State.ToString().ToLowerInvariant().ShouldBe(kind);
        selected.Published.Target.TargetId.ShouldBe(entropy);
        var current = (await coordinator.ReadGenerationAsync<MagicLinkCapabilityProjectionStorage>(scope, 0, key,
            TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        current.Continuation.ShouldNotBeNull();
        for (int ordinal = 0; ordinal < previous.FragmentCount; ordinal++)
            (await coordinator.ReadGenerationAsync<MagicLinkCapabilityProjectionFragment>(scope, 0,
                key + ":fragment:" + previous.Digest + ":" + ordinal, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        (await Deliver(provider, owner, completion)).ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        (await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken)).PendingDeliveryCount.ShouldBe(0);

        // Every pointer binding is checked before accepting reconstructed state.
        MagicLinkCapabilityProjectionContinuation continuation = current.Continuation.ShouldNotBeNull();
        MagicLinkCapabilityProjectionView transitionOnly = terminalFirst
            ? selected with { Used = null, Revoked = null, Expired = null, TerminalInCapabilityStream = false,
                TerminalPosition = null, Published = selected.Issuance }
            : selected with { Issuance = null, Published = null };
        byte[] transitionOnlyBytes = JsonSerializer.SerializeToUtf8Bytes(transitionOnly, s_json);
        MagicLinkCapabilityProjectionStorage[] invalid =
        [
            current with { Inline = selected },
            current with { Continuation = continuation with { Base = new(selected, null, 0, 0) } },
            current with { Continuation = continuation with
            {
                Base = new(MagicLinkCapabilityProjectionView.Empty, null, 0, 0),
                ViewDigest = Convert.ToHexString(SHA256.HashData(transitionOnlyBytes)), ViewByteLength = transitionOnlyBytes.Length
            } },
            current with { Continuation = continuation with { Base = new(selected with
                { Published = selected.Published with { CapabilityId = new("foreign") } }, null, 0, 0) } },
            current with { Continuation = continuation with { TenantId = "foreign" } },
            current with { Continuation = continuation with { CapabilityId = "foreign" } },
            current with { Continuation = continuation with { ViewDigest = new string('0', 64) } },
            current with { Continuation = continuation with { ViewByteLength = continuation.ViewByteLength + 1 } },
            current with { Continuation = continuation with { TransitionByteLength = int.MaxValue } },
            current with { Continuation = continuation with { TransitionFragmentCount = 128 } },
            current with { Continuation = continuation with { Base = previous with { Continuation = continuation } } },
            current with { Continuation = continuation with { Base = previous with { Digest = "malformed" } } }
        ];
        foreach (MagicLinkCapabilityProjectionStorage malformed in invalid)
        {
            inner.SeedRaw(scope.StoreName, Physical(scope, key), malformed);
            _ = await Should.ThrowAsync<InvalidOperationException>(() => handler.ReadViewAsync("tenant-1", id, coordinator,
                TestContext.Current.CancellationToken));
        }
        inner.SeedRaw(scope.StoreName, Physical(scope, key), current);
        // Rebuild staging deliberately materializes the established flat encoding.
        if (legacyTerminal) return;
        await Rebuild(provider, "continuation-to-flat", [(id.Value, issue), (terminalOwner, fact)]);
        var staged = (await coordinator.ReadGenerationAsync<MagicLinkCapabilityProjectionStorage>(scope, 1, key,
            TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        staged.Continuation.ShouldBeNull();
        (await Read(coordinator, handler, id.Value)).ShouldNotBeNull().Published.ShouldNotBeNull().State.ToString().ToLowerInvariant()
            .ShouldBe(kind);
    }

    [Fact]
    public async Task LargeViewRetryAfterPartialFragmentBatchesKeepsPriorCompletePointer()
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        MagicLinkConfirmationCapabilityIssued issued = Issued("cap-fragment-retry") with
        {
            Target = TimeEntryTargetReference.ForProject(new ProjectReference(new string('p', 950_000)))
        };
        (await Deliver(provider, "cap-fragment-retry", Event(1, 1, new StoredMagicLinkIssued(issued))))
            .ShouldBe(ProjectionDispatchStatus.Completed);
        int commits = 0;
        store.BatchFaultHook = (phase, _, _) =>
        {
            if (phase == ReadModelBatchPhase.BeforeCommit && ++commits == 3)
            {
                throw new IOException("Injected fragment batch outage.");
            }

            return Task.CompletedTask;
        };
        ProjectionEventDto used = Event(1, 2, new StoredMagicLinkUsed(Used("cap-fragment-retry")));
        (await Deliver(provider, "entry-1", used)).ShouldBe(ProjectionDispatchStatus.Retryable);
        commits.ShouldBeGreaterThanOrEqualTo(3);
        MagicLinkCapabilityProjectionView prior = (await Read(coordinator, handler, "cap-fragment-retry"))
            .ShouldNotBeNull();
        prior.Published.ShouldNotBeNull().State.ShouldBe(Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Issued);
        store.BatchFaultHook = null;
        (await Deliver(provider, "entry-1", used))
            .ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        (await Read(coordinator, handler, "cap-fragment-retry")).ShouldNotBeNull().Published.ShouldNotBeNull()
            .State.ShouldBe(Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Used);
    }

    [Fact]
    public async Task FragmentReadRechecksSelectedEpochAfterConcurrentPromotion()
    {
        var inner = new InMemoryReadModelStore();
        var store = new ReadFaultStore(inner);
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, inner);
        using ServiceProvider provider = Provider(inner, coordinator, handler);
        var id = new MagicLinkCapabilityId("cap-fragment-promotion");
        MagicLinkConfirmationCapabilityIssued issued = Issued(id.Value) with
        {
            Target = TimeEntryTargetReference.ForProject(new ProjectReference(new string('p', 40_000)))
        };
        (await Deliver(provider, id.Value, Event(1, 1, new StoredMagicLinkIssued(issued))))
            .ShouldBe(ProjectionDispatchStatus.Completed);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        string key = MagicLinkCapabilityReadModelAddress.StateKey("tenant-1", id);
        (await coordinator.ReadGenerationAsync<MagicLinkCapabilityProjectionStorage>(scope, 0, key,
            TestContext.Current.CancellationToken)).Value.ShouldNotBeNull().FragmentCount.ShouldBeGreaterThan(0);
        store.OnFragmentReadAsync = async () =>
        {
            _ = await coordinator.BeginAsync(scope, "promote-during-fragments", "captured",
                new Dictionary<string, long> { [id.Value] = 1 }, TestContext.Current.CancellationToken);
            _ = await coordinator.StageAsync(scope, "promote-during-fragments",
                [ReadModelBatchOperation.Write("other-capability", new { Value = 1 },
                    ReadModelBatchConcurrency.LastWrite)], TestContext.Current.CancellationToken);
            await coordinator.CommitAsync(scope, "promote-during-fragments", TestContext.Current.CancellationToken);
        };

        SharedProjectionReadResult<MagicLinkCapabilityProjectionView> selected = await handler.ReadViewAsync(
            "tenant-1", id, coordinator, TestContext.Current.CancellationToken);

        selected.Generation.ShouldBe(1);
        selected.Value.ShouldBeNull();
        store.OnFragmentReadAsync.ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WrongCapabilityStoredUnderSelectedKeyFailsClosed(bool fragmented)
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        (await Deliver(provider, "cap-1", Event(1, 1, new StoredMagicLinkIssued(Issued("cap-1")))))
            .ShouldBe(ProjectionDispatchStatus.Completed);
        MagicLinkConfirmationCapabilityIssued other = Issued("cap-2") with
        {
            Target = TimeEntryTargetReference.ForProject(new ProjectReference(
                fragmented ? new string('p', 40_000) : "project-2"))
        };
        (await Deliver(provider, "cap-2", Event(1, 2, new StoredMagicLinkIssued(other))))
            .ShouldBe(ProjectionDispatchStatus.Completed);
        MagicLinkCapabilityProjectionView wrong = (await Read(coordinator, handler, "cap-2")).ShouldNotBeNull();
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        MagicLinkCapabilityId id = new("cap-1");
        string key = MagicLinkCapabilityReadModelAddress.StateKey("tenant-1", id);
        string physical = Physical(scope, key);
        ReadModelEntry<MagicLinkCapabilityProjectionStorage> existing = await store
            .GetAsync<MagicLinkCapabilityProjectionStorage>(scope.StoreName, physical, TestContext.Current.CancellationToken);
        MagicLinkCapabilityProjectionStorage replacement;
        if (fragmented)
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(wrong, s_json);
            string digest = Convert.ToHexString(SHA256.HashData(bytes));
            int count = (bytes.Length + 24 * 1024 - 1) / (24 * 1024);
            for (int ordinal = 0; ordinal < count; ordinal++)
            {
                byte[] data = bytes.AsSpan(ordinal * 24 * 1024,
                    Math.Min(24 * 1024, bytes.Length - ordinal * 24 * 1024)).ToArray();
                string fragment = key + ":fragment:" + digest + ":" + ordinal;
                (await store.TrySaveAsync(scope.StoreName, Physical(scope, fragment),
                    new MagicLinkCapabilityProjectionFragment(data), string.Empty,
                    TestContext.Current.CancellationToken)).ShouldBeTrue();
            }

            replacement = new MagicLinkCapabilityProjectionStorage(null, digest, bytes.Length, count);
        }
        else
        {
            replacement = new MagicLinkCapabilityProjectionStorage(wrong, null, 0, 0);
        }

        (await store.TrySaveAsync(scope.StoreName, physical, replacement, existing.ETag!,
            TestContext.Current.CancellationToken)).ShouldBeTrue();
        _ = await Should.ThrowAsync<InvalidOperationException>(() => handler.ReadViewAsync(
            "tenant-1", id, coordinator, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MalformedSelectedPointerKeepsValidJournalEventRetryableUntilRepair()
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        (await Deliver(provider, "cap-1", Event(1, 1, new StoredMagicLinkIssued(Issued("cap-1")))))
            .ShouldBe(ProjectionDispatchStatus.Completed);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        string logical = MagicLinkCapabilityReadModelAddress.StateKey("tenant-1", new MagicLinkCapabilityId("cap-1"));
        string physical = Physical(scope, logical);
        ReadModelEntry<MagicLinkCapabilityProjectionStorage> old = await store
            .GetAsync<MagicLinkCapabilityProjectionStorage>(scope.StoreName, physical, TestContext.Current.CancellationToken);
        (await store.TrySaveAsync(scope.StoreName, physical,
            new MagicLinkCapabilityProjectionStorage(null, new string('A', 64), int.MaxValue, 1, 40 * 1024),
            old.ETag!, TestContext.Current.CancellationToken)).ShouldBeTrue();

        ProjectionEventDto terminal = Event(1, 2, new StoredMagicLinkUsed(Used("cap-1")));
        (await Deliver(provider, "entry-1", terminal)).ShouldBe(ProjectionDispatchStatus.Retryable);
        (await Deliver(provider, "entry-1", terminal)).ShouldBe(ProjectionDispatchStatus.Retryable);
        ReadModelEntry<MagicLinkCapabilityProjectionStorage> broken = await store
            .GetAsync<MagicLinkCapabilityProjectionStorage>(scope.StoreName, physical, TestContext.Current.CancellationToken);
        (await store.TrySaveAsync(scope.StoreName, physical, old.Value.ShouldNotBeNull(),
            broken.ETag!, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await Deliver(provider, "entry-1", terminal)).ShouldBeOneOf(
            ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        (await Read(coordinator, handler, "cap-1")).ShouldNotBeNull().Published.ShouldNotBeNull()
            .State.ShouldBe(Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Used);
        (await Deliver(provider, "cap-2", Event(1, 3, new StoredMagicLinkIssued(Issued("cap-2")))))
            .ShouldBe(ProjectionDispatchStatus.Completed);
    }

    [Fact]
    public async Task NormalEpochDeliveryRejectsChangedSamePositionAndPreservesSelectedView()
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        ProjectionEventDto original = Event(1, 1, new StoredMagicLinkIssued(Issued("cap-1")));
        (await Deliver(provider, "cap-1", original)).ShouldBe(ProjectionDispatchStatus.Completed);
        ProjectionEventDto changed = original with { Payload = JsonSerializer.SerializeToUtf8Bytes(new StoredMagicLinkIssued(Issued("cap-1") with { TokenHash = new MagicLinkTokenHash("changed-hash") })) };
        (await Deliver(provider, "cap-1", changed)).ShouldBe(ProjectionDispatchStatus.Failed);
        (await Read(coordinator, handler, "cap-1")).ShouldNotBeNull().Published.ShouldNotBeNull()
            .State.ShouldBe(Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Issued);
    }

    [Fact]
    public async Task TenThousandValidHighEntropyOwnersProduceBoundedIndependentPartitions()
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        var identity = new DomainSharedProjectionRebuildIdentity(
            "tenant-1", "timesheets", MagicLinkConfirmationCapabilityProjection.ProjectionName,
            "high-entropy-10000", s_catalog);
        var partitions = new List<DomainSharedProjectionRebuildPartition>(10_000);
        var histories = new List<(string Owner, ProjectionEventDto Event)>(10_000);
        for (int ordinal = 0; ordinal < 10_000; ordinal++)
        {
            string id = "cap-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(58));
            MagicLinkConfirmationCapabilityIssued issued = Issued("cap-1") with
            {
                CapabilityId = new MagicLinkCapabilityId(id),
                TokenHash = new MagicLinkTokenHash(Convert.ToHexString(RandomNumberGenerator.GetBytes(64))),
                Source = new MagicLinkAuditMetadata(
                    Convert.ToHexString(RandomNumberGenerator.GetBytes(40)),
                    Convert.ToHexString(RandomNumberGenerator.GetBytes(60)))
            };
            ProjectionEventDto item = Event(1, ordinal + 1, new StoredMagicLinkIssued(issued));
            histories.Add((id, item));
            ReadOnlyMemory<byte> bytes = await handler.PreparePartitionAsync(
                identity,
                new ProjectionRequest("tenant-1", "timesheets", id,
                    [item]),
                TestContext.Current.CancellationToken);
            bytes.Length.ShouldBeLessThan(1_048_576);
            partitions.Add(new DomainSharedProjectionRebuildPartition(ordinal, id, bytes));
        }

        DomainProjectionRebuildPlan plan = await handler.FinalizePartitionsAsync(
            identity, partitions, TestContext.Current.CancellationToken);
        plan.Operations.Count.ShouldBe(10_001);
        plan.Operations.All(static operation => operation.CanonicalValue.Length < 64 * 1024).ShouldBeTrue();
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-1"), handler.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        histories.Sort(static (left, right) => string.CompareOrdinal(left.Owner, right.Owner));
        await Rebuild(provider, "high-entropy-full-sdk", histories);
        (await Read(coordinator, handler, histories[0].Owner)).ShouldNotBeNull().Published.ShouldNotBeNull();
        (await Read(coordinator, handler, histories[^1].Owner)).ShouldNotBeNull().Published.ShouldNotBeNull();
    }

    [Fact]
    public async Task LargeValidCapabilityInventoryStreamsMoreThan64MiBThroughFullRebuild()
    {
        const int ownerCount = 2_600;
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-1"), handler.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        var identity = new DomainSharedProjectionRebuildIdentity("tenant-1", "timesheets",
            MagicLinkConfirmationCapabilityProjection.ProjectionName, "large-capability-stream", s_catalog);
        var histories = new List<(string Owner, ProjectionEventDto Event)>(ownerCount);
        for (int ordinal = 0; ordinal < ownerCount; ordinal++)
        {
            string id = "cap-" + ordinal.ToString("D4");
            string targetId = Convert.ToHexString(RandomNumberGenerator.GetBytes(8_192));
            MagicLinkConfirmationCapabilityIssued issued = Issued(id) with
            {
                Target = new TimeEntryTargetReference(TimeEntryTargetKind.Project, targetId)
            };
            histories.Add((id, Event(1, ordinal + 1, new StoredMagicLinkIssued(issued))));
        }

        ReadOnlyMemory<byte> sample = await handler.PreparePartitionAsync(identity,
            new ProjectionRequest("tenant-1", "timesheets", histories[0].Owner, [histories[0].Event]),
            TestContext.Current.CancellationToken);
        DomainProjectionRebuildPlan samplePlan = await handler.FinalizePartitionsAsync(identity,
            [new DomainSharedProjectionRebuildPartition(0, histories[0].Owner, sample)],
            TestContext.Current.CancellationToken);
        ((long)samplePlan.Operations[0].CanonicalValue.Length * ownerCount)
            .ShouldBeGreaterThan(64L * 1024 * 1024);

        await Rebuild(provider, "large-capability-stream", histories);
        (await Read(coordinator, handler, histories[0].Owner)).ShouldNotBeNull().Published.ShouldNotBeNull();
        (await Read(coordinator, handler, histories[^1].Owner)).ShouldNotBeNull().Published.ShouldNotBeNull();
    }

    [Fact]
    public async Task OneTimeEntryOwnerLargerThanOneMiBProducesBoundedCompleteChunks()
    {
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(new InMemoryReadModelStore());
        var identity = new DomainSharedProjectionRebuildIdentity(
            "tenant-1", "timesheets", MagicLinkConfirmationCapabilityProjection.ProjectionName,
            "large-time-entry-owner", s_catalog);
        ProjectionEventDto[] history = Enumerable.Range(1, 4_000)
            .Select(index => Event(index, index, new StoredMagicLinkExpired(
                new MagicLinkConfirmationCapabilityExpired(
                    new MagicLinkCapabilityId("cap-" + index.ToString("D4") + "-" + new string('c', 90)),
                    new TenantReference("tenant-1"),
                    new DateTimeOffset(2026, 6, 19, 15, 0, 0, TimeSpan.Zero),
                    new MagicLinkAuditMetadata(new string('s', 80), new string('r', 120)))
                { TimeEntryId = new TimeEntryId("entry-1") })))
            .ToArray();
        var request = new ProjectionRequest("tenant-1", "timesheets", "entry-1", history);
        ReadOnlyMemory<byte> unchunked = await handler.PreparePartitionAsync(
            identity, request, TestContext.Current.CancellationToken);
        unchunked.Length.ShouldBeGreaterThan(1_048_576);
        IReadOnlyList<ReadOnlyMemory<byte>> chunks = await handler.PreparePartitionChunksAsync(
            identity, request, TestContext.Current.CancellationToken);
        chunks.Count.ShouldBeGreaterThan(1);
        chunks.All(static chunk => chunk.Length <= 256 * 1024).ShouldBeTrue();
        async IAsyncEnumerable<ProjectionEventDto> StreamHistory()
        {
            foreach (ProjectionEventDto item in history)
            {
                await Task.Yield();
                yield return item;
            }
        }
        var streamed = new List<ReadOnlyMemory<byte>>();
        await foreach (ReadOnlyMemory<byte> chunk in handler.StreamPartitionChunksAsync(identity,
            "entry-1", StreamHistory(), TestContext.Current.CancellationToken))
            streamed.Add(chunk);
        streamed.Count.ShouldBe(chunks.Count);
        for (int ordinal = 0; ordinal < chunks.Count; ordinal++)
            streamed[ordinal].Span.SequenceEqual(chunks[ordinal].Span).ShouldBeTrue();
        DomainProjectionRebuildPlan plan = await handler.FinalizePartitionsAsync(
            identity,
            chunks.Select((chunk, ordinal) => new DomainSharedProjectionRebuildPartition(
                0, "entry-1", chunk, ordinal)).ToArray(),
            TestContext.Current.CancellationToken);
        plan.Operations.Count.ShouldBe(4_001);
    }

    [Fact]
    public async Task TenThousandTerminalViewsStreamWithoutAnOwnerWideViewCollection()
    {
        const int terminalCount = 10_000;
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(new InMemoryReadModelStore());
        var identity = new DomainSharedProjectionRebuildIdentity(
            "tenant-1", "timesheets", MagicLinkConfirmationCapabilityProjection.ProjectionName,
            "streamed-terminal-owner", s_catalog);
        async IAsyncEnumerable<ProjectionEventDto> Events()
        {
            for (int index = 1; index <= terminalCount; index++)
            {
                var expired = new MagicLinkConfirmationCapabilityExpired(
                    new MagicLinkCapabilityId("cap-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(40))),
                    new TenantReference("tenant-1"),
                    new DateTimeOffset(2026, 6, 19, 15, 0, 0, TimeSpan.Zero),
                    new MagicLinkAuditMetadata("timesheets", "source-" + index))
                { TimeEntryId = new TimeEntryId("entry-1") };
                await Task.Yield();
                yield return Event(index, index, new StoredMagicLinkExpired(expired));
            }
        }

        int chunks = 0;
        await foreach (ReadOnlyMemory<byte> chunk in handler.StreamPartitionChunksAsync(
            identity, "entry-1", Events(), TestContext.Current.CancellationToken))
        {
            chunk.Length.ShouldBeLessThanOrEqualTo(256 * 1024);
            chunks++;
        }
        chunks.ShouldBe(terminalCount);
    }

    [Fact]
    public async Task NearLimitTerminalOwnerAndJoinedCapabilityCommitThroughTheComposedSdk()
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-1"), handler.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        var identity = new DomainSharedProjectionRebuildIdentity(
            "tenant-1", "timesheets", MagicLinkConfirmationCapabilityProjection.ProjectionName,
            "chunked-composed-owner", s_catalog);
        var contributor = new PartyReference(Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(700 * 1024)));
        (string Owner, ProjectionEventDto Event)[] owners =
        [
            ("cap-1", Event(1, 1, new StoredMagicLinkIssued(Issued("cap-1")))),
            ("entry-1", Event(1, 2, new StoredMagicLinkUsed(Used("cap-1")))),
            ("entry-2", Event(1, 3, new StoredMagicLinkUsed(Used("cap-2") with
                { Contributor = contributor, TimeEntryId = new TimeEntryId("entry-2") })))
        ];
        (string nearLimitOwner, ProjectionEventDto nearLimitEvent) = owners[^1];
        nearLimitEvent.Payload.Length.ShouldBeGreaterThan(900 * 1024);
        nearLimitEvent.Payload.Length.ShouldBeLessThanOrEqualTo(1_048_576);
        async IAsyncEnumerable<ProjectionEventDto> OneEvent()
        {
            await Task.Yield();
            yield return nearLimitEvent;
        }
        var chunks = new List<ReadOnlyMemory<byte>>();
        await foreach (ReadOnlyMemory<byte> chunk in handler.StreamPartitionChunksAsync(identity,
            nearLimitOwner, OneEvent(), TestContext.Current.CancellationToken))
        {
            chunk.Length.ShouldBeLessThanOrEqualTo(1_048_576);
            chunks.Add(chunk);
        }
        chunks.Count.ShouldBeGreaterThan(1);
        string inventory = DomainSharedProjectionRebuildFingerprint.EmptyInventory;
        for (int ordinal = 0; ordinal < owners.Length; ordinal++)
            inventory = DomainSharedProjectionRebuildFingerprint.AppendInventory(inventory, ordinal,
                DomainSharedProjectionRebuildFingerprint.ComputeHistory(owners[ordinal].Owner,
                    false, [owners[ordinal].Event]));
        DomainSharedProjectionRebuildResponse current = await RebuildDispatch(provider,
            new DomainSharedProjectionRebuildRequest(DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildAction.Begin, identity, CaptureInventoryFingerprint: inventory,
                SourceHighWatermarks: new Dictionary<string, long>
                    { ["cap-1"] = 1, ["entry-1"] = 2, ["entry-2"] = 3 }));
        current.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        for (int ordinal = 0; ordinal < owners.Length; ordinal++)
        {
            (string owner, ProjectionEventDto item) = owners[ordinal];
            current = await RebuildDispatch(provider, new DomainSharedProjectionRebuildRequest(
                DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildAction.AccumulatePart, identity,
                AggregateOrdinal: ordinal, AggregateId: owner, Events: [item], PartOrdinal: 0));
            current.Status.ShouldBe(ProjectionDispatchStatus.Completed, current.ReasonCode);
            current = await RebuildDispatch(provider, new DomainSharedProjectionRebuildRequest(
                DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildAction.AccumulateComplete, identity,
                AggregateOrdinal: ordinal, AggregateId: owner, PartCount: 1,
                ExpectedHistoryFingerprint: DomainSharedProjectionRebuildFingerprint.ComputeHistory(
                    owner, false, [item])));
            current.Status.ShouldBe(ProjectionDispatchStatus.Completed, current.ReasonCode);
        }

        current = await RebuildDispatch(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Finalize,
            identity, ExpectedAggregateCount: owners.Length, ExpectedInventoryFingerprint: inventory));
        current.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildDispatch(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Stage,
            identity))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildDispatch(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Commit,
            identity))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await Read(coordinator, handler, "cap-1")).ShouldNotBeNull().Published.ShouldNotBeNull()
            .State.ShouldBe(Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Used);
    }

    [Fact]
    public async Task AppliedRebuildSelectsCompleteNewGenerationAndPrunesMissingCapability()
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-1"), handler.OrdinaryWriterId, TestContext.Current.CancellationToken);
        (string Owner, ProjectionEventDto Event)[] first =
        [
            ("cap-1", Event(1, 1, new StoredMagicLinkIssued(Issued("cap-1")))),
            ("cap-2", Event(1, 2, new StoredMagicLinkIssued(Issued("cap-2"))))
        ];
        await Rebuild(provider, "rebuild-1", first);
        foreach (string capability in new[] { "cap-1", "cap-2" })
        {
            (await store.GetAsync<object>(handler.RebuildStoreName,
                ScratchIndexKey("rebuild-1", capability), TestContext.Current.CancellationToken))
                .Value.ShouldBeNull();
            (await store.GetAsync<object>(handler.RebuildStoreName,
                ScratchIndexMarkerKey("rebuild-1", capability, capability == "cap-1" ? 0 : 1),
                TestContext.Current.CancellationToken)).Value.ShouldBeNull();
        }
        (await Read(coordinator, handler, "cap-1")).ShouldNotBeNull();
        (await Read(coordinator, handler, "cap-2")).ShouldNotBeNull();
        SharedProjectionReadResult<MagicLinkConfirmationCapabilityReadModel> expired = await handler.ReadObservedAsync(
            "tenant-1", new MagicLinkCapabilityId("cap-1"),
            new DateTimeOffset(2026, 6, 22, 0, 0, 0, TimeSpan.Zero), coordinator,
            TestContext.Current.CancellationToken);
        expired.Value.ShouldNotBeNull().ExpiryState.ShouldBe(MagicLinkExpiryState.Expired);

        await Rebuild(provider, "rebuild-2", [first[1]]);
        (await store.GetAsync<object>(handler.RebuildStoreName,
            ScratchIndexKey("rebuild-2", "cap-2"), TestContext.Current.CancellationToken))
            .Value.ShouldBeNull();
        (await Read(coordinator, handler, "cap-1")).ShouldBeNull();
        (await Read(coordinator, handler, "cap-2")).ShouldNotBeNull();
    }

    [Fact]
    public async Task AbortedStagedRebuildRemovesPrivateCapabilityContributionIndex()
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-1"), handler.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        ProjectionEventDto issued = Event(1, 1, new StoredMagicLinkIssued(Issued("cap-abort")));
        var identity = new DomainSharedProjectionRebuildIdentity("tenant-1", "timesheets",
            MagicLinkConfirmationCapabilityProjection.ProjectionName, "staged-abort", s_catalog);
        string fingerprint = DomainSharedProjectionRebuildFingerprint.AppendInventory(
            DomainSharedProjectionRebuildFingerprint.EmptyInventory, 0,
            DomainSharedProjectionRebuildFingerprint.ComputeHistory("cap-abort", false, [issued]));
        (await RebuildDispatch(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin, identity,
            CaptureInventoryFingerprint: fingerprint,
            SourceHighWatermarks: new Dictionary<string, long> { ["cap-abort"] = 1 })))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        DomainSharedProjectionRebuildResponse current = await RebuildDispatch(provider,
            new DomainSharedProjectionRebuildRequest(DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildAction.Accumulate, identity, AggregateOrdinal: 0,
                AggregateId: "cap-abort", Events: [issued]));
        current = await RebuildDispatch(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Finalize, identity,
            ExpectedAggregateCount: current.AcceptedAggregateCount,
            ExpectedInventoryFingerprint: current.InventoryFingerprint));
        current.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildDispatch(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Stage, identity)))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        string indexKey = ScratchIndexKey("staged-abort", "cap-abort");
        (await store.GetAsync<object>(handler.RebuildStoreName, indexKey,
            TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();

        (await RebuildDispatch(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Abort, identity)))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await store.GetAsync<object>(handler.RebuildStoreName, indexKey,
            TestContext.Current.CancellationToken)).Value.ShouldBeNull();
        (await store.GetAsync<object>(handler.RebuildStoreName,
            ScratchIndexMarkerKey("staged-abort", "cap-abort", 0),
            TestContext.Current.CancellationToken)).Value.ShouldBeNull();
        (await Read(coordinator, handler, "cap-abort")).ShouldBeNull();
    }

    [Fact]
    public async Task PausedContributionIndexWriterCannotRecreateScratchAfterAbortCleanup()
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-1"), handler.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        ProjectionEventDto issued = Event(1, 1, new StoredMagicLinkIssued(Issued("cap-race")));
        var identity = new DomainSharedProjectionRebuildIdentity("tenant-1", "timesheets",
            MagicLinkConfirmationCapabilityProjection.ProjectionName, "scratch-race", s_catalog);
        string fingerprint = DomainSharedProjectionRebuildFingerprint.AppendInventory(
            DomainSharedProjectionRebuildFingerprint.EmptyInventory, 0,
            DomainSharedProjectionRebuildFingerprint.ComputeHistory("cap-race", false, [issued]));
        (await RebuildDispatch(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin, identity,
            CaptureInventoryFingerprint: fingerprint,
            SourceHighWatermarks: new Dictionary<string, long> { ["cap-race"] = 1 })))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        DomainSharedProjectionRebuildResponse current = await RebuildDispatch(provider,
            new DomainSharedProjectionRebuildRequest(DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildAction.Accumulate, identity, AggregateOrdinal: 0,
                AggregateId: "cap-race", Events: [issued]));
        (await RebuildDispatch(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Finalize, identity,
            ExpectedAggregateCount: current.AcceptedAggregateCount,
            ExpectedInventoryFingerprint: current.InventoryFingerprint)))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);

        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int pauses = 0;
        store.BatchFaultHook = async (phase, _, token) =>
        {
            if (phase == ReadModelBatchPhase.BeforeDispatch && Interlocked.Increment(ref pauses) == 1)
            {
                reached.TrySetResult();
                await release.Task.WaitAsync(token);
            }
        };
        Task<DomainSharedProjectionRebuildResponse> stage = RebuildDispatch(provider,
            new DomainSharedProjectionRebuildRequest(DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildAction.Stage, identity));
        await reached.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        (await RebuildDispatch(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Abort, identity)))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        release.TrySetResult();
        (await stage).Status.ShouldNotBe(ProjectionDispatchStatus.Completed);
        (await store.GetAsync<object>(handler.RebuildStoreName,
            ScratchIndexKey("scratch-race", "cap-race"), TestContext.Current.CancellationToken))
            .Value.ShouldBeNull();
    }

    [Fact]
    public async Task StreamedRebuildJoinsIssuanceAndTimeEntryTerminalOnce()
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-1"), handler.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        await Rebuild(provider, "joined-stream", [
            ("cap-1", Event(1, 1, new StoredMagicLinkIssued(Issued("cap-1")))),
            ("entry-1", Event(1, 2, new StoredMagicLinkUsed(Used("cap-1"))))
        ]);
        (await Read(coordinator, handler, "cap-1")).ShouldNotBeNull().Published.ShouldNotBeNull()
            .State.ShouldBe(Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Used);
    }

    [Fact]
    public async Task StreamedRebuildJoinsTerminalOwnerBeforeIssuanceOwner()
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-1"), handler.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        await Rebuild(provider, "terminal-before-issuance", [
            ("entry-1", Event(1, 2, new StoredMagicLinkUsed(Used("z-cap")))),
            ("z-cap", Event(1, 1, new StoredMagicLinkIssued(Issued("z-cap"))))
        ]);
        (await Read(coordinator, handler, "z-cap")).ShouldNotBeNull().Published.ShouldNotBeNull()
            .State.ShouldBe(Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Used);
    }

    [Fact]
    public async Task EarlierTerminalPositionCannotBecomeUsedOnLiveDelivery()
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        (await Deliver(provider, "entry-1", Event(1, 1, new StoredMagicLinkUsed(Used("cap-1")))))
            .ShouldBe(ProjectionDispatchStatus.Completed);

        ProjectionDispatchStatus issuance = await Deliver(provider, "cap-1",
            Event(1, 2, new StoredMagicLinkIssued(Issued("cap-1"))));
        issuance.ShouldBeOneOf(ProjectionDispatchStatus.Retryable,
            ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        MagicLinkCapabilityProjectionView? selected = await Read(coordinator, handler, "cap-1");
        selected?.Published?.State.ShouldNotBe(
            Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Used);
    }

    [Fact]
    public async Task EarlierTerminalPositionCannotBePromotedByReverseOwnerRebuild()
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-1"), handler.OrdinaryWriterId,
            TestContext.Current.CancellationToken);

        ProjectionEventDto terminal = Event(1, 1, new StoredMagicLinkUsed(Used("z-cap")));
        ProjectionEventDto issued = Event(1, 2, new StoredMagicLinkIssued(Issued("z-cap")));
        var identity = new DomainSharedProjectionRebuildIdentity(
            "tenant-1", "timesheets", MagicLinkConfirmationCapabilityProjection.ProjectionName,
            "invalid-terminal-position", s_catalog);
        string fingerprint = DomainSharedProjectionRebuildFingerprint.AppendInventory(
            DomainSharedProjectionRebuildFingerprint.AppendInventory(
                DomainSharedProjectionRebuildFingerprint.EmptyInventory, 0,
                DomainSharedProjectionRebuildFingerprint.ComputeHistory("entry-1", false, [terminal])),
            1, DomainSharedProjectionRebuildFingerprint.ComputeHistory("z-cap", false, [issued]));
        DomainSharedProjectionRebuildResponse current = await RebuildDispatch(provider, new(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
            identity, CaptureInventoryFingerprint: fingerprint,
            SourceHighWatermarks: new Dictionary<string, long>
            { ["entry-1"] = 1, ["z-cap"] = 2 }));
        current.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        current = await RebuildDispatch(provider, new(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Accumulate,
            identity, AggregateOrdinal: 0, AggregateId: "entry-1", Events: [terminal]));
        current.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        current = await RebuildDispatch(provider, new(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Accumulate,
            identity, AggregateOrdinal: 1, AggregateId: "z-cap", Events: [issued]));
        if (current.Status == ProjectionDispatchStatus.Completed)
        {
            current = await RebuildDispatch(provider, new(
                DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Finalize,
                identity, ExpectedAggregateCount: current.AcceptedAggregateCount,
                ExpectedInventoryFingerprint: current.InventoryFingerprint));
            if (current.Status == ProjectionDispatchStatus.Completed)
                current = await RebuildDispatch(provider, new(
                    DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Stage,
                    identity));
            if (current.Status == ProjectionDispatchStatus.Completed)
                current = await RebuildDispatch(provider, new(
                    DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Commit,
                    identity));
        }
        current.Status.ShouldNotBe(ProjectionDispatchStatus.Completed);
        (await Read(coordinator, handler, "z-cap"))?.Published?.State.ShouldNotBe(
            Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Used);
    }

    [Fact]
    public async Task CorruptSelectedStorageCanBeRebuiltWithValidPendingJournal()
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        ProjectionEventDto issued = Event(1, 1, new StoredMagicLinkIssued(Issued("cap-repair")));
        ProjectionEventDto used = Event(1, 2, new StoredMagicLinkUsed(Used("cap-repair")));
        (await Deliver(provider, "cap-repair", issued)).ShouldBe(ProjectionDispatchStatus.Completed);

        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        string logicalKey = MagicLinkCapabilityReadModelAddress.StateKey("tenant-1",
            new MagicLinkCapabilityId("cap-repair"));
        string physicalKey = "shared-generation:" + scope.ComputeHash() + ":0:"
            + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(logicalKey)));
        ReadModelEntry<MagicLinkCapabilityProjectionStorage> selected = await store
            .GetAsync<MagicLinkCapabilityProjectionStorage>(scope.StoreName, physicalKey,
                TestContext.Current.CancellationToken);
        selected.Value.ShouldNotBeNull();
        (await store.TrySaveAsync(scope.StoreName, physicalKey,
            selected.Value with { Digest = "corrupt" }, selected.ETag.ShouldNotBeNull(),
            TestContext.Current.CancellationToken)).ShouldBeTrue();

        (await Deliver(provider, "entry-1", used)).ShouldBe(ProjectionDispatchStatus.Retryable);
        await Rebuild(provider, "repair-selected-with-journal", [("cap-repair", issued), ("entry-1", used)]);
        (await Read(coordinator, handler, "cap-repair")).ShouldNotBeNull().Published.ShouldNotBeNull()
            .State.ShouldBe(Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Used);
        (await Deliver(provider, "entry-1", used))
            .ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
    }

    [Fact]
    public async Task RebuildRejectsCapturedHistoryThatChangesPendingJournalEvent()
    {
        var store = new InMemoryReadModelStore();
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, coordinator, handler);
        ProjectionEventDto issued = Event(1, 1, new StoredMagicLinkIssued(Issued("cap-mismatch")));
        ProjectionEventDto used = Event(1, 2, new StoredMagicLinkUsed(Used("cap-mismatch")));
        (await Deliver(provider, "cap-mismatch", issued)).ShouldBe(ProjectionDispatchStatus.Completed);
        SharedProjectionScope scope = handler.CreateScope("tenant-1");
        string logicalKey = MagicLinkCapabilityReadModelAddress.StateKey("tenant-1",
            new MagicLinkCapabilityId("cap-mismatch"));
        string physicalKey = "shared-generation:" + scope.ComputeHash() + ":0:"
            + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(logicalKey)));
        ReadModelEntry<MagicLinkCapabilityProjectionStorage> selected = await store
            .GetAsync<MagicLinkCapabilityProjectionStorage>(scope.StoreName, physicalKey,
                TestContext.Current.CancellationToken);
        (await store.TrySaveAsync(scope.StoreName, physicalKey,
            selected.Value.ShouldNotBeNull() with { Digest = "corrupt" }, selected.ETag.ShouldNotBeNull(),
            TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await Deliver(provider, "entry-1", used)).ShouldBe(ProjectionDispatchStatus.Retryable);

        ProjectionEventDto changed = Event(1, 2, new StoredMagicLinkUsed(Used("cap-mismatch") with
        {
            UsedAtUtc = new DateTimeOffset(2026, 6, 19, 15, 1, 0, TimeSpan.Zero)
        }));
        await Rebuild(provider, "reject-changed-journal", [("cap-mismatch", issued), ("entry-1", changed)],
            ProjectionDispatchStatus.Failed);
        SharedProjectionEpochStatus status = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        status.ActiveGeneration.ShouldBe(0);
        status.PendingDeliveryCount.ShouldBe(1);
    }

    [Fact]
    public async Task SelectedGenerationReadOutageIsRetryableAndKeepsLastView()
    {
        var inner = new InMemoryReadModelStore();
        var fault = new ReadFaultStore(inner);
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(fault);
        var coordinator = new SharedProjectionEpochCoordinator(fault, inner);
        using ServiceProvider provider = Provider(inner, coordinator, handler);
        (await Deliver(provider, "cap-1", Event(1, 1, new StoredMagicLinkIssued(Issued("cap-1")))))
            .ShouldBe(ProjectionDispatchStatus.Completed);
        fault.FailNextGenerationRead = true;
        ProjectionDispatchStatus result = await Deliver(provider, "entry-1", Event(1, 2, new StoredMagicLinkUsed(Used("cap-1"))));
        result.ShouldBe(ProjectionDispatchStatus.Retryable);
        (await Read(coordinator, handler, "cap-1")).ShouldNotBeNull().Published.ShouldNotBeNull()
            .State.ShouldBe(Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Issued);
    }

    [Fact]
    public async Task SelectedDaprReadExceptionIsUnavailableAndCallerCancellationPropagates()
    {
        var inner = new InMemoryReadModelStore();
        var fault = new ReadFaultStore(inner);
        var handler = new MagicLinkConfirmationCapabilityProjectionHandler(fault);
        var coordinator = new SharedProjectionEpochCoordinator(fault, inner);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-1"), handler.OrdinaryWriterId,
            TestContext.Current.CancellationToken);
        fault.NextSelectedReadException = new Dapr.DaprException("Injected selected read outage.");

        SharedProjectionReadUnavailableException unavailable = await Should.ThrowAsync<
            SharedProjectionReadUnavailableException>(() => handler.ReadViewAsync(
                "tenant-1", new MagicLinkCapabilityId("cap-1"), coordinator,
                TestContext.Current.CancellationToken));
        unavailable.InnerException.ShouldBeOfType<Dapr.DaprException>();

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        _ = await Should.ThrowAsync<OperationCanceledException>(() => handler.ReadViewAsync(
            "tenant-1", new MagicLinkCapabilityId("cap-1"), coordinator, canceled.Token));
    }

    private static async Task Rebuild(
        ServiceProvider provider,
        string operation,
        IReadOnlyList<(string Owner, ProjectionEventDto Event)> histories,
        ProjectionDispatchStatus expectedCommit = ProjectionDispatchStatus.Completed)
    {
        string fingerprint = DomainSharedProjectionRebuildFingerprint.EmptyInventory;
        for (int i = 0; i < histories.Count; i++)
        {
            fingerprint = DomainSharedProjectionRebuildFingerprint.AppendInventory(
                fingerprint, i,
                DomainSharedProjectionRebuildFingerprint.ComputeHistory(histories[i].Owner, false, [histories[i].Event]));
        }

        var identity = new DomainSharedProjectionRebuildIdentity(
            "tenant-1", "timesheets", MagicLinkConfirmationCapabilityProjection.ProjectionName, operation, s_catalog);
        var begin = new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Begin,
            identity,
            CaptureInventoryFingerprint: fingerprint,
            SourceHighWatermarks: histories.ToDictionary(static item => item.Owner, static item => item.Event.GlobalPosition, StringComparer.Ordinal));
        DomainSharedProjectionRebuildResponse current = await RebuildDispatch(provider, begin);
        current.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        for (int i = 0; i < histories.Count; i++)
        {
            current = await RebuildDispatch(provider, new DomainSharedProjectionRebuildRequest(
                DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildAction.Accumulate,
                identity,
                AggregateOrdinal: i,
                AggregateId: histories[i].Owner,
                Events: [histories[i].Event]));
            current.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        }

        current = await RebuildDispatch(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Finalize,
            identity,
            ExpectedAggregateCount: current.AcceptedAggregateCount,
            ExpectedInventoryFingerprint: current.InventoryFingerprint));
        current.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        current = await RebuildDispatch(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Stage,
            identity));
        current.Status.ShouldBe(ProjectionDispatchStatus.Completed, current.ReasonCode);
        current.BatchFingerprint.ShouldStartWith("stream:");
        current = await RebuildDispatch(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Commit,
            identity));
        current.Status.ShouldBe(expectedCommit);
        if (expectedCommit != ProjectionDispatchStatus.Completed) return;
        current = await RebuildDispatch(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Verify,
            identity));
        current.Status.ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
    }

    private static string ScratchIndexKey(string operation, string capability)
        => "capability-rebuild-index:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            "tenant-1\0" + operation + "\0" + capability)));

    private static string ScratchIndexMarkerKey(string operation, string capability, long ownerOrdinal)
    {
        string batchId = "scratch-index:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            operation + "\0" + capability + "\0" + ownerOrdinal + ":0")));
        return new ReadModelBatchScope("statestore", "tenant-1", "timesheets",
            MagicLinkConfirmationCapabilityProjection.ProjectionName,
            MagicLinkConfirmationCapabilityProjection.ProjectionName, batchId).ComputeMarkerKey();
    }

    private static Task<DomainSharedProjectionRebuildResponse> RebuildDispatch(
        ServiceProvider provider,
        DomainSharedProjectionRebuildRequest request)
        => DomainSharedProjectionRebuildDispatcher.DispatchAsync(
            provider, request, new ProjectionDispatchOptions(), s_identity, TestContext.Current.CancellationToken);

    private sealed class ReadFaultStore(InMemoryReadModelStore inner) : IReadModelStore
    {
        public bool FailNextGenerationRead { get; set; }

        public Exception? NextSelectedReadException { get; set; }

        public Func<Task>? OnFragmentReadAsync { get; set; }

        public async Task<ReadModelEntry<TValue>> GetAsync<TValue>(
            string storeName, string key, CancellationToken cancellationToken = default) where TValue : class
        {
            if (NextSelectedReadException is { } selectedException
                && key.StartsWith("shared-generation:", StringComparison.Ordinal))
            {
                NextSelectedReadException = null;
                throw selectedException;
            }
            if (FailNextGenerationRead && key.StartsWith("shared-generation:", StringComparison.Ordinal))
            {
                FailNextGenerationRead = false;
                throw new HttpRequestException("Injected read outage.");
            }

            if (typeof(TValue) == typeof(MagicLinkCapabilityProjectionFragment)
                && OnFragmentReadAsync is { } callback)
            {
                OnFragmentReadAsync = null;
                await callback().ConfigureAwait(false);
            }

            return await inner.GetAsync<TValue>(storeName, key, cancellationToken).ConfigureAwait(false);
        }

        public Task SaveAsync<TValue>(
            string storeName, string key, TValue value, CancellationToken cancellationToken = default) where TValue : class
            => inner.SaveAsync(storeName, key, value, cancellationToken);

        public Task<bool> TrySaveAsync<TValue>(
            string storeName, string key, TValue value, string etag, CancellationToken cancellationToken = default) where TValue : class
            => inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
    }

    private static ServiceProvider Provider(
        InMemoryReadModelStore store,
        SharedProjectionEpochCoordinator coordinator,
        MagicLinkConfirmationCapabilityProjectionHandler handler)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IReadModelStore>(store);
        _ = services.AddSingleton<IReadModelBatchStore>(store);
        _ = services.AddSingleton(coordinator);
        _ = services.AddScoped<IAsyncDomainProjectionHandler>(_ => handler);
        return services.BuildServiceProvider();
    }

    private static async Task<ProjectionDispatchStatus> Deliver(ServiceProvider provider, string owner, ProjectionEventDto item)
    {
        ProjectionDispatchResponse result = await DomainProjectionDispatcher.DispatchAsync(
            provider,
            new ProjectionDispatchRequest(
                new ProjectionRequest("tenant-1", "timesheets", owner, [item]),
                [MagicLinkConfirmationCapabilityProjection.ProjectionName],
                "delivery-" + owner + "-" + item.GlobalPosition,
                s_catalog),
            new ProjectionDispatchOptions(),
            s_identity,
            TestContext.Current.CancellationToken);
        return result.Outcomes.Single().Status;
    }

    private static async Task<MagicLinkCapabilityProjectionView?> Read(
        SharedProjectionEpochCoordinator coordinator,
        MagicLinkConfirmationCapabilityProjectionHandler handler,
        string id)
    {
        SharedProjectionReadResult<MagicLinkCapabilityProjectionView> result = await handler.ReadViewAsync(
            "tenant-1", new MagicLinkCapabilityId(id), coordinator, TestContext.Current.CancellationToken);
        result.IsAvailable.ShouldBeTrue();
        return result.Value;
    }

    private static ProjectionEventDto Event(long sequence, long position, object payload)
        => new(payload.GetType().Name, JsonSerializer.SerializeToUtf8Bytes(payload, s_json), "json", sequence,
            DateTimeOffset.UnixEpoch, "correlation-1", "message-" + position, "user-1", position);

    private static string Physical(SharedProjectionScope scope, string key)
        => "shared-generation:" + scope.ComputeHash() + ":0:"
            + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    private static MagicLinkConfirmationCapabilityIssued Issued(string id)
        => new(new MagicLinkCapabilityId(id), new TenantReference("tenant-1"), new PartyReference("party-1"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            new ActivityTypeId("activity-type-1"), new TimeEntryId("entry-1"),
            MagicLinkTargetKind.ProposedTimeEntry, MagicLinkAllowedAction.Confirm,
            new MagicLinkTokenHash("hash-" + id),
            new DateTimeOffset(2026, 6, 21, 12, 0, 0, TimeSpan.Zero),
            new PartyReference("issuer-1"), new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero),
            new MagicLinkAuditMetadata("timesheets", "issue-" + id), true);

    private static MagicLinkConfirmationCapabilityUsed Used(string id)
        => new(new MagicLinkCapabilityId(id), new TenantReference("tenant-1"), new PartyReference("party-1"),
            new TimeEntryId("entry-1"), new DateTimeOffset(2026, 6, 19, 15, 0, 0, TimeSpan.Zero),
            new MagicLinkAuditMetadata("timesheets", "use-" + id));
}
