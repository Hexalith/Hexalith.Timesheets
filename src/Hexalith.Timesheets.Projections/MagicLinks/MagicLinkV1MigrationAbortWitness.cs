namespace Hexalith.Timesheets.Projections.MagicLinks;

/// <summary>
/// Immutable reservation ownership or abort intent persisted through EventStore's read-model policy.
/// It lives outside disposable generation pages so erased checkpoints cannot lose retry ownership.
/// It authorizes only cleanup of the bound private epoch, never promotion or legacy delivery recovery.
/// </summary>
/// <param name="Epoch">The intended private epoch, checked against the SDK reservation before staging.</param>
/// <param name="OperationId">The original authorized operation.</param>
/// <param name="Receipt">The exact original verified receipt, retained even after revocation.</param>
/// <param name="SourceGeneration">The SDK-selected generation retained by the reservation or its cleanup.</param>
/// <param name="SourceDigest">The original expected source snapshot; it does not certify an intervening selection.</param>
public sealed record MagicLinkV1MigrationAbortWitness(long Epoch, string OperationId,
    MagicLinkV1InventoryReceipt Receipt, long SourceGeneration, string SourceDigest);
