using Hexalith.EventStore.Client.Projections;
using Hexalith.Timesheets.Contracts;

namespace Hexalith.Timesheets.ReadModels.ApprovedTimeLedger;

/// <summary>Names the generation-selected tenant ledger and its sole ordinary writer.</summary>
public static class ApprovedTimeLedgerEpoch
{
    /// <summary>The canonical projection family.</summary>
    public const string ProjectionName = "approved-time-ledger";

    /// <summary>The EventStore-managed read-model component.</summary>
    public const string StoreName = "timesheets-readmodels";

    /// <summary>The logical slot and key within a tenant generation.</summary>
    public const string SlotName = "ledger";

    /// <summary>The only ordinary ledger writer admitted by the SDK fence.</summary>
    public const string OrdinaryWriterId = "approved-time-ledger-writer";

    /// <summary>Creates the SDK scope for the specified tenant.</summary>
    public static SharedProjectionScope CreateScope(string tenantId)
        => new(StoreName, tenantId, TimesheetsDomain.Name, ProjectionName, [OrdinaryWriterId]);
}
