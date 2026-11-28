using Hexalith.EventStore.Contracts.Events;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;

namespace Hexalith.Timesheets.ReadModels.TimeEntries;

/// <summary>The EventStore-owned wrapper for a committed approvedcorrected Time Entry transition.</summary>
/// <param name="Event">The Timesheets lifecycle event.</param>
public sealed record StoredTimeEntryApprovedCorrected(TimeEntryApprovedCorrected Event) : IEventPayload;
