using Hexalith.Timesheets.Contracts.Events.MagicLinks;

namespace Hexalith.Timesheets.Server.MagicLinks;

public static class MagicLinkTokenHashCapabilityIndexProjection
{
    public const string ProjectionName = "magic-link-token-hash-capability-index";

    public const string StateStoreName = "statestore";

    public const string StateKey = "timesheets:magic-links:token-hash-capability-index:v1";

    public static MagicLinkTokenHashCapabilityIndexReadModel Rebuild(
        IEnumerable<MagicLinkConfirmationCapabilityIssued> issuedEvents)
    {
        ArgumentNullException.ThrowIfNull(issuedEvents);

        Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry> entries = new(StringComparer.Ordinal);
        foreach (MagicLinkConfirmationCapabilityIssued issued in issuedEvents)
        {
            entries[issued.TokenHash.Value] = new(issued.Tenant, issued.CapabilityId);
        }

        return new MagicLinkTokenHashCapabilityIndexReadModel(entries);
    }

    public static MagicLinkTokenHashCapabilityIndexReadModel Apply(
        MagicLinkTokenHashCapabilityIndexReadModel? current,
        MagicLinkConfirmationCapabilityIssued issued)
    {
        ArgumentNullException.ThrowIfNull(issued);

        Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry> entries = current?.Entries is null
            ? new Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry>(StringComparer.Ordinal)
            : new Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry>(current.Entries, StringComparer.Ordinal);

        entries[issued.TokenHash.Value] = new(issued.Tenant, issued.CapabilityId);
        return new MagicLinkTokenHashCapabilityIndexReadModel(entries);
    }
}
