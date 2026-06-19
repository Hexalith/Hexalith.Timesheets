using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Events.MagicLinks;

public sealed record MagicLinkConfirmationCapabilityUsed(
    MagicLinkCapabilityId CapabilityId,
    TenantReference Tenant,
    PartyReference Contributor,
    TimeEntryId TimeEntryId,
    DateTimeOffset UsedAtUtc,
    MagicLinkAuditMetadata Source);
