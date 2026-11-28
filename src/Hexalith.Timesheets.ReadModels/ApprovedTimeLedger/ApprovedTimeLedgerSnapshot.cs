using System.Text.Json.Nodes;

namespace Hexalith.Timesheets.ReadModels.ApprovedTimeLedger;

/// <summary>Stores one tenant ledger generation's compact, validated Time Entry folds.</summary>
/// <param name="TenantId">The owning tenant boundary.</param>
/// <param name="Position">The largest included global event position.</param>
/// <param name="Owners">Compact Time Entry folds keyed by owner within the tenant.</param>
internal sealed record ApprovedTimeLedgerSnapshot(
    string TenantId,
    long Position,
    Dictionary<string, JsonObject> Owners);
