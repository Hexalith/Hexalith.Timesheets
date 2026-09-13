using System.Text.Json;

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
    public async Task Catalog_live_delivery_is_not_fresh_until_deterministic_shared_rebuild_finishes()
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
        live.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Stale);
        live.Items.ShouldHaveSingleItem().Label.ShouldBe("Renamed");

        var identity = new DomainSharedProjectionRebuildIdentity(
            "tenant-1",
            "timesheets",
            TenantActivityTypeCatalogProjection.ProjectionName,
            "operation-1",
            "catalog-1");
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(
            identity,
            TestContext.Current.CancellationToken);
        candidate = await handler.AccumulateAsync(
            identity,
            candidate,
            request,
            TestContext.Current.CancellationToken);
        DomainProjectionRebuildPlan plan = await handler.FinalizeAsync(
            identity,
            candidate,
            TestContext.Current.CancellationToken);
        ActivityTypeCatalogReadModel rebuilt = JsonSerializer.Deserialize<ActivityTypeCatalogReadModel>(
            plan.Operations.ShouldHaveSingleItem().CanonicalValue.Span,
            s_jsonOptions).ShouldNotBeNull();

        rebuilt.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        rebuilt.Items.ShouldHaveSingleItem().Label.ShouldBe("Renamed");
    }

    [Fact]
    public async Task Catalog_live_merge_preserves_a_sibling_activity_type_aggregate()
    {
        var store = new ScriptedReadModelStore();
        var handler = new TenantActivityTypeCatalogProjectionHandler(store);

        DomainProjectionHandlerResult first = await handler.ProjectAsync(
            new ProjectionRequest(
                "tenant-1",
                "timesheets",
                "activity-1",
                [Event(1, ActivityCreated(new ActivityTypeId("activity-1"), "Delivery"))]),
            "dispatch-activity-1",
            TestContext.Current.CancellationToken);
        DomainProjectionHandlerResult second = await handler.ProjectAsync(
            new ProjectionRequest(
                "tenant-1",
                "timesheets",
                "activity-2",
                [Event(1, ActivityCreated(new ActivityTypeId("activity-2"), "Research"))]),
            "dispatch-activity-2",
            TestContext.Current.CancellationToken);

        first.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        second.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        ActivityTypeCatalogReadModel catalog = store.Get<ActivityTypeCatalogReadModel>(
            MagicLinkActivityTypeCatalogReadModelAddress.StateKey(new TenantReference("tenant-1")));
        catalog.Items.Select(static item => item.ActivityTypeId.Value)
            .ShouldBe(["activity-1", "activity-2"], ignoreOrder: true);
    }

    [Fact]
    public async Task Catalog_live_delivery_rejects_a_missing_aggregate_sequence()
    {
        var store = new ScriptedReadModelStore();
        var handler = new TenantActivityTypeCatalogProjectionHandler(store);

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            new ProjectionRequest(
                "tenant-1",
                "timesheets",
                "activity-1",
                [
                    Event(1, ActivityCreated()),
                    Event(3, new ActivityTypeRenamed(new ActivityTypeId("activity-1"), "Renamed"))
                ]),
            "dispatch-gap",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
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
        private int _version;

        public int ConflictsRemaining { get; set; }

        public int TrySaveCount { get; private set; }

        public T Get<T>(string key)
            where T : class
            => (T)_values[key];

        public void Set<T>(string key, T value)
            where T : class
            => _values[key] = value;

        public Task<ReadModelEntry<TValue>> GetAsync<TValue>(
            string storeName,
            string key,
            CancellationToken cancellationToken = default)
            where TValue : class
            => Task.FromResult(new ReadModelEntry<TValue>(
                _values.TryGetValue(key, out object? value) ? value as TValue : null,
                _values.ContainsKey(key) ? _version.ToString(System.Globalization.CultureInfo.InvariantCulture) : null));

        public Task SaveAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            CancellationToken cancellationToken = default)
            where TValue : class
        {
            _values[key] = value;
            _version++;
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
            if (ConflictsRemaining-- > 0)
            {
                return Task.FromResult(false);
            }

            _values[key] = value;
            _version++;
            return Task.FromResult(true);
        }
    }
}
