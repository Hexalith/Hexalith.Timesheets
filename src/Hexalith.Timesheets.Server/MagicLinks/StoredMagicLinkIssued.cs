using Hexalith.EventStore.Contracts.Events;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;

namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>Internal EventStore payloads keep public domain contracts free of persistence SDK types.</summary>
/// <param name="Event">The domain issuance event.</param>
public sealed record StoredMagicLinkIssued(MagicLinkConfirmationCapabilityIssued Event) : IEventPayload;
