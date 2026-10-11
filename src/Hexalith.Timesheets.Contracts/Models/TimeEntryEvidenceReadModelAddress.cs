using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

/// <summary>Locates the aggregate-owned Time Entry evidence slot.</summary>
public static class TimeEntryEvidenceReadModelAddress
{
    /// <summary>The configured read-model store.</summary>
    public const string StateStoreName = "timesheets-readmodels";

    /// <summary>The aggregate-owned slot.</summary>
    public const string SlotName = "evidence";

    /// <summary>Creates the EventStore canonical aggregate-owned key after validating its components.</summary>
    public static string StateKey(string tenantId, TimeEntryId timeEntryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(timeEntryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeEntryId.Value);
        if (tenantId.Contains(':', StringComparison.Ordinal)
            || timeEntryId.Value.Contains(':', StringComparison.Ordinal))
        {
            throw new ArgumentException("Read-model owner identifiers cannot contain a reserved separator.");
        }

        return $"readmodel:{tenantId.ToLowerInvariant()}:timesheets:time-entry-evidence:{timeEntryId.Value}:{SlotName}";
    }
}
