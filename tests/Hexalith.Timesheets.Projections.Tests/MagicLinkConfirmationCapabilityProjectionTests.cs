using System.Text.Json;

using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Projections.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks;

using Shouldly;

using CapabilityState = Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState;

namespace Hexalith.Timesheets.Projections.Tests;

public sealed class MagicLinkConfirmationCapabilityProjectionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("tenant-2")]
    [InlineData("TENANT-1")]
    [InlineData("tenant-1 ")]
    public void CapabilityRejectsCheckpointTenantDifferentFromIssuance(string? checkpointTenant)
    {
        foreach (ProjectionFreshness freshness in Enum.GetValues<ProjectionFreshness>())
        {
            var checkpoint = new TimesheetsProjectionCheckpoint(
                checkpointTenant!, MagicLinkConfirmationCapabilityProjection.ProjectionName, 1, freshness);
            InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => Projector().Project(
                CapabilityId(), [Event("m1", 1, Issued())], checkpoint, ObservedAtUtc()));
            exception.Message.ShouldNotContain("tenant-1");
            if (!string.IsNullOrWhiteSpace(checkpointTenant))
            {
                exception.Message.ShouldNotContain(checkpointTenant);
            }
        }
    }

    [Fact]
    public void CapabilityWithoutIssuanceStillReturnsNoModel()
    {
        Projector().Project(CapabilityId(), [],
            FreshCheckpoint(0) with { TenantId = "tenant-2" }, ObservedAtUtc()).ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CapabilityValidatesCheckpointAgainstSelectedIssuance(bool unrelatedFirst)
    {
        MagicLinkConfirmationCapabilityIssued unrelated = Issued() with
        {
            CapabilityId = new MagicLinkCapabilityId("other-capability"),
            Tenant = new TenantReference("tenant-2")
        };
        MagicLinkProjectionEvent[] events =
        [
            Event("unrelated", unrelatedFirst ? 1 : 2, unrelated),
            Event("selected", unrelatedFirst ? 2 : 1, Issued())
        ];
        TimesheetsProjectionCheckpoint checkpoint = FreshCheckpoint(2);

        MagicLinkConfirmationCapabilityReadModel? model = Projector().Project(
            CapabilityId(), events, checkpoint, ObservedAtUtc());

        model.ShouldNotBeNull();
        model.CapabilityId.ShouldBe(CapabilityId());
        model.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        Should.Throw<InvalidOperationException>(() => Projector().Project(
            CapabilityId(), events, checkpoint with { TenantId = "tenant-2" }, ObservedAtUtc()));
        Projector().Project(CapabilityId(), [Event("unrelated", 1, unrelated)],
            checkpoint, ObservedAtUtc()).ShouldBeNull();
    }

    [Fact]
    public void Projection_exposes_issued_state_with_text_badges_and_freshness()
    {
        MagicLinkConfirmationCapabilityReadModel? model = Projector().Project(
            CapabilityId(),
            [Event("m1", 1, Issued())],
            FreshCheckpoint(1),
            ObservedAtUtc());

        model.ShouldNotBeNull();
        model.CapabilityId.ShouldBe(CapabilityId());
        model.State.ShouldBe(CapabilityState.Issued);
        model.ExpiryState.ShouldBe(MagicLinkExpiryState.Active);
        model.StateBadgeText.ShouldBe("Issued");
        model.ExpiryBadgeText.ShouldBe("Active");
        model.ProjectionFreshness.Cursor.ShouldBe("1");
        model.IssueMetadata.ShouldBe(new MagicLinkAuditMetadata("timesheets", "issue-1"));
        Serialized(model).ShouldNotContain("token", Case.Insensitive);
    }

    [Fact]
    public void Projection_is_duplicate_tolerant_for_replayed_events()
    {
        MagicLinkProjectionEvent issued = Event("m1", 1, Issued());
        MagicLinkProjectionEvent revoked = Event("m2", 2, Revoked());

        MagicLinkConfirmationCapabilityReadModel? model = Projector().Project(
            CapabilityId(),
            [issued, issued, revoked, revoked],
            FreshCheckpoint(2),
            ObservedAtUtc());

        model.ShouldNotBeNull();
        model.State.ShouldBe(CapabilityState.Revoked);
        model.RevokedBy.ShouldBe(new PartyReference("operator-1"));
        model.StateBadgeText.ShouldBe("Revoked");
    }

    [Fact]
    public void Projection_rejects_terminal_mutations_after_revocation()
    {
        Should.Throw<InvalidOperationException>(() => Projector().Project(
            CapabilityId(),
            [
                Event("m3", 3, Expired()),
                Event("m2", 2, Revoked()),
                Event("m1", 1, Issued())
            ],
            FreshCheckpoint(3),
            ExpiresAtUtc()));
    }

    [Fact]
    public void Projection_exposes_expired_state_from_expiry_event()
    {
        MagicLinkConfirmationCapabilityReadModel? model = Projector().Project(
            CapabilityId(),
            [Event("m1", 1, Issued()), Event("m2", 2, Expired())],
            FreshCheckpoint(2),
            ExpiresAtUtc());

        model.ShouldNotBeNull();
        model.State.ShouldBe(CapabilityState.Expired);
        model.ExpiryState.ShouldBe(MagicLinkExpiryState.Expired);
        model.ExpiryBadgeText.ShouldBe("Expired");
        model.ExpiredAtUtc.ShouldBe(ExpiresAtUtc());
    }

    [Fact]
    public void Projection_rejects_terminal_mutations_after_use()
    {
        Should.Throw<InvalidOperationException>(() => Projector().Project(
            CapabilityId(),
            [
                Event("m1", 1, Issued()),
                Event("m2", 2, Used()),
                Event("m3", 3, Revoked())
            ],
            FreshCheckpoint(3),
            ObservedAtUtc()));
    }

    [Fact]
    public void Projection_exposes_safe_adjustment_use_outcome_category()
    {
        MagicLinkConfirmationCapabilityUsed used = Used() with { OutcomeCategory = "adjusted" };

        MagicLinkConfirmationCapabilityReadModel? model = Projector().Project(
            CapabilityId(),
            [Event("m1", 1, Issued()), Event("m2", 2, used)],
            FreshCheckpoint(2),
            ObservedAtUtc());

        model.ShouldNotBeNull();
        model.State.ShouldBe(CapabilityState.Used);
        model.UseOutcomeCategory.ShouldBe("adjusted");
        Serialized(model).ShouldNotContain("token", Case.Insensitive);
        Serialized(model).ShouldNotContain("hash", Case.Insensitive);
    }

    [Fact]
    public void Projection_reconciles_stored_cross_stream_terminal_before_issuance_sequence()
    {
        MagicLinkConfirmationCapabilityReadModel? model = Projector().Project(
            CapabilityId(),
            [
                Event("target-use", 2, new StoredMagicLinkUsed(Used())),
                Event("capability-issue", 3, new StoredMagicLinkIssued(Issued()))
            ],
            FreshCheckpoint(3),
            ObservedAtUtc());

        model.ShouldNotBeNull().State.ShouldBe(CapabilityState.Used);
    }

    [Fact]
    public void Projection_rejects_legacy_terminal_before_issuance_in_capability_stream()
    {
        Should.Throw<InvalidOperationException>(() => Projector().Project(
            CapabilityId(),
            [Event("legacy-terminal", 1, Revoked()), Event("late-issue", 2, Issued())],
            FreshCheckpoint(2),
            ObservedAtUtc()));
    }

    [Fact]
    public void Projection_rejects_repeated_physical_issuance_even_when_identical()
    {
        Should.Throw<InvalidOperationException>(() => Projector().Project(
            CapabilityId(),
            [Event("first-issue", 1, Issued()), Event("second-issue", 2, Issued())],
            FreshCheckpoint(2), ObservedAtUtc()));
    }

    [Fact]
    public void Projection_rejects_distinct_payloads_at_one_sequence_within_each_stream()
    {
        MagicLinkProjectionEvent issuance = Event("issue", 1, Issued());
        Projector().Project(CapabilityId(), [issuance, Event("", 1, Issued())],
            FreshCheckpoint(1), ObservedAtUtc()).ShouldNotBeNull();
        Should.Throw<InvalidOperationException>(() => Projector().Project(CapabilityId(),
            [issuance, Event("other", 1, Revoked())], FreshCheckpoint(1), ObservedAtUtc()));

        MagicLinkProjectionEvent ownerUse = Event("owner-use", 1, new StoredMagicLinkUsed(Used()));
        Projector().Project(CapabilityId(), [issuance, ownerUse],
            FreshCheckpoint(1), ObservedAtUtc()).ShouldNotBeNull().State.ShouldBe(CapabilityState.Used);
        Should.Throw<InvalidOperationException>(() => Projector().Project(CapabilityId(),
            [issuance, ownerUse, Event("owner-revoke", 1, new StoredMagicLinkRevoked(Revoked()))],
            FreshCheckpoint(1), ObservedAtUtc()));
    }

    [Theory]
    [InlineData("used", false, false)]
    [InlineData("used", false, true)]
    [InlineData("used", true, false)]
    [InlineData("used", true, true)]
    [InlineData("revoked", false, false)]
    [InlineData("revoked", false, true)]
    [InlineData("revoked", true, false)]
    [InlineData("revoked", true, true)]
    [InlineData("expired", false, false)]
    [InlineData("expired", false, true)]
    [InlineData("expired", true, false)]
    [InlineData("expired", true, true)]
    public void MixedCapabilityStreamsKeepIndependentSequenceSpaces(string terminal, bool stored, bool reverse)
    {
        var otherId = new MagicLinkCapabilityId("other-capability");
        var otherTenant = new TenantReference("tenant-2");
        var otherEntry = new TimeEntryId("other-entry");
        MagicLinkConfirmationCapabilityIssued unrelated = Issued() with
        {
            CapabilityId = otherId,
            Tenant = otherTenant,
            TimeEntryId = otherEntry
        };
        object selectedTerminal = terminal switch
        {
            "used" => stored ? new StoredMagicLinkUsed(Used()) : Used(),
            "revoked" => stored
                ? new StoredMagicLinkRevoked(Revoked() with { TimeEntryId = Issued().TimeEntryId }) : Revoked(),
            _ => stored
                ? new StoredMagicLinkExpired(Expired() with { TimeEntryId = Issued().TimeEntryId }) : Expired()
        };
        object unrelatedTerminal = terminal switch
        {
            "used" => Used() with { CapabilityId = otherId, Tenant = otherTenant, TimeEntryId = otherEntry },
            "revoked" => Revoked() with { CapabilityId = otherId, Tenant = otherTenant, TimeEntryId = otherEntry },
            _ => Expired() with { CapabilityId = otherId, Tenant = otherTenant, TimeEntryId = otherEntry }
        };
        if (stored)
        {
            unrelatedTerminal = unrelatedTerminal switch
            {
                MagicLinkConfirmationCapabilityUsed used => new StoredMagicLinkUsed(used),
                MagicLinkConfirmationCapabilityRevoked revoked => new StoredMagicLinkRevoked(revoked),
                MagicLinkConfirmationCapabilityExpired expired => new StoredMagicLinkExpired(expired),
                _ => throw new InvalidOperationException()
            };
        }

        MagicLinkProjectionEvent[] selected =
        [
            Event("selected-issue", 1, stored ? new StoredMagicLinkIssued(Issued()) : Issued()),
            Event("selected-terminal", 2, selectedTerminal)
        ];
        MagicLinkProjectionEvent[] mixed =
        [
            selected[0],
            Event("unrelated-issue", 1, stored ? new StoredMagicLinkIssued(unrelated) : unrelated),
            selected[0],
            selected[1],
            Event("unrelated-terminal", 2, unrelatedTerminal),
            selected[1]
        ];
        if (reverse) Array.Reverse(mixed);

        MagicLinkConfirmationCapabilityReadModel expected = Projector().Project(
            CapabilityId(), selected, FreshCheckpoint(2), ObservedAtUtc()).ShouldNotBeNull();
        MagicLinkConfirmationCapabilityReadModel actual = Projector().Project(
            CapabilityId(), mixed, FreshCheckpoint(2), ObservedAtUtc()).ShouldNotBeNull();

        actual.ShouldBe(expected);
        actual.State.ShouldBe(terminal switch
        {
            "used" => CapabilityState.Used,
            "revoked" => CapabilityState.Revoked,
            _ => CapabilityState.Expired
        });
    }

    [Fact]
    public void UnselectedContradictoryStreamsDoNotCreateAModel()
    {
        MagicLinkConfirmationCapabilityIssued other = Issued() with
        {
            CapabilityId = new MagicLinkCapabilityId("other-capability")
        };
        Projector().Project(CapabilityId(),
            [Event("other-issue", 1, other), Event("different-issue", 1, other with { Tenant = new("tenant-2") })],
            FreshCheckpoint(1), ObservedAtUtc()).ShouldBeNull();
    }

    [Theory]
    [InlineData("used", false)]
    [InlineData("used", true)]
    [InlineData("revoked", false)]
    [InlineData("revoked", true)]
    [InlineData("expired", false)]
    [InlineData("expired", true)]
    public void SelectedCapabilityStillRejectsMismatchedTerminalScope(string terminal, bool foreignTenant)
    {
        TenantReference tenant = foreignTenant ? new("tenant-2") : Issued().Tenant;
        TimeEntryId entry = foreignTenant ? Issued().TimeEntryId : new("other-entry");
        object payload = terminal switch
        {
            "used" => new StoredMagicLinkUsed(Used() with { Tenant = tenant, TimeEntryId = entry }),
            "revoked" => new StoredMagicLinkRevoked(Revoked() with { Tenant = tenant, TimeEntryId = entry }),
            _ => new StoredMagicLinkExpired(Expired() with { Tenant = tenant, TimeEntryId = entry })
        };

        Should.Throw<InvalidOperationException>(() => Projector().Project(CapabilityId(),
            [
                Event("unrelated-issue", 1, Issued() with { CapabilityId = new("other-capability") }),
                Event("issue", 1, Issued()),
                Event("unrelated-terminal", 2, new StoredMagicLinkUsed(Used() with { CapabilityId = new("other-capability") })),
                Event("bad-terminal", 2, payload)
            ],
            FreshCheckpoint(2), ObservedAtUtc()));
    }

    [Fact]
    public void UnknownPayloadStillConflictsWithSelectedEvidenceAtTheSameSequence()
    {
        Should.Throw<InvalidOperationException>(() => Projector().Project(CapabilityId(),
            [Event("issue", 1, Issued()), Event("unknown", 1, new { Unknown = true })],
            FreshCheckpoint(1), ObservedAtUtc()));
    }

    private static string Serialized(MagicLinkConfirmationCapabilityReadModel model)
        => JsonSerializer.Serialize(model, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static MagicLinkConfirmationCapabilityProjection Projector() => new();

    private static MagicLinkProjectionEvent Event(string messageId, long sequenceNumber, object payload)
        => new(messageId, sequenceNumber, payload);

    private static TimesheetsProjectionCheckpoint FreshCheckpoint(long sequenceNumber)
        => new("tenant-1", MagicLinkConfirmationCapabilityProjection.ProjectionName, sequenceNumber, ProjectionFreshness.Fresh);

    private static MagicLinkConfirmationCapabilityIssued Issued()
        => new(
            CapabilityId(),
            new TenantReference("tenant-1"),
            new PartyReference("party-1"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            new ActivityTypeId("activity-type-1"),
            new TimeEntryId("time-entry-1"),
            MagicLinkTargetKind.ProposedTimeEntry,
            MagicLinkAllowedAction.Confirm,
            new MagicLinkTokenHash("hash-only"),
            ExpiresAtUtc(),
            new PartyReference("operator-1"),
            IssuedAtUtc(),
            new MagicLinkAuditMetadata("timesheets", "issue-1"),
            true);

    private static MagicLinkConfirmationCapabilityRevoked Revoked()
        => new(
            CapabilityId(),
            new TenantReference("tenant-1"),
            new PartyReference("operator-1"),
            RevokedAtUtc(),
            new MagicLinkAuditMetadata("timesheets", "revoke-1"));

    private static MagicLinkConfirmationCapabilityExpired Expired()
        => new(
            CapabilityId(),
            new TenantReference("tenant-1"),
            ExpiresAtUtc(),
            new MagicLinkAuditMetadata("timesheets", "expire-1"));

    private static MagicLinkConfirmationCapabilityUsed Used()
        => new(
            CapabilityId(),
            new TenantReference("tenant-1"),
            new PartyReference("party-1"),
            new TimeEntryId("time-entry-1"),
            UsedAtUtc(),
            new MagicLinkAuditMetadata("magic-link", "capability-1"));

    private static MagicLinkCapabilityId CapabilityId() => new("capability-1");

    private static DateTimeOffset IssuedAtUtc() => new(2026, 6, 19, 12, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset ObservedAtUtc() => new(2026, 6, 19, 13, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset RevokedAtUtc() => new(2026, 6, 19, 14, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset UsedAtUtc() => new(2026, 6, 19, 15, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset ExpiresAtUtc() => new(2026, 6, 21, 12, 0, 0, TimeSpan.Zero);
}
