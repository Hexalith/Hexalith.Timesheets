using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Timesheets.Contracts.Events.ActivityTypes;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections.ActivityTypes;
using Hexalith.Timesheets.Projections.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks;

using Shouldly;

namespace Hexalith.Timesheets.Projections.Tests;

public sealed partial class MagicLinkStateProjectionHandlerTests
{
    /// <summary>Provides malformed serialized state for both rebuild stages and handlers.</summary>
    /// <returns>The handler, stage and candidate bytes represented as JSON text.</returns>
    public static TheoryData<string, string, string> MalformedRebuildCandidates()
    {
        var cases = new TheoryData<string, string, string>();
        string[] common = ["null", "{", "[]"];
        string[] index =
        [
            "{}", "{\"entries\":null}", "{\"entries\":[null]}",
            """{"entries":[{"tokenHash":null,"candidate":{"tenant":{"tenantId":"tenant-1"},"capabilityId":{"value":"capability-1"}}}]}""",
            """{"entries":[{"tokenHash":" ","candidate":{"tenant":{"tenantId":"tenant-1"},"capabilityId":{"value":"capability-1"}}}]}""",
            """{"entries":[{"tokenHash":"hash","candidate":null}]}""",
            """{"entries":[{"tokenHash":"hash","candidate":{"tenant":null,"capabilityId":{"value":"capability-1"}}}]}""",
            """{"entries":[{"tokenHash":"hash","candidate":{"tenant":{"tenantId":null},"capabilityId":{"value":"capability-1"}}}]}""",
            """{"entries":[{"tokenHash":"hash","candidate":{"tenant":{"tenantId":" "},"capabilityId":{"value":"capability-1"}}}]}""",
            """{"entries":[{"tokenHash":"hash","candidate":{"tenant":{"tenantId":"tenant-1"},"capabilityId":null}}]}""",
            """{"entries":[{"tokenHash":"hash","candidate":{"tenant":{"tenantId":"tenant-1"},"capabilityId":{"value":null}}}]}""",
            """{"entries":[{"tokenHash":"hash","candidate":{"tenant":{"tenantId":"tenant-1"},"capabilityId":{"value":" "}}}]}"""
        ];
        string[] catalog =
        [
            "{}", """{"items":null,"projectionFreshness":{"state":1,"cursor":"1"}}""",
            """{"items":[null],"projectionFreshness":{"state":1,"cursor":"1"}}""",
            """{"items":[{"activityTypeId":null}],"projectionFreshness":{"state":1,"cursor":"1"}}""",
            """{"items":[{"activityTypeId":{"value":" "}}],"projectionFreshness":{"state":1,"cursor":"1"}}""",
            """{"items":[],"projectionFreshness":null}"""
        ];
        foreach (string handler in new[] { "catalog", "index" })
        {
            foreach (string stage in new[] { "accumulate", "accumulate-empty", "finalize" })
            {
                foreach (string json in common.Concat(handler == "index" ? index : catalog))
                {
                    cases.Add(handler, stage, json);
                }
            }
        }

        return cases;
    }

    /// <summary>Rejects malformed persisted candidates without returning a write plan or changing state.</summary>
    /// <param name="handlerName">The shared projection under test.</param>
    /// <param name="stage">The rebuild stage receiving the candidate.</param>
    /// <param name="json">The persisted candidate JSON.</param>
    [Theory]
    [MemberData(nameof(MalformedRebuildCandidates))]
    public async Task MalformedRebuildCandidatesFailConsistentlyWithoutWriting(string handlerName, string stage, string json)
    {
        var store = new ScriptedReadModelStore();
        string before = SeedExistingState(store, handlerName);
        string etagBefore = store.ETagFor(RebuildStateKey(handlerName));
        IAsyncDomainSharedProjectionRebuildHandler handler = RebuildHandler(handlerName, store);
        var candidate = new DomainSharedProjectionRebuildCandidate(Encoding.UTF8.GetBytes(json));
        ProjectionRequest history = ValidRequest(handlerName);
        if (stage == "accumulate-empty") history = history with { Events = [] };

        _ = await Should.ThrowAsync<ProjectionFoldException>(async () =>
        {
            if (stage == "finalize")
            {
                _ = await handler.FinalizeAsync(RebuildIdentity(handlerName), candidate, TestContext.Current.CancellationToken);
            }
            else
            {
                _ = await handler.AccumulateAsync(RebuildIdentity(handlerName), candidate, history,
                    TestContext.Current.CancellationToken);
            }
        });

        ReadExistingStateSnapshot(store, handlerName).ShouldBe(before);
        store.ETagFor(RebuildStateKey(handlerName)).ShouldBe(etagBefore);
        store.TrySaveCount.ShouldBe(0);
    }

    /// <summary>Maps malformed persisted index merge fields into deterministic failures.</summary>
    /// <param name="json">The malformed existing index JSON.</param>
    [Theory]
    [InlineData("{\"entries\":null}")]
    [InlineData("{\"entries\":{},\"contributions\":null}")]
    [InlineData("{\"entries\":{\"hash\":null}}")]
    [InlineData("""{"entries":{"hash":{"tenant":null,"capabilityId":{"value":"id"}}}}""")]
    [InlineData("""{"entries":{"hash":{"tenant":{"tenantId":"tenant-2"},"capabilityId":null}}}""")]
    [InlineData("""{"entries":{},"contributions":{"hash":null}}""")]
    [InlineData("""{"entries":{},"contributions":{"hash":[null]}}""")]
    public async Task MalformedPersistedIndexFailsBeforeReturningARebuildPlan(string json)
    {
        var store = new ScriptedReadModelStore();
        var persisted = JsonSerializer.Deserialize<MagicLinkTokenHashCapabilityIndexReadModel>(json, s_jsonOptions)!;
        string key = RebuildStateKey("index");
        store.Set(key, persisted);
        string before = Snapshot(persisted);
        string etagBefore = store.ETagFor(key);

        _ = await Should.ThrowAsync<ProjectionFoldException>(() => RebuildAsync("index", store, TestContext.Current.CancellationToken));

        Snapshot(store.Get<MagicLinkTokenHashCapabilityIndexReadModel>(key)).ShouldBe(before);
        store.ETagFor(key).ShouldBe(etagBefore);
        store.TrySaveCount.ShouldBe(0);
    }

    /// <summary>Does not silently treat an existing catalog's null item collection as an empty catalog.</summary>
    [Fact]
    public async Task NullPersistedCatalogItemsFailLiveMergeWithoutWriting()
    {
        var store = new ScriptedReadModelStore();
        var model = new ActivityTypeCatalogReadModel(null!,
            new(ProjectionFreshnessState.Fresh, "1", null, null));
        string key = RebuildStateKey("catalog");
        store.Set(key, model);
        string etagBefore = store.ETagFor(key);

        DomainProjectionHandlerResult result = await ProjectAsync("catalog", store, ValidRequest("catalog"),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.HandlerFailure);
        store.Get<ActivityTypeCatalogReadModel>(key).ShouldBeSameAs(model);
        store.ETagFor(key).ShouldBe(etagBefore);
        store.TrySaveCount.ShouldBe(0);
    }

    /// <summary>Direct deserialization rejects recognized events with malformed wire fields.</summary>
    /// <param name="scenario">The malformed wire field.</param>
    [Theory]
    [InlineData("null-format")]
    [InlineData("blank-format")]
    [InlineData("null-payload")]
    [InlineData("empty-payload")]
    [InlineData("invalid-json")]
    [InlineData("json-null")]
    [InlineData("non-json")]
    public void RecognizedEventsRejectMalformedWireFields(string scenario)
    {
        ProjectionEventDto malformed = MalformedWireEvent(Event(1, ActivityCreated()), scenario);
        InvalidOperationException thrown = Should.Throw<InvalidOperationException>(() =>
            ProjectionEventReader.Deserialize<ActivityTypeCreated>(malformed));
        if (scenario is "null-format" or "blank-format" or "null-payload")
        {
            thrown.Message.ShouldBe("A recognized projection event envelope is malformed.");
            thrown.InnerException.ShouldBeNull();
        }
        else if (scenario == "non-json")
        {
            thrown.Message.ShouldBe("A recognized projection event does not use JSON serialization.");
        }
    }

    /// <summary>Rejects null envelope fields before duplicate equivalence, even for unknown event types.</summary>
    /// <param name="scenario">The malformed wire field.</param>
    /// <param name="unknown">Whether the event type is unknown to this module.</param>
    [Theory]
    [InlineData("null-format", false)]
    [InlineData("blank-format", false)]
    [InlineData("null-payload", false)]
    [InlineData("null-format", true)]
    [InlineData("blank-format", true)]
    [InlineData("null-payload", true)]
    public void NormalizationRejectsMalformedEnvelopesBeforeEquivalence(string scenario, bool unknown)
    {
        ProjectionEventDto malformed = MalformedWireEvent(Event(1, ActivityCreated()), scenario);
        if (unknown) malformed = malformed with { EventTypeName = "FutureEvent" };
        _ = Should.Throw<InvalidOperationException>(() => ProjectionEventReader.Normalize([malformed, malformed]));
        _ = Should.Throw<InvalidOperationException>(() => ProjectionEventReader.Normalize(
            [malformed, malformed with { Payload = [] }]));
    }

    /// <summary>Malformed wire fields reach the real handler's deterministic denial without changing persisted state.</summary>
    /// <param name="handlerName">The projection receiving the event.</param>
    [Theory]
    [InlineData("catalog")]
    [InlineData("index")]
    public async Task MalformedEventEnvelopesFailLiveDeliveryWithoutWriting(string handlerName)
    {
        foreach (string scenario in new[] { "null-format", "blank-format", "null-payload", "empty-payload", "invalid-json", "json-null", "non-json" })
        {
            var store = new ScriptedReadModelStore();
            string before = SeedExistingState(store, handlerName);
            string etagBefore = store.ETagFor(RebuildStateKey(handlerName));
            ProjectionRequest request = ValidRequest(handlerName);
            request = request with { Events = [MalformedWireEvent(request.Events[0], scenario)] };

            DomainProjectionHandlerResult result = await ProjectAsync(handlerName, store, request, TestContext.Current.CancellationToken);

            result.Status.ShouldBe(ProjectionDispatchStatus.Failed, scenario);
            result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict, scenario);
            ReadExistingStateSnapshot(store, handlerName).ShouldBe(before);
            store.ETagFor(RebuildStateKey(handlerName)).ShouldBe(etagBefore);
            store.TrySaveCount.ShouldBe(0);
        }
    }

    /// <summary>Store read outages retain their transport type rather than being classified as corrupt candidates.</summary>
    /// <param name="handlerName">The projection whose finalization reads the store.</param>
    /// <param name="failure">The transport failure type, including types also caught by fold guards.</param>
    [Theory]
    [InlineData("catalog", "http")]
    [InlineData("catalog", "invalid-operation")]
    [InlineData("catalog", "json")]
    [InlineData("index", "http")]
    [InlineData("index", "invalid-operation")]
    [InlineData("index", "json")]
    public async Task RebuildReadOutagesRemainTransportFailures(string handlerName, string failure)
    {
        Exception outage = failure switch
        {
            "http" => new HttpRequestException("Read unavailable."),
            "invalid-operation" => new InvalidOperationException("Read unavailable."),
            "json" => new JsonException("Read unavailable."),
            _ => throw new InvalidOperationException("Unknown transport failure.")
        };
        var store = new ScriptedReadModelStore { GetException = outage };
        IAsyncDomainSharedProjectionRebuildHandler handler = RebuildHandler(handlerName, store);
        DomainSharedProjectionRebuildIdentity identity = RebuildIdentity(handlerName);
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(identity, TestContext.Current.CancellationToken);

        Exception? thrown = await Record.ExceptionAsync(async () =>
            await handler.FinalizeAsync(identity, candidate, TestContext.Current.CancellationToken));

        thrown.ShouldBeSameAs(outage);
        store.TrySaveCount.ShouldBe(0);
    }

    /// <summary>Re-derives the unique lookup from canonical contributions rather than trusting an unused derived cache.</summary>
    [Fact]
    public async Task PersistedCanonicalContributionsRepairTheDerivedLookup()
    {
        const string json = """
            {"entries":{"bad":null},"contributions":{"other":[{"tenant":{"tenantId":"tenant-2"},"capabilityId":{"value":"other-capability"}}]}}
            """;
        var store = new ScriptedReadModelStore();
        store.Set(RebuildStateKey("index"), JsonSerializer.Deserialize<MagicLinkTokenHashCapabilityIndexReadModel>(json, s_jsonOptions)!);

        DomainProjectionRebuildPlan plan = await RebuildAsync("index", store, TestContext.Current.CancellationToken);
        var rebuilt = JsonSerializer.Deserialize<MagicLinkTokenHashCapabilityIndexReadModel>(
            plan.Operations.ShouldHaveSingleItem().CanonicalValue.Span, s_jsonOptions)!;

        rebuilt.Entries.Keys.ShouldBe(["hash-only", "other"], ignoreOrder: true);
        rebuilt.Entries["other"].CapabilityId.Value.ShouldBe("other-capability");
        rebuilt.Entries.ShouldNotContainKey("bad");
        store.TrySaveCount.ShouldBe(0);
    }

    /// <summary>Valid empty candidates remain usable after both forms of accumulation and finalization.</summary>
    /// <param name="handlerName">The projection under test.</param>
    [Theory]
    [InlineData("catalog")]
    [InlineData("index")]
    public async Task ValidEmptyCandidatesRemainRebuildable(string handlerName)
    {
        var store = new ScriptedReadModelStore();
        IAsyncDomainSharedProjectionRebuildHandler handler = RebuildHandler(handlerName, store);
        DomainSharedProjectionRebuildIdentity identity = RebuildIdentity(handlerName);
        DomainSharedProjectionRebuildCandidate empty = await handler.CreateEmptyCandidateAsync(identity, TestContext.Current.CancellationToken);
        DomainSharedProjectionRebuildCandidate accumulated = await handler.AccumulateAsync(identity, empty,
            ValidRequest(handlerName) with { Events = [] }, TestContext.Current.CancellationToken);

        DomainProjectionRebuildPlan plan = await handler.FinalizeAsync(identity, accumulated, TestContext.Current.CancellationToken);

        plan.Operations.ShouldHaveSingleItem().Key.ShouldBe(RebuildStateKey(handlerName));
        if (handlerName == "catalog")
        {
            var catalog = JsonSerializer.Deserialize<ActivityTypeCatalogReadModel>(plan.Operations[0].CanonicalValue.Span, s_jsonOptions)!;
            catalog.Items.ShouldBeEmpty();
            catalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        }
        else
        {
            JsonSerializer.Deserialize<MagicLinkTokenHashCapabilityIndexReadModel>(
                plan.Operations[0].CanonicalValue.Span, s_jsonOptions)!.Entries.ShouldBeEmpty();
        }
        store.TrySaveCount.ShouldBe(0);
    }

    /// <summary>Requested cancellation takes precedence over malformed candidate validation.</summary>
    /// <param name="handlerName">The projection under test.</param>
    [Theory]
    [InlineData("catalog")]
    [InlineData("index")]
    public async Task RebuildCancellationRemainsCancellation(string handlerName)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var store = new ScriptedReadModelStore();
        IAsyncDomainSharedProjectionRebuildHandler handler = RebuildHandler(handlerName, store);
        DomainSharedProjectionRebuildIdentity identity = RebuildIdentity(handlerName);
        var malformed = new DomainSharedProjectionRebuildCandidate(Encoding.UTF8.GetBytes("{"));

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await handler.AccumulateAsync(identity, malformed, ValidRequest(handlerName), cancellation.Token));
        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await handler.FinalizeAsync(identity, malformed, cancellation.Token));

        store.TrySaveCount.ShouldBe(0);
    }

    private static IAsyncDomainSharedProjectionRebuildHandler RebuildHandler(string handlerName, ScriptedReadModelStore store)
        => handlerName == "catalog"
            ? new TenantActivityTypeCatalogProjectionHandler(store)
            : new MagicLinkTokenHashCapabilityIndexProjectionHandler(store);

    private static ProjectionEventDto MalformedWireEvent(ProjectionEventDto source, string scenario)
        => scenario switch
        {
            "null-format" => source with { SerializationFormat = null! },
            "blank-format" => source with { SerializationFormat = " " },
            "null-payload" => source with { Payload = null! },
            "empty-payload" => source with { Payload = [] },
            "invalid-json" => source with { Payload = Encoding.UTF8.GetBytes("{") },
            "json-null" => source with { Payload = Encoding.UTF8.GetBytes("null") },
            "non-json" => source with { SerializationFormat = "protobuf" },
            _ => throw new InvalidOperationException("Unknown malformed wire scenario.")
        };
}
