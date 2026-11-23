using System.Buffers;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.Timesheets.Contracts.Events.Exports;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.Exports;

/// <summary>Defines the v1 request owner and the command identity bound to its original receipt.</summary>
internal static class ApprovedTimeExportReceipt
{
    private const string OwnerPrefix = "finance-export-";

    internal static QueryApprovedTimeLedger Normalize(QueryApprovedTimeLedger filters)
    {
        string? period = string.IsNullOrWhiteSpace(filters.TenantLocalPeriodKey)
            ? null : string.Join('/', filters.TenantLocalPeriodKey.Split('/', StringSplitOptions.TrimEntries));
        return filters with { TenantLocalPeriodKey = period, PageSize = filters.PageSize <= 0 ? 50 : Math.Min(500, filters.PageSize) };
    }

    /// <summary>Describes which row states the filters actually select, retaining the submitted flags in Filters.</summary>
    internal static ApprovedTimeExportScope Scope(QueryApprovedTimeLedger filters, int rowCount)
    {
        bool currentOnly = filters.CurrentRowsOnly && !filters.IncludeSupersededRows;
        return new(filters, rowCount, !currentOnly, currentOnly);
    }

    /// <summary>Projects the effective scope of an already verified stored receipt.</summary>
    internal static ApprovedTimeExportScope EffectiveScope(ApprovedTimeExportScope scope)
    {
        ApprovedTimeExportScope effective = Scope(scope.Filters, scope.RowCount);
        return scope with { CurrentRowsOnly = effective.CurrentRowsOnly, IncludesSupersededRows = effective.IncludesSupersededRows };
    }

    internal static TimesheetsAuthorizationRequest Authority(TimesheetsRequestContext context, QueryApprovedTimeLedger? filters)
        => new(context, TimesheetsOperation.Export) { Project = filters?.Project, Work = filters?.Work, Contributor = filters?.Contributor };

    internal static bool ValidRequestId(string? id)
        => id is null || (id.Length <= 256 && !string.IsNullOrWhiteSpace(id)
            && !id.Any(char.IsControl) && IsWellFormed(id));

    internal static bool TryGetOwnerTenant(ApprovedTimeExported receipt, out string storageTenant)
        => TenantCanonicalizer.TryCanonicalize(receipt.Tenant?.TenantId, out storageTenant)
            && !string.IsNullOrWhiteSpace(receipt.CorrelationId) && IsWellFormed(receipt.CorrelationId)
            && ValidRequestId(receipt.RequestId);

    // EventStore lowercases the storage tenant. Timesheets authorization and payload comparisons retain the exact tenant.
    internal static string Owner(string storageTenant, ApprovedTimeExported receipt)
        => OwnerPrefix + MagicLinkDurableSubmissionService.CreateMessageId(
            JsonSerializer.Serialize(new[] { storageTenant, receipt.RequestId ?? receipt.CorrelationId }));

    internal static bool IsReceiptOwner(string owner)
        => owner.StartsWith(OwnerPrefix, StringComparison.Ordinal)
            && MessageIdGrammar.IsValid(owner[OwnerPrefix.Length..], version: 2);

    // Frozen v1 scalar order. Future public record properties do not change the lineage of stored receipts.
    // Validation requires scope.Filters == Filters and checks duplicate scope fields before deriving this identity.
    internal static string MessageId(ApprovedTimeExported receipt, string attemptId)
    {
        var filters = receipt.Filters;
        var scope = receipt.OutputScope;
        object?[] fields =
        [
            attemptId, receipt.RequestId, receipt.Tenant.TenantId, receipt.Requester!.PartyId, receipt.CorrelationId,
            receipt.RequestedAtUtc, receipt.GeneratedAtUtc, filters.Project?.ProjectId, filters.Work?.WorkId,
            filters.Contributor?.PartyId, filters.ActivityTypeId?.Value, filters.TenantLocalPeriodKey,
            filters.ServiceDateFrom, filters.ServiceDateTo, (int?)filters.BillableState, filters.CurrentRowsOnly,
            filters.IncludeSupersededRows, (int)filters.SortBy, (int)filters.SortDirection, filters.PageSize,
            scope.LedgerCheckpoint, scope.LedgerCursor, scope.LedgerAsOfUtc, (int)receipt.FreshnessState,
            receipt.RowCount, (int)receipt.Format, receipt.FormatVersion, receipt.OutputContentHashSha256, receipt.OutputRowsHashSha256
        ];
        return MagicLinkDurableSubmissionService.CreateMessageId("approved-time-export-receipt-v1\n" + JsonSerializer.Serialize(fields));
    }

    internal static bool IsValid(ApprovedTimeExported receipt, out string storageTenant, bool allowLegacyScopeFlags = false)
        => TryGetOwnerTenant(receipt, out storageTenant) && receipt.Requester is not null
            && receipt.RequestedAtUtc != default && receipt.RequestedAtUtc.Offset == TimeSpan.Zero
            && receipt.GeneratedAtUtc >= receipt.RequestedAtUtc && receipt.GeneratedAtUtc.Offset == TimeSpan.Zero
            && receipt.Format == ApprovedTimeExportFormat.Csv && receipt.FormatVersion == "approved-time-export.csv.v1"
            && receipt.FreshnessState == ProjectionFreshnessState.Fresh
            && receipt.Filters is { BillableState: BillableState.Billable, Cursor: null } filters
            && filters == Normalize(filters)
            && receipt.OutputScope is { RowCount: > 0 } scope && scope.RowCount == receipt.RowCount
            && scope.Filters == filters
            && (scope == EffectiveScope(scope) || (allowLegacyScopeFlags
                && scope.IncludesSupersededRows == filters.IncludeSupersededRows && scope.CurrentRowsOnly == filters.CurrentRowsOnly))
            && (scope.LedgerAsOfUtc is null || scope.LedgerAsOfUtc.Value.Offset == TimeSpan.Zero)
            && IsSha256(receipt.OutputContentHashSha256) && IsSha256(receipt.OutputRowsHashSha256);

    private static bool IsSha256(string? hash)
        => hash is { Length: 64 } && hash.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsWellFormed(string value)
    {
        ReadOnlySpan<char> remaining = value.AsSpan();
        while (!remaining.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(remaining, out _, out int consumed) != OperationStatus.Done)
            {
                return false;
            }

            remaining = remaining[consumed..];
        }

        return true;
    }
}
