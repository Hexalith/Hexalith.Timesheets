namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>One authoritative candidate contribution stored inside a numeric directory page.</summary>
/// <param name="TokenHash">The lookup hash, kept only in the selected value.</param>
/// <param name="Candidate">The tenant and capability owner.</param>
public sealed record MagicLinkTokenHashCapabilityIndexDirectoryEntry(
    string TokenHash, MagicLinkTokenHashCapabilityIndexEntry Candidate);
