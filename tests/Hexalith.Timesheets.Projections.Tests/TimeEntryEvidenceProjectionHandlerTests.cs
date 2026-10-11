using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections.TimeEntries;
using Hexalith.Timesheets.Server.MagicLinks;

using Shouldly;

namespace Hexalith.Timesheets.Projections.Tests;

public sealed class TimeEntryEvidenceProjectionHandlerTests
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

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
        replay.Status.ShouldBe(ProjectionDispatchStatus.Completed);
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

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        store.Get().ShouldBeSameAs(full);
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
            Request(Event(1, Recorded()), Event(2, Adjusted() with { AdjustedAtUtc = default })),
            Request(Event(1, Recorded()), Event(2, Submitted()), Event(3, Approved() with { Approver = null! })),
            Request(Event(1, Recorded()), Event(2, Submitted()), Event(3, Approved() with { AuthoritySource = null! })),
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
            Recorded() with { Comment = new TimeEntryComment("sensitive",
                TimeEntryCommentPolicy.SensitiveDefault with { ProjectionInclusion = TimesheetsCommentPolicyDecision.Unknown }) }
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
        => new("tenant-1", "timesheets", "entry-1", events);

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
        private readonly Dictionary<string, (TimeEntryEvidenceReadModel Value, int Version)> _rows = new(StringComparer.Ordinal);

        public bool FailRead { get; init; }

        public bool FailWrite { get; init; }

        public bool HasValue => _rows.Count != 0;

        public IReadOnlyCollection<string> Keys => _rows.Keys;

        public TimeEntryEvidenceReadModel Get(string tenantId = "tenant-1")
            => _rows[TimeEntryEvidenceReadModelAddress.StateKey(tenantId, new TimeEntryId("entry-1"))].Value;

        public void Seed(TimeEntryEvidenceReadModel value)
        {
            string key = TimeEntryEvidenceReadModelAddress.StateKey("tenant-1", new TimeEntryId("entry-1"));
            _rows[key] = (value, _rows.TryGetValue(key, out var current) ? current.Version + 1 : 1);
        }

        public Task<ReadModelEntry<TValue>> GetAsync<TValue>(
            string storeName, string key, CancellationToken cancellationToken = default)
            where TValue : class
        {
            if (FailRead) throw new HttpRequestException("Store unavailable.");
            return Task.FromResult(_rows.TryGetValue(key, out var row)
                ? new ReadModelEntry<TValue>(row.Value as TValue, row.Version.ToString())
                : new ReadModelEntry<TValue>(null, null));
        }

        public Task SaveAsync<TValue>(
            string storeName, string key, TValue value, CancellationToken cancellationToken = default)
            where TValue : class
        {
            _rows[key] = (value as TimeEntryEvidenceReadModel ?? throw new InvalidOperationException("Wrong model."),
                _rows.TryGetValue(key, out var current) ? current.Version + 1 : 1);
            return Task.CompletedTask;
        }

        public Task<bool> TrySaveAsync<TValue>(
            string storeName, string key, TValue value, string etag, CancellationToken cancellationToken = default)
            where TValue : class
        {
            if (FailWrite) throw new HttpRequestException("Store unavailable.");
            if (etag != (_rows.TryGetValue(key, out var current) ? current.Version.ToString() : string.Empty)) return Task.FromResult(false);
            _rows[key] = (value as TimeEntryEvidenceReadModel ?? throw new InvalidOperationException("Wrong model."),
                current.Version + 1);
            return Task.FromResult(true);
        }
    }
}
