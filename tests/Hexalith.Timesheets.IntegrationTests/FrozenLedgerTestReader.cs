using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Projections.ApprovedTimeLedger;
using Hexalith.Timesheets.Projections.TimeEntries;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.IntegrationTests;

internal sealed class FrozenLedgerTestReader(
    ApprovedTimeLedgerProjection projection,
    IReadOnlyList<TimeEntryProjectionEvent> events,
    TimesheetsProjectionCheckpoint checkpoint) : IApprovedTimeLedgerProjectionReader
{
    private readonly TimeEntryProjectionEvent[] _history = [.. events];
    private readonly string _snapshotId = Guid.NewGuid().ToString("N");

    public ValueTask<ApprovedTimeLedgerReadModel?> QueryAsync(
        TimesheetsRequestContext context, QueryApprovedTimeLedger query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context.Tenant);
        return ValueTask.FromResult<ApprovedTimeLedgerReadModel?>(projection.Project(
            context.Tenant.TenantId, _history, checkpoint, query) with { ExportSnapshotId = _snapshotId });
    }
}
