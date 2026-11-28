using Hexalith.EventStore.Contracts.Events;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;

namespace Hexalith.Timesheets.ReadModels.TimeEntries;

/// <summary>The EventStore-owned wrapper for a committed approved Time Entry transition.</summary>
/// <param name="Event">The Timesheets lifecycle event.</param>
public sealed record StoredTimeEntryApproved(TimeEntryApproved Event) : IEventPayload;
