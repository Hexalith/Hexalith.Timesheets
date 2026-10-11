namespace Hexalith.Timesheets.Projections.MagicLinks;

/// <summary>A generation-selected capability value or a pointer to its bounded immutable fragments.</summary>
/// <param name="Inline">The complete inline view when it fits the physical value limit.</param>
/// <param name="Digest">The immutable fragment digest when the view is externalized.</param>
/// <param name="ByteLength">The canonical serialized byte length.</param>
/// <param name="FragmentCount">The number of immutable fragments.</param>
/// <param name="FragmentBytes">The byte ceiling used for each fragment.</param>
public sealed record MagicLinkCapabilityProjectionStorage(
    MagicLinkCapabilityProjectionView? Inline,
    string? Digest,
    int ByteLength,
    int FragmentCount,
    int FragmentBytes = 0);

/// <summary>One bounded piece of a capability view, written before its selected pointer.</summary>
/// <param name="Data">The fragment's canonical bytes.</param>
public sealed record MagicLinkCapabilityProjectionFragment(byte[] Data);
