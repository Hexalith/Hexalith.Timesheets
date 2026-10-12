using System.Runtime.CompilerServices;

using Hexalith.Timesheets.Server.MagicLinks;

namespace Hexalith.Timesheets.Projections.MagicLinks;

/// <summary>Default posture: no independently verified installed inventory or maintenance proof exists.</summary>
public sealed class UnavailableMagicLinkV1VerifiedInventoryProvider : IMagicLinkV1VerifiedInventoryProvider
{
    /// <inheritdoc />
    public Task<MagicLinkV1InventoryReceipt?> GetVerifiedReceiptAsync(string receiptId,
        CancellationToken cancellationToken) => Task.FromResult<MagicLinkV1InventoryReceipt?>(null);

    /// <inheritdoc />
    public async IAsyncEnumerable<MagicLinkTokenHashCapabilityIndexDirectoryEntry> ReadContributionsAsync(
        MagicLinkV1InventoryReceipt receipt, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return await Task.FromException<MagicLinkTokenHashCapabilityIndexDirectoryEntry>(
            new UnauthorizedAccessException("Verified migration evidence is unavailable.")).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> ValidateReceiptAsync(MagicLinkV1InventoryReceipt receipt,
        CancellationToken cancellationToken) => Task.FromResult(false);
}
