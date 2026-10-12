using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.Timesheets.Server.MagicLinks;

namespace Hexalith.Timesheets.Projections.MagicLinks;

/// <summary>Version-1 receipt and contribution byte recipe shared with independently installed verifiers.</summary>
/// <remarks>
/// Contributions use UTF-8 compact JSON without a BOM or whitespace, property order TokenHash, Candidate;
/// Candidate uses Tenant then CapabilityId, containing TenantId and Value respectively. Names retain
/// PascalCase. System.Text.Json's fixed default encoder escapes non-ASCII and HTML-sensitive characters
/// as uppercase hex Unicode escapes, supplementary characters as surrogate pairs, quotes as \u0022,
/// backslashes as \\, and control characters with the standard JSON escapes. Prefix each JSON byte
/// sequence with its unsigned four-byte big-endian length, concatenate contributions in ordinal
/// tenant/owner order, and compute uppercase SHA-256. Empty input hashes the empty byte sequence.
/// Receipt fingerprints hash unprefixed UTF-8 compact JSON in record-constructor property order using
/// the same escaping. A different naming policy, property order, encoder or length byte order requires
/// a new encoding version; it cannot certify a version-1 receipt.
/// </remarks>
public static class MagicLinkV1InventoryDigest
{
    /// <summary>Appends a four-byte big-endian byte length and default JSON directory-entry encoding.</summary>
    public static void Append(IncrementalHash digest, MagicLinkTokenHashCapabilityIndexDirectoryEntry row)
    {
        ArgumentNullException.ThrowIfNull(digest);
        ArgumentNullException.ThrowIfNull(row);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(row);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        digest.AppendData(length);
        digest.AppendData(bytes);
    }

    /// <summary>Binds every field of the server receipt to the migration authority and durable epoch.</summary>
    public static string ReceiptFingerprint(MagicLinkV1InventoryReceipt receipt)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(receipt)));
}
