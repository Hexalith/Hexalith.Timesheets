namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>Small selected root of the token and tenant tries, with numeric node allocation.</summary>
public sealed record MagicLinkTokenHashCapabilityIndexTreeRoot(
    int StorageVersion,
    long NextNodeId,
    MagicLinkTokenHashCapabilityIndexNodeReference? TokenRoot,
    MagicLinkTokenHashCapabilityIndexNodeReference? TenantRoot)
{
    /// <summary>Numeric owner-admission trie, selected with the token and roster tries.</summary>
    public MagicLinkTokenHashCapabilityIndexNodeReference? OwnerRoot { get; init; }
}
