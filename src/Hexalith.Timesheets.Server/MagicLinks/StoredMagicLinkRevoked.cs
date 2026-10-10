using Hexalith.EventStore.Contracts.Events;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;

namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>A revoked capability terminal event owned by its Time Entry.</summary>
/// <param name="Event">The domain revocation event.</param>
public sealed record StoredMagicLinkRevoked(MagicLinkConfirmationCapabilityRevoked Event) : IEventPayload;
