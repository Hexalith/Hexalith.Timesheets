namespace Hexalith.Timesheets.Endpoints.MagicLinks;

/// <summary>Explicit operator request; complete evidence is resolved and verified only by the server.</summary>
/// <param name="OperationId">The immutable operation to begin or resume.</param>
/// <param name="ReceiptId">Identifier of independently verified server-side inventory evidence.</param>
public sealed record MagicLinkV1MigrationRequest(string OperationId, string ReceiptId);
