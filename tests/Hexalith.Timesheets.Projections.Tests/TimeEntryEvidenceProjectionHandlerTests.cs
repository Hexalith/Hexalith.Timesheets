using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections.TimeEntries;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.Policies;

using Shouldly;

namespace Hexalith.Timesheets.Projections.Tests;

public sealed class TimeEntryEvidenceProjectionHandlerTests
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ContiguousSuffixAppendsSelectedLineageWithoutFullOwnerReplay()
    {
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        ProjectionEventDto recorded = Event(1, Recorded());
        ProjectionEventDto unrelated = Event(2, new { Other = "ignored" });
        ProjectionEventDto submitted = Event(3, Submitted());
        (await handler.ProjectAsync(Request(recorded), "first",
            TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        store.GetPacked()["effectiveValuesDigest"].ShouldNotBeNull();
        (await handler.ProjectAsync(Request(unrelated), "second",
            TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await handler.ProjectAsync(Request(submitted), "third",
            TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        store.Get().EventPayloadLineage.Count.ShouldBe(3);
        store.Get().EventLineage.Select(static item => item.Ordinal).ShouldBe([1, 3]);
        store.Get().ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);

        var replayStore = new TestStore();
        var replayHandler = new TimeEntryEvidenceProjectionHandler(replayStore);
        (await replayHandler.ProjectAsync(Request(recorded, unrelated, submitted), "full",
            TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        JsonNode.DeepEquals(store.GetPacked(), replayStore.GetPacked()).ShouldBeTrue();
    }

    [Fact]
    public async Task AdjustmentAndCorrectionSuffixesMatchFullReplay()
    {
        TimeEntryAdjustedThroughMagicLink adjusted = Adjusted();
        TimeEntryCorrected corrected = Corrected(adjusted.AdjustedValues,
            adjusted.AdjustedValues with { DurationMinutes = 50 });
        ProjectionEventDto[] events = [Event(1, Recorded()), Event(2, adjusted),
            Event(3, Submitted()), Event(4, Rejected()), Event(5, corrected)];
        var suffixStore = new TestStore();
        var suffixHandler = new TimeEntryEvidenceProjectionHandler(suffixStore);
        foreach (ProjectionEventDto item in events)
            (await suffixHandler.ProjectAsync(Request(item), "suffix-" + item.SequenceNumber,
                TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        suffixStore.Get().DurationMinutes.ShouldBe(50);
        suffixStore.Get().CorrectionState.ShouldBe(TimeEntryCorrectionState.Corrected);

        var replayStore = new TestStore();
        (await new TimeEntryEvidenceProjectionHandler(replayStore).ProjectAsync(Request(events),
            "full", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        JsonNode.DeepEquals(suffixStore.GetPacked(), replayStore.GetPacked()).ShouldBeTrue();
    }

    [Fact]
    public async Task MalformedSelectedValuesDigestFailsSuffixWithoutReplacingEvidence()
    {
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        (await handler.ProjectAsync(Request(Event(1, Recorded())), "recorded",
            TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        JsonObject corrupt = (JsonObject)store.GetPacked().DeepClone();
        corrupt["effectiveValuesDigest"] = 12;
        Resign(corrupt);
        store.SeedPacked(corrupt);

        (await handler.ProjectAsync(Request(Event(2, Submitted())), "malformed-digest",
            TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Failed);
        JsonNode.DeepEquals(corrupt, store.GetPacked()).ShouldBeTrue();
    }

    [Fact]
    public async Task SuffixRejectsChangedPreviousAdjustmentValuesWithoutReplacingSelected()
    {
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        (await handler.ProjectAsync(Request(Event(1, Recorded())), "first",
            TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        JsonObject before = (JsonObject)store.GetPacked().DeepClone();
        TimeEntryAdjustedThroughMagicLink contradictory = Adjusted() with
        {
            PreviousValues = Adjusted().PreviousValues with { DurationMinutes = 999 }
        };
        (await handler.ProjectAsync(Request(Event(2, contradictory)), "contradiction",
            TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Failed);
        JsonNode.DeepEquals(before, store.GetPacked()).ShouldBeTrue();
    }

    [Theory]
    [InlineData("TimeEntryRecorded", "01010000000000000001")]
    [InlineData("TimeEntrySubmitted", "02010000000000000001")]
    [InlineData("TimeEntryContributorConfirmed", "03010000000000000001")]
    [InlineData("TimeEntryAdjustedThroughMagicLink", "04010000000000000001")]
    [InlineData("TimeEntryApproved", "05010000000000000001")]
    [InlineData("TimeEntryRejected", "06010000000000000001")]
    [InlineData("TimeEntryCorrected", "07010000000000000001")]
    [InlineData("TimeEntryApprovedCorrected", "08010000000000000001")]
    public void CompactEventCodesMatchFixedPersistedBytes(string name, string expectedHex)
    {
        TimeEntryEventLineageItem item = new(name, 1, TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents);
        byte[] fixedBytes = Convert.FromHexString(expectedHex);
        TimeEntryEvidenceLineageCodec.PackEvents([item]).ShouldBe(fixedBytes);
        TimeEntryEvidenceLineageCodec.UnpackEvents(fixedBytes).Single().EventName.ShouldBe(name);
    }

    [Fact]
    public async Task ShorterFullReplayCannotReplaceNewerSelectedEvidence()
    {
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        (await handler.ProjectAsync(Request(Event(1, Recorded()), Event(2, Submitted())),
            "selected", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        TimeEntryEvidenceReadModel selected = store.Get();

        _ = await Should.ThrowAsync<DomainProjectionRebuildRejectedException>(() => handler.PrepareRebuildAsync(
            Request(Event(1, Recorded())), "stale-rebuild", TestContext.Current.CancellationToken));

        store.Get().ShouldBeSameAs(selected);
    }

    [Fact]
    public async Task LegacyExplicitLineageSelectedRowExtendsAndMigratesOnDelivery()
    {
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        (await handler.ProjectAsync(Request(Event(1, Recorded())), "recorded",
            TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        JsonObject legacy = JsonSerializer.SerializeToNode(store.Get() with
        {
            EventLineage = store.Get().EventLineage,
            EventPayloadLineage = [LegacyDigest(Event(1, Recorded()))],
            PackedEventLineage = null,
            PackedPayloadLineage = null
        }, s_jsonOptions)!.AsObject();
        store.SeedPacked(legacy);

        (await handler.ProjectAsync(Request(Event(1, Recorded()), Event(2, Submitted())),
            "extend", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        JsonObject selected = store.GetPacked();
        selected["materializedDigest"].ShouldNotBeNull();
        TimeEntryEvidenceCompactStorage.Unpack(selected).ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
    }

    [Fact]
    public async Task LegacyExplicitLineageSelectedRowMustMatchItsCanonicalPrefixBeforeRebuild()
    {
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        (await handler.ProjectAsync(Request(Event(1, Recorded())), "recorded",
            TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        JsonObject legacy = JsonSerializer.SerializeToNode(store.Get() with
        {
            EventLineage = store.Get().EventLineage,
            EventPayloadLineage = [LegacyDigest(Event(1, Recorded()))],
            PackedEventLineage = null,
            PackedPayloadLineage = null
        }, s_jsonOptions)!.AsObject();
        store.SeedPacked(legacy);
        _ = await Should.ThrowAsync<DomainProjectionRebuildRejectedException>(() => handler.PrepareRebuildAsync(
            Request(Event(1, Recorded() with { DurationMinutes = 46 }), Event(2, Submitted())),
            "changed-legacy-owner", TestContext.Current.CancellationToken));
        DomainProjectionRebuildPlan extended = await handler.PrepareRebuildAsync(
            Request(Event(1, Recorded()), Event(2, Submitted())), "legacy-rebuild",
            TestContext.Current.CancellationToken);
        extended.Operations.ShouldHaveSingleItem();

        legacy["durationMinutes"] = 999;
        store.SeedPacked(legacy);
        _ = await Should.ThrowAsync<DomainProjectionRebuildRejectedException>(() => handler.PrepareRebuildAsync(
            Request(Event(1, Recorded()), Event(2, Submitted())), "corrupt-legacy",
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FirstFullReplayRequiresTheAuthoritativeOwnerHead()
    {
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        ProjectionRequest prefix = Request(Event(1, Recorded())) with { RebuildSourceHeadSequence = 2 };

        _ = await Should.ThrowAsync<DomainProjectionRebuildRejectedException>(() =>
            handler.PrepareRebuildAsync(prefix, "partial-initial", TestContext.Current.CancellationToken));

        store.HasValue.ShouldBeFalse();
    }

    [Fact]
    public async Task EmptyReplayDeletesSelectedEvidenceOnlyWithSourceErasureProof()
    {
        var store = new InMemoryReadModelStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        DomainProjectionRebuildPlan initial = await handler.PrepareRebuildAsync(
            Request(Event(1, Recorded())), "initial", TestContext.Current.CancellationToken);
        var scope = new ReadModelBatchScope(TimeEntryEvidenceReadModelAddress.StateStoreName,
            "tenant-1", "timesheets", "entry-1", TimeEntryEvidenceProjection.ProjectionName, "initial");
        (await store.ExecuteAsync(new ReadModelBatch(scope, initial.Operations),
            TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        string key = TimeEntryEvidenceReadModelAddress.StateKey("tenant-1", new TimeEntryId("entry-1"));

        ProjectionRequest activeEmpty = Request() with { RebuildSourceHeadSequence = 0 };
        _ = await Should.ThrowAsync<DomainProjectionRebuildRejectedException>(() =>
            handler.PrepareRebuildAsync(activeEmpty, "active-empty", TestContext.Current.CancellationToken));
        (await store.GetAsync<JsonObject>(TimeEntryEvidenceReadModelAddress.StateStoreName,
            key, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();

        ProjectionRequest erasedEmpty = activeEmpty with { RebuildSourceErased = true };
        DomainProjectionRebuildPlan erasure = await handler.PrepareRebuildAsync(
            erasedEmpty, "erased", TestContext.Current.CancellationToken);
        (await store.ExecuteAsync(new ReadModelBatch(scope with { BatchId = "erased" }, erasure.Operations),
            TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await store.GetAsync<JsonObject>(TimeEntryEvidenceReadModelAddress.StateStoreName,
            key, TestContext.Current.CancellationToken)).Value.ShouldBeNull();
    }

    [Fact]
    public async Task EqualPrefixReplayRejectsAlteredSelectedDecisionEvidence()
    {
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        ProjectionRequest complete = Request(Event(1, Recorded()), Event(2, Submitted()), Event(3, Approved()));
        (await handler.ProjectAsync(complete, "initial", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        JsonObject altered = store.GetPacked();
        altered["approvalState"] = "Rejected";
        Resign(altered);
        store.SeedPacked(altered);

        (await handler.ProjectAsync(complete, "redelivery", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Failed);
        _ = await Should.ThrowAsync<DomainProjectionRebuildRejectedException>(() =>
            handler.PrepareRebuildAsync(complete, "equal-prefix-rebuild", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SecondDistinctApprovedCorrectionIsRejectedByPersistedDeliveryAndRebuild()
    {
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        TimeEntryCorrectionValues original = Adjusted().PreviousValues;
        TimeEntryCorrectionValues first = original with { DurationMinutes = 50 };
        TimeEntryApprovedCorrected correction = new(
            new TimeEntryId("entry-1"), new TimeEntryCorrectionId("correction-1"),
            new TenantReference("tenant-1"), new PartyReference("approver-1"),
            new DateTimeOffset(2026, 6, 19, 14, 0, 0, TimeSpan.Zero),
            original, first, new TimeEntryCorrectionReason("Approved correction"),
            new TimeEntryApprovalDecisionId("decision-1"), TimeEntryApprovalScope.IndividualEntry,
            TimeEntryApprovalState.Approved, TimeEntryCorrectionState.Corrected);
        ProjectionEventDto[] prefix = [Event(1, Recorded()), Event(2, Submitted()),
            Event(3, Approved()), Event(4, correction)];
        (await handler.ProjectAsync(Request(prefix), "first-correction", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        TimeEntryEvidenceReadModel selected = store.Get();
        ProjectionRequest duplicateCorrection = Request([.. prefix, Event(5, correction with
        {
            TimeEntryCorrectionId = new TimeEntryCorrectionId("correction-2"),
            PreviousValues = first,
            CorrectedValues = first with { DurationMinutes = 55 }
        })]);

        (await handler.ProjectAsync(duplicateCorrection, "second-correction", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Failed);
        _ = await Should.ThrowAsync<DomainProjectionRebuildRejectedException>(() =>
            handler.PrepareRebuildAsync(duplicateCorrection, "second-correction-rebuild",
                TestContext.Current.CancellationToken));
        store.Get().ShouldBeSameAs(selected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyCorrectionPreviousScopeInheritsActualScopeWithoutMaskingContradictions(bool approved)
    {
        TimeEntryCorrectionValues effective = Adjusted().PreviousValues;
        TimeEntryCorrectionValues legacyPrevious = effective with { ActivityTypeScope = null };
        object correction = approved
            ? new TimeEntryApprovedCorrected(
                new TimeEntryId("entry-1"), new TimeEntryCorrectionId("correction-1"),
                new TenantReference("tenant-1"), new PartyReference("approver-1"),
                new DateTimeOffset(2026, 6, 19, 14, 0, 0, TimeSpan.Zero),
                legacyPrevious, effective with { DurationMinutes = 50 },
                new TimeEntryCorrectionReason("Approved correction"),
                new TimeEntryApprovalDecisionId("decision-1"), TimeEntryApprovalScope.IndividualEntry,
                TimeEntryApprovalState.Approved, TimeEntryCorrectionState.Corrected)
            : Corrected(legacyPrevious, effective with { DurationMinutes = 50 });
        ProjectionEventDto[] prefix = [Event(1, Recorded()), Event(2, Submitted()),
            Event(3, approved ? Approved() : Rejected())];
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        ProjectionRequest valid = Request([.. prefix, Event(4, correction)]);
        (await handler.ProjectAsync(valid, "legacy-previous", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        store.Get().DurationMinutes.ShouldBe(50);
        (await handler.PrepareRebuildAsync(valid, "legacy-previous-rebuild",
            TestContext.Current.CancellationToken)).Operations.ShouldNotBeEmpty();

        foreach (TimeEntryCorrectionValues badPrevious in new[]
        {
            effective with { ActivityTypeScope = ActivityTypeScope.Project },
            legacyPrevious with { DurationMinutes = 999 }
        })
        {
            object badCorrection = approved
                ? ((TimeEntryApprovedCorrected)correction) with { PreviousValues = badPrevious }
                : ((TimeEntryCorrected)correction) with { PreviousValues = badPrevious };
            ProjectionRequest invalid = Request([.. prefix, Event(4, badCorrection)]);
            (await new TimeEntryEvidenceProjectionHandler(new TestStore()).ProjectAsync(
                invalid, "bad-previous", TestContext.Current.CancellationToken))
                .Status.ShouldBe(ProjectionDispatchStatus.Failed);
        }
    }

    [Fact]
    public async Task CompleteUnrecognizedReplayPrunesSelectedEvidence()
    {
        var store = new InMemoryReadModelStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        ProjectionEventDto recorded = Event(1, Recorded());
        DomainProjectionRebuildPlan initialPlan = await handler.PrepareRebuildAsync(
            Request(recorded), "initial", TestContext.Current.CancellationToken);
        var initialBatch = new ReadModelBatch(new ReadModelBatchScope(
            TimeEntryEvidenceReadModelAddress.StateStoreName, "tenant-1", "timesheets",
            "entry-1", TimeEntryEvidenceProjection.ProjectionName, "initial"), initialPlan.Operations);
        (await store.ExecuteAsync(initialBatch, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        string key = TimeEntryEvidenceReadModelAddress.StateKey("tenant-1", new TimeEntryId("entry-1"));
        JsonObject selected = (await store.GetAsync<JsonObject>(TimeEntryEvidenceReadModelAddress.StateStoreName,
            key, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        TimeEntryEvidenceCompactStorage.Unpack(selected).EventLineage.Count.ShouldBe(1);

        ProjectionEventDto unrelated = recorded with
        {
            EventTypeName = "UnrelatedOwnerEvent",
            Payload = JsonSerializer.SerializeToUtf8Bytes(new { ignored = true }, s_jsonOptions),
            MessageId = "unrelated-message"
        };
        DomainProjectionRebuildPlan plan = await handler.PrepareRebuildAsync(
            Request(unrelated), "complete-prune", TestContext.Current.CancellationToken);
        var batch = new ReadModelBatch(new ReadModelBatchScope(
            TimeEntryEvidenceReadModelAddress.StateStoreName, "tenant-1", "timesheets",
            "entry-1", TimeEntryEvidenceProjection.ProjectionName, "complete-prune"), plan.Operations);
        (await store.ExecuteAsync(batch, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await store.GetAsync<JsonObject>(TimeEntryEvidenceReadModelAddress.StateStoreName,
            key, TestContext.Current.CancellationToken)).Value.ShouldBeNull();
    }

    [Fact]
    public async Task AlteredSelectedApprovalStateCannotSurvivePrefixReplay()
    {
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        (await handler.ProjectAsync(Request(Event(1, Recorded()), Event(2, Submitted()), Event(3, Approved())),
            "selected", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        JsonObject corrupt = store.GetPacked();
        corrupt["approvalState"] = "Rejected";
        store.SeedPacked(corrupt);

        (await handler.ProjectAsync(Request(Event(1, Recorded())), "old-prefix",
            TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Failed);
    }

    [Fact]
    public async Task CorrectionReasonAndSourcePositionsMustMatchPriorEvidence()
    {
        TimeEntryCorrectionValues values = Adjusted().PreviousValues;
        TimeEntryCorrected corrected = Corrected(values, values with { DurationMinutes = 50 });
        ProjectionEventDto[] prefix = [Event(1, Recorded()), Event(2, Submitted()), Event(3, Rejected())];
        ProjectionRequest[] invalid =
        [
            Request([.. prefix, Event(4, corrected with
            {
                RejectionReason = new TimeEntryRejectionReason("Other reason")
            })]),
            Request(Event(1, Recorded()) with { GlobalPosition = 5 },
                Event(2, Submitted()) with { GlobalPosition = 4 }),
            Request(Event(1, Recorded()) with { GlobalPosition = 0 })
        ];
        foreach (ProjectionRequest request in invalid)
        {
            var store = new TestStore();
            (await new TimeEntryEvidenceProjectionHandler(store).ProjectAsync(request, "invalid",
                TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Failed);
            store.HasValue.ShouldBeFalse();
        }
    }

    [Fact]
    public void CompactSelectedStorageRejectsNullContradictoryAndUnknownLineage()
    {
        var store = new TestStore();
        store.Seed(new TimeEntryEvidenceProjection().Project("tenant-1", new TimeEntryId("entry-1"),
            [new TimeEntryProjectionEvent("message-1", 1, Recorded(), "tenant-1")],
            new TimesheetsProjectionCheckpoint("tenant-1", TimeEntryEvidenceProjection.ProjectionName,
                1, ProjectionFreshness.Fresh))! with
        {
            EventPayloadLineage = [new string('A', 64)]
        });
        JsonObject original = store.GetPacked();

        JsonObject nullLineage = (JsonObject)original.DeepClone();
        nullLineage["eventLineage"] = null;
        Should.Throw<InvalidOperationException>(() => TimeEntryEvidenceCompactStorage.Unpack(nullLineage));

        JsonObject contradictory = (JsonObject)original.DeepClone();
        contradictory["eventLineage"] = JsonSerializer.SerializeToNode(
            new[] { new TimeEntryEventLineageItem("TimeEntrySubmitted", 1,
                TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents) }, s_jsonOptions);
        Resign(contradictory);
        Should.Throw<InvalidOperationException>(() => TimeEntryEvidenceCompactStorage.Unpack(contradictory));

        JsonObject unsupported = (JsonObject)original.DeepClone();
        byte[] bytes = Convert.FromBase64String(unsupported["packedEventLineage"]!.GetValue<string>());
        bytes[0] = byte.MaxValue;
        unsupported["packedEventLineage"] = Convert.ToBase64String(bytes);
        Resign(unsupported);
        Should.Throw<InvalidOperationException>(() => TimeEntryEvidenceCompactStorage.Unpack(unsupported));
    }

    private static void Resign(JsonObject row)
    {
        static JsonNode? Sort(JsonNode? node)
        {
            if (node is JsonObject objectNode)
            {
                var sorted = new JsonObject();
                foreach (KeyValuePair<string, JsonNode?> property in objectNode
                    .OrderBy(static property => property.Key, StringComparer.Ordinal))
                {
                    sorted[property.Key] = Sort(property.Value);
                }

                return sorted;
            }

            if (node is JsonArray arrayNode)
            {
                var sorted = new JsonArray();
                foreach (JsonNode? item in arrayNode)
                {
                    sorted.Add(Sort(item));
                }

                return sorted;
            }

            return node?.DeepClone();
        }

        JsonObject unsigned = (JsonObject)row.DeepClone();
        _ = unsigned.Remove("materializedDigest");
        row["materializedDigest"] = Convert.ToHexString(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(Sort(unsigned), s_jsonOptions)));
    }

    [Fact]
    public async Task Stored_record_submission_and_approval_publish_decision_and_lock_evidence()
    {
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        var request = Request(
            Event(1, new StoredTimeEntryRecorded(Recorded())),
            Event(2, new StoredTimeEntrySubmitted(Submitted())),
            Event(3, Approved()));

        var result = await handler.ProjectAsync(request, "dispatch-1", TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        TimeEntryEvidenceReadModel model = store.Get();
        model.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved);
        model.ApprovalDecision.ShouldNotBeNull();
        model.LockEvidence.LockState.ShouldBe(TimeEntryLockState.LockedFromDirectEdit);
        model.EventPayloadLineage.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Older_replay_retains_newer_evidence_and_conflicting_prefix_fails()
    {
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        _ = await handler.ProjectAsync(
            Request(Event(1, Recorded()), Event(2, Submitted())),
            "dispatch-1",
            TestContext.Current.CancellationToken);
        TimeEntryEvidenceReadModel newer = store.Get();

        var replay = await handler.ProjectAsync(
            Request(Event(1, Recorded())),
            "dispatch-2",
            TestContext.Current.CancellationToken);
        replay.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        store.Get().ShouldBeSameAs(newer);

        var conflict = await handler.ProjectAsync(
            Request(Event(1, Recorded() with { DurationMinutes = 90 })),
            "dispatch-3",
            TestContext.Current.CancellationToken);
        conflict.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        store.Get().ShouldBeSameAs(newer);
    }

    [Fact]
    public async Task Higher_prefix_must_extend_the_full_stored_payload_history()
    {
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        _ = await handler.ProjectAsync(
            Request(Event(1, Recorded())), "first", TestContext.Current.CancellationToken);
        TimeEntryEvidenceReadModel original = store.Get();

        var result = await handler.ProjectAsync(
            Request(Event(1, Recorded() with { DurationMinutes = 90 }), Event(2, Submitted())),
            "divergent-extension",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        store.Get().ShouldBeSameAs(original);
    }

    [Fact]
    public async Task Older_prefix_with_unrelated_owner_event_retains_later_evidence()
    {
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        ProjectionEventDto unrelated = Event(2, new { Marker = "other" });
        _ = await handler.ProjectAsync(
            Request(Event(1, Recorded()), unrelated, Event(3, Submitted())),
            "full", TestContext.Current.CancellationToken);
        TimeEntryEvidenceReadModel full = store.Get();

        var result = await handler.ProjectAsync(
            Request(Event(1, Recorded()), unrelated),
            "prefix", TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        store.Get().ShouldBeSameAs(full);
    }

    [Fact]
    public async Task OlderPrefixCannotEndorseResignedSelectedApprovalEvidence()
    {
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        (await handler.ProjectAsync(Request(Event(1, Recorded()), Event(2, Submitted()), Event(3, Approved())),
            "selected", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        JsonObject altered = store.GetPacked();
        altered["approvalState"] = "Rejected";
        Resign(altered);
        store.SeedPacked(altered);
        TimeEntryEvidenceReadModel selected = store.Get();

        (await handler.ProjectAsync(Request(Event(1, Recorded()), Event(2, Submitted())),
            "older-prefix", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Failed);
        store.Get().ShouldBeSameAs(selected);
        JsonNode.DeepEquals(store.GetPacked(), altered).ShouldBeTrue();
    }

    [Fact]
    public async Task ChangedUnrelatedPayloadInAnOwnerPrefixFailsWithoutReplacingEvidence()
    {
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        _ = await handler.ProjectAsync(Request(Event(1, Recorded()), Event(2, new { Marker = "first" }),
            Event(3, Submitted())), "original", TestContext.Current.CancellationToken);
        TimeEntryEvidenceReadModel original = store.Get();

        var result = await handler.ProjectAsync(
            Request(Event(1, Recorded()), Event(2, new { Marker = "changed" }), Event(3, Submitted())),
            "changed", TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        store.Get().ShouldBeSameAs(original);
    }

    [Fact]
    public async Task ChangedAuthoritativeEnvelopeInAnOwnerPrefixFailsWithoutReplacingEvidence()
    {
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        ProjectionEventDto originalEvent = Event(2, new { Marker = "same" });
        _ = await handler.ProjectAsync(Request(Event(1, Recorded()), originalEvent,
            Event(3, Submitted())), "original", TestContext.Current.CancellationToken);
        TimeEntryEvidenceReadModel original = store.Get();

        var result = await handler.ProjectAsync(Request(Event(1, Recorded()),
            originalEvent with { MessageId = "different-message", GlobalPosition = 42 },
            Event(3, Submitted())), "changed-envelope", TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        store.Get().ShouldBeSameAs(original);
    }

    [Theory]
    [InlineData("metadataVersion")]
    [InlineData("storedContract")]
    [InlineData("storedVersion")]
    [InlineData("storedFormat")]
    [InlineData("effectiveContract")]
    [InlineData("effectiveVersion")]
    [InlineData("effectiveFormat")]
    [InlineData("effectivePayload")]
    [InlineData("storedType")]
    [InlineData("storedDigest")]
    [InlineData("registry")]
    [InlineData("adapted")]
    public void SameSequenceDuplicateMustMatchEveryOptionalProvenanceMember(string member)
    {
        ProjectionEventDto original = Event(1, Recorded());
        ProjectionEventDto changed = member switch
        {
            "metadataVersion" => original with { MetadataVersion = 1 },
            "storedContract" => original with { StoredEventContractType = "stored-contract" },
            "storedVersion" => original with { StoredPayloadVersion = 2 },
            "storedFormat" => original with { StoredSerializationFormat = "json" },
            "effectiveContract" => original with { EffectiveEventContractType = "effective-contract" },
            "effectiveVersion" => original with { EffectivePayloadVersion = 2 },
            "effectiveFormat" => original with { EffectiveSerializationFormat = "json" },
            "effectivePayload" => original with { EffectivePayload = [1, 2] },
            "storedType" => original with { StoredEventTypeName = "stored-type" },
            "storedDigest" => original with { StoredDigest = [3, 4] },
            "registry" => original with { RegistryFingerprint = "registry" },
            "adapted" => original with { IsAdapted = true },
            _ => throw new InvalidOperationException(member)
        };

        _ = Should.Throw<InvalidOperationException>(() => ProjectionEventReader.Normalize([original, changed]));
    }

    [Fact]
    public async Task PersistedRejectionCarriesReasonAndLeavesEntryUnlocked()
    {
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);

        var result = await handler.ProjectAsync(
            Request(Event(1, new StoredTimeEntryRecorded(Recorded())),
                Event(2, new StoredTimeEntrySubmitted(Submitted())), Event(3, Rejected())),
            "rejected", TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        TimeEntryEvidenceReadModel row = store.Get();
        row.ApprovalState.ShouldBe(TimeEntryApprovalState.Rejected);
        row.ApprovalDecision.ShouldNotBeNull().Reason.ShouldNotBeNull().Value.ShouldBe("Needs correction");
        row.LockEvidence.LockState.ShouldBe(TimeEntryLockState.Unlocked);
    }

    [Fact]
    public async Task TenThousandEventHistoryKeepsCompleteLineageWithinOneBatchManifest()
    {
        ProjectionEventDto[] events = new ProjectionEventDto[10_000];
        events[0] = Event(1, Recorded());
        for (int index = 1; index < events.Length; index++)
        {
            string requestId = Convert.ToHexString(SHA256.HashData(BitConverter.GetBytes(index)));
            events[index] = Event(index + 1, new TimeEntryContributorConfirmed(
                new TimeEntryId("entry-1"), new PartyReference("contributor-1"),
                new TenantReference("tenant-1"), new DateTimeOffset(2026, 6, 19, 15, 0, 0, TimeSpan.Zero),
                new ExternalContributionSource("magic-link", requestId)));
        }

        var store = new InMemoryReadModelStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        ProjectionRequest request = Request(events);
        (await handler.ProjectAsync(request, "large-live", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        string key = TimeEntryEvidenceReadModelAddress.StateKey("tenant-1", new TimeEntryId("entry-1"));
        TimeEntryEvidenceReadModel live = (await store.GetAsync<TimeEntryEvidenceReadModel>(
            TimeEntryEvidenceReadModelAddress.StateStoreName, key, TestContext.Current.CancellationToken))
            .Value.ShouldNotBeNull();
        live.EventLineage.Count.ShouldBe(10_000);
        live.EventPayloadLineage.Count.ShouldBe(10_000);
        ReferenceEquals(live.EventLineage, live.EventLineage).ShouldBeTrue();
        ReferenceEquals(live.EventPayloadLineage, live.EventPayloadLineage).ShouldBeTrue();
        JsonObject compact = (await store.GetAsync<JsonObject>(TimeEntryEvidenceReadModelAddress.StateStoreName,
            key, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        JsonSerializer.SerializeToUtf8Bytes(compact, s_jsonOptions).Length.ShouldBeLessThan(1_048_576);

        var rebuiltStore = new InMemoryReadModelStore();
        var rebuiltHandler = new TimeEntryEvidenceProjectionHandler(rebuiltStore);
        DomainProjectionRebuildPlan plan = await rebuiltHandler.PrepareRebuildAsync(
            request, "large-rebuild", TestContext.Current.CancellationToken);
        var batch = new ReadModelBatch(new ReadModelBatchScope(
            TimeEntryEvidenceReadModelAddress.StateStoreName, "tenant-1", "timesheets",
            "entry-1", TimeEntryEvidenceProjection.ProjectionName, "large-rebuild"), plan.Operations);
        (await rebuiltStore.ExecuteAsync(batch, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        TimeEntryEvidenceReadModel rebuilt = (await rebuiltStore.GetAsync<TimeEntryEvidenceReadModel>(
            TimeEntryEvidenceReadModelAddress.StateStoreName, key, TestContext.Current.CancellationToken))
            .Value.ShouldNotBeNull();
        rebuilt.EventLineage.ShouldBe(live.EventLineage);
        rebuilt.EventPayloadLineage.ShouldBe(live.EventPayloadLineage);
    }

    [Fact]
    public async Task OrdinaryDeliveryAboveTenThousandEventsFailsWithoutAStoreRetry()
    {
        ProjectionEventDto[] events = new ProjectionEventDto[10_001];
        ProjectionEventDto first = Event(1, Recorded());
        for (int index = 0; index < events.Length; index++)
        {
            events[index] = first with
            {
                SequenceNumber = index + 1,
                GlobalPosition = index + 1,
                MessageId = "message-" + (index + 1).ToString(CultureInfo.InvariantCulture)
            };
        }

        var store = new InMemoryReadModelStore();
        DomainProjectionHandlerResult result = await new TimeEntryEvidenceProjectionHandler(store)
            .ProjectAsync(Request(events), "over-bound", TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        (await store.GetAsync<JsonObject>(TimeEntryEvidenceReadModelAddress.StateStoreName,
            TimeEntryEvidenceReadModelAddress.StateKey("tenant-1", new TimeEntryId("entry-1")),
            TestContext.Current.CancellationToken)).Value.ShouldBeNull();
    }

    [Fact]
    public async Task CompactSelectedRowRestoresExpectedMixedEventNamesAndOrdinals()
    {
        var store = new InMemoryReadModelStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        (await handler.ProjectAsync(Request(Event(1, new StoredTimeEntryRecorded(Recorded())),
            Event(2, new StoredTimeEntrySubmitted(Submitted())), Event(3, Approved())),
            "mixed", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);

        string key = TimeEntryEvidenceReadModelAddress.StateKey("tenant-1", new TimeEntryId("entry-1"));
        TimeEntryEvidenceReadModel restored = (await store.GetAsync<TimeEntryEvidenceReadModel>(
            TimeEntryEvidenceReadModelAddress.StateStoreName, key, TestContext.Current.CancellationToken))
            .Value.ShouldNotBeNull();
        restored.EventLineage.Select(item => (item.EventName, item.Ordinal)).ShouldBe(
        [
            ("TimeEntryRecorded", 1L),
            ("TimeEntrySubmitted", 2L),
            ("TimeEntryApproved", 3L)
        ]);
    }

    [Fact]
    public async Task InvalidStoredCompactOrdinalCannotSurviveOlderPrefixReplay()
    {
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        _ = await handler.ProjectAsync(Request(Event(1, Recorded()), Event(2, Submitted())),
            "original", TestContext.Current.CancellationToken);
        JsonObject invalid = store.GetPacked();
        byte[] eventBytes = Convert.FromBase64String(invalid["packedEventLineage"]!.GetValue<string>());
        System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(eventBytes.AsSpan(11, 8), 9);
        invalid["packedEventLineage"] = Convert.ToBase64String(eventBytes);
        Resign(invalid);
        store.SeedPacked(invalid);

        var result = await handler.ProjectAsync(Request(Event(1, Recorded())),
            "older", TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        JsonNode.DeepEquals(store.GetPacked(), invalid).ShouldBeTrue();
    }

    [Fact]
    public async Task InvalidCorrectionDecisionStateAndAdjustmentAfterCorrectionDoNotPublish()
    {
        TimeEntryCorrectionValues values = Adjusted().PreviousValues;
        TimeEntryCorrected corrected = Corrected(values, values with { DurationMinutes = 50 });
        ProjectionEventDto[] prefix = [Event(1, Recorded()), Event(2, Submitted()), Event(3, Rejected())];
        ProjectionRequest[] invalid =
        [
            Request([.. prefix, Event(4, corrected with { CorrectionState = TimeEntryCorrectionState.None })]),
            Request([.. prefix, Event(4, corrected with { RejectionDecisionId = new TimeEntryApprovalDecisionId("foreign") })]),
            Request([.. prefix, Event(4, corrected), Event(5, Adjusted() with
            {
                PreviousValues = corrected.CorrectedValues
            })])
        ];
        foreach (ProjectionRequest request in invalid)
        {
            var store = new TestStore();
            var result = await new TimeEntryEvidenceProjectionHandler(store).ProjectAsync(
                request, "invalid-transition", TestContext.Current.CancellationToken);
            result.Status.ShouldBe(ProjectionDispatchStatus.Failed);
            store.HasValue.ShouldBeFalse();
            await Should.ThrowAsync<DomainProjectionRebuildRejectedException>(() =>
                new TimeEntryEvidenceProjectionHandler(store).PrepareRebuildAsync(
                    request, "invalid-rebuild", TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task CorrectionCategoryAndLegacyScopeRemainEffectiveForLaterEvents()
    {
        var handler = new TimeEntryEvidenceProjectionHandler(new TestStore());
        TimeEntryCorrectionValues original = Adjusted().PreviousValues;
        TimeEntryCorrected legacy = Corrected(original, original with
        {
            ActivityTypeScope = null,
            DurationMinutes = 50
        });
        TimeEntryCorrectionValues effective = legacy.CorrectedValues with { ActivityTypeScope = ActivityTypeScope.Tenant };
        TimeEntryCorrected next = Corrected(effective, effective with { DurationMinutes = 55 });

        var validStore = new TestStore();
        var valid = await new TimeEntryEvidenceProjectionHandler(validStore).ProjectAsync(
            Request(Event(1, Recorded()), Event(2, Submitted()), Event(3, Rejected()),
                Event(4, legacy), Event(5, Submitted()), Event(6, Rejected()), Event(7, next)),
            "legacy-scope", TestContext.Current.CancellationToken);
        valid.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        validStore.HasValue.ShouldBeFalse();

        var once = await new TimeEntryEvidenceProjectionHandler(validStore).ProjectAsync(
            Request(Event(1, Recorded()), Event(2, Submitted()), Event(3, Rejected()), Event(4, legacy)),
            "legacy-scope-once", TestContext.Current.CancellationToken);
        once.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        validStore.Get().DurationMinutes.ShouldBe(50);
        validStore.Get().ActivityTypeScope.ShouldBe(ActivityTypeScope.Tenant);

        TimeEntryCorrected employee = Corrected(original,
            original with { ContributorCategory = ContributorCategory.Employee });
        var invalid = await handler.ProjectAsync(
            Request(Event(1, Recorded()), Event(2, Submitted()), Event(3, Rejected()),
                Event(4, employee), Event(5, new TimeEntryContributorConfirmed(
                    new TimeEntryId("entry-1"), new PartyReference("contributor-1"),
                    new TenantReference("tenant-1"), DateTimeOffset.UtcNow,
                    new ExternalContributionSource("magic-link", "capability-1")))),
            "category-after-correction", TestContext.Current.CancellationToken);
        invalid.Status.ShouldBe(ProjectionDispatchStatus.Failed);
    }

    [Fact]
    public async Task Stored_owner_mismatch_fails_even_for_an_older_replay()
    {
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        _ = await handler.ProjectAsync(
            Request(Event(1, Recorded()), Event(2, Submitted())),
            "first", TestContext.Current.CancellationToken);
        store.Seed(store.Get() with { TimeEntryId = new TimeEntryId("other-entry") });

        var result = await handler.ProjectAsync(
            Request(Event(1, Recorded())), "older", TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        store.Get().TimeEntryId.Value.ShouldBe("other-entry");
    }

    [Fact]
    public async Task Stored_adjustment_wrapper_publishes_adjusted_evidence()
    {
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);

        var result = await handler.ProjectAsync(
            Request(Event(1, new StoredTimeEntryRecorded(Recorded())),
                Event(2, new StoredTimeEntryAdjusted(Adjusted()))),
            "adjusted", TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        store.Get().DurationMinutes.ShouldBe(60);
        store.Get().ExternalAdjustment.ShouldNotBeNull();
        store.Get().EventPayloadLineage.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(TimesheetsCommentPolicyDecision.PolicyRequired)]
    [InlineData(TimesheetsCommentPolicyDecision.Excluded)]
    [InlineData(TimesheetsCommentPolicyDecision.Redacted)]
    public async Task PersistedAndRebuiltEvidenceOmitsCommentsWithoutProjectionPermission(
        TimesheetsCommentPolicyDecision decision)
    {
        TimeEntryComment original = new("private recording", TimeEntryCommentPolicy.SensitiveDefault with
        {
            ProjectionInclusion = decision
        });
        TimeEntryComment changed = new("private adjustment", TimeEntryCommentPolicy.SensitiveDefault with
        {
            ProjectionInclusion = decision
        });
        TimeEntryComment corrected = new("private correction", TimeEntryCommentPolicy.SensitiveDefault with
        {
            ProjectionInclusion = decision
        });
        TimeEntryRecorded recorded = Recorded() with { Comment = original };
        TimeEntryAdjustedThroughMagicLink adjustment = Adjusted() with
        {
            PreviousValues = Adjusted().PreviousValues with { Comment = original },
            AdjustedValues = Adjusted().AdjustedValues with { Comment = changed }
        };
        TimeEntryCorrected correction = Corrected(adjustment.AdjustedValues,
            adjustment.AdjustedValues with { Comment = corrected });
        ProjectionRequest request = Request(Event(1, recorded), Event(2, adjustment),
            Event(3, Submitted()), Event(4, Rejected()), Event(5, correction));
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);

        (await handler.ProjectAsync(request, "policy-live", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        TimeEntryEvidenceReadModel selected = store.Get();
        selected.Comment.ShouldBeNull();
        selected.CommentProjectionState.ShouldBe(TimesheetsCommentPolicyDecision.PolicyRequired);
        selected.ExternalAdjustment.ShouldNotBeNull().PreviousValues.Comment.ShouldBeNull();
        selected.ExternalAdjustment.AdjustedValues.Comment.ShouldBeNull();
        selected.Correction.ShouldNotBeNull().PreviousValues.Comment.ShouldBeNull();
        selected.Correction.CorrectedValues.Comment.ShouldBeNull();
        string selectedJson = store.GetPacked().ToJsonString();
        selectedJson.ShouldNotContain("private recording");
        selectedJson.ShouldNotContain("private adjustment");
        selectedJson.ShouldNotContain("private correction");

        var rebuiltStore = new InMemoryReadModelStore();
        DomainProjectionRebuildPlan plan = await new TimeEntryEvidenceProjectionHandler(rebuiltStore)
            .PrepareRebuildAsync(request, "policy-rebuild", TestContext.Current.CancellationToken);
        var batch = new ReadModelBatch(new ReadModelBatchScope(
            TimeEntryEvidenceReadModelAddress.StateStoreName, "tenant-1", "timesheets", "entry-1",
            TimeEntryEvidenceProjection.ProjectionName, "policy-rebuild"), plan.Operations);
        (await rebuiltStore.ExecuteAsync(batch, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        string key = TimeEntryEvidenceReadModelAddress.StateKey("tenant-1", new TimeEntryId("entry-1"));
        JsonObject rebuilt = (await rebuiltStore.GetAsync<JsonObject>(
            TimeEntryEvidenceReadModelAddress.StateStoreName, key, TestContext.Current.CancellationToken))
            .Value.ShouldNotBeNull();
        rebuilt.ToJsonString().ShouldNotContain("private");
        TimeEntryEvidenceCompactStorage.Unpack(rebuilt).CommentProjectionState
            .ShouldBe(TimesheetsCommentPolicyDecision.PolicyRequired);
    }

    [Fact]
    public async Task AllowedCommentRemainsInSelectedEvidence()
    {
        TimeEntryComment allowed = new("approved note", TimeEntryCommentPolicy.SensitiveDefault with
        {
            ProjectionInclusion = TimesheetsCommentPolicyDecision.Allowed,
            ServerPublicationVersion = TimeEntryCommentPublicationIssuer.CurrentVersion,
            ServerProjectionInclusion = TimesheetsCommentPolicyDecision.Allowed
        });
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store, new TimesheetsEvidencePolicyOptions
        {
            CommentSensitivityPolicyConfigured = true,
            ProjectionCommentsAllowed = true
        });
        (await handler.ProjectAsync(Request(Event(1, Recorded() with { Comment = allowed })),
            "allowed-comment", TestContext.Current.CancellationToken)).Status
            .ShouldBe(ProjectionDispatchStatus.Completed);

        store.Get().Comment.ShouldBe(allowed);
        store.Get().CommentProjectionState.ShouldBe(TimesheetsCommentPolicyDecision.Allowed);
        store.GetPacked().ToJsonString().ShouldContain("approved note");
    }

    [Fact]
    public async Task PublicationPolicyVersionMigratesSameLineageOnlyAfterReplay()
    {
        TimeEntryComment issued = new("authorized text", TimeEntryCommentPolicy.SensitiveDefault with
        {
            ServerPublicationVersion = TimeEntryCommentPublicationIssuer.CurrentVersion,
            ServerProjectionInclusion = TimesheetsCommentPolicyDecision.Allowed,
            ProjectionInclusion = TimesheetsCommentPolicyDecision.Allowed
        });
        ProjectionRequest request = Request(Event(1, Recorded() with { Comment = issued }));
        var store = new TestStore();
        (await new TimeEntryEvidenceProjectionHandler(store).ProjectAsync(request, "denied-policy",
            TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        store.Get().Comment.ShouldBeNull();
        store.Get().CommentPublicationPolicyVersion.ShouldBe(1);
        store.GetPacked().ToJsonString().ShouldNotContain("authorized text");

        var enabled = new TimeEntryEvidenceProjectionHandler(store, new TimesheetsEvidencePolicyOptions
        {
            CommentSensitivityPolicyConfigured = true,
            ProjectionCommentsAllowed = true
        });
        // A newly configured handler sees only the old selected row until it replays the owner.
        TimeEntryEvidenceCompactStorage.Unpack(store.GetPacked()).Comment.ShouldBeNull();
        (await enabled.ProjectAsync(request, "enabled-replay", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        store.Get().Comment.ShouldBe(issued);
        store.Get().CommentPublicationPolicyVersion.ShouldBe(2);
        DomainProjectionRebuildPlan rebuilt = await enabled.PrepareRebuildAsync(request with
        {
            RebuildSourceHeadSequence = 1
        }, "enabled-rebuild", TestContext.Current.CancellationToken);
        rebuilt.Operations.ShouldHaveSingleItem();

        // Restarting with a disabled policy strips the selected text before any replay.
        (await new TimeEntryEvidenceProjectionHandler(store).ProjectAsync(request, "disabled-replay",
            TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        store.Get().Comment.ShouldBeNull();
        store.Get().CommentPublicationPolicyVersion.ShouldBe(1);
    }

    [Fact]
    public async Task EnabledPolicyRequiresFullOwnerReplayBeforeAppendingSuffix()
    {
        TimeEntryComment issued = new("authorized text", TimeEntryCommentPolicy.SensitiveDefault with
        {
            ServerPublicationVersion = TimeEntryCommentPublicationIssuer.CurrentVersion,
            ServerProjectionInclusion = TimesheetsCommentPolicyDecision.Allowed,
            ProjectionInclusion = TimesheetsCommentPolicyDecision.Allowed
        });
        ProjectionEventDto recorded = Event(1, Recorded() with { Comment = issued });
        ProjectionEventDto unrelated = Event(2, new { Other = "ignored" });
        var store = new TestStore();
        (await new TimeEntryEvidenceProjectionHandler(store).ProjectAsync(Request(recorded),
            "disabled-recording", TestContext.Current.CancellationToken)).Status
            .ShouldBe(ProjectionDispatchStatus.Completed);
        JsonObject disabled = store.GetPacked();
        disabled.ToJsonString().ShouldNotContain("authorized text");

        var enabled = new TimeEntryEvidenceProjectionHandler(store, new TimesheetsEvidencePolicyOptions
        {
            CommentSensitivityPolicyConfigured = true,
            ProjectionCommentsAllowed = true
        });
        (await enabled.ProjectAsync(Request(unrelated), "suffix-needs-replay",
            TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        JsonNode.DeepEquals(disabled, store.GetPacked()).ShouldBeTrue();

        (await enabled.ProjectAsync(Request(recorded, unrelated), "full-owner-replay",
            TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        store.Get().Comment.ShouldBe(issued);
        store.Get().CommentPublicationPolicyVersion.ShouldBe(2);
        store.Get().EventPayloadLineage.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RemovingCurrentCommentClearsAllowedProjectionState()
    {
        TimeEntryComment issued = new("authorized text", TimeEntryCommentPolicy.SensitiveDefault with
        {
            ServerPublicationVersion = TimeEntryCommentPublicationIssuer.CurrentVersion,
            ServerProjectionInclusion = TimesheetsCommentPolicyDecision.Allowed,
            ProjectionInclusion = TimesheetsCommentPolicyDecision.Allowed
        });
        TimeEntryAdjustedThroughMagicLink adjustment = Adjusted() with
        {
            PreviousValues = Adjusted().PreviousValues with { Comment = issued },
            AdjustedValues = Adjusted().AdjustedValues with { Comment = null }
        };
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store, new TimesheetsEvidencePolicyOptions
        {
            CommentSensitivityPolicyConfigured = true,
            ProjectionCommentsAllowed = true
        });
        ProjectionRequest request = Request(Event(1, Recorded() with { Comment = issued }),
            Event(2, adjustment));
        (await handler.ProjectAsync(request, "comment-removed", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        store.Get().Comment.ShouldBeNull();
        store.Get().CommentProjectionState.ShouldBe(TimesheetsCommentPolicyDecision.Unknown);
    }

    [Fact]
    public async Task CallerAllowedCommentWithoutTrustedPolicyIsAbsentFromSelectedEvidence()
    {
        TimeEntryComment submitted = new("caller private note", TimeEntryCommentPolicy.SensitiveDefault with
        {
            ProjectionInclusion = TimesheetsCommentPolicyDecision.Allowed
        });
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);

        (await handler.ProjectAsync(Request(Event(1, Recorded() with { Comment = submitted })),
            "untrusted-allowed-comment", TestContext.Current.CancellationToken)).Status
            .ShouldBe(ProjectionDispatchStatus.Completed);

        store.Get().Comment.ShouldBeNull();
        store.Get().CommentProjectionState.ShouldBe(TimesheetsCommentPolicyDecision.PolicyRequired);
        store.GetPacked().ToJsonString().ShouldNotContain("caller private note");
    }

    [Fact]
    public async Task CallerAllowedNestedCommentsAreAbsentFromPersistedAdjustmentAndCorrection()
    {
        TimeEntryComment original = new("private recording", TimeEntryCommentPolicy.SensitiveDefault with
        {
            ProjectionInclusion = TimesheetsCommentPolicyDecision.Allowed
        });
        TimeEntryComment changed = new("private adjustment", TimeEntryCommentPolicy.SensitiveDefault with
        {
            ProjectionInclusion = TimesheetsCommentPolicyDecision.Allowed
        });
        TimeEntryComment corrected = new("private correction", TimeEntryCommentPolicy.SensitiveDefault with
        {
            ProjectionInclusion = TimesheetsCommentPolicyDecision.Allowed
        });
        TimeEntryAdjustedThroughMagicLink adjustment = Adjusted() with
        {
            PreviousValues = Adjusted().PreviousValues with { Comment = original },
            AdjustedValues = Adjusted().AdjustedValues with { Comment = changed }
        };
        TimeEntryCorrected correction = Corrected(adjustment.AdjustedValues,
            adjustment.AdjustedValues with { Comment = corrected });
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);

        (await handler.ProjectAsync(Request(Event(1, Recorded() with { Comment = original }),
                Event(2, adjustment), Event(3, Submitted()), Event(4, Rejected()),
                Event(5, correction)), "untrusted-nested-comments", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);

        TimeEntryEvidenceReadModel selected = store.Get();
        selected.Comment.ShouldBeNull();
        selected.CommentProjectionState.ShouldBe(TimesheetsCommentPolicyDecision.PolicyRequired);
        selected.ExternalAdjustment.ShouldNotBeNull().PreviousValues.Comment.ShouldBeNull();
        selected.ExternalAdjustment.AdjustedValues.Comment.ShouldBeNull();
        selected.Correction.ShouldNotBeNull().PreviousValues.Comment.ShouldBeNull();
        selected.Correction.CorrectedValues.Comment.ShouldBeNull();
        string persisted = store.GetPacked().ToJsonString();
        persisted.ShouldNotContain("private recording");
        persisted.ShouldNotContain("private adjustment");
        persisted.ShouldNotContain("private correction");
    }

    [Fact]
    public async Task LegacySelectedCommentIsStrippedWhenExtended()
    {
        TimeEntryComment excluded = new("legacy private note", TimeEntryCommentPolicy.SensitiveDefault with
        {
            ProjectionInclusion = TimesheetsCommentPolicyDecision.Excluded
        });
        TimeEntryRecorded recorded = Recorded() with { Comment = excluded };
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        (await handler.ProjectAsync(Request(Event(1, recorded)), "recorded",
            TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        TimeEntryEvidenceReadModel legacy = store.Get() with
        {
            Comment = excluded,
            CommentProjectionState = TimesheetsCommentPolicyDecision.Unknown,
            EventLineage = store.Get().EventLineage,
            EventPayloadLineage = [LegacyDigest(Event(1, recorded))],
            PackedEventLineage = null,
            PackedPayloadLineage = null
        };
        store.SeedPacked(JsonSerializer.SerializeToNode(legacy, s_jsonOptions)!.AsObject());
        store.GetPacked().ToJsonString().ShouldContain("legacy private note");

        ProjectionRequest extended = Request(Event(1, recorded), Event(2, Submitted()));
        DomainProjectionRebuildPlan streamed = await handler.PrepareStreamedRebuildAsync(
            extended with { Events = [] }, Stream(extended.Events), "streamed-legacy",
            TestContext.Current.CancellationToken);
        JsonObject streamedRow = JsonNode.Parse(streamed.Operations.Single().CanonicalValue.Span)!.AsObject();
        streamedRow.ToJsonString().ShouldNotContain("legacy private note");

        (await handler.ProjectAsync(extended,
            "migrate-legacy", TestContext.Current.CancellationToken)).Status
            .ShouldBe(ProjectionDispatchStatus.Completed);
        store.GetPacked().ToJsonString().ShouldNotContain("legacy private note");
        store.Get().Comment.ShouldBeNull();
        store.Get().CommentProjectionState.ShouldBe(TimesheetsCommentPolicyDecision.PolicyRequired);
    }

    [Fact]
    public async Task StreamedRebuildMatchesArrayFoldAndPreservesSourceErasure()
    {
        ProjectionRequest request = Request(Event(1, Recorded()), Event(2, Submitted()), Event(3, Rejected()));
        var handler = new TimeEntryEvidenceProjectionHandler(new InMemoryReadModelStore());
        DomainProjectionRebuildPlan array = await handler.PrepareRebuildAsync(request,
            "array-plan", TestContext.Current.CancellationToken);
        DomainProjectionRebuildPlan streamed = await handler.PrepareStreamedRebuildAsync(
            request with { Events = [] }, Stream(request.Events), "stream-plan",
            TestContext.Current.CancellationToken);
        streamed.Operations.Single().CanonicalValue.Span.SequenceEqual(
            array.Operations.Single().CanonicalValue.Span).ShouldBeTrue();

        ProjectionRequest erased = Request() with { RebuildSourceErased = true };
        DomainProjectionRebuildPlan deletion = await handler.PrepareStreamedRebuildAsync(
            erased, Stream([]), "stream-erase", TestContext.Current.CancellationToken);
        deletion.Operations.Single().Kind.ShouldBe(ReadModelBatchOperationKind.Delete);
    }

    private static async IAsyncEnumerable<ProjectionEventDto> Stream(ProjectionEventDto[] events)
    {
        foreach (ProjectionEventDto item in events)
        {
            yield return item;
            await Task.Yield();
        }
    }

    [Fact]
    public async Task Invalid_transitions_and_second_recording_do_not_publish()
    {
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        ProjectionRequest[] invalid =
        [
            Request(Event(1, Submitted()), Event(2, Recorded())),
            Request(Event(1, Recorded()), Event(2, Recorded())),
            Request(Event(1, Recorded()), Event(2, Approved()))
        ];

        foreach (ProjectionRequest request in invalid)
        {
            var result = await handler.ProjectAsync(request, "invalid", TestContext.Current.CancellationToken);
            result.Status.ShouldBe(ProjectionDispatchStatus.Failed);
            store.HasValue.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task Missing_recording_fields_and_mismatched_approval_states_fail_without_publishing()
    {
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        ProjectionRequest[] invalid =
        [
            Request(Event(1, Recorded() with { Contributor = null! })),
            Request(Event(1, Recorded() with { ActivityTypeId = null! })),
            Request(Event(1, Recorded() with { DurationMinutes = 0 })),
            Request(Event(1, Recorded() with { ApprovalState = TimeEntryApprovalState.Submitted })),
            Request(Event(1, Recorded()), Event(2, Submitted() with { ApprovalState = TimeEntryApprovalState.Draft })),
            Request(Event(1, Recorded()), Event(2, Submitted()), Event(3, Approved() with { ApprovalState = TimeEntryApprovalState.Rejected }))
        ];

        foreach (ProjectionRequest request in invalid)
        {
            var result = await handler.ProjectAsync(
                request, "invalid-fields", TestContext.Current.CancellationToken);
            result.Status.ShouldBe(ProjectionDispatchStatus.Failed);
            store.HasValue.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task Malformed_recognized_effects_never_publish_fresh_evidence()
    {
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        var confirmation = new TimeEntryContributorConfirmed(
            new TimeEntryId("entry-1"), new PartyReference("contributor-1"),
            new TenantReference("tenant-1"), DateTimeOffset.UtcNow,
            new ExternalContributionSource("magic-link", "capability-1"));
        ProjectionRequest[] invalid =
        [
            Request(Event(1, Recorded()), Event(2, Submitted() with { Tenant = null! })),
            Request(Event(1, Recorded()), Event(2, Submitted() with { SubmittedAtUtc = default })),
            Request(Event(1, Recorded()), Event(2, confirmation with { Contributor = null! })),
            Request(Event(1, Recorded()), Event(2, confirmation with { ConfirmedAtUtc = default })),
            Request(Event(1, Recorded()), Event(2, confirmation with { Source = null! })),
            Request(Event(1, Recorded()), Event(2, confirmation with { Contributor = new PartyReference("other-contributor") })),
            Request(Event(1, Recorded() with { ContributorCategory = ContributorCategory.Employee }), Event(2, confirmation)),
            Request(Event(1, Recorded()), Event(2, Adjusted() with { PreviousValues = null! })),
            Request(Event(1, Recorded()), Event(2, Adjusted() with { PreviousValues = Adjusted().PreviousValues with { DurationMinutes = 999 } })),
            Request(Event(1, Recorded()), Event(2, Adjusted() with { AdjustedValues = null! })),
            Request(Event(1, Recorded()), Event(2, Adjusted() with { ActivityTypeScope = ActivityTypeScope.Project })),
            Request(Event(1, Recorded()), Event(2, Adjusted() with
            {
                AdjustedValues = Adjusted().AdjustedValues with { ContributorCategory = (ContributorCategory)99 }
            })),
            Request(Event(1, Recorded()), Event(2, Adjusted() with { AdjustedAtUtc = default })),
            Request(Event(1, Recorded()), Event(2, Submitted()), Event(3, Approved() with { Approver = null! })),
            Request(Event(1, Recorded()), Event(2, Submitted()), Event(3, Approved() with { AuthoritySource = null! })),
            Request(Event(1, Recorded()), Event(2, Submitted()), Event(3, Approved() with
            {
                AuthoritySource = Approved().AuthoritySource with { Action = (ApprovalAuthorityAction)99 }
            })),
            Request(Event(1, Recorded()), Event(2, Submitted()), Event(3, Approved() with
            {
                AuthoritySource = Approved().AuthoritySource with { Source = (ApprovalAuthoritySource)99 }
            })),
            Request(Event(1, Recorded()), Event(2, Submitted()), Event(3, Approved() with
            {
                AuthoritySource = Approved().AuthoritySource with { DecisionState = (ApprovalAuthorityDecisionState)99 }
            })),
            Request(Event(1, Recorded()), Event(2, Submitted()), Event(3, Approved() with
            {
                AuthoritySource = Approved().AuthoritySource with { Action = ApprovalAuthorityAction.EntryRejection }
            })),
            Request(Event(1, Recorded()), Event(2, Submitted()), Event(3, Approved() with { DecidedAtUtc = default }))
        ];

        foreach (ProjectionRequest request in invalid)
        {
            var result = await handler.ProjectAsync(
                request, "malformed-effect", TestContext.Current.CancellationToken);
            result.Status.ShouldBe(ProjectionDispatchStatus.Failed);
            store.HasValue.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task Invalid_nested_recording_metrics_and_comment_policy_do_not_publish()
    {
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        TimeEntryRecorded[] invalid =
        [
            Recorded() with { AiMetrics = AiEffortMetrics.Unavailable with { WallClockDurationMilliseconds = -1 } },
            Recorded() with { AiMetrics = AiEffortMetrics.Unavailable with { Availability = (AiMetricAvailability)99 } },
            Recorded() with { AiMetrics = AiEffortMetrics.Unavailable with { TokenAvailability = (AiTokenMetricAvailability)99 } },
            Recorded() with { AiMetrics = AiEffortMetrics.Unavailable with
                { Source = AiEffortMetricSourceMetadata.Unavailable with { SourceCategory = (AiEffortMetricSourceCategory)99 } } },
            Recorded() with { ContributorCategory = (ContributorCategory)99 },
            Recorded() with { Comment = new TimeEntryComment("sensitive",
                TimeEntryCommentPolicy.SensitiveDefault with { ProjectionInclusion = TimesheetsCommentPolicyDecision.Unknown }) },
            Recorded() with { Comment = new TimeEntryComment("sensitive",
                TimeEntryCommentPolicy.SensitiveDefault with { InternalDisplay = (TimesheetsCommentPolicyDecision)99 }) },
            Recorded() with { Comment = new TimeEntryComment("sensitive",
                TimeEntryCommentPolicy.SensitiveDefault with { ExternalConfirmationDisplay = (TimesheetsCommentPolicyDecision)99 }) },
            Recorded() with { Comment = new TimeEntryComment("sensitive",
                TimeEntryCommentPolicy.SensitiveDefault with { ServerExternalConfirmationDisplay = (TimesheetsCommentPolicyDecision)99 }) },
            Recorded() with { Comment = new TimeEntryComment("sensitive",
                TimeEntryCommentPolicy.SensitiveDefault with { ProjectionInclusion = (TimesheetsCommentPolicyDecision)99 }) },
            Recorded() with { Comment = new TimeEntryComment("sensitive",
                TimeEntryCommentPolicy.SensitiveDefault with { ExportInclusion = (TimesheetsCommentPolicyDecision)99 }) },
            Recorded() with { Comment = new TimeEntryComment("sensitive",
                TimeEntryCommentPolicy.SensitiveDefault with { SupportDiagnostics = (TimesheetsCommentPolicyDecision)99 }) },
            Recorded() with { Comment = new TimeEntryComment("sensitive",
                TimeEntryCommentPolicy.SensitiveDefault with { RedactionRequirement = (TimesheetsCommentRedactionRequirement)99 }) },
            Recorded() with { Comment = new TimeEntryComment("sensitive",
                TimeEntryCommentPolicy.SensitiveDefault with { RetentionCategory = (TimesheetsEvidenceRetentionCategory)99 }) }
        ];

        foreach (TimeEntryRecorded recorded in invalid)
        {
            var result = await handler.ProjectAsync(Request(Event(1, recorded)), "invalid-nested",
                TestContext.Current.CancellationToken);
            result.Status.ShouldBe(ProjectionDispatchStatus.Failed);
            store.HasValue.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task Same_entry_id_in_two_tenants_uses_distinct_canonical_store_keys()
    {
        var store = new TestStore();
        var handler = new TimeEntryEvidenceProjectionHandler(store);
        _ = await handler.ProjectAsync(Request(Event(1, Recorded())), "tenant-1", TestContext.Current.CancellationToken);
        _ = await handler.ProjectAsync(
            new ProjectionRequest("tenant-2", "timesheets", "entry-1",
                [Event(1, Recorded() with { DurationMinutes = 90 })]),
            "tenant-2", TestContext.Current.CancellationToken);

        store.Get("tenant-1").DurationMinutes.ShouldBe(45);
        store.Get("tenant-2").DurationMinutes.ShouldBe(90);
        store.Keys.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Read_or_write_outage_is_retryable_without_publishing(bool readOutage)
    {
        var store = new TestStore { FailRead = readOutage, FailWrite = !readOutage };
        var handler = new TimeEntryEvidenceProjectionHandler(store);

        var result = await handler.ProjectAsync(
            Request(Event(1, Recorded())),
            "outage",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        store.HasValue.ShouldBeFalse();
    }

    [Fact]
    public void Aggregate_owned_address_rejects_colon_components()
    {
        Should.Throw<ArgumentException>(() => TimeEntryEvidenceReadModelAddress.StateKey(
            "tenant:other", new TimeEntryId("entry-1")));
        Should.Throw<ArgumentException>(() => TimeEntryEvidenceReadModelAddress.StateKey(
            "tenant-1", new TimeEntryId("entry:other")));
        TimeEntryEvidenceReadModelAddress.StateKey("tenant-1", new TimeEntryId("entry-1"))
            .ShouldBe("readmodel:tenant-1:timesheets:time-entry-evidence:entry-1:evidence");
    }

    private static ProjectionRequest Request(params ProjectionEventDto[] events)
        => new("tenant-1", "timesheets", "entry-1", events)
        {
            RebuildSourceHeadSequence = events.LongLength
        };

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

    private static string LegacyDigest(ProjectionEventDto item)
        => Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(item.EventTypeName + "\0").Concat(item.Payload).ToArray()));

    private static TimeEntryRecorded Recorded()
        => new(
            new TimeEntryId("entry-1"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            new PartyReference("contributor-1"),
            new ActivityTypeId("activity-1"),
            ActivityTypeScope.Tenant,
            new DateOnly(2026, 6, 19),
            45,
            BillableState.Billable,
            TimeEntryApprovalState.Draft,
            ContributorCategory.ExternalContributor,
            null);

    private static TimeEntrySubmitted Submitted()
        => new(
            new TimeEntryId("entry-1"),
            new PartyReference("submitter-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 12, 30, 0, TimeSpan.Zero),
            new TimeEntrySubmissionId("submission-1"),
            TimeEntrySubmissionScope.SelectedEntries,
            TimeEntryApprovalState.Submitted);

    private static TimeEntryApproved Approved()
        => new(
            new TimeEntryId("entry-1"),
            new PartyReference("approver-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            new TimeEntryApprovalDecisionId("decision-1"),
            TimeEntryApprovalState.Approved,
            new ApprovalAuthoritySourceAttribution(
                ApprovalAuthorityAction.EntryApproval,
                ApprovalAuthoritySource.ProjectApprover,
                ApprovalAuthorityDecisionState.Allowed,
                "timesheets.approval-authority.v1",
                "v1",
                ProjectionFreshnessMetadata.Fresh),
            TimeEntryApprovalScope.IndividualEntry);

    private static TimeEntryRejected Rejected()
        => new(
            new TimeEntryId("entry-1"),
            new PartyReference("approver-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            new TimeEntryApprovalDecisionId("rejection-1"),
            TimeEntryApprovalState.Rejected,
            Approved().AuthoritySource with { Action = ApprovalAuthorityAction.EntryRejection },
            TimeEntryApprovalScope.IndividualEntry,
            new TimeEntryRejectionReason("Needs correction"));

    private static TimeEntryCorrected Corrected(TimeEntryCorrectionValues previous,
        TimeEntryCorrectionValues next)
        => new(new TimeEntryId("entry-1"), new TimeEntryCorrectionId("correction-1"),
            new TenantReference("tenant-1"), new PartyReference("approver-1"),
            new DateTimeOffset(2026, 6, 19, 14, 0, 0, TimeSpan.Zero),
            previous, next, new TimeEntryRejectionReason("Needs correction"),
            new TimeEntryApprovalDecisionId("rejection-1"), TimeEntryApprovalState.Draft,
            TimeEntryCorrectionState.Corrected);

    private static TimeEntryAdjustedThroughMagicLink Adjusted()
        => new(
            new TimeEntryId("entry-1"),
            new TenantReference("tenant-1"),
            new PartyReference("contributor-1"),
            new DateTimeOffset(2026, 6, 19, 12, 45, 0, TimeSpan.Zero),
            ActivityTypeScope.Tenant,
            AdjustmentValues(45, new DateOnly(2026, 6, 19)) with { ActivityTypeScope = ActivityTypeScope.Tenant },
            AdjustmentValues(60, new DateOnly(2026, 6, 20)) with { ActivityTypeScope = ActivityTypeScope.Tenant },
            new ExternalContributionSource("magic-link", "capability-1"));

    private static TimeEntryCorrectionValues AdjustmentValues(int minutes, DateOnly date)
        => new(
            TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            new PartyReference("contributor-1"),
            new ActivityTypeId("activity-1"),
            date,
            minutes,
            BillableState.Billable,
            ContributorCategory.ExternalContributor,
            null);

    private sealed class TestStore : IReadModelStore
    {
        private readonly Dictionary<string, (JsonObject Value, TimeEntryEvidenceReadModel Model, int Version)> _rows = new(StringComparer.Ordinal);

        public bool FailRead { get; init; }

        public bool FailWrite { get; init; }

        public bool HasValue => _rows.Count != 0;

        public IReadOnlyCollection<string> Keys => _rows.Keys;

        public TimeEntryEvidenceReadModel Get(string tenantId = "tenant-1")
            => _rows[TimeEntryEvidenceReadModelAddress.StateKey(tenantId, new TimeEntryId("entry-1"))].Model;

        public JsonObject GetPacked() => (JsonObject)_rows[
            TimeEntryEvidenceReadModelAddress.StateKey("tenant-1", new TimeEntryId("entry-1"))].Value.DeepClone();

        public void SeedPacked(JsonObject value)
        {
            string key = TimeEntryEvidenceReadModelAddress.StateKey("tenant-1", new TimeEntryId("entry-1"));
            _rows[key] = (value, _rows[key].Model, _rows[key].Version + 1);
        }

        public void Seed(TimeEntryEvidenceReadModel value)
        {
            string key = TimeEntryEvidenceReadModelAddress.StateKey("tenant-1", new TimeEntryId("entry-1"));
            _rows[key] = (TimeEntryEvidenceCompactStorage.Pack(value), value,
                _rows.TryGetValue(key, out var current) ? current.Version + 1 : 1);
        }

        public Task<ReadModelEntry<TValue>> GetAsync<TValue>(
            string storeName, string key, CancellationToken cancellationToken = default)
            where TValue : class
        {
            if (FailRead) throw new HttpRequestException("Store unavailable.");
            return Task.FromResult(_rows.TryGetValue(key, out var row)
                ? new ReadModelEntry<TValue>((row.Value as TValue ?? row.Model as TValue), row.Version.ToString())
                : new ReadModelEntry<TValue>(null, null));
        }

        public Task SaveAsync<TValue>(
            string storeName, string key, TValue value, CancellationToken cancellationToken = default)
            where TValue : class
        {
            JsonObject packed = value as JsonObject ?? throw new InvalidOperationException("Wrong model.");
            TimeEntryEvidenceReadModel model = _rows.TryGetValue(key, out var current)
                && JsonNode.DeepEquals(current.Value, packed)
                    ? current.Model
                    : TimeEntryEvidenceCompactStorage.Unpack(packed);
            _rows[key] = (packed, model, current.Version + 1);
            return Task.CompletedTask;
        }

        public Task<bool> TrySaveAsync<TValue>(
            string storeName, string key, TValue value, string etag, CancellationToken cancellationToken = default)
            where TValue : class
        {
            if (FailWrite) throw new HttpRequestException("Store unavailable.");
            if (etag != (_rows.TryGetValue(key, out var current) ? current.Version.ToString() : string.Empty)) return Task.FromResult(false);
            JsonObject packed = value as JsonObject ?? throw new InvalidOperationException("Wrong model.");
            TimeEntryEvidenceReadModel model = JsonNode.DeepEquals(current.Value, packed)
                ? current.Model
                : TimeEntryEvidenceCompactStorage.Unpack(packed);
            _rows[key] = (packed, model, current.Version + 1);
            return Task.FromResult(true);
        }
    }
}
