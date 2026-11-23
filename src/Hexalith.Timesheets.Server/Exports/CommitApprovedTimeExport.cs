using Hexalith.Timesheets.Contracts.Events.Exports;

namespace Hexalith.Timesheets.Server.Exports;

/// <summary>Requests the first receipt on the tenant's owner stream for this export request.</summary>
/// <param name="Receipt">Authorized audit metadata and the digest of the final CSV.</param>
/// <param name="AttemptId">The server-generated submission identity bound to this receipt.</param>
public sealed record CommitApprovedTimeExport(ApprovedTimeExported Receipt, string AttemptId);
