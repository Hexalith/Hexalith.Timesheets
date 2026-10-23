namespace Hexalith.Timesheets.Projections.MagicLinks;

/// <summary>One bounded piece of a capability view, written before its selected pointer.</summary>
/// <param name="Data">The fragment's canonical bytes.</param>
public sealed record MagicLinkCapabilityProjectionFragment(byte[] Data);
