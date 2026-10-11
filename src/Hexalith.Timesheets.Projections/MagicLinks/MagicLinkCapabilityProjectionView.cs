using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Projections.MagicLinks;

/// <summary>Token-free durable contribution and published display for one capability.</summary>
/// <param name="Issuance">The validated issuance display, when known.</param>
/// <param name="Used">Pending or applied use evidence.</param>
/// <param name="Revoked">Pending or applied revocation evidence.</param>
/// <param name="Expired">Pending or applied expiry evidence.</param>
/// <param name="TerminalInCapabilityStream">Whether a legacy terminal came from the capability owner.</param>
/// <param name="Published">The complete display row, or null while issuance is missing.</param>
/// <param name="TerminalPosition">The terminal event's source position.</param>
public sealed record MagicLinkCapabilityProjectionView(
    MagicLinkConfirmationCapabilityReadModel? Issuance,
    MagicLinkConfirmationCapabilityUsed? Used,
    MagicLinkConfirmationCapabilityRevoked? Revoked,
    MagicLinkConfirmationCapabilityExpired? Expired,
    bool TerminalInCapabilityStream,
    MagicLinkConfirmationCapabilityReadModel? Published,
    long? TerminalPosition)
{
    /// <summary>Gets a value without any issuance or terminal evidence.</summary>
    public static MagicLinkCapabilityProjectionView Empty { get; } = new(null, null, null, null, false, null, null);

    /// <summary>Refreshes the display expiry at the time it is observed; never authorizes use.</summary>
    public MagicLinkConfirmationCapabilityReadModel? Observe(DateTimeOffset observedAtUtc)
    {
        if (Published is not { } row)
        {
            return null;
        }

        MagicLinkExpiryState expiry = Expired is not null || row.State == MagicLinkCapabilityState.Expired
            || observedAtUtc >= row.ExpiresAtUtc
            ? MagicLinkExpiryState.Expired
            : row.ExpiresAtUtc - observedAtUtc <= TimeSpan.FromHours(24)
                ? MagicLinkExpiryState.ExpiringSoon
                : MagicLinkExpiryState.Active;
        return row with
        {
            ExpiryState = expiry,
            ExpiryBadgeText = expiry switch
            {
                MagicLinkExpiryState.Active => "Active",
                MagicLinkExpiryState.ExpiringSoon => "Expiring soon",
                _ => "Expired"
            },
            ProjectionFreshness = row.ProjectionFreshness with { AsOfUtc = observedAtUtc }
        };
    }
}
