using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Attributes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Timesheets.Contracts.Events.ActivityTypes;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections.ActivityTypes;
using Hexalith.Timesheets.Projections.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks;

using Shouldly;

namespace Hexalith.Timesheets.Projections.Tests;

public sealed class MagicLinkStateProjectionHandlerTests
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Index_live_writer_is_idempotent_and_persists_only_safe_candidate_fields()
    {
        var store = new ScriptedReadModelStore();
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store);
        ProjectionEventDto issued = Event(1, Issued());
        var request = new ProjectionRequest("tenant-1", "timesheets", "capability-1", [issued, issued]);

        DomainProjectionHandlerResult first = await handler.ProjectAsync(
            request,
            "dispatch-1",
            TestContext.Current.CancellationToken);
        DomainProjectionHandlerResult second = await handler.ProjectAsync(
            request with { Events = [issued] },
            "dispatch-2",
            TestContext.Current.CancellationToken);

        first.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        second.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        MagicLinkTokenHashCapabilityIndexReadModel index = store.Get<MagicLinkTokenHashCapabilityIndexReadModel>(
            MagicLinkTokenHashCapabilityIndexProjection.StateKey);
        index.Entries.ShouldHaveSingleItem();
        index.Entries["hash-only"].ShouldBe(new MagicLinkTokenHashCapabilityIndexEntry(
            new TenantReference("tenant-1"),
            new MagicLinkCapabilityId("capability-1")));
        string json = JsonSerializer.Serialize(index, s_jsonOptions);
        json.ShouldNotContain("opaque-token");
        json.ShouldNotContain("oneTimeToken", Case.Insensitive);
    }

    [Fact]
    public async Task Index_live_collision_fails_without_replacing_the_original_candidate()
    {
        var store = new ScriptedReadModelStore();
        var original = new MagicLinkTokenHashCapabilityIndexEntry(
            new TenantReference("tenant-1"),
            new MagicLinkCapabilityId("capability-1"));
        store.Set(
            MagicLinkTokenHashCapabilityIndexProjection.StateKey,
            new MagicLinkTokenHashCapabilityIndexReadModel(new Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry>
            {
                ["hash-only"] = original
            }));
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store);

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            new ProjectionRequest(
                "tenant-1",
                "timesheets",
                "capability-2",
                [Event(1, Issued(new MagicLinkCapabilityId("capability-2")))]),
            "dispatch-collision",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        store.Get<MagicLinkTokenHashCapabilityIndexReadModel>(
            MagicLinkTokenHashCapabilityIndexProjection.StateKey).Entries["hash-only"].ShouldBe(original);
    }

    [Fact]
    public async Task Projection_normalization_rejects_cross_sequence_message_id_reuse()
    {
        var store = new ScriptedReadModelStore();
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store);
        ProjectionEventDto first = Event(1, Issued());
        ProjectionEventDto second = Event(2, Issued()) with { MessageId = first.MessageId };

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            new ProjectionRequest("tenant-1", "timesheets", "capability-1", [first, second]),
            "dispatch-ambiguous-message",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
    }

    [Fact]
    public async Task Projection_normalization_rejects_same_sequence_duplicate_with_conflicting_metadata()
    {
        var store = new ScriptedReadModelStore();
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store);
        ProjectionEventDto first = Event(1, Issued());
        ProjectionEventDto conflicting = first with { GlobalPosition = first.GlobalPosition + 1 };

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            new ProjectionRequest("tenant-1", "timesheets", "capability-1", [first, conflicting]),
            "dispatch-ambiguous-metadata",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
    }

    [Fact]
    public async Task Index_live_writer_rejects_identical_issuances_at_different_sequences_without_writing()
    {
        var store = new ScriptedReadModelStore();
        string expectedState = SeedExistingState(store, "index");
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store);

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            new ProjectionRequest(
                "tenant-1",
                "timesheets",
                "capability-1",
                [Event(1, Issued()), Event(2, Issued())]),
            "dispatch-twice-issued",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        ReadExistingStateSnapshot(store, "index").ShouldBe(expectedState);
        store.TrySaveCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("catalog", "null-events")]
    [InlineData("catalog", "null-event")]
    [InlineData("catalog", "null-event-type")]
    [InlineData("catalog", "blank-event-type")]
    [InlineData("catalog", "missing-activity-type")]
    [InlineData("catalog", "blank-activity-type")]
    [InlineData("index", "null-events")]
    [InlineData("index", "null-event")]
    [InlineData("index", "null-event-type")]
    [InlineData("index", "blank-event-type")]
    [InlineData("index", "missing-tenant")]
    [InlineData("index", "blank-tenant")]
    [InlineData("index", "missing-capability")]
    [InlineData("index", "blank-capability")]
    [InlineData("index", "missing-token-hash")]
    [InlineData("index", "blank-token-hash")]
    public async Task Malformed_live_deliveries_fail_with_identity_conflict_without_writing(
        string handlerName,
        string scenario)
    {
        var store = new ScriptedReadModelStore();
        string expectedState = SeedExistingState(store, handlerName);
        ProjectionRequest request = MalformedRequest(handlerName, scenario);

        DomainProjectionHandlerResult result = await ProjectAsync(
            handlerName,
            store,
            request,
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        ReadExistingStateSnapshot(store, handlerName).ShouldBe(expectedState);
        store.TrySaveCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("catalog", "get")]
    [InlineData("catalog", "save")]
    [InlineData("index", "get")]
    [InlineData("index", "save")]
    public async Task Live_writer_uncancelled_store_failures_are_retryable_without_changing_state(
        string handlerName,
        string operation)
    {
        var store = new ScriptedReadModelStore();
        string expectedState = SeedExistingState(store, handlerName);
        if (operation == "get")
        {
            store.GetException = new HttpRequestException("Transport failed.");
        }
        else
        {
            store.TrySaveException = new JsonException("Save serialization failed.");
        }

        DomainProjectionHandlerResult result = await ProjectAsync(
            handlerName,
            store,
            ValidRequest(handlerName),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryStateUnavailable);
        ReadExistingStateSnapshot(store, handlerName).ShouldBe(expectedState);
        store.TrySaveCount.ShouldBe(operation == "save" ? 1 : 0);
    }

    [Theory]
    [InlineData("catalog", "get")]
    [InlineData("catalog", "save")]
    [InlineData("index", "get")]
    [InlineData("index", "save")]
    public async Task Live_writer_uncancelled_cancellation_exceptions_are_retryable_without_changing_state(
        string handlerName,
        string operation)
    {
        var store = new ScriptedReadModelStore();
        string expectedState = SeedExistingState(store, handlerName);
        if (operation == "get")
        {
            store.GetException = new OperationCanceledException("Store read timed out.");
        }
        else
        {
            store.TrySaveException = new OperationCanceledException("Store write timed out.");
        }

        DomainProjectionHandlerResult result = await ProjectAsync(
            handlerName,
            store,
            ValidRequest(handlerName),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryStateUnavailable);
        ReadExistingStateSnapshot(store, handlerName).ShouldBe(expectedState);
        store.TrySaveCount.ShouldBe(operation == "save" ? 1 : 0);
    }

    [Theory]
    [InlineData("catalog", "get")]
    [InlineData("catalog", "save")]
    [InlineData("index", "get")]
    [InlineData("index", "save")]
    public async Task Live_writer_propagates_requested_cancellation_without_changing_state(
        string handlerName,
        string operation)
    {
        using var cancellation = new CancellationTokenSource();
        var store = new ScriptedReadModelStore
        {
            CancelBeforeGet = operation == "get" ? cancellation : null,
            CancelBeforeTrySave = operation == "save" ? cancellation : null
        };
        string expectedState = SeedExistingState(store, handlerName);

        _ = await Should.ThrowAsync<OperationCanceledException>(() => ProjectAsync(
            handlerName,
            store,
            ValidRequest(handlerName),
            cancellation.Token));

        ReadExistingStateSnapshot(store, handlerName).ShouldBe(expectedState);
        store.TrySaveCount.ShouldBe(operation == "save" ? 1 : 0);
    }

    [Fact]
    public async Task Index_rebuild_replaces_only_target_tenant_slice()
    {
        var store = new ScriptedReadModelStore();
        store.Set(
            MagicLinkTokenHashCapabilityIndexProjection.StateKey,
            new MagicLinkTokenHashCapabilityIndexReadModel(new Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry>
            {
                ["stale"] = new(new TenantReference("tenant-1"), new MagicLinkCapabilityId("stale-capability")),
                ["other"] = new(new TenantReference("tenant-2"), new MagicLinkCapabilityId("other-capability"))
            }));
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store);
        var identity = new DomainSharedProjectionRebuildIdentity(
            "tenant-1",
            "timesheets",
            MagicLinkTokenHashCapabilityIndexProjection.ProjectionName,
            "operation-1",
            "catalog-1");
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(
            identity,
            TestContext.Current.CancellationToken);
        candidate = await handler.AccumulateAsync(
            identity,
            candidate,
            new ProjectionRequest("tenant-1", "timesheets", "capability-1", [Event(1, Issued())]),
            TestContext.Current.CancellationToken);

        DomainProjectionRebuildPlan plan = await handler.FinalizeAsync(
            identity,
            candidate,
            TestContext.Current.CancellationToken);
        MagicLinkTokenHashCapabilityIndexReadModel rebuilt = JsonSerializer.Deserialize<MagicLinkTokenHashCapabilityIndexReadModel>(
            plan.Operations.ShouldHaveSingleItem().CanonicalValue.Span,
            s_jsonOptions).ShouldNotBeNull();

        rebuilt.Entries.Keys.ShouldBe(["hash-only", "other"], ignoreOrder: true);
        rebuilt.Entries["other"].Tenant.ShouldBe(new TenantReference("tenant-2"));
        rebuilt.Entries.ShouldNotContainKey("stale");
    }

    [Fact]
    public async Task Index_rebuild_drops_a_hash_another_tenant_already_owns()
    {
        // ReplaceTenant removes an entry whose hash collides with another tenant's persisted slice
        // rather than letting either side win. Index_rebuild_replaces_only_target_tenant_slice never
        // reaches that branch: its replacement key does not collide, so TryAdd always succeeds and
        // the collision handling could be deleted with every assertion still green. Shipped, that
        // would let a tenant-1 rebuild hijack — or be shadowed by — tenant-2's hash mapping, and the
        // loader would resolve a candidate in the wrong tenant's stream.
        var store = new ScriptedReadModelStore();
        store.Set(
            MagicLinkTokenHashCapabilityIndexProjection.StateKey,
            new MagicLinkTokenHashCapabilityIndexReadModel(new Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry>
            {
                ["hash-only"] = new(new TenantReference("tenant-2"), new MagicLinkCapabilityId("other-capability"))
            }));
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store);
        var identity = new DomainSharedProjectionRebuildIdentity(
            "tenant-1",
            "timesheets",
            MagicLinkTokenHashCapabilityIndexProjection.ProjectionName,
            "operation-1",
            "catalog-1");
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(
            identity,
            TestContext.Current.CancellationToken);
        candidate = await handler.AccumulateAsync(
            identity,
            candidate,
            new ProjectionRequest("tenant-1", "timesheets", "capability-1", [Event(1, Issued())]),
            TestContext.Current.CancellationToken);

        DomainProjectionRebuildPlan plan = await handler.FinalizeAsync(
            identity,
            candidate,
            TestContext.Current.CancellationToken);
        MagicLinkTokenHashCapabilityIndexReadModel rebuilt = JsonSerializer.Deserialize<MagicLinkTokenHashCapabilityIndexReadModel>(
            plan.Operations.ShouldHaveSingleItem().CanonicalValue.Span,
            s_jsonOptions).ShouldNotBeNull();

        rebuilt.Entries.ShouldNotContainKey("hash-only");
    }

    [Fact]
    public async Task Index_rebuild_rejects_a_candidate_carrying_another_tenants_entry()
    {
        // ToLoaderVisibleIndex is the last tenant-isolation check before the shared index write, and
        // no test could reach it: every candidate in this file comes from AccumulateAsync, which
        // already forces identity.TenantId == aggregateHistory.TenantId. A candidate blob that
        // crossed the rebuild coordinator's persistence boundary carrying a foreign entry would be
        // merged in, and ReplaceTenant only strips entries belonging to the rebuild tenant — so the
        // injected one would survive. The candidate is built from raw bytes because
        // IndexRebuildCandidate is internal.
        var store = new ScriptedReadModelStore();
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store);
        var identity = new DomainSharedProjectionRebuildIdentity(
            "tenant-1",
            "timesheets",
            MagicLinkTokenHashCapabilityIndexProjection.ProjectionName,
            "operation-1",
            "catalog-1");
        DomainSharedProjectionRebuildCandidate foreign = new(
            System.Text.Encoding.UTF8.GetBytes(
                """
                {"entries":[{"tokenHash":"hash-only","candidate":{"tenant":{"tenantId":"tenant-2"},"capabilityId":{"value":"capability-2"}}}]}
                """));

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await handler.FinalizeAsync(
            identity,
            foreign,
            TestContext.Current.CancellationToken));
        store.Contains(MagicLinkTokenHashCapabilityIndexProjection.StateKey).ShouldBeFalse();
    }

    [Fact]
    public async Task Index_rebuild_omits_ambiguous_hash_deterministically_in_both_delivery_orders()
    {
        var store = new ScriptedReadModelStore();
        var handler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(store);
        var identity = new DomainSharedProjectionRebuildIdentity(
            "tenant-1",
            "timesheets",
            MagicLinkTokenHashCapabilityIndexProjection.ProjectionName,
            "operation-1",
            "catalog-1");
        ProjectionRequest firstHistory = new(
            "tenant-1",
            "timesheets",
            "capability-1",
            [Event(1, Issued(new MagicLinkCapabilityId("capability-1")))]);
        ProjectionRequest secondHistory = new(
            "tenant-1",
            "timesheets",
            "capability-2",
            [Event(1, Issued(new MagicLinkCapabilityId("capability-2")))]);

        async Task<(DomainProjectionRebuildPlan Plan, string CandidateJson)> RebuildAsync(
            params ProjectionRequest[] histories)
        {
            DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(
                identity,
                TestContext.Current.CancellationToken);
            foreach (ProjectionRequest history in histories)
            {
                candidate = await handler.AccumulateAsync(
                    identity,
                    candidate,
                    history,
                    TestContext.Current.CancellationToken);
            }

            DomainProjectionRebuildPlan plan = await handler.FinalizeAsync(
                identity,
                candidate,
                TestContext.Current.CancellationToken);
            return (plan, System.Text.Encoding.UTF8.GetString(candidate.State.Span));
        }

        (DomainProjectionRebuildPlan first, string candidateJson) = await RebuildAsync(firstHistory, secondHistory);
        (DomainProjectionRebuildPlan second, _) = await RebuildAsync(secondHistory, firstHistory);

        first.Operations.ShouldHaveSingleItem().CanonicalValue.Span.SequenceEqual(
            second.Operations.ShouldHaveSingleItem().CanonicalValue.Span).ShouldBeTrue();
        MagicLinkTokenHashCapabilityIndexReadModel rebuilt = JsonSerializer.Deserialize<MagicLinkTokenHashCapabilityIndexReadModel>(
            first.Operations[0].CanonicalValue.Span,
            s_jsonOptions).ShouldNotBeNull();
        rebuilt.Entries.ShouldNotContainKey("hash-only");
        candidateJson.ShouldContain("hash-only");
        candidateJson.ShouldContain("tenant-1");
        candidateJson.ShouldContain("capability-1");
        candidateJson.ShouldContain("capability-2");
        candidateJson.ShouldNotContain("party-1");
        candidateJson.ShouldNotContain("project-1");
        candidateJson.ShouldNotContain("time-entry-1");
        candidateJson.ShouldNotContain("issue-1");
    }

    [Fact]
    public async Task Catalog_first_complete_live_delivery_is_promoted_to_fresh()
    {
        var store = new ScriptedReadModelStore();
        var handler = new TenantActivityTypeCatalogProjectionHandler(store);
        var request = new ProjectionRequest(
            "tenant-1",
            "timesheets",
            "activity-1",
            [Event(2, new ActivityTypeRenamed(new ActivityTypeId("activity-1"), "Renamed")), Event(1, ActivityCreated())]);

        DomainProjectionHandlerResult delivery = await handler.ProjectAsync(
            request,
            "dispatch-1",
            TestContext.Current.CancellationToken);
        ActivityTypeCatalogReadModel live = store.Get<ActivityTypeCatalogReadModel>(
            MagicLinkActivityTypeCatalogReadModelAddress.StateKey(new TenantReference("tenant-1")));

        delivery.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        live.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        live.ProjectionFreshness.Cursor.ShouldBe("2");
        live.Items.ShouldHaveSingleItem().Label.ShouldBe("Renamed");
    }

    [Fact]
    public async Task Catalog_sibling_live_delivery_preserves_an_already_fresh_catalog()
    {
        var store = new ScriptedReadModelStore();
        var handler = new TenantActivityTypeCatalogProjectionHandler(store);
        string key = MagicLinkActivityTypeCatalogReadModelAddress.StateKey(new TenantReference("tenant-1"));
        store.Set(
            key,
            new ActivityTypeCatalogReadModel(
                [
                    new ActivityTypeCatalogItem(
                        new ActivityTypeId("activity-1"),
                        ActivityTypeScope.Tenant,
                        null,
                        "Delivery",
                        true,
                        BillableState.Billable)
                ],
                new ProjectionFreshnessMetadata(ProjectionFreshnessState.Fresh, "10", null, null)));
        ProjectionEventDto siblingCreated = Event(
            1,
            ActivityCreated(new ActivityTypeId("activity-2"), "Research")) with
        {
            GlobalPosition = 11
        };

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            new ProjectionRequest(
                "tenant-1",
                "timesheets",
                "activity-2",
                [siblingCreated]),
            "dispatch-activity-2",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        ActivityTypeCatalogReadModel catalog = store.Get<ActivityTypeCatalogReadModel>(key);
        catalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        catalog.ProjectionFreshness.Cursor.ShouldBe("11");
        catalog.Items.Select(static item => item.ActivityTypeId.Value)
            .ShouldBe(["activity-1", "activity-2"], ignoreOrder: true);
    }

    [Fact]
    public async Task Catalog_complete_live_delivery_preserves_an_existing_stale_catalog()
    {
        var store = new ScriptedReadModelStore();
        var handler = new TenantActivityTypeCatalogProjectionHandler(store);
        string key = MagicLinkActivityTypeCatalogReadModelAddress.StateKey(new TenantReference("tenant-1"));
        store.Set(
            key,
            new ActivityTypeCatalogReadModel(
                [
                    new ActivityTypeCatalogItem(
                        new ActivityTypeId("activity-1"),
                        ActivityTypeScope.Tenant,
                        null,
                        "Delivery",
                        true,
                        BillableState.Billable)
                ],
                new ProjectionFreshnessMetadata(ProjectionFreshnessState.Stale, "10", null, null)));
        ProjectionEventDto siblingCreated = Event(
            1,
            ActivityCreated(new ActivityTypeId("activity-2"), "Research")) with
        {
            GlobalPosition = 11
        };

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            new ProjectionRequest(
                "tenant-1",
                "timesheets",
                "activity-2",
                [siblingCreated]),
            "dispatch-activity-2",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        ActivityTypeCatalogReadModel catalog = store.Get<ActivityTypeCatalogReadModel>(key);

        // One aggregate's complete history says nothing about whether the tenant catalog is
        // complete, so a catalog the writer marked Stale stays Stale. Only FinalizeAsync, which
        // replays the whole tenant, may promote it. The delivered item and cursor still land.
        catalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Stale);
        catalog.ProjectionFreshness.Cursor.ShouldBe("11");
        catalog.Items.Select(static item => item.ActivityTypeId.Value)
            .ShouldBe(["activity-1", "activity-2"], ignoreOrder: true);
    }

    [Theory]
    [InlineData("missing-sequence")]
    [InlineData("missing-creation")]
    [InlineData("identity-conflict")]
    [InlineData("malformed-json")]
    [InlineData("unsupported-serialization")]
    public async Task Catalog_incomplete_live_delivery_fails_without_changing_the_catalog(string scenario)
    {
        var store = new ScriptedReadModelStore();
        var handler = new TenantActivityTypeCatalogProjectionHandler(store);
        string key = MagicLinkActivityTypeCatalogReadModelAddress.StateKey(new TenantReference("tenant-1"));
        var existing = new ActivityTypeCatalogReadModel(
            [
                new ActivityTypeCatalogItem(
                    new ActivityTypeId("activity-existing"),
                    ActivityTypeScope.Tenant,
                    null,
                    "Existing",
                    true,
                    BillableState.Billable)
            ],
            new ProjectionFreshnessMetadata(ProjectionFreshnessState.Fresh, "8", null, null));
        store.Set(key, existing);
        ProjectionEventDto[] events = scenario switch
        {
            "missing-sequence" =>
            [
                Event(1, ActivityCreated()),
                Event(3, new ActivityTypeRenamed(new ActivityTypeId("activity-1"), "Renamed"))
            ],
            "missing-creation" =>
            [Event(1, new ActivityTypeRenamed(new ActivityTypeId("activity-1"), "Renamed"))],
            "identity-conflict" =>
            [Event(1, ActivityCreated(new ActivityTypeId("activity-2")))],
            "malformed-json" =>
            [
                Event(1, ActivityCreated()),
                Event(2, new ActivityTypeRenamed(new ActivityTypeId("activity-1"), "Renamed")) with
                {
                    Payload = "{"u8.ToArray()
                }
            ],
            "unsupported-serialization" =>
            [
                Event(1, ActivityCreated()),
                Event(2, new ActivityTypeRenamed(new ActivityTypeId("activity-1"), "Renamed")) with
                {
                    SerializationFormat = "application/octet-stream"
                }
            ],
            _ => throw new InvalidOperationException($"Unknown incomplete-history scenario '{scenario}'.")
        };

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            new ProjectionRequest("tenant-1", "timesheets", "activity-1", events),
            "dispatch-gap",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        store.Get<ActivityTypeCatalogReadModel>(key).ShouldBe(existing);
        store.TrySaveCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(42, 7)]
    [InlineData(7, 42)]
    public async Task Catalog_rebuild_finalize_uses_the_greater_cursor(long persistedCursor, long candidateCursor)
    {
        var store = new ScriptedReadModelStore();
        var handler = new TenantActivityTypeCatalogProjectionHandler(store);
        string key = MagicLinkActivityTypeCatalogReadModelAddress.StateKey(new TenantReference("tenant-1"));
        store.Set(
            key,
            new ActivityTypeCatalogReadModel(
                [],
                new ProjectionFreshnessMetadata(
                    ProjectionFreshnessState.Fresh,
                    persistedCursor.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    null,
                    null)));
        var identity = new DomainSharedProjectionRebuildIdentity(
            "tenant-1",
            "timesheets",
            TenantActivityTypeCatalogProjection.ProjectionName,
            "operation-1",
            "catalog-1");
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(
            identity,
            TestContext.Current.CancellationToken);
        ProjectionEventDto created = Event(1, ActivityCreated()) with { GlobalPosition = candidateCursor };
        candidate = await handler.AccumulateAsync(
            identity,
            candidate,
            new ProjectionRequest("tenant-1", "timesheets", "activity-1", [created]),
            TestContext.Current.CancellationToken);

        DomainProjectionRebuildPlan plan = await handler.FinalizeAsync(
            identity,
            candidate,
            TestContext.Current.CancellationToken);
        ActivityTypeCatalogReadModel rebuilt = JsonSerializer.Deserialize<ActivityTypeCatalogReadModel>(
            plan.Operations.ShouldHaveSingleItem().CanonicalValue.Span,
            s_jsonOptions).ShouldNotBeNull();

        rebuilt.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        rebuilt.ProjectionFreshness.Cursor.ShouldBe("42");
        rebuilt.Items.ShouldHaveSingleItem().ActivityTypeId.ShouldBe(new ActivityTypeId("activity-1"));
    }

    [Fact]
    public async Task Live_writers_retry_optimistic_concurrency_and_report_exhaustion()
    {
        var recoveredStore = new ScriptedReadModelStore { ConflictsRemaining = 2 };
        var recoveredHandler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(recoveredStore);
        ProjectionRequest request = new("tenant-1", "timesheets", "capability-1", [Event(1, Issued())]);

        DomainProjectionHandlerResult recovered = await recoveredHandler.ProjectAsync(
            request,
            "dispatch-1",
            TestContext.Current.CancellationToken);

        recovered.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        recoveredStore.TrySaveCount.ShouldBe(3);

        var exhaustedStore = new ScriptedReadModelStore { ConflictsRemaining = 3 };
        var exhaustedHandler = new MagicLinkTokenHashCapabilityIndexProjectionHandler(exhaustedStore);
        DomainProjectionHandlerResult exhausted = await exhaustedHandler.ProjectAsync(
            request,
            "dispatch-2",
            TestContext.Current.CancellationToken);

        exhausted.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        exhausted.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryStateUnavailable);
    }

    [Theory]
    [InlineData("catalog", "absent")]
    [InlineData("catalog", "etag")]
    [InlineData("catalog", "unreadable-value")]
    [InlineData("catalog", "null-etag")]
    [InlineData("catalog", "empty-etag")]
    [InlineData("index", "absent")]
    [InlineData("index", "etag")]
    [InlineData("index", "unreadable-value")]
    [InlineData("index", "null-etag")]
    [InlineData("index", "empty-etag")]
    public async Task Rebuild_finalize_chooses_write_concurrency_from_row_existence_not_from_the_value(
        string handlerName,
        string rowState)
    {
        // CreateOnly is accepted only while the key is absent, so selecting it for a row that already
        // exists strands the rebuild write permanently. Each row state needs its own arm, and neither
        // half of the entry alone identifies existence: an ETag-only ternary collapses "exists without
        // an ETag" onto the create-only arm, while a value-only one collapses "exists but its bytes
        // materialize as null" onto it. No assertion on the plan's canonical value alone sees either.
        var store = new ScriptedReadModelStore();
        string key = RebuildStateKey(handlerName);
        if (rowState == "unreadable-value")
        {
            // The shipped stores return an existing row as Deserialize<TValue>(bytes) paired with its
            // ETag, so an empty, JSON-null or otherwise-shaped payload yields a null value under a live
            // ETag. The key - and its ETag - are still there, so the plan must heal the row.
            store.Set(key, new UnreadableRow());
        }
        else if (rowState != "absent")
        {
            _ = SeedExistingState(store, handlerName);
        }

        store.SuppressETag = rowState is "null-etag" or "empty-etag";
        store.ETagWhenSuppressed = rowState == "empty-etag" ? string.Empty : null;
        ReadModelBatchConcurrency expected = rowState switch
        {
            "absent" => ReadModelBatchConcurrency.CreateOnly,
            "etag" or "unreadable-value" => ReadModelBatchConcurrency.Match(store.ETagFor(key)),
            _ => ReadModelBatchConcurrency.LastWrite
        };

        DomainProjectionRebuildPlan plan = await RebuildAsync(
            handlerName,
            store,
            TestContext.Current.CancellationToken);

        plan.StoreName.ShouldBe(RebuildStoreName(handlerName));
        ReadModelBatchOperation operation = plan.Operations.ShouldHaveSingleItem();
        operation.Key.ShouldBe(key);
        operation.Kind.ShouldBe(ReadModelBatchOperationKind.Write);
        operation.Concurrency.ShouldBe(expected, $"{handlerName}/{rowState}");
        store.TrySaveCount.ShouldBe(0);
    }

    [Fact]
    public void Both_projection_handlers_are_discovered_as_the_timesheets_telemetry_domain()
    {
        // Mirrors the SDK's handler-domain scan: an [EventStoreDomain] attribute short-circuits it,
        // and without one the type must expose a parameterless constructor to be instantiated and
        // asked. Both handlers take an injected read-model store, so dropping the attribute makes them
        // silently invisible and the host registers no Timesheets domain telemetry.
        Type[] handlerTypes = typeof(MagicLinkTokenHashCapabilityIndexProjectionHandler).Assembly
            .GetTypes()
            .Where(static type => type is { IsClass: true, IsAbstract: false }
                && typeof(IAsyncDomainProjectionHandler).IsAssignableFrom(type))
            .ToArray();

        handlerTypes.ShouldContain(typeof(MagicLinkTokenHashCapabilityIndexProjectionHandler));
        handlerTypes.ShouldContain(typeof(TenantActivityTypeCatalogProjectionHandler));
        foreach (Type handlerType in handlerTypes)
        {
            DiscoverDomainName(handlerType).ShouldBe("timesheets", handlerType.FullName);
        }
    }

    [Fact]
    public async Task Catalog_live_delivery_onto_a_null_persisted_freshness_fails_without_writing()
    {
        var store = new ScriptedReadModelStore();
        var handler = new TenantActivityTypeCatalogProjectionHandler(store);
        string key = MagicLinkActivityTypeCatalogReadModelAddress.StateKey(new TenantReference("tenant-1"));
        var persisted = new ActivityTypeCatalogReadModel([], null!);
        store.Set(key, persisted);

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            ValidRequest("catalog"),
            "dispatch-null-freshness",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.HandlerFailure);
        store.Get<ActivityTypeCatalogReadModel>(key).ShouldBeSameAs(persisted);
        store.TrySaveCount.ShouldBe(0);
    }

    [Fact]
    public async Task Catalog_rebuild_accumulate_maps_a_null_candidate_freshness_to_a_declared_fold_failure()
    {
        var store = new ScriptedReadModelStore();
        var handler = new TenantActivityTypeCatalogProjectionHandler(store);
        DomainSharedProjectionRebuildIdentity identity = RebuildIdentity("catalog");

        ProjectionFoldException thrown = await Should.ThrowAsync<ProjectionFoldException>(async () =>
            await handler.AccumulateAsync(
                identity,
                NullFreshnessCandidate(),
                new ProjectionRequest("tenant-1", "timesheets", "activity-1", [Event(1, ActivityCreated())]),
                TestContext.Current.CancellationToken));

        thrown.InnerException.ShouldBeOfType<NullReferenceException>();
        store.TrySaveCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("persisted")]
    [InlineData("candidate")]
    public async Task Catalog_rebuild_finalize_maps_a_null_freshness_to_a_declared_fold_failure(string source)
    {
        // A persisted read model or a persisted rebuild candidate can deserialize with a JSON-null
        // ProjectionFreshness. Both cursor reads dereference it, so without the guarded mapping the
        // rebuild fails with an unhandled NullReferenceException instead of this handler's declared
        // deterministic fold failure.
        var store = new ScriptedReadModelStore();
        var handler = new TenantActivityTypeCatalogProjectionHandler(store);
        string key = MagicLinkActivityTypeCatalogReadModelAddress.StateKey(new TenantReference("tenant-1"));
        DomainSharedProjectionRebuildIdentity identity = RebuildIdentity("catalog");
        DomainSharedProjectionRebuildCandidate candidate;
        if (source == "persisted")
        {
            store.Set(key, new ActivityTypeCatalogReadModel([], null!));
            candidate = await handler.CreateEmptyCandidateAsync(identity, TestContext.Current.CancellationToken);
            candidate = await handler.AccumulateAsync(
                identity,
                candidate,
                new ProjectionRequest("tenant-1", "timesheets", "activity-1", [Event(1, ActivityCreated())]),
                TestContext.Current.CancellationToken);
        }
        else
        {
            candidate = NullFreshnessCandidate();
        }

        ProjectionFoldException thrown = await Should.ThrowAsync<ProjectionFoldException>(async () =>
            await handler.FinalizeAsync(identity, candidate, TestContext.Current.CancellationToken));

        thrown.InnerException.ShouldBeOfType<NullReferenceException>();
        store.TrySaveCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Projection_event_reader_treats_an_absent_event_type_name_as_an_unknown_event(string? eventTypeName)
    {
        // ProjectionEventDto declares the member non-nullable, but a wire payload carrying JSON null
        // deserializes it to null anyway. Only call ordering - Normalize running first - keeps the
        // reader safe today, and nothing in the type enforces that ordering.
        ProjectionEventDto projectionEvent = Event(1, ActivityCreated()) with
        {
            EventTypeName = eventTypeName!
        };

        ProjectionEventReader.Deserialize<ActivityTypeCreated>(projectionEvent).ShouldBeNull();
    }

    private static ProjectionEventDto Event(long sequence, object payload)
        => new(
            payload.GetType().FullName!,
            JsonSerializer.SerializeToUtf8Bytes(payload, s_jsonOptions),
            "json",
            sequence,
            new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero),
            "correlation-1",
            $"message-{sequence}",
            "operator-1",
            sequence);

    private static ProjectionRequest MalformedRequest(string handlerName, string scenario)
    {
        string aggregateId = handlerName == "catalog" ? "activity-1" : "capability-1";
        if (scenario == "null-events")
        {
            return new ProjectionRequest("tenant-1", "timesheets", aggregateId, null!);
        }

        object payload = handlerName == "catalog" ? ActivityCreated() : Issued();
        if (scenario == "null-event")
        {
            return new ProjectionRequest("tenant-1", "timesheets", aggregateId, [null!]);
        }

        if (scenario is "null-event-type" or "blank-event-type")
        {
            ProjectionEventDto projectionEvent = Event(1, payload) with
            {
                EventTypeName = scenario == "null-event-type" ? null! : " "
            };
            return new ProjectionRequest("tenant-1", "timesheets", aggregateId, [projectionEvent]);
        }

        (string propertyName, string valuePropertyName) = scenario switch
        {
            "missing-activity-type" or "blank-activity-type" => ("activityTypeId", "value"),
            "missing-tenant" or "blank-tenant" => ("tenant", "tenantId"),
            "missing-capability" or "blank-capability" => ("capabilityId", "value"),
            "missing-token-hash" or "blank-token-hash" => ("tokenHash", "value"),
            _ => throw new InvalidOperationException($"Unknown malformed-delivery scenario '{scenario}'.")
        };
        JsonObject json = JsonSerializer.SerializeToNode(payload, s_jsonOptions)
            .ShouldNotBeNull()
            .AsObject();
        if (scenario.StartsWith("missing-", StringComparison.Ordinal))
        {
            _ = json.Remove(propertyName);
        }
        else
        {
            json[propertyName] = new JsonObject { [valuePropertyName] = " " };
        }

        return new ProjectionRequest(
            "tenant-1",
            "timesheets",
            aggregateId,
            [Event(1, payload) with { Payload = JsonSerializer.SerializeToUtf8Bytes(json, s_jsonOptions) }]);
    }

    private static ProjectionRequest ValidRequest(string handlerName)
        => handlerName == "catalog"
            ? new ProjectionRequest("tenant-1", "timesheets", "activity-1", [Event(1, ActivityCreated())])
            : new ProjectionRequest("tenant-1", "timesheets", "capability-1", [Event(1, Issued())]);

    private static Task<DomainProjectionHandlerResult> ProjectAsync(
        string handlerName,
        IReadModelStore store,
        ProjectionRequest request,
        CancellationToken cancellationToken)
        => handlerName == "catalog"
            ? new TenantActivityTypeCatalogProjectionHandler(store).ProjectAsync(request, "dispatch-catalog", cancellationToken)
            : new MagicLinkTokenHashCapabilityIndexProjectionHandler(store).ProjectAsync(request, "dispatch-index", cancellationToken);

    private static string SeedExistingState(ScriptedReadModelStore store, string handlerName)
    {
        if (handlerName == "catalog")
        {
            var existing = new ActivityTypeCatalogReadModel(
                [new ActivityTypeCatalogItem(
                    new ActivityTypeId("activity-existing"),
                    ActivityTypeScope.Tenant,
                    null,
                    "Existing",
                    true,
                    BillableState.Billable)],
                new ProjectionFreshnessMetadata(ProjectionFreshnessState.Fresh, "8", null, null));
            store.Set(MagicLinkActivityTypeCatalogReadModelAddress.StateKey(new TenantReference("tenant-1")), existing);
            return Snapshot(existing);
        }

        var index = new MagicLinkTokenHashCapabilityIndexReadModel(
            new Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry>(StringComparer.Ordinal)
            {
                ["existing-hash"] = new(
                    new TenantReference("tenant-1"),
                    new MagicLinkCapabilityId("existing-capability"))
            });
        store.Set(MagicLinkTokenHashCapabilityIndexProjection.StateKey, index);
        return Snapshot(index);
    }

    private static string ReadExistingStateSnapshot(ScriptedReadModelStore store, string handlerName)
        => Snapshot(handlerName == "catalog"
            ? store.Get<ActivityTypeCatalogReadModel>(
                MagicLinkActivityTypeCatalogReadModelAddress.StateKey(new TenantReference("tenant-1")))
            : store.Get<MagicLinkTokenHashCapabilityIndexReadModel>(MagicLinkTokenHashCapabilityIndexProjection.StateKey));

    private static string Snapshot(object value)
        => JsonSerializer.Serialize(value, value.GetType(), s_jsonOptions);

    /// <summary>A persisted row that materializes as null for every read-model type under test.</summary>
    private sealed record UnreadableRow;

    private static string? DiscoverDomainName(Type handlerType)
    {
        EventStoreDomainAttribute? attribute = handlerType.GetCustomAttribute<EventStoreDomainAttribute>();
        return attribute is not null
            ? attribute.DomainName
            : handlerType.GetConstructor(Type.EmptyTypes) is null
                ? null
                : (Activator.CreateInstance(handlerType) as IAsyncDomainProjectionHandler)?.Domain;
    }

    private static DomainSharedProjectionRebuildIdentity RebuildIdentity(string handlerName)
        => new(
            "tenant-1",
            "timesheets",
            handlerName == "catalog"
                ? TenantActivityTypeCatalogProjection.ProjectionName
                : MagicLinkTokenHashCapabilityIndexProjection.ProjectionName,
            "operation-1",
            "catalog-1");

    private static string RebuildStateKey(string handlerName)
        => handlerName == "catalog"
            ? MagicLinkActivityTypeCatalogReadModelAddress.StateKey(new TenantReference("tenant-1"))
            : MagicLinkTokenHashCapabilityIndexProjection.StateKey;

    private static string RebuildStoreName(string handlerName)
        => handlerName == "catalog"
            ? MagicLinkActivityTypeCatalogReadModelAddress.StateStoreName
            : MagicLinkTokenHashCapabilityIndexProjection.StateStoreName;

    private static DomainSharedProjectionRebuildCandidate NullFreshnessCandidate()
        => new(System.Text.Encoding.UTF8.GetBytes("""
            {"items":[],"projectionFreshness":null}
            """));

    private static async Task<DomainProjectionRebuildPlan> RebuildAsync(
        string handlerName,
        ScriptedReadModelStore store,
        CancellationToken cancellationToken)
    {
        DomainSharedProjectionRebuildIdentity identity = RebuildIdentity(handlerName);
        IAsyncDomainSharedProjectionRebuildHandler handler = handlerName == "catalog"
            ? new TenantActivityTypeCatalogProjectionHandler(store)
            : new MagicLinkTokenHashCapabilityIndexProjectionHandler(store);
        ProjectionRequest history = handlerName == "catalog"
            ? new ProjectionRequest("tenant-1", "timesheets", "activity-1", [Event(1, ActivityCreated())])
            : new ProjectionRequest("tenant-1", "timesheets", "capability-1", [Event(1, Issued())]);
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(
            identity,
            cancellationToken);
        candidate = await handler.AccumulateAsync(identity, candidate, history, cancellationToken);
        return await handler.FinalizeAsync(identity, candidate, cancellationToken);
    }

    private static MagicLinkConfirmationCapabilityIssued Issued(MagicLinkCapabilityId? capabilityId = null)
        => new(
            capabilityId ?? new MagicLinkCapabilityId("capability-1"),
            new TenantReference("tenant-1"),
            new PartyReference("party-1"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            new ActivityTypeId("activity-1"),
            new TimeEntryId("time-entry-1"),
            MagicLinkTargetKind.ProposedTimeEntry,
            MagicLinkAllowedAction.ConfirmOrAdjust,
            new MagicLinkTokenHash("hash-only"),
            new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.Zero),
            new PartyReference("operator-1"),
            new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero),
            new MagicLinkAuditMetadata("timesheets", "issue-1"),
            true);

    private static ActivityTypeCreated ActivityCreated(ActivityTypeId? activityTypeId = null, string label = "Delivery")
        => new(
            activityTypeId ?? new ActivityTypeId("activity-1"),
            ActivityTypeScope.Tenant,
            null,
            label,
            BillableState.Billable);

    private sealed class ScriptedReadModelStore : IReadModelStore
    {
        private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

        // Per key, not one global counter: a shared version would let a plan that read some other
        // key's ETag still satisfy an expected-ETag assertion, so the match case would not be real.
        private readonly Dictionary<string, int> _versions = new(StringComparer.Ordinal);

        public int ConflictsRemaining { get; set; }

        public CancellationTokenSource? CancelBeforeGet { get; set; }

        public CancellationTokenSource? CancelBeforeTrySave { get; set; }

        public Exception? GetException { get; set; }

        public Exception? TrySaveException { get; set; }

        public int TrySaveCount { get; private set; }

        /// <summary>
        /// When set, a key the store holds is read back with <see cref="ETagWhenSuppressed"/> instead
        /// of its per-key version, modelling a store that returns an existing row without an ETag.
        /// </summary>
        public bool SuppressETag { get; set; }

        /// <summary>The ETag an existing row is read back with while <see cref="SuppressETag"/> is set.</summary>
        public string? ETagWhenSuppressed { get; set; }

        public T Get<T>(string key)
            where T : class
            => (T)_values[key];

        public bool Contains(string key) => _values.ContainsKey(key);

        public string ETagFor(string key)
            => $"{key}#{_versions[key].ToString(System.Globalization.CultureInfo.InvariantCulture)}";

        public void Set<T>(string key, T value)
            where T : class
            => Commit(key, value);

        public Task<ReadModelEntry<TValue>> GetAsync<TValue>(
            string storeName,
            string key,
            CancellationToken cancellationToken = default)
            where TValue : class
        {
            if (CancelBeforeGet is not null)
            {
                CancelBeforeGet.Cancel();
                return Task.FromCanceled<ReadModelEntry<TValue>>(cancellationToken);
            }

            if (GetException is not null)
            {
                return Task.FromException<ReadModelEntry<TValue>>(GetException);
            }

            if (!_values.TryGetValue(key, out object? value))
            {
                return Task.FromResult(new ReadModelEntry<TValue>(null, null));
            }

            return Task.FromResult(new ReadModelEntry<TValue>(
                value as TValue,
                SuppressETag ? ETagWhenSuppressed : ETagFor(key)));
        }

        public Task SaveAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            CancellationToken cancellationToken = default)
            where TValue : class
        {
            Commit(key, value);
            return Task.CompletedTask;
        }

        public Task<bool> TrySaveAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            string etag,
            CancellationToken cancellationToken = default)
            where TValue : class
        {
            TrySaveCount++;
            if (CancelBeforeTrySave is not null)
            {
                CancelBeforeTrySave.Cancel();
                return Task.FromCanceled<bool>(cancellationToken);
            }

            if (TrySaveException is not null)
            {
                return Task.FromException<bool>(TrySaveException);
            }

            if (ConflictsRemaining-- > 0)
            {
                return Task.FromResult(false);
            }

            Commit(key, value);
            return Task.FromResult(true);
        }

        private void Commit(string key, object value)
        {
            _values[key] = value;
            _versions[key] = _versions.TryGetValue(key, out int version) ? version + 1 : 1;
        }
    }
}
