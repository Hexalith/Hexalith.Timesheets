using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Timesheets.Contracts.Events.ActivityTypes;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.Runtime;

using Shouldly;

using CapabilityState = Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class EventStoreMagicLinkConfirmationCapabilityStateLoaderTests
{
    [Fact]
    public async Task LoadTokenStateAsync_resolves_index_and_folds_capability_time_entry_and_catalog()
    {
        var readModels = new InMemoryReadModelStore(IndexWith(Hash()));
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(2, "capability-2", Issued()), Event(1, "capability-1", Issued()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()))
            .WithStream(Tenant().TenantId, null, Event(1, "activity-1", ActivityCreated()));
        var loader = CreateLoader(gateway, readModels);

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        state.CapabilityState.ShouldNotBeNull().CapabilityId.ShouldBe(CapabilityId());
        state.CapabilityState.TokenHash.ShouldBe(Hash());
        state.TimeEntryState.ShouldNotBeNull().TimeEntryId.ShouldBe(TimeEntryId());
        state.ActivityTypeCatalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        state.ActivityTypeCatalog.Items.ShouldHaveSingleItem().ActivityTypeId.ShouldBe(ActivityId());
        gateway.Requests.Select(static request => request.AggregateId).ShouldBe([CapabilityId().Value, TimeEntryId().Value, null]);
    }

    [Fact]
    public async Task LoadTokenStateAsync_uses_folded_capability_state_as_single_use_authority()
    {
        var readModels = new InMemoryReadModelStore(IndexWith(Hash()));
        var gateway = new ScriptedGatewayClient()
            .WithStream(
                Tenant().TenantId,
                CapabilityId().Value,
                Event(2, "capability-2", Used()),
                Event(1, "capability-1", Issued()),
                Event(2, "capability-2", Used()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()))
            .WithStream(Tenant().TenantId, null, Event(1, "activity-1", ActivityCreated()));
        var loader = CreateLoader(gateway, readModels);

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        state.CapabilityState.ShouldNotBeNull().State.ShouldBe(CapabilityState.Used);
        state.CapabilityState.IsTerminal.ShouldBeTrue();
        state.TimeEntryState.ShouldNotBeNull();
    }

    [Fact]
    public async Task LoadTokenStateAsync_fails_closed_when_index_has_no_hash()
    {
        var gateway = new ScriptedGatewayClient();
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(new MagicLinkTokenHashCapabilityIndexReadModel(
            new Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry>(StringComparer.Ordinal))));

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        state.CapabilityState.ShouldBeNull();
        state.TimeEntryState.ShouldBeNull();
        state.ActivityTypeCatalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Unavailable);
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadCapabilityAsync_uses_trusted_ambient_tenant_for_admin_paths()
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        Hexalith.Timesheets.Server.MagicLinks.MagicLinkCapabilityState? state = await loader
            .LoadCapabilityAsync(CapabilityId(), TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        state.ShouldNotBeNull().Tenant.ShouldBe(Tenant());
        gateway.Requests.ShouldHaveSingleItem().Tenant.ShouldBe(Tenant().TenantId);
    }

    [Fact]
    public void Token_hash_index_rebuilds_from_issued_events_without_raw_token_material()
    {
        MagicLinkTokenHashCapabilityIndexReadModel index = MagicLinkTokenHashCapabilityIndexProjection.Rebuild(
        [
            Issued(),
            Issued(new MagicLinkCapabilityId("capability-2"), new MagicLinkTokenHash("hash-two"))
        ]);

        index.Entries[Hash().Value].CapabilityId.ShouldBe(CapabilityId());
        index.Entries["hash-two"].CapabilityId.ShouldBe(new MagicLinkCapabilityId("capability-2"));

        string json = JsonSerializer.Serialize(index, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        json.ShouldNotContain("opaque-once");
        json.ShouldNotContain("oneTimeToken", Case.Insensitive);
        json.ShouldContain("hash-only");
    }

    [Fact]
    public async Task LoadTokenStateAsync_fails_closed_when_capability_aggregate_is_missing()
    {
        // The index resolves a candidate, but the capability stream folds to no state.
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()))
            .WithStream(Tenant().TenantId, null, Event(1, "activity-1", ActivityCreated()));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadTokenStateAsync_fails_closed_when_folded_token_hash_does_not_match_candidate()
    {
        // The index points at a capability whose authoritative folded TokenHash differs from the
        // hash derived from the presented token. The candidate is non-authoritative; the fold wins.
        var gateway = new ScriptedGatewayClient()
            .WithStream(
                Tenant().TenantId,
                CapabilityId().Value,
                Event(1, "capability-1", Issued(hash: new MagicLinkTokenHash("rotated-hash"))))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()))
            .WithStream(Tenant().TenantId, null, Event(1, "activity-1", ActivityCreated()));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadTokenStateAsync_fails_closed_when_folded_capability_tenant_differs_from_candidate()
    {
        // Cross-tenant: the candidate tenant resolves the stream, but the folded capability carries a
        // different tenant. The loader must not bridge tenants.
        var gateway = new ScriptedGatewayClient()
            .WithStream(
                Tenant().TenantId,
                CapabilityId().Value,
                Event(1, "capability-1", Issued(tenant: new TenantReference("tenant-2"))))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()))
            .WithStream(Tenant().TenantId, null, Event(1, "activity-1", ActivityCreated()));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadTokenStateAsync_fails_closed_when_scoped_time_entry_is_not_recorded()
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithStream(Tenant().TenantId, null, Event(1, "activity-1", ActivityCreated()));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadTokenStateAsync_fails_closed_when_event_store_read_throws()
    {
        var gateway = new ScriptedGatewayClient()
            .WithThrow(Tenant().TenantId, CapabilityId().Value)
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()))
            .WithStream(Tenant().TenantId, null, Event(1, "activity-1", ActivityCreated()));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadTokenStateAsync_returns_unavailable_catalog_when_catalog_read_throws()
    {
        // Capability and Time Entry fold, but the catalog read fails. The loader surfaces an explicit
        // Unavailable freshness so the trust-bearing command service fails closed downstream — it never
        // returns a Fresh catalog it could not load.
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()))
            .WithThrow(Tenant().TenantId, null);
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        state.ActivityTypeCatalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Unavailable);
        state.ActivityTypeCatalog.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadTokenStateAsync_returns_folded_revoked_state_proving_index_is_not_revocation_authority()
    {
        // The index still resolves the candidate after revocation; revocation truth lives only in the
        // folded aggregate, which the downstream validators reject.
        var gateway = new ScriptedGatewayClient()
            .WithStream(
                Tenant().TenantId,
                CapabilityId().Value,
                Event(1, "capability-1", Issued()),
                Event(2, "capability-2", Revoked()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()))
            .WithStream(Tenant().TenantId, null, Event(1, "activity-1", ActivityCreated()));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        state.CapabilityState.ShouldNotBeNull().State.ShouldBe(CapabilityState.Revoked);
        state.CapabilityState.IsTerminal.ShouldBeTrue();
    }

    [Fact]
    public async Task LoadTokenStateAsync_folds_capability_deterministically_across_orderings_and_duplicates()
    {
        var ascending = new ScriptedGatewayClient()
            .WithStream(
                Tenant().TenantId,
                CapabilityId().Value,
                Event(1, "capability-1", Issued()),
                Event(2, "capability-2", Used()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()))
            .WithStream(Tenant().TenantId, null, Event(1, "activity-1", ActivityCreated()));
        var shuffledWithDuplicates = new ScriptedGatewayClient()
            .WithStream(
                Tenant().TenantId,
                CapabilityId().Value,
                Event(2, "capability-2", Used()),
                Event(1, "capability-1", Issued()),
                Event(2, "capability-2", Used()),
                Event(1, "capability-1", Issued()))
            .WithStream(
                Tenant().TenantId,
                TimeEntryId().Value,
                Event(1, "time-1", Recorded()),
                Event(1, "time-1", Recorded()))
            .WithStream(Tenant().TenantId, null, Event(1, "activity-1", ActivityCreated()));

        MagicLinkEndpointTokenState first = await CreateLoader(ascending, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
        MagicLinkEndpointTokenState second = await CreateLoader(shuffledWithDuplicates, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        first.CapabilityState.ShouldNotBeNull().State.ShouldBe(CapabilityState.Used);
        second.CapabilityState.ShouldNotBeNull();
        second.CapabilityState.State.ShouldBe(first.CapabilityState.State);
        second.CapabilityState.IsTerminal.ShouldBe(first.CapabilityState.IsTerminal);
        second.CapabilityState.CapabilityId.ShouldBe(first.CapabilityState.CapabilityId);
        second.CapabilityState.TokenHash.ShouldBe(first.CapabilityState.TokenHash);
        second.CapabilityState.UsedAtUtc.ShouldBe(first.CapabilityState.UsedAtUtc);
        second.CapabilityState.ExpiresAtUtc.ShouldBe(first.CapabilityState.ExpiresAtUtc);
        second.TimeEntryState.ShouldNotBeNull().TimeEntryId.ShouldBe(first.TimeEntryState.ShouldNotBeNull().TimeEntryId);
    }

    [Fact]
    public async Task LoadCapabilityAsync_returns_null_when_capability_aggregate_is_missing()
    {
        var gateway = new ScriptedGatewayClient();
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        Hexalith.Timesheets.Server.MagicLinks.MagicLinkCapabilityState? state = await loader
            .LoadCapabilityAsync(CapabilityId(), TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        state.ShouldBeNull();
    }

    [Fact]
    public async Task LoadCapabilityAsync_returns_null_when_no_ambient_tenant()
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()));
        var loader = CreateLoaderWithoutTenant(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        Hexalith.Timesheets.Server.MagicLinks.MagicLinkCapabilityState? state = await loader
            .LoadCapabilityAsync(CapabilityId(), TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        state.ShouldBeNull();
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadActivityTypeCatalogAsync_fails_closed_when_no_ambient_tenant()
    {
        var gateway = new ScriptedGatewayClient();
        var loader = CreateLoaderWithoutTenant(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        ActivityTypeCatalogReadModel catalog = await loader
            .LoadActivityTypeCatalogAsync(TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        catalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Unavailable);
        catalog.Items.ShouldBeEmpty();
        gateway.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LoadTokenStateAsync_fails_closed_for_blank_token_without_any_read(string blankToken)
    {
        // A malformed/blank token must collapse to the identical opaque state before any index or
        // EventStore read — no resolution work, no observable difference from any other failure.
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync(blankToken, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        ShouldBeOpaqueFailClosed(state);
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadTokenStateAsync_fails_closed_when_token_hashing_rejects_malformed_token()
    {
        // When hashing the presented token throws (malformed material), the loader fails closed without
        // touching the index or EventStore — identical opaque outcome, no disclosure of the reason.
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()));
        var loader = new EventStoreMagicLinkConfirmationCapabilityStateLoader(
            gateway,
            new InMemoryReadModelStore(IndexWith(Hash())),
            new ThrowingTokenGenerator(),
            new FixedTrustedContextAccessor(Tenant(), Operator(), "correlation-1"));

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("malformed-token", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        ShouldBeOpaqueFailClosed(state);
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadTokenStateAsync_returns_folded_expired_state_proving_index_is_not_expiry_authority()
    {
        // Parity with the revoked/used proofs: the index still resolves the candidate after expiry, but
        // expiry truth lives only in the folded aggregate, which the downstream validators reject.
        var gateway = new ScriptedGatewayClient()
            .WithStream(
                Tenant().TenantId,
                CapabilityId().Value,
                Event(1, "capability-1", Issued()),
                Event(2, "capability-2", Expired()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()))
            .WithStream(Tenant().TenantId, null, Event(1, "activity-1", ActivityCreated()));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        state.CapabilityState.ShouldNotBeNull().State.ShouldBe(CapabilityState.Expired);
        state.CapabilityState.IsTerminal.ShouldBeTrue();
    }

    [Fact]
    public async Task LoadActivityTypeCatalogAsync_folds_fresh_tenant_catalog_for_admin_paths()
    {
        // The admin issue/revoke/expire endpoints call the tenant-less LoadActivityTypeCatalogAsync(): with a
        // trusted ambient tenant it must fold a Fresh catalog from the domain-wide (aggregateId == null) read.
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, null, Event(1, "activity-1", ActivityCreated()));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        ActivityTypeCatalogReadModel catalog = await loader
            .LoadActivityTypeCatalogAsync(TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        catalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        catalog.Items.ShouldHaveSingleItem().ActivityTypeId.ShouldBe(ActivityId());
        gateway.Requests.ShouldHaveSingleItem().AggregateId.ShouldBeNull();
    }

    [Fact]
    public async Task LoadActivityTypeCatalogAsync_folds_only_tenant_scoped_items_and_reflects_rename_and_deactivation()
    {
        // Catalog fold correctness: project-scoped activity types are excluded, and rename/deactivate events
        // are applied deterministically so the folded item reflects the latest label and active state.
        var gateway = new ScriptedGatewayClient()
            .WithStream(
                Tenant().TenantId,
                null,
                Event(1, "activity-create", ActivityCreated()),
                Event(2, "activity-rename", new ActivityTypeRenamed(ActivityId(), "Renamed Delivery")),
                Event(3, "activity-deactivate", new ActivityTypeDeactivated(ActivityId())),
                Event(4, "project-activity", ProjectScopedActivityCreated()));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        ActivityTypeCatalogReadModel catalog = await loader
            .LoadActivityTypeCatalogAsync(TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        catalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        ActivityTypeCatalogItem item = catalog.Items.ShouldHaveSingleItem();
        item.ActivityTypeId.ShouldBe(ActivityId());
        item.Label.ShouldBe("Renamed Delivery");
        item.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task LoadActivityTypeCatalogAsync_folds_events_across_continuation_pages()
    {
        // The reader pages the stream via continuation tokens; the fold must accumulate every page so a
        // catalog larger than a single page rebuilds completely and stays Fresh.
        var gateway = new ScriptedGatewayClient()
            .WithPagedStream(
                Tenant().TenantId,
                null,
                [Event(1, "activity-1", ActivityCreated())],
                [Event(2, "activity-2", SecondActivityCreated())]);
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        ActivityTypeCatalogReadModel catalog = await loader
            .LoadActivityTypeCatalogAsync(TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        catalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        catalog.Items
            .Select(static item => item.ActivityTypeId.Value)
            .ShouldBe(["activity-type-1", "activity-type-2"], ignoreOrder: true);
        gateway.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task LoadCapabilityAsync_folds_terminal_state_for_admin_revoke_and_expire_paths()
    {
        // The admin revoke/expire endpoints load existing capability state through LoadCapabilityAsync. Prior
        // events must fold so an already-terminal capability is observed as terminal (not as a fresh issue).
        var gateway = new ScriptedGatewayClient()
            .WithStream(
                Tenant().TenantId,
                CapabilityId().Value,
                Event(1, "capability-1", Issued()),
                Event(2, "capability-2", Revoked()));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        Hexalith.Timesheets.Server.MagicLinks.MagicLinkCapabilityState? state = await loader
            .LoadCapabilityAsync(CapabilityId(), TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        state.ShouldNotBeNull().State.ShouldBe(CapabilityState.Revoked);
        state.IsTerminal.ShouldBeTrue();
    }

    private static void ShouldBeOpaqueFailClosed(MagicLinkEndpointTokenState state)
    {
        // Every loader failure stage collapses to the identical opaque token state: no capability, no
        // Time Entry, and an Unavailable catalog. No channel reveals which stage failed.
        state.CapabilityState.ShouldBeNull();
        state.TimeEntryState.ShouldBeNull();
        state.ActivityTypeCatalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Unavailable);
        state.ActivityTypeCatalog.Items.ShouldBeEmpty();
    }

    private static EventStoreMagicLinkConfirmationCapabilityStateLoader CreateLoader(
        IEventStoreGatewayClient gateway,
        IReadModelStore readModels)
        => new(
            gateway,
            readModels,
            new DeterministicTokenGenerator(),
            new FixedTrustedContextAccessor(Tenant(), Operator(), "correlation-1"));

    private static EventStoreMagicLinkConfirmationCapabilityStateLoader CreateLoaderWithoutTenant(
        IEventStoreGatewayClient gateway,
        IReadModelStore readModels)
        => new(
            gateway,
            readModels,
            new DeterministicTokenGenerator(),
            new FixedTrustedContextAccessor(null, null, "correlation-1"));

    private static MagicLinkTokenHashCapabilityIndexReadModel IndexWith(MagicLinkTokenHash hash)
        => new(new Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry>(StringComparer.Ordinal)
        {
            [hash.Value] = new(Tenant(), CapabilityId())
        });

    private static StreamReadEvent Event(long sequence, string messageId, object payload)
        => new(
            sequence,
            payload.GetType().Name,
            JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            "json",
            1,
            messageId,
            "correlation-1",
            null,
            new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero),
            "operator-1");

    private static MagicLinkConfirmationCapabilityIssued Issued(
        MagicLinkCapabilityId? capabilityId = null,
        MagicLinkTokenHash? hash = null,
        TenantReference? tenant = null)
        => new(
            capabilityId ?? CapabilityId(),
            tenant ?? Tenant(),
            Contributor(),
            TimeEntryTargetReference.ForProject(Project()),
            ActivityId(),
            TimeEntryId(),
            MagicLinkTargetKind.ProposedTimeEntry,
            MagicLinkAllowedAction.ConfirmOrAdjust,
            hash ?? Hash(),
            new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.Zero),
            Operator(),
            new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero),
            new MagicLinkAuditMetadata("timesheets", "issue-1"),
            true);

    private static MagicLinkConfirmationCapabilityUsed Used()
        => new(
            CapabilityId(),
            Tenant(),
            Contributor(),
            TimeEntryId(),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            new MagicLinkAuditMetadata("magic-link", "capability-1"));

    private static MagicLinkConfirmationCapabilityRevoked Revoked()
        => new(
            CapabilityId(),
            Tenant(),
            Operator(),
            new DateTimeOffset(2026, 6, 19, 14, 0, 0, TimeSpan.Zero),
            new MagicLinkAuditMetadata("magic-link", "capability-1"));

    private static MagicLinkConfirmationCapabilityExpired Expired()
        => new(
            CapabilityId(),
            Tenant(),
            new DateTimeOffset(2026, 6, 19, 15, 0, 0, TimeSpan.Zero),
            new MagicLinkAuditMetadata("magic-link", "capability-1"));

    private static TimeEntryRecorded Recorded()
        => new(
            TimeEntryId(),
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            ActivityId(),
            ActivityTypeScope.Tenant,
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            TimeEntryApprovalState.Draft,
            ContributorCategory.ExternalContributor,
            null)
        {
            ExternalSource = new ExternalContributionSource("external", "source-1")
        };

    private static ActivityTypeCreated ActivityCreated()
        => new(
            ActivityId(),
            ActivityTypeScope.Tenant,
            null,
            "Delivery",
            BillableState.Billable);

    private static ActivityTypeCreated SecondActivityCreated()
        => new(
            new ActivityTypeId("activity-type-2"),
            ActivityTypeScope.Tenant,
            null,
            "Research",
            BillableState.Billable);

    private static ActivityTypeCreated ProjectScopedActivityCreated()
        => new(
            new ActivityTypeId("project-activity-type"),
            ActivityTypeScope.Project,
            Project(),
            "Project Work",
            BillableState.Billable);

    private static TenantReference Tenant() => new("tenant-1");

    private static PartyReference Contributor() => new("party-1");

    private static PartyReference Operator() => new("operator-1");

    private static ProjectReference Project() => new("project-1");

    private static ActivityTypeId ActivityId() => new("activity-type-1");

    private static TimeEntryId TimeEntryId() => new("time-entry-1");

    private static MagicLinkCapabilityId CapabilityId() => new("capability-1");

    private static MagicLinkTokenHash Hash() => new("hash-only");

    private sealed class DeterministicTokenGenerator : IMagicLinkTokenGenerator
    {
        public MagicLinkTokenMaterial Generate() => new("opaque-once", Hash());

        public MagicLinkTokenHash DeriveHash(string oneTimeToken)
            => string.Equals(oneTimeToken, "opaque-once", StringComparison.Ordinal)
                ? Hash()
                : new MagicLinkTokenHash("different-hash");
    }

    private sealed class ThrowingTokenGenerator : IMagicLinkTokenGenerator
    {
        public MagicLinkTokenMaterial Generate() => new("opaque-once", Hash());

        public MagicLinkTokenHash DeriveHash(string oneTimeToken)
            => throw new ArgumentException("Malformed token material.", nameof(oneTimeToken));
    }

    private sealed class FixedTrustedContextAccessor(
        TenantReference? tenant,
        PartyReference? actor,
        string correlationId) : ITimesheetsTrustedContextAccessor
    {
        public TenantReference? CurrentTenant => tenant;

        public PartyReference? CurrentActor => actor;

        public string? CurrentCorrelationId => correlationId;
    }

    private sealed class InMemoryReadModelStore(MagicLinkTokenHashCapabilityIndexReadModel? index) : IReadModelStore
    {
        public Task<ReadModelEntry<TValue>> GetAsync<TValue>(
            string storeName,
            string key,
            CancellationToken cancellationToken = default)
            where TValue : class
            => Task.FromResult(new ReadModelEntry<TValue>(index as TValue, "etag-1"));

        public Task SaveAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            CancellationToken cancellationToken = default)
            where TValue : class
            => Task.CompletedTask;

        public Task<bool> TrySaveAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            string etag,
            CancellationToken cancellationToken = default)
            where TValue : class
            => Task.FromResult(true);
    }

    private sealed class ScriptedGatewayClient : IEventStoreGatewayClient
    {
        private readonly Dictionary<(string Tenant, string? AggregateId), StreamReadEvent[]> _streams = [];

        private readonly Dictionary<(string Tenant, string? AggregateId), StreamReadEvent[][]> _pagedStreams = [];

        private readonly HashSet<(string Tenant, string? AggregateId)> _throwingKeys = [];

        public List<StreamReadRequest> Requests { get; } = [];

        public ScriptedGatewayClient WithStream(string tenant, string? aggregateId, params StreamReadEvent[] events)
        {
            _streams[(tenant, aggregateId)] = events;
            return this;
        }

        public ScriptedGatewayClient WithPagedStream(string tenant, string? aggregateId, params StreamReadEvent[][] pages)
        {
            _pagedStreams[(tenant, aggregateId)] = pages;
            return this;
        }

        public ScriptedGatewayClient WithThrow(string tenant, string? aggregateId)
        {
            _throwingKeys.Add((tenant, aggregateId));
            return this;
        }

        public Task<SubmitCommandResponse> SubmitCommandAsync(
            SubmitCommandRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<EventStoreQueryResult> SubmitQueryAsync(
            SubmitQueryRequest request,
            string? ifNoneMatch = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<EventStoreQueryResult<T>> SubmitQueryAsync<T>(
            SubmitQueryRequest request,
            string? ifNoneMatch = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<StreamReadPage> ReadStreamAsync(
            StreamReadRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (_throwingKeys.Contains((request.Tenant, request.AggregateId)))
            {
                throw new InvalidOperationException("EventStore stream read failed.");
            }

            if (_pagedStreams.TryGetValue((request.Tenant, request.AggregateId), out StreamReadEvent[][]? pages))
            {
                int pageIndex = request.ContinuationToken is null
                    ? 0
                    : int.Parse(request.ContinuationToken.Value, System.Globalization.CultureInfo.InvariantCulture);
                StreamReadEvent[] pageEvents = pageIndex < pages.Length ? pages[pageIndex] : [];
                bool hasMore = pageIndex + 1 < pages.Length;
                return Task.FromResult(new StreamReadPage(
                    request.Tenant,
                    request.Domain,
                    request.AggregateId,
                    pageEvents,
                    new StreamReadMetadata(
                        request.FromSequence,
                        request.ToSequence,
                        pageEvents.Length == 0 ? null : pageEvents.Max(static @event => @event.SequenceNumber),
                        pageEvents.Length == 0 ? 0 : pageEvents.Max(static @event => @event.SequenceNumber),
                        pageEvents.Length,
                        hasMore,
                        hasMore
                            ? new ReplayContinuationToken((pageIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))
                            : null)));
            }

            StreamReadEvent[] events = _streams.TryGetValue((request.Tenant, request.AggregateId), out StreamReadEvent[]? value)
                ? value
                : [];
            return Task.FromResult(new StreamReadPage(
                request.Tenant,
                request.Domain,
                request.AggregateId,
                events,
                new StreamReadMetadata(
                    request.FromSequence,
                    request.ToSequence,
                    events.Length == 0 ? null : events.Max(static @event => @event.SequenceNumber),
                    events.Length == 0 ? 0 : events.Max(static @event => @event.SequenceNumber),
                    events.Length,
                    false,
                    null)));
        }
    }
}
