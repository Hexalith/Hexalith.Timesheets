using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Timesheets.Projections.MagicLinks;

namespace Hexalith.Timesheets.Projections.Tests;

/// <summary>One-shot deterministic failure at a durable SDK boundary, backed by serialized persisted bytes.</summary>
internal sealed class MagicLinkV1FaultStore(InMemoryReadModelStore inner) : IReadModelStore, IReadModelBatchStore, IReadModelConditionalEraser
{
    public string? Fault { get; set; }

    public int Fired { get; private set; }

    public int MaximumPhysicalMutations { get; private set; }

    public int MaximumRosterRows { get; private set; }
    public int MaximumPhysicalBytes { get; private set; }
    public int MaximumRosterBytes { get; private set; }
    public int ByteSplitPages { get; private set; }
    public Action? AfterSeal { get; set; }
    public int SealCallbacks { get; private set; }
    public Func<Task>? AfterOwnershipPersist { get; set; }
    public int OwnershipCallbacks { get; private set; }

    public bool RevokeAfterFirstTenant { get; set; }

    public Action? AfterFirstTenant { get; set; }

    public Task<ReadModelEntry<TValue>> GetAsync<TValue>(string storeName, string key,
        CancellationToken cancellationToken = default) where TValue : class
        => inner.GetAsync<TValue>(storeName, key, cancellationToken);

    public Task<bool> TryEraseAsync(string storeName, string key, string etag,
        CancellationToken cancellationToken = default) => inner.TryEraseAsync(storeName, key, etag, cancellationToken);

    public Task<(bool Present, string Etag)> TryReadEtagAsync(string storeName, string key,
        CancellationToken cancellationToken = default) => inner.TryReadEtagAsync(storeName, key, cancellationToken);

    public Task SaveAsync<TValue>(string storeName, string key, TValue value,
        CancellationToken cancellationToken = default) where TValue : class
    {
        if (Fault == "commit-marker" && key.Contains(":cow-committed:1", StringComparison.Ordinal)) Fail();
        return inner.SaveAsync(storeName, key, value, cancellationToken);
    }

    public async Task<bool> TrySaveAsync<TValue>(string storeName, string key, TValue value, string etag,
        CancellationToken cancellationToken = default) where TValue : class
    {
        if (value.GetType().Name == "SharedProjectionEpochState")
        {
            using JsonDocument document = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(value));
            JsonElement state = document.RootElement;
            if (state.GetProperty("OperationId").GetString() == "explicit-v1")
            {
                if (Fault == "seal" && state.GetProperty("StageFingerprint").ValueKind == JsonValueKind.String) Fail();
                if (Fault == "commit" && state.GetProperty("ActiveGeneration").GetInt64() == state.GetProperty("Epoch").GetInt64()) Fail();
                bool saved = await inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
                if (saved && Fault == "begin-after" && !state.GetProperty("StageStreaming").GetBoolean()
                    && state.GetProperty("Epoch").GetInt64() > state.GetProperty("ActiveGeneration").GetInt64()) Fail();
                if (saved && state.GetProperty("StageFingerprint").ValueKind == JsonValueKind.String
                    && state.GetProperty("Epoch").GetInt64() > state.GetProperty("ActiveGeneration").GetInt64())
                {
                    if (Fault == "seal-after") Fail();
                    if (AfterSeal is { } callback)
                    {
                        AfterSeal = null;
                        SealCallbacks++;
                        callback();
                    }
                }
                if (Fault == "checkpoint-advance" && state.GetProperty("StageCompletedPageCount").GetInt32() > 1
                    && (await inner.GetAsync<MagicLinkV1MigrationCheckpoint>(storeName,
                        PhysicalKey(1, MagicLinkTokenHashCapabilityIndexV1Migration.CheckpointKey(1, 1)), cancellationToken)).Value is not null)
                    Fail();
                return saved;
            }
        }
        bool persisted = await inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
        if (persisted && value is MagicLinkV1MigrationAbortWitness { OperationId: "explicit-v1" }
            && key.Contains(":ownership:", StringComparison.Ordinal) && AfterOwnershipPersist is { } ownershipCallback)
        {
            AfterOwnershipPersist = null;
            OwnershipCallbacks++;
            await ownershipCallback();
        }
        return persisted;
    }

    public async Task<ReadModelBatchResult> ExecuteAsync(ReadModelBatch batch,
        CancellationToken cancellationToken = default)
    {
        ReadModelBatchOperation[] physical = [.. batch.Operations.Where(operation => operation.Key.StartsWith("shared-generation:", StringComparison.Ordinal))];
        if (physical.Length > 0)
        {
            MaximumPhysicalMutations = Math.Max(MaximumPhysicalMutations, physical.Length);
            int actualBytes = physical.Sum(operation => operation.CanonicalValue.Length);
            MaximumPhysicalBytes = Math.Max(MaximumPhysicalBytes, actualBytes);
            bool hasNode = physical.Any(operation => operation.ValueTypeName.Contains("IndexTreeNode", StringComparison.Ordinal));
            bool hasTenantCheckpoint = physical.Any(operation => operation.ValueTypeName.Contains(nameof(MagicLinkV1MigrationCheckpoint), StringComparison.Ordinal)
                && JsonDocument.Parse(operation.CanonicalValue).RootElement.GetProperty("ordinal").GetInt64() == 1);
            foreach (ReadModelBatchOperation operation in physical.Where(operation => operation.ValueTypeName.Contains("TenantRosterPage", StringComparison.Ordinal)))
            {
                using JsonDocument page = JsonDocument.Parse(operation.CanonicalValue);
                MaximumRosterRows = Math.Max(MaximumRosterRows, page.RootElement.GetProperty("rows").GetArrayLength());
                MaximumRosterBytes = Math.Max(MaximumRosterBytes, operation.CanonicalValue.Length);
            }
            if (physical.Length < 8 && actualBytes > 128 * 1024
                && physical.Any(operation => operation.ValueTypeName.Contains("TenantRosterPage", StringComparison.Ordinal))) ByteSplitPages++;
            if (Fault == "checkpoint-zero-before" && physical.Any(operation => operation.ValueTypeName.Contains(nameof(MagicLinkV1MigrationCheckpoint), StringComparison.Ordinal)
                && JsonDocument.Parse(operation.CanonicalValue).RootElement.GetProperty("ordinal").GetInt64() == 0)) Fail();
            if (Fault == "node-before" && hasNode || Fault == "checkpoint-before" && hasTenantCheckpoint) Fail();
            ReadModelBatchResult result = await inner.ExecuteAsync(batch, cancellationToken);
            if (Fault == "abort-cleanup" && physical.Any(operation => operation.Kind == ReadModelBatchOperationKind.Delete
                && operation.Key == PhysicalKey(1, MagicLinkTokenHashCapabilityIndexV1Migration.CheckpointKey(1, 0)))) Fail();
            if (Fault == "node-after" && hasNode || Fault == "checkpoint-after" && hasTenantCheckpoint) Fail();
            if (Fault == "cancel-node" && hasNode)
            {
                Fault = null;
                Fired++;
                throw new OperationCanceledException("Simulated cancellation after a private node page.");
            }
            if (hasTenantCheckpoint && RevokeAfterFirstTenant)
            {
                RevokeAfterFirstTenant = false;
                AfterFirstTenant?.Invoke();
            }
            return result;
        }
        return await inner.ExecuteAsync(batch, cancellationToken);
    }

    private void Fail()
    {
        Fault = null;
        Fired++;
        throw new IOException("Simulated interruption at a durable migration boundary.");
    }

    private static string PhysicalKey(long generation, string logicalKey)
        => "shared-generation:" + Hexalith.Timesheets.Server.MagicLinks.MagicLinkTokenHashCapabilityIndexProjection.GlobalScope().ComputeHash()
            + ":" + generation + ":" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(logicalKey)));
}
