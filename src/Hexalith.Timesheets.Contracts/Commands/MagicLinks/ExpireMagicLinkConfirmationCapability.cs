using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Commands.MagicLinks;

public sealed record ExpireMagicLinkConfirmationCapability(
    MagicLinkCapabilityId CapabilityId,
    MagicLinkAuditMetadata Source);
