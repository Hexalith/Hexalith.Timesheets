using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Commands.MagicLinks;

public sealed record RevokeMagicLinkConfirmationCapability(
    MagicLinkCapabilityId CapabilityId,
    MagicLinkAuditMetadata Source);
