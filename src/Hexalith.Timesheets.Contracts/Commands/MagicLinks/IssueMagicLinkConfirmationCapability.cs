using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Commands.MagicLinks;

public sealed record IssueMagicLinkConfirmationCapability(
    MagicLinkCapabilityId CapabilityId,
    MagicLinkConfirmationScope Scope,
    MagicLinkAllowedAction AllowedAction,
    DateTimeOffset ExpiresAtUtc,
    MagicLinkAuditMetadata Source);
