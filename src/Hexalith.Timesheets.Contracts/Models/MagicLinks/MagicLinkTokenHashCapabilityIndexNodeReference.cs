namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>Content-bound location of one immutable, numerically addressed index node.</summary>
public sealed record MagicLinkTokenHashCapabilityIndexNodeReference(
    long Generation,
    long NodeId,
    string Digest);
