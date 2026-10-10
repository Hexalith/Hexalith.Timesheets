using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Server.MagicLinks.Commands;

/// <summary>Server-resolved terminal intent for the Time Entry owner.</summary>
/// <param name="CapabilityId">The issued capability identifier.</param>
/// <param name="Tenant">The server-resolved tenant.</param>
/// <param name="TimeEntryId">The owning Time Entry identifier.</param>
/// <param name="Actor">The authorized administrator for a revocation.</param>
/// <param name="Action">The terminal action.</param>
/// <param name="Source">The audit source.</param>
/// <param name="AtUtc">The UTC transition instant.</param>
public sealed record CommitMagicLinkTransition(
    MagicLinkCapabilityId CapabilityId,
    TenantReference Tenant,
    TimeEntryId TimeEntryId,
    PartyReference? Actor,
    MagicLinkTransitionAction Action,
    MagicLinkAuditMetadata Source,
    DateTimeOffset AtUtc);
