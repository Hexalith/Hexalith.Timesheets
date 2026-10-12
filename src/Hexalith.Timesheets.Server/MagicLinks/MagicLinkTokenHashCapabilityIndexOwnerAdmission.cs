namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>Selected proof that an owner has one authoritative issuance hash.</summary>
/// <param name="TokenHash">The admitted issuance hash.</param>
/// <param name="Candidate">The exact tenant and capability owner.</param>
public sealed record MagicLinkTokenHashCapabilityIndexOwnerAdmission(
    string TokenHash, MagicLinkTokenHashCapabilityIndexEntry Candidate);
