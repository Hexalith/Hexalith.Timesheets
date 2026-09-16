using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Security;
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
        gateway.Requests.Select(static request => request.AggregateId).ShouldBe([CapabilityId().Value, TimeEntryId().Value]);
    }

    [Fact]
    public async Task LoadTokenStateAsync_accepts_exact_fully_qualified_production_event_names()
    {
        StreamReadEvent issued = Event(1, "capability-1", Issued()) with
        {
            EventTypeName = typeof(MagicLinkConfirmationCapabilityIssued).FullName!
        };
        StreamReadEvent recorded = Event(1, "time-1", Recorded()) with
        {
            EventTypeName = typeof(TimeEntryRecorded).FullName!
        };
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, issued)
            .WithStream(Tenant().TenantId, TimeEntryId().Value, recorded);

        MagicLinkEndpointTokenState state = await CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        state.CapabilityState.ShouldNotBeNull().CapabilityId.ShouldBe(CapabilityId());
        state.TimeEntryState.ShouldNotBeNull().TimeEntryId.ShouldBe(TimeEntryId());
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
        var readModels = new InMemoryReadModelStore(new MagicLinkTokenHashCapabilityIndexReadModel(
            new Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry>(StringComparer.Ordinal)));
        var loader = CreateLoader(gateway, readModels);

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        state.CapabilityState.ShouldBeNull();
        state.TimeEntryState.ShouldBeNull();
        state.ActivityTypeCatalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Unavailable);
        gateway.Requests.ShouldBeEmpty();
        readModels.ReadCount.ShouldBe(1);
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
    public async Task LoadTokenStateAsyncRejectsMismatchedActivityTypeIds()
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithStream(
                Tenant().TenantId,
                TimeEntryId().Value,
                Event(1, "time-1", Recorded(activityTypeId: new ActivityTypeId("activity-type-2"))));

        MagicLinkEndpointTokenState state = await CreateLoader(
                gateway,
                new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadTokenStateAsyncAcceptsNewCorrectionFromProjectToTenantScope()
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithStream(
                Tenant().TenantId,
                TimeEntryId().Value,
                Event(1, "time-1", Recorded(activityTypeScope: ActivityTypeScope.Project)),
                Event(2, "time-2", Corrected(ActivityTypeScope.Tenant)));

        MagicLinkEndpointTokenState state = await CreateLoader(
                gateway,
                new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        state.CapabilityState.ShouldNotBeNull();
        state.TimeEntryState.ShouldNotBeNull().ActivityTypeScope.ShouldBe(ActivityTypeScope.Tenant);
        state.ActivityTypeCatalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
    }

    [Fact]
    public async Task LoadTokenStateAsyncRejectsLegacyCorrectionThatRetainsProjectScope()
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithStream(
                Tenant().TenantId,
                TimeEntryId().Value,
                Event(1, "time-1", Recorded(activityTypeScope: ActivityTypeScope.Project)),
                Event(2, "time-2", Corrected(null)));

        MagicLinkEndpointTokenState state = await CreateLoader(
                gateway,
                new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadTokenStateAsyncFoldsSubmittedStateAndRejectsLaterWrongTenantEvent()
    {
        var validGateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithStream(
                Tenant().TenantId,
                TimeEntryId().Value,
                Event(1, "time-1", Recorded()),
                Event(2, "time-2", Submitted(Tenant())));
        var invalidGateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithStream(
                Tenant().TenantId,
                TimeEntryId().Value,
                Event(1, "time-1", Recorded()),
                Event(2, "time-2", Submitted(new TenantReference("tenant-2"))));

        MagicLinkEndpointTokenState valid = await CreateLoader(
                validGateway,
                new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);
        MagicLinkEndpointTokenState invalid = await CreateLoader(
                invalidGateway,
                new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        valid.TimeEntryState.ShouldNotBeNull().ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
        ShouldBeOpaqueFailClosed(invalid);
    }

    [Fact]
    public async Task LoadTokenStateAsyncFailsClosedWhenIndexReadThrows()
    {
        var readModels = new InMemoryReadModelStore(IndexWith(Hash()), throwIndexRead: true);

        MagicLinkEndpointTokenState state = await CreateLoader(new ScriptedGatewayClient(), readModels)
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
        readModels.ReadCount.ShouldBe(1);
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

    [Theory]
    [InlineData("foreign-suffix")]
    [InlineData("null-payload")]
    public async Task LoadTokenStateAsync_fails_closed_for_unrecognized_or_null_capability_payload(string caseName)
    {
        StreamReadEvent issued = Event(1, "capability-1", Issued());
        issued = caseName == "foreign-suffix"
            ? issued with { EventTypeName = $"Foreign.{typeof(MagicLinkConfirmationCapabilityIssued).FullName}" }
            : issued with { Payload = null! };
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, issued);

        MagicLinkEndpointTokenState state = await CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
    }

    [Theory]
    [InlineData("capability")]
    [InlineData("time-entry")]
    public async Task LoadTokenStateAsync_fails_closed_when_payload_identity_differs_from_requested_stream(string stage)
    {
        MagicLinkConfirmationCapabilityIssued issued = stage == "capability"
            ? Issued(new MagicLinkCapabilityId("capability-2"))
            : Issued();
        TimeEntryRecorded recorded = stage == "time-entry"
            ? Recorded(new TimeEntryId("time-entry-2"))
            : Recorded();
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", issued))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", recorded));

        MagicLinkEndpointTokenState state = await CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadTokenStateAsync_fails_closed_for_timeout_cancellation_without_caller_cancellation()
    {
        var gateway = new ScriptedGatewayClient()
            .WithException(Tenant().TenantId, CapabilityId().Value, new OperationCanceledException("Timed out."));

        MagicLinkEndpointTokenState state = await CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadTokenStateAsync_propagates_genuine_caller_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var gateway = new ScriptedGatewayClient()
            .WithException(
                Tenant().TenantId,
                CapabilityId().Value,
                new OperationCanceledException(cancellation.Token));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await loader.LoadTokenStateAsync("opaque-once", cancellation.Token));
    }

    [Fact]
    public async Task LoadTokenStateAsync_fails_closed_for_recognized_event_with_unsupported_serialization()
    {
        StreamReadEvent issued = Event(1, "capability-1", Issued()) with
        {
            SerializationFormat = "application/octet-stream"
        };
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, issued)
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));
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
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash()), throwCatalogRead: true));

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        ShouldBeOpaqueFailClosed(state);
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
        var readModels = new InMemoryReadModelStore(IndexWith(Hash()));
        var loader = CreateLoader(gateway, readModels);

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync(blankToken, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        ShouldBeOpaqueFailClosed(state);
        gateway.Requests.ShouldBeEmpty();
        readModels.ReadCount.ShouldBe(0);
    }

    [Fact]
    public async Task LoadTokenStateAsync_fails_closed_when_token_hashing_rejects_malformed_token()
    {
        // When hashing the presented token throws (malformed material), the loader fails closed without
        // touching the index or EventStore — identical opaque outcome, no disclosure of the reason.
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()));
        var readModels = new InMemoryReadModelStore(IndexWith(Hash()));
        var loader = new EventStoreMagicLinkConfirmationCapabilityStateLoader(
            gateway,
            readModels,
            new ThrowingTokenGenerator(),
            new FixedTrustedContextAccessor(Tenant(), Operator(), "correlation-1"));

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("malformed-token", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        ShouldBeOpaqueFailClosed(state);
        gateway.Requests.ShouldBeEmpty();
        readModels.ReadCount.ShouldBe(0);
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
    public async Task LoadActivityTypeCatalogAsync_reads_fresh_tenant_catalog_for_admin_paths()
    {
        var gateway = new ScriptedGatewayClient();
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        ActivityTypeCatalogReadModel catalog = await loader
            .LoadActivityTypeCatalogAsync(TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        catalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        catalog.Items.ShouldHaveSingleItem().ActivityTypeId.ShouldBe(ActivityId());
        gateway.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadActivityTypeCatalogAsync_rejects_non_fresh_catalog()
    {
        var gateway = new ScriptedGatewayClient();
        var loader = CreateLoader(
            gateway,
            new InMemoryReadModelStore(
                IndexWith(Hash()),
                new ActivityTypeCatalogReadModel([], ProjectionFreshnessMetadata.Stale())));

        ActivityTypeCatalogReadModel catalog = await loader
            .LoadActivityTypeCatalogAsync(TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        catalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Unavailable);
        catalog.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadActivityTypeCatalogAsync_rejects_fresh_catalog_with_blank_label()
    {
        var malformed = new ActivityTypeCatalogReadModel(
            [new ActivityTypeCatalogItem(
                ActivityId(),
                ActivityTypeScope.Tenant,
                null,
                " ",
                true,
                BillableState.Billable)],
            new ProjectionFreshnessMetadata(ProjectionFreshnessState.Fresh, "1", null, null));
        var loader = CreateLoader(
            new ScriptedGatewayClient(),
            new InMemoryReadModelStore(IndexWith(Hash()), malformed));

        ActivityTypeCatalogReadModel catalog = await loader
            .LoadActivityTypeCatalogAsync(TestContext.Current.CancellationToken);

        catalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Unavailable);
        catalog.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadTokenStateAsync_pages_aggregate_streams_with_exclusive_from_sequence()
    {
        var gateway = new ScriptedGatewayClient()
            .WithPagedStream(
                Tenant().TenantId,
                CapabilityId().Value,
                [Event(1, "capability-1", Issued())],
                [Event(2, "capability-2", Used())])
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        state.CapabilityState.ShouldNotBeNull().State.ShouldBe(CapabilityState.Used);
        gateway.Requests
            .Where(static request => request.AggregateId == "capability-1")
            .Select(static request => request.FromSequence)
            .ShouldBe([0, 1]);
    }

    [Fact]
    public async Task LoadTokenStateAsync_fails_closed_when_later_page_regresses_latest_sequence()
    {
        var gateway = new ScriptedGatewayClient()
            .WithPagedStreamLatestSequences(
                Tenant().TenantId,
                CapabilityId().Value,
                [3, 2],
                [Event(1, "capability-1", Issued())],
                [Event(2, "capability-2", Used())]);
        var loader = CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())));

        MagicLinkEndpointTokenState state = await loader
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        ShouldBeOpaqueFailClosed(state);
        gateway.Requests
            .Where(static request => request.AggregateId == "capability-1")
            .Select(static request => request.FromSequence)
            .ShouldBe([0, 1]);
    }

    [Fact]
    public async Task LoadTokenStateAsync_fails_closed_for_aggregate_sequence_gap()
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(
                Tenant().TenantId,
                CapabilityId().Value,
                Event(1, "capability-1", Issued()),
                Event(3, "capability-3", Used()));

        MagicLinkEndpointTokenState state = await CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
    }

    [Theory]
    [InlineData("missing-last")]
    [InlineData("unsolicited-to")]
    public async Task LoadTokenStateAsync_fails_closed_for_malformed_stream_page_bounds(string caseName)
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithPageTransform(
                Tenant().TenantId,
                CapabilityId().Value,
                page => page with
                {
                    Metadata = caseName == "missing-last"
                        ? page.Metadata with { LastSequenceReturned = null }
                        : page.Metadata with { ToSequence = 1 }
                });

        MagicLinkEndpointTokenState state = await CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("domain")]
    [InlineData("aggregate")]
    public async Task LoadTokenStateAsync_fails_closed_when_stream_page_scope_differs_from_request(string scopePart)
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()))
            .WithPageTransform(
                Tenant().TenantId,
                CapabilityId().Value,
                page => scopePart switch
                {
                    "tenant" => page with { Tenant = "tenant-2" },
                    "domain" => page with { Domain = "other-domain" },
                    "aggregate" => page with { AggregateId = "capability-2" },
                    _ => throw new InvalidOperationException($"Unknown page-scope part '{scopePart}'.")
                });

        MagicLinkEndpointTokenState state = await CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadTokenStateAsyncFailsClosedWhenPageEventCountDisagreesWithEvents()
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithPageTransform(
                Tenant().TenantId,
                CapabilityId().Value,
                page => page with { Metadata = page.Metadata with { EventCount = page.Events.Count + 1 } });

        MagicLinkEndpointTokenState state = await CreateLoader(
                gateway,
                new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
    }

    [Theory]
    [InlineData("event-type")]
    [InlineData("payload")]
    [InlineData("serialization-format")]
    [InlineData("metadata-version")]
    [InlineData("message-id")]
    [InlineData("correlation-id")]
    [InlineData("causation-id")]
    [InlineData("timestamp")]
    [InlineData("user-id")]
    [InlineData("protection-metadata")]
    public async Task LoadTokenStateAsync_fails_closed_for_same_sequence_events_with_conflicting_envelope(
        string conflict)
    {
        StreamReadEvent first = Event(1, "capability-1", Issued());
        if (conflict == "protection-metadata")
        {
            first = first with
            {
                ProtectionMetadata = ProtectionMetadata("first")
            };
        }

        StreamReadEvent conflicting = conflict switch
        {
            "event-type" => first with { EventTypeName = typeof(MagicLinkConfirmationCapabilityUsed).Name },
            "payload" => first with
            {
                Payload = JsonSerializer.SerializeToUtf8Bytes(
                    Issued(hash: new MagicLinkTokenHash("conflicting-hash")),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))
            },
            "serialization-format" => first with { SerializationFormat = "xml" },
            "metadata-version" => first with { MetadataVersion = 2 },
            "message-id" => first with { MessageId = "capability-conflict" },
            "correlation-id" => first with { CorrelationId = "correlation-2" },
            "causation-id" => first with { CausationId = "causation-2" },
            "timestamp" => first with { Timestamp = first.Timestamp.AddMinutes(1) },
            "user-id" => first with { UserId = "operator-2" },
            "protection-metadata" => first with
            {
                ProtectionMetadata = ProtectionMetadata("second")
            },
            _ => throw new InvalidOperationException($"Unknown envelope conflict '{conflict}'.")
        };
        var gateway = new ScriptedGatewayClient()
            .WithStream(
                Tenant().TenantId,
                CapabilityId().Value,
                first,
                conflicting)
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));

        MagicLinkEndpointTokenState state = await CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadTokenStateAsync_accepts_same_sequence_duplicates_with_equivalent_protection_metadata()
    {
        StreamReadEvent first = Event(1, "capability-1", Issued()) with
        {
            ProtectionMetadata = ProtectionMetadata("same-reason")
        };
        StreamReadEvent duplicate = first with
        {
            Payload = first.Payload.ToArray(),
            ProtectionMetadata = ProtectionMetadata("same-reason")
        };
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, first, duplicate)
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));

        MagicLinkEndpointTokenState state = await CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        state.CapabilityState.ShouldNotBeNull().CapabilityId.ShouldBe(CapabilityId());
        state.TimeEntryState.ShouldNotBeNull().TimeEntryId.ShouldBe(TimeEntryId());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task LoadTokenStateAsync_fails_closed_for_blank_event_message_id(string? messageId)
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(
                Tenant().TenantId,
                CapabilityId().Value,
                Event(1, "capability-1", Issued()) with { MessageId = messageId! })
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));

        MagicLinkEndpointTokenState state = await CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
    }

    [Fact]
    public async Task LoadTokenStateAsync_fails_closed_when_one_message_id_appears_at_different_sequences()
    {
        var gateway = new ScriptedGatewayClient()
            .WithStream(
                Tenant().TenantId,
                CapabilityId().Value,
                Event(1, "repeated-message", Issued()),
                Event(2, "repeated-message", Used()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));

        MagicLinkEndpointTokenState state = await CreateLoader(gateway, new InMemoryReadModelStore(IndexWith(Hash())))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);
    }

    [Theory]
    [InlineData("project-scope")]
    [InlineData("project-reference")]
    [InlineData("duplicate-identifier")]
    public async Task LoadTokenStateAsync_fails_closed_for_invalid_fresh_catalog_shape(string scenario)
    {
        ActivityTypeCatalogReadModel malformed = InvalidFreshCatalog(scenario);
        var gateway = new ScriptedGatewayClient()
            .WithStream(Tenant().TenantId, CapabilityId().Value, Event(1, "capability-1", Issued()))
            .WithStream(Tenant().TenantId, TimeEntryId().Value, Event(1, "time-1", Recorded()));

        MagicLinkEndpointTokenState state = await CreateLoader(
                gateway,
                new InMemoryReadModelStore(IndexWith(Hash()), malformed))
            .LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        ShouldBeOpaqueFailClosed(state);

        ActivityTypeCatalogReadModel adminCatalog = await CreateLoader(
                new ScriptedGatewayClient(),
                new InMemoryReadModelStore(IndexWith(Hash()), malformed))
            .LoadActivityTypeCatalogAsync(TestContext.Current.CancellationToken);
        adminCatalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Unavailable);
        adminCatalog.Items.ShouldBeEmpty();
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

    private static EventStorePayloadProtectionMetadata ProtectionMetadata(string compatibilityValue)
        => new(
            PayloadProtectionState.Unprotected,
            EventStorePayloadProtectionMetadata.CurrentMetadataVersion,
            null,
            null,
            "application/json",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["compatibility"] = compatibilityValue
            });

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

    private static TimeEntryRecorded Recorded(
        TimeEntryId? timeEntryId = null,
        ActivityTypeId? activityTypeId = null,
        ActivityTypeScope activityTypeScope = ActivityTypeScope.Tenant)
        => new(
            timeEntryId ?? TimeEntryId(),
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            activityTypeId ?? ActivityId(),
            activityTypeScope,
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            TimeEntryApprovalState.Draft,
            ContributorCategory.ExternalContributor,
            null)
        {
            ExternalSource = new ExternalContributionSource("external", "source-1")
        };

    private static TimeEntrySubmitted Submitted(TenantReference tenant)
        => new(
            TimeEntryId(),
            Operator(),
            tenant,
            new DateTimeOffset(2026, 6, 19, 12, 30, 0, TimeSpan.Zero),
            new TimeEntrySubmissionId("submission-1"),
            TimeEntrySubmissionScope.SelectedEntries,
            TimeEntryApprovalState.Submitted);

    private static TimeEntryCorrected Corrected(ActivityTypeScope? correctedScope)
    {
        TimeEntryCorrectionValues previous = new(
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            ActivityId(),
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            ContributorCategory.ExternalContributor,
            null)
        {
            ActivityTypeScope = correctedScope is null ? null : ActivityTypeScope.Project
        };
        TimeEntryCorrectionValues corrected = previous with
        {
            ActivityTypeScope = correctedScope,
            DurationMinutes = 75
        };
        return new(
            TimeEntryId(),
            new TimeEntryCorrectionId("correction-1"),
            Tenant(),
            Operator(),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            previous,
            corrected,
            new TimeEntryRejectionReason("Rejected."),
            new TimeEntryApprovalDecisionId("decision-1"),
            TimeEntryApprovalState.Draft,
            TimeEntryCorrectionState.Corrected);
    }

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

    private static ActivityTypeCatalogReadModel InvalidFreshCatalog(string scenario)
    {
        ActivityTypeCatalogItem first = scenario switch
        {
            "project-scope" => new ActivityTypeCatalogItem(
                ProjectScopedActivityCreated().ActivityTypeId,
                ActivityTypeScope.Project,
                null,
                "Project Work",
                true,
                BillableState.Billable),
            "project-reference" => new ActivityTypeCatalogItem(
                ActivityId(),
                ActivityTypeScope.Tenant,
                Project(),
                "Delivery",
                true,
                BillableState.Billable),
            "duplicate-identifier" => new ActivityTypeCatalogItem(
                ActivityId(),
                ActivityTypeScope.Tenant,
                null,
                "Delivery",
                true,
                BillableState.Billable),
            _ => throw new InvalidOperationException($"Unknown invalid-catalog scenario '{scenario}'.")
        };
        ActivityTypeCatalogItem[] items = scenario == "duplicate-identifier"
            ? [
                first,
                new ActivityTypeCatalogItem(
                    ActivityId(),
                    ActivityTypeScope.Tenant,
                    null,
                    SecondActivityCreated().Label,
                    true,
                    BillableState.Billable)
            ]
            : [first];
        return new ActivityTypeCatalogReadModel(
            items,
            new ProjectionFreshnessMetadata(ProjectionFreshnessState.Fresh, "2", null, null));
    }

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

    private sealed class InMemoryReadModelStore(
        MagicLinkTokenHashCapabilityIndexReadModel? index,
        ActivityTypeCatalogReadModel? catalog = null,
        bool throwCatalogRead = false,
        bool throwIndexRead = false) : IReadModelStore
    {
        public int ReadCount { get; private set; }

        public Task<ReadModelEntry<TValue>> GetAsync<TValue>(
            string storeName,
            string key,
            CancellationToken cancellationToken = default)
            where TValue : class
        {
            ReadCount++;

            if (typeof(TValue) == typeof(ActivityTypeCatalogReadModel))
            {
                // Verify the addressing, not just the type. Dispatching on typeof(TValue) alone let
                // the loader read any store and any key and still pass, so the tenant-scoped catalog
                // key and its state-store component were never actually asserted by these tests.
                storeName.ShouldBe(MagicLinkActivityTypeCatalogReadModelAddress.StateStoreName);
                key.ShouldBe(MagicLinkActivityTypeCatalogReadModelAddress.StateKey(Tenant()));

                if (throwCatalogRead)
                {
                    throw new InvalidOperationException("Catalog read failed.");
                }

                ActivityTypeCatalogReadModel value = catalog ?? new ActivityTypeCatalogReadModel(
                    [new ActivityTypeCatalogItem(
                        ActivityId(),
                        ActivityTypeScope.Tenant,
                        null,
                        "Delivery",
                        true,
                        BillableState.Billable)],
                    new ProjectionFreshnessMetadata(ProjectionFreshnessState.Fresh, "1", null, null));
                return Task.FromResult(new ReadModelEntry<TValue>(value as TValue, "etag-catalog"));
            }

            storeName.ShouldBe(MagicLinkTokenHashCapabilityIndexProjection.StateStoreName);
            key.ShouldBe(MagicLinkTokenHashCapabilityIndexProjection.StateKey);
            if (throwIndexRead)
            {
                throw new InvalidOperationException("Index read failed.");
            }

            return Task.FromResult(new ReadModelEntry<TValue>(index as TValue, "etag-index"));
        }

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

        // The magic-link loader never queries command status; this member exists only to satisfy
        // IEventStoreGatewayClient.
        public Task<CommandStatusQueryResponse?> GetCommandStatusAsync(
            string messageId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        private readonly Dictionary<(string Tenant, string? AggregateId), StreamReadEvent[]> _streams = [];

        private readonly Dictionary<(string Tenant, string? AggregateId), StreamReadEvent[][]> _pagedStreams = [];

        private readonly Dictionary<(string Tenant, string? AggregateId), long[]> _pagedLatestSequences = [];

        private readonly Dictionary<(string Tenant, string? AggregateId), Exception> _exceptions = [];

        private readonly Dictionary<(string Tenant, string? AggregateId), Func<StreamReadPage, StreamReadPage>> _pageTransforms = [];

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

        public ScriptedGatewayClient WithPagedStreamLatestSequences(
            string tenant,
            string? aggregateId,
            long[] latestSequences,
            params StreamReadEvent[][] pages)
        {
            if (latestSequences.Length != pages.Length)
            {
                throw new ArgumentException("Each page must have one LatestSequence value.", nameof(latestSequences));
            }

            _pagedStreams[(tenant, aggregateId)] = pages;
            _pagedLatestSequences[(tenant, aggregateId)] = latestSequences;
            return this;
        }

        public ScriptedGatewayClient WithThrow(string tenant, string? aggregateId)
        {
            _exceptions[(tenant, aggregateId)] = new InvalidOperationException("EventStore stream read failed.");
            return this;
        }

        public ScriptedGatewayClient WithException(string tenant, string? aggregateId, Exception exception)
        {
            _exceptions[(tenant, aggregateId)] = exception;
            return this;
        }

        public ScriptedGatewayClient WithPageTransform(
            string tenant,
            string? aggregateId,
            Func<StreamReadPage, StreamReadPage> transform)
        {
            _pageTransforms[(tenant, aggregateId)] = transform;
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
            if (_exceptions.TryGetValue((request.Tenant, request.AggregateId), out Exception? exception))
            {
                throw exception;
            }

            if (_pagedStreams.TryGetValue((request.Tenant, request.AggregateId), out StreamReadEvent[][]? pages))
            {
                int pageIndex = pages.TakeWhile(page => page.Length == 0
                    || page.Max(static item => item.SequenceNumber) <= request.FromSequence).Count();
                StreamReadEvent[] pageEvents = pageIndex < pages.Length ? pages[pageIndex] : [];
                bool hasMore = pageIndex + 1 < pages.Length;
                long latestSequence = _pagedLatestSequences.TryGetValue(
                        (request.Tenant, request.AggregateId),
                        out long[]? latestSequences)
                    && pageIndex < latestSequences.Length
                        ? latestSequences[pageIndex]
                        : pageEvents.Length == 0
                            ? 0
                            : pageEvents.Max(static item => item.SequenceNumber);
                StreamReadPage result = new(
                    request.Tenant,
                    request.Domain,
                    request.AggregateId,
                    pageEvents,
                    new StreamReadMetadata(
                        request.FromSequence,
                        request.ToSequence,
                        pageEvents.Length == 0 ? null : pageEvents.Max(static @event => @event.SequenceNumber),
                        latestSequence,
                        pageEvents.Length,
                        hasMore,
                        hasMore
                            ? new ReplayContinuationToken((pageIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))
                            : null));
                return Task.FromResult(Transform(request, result));
            }

            StreamReadEvent[] events = _streams.TryGetValue((request.Tenant, request.AggregateId), out StreamReadEvent[]? value)
                ? value
                : [];
            StreamReadPage page = new(
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
                    null));
            return Task.FromResult(Transform(request, page));
        }

        private StreamReadPage Transform(StreamReadRequest request, StreamReadPage page)
            => _pageTransforms.TryGetValue(
                (request.Tenant, request.AggregateId),
                out Func<StreamReadPage, StreamReadPage>? transform)
                    ? transform(page)
                    : page;
    }
}
