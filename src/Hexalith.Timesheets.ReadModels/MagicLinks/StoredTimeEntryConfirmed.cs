using Hexalith.EventStore.Contracts.Events;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;

namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>A confirmation effect paired with a stored capability use.</summary>
/// <param name="Event">The domain confirmation event.</param>
public sealed record StoredTimeEntryConfirmed(TimeEntryContributorConfirmed Event) : IEventPayload;
