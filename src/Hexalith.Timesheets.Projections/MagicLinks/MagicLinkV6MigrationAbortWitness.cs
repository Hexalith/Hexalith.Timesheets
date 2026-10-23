namespace Hexalith.Timesheets.Projections.MagicLinks;

/// <summary>Immutable proof of a certified v6 migration reservation, retained outside disposable staging.</summary>
/// <param name="StoreName">The exact read-model store.</param>
/// <param name="TenantId">The global index scope tenant.</param>
/// <param name="Domain">The owning domain.</param>
/// <param name="Family">The projection family.</param>
/// <param name="OperationId">The operation authorized at Begin.</param>
/// <param name="InventoryFingerprint">The immutable certified inventory binding proven by Begin.</param>
/// <param name="Epoch">The reserved private epoch, which recovery must never promote.</param>
/// <param name="SourceGeneration">The retained selected source generation.</param>
public sealed record MagicLinkV6MigrationAbortWitness(string StoreName, string TenantId, string Domain,
    string Family, string OperationId, string InventoryFingerprint, long Epoch, long SourceGeneration);
