using Hexalith.EventStore.Contracts.Events;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;

namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>A capability use committed with its matching Time Entry effect in one owner stream.</summary>
/// <param name="Event">The domain use event.</param>
public sealed record StoredMagicLinkUsed(MagicLinkConfirmationCapabilityUsed Event) : IEventPayload;
