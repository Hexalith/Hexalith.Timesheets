using Hexalith.EventStore.Contracts.Events;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;

namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>An internal Time Entry recording payload used by actor-backed verification fixtures.</summary>
/// <param name="Event">The domain recording event.</param>
public sealed record StoredTimeEntryRecorded(TimeEntryRecorded Event) : IEventPayload;
