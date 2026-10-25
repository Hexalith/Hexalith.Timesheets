using Hexalith.EventStore.Contracts.Events;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;

namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>A submitted Time Entry transition committed with its recording.</summary>
/// <param name="Event">The domain submission event.</param>
public sealed record StoredTimeEntrySubmitted(TimeEntrySubmitted Event) : IEventPayload;
