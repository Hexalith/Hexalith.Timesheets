using System.Runtime.CompilerServices;
using System.Security.Cryptography;

using Hexalith.Timesheets.Projections.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks;

namespace Hexalith.Timesheets.Projections.Tests;

/// <summary>Simulated verification only; this fixture provides no deployed coverage or maintenance proof.</summary>
internal sealed class SimulatedMagicLinkV1Inventory : IMagicLinkV1VerifiedInventoryProvider
{
    public SimulatedMagicLinkV1Inventory(IReadOnlyList<MagicLinkTokenHashCapabilityIndexDirectoryEntry> rows)
    {
        Rows = rows;
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (MagicLinkTokenHashCapabilityIndexDirectoryEntry row in rows) MagicLinkV1InventoryDigest.Append(hash, row);
        Receipt = new("verified-receipt", "source-revision", "simulated-installation-proof",
            "simulated-writer-delivery-quiescence", rows.Count, Convert.ToHexString(hash.GetHashAndReset()));
    }

    public MagicLinkV1InventoryReceipt Receipt { get; set; }

    public IReadOnlyList<MagicLinkTokenHashCapabilityIndexDirectoryEntry> Rows { get; set; }

    public bool Valid { get; set; } = true;

    public bool Available { get; set; } = true;

    public int Enumerations { get; private set; }

    public Action<int>? BeforeEnumeration { get; set; }
    public Action? BeforeReceiptLookup { get; set; }

    public Task<MagicLinkV1InventoryReceipt?> GetVerifiedReceiptAsync(string receiptId,
        CancellationToken cancellationToken)
    {
        if (BeforeReceiptLookup is { } callback)
        {
            BeforeReceiptLookup = null;
            callback();
        }
        return Task.FromResult(Available && receiptId == Receipt.ReceiptId ? Receipt : null);
    }

    public Task<bool> ValidateReceiptAsync(MagicLinkV1InventoryReceipt receipt,
        CancellationToken cancellationToken) => Task.FromResult(Valid && receipt == Receipt);

    public async IAsyncEnumerable<MagicLinkTokenHashCapabilityIndexDirectoryEntry> ReadContributionsAsync(
        MagicLinkV1InventoryReceipt receipt, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        BeforeEnumeration?.Invoke(++Enumerations);
        foreach (MagicLinkTokenHashCapabilityIndexDirectoryEntry row in Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
        }
        await Task.CompletedTask;
    }
}
