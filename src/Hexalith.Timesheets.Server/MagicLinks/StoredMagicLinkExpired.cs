using Hexalith.EventStore.Contracts.Events;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;

namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>An expired capability terminal event owned by its Time Entry.</summary>
/// <param name="Event">The domain expiry event.</param>
public sealed record StoredMagicLinkExpired(MagicLinkConfirmationCapabilityExpired Event) : IEventPayload;
