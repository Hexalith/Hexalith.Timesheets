using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Queries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.ReadModels.ApprovedTimeLedger;
using Hexalith.Timesheets.ReadModels.TimeEntries;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.Policies;

namespace Hexalith.Timesheets.Server.ApprovedTimeLedger;

/// <summary>Reads one generation-selected, tenant-bound ledger snapshot through the SDK.</summary>
public sealed class ProjectionBackedApprovedTimeLedgerReader(
    SharedProjectionEpochCoordinator coordinator, IQueryCursorCodec cursorCodec, TimesheetsEvidencePolicyOptions evidencePolicy)
    : IApprovedTimeLedgerProjectionReader
{
    /// <inheritdoc />
    public async ValueTask<bool> IsDeliveryAvailableAsync(TimesheetsRequestContext context, CancellationToken cancellationToken)
    {
        if (!context.IsProjectWorkflow || context.Tenant is not { } tenant)
        {
            return false;
        }

        try
        {
            SharedProjectionReadResult<ApprovedTimeLedgerSnapshot> selected = await ReadSelectedAsync(
                tenant.TenantId, cancellationToken).ConfigureAwait(false);
            return IsHealthy(selected) && (selected.Value is { } snapshot
                ? !string.IsNullOrWhiteSpace(selected.SelectedETag) && IsTenantSnapshot(snapshot, tenant.TenantId)
                // A registered writer establishes an epoch before the first owner event.
                : selected.SelectedETag is null);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask<ApprovedTimeLedgerReadModel?> QueryAsync(
        TimesheetsRequestContext context, QueryApprovedTimeLedger query, CancellationToken cancellationToken)
    {
        if (!context.IsProjectWorkflow || context.Tenant is null || context.Actor is null || query.Work is not null)
        {
            return null;
        }

        try
        {
            string tenant = context.Tenant.TenantId;
            SharedProjectionReadResult<ApprovedTimeLedgerSnapshot> selected = await ReadSelectedAsync(
                tenant, cancellationToken).ConfigureAwait(false);
            if (!IsHealthy(selected) || selected.Value is not { } snapshot
                || string.IsNullOrWhiteSpace(selected.SelectedETag) || !IsTenantSnapshot(snapshot, tenant))
            {
                return null;
            }

            string version = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
                new { selected.Generation, selected.SelectedETag, snapshot.Position })));
            string scope = QueryCursorScope.Create().Add("tenant", tenant).Add("actor", context.Actor.PartyId)
                .Add("version", version).Add("filters", JsonSerializer.Serialize(query with { Cursor = null })).Build();
            if (!cursorCodec.TryDecode(query.Cursor, nameof(QueryApprovedTimeLedger), scope, out string? position, out _)
                || position is not null && !int.TryParse(position, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                return null;
            }

            int offset = position is null ? 0 : int.Parse(position, CultureInfo.InvariantCulture);
            int size = query.PageSize <= 0 ? 50 : Math.Min(query.PageSize, 500);
            List<ApprovedTimeLedgerRowReadModel> rows = [];
            foreach (var owner in snapshot.Owners)
            {
                TimeEntryEvidenceReadModel evidence = TimeEntryEvidenceCompactStorage.Unpack(owner.Value);
                if (evidence.TimeEntryId.Value != owner.Key)
                {
                    return null;
                }

                if (evidence.ApprovalState != TimeEntryApprovalState.Approved
                    || evidence.Target.TargetKind != TimeEntryTargetKind.Project)
                {
                    continue;
                }

                if (evidence.ApprovalDecision is not { } approval || approval.Tenant != context.Tenant
                    || approval.TimeEntryId != evidence.TimeEntryId
                    || evidence.ApprovedCorrection is { } corrected
                        && (corrected.Tenant != context.Tenant || corrected.TimeEntryId != evidence.TimeEntryId)
                    || evidence.Correction is { } correction
                        && (correction.Tenant != context.Tenant || correction.TimeEntryId != evidence.TimeEntryId))
                {
                    return null;
                }

                bool publish = evidencePolicy.AllowsProjectionPublication();
                ApprovedTimeLedgerRowReadModel current = ApprovedTimeLedgerRowReadModel.CurrentFromEvidence(evidence, publish);
                ApprovedTimeLedgerRowReadModel? superseded = ApprovedTimeLedgerRowReadModel.SupersededFromApprovedCorrection(evidence, publish);
                if (query.Includes(current))
                {
                    rows.Add(current);
                }

                if (superseded is not null && query.Includes(superseded))
                {
                    rows.Add(superseded);
                }
            }

            ApprovedTimeLedgerRowReadModel[] items = ApprovedTimeLedgerOrdering.Sort(rows, query).Skip(offset).Take(size).ToArray();
            string? next = offset < rows.Count - items.Length
                ? cursorCodec.Encode(nameof(QueryApprovedTimeLedger), scope, (offset + items.Length).ToString(CultureInfo.InvariantCulture))
                : null;
            return new ApprovedTimeLedgerReadModel(items, next,
                new(ProjectionFreshnessState.Fresh, snapshot.Position.ToString(CultureInfo.InvariantCulture), null, null),
                true, string.Empty)
            {
                ExportSnapshotId = version
            };
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private Task<SharedProjectionReadResult<ApprovedTimeLedgerSnapshot>> ReadSelectedAsync(
        string tenant, CancellationToken cancellationToken)
        => coordinator.ReadAsync<ApprovedTimeLedgerSnapshot>(
            ApprovedTimeLedgerEpoch.CreateScope(tenant), ApprovedTimeLedgerEpoch.SlotName, cancellationToken);

    private static bool IsHealthy(SharedProjectionReadResult<ApprovedTimeLedgerSnapshot> selected)
        => selected is { IsAvailable: true, IsStale: false, IsDegraded: false };

    private static bool IsTenantSnapshot(ApprovedTimeLedgerSnapshot snapshot, string tenant)
        => snapshot.TenantId == tenant && snapshot.Owners is not null && snapshot.Position >= 0;
}
