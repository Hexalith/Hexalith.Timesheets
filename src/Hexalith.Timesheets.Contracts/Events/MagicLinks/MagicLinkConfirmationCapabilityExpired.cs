using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Events.MagicLinks;

public sealed record MagicLinkConfirmationCapabilityExpired(
    MagicLinkCapabilityId CapabilityId,
    TenantReference Tenant,
    DateTimeOffset ExpiredAtUtc,
    MagicLinkAuditMetadata Source);
