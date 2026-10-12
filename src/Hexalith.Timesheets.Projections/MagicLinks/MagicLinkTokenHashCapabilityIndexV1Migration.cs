using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks;

namespace Hexalith.Timesheets.Projections.MagicLinks;

/// <summary>
/// Explicit v1-to-v7 operator migration from independently verified issuance contributions. It never
/// treats the unique-only legacy index as a census. Writes use private EventStore generations, retaining
/// legacy data; retryable I/O and cancellation preserve the durable stage for replay after recreation.
/// </summary>
public sealed class MagicLinkTokenHashCapabilityIndexV1Migration(
    SharedProjectionEpochCoordinator coordinator,
    IReadModelStore readModelStore,
    IMagicLinkIndexMigrationAuthority authority,
    IMagicLinkV1VerifiedInventoryProvider inventory)
{
    private const int AppendWidth = 8;
    private const int AppendByteLimit = 256 * 1024;
    private const string Prefix = "timesheets:magic-links:token-hash-capability-index:v7:v1-migration:";

    /// <summary>Durable completion evidence; staged atomically with the v7 root and format witness.</summary>
    public const string CompletionKey = Prefix + "completion";

    /// <summary>Numeric checkpoint key independent of token or caller identities.</summary>
    public static string CheckpointKey(long epoch, long ordinal)
        => Prefix + epoch.ToString("D16", System.Globalization.CultureInfo.InvariantCulture) + ":"
            + ordinal.ToString("D16", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Numeric abort intent retained outside disposable SDK generation pages.</summary>
    public static string AbortWitnessKey(long epoch)
        => Prefix + "abort:" + epoch.ToString("D16", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Begins or resumes an explicitly authorized receipt. Empty journal and independently enforced
    /// source/delivery quiescence are mandatory; no legacy checkpoint/event identity is fabricated.
    /// </summary>
    public async Task MigrateAsync(string operationId, string receiptId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(receiptId);
        if (Encoding.UTF8.GetByteCount(operationId) > ReadModelBatchScope.MaxComponentByteLength
            || Encoding.UTF8.GetByteCount(receiptId) > ReadModelBatchScope.MaxComponentByteLength)
            throw new MagicLinkV1MigrationValidationException("The migration request exceeds supported identifier bounds.");
        // The uninstalled launch defaults have no migration authority or receipt to resume. Refuse
        // before contacting the store; installed providers still enter owned-stage invalidation below.
        if (inventory is UnavailableMagicLinkV1VerifiedInventoryProvider
            && authority is DenyAllMagicLinkIndexMigrationAuthority)
            throw new UnauthorizedAccessException("Verified migration evidence is unavailable.");
        SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        SharedProjectionEpochStatus before = await coordinator.GetStatusAsync(scope, cancellationToken)
            .ConfigureAwait(false);
        // Completion is already selected. Repairing its SDK marker needs the original identity and
        // current operator authority, not a new certification of an expired maintenance window.
        if (before.ActiveGeneration == before.Epoch && before.OperationId == operationId && !before.IsBuilding)
        {
            MagicLinkV1MigrationCheckpoint? completed = (await coordinator.ReadGenerationAsync<MagicLinkV1MigrationCheckpoint>(
                scope, before.ActiveGeneration, CompletionKey, cancellationToken).ConfigureAwait(false)).Value;
            if (completed is not null)
            {
                MagicLinkTokenHashCapabilityIndexTreeFormat? format = (await coordinator.ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexTreeFormat>(
                    scope, before.ActiveGeneration, MagicLinkTokenHashCapabilityIndexTreeReader.FormatKey, cancellationToken).ConfigureAwait(false)).Value;
                MagicLinkTokenHashCapabilityIndexTreeRoot? root = (await coordinator.ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(
                    scope, before.ActiveGeneration, MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, cancellationToken).ConfigureAwait(false)).Value;
                if (format?.StorageVersion != 7 || root is not { StorageVersion: 7, NextNodeId: >= 0 }
                    || completed.Receipt.ExpectedContributionCount > 0
                        && (root.TokenRoot is null || root.TenantRoot is null || root.OwnerRoot is null))
                    throw new MagicLinkV1MigrationValidationException("The selected completion has an invalid v7 layout.");
                MagicLinkV1InventoryReceipt? current = await inventory.GetVerifiedReceiptAsync(receiptId, cancellationToken).ConfigureAwait(false);
                if (completed.OperationId != operationId || completed.Receipt.ReceiptId != receiptId
                    || current is not null && current != completed.Receipt
                    || completed.CompletedContributionCount != completed.Receipt.ExpectedContributionCount)
                    throw new MagicLinkV1MigrationValidationException("The completed migration identity does not match.");
                await RequireAuthorityAsync(operationId, completed.Receipt, cancellationToken).ConfigureAwait(false);
                await coordinator.CommitAsync(scope, operationId, cancellationToken).ConfigureAwait(false);
                return;
            }
        }
        if ((before.IsBuilding || before.IsAborting) && before.OperationId != operationId)
            throw new MagicLinkV1MigrationValidationException("Another migration owns the private epoch.");
        MagicLinkV1MigrationAbortWitness? abort = before.IsBuilding || before.IsAborting
            || before.ActiveGeneration != before.Epoch && before.OperationId == operationId
            ? (await readModelStore.GetAsync<MagicLinkV1MigrationAbortWitness>(scope.StoreName,
                AbortWitnessKey(before.Epoch), cancellationToken).ConfigureAwait(false)).Value : null;
        MagicLinkV1MigrationAbortWitness? ownership = (await readModelStore.GetAsync<MagicLinkV1MigrationAbortWitness>(
            scope.StoreName, OwnershipKey(operationId), cancellationToken).ConfigureAwait(false)).Value;
        MagicLinkV1MigrationAbortWitness? bound = (before.IsBuilding || before.IsAborting)
            ? (await readModelStore.GetAsync<MagicLinkV1MigrationAbortWitness>(scope.StoreName,
                BoundOwnershipKey(before.Epoch), cancellationToken).ConfigureAwait(false)).Value : null;
        if (bound is not null)
        {
            if (ownership is null || bound.OperationId != ownership.OperationId || bound.Receipt != ownership.Receipt
                || bound.SourceDigest != ownership.SourceDigest || bound.Epoch != before.Epoch
                || bound.SourceGeneration != before.ActiveGeneration)
                throw new MagicLinkV1MigrationValidationException("The reserved epoch ownership is inconsistent.");
            ownership = bound;
        }
        MagicLinkV1MigrationCheckpoint? first = ownership is null && abort is null && (before.IsBuilding || before.IsAborting)
            ? (await coordinator.ReadGenerationAsync<MagicLinkV1MigrationCheckpoint>(scope, before.Epoch,
                CheckpointKey(before.Epoch, 0), cancellationToken).ConfigureAwait(false)).Value : null;
        ownership ??= abort ?? (first is null ? null : new(before.Epoch, operationId, first.Receipt,
            first.SourceGeneration, first.SourceDigest));
        bool ownsStage = ownership is not null && (before.IsBuilding || before.IsAborting)
            && ownership.Epoch == before.Epoch && ownership.OperationId == operationId
            && ownership.SourceGeneration == before.ActiveGeneration && ownership.Receipt.ReceiptId == receiptId;
        MagicLinkV1InventoryReceipt? resolved = await inventory.GetVerifiedReceiptAsync(receiptId, cancellationToken).ConfigureAwait(false);
        // Compare all retained fields before parsing their shape or performing any cleanup.
        if (ownership is not null && (ownership.OperationId != operationId || ownership.Receipt.ReceiptId != receiptId
            || resolved is not null && resolved != ownership.Receipt))
            throw new MagicLinkV1MigrationValidationException("The receipt conflicts with the reserved migration.");
        if (abort is not null && !before.IsBuilding && !before.IsAborting)
        {
            if (ownership is null || abort.OperationId != ownership.OperationId || abort.Receipt != ownership.Receipt
                || abort.SourceDigest != ownership.SourceDigest || abort.Epoch != before.Epoch
                || abort.SourceGeneration != before.ActiveGeneration)
                throw new MagicLinkV1MigrationValidationException("The completed abort ownership is inconsistent.");
            throw new MagicLinkV1MigrationValidationException("The aborted operation is terminal; use a fresh operation.");
        }
        if (before.IsBuilding && ownership is not null && bound is null)
        {
            await RequireAuthorityAsync(operationId, ownership.Receipt, cancellationToken).ConfigureAwait(false);
            // Begin verifies the retained full receipt fingerprint while the epoch is Building. If a
            // concurrent abort opened it, bind and isolate the actually acquired later reservation.
            // The same binding recovers a crash before its persistence.
            long actual = await coordinator.BeginAsync(scope, operationId, MagicLinkV1InventoryDigest.ReceiptFingerprint(ownership.Receipt),
                new Dictionary<string, long>(), cancellationToken, operationTenantId: scope.TenantId).ConfigureAwait(false);
            SharedProjectionEpochStatus acquiredState = await coordinator.GetStatusAsync(scope, cancellationToken).ConfigureAwait(false);
            if (!acquiredState.IsBuilding || acquiredState.Epoch != actual || acquiredState.OperationId != operationId)
                throw new InvalidOperationException("The migration reservation changed during ownership recovery.");
            MagicLinkV1MigrationAbortWitness acquired = new(actual, operationId, ownership.Receipt,
                acquiredState.ActiveGeneration, ownership.SourceDigest);
            await RequireAuthorityAsync(operationId, ownership.Receipt, cancellationToken).ConfigureAwait(false);
            _ = await PersistWitnessAsync(scope, BoundOwnershipKey(actual), acquired, cancellationToken).ConfigureAwait(false);
            bool changed = actual != before.Epoch || ownership.Epoch != actual
                || ownership.SourceGeneration != acquiredState.ActiveGeneration;
            ownership = acquired;
            ownsStage = true;
            if (changed)
            {
                await AbortAsync(scope, acquired).ConfigureAwait(false);
                throw new MagicLinkV1MigrationValidationException("The migration acquired an epoch after its source changed.");
            }
        }
        if (abort is not null || before.IsAborting)
        {
            if (ownership is null || !ownsStage || abort is not null && abort != ownership)
                throw new MagicLinkV1MigrationValidationException("The private abort has no matching retained ownership.");
            await AbortAsync(scope, ownership).ConfigureAwait(false);
            throw new MagicLinkV1MigrationValidationException("The invalid migration was aborted; use a fresh operation.");
        }
        if (!before.IsBuilding && !before.IsAborting && ownership is not null
            && ownership.Epoch == before.Epoch && before.ActiveGeneration != before.Epoch)
            throw new MagicLinkV1MigrationValidationException("The aborted operation cannot begin another epoch.");
        try
        {
            MagicLinkV1InventoryReceipt receipt = resolved
                ?? throw new MagicLinkV1MigrationValidationException("Verified migration evidence is unavailable.");
            ValidateReceiptShape(receipt, receiptId);
            await VerifyAuthorityAsync(operationId, receipt, cancellationToken).ConfigureAwait(false);
            if (before.PendingDeliveryCount != 0)
                throw new MagicLinkV1MigrationValidationException("Migration requires an empty global delivery journal.");
            string sourceDigest = await ReadLegacyDigestAsync(scope, before.ActiveGeneration, cancellationToken).ConfigureAwait(false);
            if (ownership is not null && (ownership.SourceGeneration != before.ActiveGeneration || ownership.SourceDigest != sourceDigest))
                throw new MagicLinkV1MigrationValidationException("The v1 source changed before migration resume.");
            await VerifyInventoryAsync(receipt, cancellationToken).ConfigureAwait(false);
            SharedProjectionEpochStatus currentFence = await coordinator.GetStatusAsync(scope, cancellationToken).ConfigureAwait(false);
            if (currentFence.Epoch != before.Epoch || currentFence.ActiveGeneration != before.ActiveGeneration
                || currentFence.OperationId != before.OperationId || currentFence.IsBuilding != before.IsBuilding
                || currentFence.PendingDeliveryCount != 0)
                throw new MagicLinkV1MigrationValidationException("The selected source changed during inventory preflight.");
            await VerifyAuthorityAsync(operationId, receipt, cancellationToken).ConfigureAwait(false);
            long reservedEpoch = before.IsBuilding ? before.Epoch : checked(before.Epoch + 1);
            MagicLinkV1MigrationAbortWitness reservation = new(reservedEpoch, operationId, receipt, before.ActiveGeneration, sourceDigest);
            if (ownership is not null && ownership != reservation)
                throw new MagicLinkV1MigrationValidationException("The durable reservation conflicts with this operation.");
            ownership = await PersistWitnessAsync(scope, OwnershipKey(operationId), reservation, cancellationToken).ConfigureAwait(false);
            long epoch = await coordinator.BeginAsync(scope, operationId, MagicLinkV1InventoryDigest.ReceiptFingerprint(receipt),
                new Dictionary<string, long>(), cancellationToken, operationTenantId: scope.TenantId).ConfigureAwait(false);
            SharedProjectionEpochStatus reserved = await coordinator.GetStatusAsync(scope, cancellationToken).ConfigureAwait(false);
            if (!reserved.IsBuilding || reserved.Epoch != epoch || reserved.OperationId != operationId)
                throw new InvalidOperationException("The acquired migration reservation changed before binding.");
            ownsStage = true;
            ownership = new(epoch, operationId, receipt, reserved.ActiveGeneration, sourceDigest);
            await RequireAuthorityAsync(operationId, receipt, cancellationToken).ConfigureAwait(false);
            _ = await PersistWitnessAsync(scope, BoundOwnershipKey(epoch), ownership, cancellationToken).ConfigureAwait(false);
            if (epoch != reservedEpoch || reserved.ActiveGeneration != before.ActiveGeneration)
                throw new MagicLinkV1MigrationValidationException("The migration reserved an epoch after its selected source changed.");
            MagicLinkV1MigrationCheckpoint checkpoint = await ReadCheckpointAsync(scope, operationId, receipt,
                before.ActiveGeneration, sourceDigest, epoch, cancellationToken).ConfigureAwait(false);
            SharedProjectionEpochStatus status = await coordinator.GetStatusAsync(scope, cancellationToken).ConfigureAwait(false);
            if (status.StageFingerprint is null)
            {
                checkpoint = await BuildAsync(scope, operationId, receipt, epoch, checkpoint, cancellationToken).ConfigureAwait(false);
                await VerifyPromotionAsync(scope, operationId, receipt, checkpoint, cancellationToken).ConfigureAwait(false);
                await coordinator.AppendStreamingPageAsync(scope, operationId, checkpoint.NextPageOrdinal,
                [
                    ReadModelBatchOperation.Write(MagicLinkTokenHashCapabilityIndexTreeReader.FormatKey,
                        new MagicLinkTokenHashCapabilityIndexTreeFormat(7), ReadModelBatchConcurrency.LastWrite),
                    ReadModelBatchOperation.Write(MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
                        checkpoint.Root, ReadModelBatchConcurrency.LastWrite),
                    ReadModelBatchOperation.Write(CompletionKey, checkpoint, ReadModelBatchConcurrency.LastWrite)
                ], cancellationToken).ConfigureAwait(false);
                await VerifyPromotionBoundaryAsync(scope, operationId, receipt, checkpoint, cancellationToken).ConfigureAwait(false);
                _ = await coordinator.SealStreamingAsync(scope, operationId, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                MagicLinkV1MigrationCheckpoint? completed = (await coordinator.ReadGenerationAsync<MagicLinkV1MigrationCheckpoint>(
                    scope, epoch, CompletionKey, cancellationToken).ConfigureAwait(false)).Value;
                if (JsonSerializer.Serialize(completed) != JsonSerializer.Serialize(checkpoint))
                    throw new MagicLinkV1MigrationValidationException("The sealed migration has mismatched completion evidence.");
            }
            await VerifyPromotionAsync(scope, operationId, receipt, checkpoint, cancellationToken).ConfigureAwait(false);
            await coordinator.CommitAsync(scope, operationId, cancellationToken).ConfigureAwait(false);
        }
        catch (MagicLinkV1MigrationValidationException)
        {
            if (ownsStage && ownership is not null) await AbortAsync(scope, ownership).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<MagicLinkV1MigrationCheckpoint> ReadCheckpointAsync(SharedProjectionScope scope,
        string operationId, MagicLinkV1InventoryReceipt receipt, long sourceGeneration,
        string sourceDigest, long epoch, CancellationToken cancellationToken)
    {
        MagicLinkV1MigrationCheckpoint? checkpoint = null;
        for (long ordinal = 0; ; ordinal++)
        {
            MagicLinkV1MigrationCheckpoint? current = (await coordinator
                .ReadGenerationAsync<MagicLinkV1MigrationCheckpoint>(scope, epoch,
                    CheckpointKey(epoch, ordinal), cancellationToken).ConfigureAwait(false)).Value;
            if (current is null) break;
            if (current.Ordinal != ordinal || current.OperationId != operationId || current.Receipt != receipt
                || current.SourceGeneration != sourceGeneration || current.SourceDigest != sourceDigest
                || current.Root is not { StorageVersion: 7, NextNodeId: >= 0 }
                || current.NextPageOrdinal < 1 || current.NextRetirementOrdinal < 0
                || current.CompletedContributionCount < 0
                || current.CompletedContributionCount > receipt.ExpectedContributionCount
                || ordinal == 0 && (current.LastTenantId is not null || current.CompletedContributionCount != 0)
                || ordinal > 0 && (current.LastTenantId is null || checkpoint is null
                    || string.CompareOrdinal(current.LastTenantId, checkpoint.LastTenantId) <= 0
                    || current.CompletedContributionCount <= checkpoint.CompletedContributionCount
                    || current.NextPageOrdinal <= checkpoint.NextPageOrdinal
                    || current.NextRetirementOrdinal < checkpoint.NextRetirementOrdinal
                    || current.Root.NextNodeId <= checkpoint.Root.NextNodeId))
                throw new MagicLinkV1MigrationValidationException("A private v1 migration checkpoint is inconsistent.");
            SharedProjectionStreamingStageStatus stage = await coordinator.GetStreamingStageStatusAsync(
                scope, operationId, cancellationToken).ConfigureAwait(false);
            if (stage.CompletedPageCount < current.NextPageOrdinal)
                await coordinator.AppendStreamingPageAsync(scope, operationId, current.NextPageOrdinal - 1,
                    [ReadModelBatchOperation.Write(CheckpointKey(epoch, ordinal), current,
                        ReadModelBatchConcurrency.LastWrite)], cancellationToken).ConfigureAwait(false);
            checkpoint = current;
        }
        if (checkpoint is not null) return checkpoint;
        checkpoint = new(0, operationId, receipt, sourceGeneration, sourceDigest, null, 0,
            new MagicLinkTokenHashCapabilityIndexTreeRoot(7, 0, null, null), 0, 1);
        await coordinator.AppendStreamingPageAsync(scope, operationId, 0,
            [ReadModelBatchOperation.Write(CheckpointKey(epoch, 0), checkpoint,
                ReadModelBatchConcurrency.LastWrite)], cancellationToken).ConfigureAwait(false);
        return checkpoint;
    }

    private async Task<MagicLinkV1MigrationCheckpoint> BuildAsync(SharedProjectionScope scope,
        string operationId, MagicLinkV1InventoryReceipt receipt, long epoch,
        MagicLinkV1MigrationCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        var reader = new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, scope);
        long replayed = 0;
        bool matchedCheckpoint = checkpoint.LastTenantId is null;
        await foreach (List<MagicLinkTokenHashCapabilityIndexDirectoryEntry> rows in ReadTenantsAsync(
            receipt, cancellationToken).ConfigureAwait(false))
        {
            replayed = checked(replayed + rows.Count);
            string tenant = rows[0].Candidate.Tenant.TenantId;
            if (checkpoint.LastTenantId is not null && string.CompareOrdinal(tenant, checkpoint.LastTenantId) <= 0)
            {
                await VerifyTenantMembershipAsync(reader, checkpoint.Root, tenant, rows, cancellationToken).ConfigureAwait(false);
                if (tenant == checkpoint.LastTenantId)
                {
                    if (replayed != checkpoint.CompletedContributionCount)
                        throw new MagicLinkV1MigrationValidationException("The inventory does not match the completed checkpoint.");
                    matchedCheckpoint = true;
                }
                continue;
            }
            if (!matchedCheckpoint)
                throw new MagicLinkV1MigrationValidationException("The checkpoint tenant is absent from the replayed inventory.");
            var builder = new MagicLinkTokenHashCapabilityIndexTreeBuilder(reader, checkpoint.Root, epoch);
            await builder.ReplaceTenantAsync(tenant, rows, cancellationToken).ConfigureAwait(false);
            int pageOrdinal = checkpoint.NextPageOrdinal;
            var page = new List<ReadModelBatchOperation>(AppendWidth);
            int pageBytes = 0;
            foreach (ReadModelBatchOperation operation in builder.Operations(false, checkpoint.NextRetirementOrdinal))
            {
                int bytes = checked(operation.CanonicalValue.Length + Encoding.UTF8.GetByteCount(operation.Key)
                    + Encoding.UTF8.GetByteCount(operation.ValueTypeName) + 1024);
                if (bytes > AppendByteLimit)
                    throw new MagicLinkV1MigrationValidationException("A private migration value exceeds the SDK append limit.");
                if (page.Count == AppendWidth || pageBytes + bytes > AppendByteLimit)
                {
                    await coordinator.AppendStreamingPageAsync(scope, operationId, pageOrdinal++, page,
                        cancellationToken).ConfigureAwait(false);
                    page = new(AppendWidth);
                    pageBytes = 0;
                }
                page.Add(operation);
                pageBytes += bytes;
            }
            if (page.Count != 0)
                await coordinator.AppendStreamingPageAsync(scope, operationId, pageOrdinal++, page,
                    cancellationToken).ConfigureAwait(false);
            checkpoint = checkpoint with
            {
                Ordinal = checked(checkpoint.Ordinal + 1), LastTenantId = tenant,
                CompletedContributionCount = replayed, Root = builder.SnapshotRoot,
                NextRetirementOrdinal = checked(checkpoint.NextRetirementOrdinal + builder.RetirementCount),
                NextPageOrdinal = checked(pageOrdinal + 1)
            };
            await coordinator.AppendStreamingPageAsync(scope, operationId, pageOrdinal,
                [ReadModelBatchOperation.Write(CheckpointKey(epoch, checkpoint.Ordinal), checkpoint,
                    ReadModelBatchConcurrency.LastWrite)], cancellationToken).ConfigureAwait(false);
        }
        if (!matchedCheckpoint || replayed != receipt.ExpectedContributionCount
            || checkpoint.CompletedContributionCount != replayed)
            throw new MagicLinkV1MigrationValidationException("The inventory is inconsistent with migration progress.");
        return checkpoint;
    }

    private async Task VerifyInventoryAsync(MagicLinkV1InventoryReceipt receipt, CancellationToken cancellationToken)
    {
        await foreach (List<MagicLinkTokenHashCapabilityIndexDirectoryEntry> _ in ReadTenantsAsync(receipt,
            cancellationToken).ConfigureAwait(false)) { }
    }

    private async IAsyncEnumerable<List<MagicLinkTokenHashCapabilityIndexDirectoryEntry>> ReadTenantsAsync(
        MagicLinkV1InventoryReceipt receipt, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        string? priorTenant = null;
        string? priorOwner = null;
        var rows = new List<MagicLinkTokenHashCapabilityIndexDirectoryEntry>();
        long count = 0;
        await foreach (MagicLinkTokenHashCapabilityIndexDirectoryEntry row in inventory
            .ReadContributionsAsync(receipt, cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (row?.Candidate?.Tenant is null || row.Candidate.CapabilityId is null
                || string.IsNullOrWhiteSpace(row.Candidate.Tenant.TenantId)
                || string.IsNullOrWhiteSpace(row.Candidate.CapabilityId.Value)
                || row.Candidate.CapabilityId.Value.Length > MagicLinkCapabilityId.MaxLength
                || Encoding.UTF8.GetByteCount(row.Candidate.Tenant.TenantId) > 256
                || string.IsNullOrWhiteSpace(row.TokenHash)
                || row.TokenHash.Length > MagicLinkTokenHash.MaxLength
                || row.TokenHash.Any(character => character is not (>= 'A' and <= 'Z' or >= 'a' and <= 'z'
                    or >= '0' and <= '9' or '-' or '_')))
                throw new MagicLinkV1MigrationValidationException("An inventory contribution is invalid.");
            string tenant = row.Candidate.Tenant.TenantId;
            string owner = row.Candidate.CapabilityId.Value;
            if (priorTenant is not null && (string.CompareOrdinal(tenant, priorTenant) < 0
                || tenant == priorTenant && string.CompareOrdinal(owner, priorOwner) <= 0))
                throw new MagicLinkV1MigrationValidationException("The inventory has unordered, duplicate or conflicting owners.");
            if (priorTenant is not null && tenant != priorTenant)
            {
                yield return rows;
                rows = [];
            }
            rows.Add(row);
            if (rows.Count > 10_000)
                throw new MagicLinkV1MigrationValidationException("A migration tenant exceeds the 10,000-contribution bound.");
            MagicLinkV1InventoryDigest.Append(hash, row);
            count = checked(count + 1);
            if (count > receipt.ExpectedContributionCount)
                throw new MagicLinkV1MigrationValidationException("The inventory exceeds its certified contribution count.");
            priorTenant = tenant;
            priorOwner = owner;
        }
        if (count != receipt.ExpectedContributionCount
            || Convert.ToHexString(hash.GetHashAndReset()) != receipt.ExpectedContributionDigest)
            throw new MagicLinkV1MigrationValidationException("The inventory count or digest does not match the server receipt.");
        if (rows.Count > 0) yield return rows;
    }

    private async Task VerifyPromotionAsync(SharedProjectionScope scope, string operationId,
        MagicLinkV1InventoryReceipt receipt, MagicLinkV1MigrationCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        var reader = new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, scope);
        long tenantCount = 0;
        await foreach (List<MagicLinkTokenHashCapabilityIndexDirectoryEntry> rows in ReadTenantsAsync(receipt,
            cancellationToken).ConfigureAwait(false))
        {
            await VerifyTenantMembershipAsync(reader, checkpoint.Root, rows[0].Candidate.Tenant.TenantId,
                rows, cancellationToken).ConfigureAwait(false);
            tenantCount++;
        }
        // Exact global leaf counts exclude extra candidates or tenants without retaining global rows.
        if (await CountMembershipAsync(reader, checkpoint.Root.TokenRoot, 0, string.Empty, new(), cancellationToken)
                .ConfigureAwait(false) != receipt.ExpectedContributionCount
            || await CountMembershipAsync(reader, checkpoint.Root.OwnerRoot, 1, string.Empty, new(), cancellationToken)
                .ConfigureAwait(false) != receipt.ExpectedContributionCount
            || await CountMembershipAsync(reader, checkpoint.Root.TenantRoot, 2, string.Empty, new(), cancellationToken)
                .ConfigureAwait(false) != tenantCount)
            throw new MagicLinkV1MigrationValidationException("The complete staged membership differs from certified source.");
        // Recheck after the entire replay, immediately before the selector transitions.
        await VerifyPromotionBoundaryAsync(scope, operationId, receipt, checkpoint, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<long> CountMembershipAsync(MagicLinkTokenHashCapabilityIndexTreeReader reader,
        MagicLinkTokenHashCapabilityIndexNodeReference? reference, int tree, string path,
        HashSet<(long Generation, long NodeId)> ancestors, CancellationToken cancellationToken)
    {
        if (reference is null) return 0;
        if (!ancestors.Add((reference.Generation, reference.NodeId)))
            throw new MagicLinkV1MigrationValidationException("A staged index path is cyclic.");
        try
        {
            MagicLinkTokenHashCapabilityIndexTreeNode node = await reader.ReadNodeAsync(reference, cancellationToken).ConfigureAwait(false);
            if (path.Length + node.Prefix.Length > 1400)
                throw new MagicLinkV1MigrationValidationException("A staged index path exceeds supported identifier bounds.");
            path += node.Prefix;
            long count = 0;
            if (node.Row is { } row)
            {
                string expected = tree == 0 ? MagicLinkTokenHashCapabilityIndexTreeReader.TokenPath(row.TokenHash, row.Candidate)
                    : MagicLinkTokenHashCapabilityIndexTreeReader.OwnerPath(row.Candidate.Tenant.TenantId, row.Candidate.CapabilityId.Value);
                if (tree == 2 || path != expected)
                    throw new MagicLinkV1MigrationValidationException("A staged contribution has an inconsistent path.");
                count++;
            }
            if (node.Roster is { } roster)
            {
                if (tree != 2 || path != MagicLinkTokenHashCapabilityIndexTreeReader.TenantPath(roster.TenantId))
                    throw new MagicLinkV1MigrationValidationException("A staged roster has an inconsistent path.");
                count++;
            }
            foreach ((char edge, MagicLinkTokenHashCapabilityIndexNodeReference child) in node.Children)
                count = checked(count + await CountMembershipAsync(reader, child, tree, path + edge,
                    ancestors, cancellationToken).ConfigureAwait(false));
            return count;
        }
        finally
        {
            ancestors.Remove((reference.Generation, reference.NodeId));
        }
    }

    private async Task VerifyPromotionBoundaryAsync(SharedProjectionScope scope, string operationId,
        MagicLinkV1InventoryReceipt receipt, MagicLinkV1MigrationCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        SharedProjectionEpochStatus status = await coordinator.GetStatusAsync(scope, cancellationToken).ConfigureAwait(false);
        if (!status.IsBuilding || status.OperationId != operationId || status.PendingDeliveryCount != 0
            || status.ActiveGeneration != checkpoint.SourceGeneration
            || checkpoint.CompletedContributionCount != receipt.ExpectedContributionCount
            || await ReadLegacyDigestAsync(scope, checkpoint.SourceGeneration, cancellationToken).ConfigureAwait(false) != checkpoint.SourceDigest)
            throw new MagicLinkV1MigrationValidationException("Migration source or quiescence changed before promotion.");
        await VerifyAuthorityAsync(operationId, receipt, cancellationToken).ConfigureAwait(false);
    }

    private static async Task VerifyTenantMembershipAsync(MagicLinkTokenHashCapabilityIndexTreeReader reader,
        MagicLinkTokenHashCapabilityIndexTreeRoot root, string tenant,
        IReadOnlyList<MagicLinkTokenHashCapabilityIndexDirectoryEntry> rows, CancellationToken cancellationToken)
    {
        IReadOnlyList<MagicLinkTokenHashCapabilityIndexDirectoryEntry>? roster = await reader.ReadRosterAsync(root.TenantRoot,
            tenant, cancellationToken).ConfigureAwait(false);
        if (roster is null || roster.Count != rows.Count || !new HashSet<MagicLinkTokenHashCapabilityIndexDirectoryEntry>(roster).SetEquals(rows))
            throw new MagicLinkV1MigrationValidationException("The staged tenant membership differs from certified source.");
        foreach (MagicLinkTokenHashCapabilityIndexDirectoryEntry row in rows)
            if (!await reader.ContainsExactRowAsync(root.TokenRoot, row, cancellationToken).ConfigureAwait(false)
                || await reader.FindOwnerAsync(root.OwnerRoot, tenant, row.Candidate.CapabilityId.Value, cancellationToken).ConfigureAwait(false) != row)
                throw new MagicLinkV1MigrationValidationException("A certified contribution is absent from the staged tries.");
    }

    private async Task RequireAuthorityAsync(string operationId, MagicLinkV1InventoryReceipt receipt,
        CancellationToken cancellationToken)
    {
        if (!await authority.AuthorizeCertifiedMigrationAsync(operationId,
            MagicLinkV1InventoryDigest.ReceiptFingerprint(receipt), cancellationToken).ConfigureAwait(false))
            throw new UnauthorizedAccessException("Verified migration evidence is unavailable.");
    }

    private async Task VerifyAuthorityAsync(string operationId, MagicLinkV1InventoryReceipt receipt,
        CancellationToken cancellationToken)
    {
        await RequireAuthorityAsync(operationId, receipt, cancellationToken).ConfigureAwait(false);
        if (!await inventory.ValidateReceiptAsync(receipt, cancellationToken).ConfigureAwait(false))
            throw new MagicLinkV1MigrationValidationException("Verified inventory evidence is invalid.");
    }

    private static string OwnershipKey(string operationId)
        => Prefix + "ownership:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(operationId)));

    private static string BoundOwnershipKey(long epoch)
        => Prefix + "reserved:" + epoch.ToString("D16", System.Globalization.CultureInfo.InvariantCulture);

    private Task<MagicLinkV1MigrationAbortWitness> PersistWitnessAsync(SharedProjectionScope scope, string key,
        MagicLinkV1MigrationAbortWitness witness, CancellationToken cancellationToken)
        => ReadModelWritePolicy.UpdateAsync<MagicLinkV1MigrationAbortWitness>(readModelStore, scope.StoreName, key,
            current => current is null || current == witness ? witness
                : throw new MagicLinkV1MigrationValidationException("The durable witness conflicts with this operation."),
            cancellationToken: cancellationToken);

    private async Task<string> ReadLegacyDigestAsync(SharedProjectionScope scope, long sourceGeneration,
        CancellationToken cancellationToken)
    {
        ReadModelEntry<MagicLinkTokenHashCapabilityIndexReadModel> selected = await coordinator
            .ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope, sourceGeneration,
                MagicLinkTokenHashCapabilityIndexProjection.StateKey, cancellationToken).ConfigureAwait(false);
        ReadModelEntry<MagicLinkTokenHashCapabilityIndexReadModel> direct = await readModelStore
            .GetAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope.StoreName,
                MagicLinkTokenHashCapabilityIndexProjection.StateKey, cancellationToken).ConfigureAwait(false);
        if (selected.Value is null && direct.Value is null)
            throw new MagicLinkV1MigrationValidationException("Migration requires a direct or selected v1 root.");
        foreach (MagicLinkTokenHashCapabilityIndexReadModel root in new[] { selected.Value, direct.Value }.OfType<MagicLinkTokenHashCapabilityIndexReadModel>())
        {
            if (root.StorageVersion is not (0 or 1) || root.Entries is null
                || root.Contributions is null || root.Contributions.Count != 0
                || root.DirectoryPages is null || root.DirectoryPages.Count != 0
                || root.CandidatePageCounts is null || root.CandidatePageCounts.Count != 0
                || root.CandidatePageIds is null || root.CandidatePageIds.Count != 0
                || root.CandidatePageDigests is null || root.CandidatePageDigests.Count != 0
                || root.DirectoryPageDigests is null || root.DirectoryPageDigests.Count != 0
                || root.NextCandidatePageId != 0
                || root.Entries.Any(pair => string.IsNullOrWhiteSpace(pair.Key)
                    || pair.Value?.Tenant is null || pair.Value.CapabilityId is null))
                throw new MagicLinkV1MigrationValidationException("The legacy root has an unsupported mixed layout.");
        }
        // Both direct and selected newer layouts are refused, including partial witnesses.
        if ((await coordinator.ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexTreeFormat>(scope,
                sourceGeneration, MagicLinkTokenHashCapabilityIndexTreeReader.FormatKey, cancellationToken).ConfigureAwait(false)).Value is not null
            || (await coordinator.ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
                sourceGeneration, MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, cancellationToken).ConfigureAwait(false)).Value is not null
            || (await readModelStore.GetAsync<MagicLinkTokenHashCapabilityIndexTreeFormat>(scope.StoreName,
                MagicLinkTokenHashCapabilityIndexTreeReader.FormatKey, cancellationToken).ConfigureAwait(false)).Value is not null
            || (await readModelStore.GetAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope.StoreName,
                MagicLinkTokenHashCapabilityIndexTreeReader.RootKey, cancellationToken).ConfigureAwait(false)).Value is not null)
            throw new MagicLinkV1MigrationValidationException("The legacy source contains a mixed v7 layout.");
        for (int shard = 0; shard < MagicLinkTokenHashCapabilityIndexProjection.ShardCount; shard++)
        {
            string key = MagicLinkTokenHashCapabilityIndexProjection.ShardKey(shard);
            if ((await coordinator.ReadGenerationAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope,
                    sourceGeneration, key, cancellationToken).ConfigureAwait(false)).Value is not null
                || (await readModelStore.GetAsync<MagicLinkTokenHashCapabilityIndexReadModel>(scope.StoreName,
                    key, cancellationToken).ConfigureAwait(false)).Value is not null)
                throw new MagicLinkV1MigrationValidationException("The legacy source contains a mixed shard layout.");
        }
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            SourceGeneration = sourceGeneration,
            Selected = selected.Value, SelectedETag = selected.ETag,
            Direct = direct.Value, DirectETag = direct.ETag
        })));
    }

    private static void ValidateReceiptShape(MagicLinkV1InventoryReceipt receipt, string receiptId)
    {
        if (receipt.ReceiptId != receiptId || string.IsNullOrWhiteSpace(receipt.SourceCutRevision)
            || string.IsNullOrWhiteSpace(receipt.InstallationCoverageReceiptId)
            || string.IsNullOrWhiteSpace(receipt.QuiescenceReceiptId)
            || new[] { receipt.ReceiptId, receipt.SourceCutRevision, receipt.InstallationCoverageReceiptId,
                receipt.QuiescenceReceiptId }.Any(value => Encoding.UTF8.GetByteCount(value) > ReadModelBatchScope.MaxComponentByteLength)
            || receipt.ExpectedContributionCount < 0 || receipt.ExpectedContributionDigest is not { Length: 64 }
            || receipt.ExpectedContributionDigest.Any(character => character is not (>= '0' and <= '9' or >= 'A' and <= 'F')))
            throw new MagicLinkV1MigrationValidationException("The verified server receipt is malformed.");
    }

    private async Task AbortAsync(SharedProjectionScope scope, MagicLinkV1MigrationAbortWitness witness)
    {
        SharedProjectionEpochStatus status = await coordinator.GetStatusAsync(scope, CancellationToken.None)
            .ConfigureAwait(false);
        if ((status.IsBuilding || status.IsAborting) && status.OperationId == witness.OperationId
            && status.Epoch == witness.Epoch && status.ActiveGeneration == witness.SourceGeneration)
        {
            await RequireAuthorityAsync(witness.OperationId, witness.Receipt, CancellationToken.None).ConfigureAwait(false);
            _ = await PersistWitnessAsync(scope, AbortWitnessKey(witness.Epoch), witness, CancellationToken.None).ConfigureAwait(false);
            // Quiescence is a prerequisite. Never manufacture a legacy fold or acknowledge a delivery
            // arriving outside that prerequisite; an unexpected journal leaves a recoverable abort.
            await coordinator.AbortAsync(scope, witness.OperationId, (_, _, _) =>
                throw new MagicLinkV1MigrationValidationException("Legacy delivery recovery requires verified event identities."),
                CancellationToken.None).ConfigureAwait(false);
        }
    }
}
