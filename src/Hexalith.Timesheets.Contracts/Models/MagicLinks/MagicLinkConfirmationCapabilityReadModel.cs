using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models.MagicLinks;

public sealed record MagicLinkConfirmationCapabilityReadModel(
    MagicLinkCapabilityId CapabilityId,
    TenantReference Tenant,
    PartyReference Contributor,
    TimeEntryTargetReference Target,
    ActivityTypeId ActivityTypeId,
    TimeEntryId TimeEntryId,
    MagicLinkTargetKind TargetKind,
    MagicLinkAllowedAction AllowedAction,
    MagicLinkCapabilityState State,
    MagicLinkExpiryState ExpiryState,
    DateTimeOffset ExpiresAtUtc,
    PartyReference Issuer,
    DateTimeOffset IssuedAtUtc,
    ProjectionFreshnessMetadata ProjectionFreshness)
{
    public PartyReference? RevokedBy { get; init; }

    public DateTimeOffset? RevokedAtUtc { get; init; }

    public DateTimeOffset? ExpiredAtUtc { get; init; }

    public MagicLinkAuditMetadata? IssueMetadata { get; init; }

    public MagicLinkAuditMetadata? RevocationMetadata { get; init; }

    public MagicLinkAuditMetadata? ExpiryMetadata { get; init; }

    public DateTimeOffset? UsedAtUtc { get; init; }

    public MagicLinkAuditMetadata? UseMetadata { get; init; }

    public string? UseOutcomeCategory { get; init; }

    public string StateBadgeText { get; init; } = State.ToString();

    public string ExpiryBadgeText { get; init; } = ExpiryState.ToString();
}
