using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>A reason-free rejection emitted only to the internal EventStore command status.</summary>
public sealed record MagicLinkCommitRejected : IRejectionEvent;
