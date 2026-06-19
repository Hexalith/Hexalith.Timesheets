using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Events.MagicLinks;

public sealed record MagicLinkConfirmationCapabilityIssued(
    MagicLinkCapabilityId CapabilityId,
    TenantReference Tenant,
    PartyReference Contributor,
    TimeEntryTargetReference Target,
    ActivityTypeId ActivityTypeId,
    TimeEntryId TimeEntryId,
    MagicLinkTargetKind TargetKind,
    MagicLinkAllowedAction AllowedAction,
    MagicLinkTokenHash TokenHash,
    DateTimeOffset ExpiresAtUtc,
    PartyReference Issuer,
    DateTimeOffset IssuedAtUtc,
    MagicLinkAuditMetadata Source,
    bool IsSingleUse);
