using Hexalith.EventStore.Contracts.Events;
using Hexalith.Timesheets.Contracts.Events.Exports;

namespace Hexalith.Timesheets.Server.Exports;

/// <summary>The immutable finance audit payload persisted by EventStore.</summary>
/// <param name="Event">The delivered file's audit metadata.</param>
/// <param name="AttemptId">The server-generated submission identity bound to this receipt.</param>
public sealed record StoredApprovedTimeExported(ApprovedTimeExported Event, string AttemptId) : IEventPayload;
