using System.Runtime.CompilerServices;
using System.Security.Cryptography;

using Hexalith.Timesheets.Projections.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks;

namespace Hexalith.Timesheets.IntegrationTests;

/// <summary>Simulated server verification for ingress tests, with no deployed completeness claim.</summary>
internal sealed class InternalMagicLinkV1Inventory(IReadOnlyList<MagicLinkTokenHashCapabilityIndexDirectoryEntry> rows)
    : IMagicLinkV1VerifiedInventoryProvider
{
    public bool Available { get; set; } = true;
    public bool InvalidDigest { get; set; }

    public int Calls { get; private set; }

    public Task<MagicLinkV1InventoryReceipt?> GetVerifiedReceiptAsync(string receiptId, CancellationToken cancellationToken)
    {
        Calls++;
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (MagicLinkTokenHashCapabilityIndexDirectoryEntry row in rows) MagicLinkV1InventoryDigest.Append(hash, row);
        return Task.FromResult<MagicLinkV1InventoryReceipt?>(Available && receiptId == "server-receipt"
            ? new("server-receipt", "immutable-source-cut", "simulated-coverage", "simulated-quiescence",
                rows.Count, InvalidDigest ? new string('0', 64) : Convert.ToHexString(hash.GetHashAndReset())) : null);
    }

    public Task<bool> ValidateReceiptAsync(MagicLinkV1InventoryReceipt receipt, CancellationToken cancellationToken)
        => Task.FromResult(Available);

    public async IAsyncEnumerable<MagicLinkTokenHashCapabilityIndexDirectoryEntry> ReadContributionsAsync(
        MagicLinkV1InventoryReceipt receipt, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (MagicLinkTokenHashCapabilityIndexDirectoryEntry row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
        }
        await Task.CompletedTask;
    }
}
