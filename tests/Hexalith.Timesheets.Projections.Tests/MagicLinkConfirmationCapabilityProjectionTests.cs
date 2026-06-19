using System.Text.Json;

using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Projections.MagicLinks;

using Shouldly;

using CapabilityState = Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState;

namespace Hexalith.Timesheets.Projections.Tests;

public sealed class MagicLinkConfirmationCapabilityProjectionTests
{
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
    public void Projection_orders_events_and_ignores_terminal_mutations_after_revocation()
    {
        MagicLinkConfirmationCapabilityReadModel? model = Projector().Project(
            CapabilityId(),
            [
                Event("m3", 3, Expired()),
                Event("m2", 2, Revoked()),
                Event("m1", 1, Issued())
            ],
            FreshCheckpoint(3),
            ExpiresAtUtc());

        model.ShouldNotBeNull();
        model.State.ShouldBe(CapabilityState.Revoked);
        model.ExpiredAtUtc.ShouldBeNull();
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

    private static MagicLinkCapabilityId CapabilityId() => new("capability-1");

    private static DateTimeOffset IssuedAtUtc() => new(2026, 6, 19, 12, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset ObservedAtUtc() => new(2026, 6, 19, 13, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset RevokedAtUtc() => new(2026, 6, 19, 14, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset ExpiresAtUtc() => new(2026, 6, 21, 12, 0, 0, TimeSpan.Zero);
}
