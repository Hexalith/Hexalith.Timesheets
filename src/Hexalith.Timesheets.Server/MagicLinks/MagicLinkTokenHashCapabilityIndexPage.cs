namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>One bounded selected page of owner contributions for a token hash.</summary>
/// <param name="TokenHash">The hash bound to the page inside the private index value.</param>
/// <param name="Candidates">The owner contributions in this page.</param>
public sealed record MagicLinkTokenHashCapabilityIndexPage(
    string TokenHash,
    IReadOnlyList<MagicLinkTokenHashCapabilityIndexEntry> Candidates);
