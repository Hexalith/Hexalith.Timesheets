using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Server.MagicLinks.Commands;

/// <summary>
/// Carries server-resolved intent to the Time Entry owner. The processor rechecks every
/// authority-bearing fact against the persisted capability and current aggregate state.
/// </summary>
/// <param name="CapabilityId">The candidate capability identifier.</param>
/// <param name="Tenant">The candidate tenant resolved from the issued capability.</param>
/// <param name="TimeEntryId">The owning Time Entry identifier.</param>
/// <param name="TokenHash">The server-derived token hash.</param>
/// <param name="Action">The requested single-use action.</param>
/// <param name="Adjustment">Editable adjustment values, when <paramref name="Action"/> is Adjust.</param>
/// <param name="UsedAtUtc">The proposed UTC audit instant.</param>
public sealed record CommitMagicLinkUse(
    MagicLinkCapabilityId CapabilityId,
    TenantReference Tenant,
    TimeEntryId TimeEntryId,
    MagicLinkTokenHash TokenHash,
    MagicLinkUseAction Action,
    AdjustTimeThroughMagicLink? Adjustment,
    DateTimeOffset UsedAtUtc);
