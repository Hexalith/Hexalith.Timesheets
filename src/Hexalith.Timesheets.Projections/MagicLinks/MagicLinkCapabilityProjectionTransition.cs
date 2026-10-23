using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;

namespace Hexalith.Timesheets.Projections.MagicLinks;

/// <summary>One normalized issuance row or terminal fact, without an issuance event or token hash.</summary>
/// <param name="Issuance">The normalized issuance display when issuance completes a terminal-first base.</param>
/// <param name="Used">A use fact.</param>
/// <param name="Revoked">A revocation fact.</param>
/// <param name="Expired">An expiry fact.</param>
/// <param name="TerminalInCapabilityStream">Whether the terminal belongs to the legacy capability stream.</param>
/// <param name="TerminalPosition">The terminal source position.</param>
public sealed record MagicLinkCapabilityProjectionTransition(
    MagicLinkConfirmationCapabilityReadModel? Issuance,
    MagicLinkConfirmationCapabilityUsed? Used,
    MagicLinkConfirmationCapabilityRevoked? Revoked,
    MagicLinkConfirmationCapabilityExpired? Expired,
    bool TerminalInCapabilityStream,
    long? TerminalPosition);
