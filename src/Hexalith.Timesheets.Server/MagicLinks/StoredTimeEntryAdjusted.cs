using Hexalith.EventStore.Contracts.Events;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;

namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>An adjustment effect paired with a stored capability use.</summary>
/// <param name="Event">The domain adjustment event.</param>
public sealed record StoredTimeEntryAdjusted(TimeEntryAdjustedThroughMagicLink Event) : IEventPayload;
