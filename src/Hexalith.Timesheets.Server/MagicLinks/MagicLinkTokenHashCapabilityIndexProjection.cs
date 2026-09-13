using Hexalith.Timesheets.Contracts.Events.MagicLinks;

namespace Hexalith.Timesheets.Server.MagicLinks;

public static class MagicLinkTokenHashCapabilityIndexProjection
{
    /// <summary>The canonical named projection route.</summary>
    public const string ProjectionName = "magic-link-token-hash-capability-index";

    /// <summary>The Dapr state-store component containing the candidate index.</summary>
    public const string StateStoreName = "statestore";

    /// <summary>The fixed cross-tenant candidate-index key.</summary>
    public const string StateKey = "timesheets:magic-links:token-hash-capability-index:v1";

    /// <summary>Rebuilds a deterministic index from issuance events.</summary>
    /// <param name="issuedEvents">The issuance events to fold.</param>
    /// <returns>The rebuilt candidate index.</returns>
    public static MagicLinkTokenHashCapabilityIndexReadModel Rebuild(
        IEnumerable<MagicLinkConfirmationCapabilityIssued> issuedEvents)
    {
        ArgumentNullException.ThrowIfNull(issuedEvents);

        Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry> entries = issuedEvents
            .GroupBy(static issued => issued.TokenHash.Value, StringComparer.Ordinal)
            .Select(static group => new
            {
                TokenHash = group.Key,
                Candidates = group
                    .Select(static issued => new MagicLinkTokenHashCapabilityIndexEntry(issued.Tenant, issued.CapabilityId))
                    .Distinct()
                    .ToArray()
            })
            .Where(static group => group.Candidates.Length == 1)
            .OrderBy(static group => group.TokenHash, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.TokenHash,
                static group => group.Candidates[0],
                StringComparer.Ordinal);

        return new MagicLinkTokenHashCapabilityIndexReadModel(entries);
    }

    /// <summary>Idempotently applies one issuance candidate.</summary>
    /// <param name="current">The current index, if present.</param>
    /// <param name="issued">The authoritative issuance event.</param>
    /// <returns>The updated index.</returns>
    public static MagicLinkTokenHashCapabilityIndexReadModel Apply(
        MagicLinkTokenHashCapabilityIndexReadModel? current,
        MagicLinkConfirmationCapabilityIssued issued)
    {
        ArgumentNullException.ThrowIfNull(issued);

        Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry> entries = current?.Entries is null
            ? new Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry>(StringComparer.Ordinal)
            : new Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry>(current.Entries, StringComparer.Ordinal);

        entries[issued.TokenHash.Value] = new(issued.Tenant, issued.CapabilityId);
        return Canonical(entries);
    }

    /// <summary>Replaces exactly one tenant slice while preserving all other tenants.</summary>
    /// <param name="current">The current shared index, if present.</param>
    /// <param name="tenantId">The tenant slice to replace.</param>
    /// <param name="replacement">The rebuilt tenant slice.</param>
    /// <returns>The merged, canonical shared index.</returns>
    public static MagicLinkTokenHashCapabilityIndexReadModel ReplaceTenant(
        MagicLinkTokenHashCapabilityIndexReadModel? current,
        string tenantId,
        MagicLinkTokenHashCapabilityIndexReadModel replacement)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(replacement);

        Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry> entries = (current?.Entries ??
                new Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry>(StringComparer.Ordinal))
            .Where(pair => !string.Equals(pair.Value.Tenant.TenantId, tenantId, StringComparison.Ordinal))
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);

        foreach (KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry> pair in replacement.Entries)
        {
            if (!entries.TryAdd(pair.Key, pair.Value) && entries[pair.Key] != pair.Value)
            {
                entries.Remove(pair.Key);
            }
        }

        return Canonical(entries);
    }

    private static MagicLinkTokenHashCapabilityIndexReadModel Canonical(
        IEnumerable<KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>> entries)
        => new(entries
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal));
}
