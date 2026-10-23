namespace Hexalith.Timesheets.Projections.MagicLinks;

/// <summary>A scoped flat base and immutable transition that reconstruct one complete capability view.</summary>
/// <param name="TenantId">The selected tenant.</param>
/// <param name="CapabilityId">The selected capability.</param>
/// <param name="Base">The validated flat storage retained by this continuation.</param>
/// <param name="TransitionDigest">The SHA-256 digest of the token-free transition bytes.</param>
/// <param name="TransitionByteLength">The canonical transition byte length.</param>
/// <param name="TransitionFragmentCount">The number of bounded transition fragments.</param>
/// <param name="ViewDigest">The reconstructed canonical view digest.</param>
/// <param name="ViewByteLength">The reconstructed canonical view byte length.</param>
public sealed record MagicLinkCapabilityProjectionContinuation(
    string TenantId,
    string CapabilityId,
    MagicLinkCapabilityProjectionStorage Base,
    string TransitionDigest,
    int TransitionByteLength,
    int TransitionFragmentCount,
    string ViewDigest,
    int ViewByteLength);
