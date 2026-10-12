namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>Numeric selected page and inclusive hash range in the bounded index directory.</summary>
/// <param name="PageId">Allocated page ordinal.</param>
/// <param name="FirstHash">First hash in the sorted page.</param>
/// <param name="LastHash">Last hash in the sorted page.</param>
public sealed record MagicLinkTokenHashCapabilityIndexDirectoryReference(
    int PageId, string FirstHash, string LastHash);
