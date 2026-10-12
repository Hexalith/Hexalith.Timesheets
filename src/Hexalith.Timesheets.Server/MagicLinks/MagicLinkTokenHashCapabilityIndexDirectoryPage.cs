namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>Bounded selected contributions for one numeric directory page.</summary>
/// <param name="Entries">Sorted hash and owner contributions.</param>
public sealed record MagicLinkTokenHashCapabilityIndexDirectoryPage(
    IReadOnlyList<MagicLinkTokenHashCapabilityIndexDirectoryEntry> Entries);
