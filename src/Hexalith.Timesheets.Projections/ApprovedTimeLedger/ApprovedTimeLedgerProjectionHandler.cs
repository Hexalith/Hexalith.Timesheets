using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Attributes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Timesheets.Contracts;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Projections.TimeEntries;
using Hexalith.Timesheets.ReadModels.ApprovedTimeLedger;
using Hexalith.Timesheets.ReadModels.TimeEntries;
using Hexalith.Timesheets.Server.Policies;

namespace Hexalith.Timesheets.Projections.ApprovedTimeLedger;

/// <summary>Materializes the tenant ledger through the SDK's generation-selected delivery and rebuild paths.</summary>
[EventStoreDomain(TimesheetsDomain.Name)]
public sealed class ApprovedTimeLedgerProjectionHandler(
    IReadModelStore store,
    TimesheetsEvidencePolicyOptions? evidencePolicyOptions = null) :
    IAsyncDomainSharedProjectionEpochHandler, IDeclaresProjectionReadModelSlots
{
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);
    private readonly TimeEntryEvidenceProjectionHandler _evidence = new(store, evidencePolicyOptions);

    /// <summary>Gets the canonical shared ledger slot.</summary>
    public static IReadOnlyList<ProjectionReadModelSlotDeclaration> ProjectionReadModelSlots { get; } =
        [new(TimesheetsDomain.Name, ApprovedTimeLedgerEpoch.ProjectionName, ApprovedTimeLedgerEpoch.SlotName,
            ProjectionReadModelSlotKind.Shared, declaresCanonicalWriter: true)];

    /// <inheritdoc />
    public string Domain => TimesheetsDomain.Name;

    /// <inheritdoc />
    public string ProjectionType => ApprovedTimeLedgerEpoch.ProjectionName;

    /// <inheritdoc />
    public string RebuildStoreName => ApprovedTimeLedgerEpoch.StoreName;

    /// <inheritdoc />
    public bool EpochEnabled => true;

    /// <inheritdoc />
    public string OrdinaryWriterId => ApprovedTimeLedgerEpoch.OrdinaryWriterId;

    /// <inheritdoc />
    public SharedProjectionScope CreateScope(string tenantId) => ApprovedTimeLedgerEpoch.CreateScope(tenantId);

    /// <inheritdoc />
    public Task<DomainProjectionHandlerResult> ProjectAsync(
        ProjectionRequest request, string dispatchId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Only named SDK delivery may mutate this shared, fenced slot.
        return Task.FromResult(DomainProjectionHandlerResult.Failed(ProjectionDispatchReasonCodes.UnsupportedCapability));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReadModelBatchOperation>> FoldCatchUpAsync(
        SharedProjectionDelivery delivery, long generation, SharedProjectionEpochCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        ProjectionRequest request;
        ProjectionEventDto item;
        try
        {
            request = JsonSerializer.Deserialize<ProjectionRequest>(delivery.CanonicalPayload, s_json)
                ?? throw new InvalidOperationException("Ledger delivery is missing.");
            Validate(request.Domain, request.TenantId);
            if (request.Events is not [var single] || request.AggregateId != delivery.SourceStream
                || single.SequenceNumber != delivery.OwnerSequenceNumber || single.GlobalPosition != delivery.EnvelopePosition)
            {
                throw new InvalidOperationException("Ledger delivery conflicts with its source.");
            }

            item = ProjectionEventReader.Normalize(request.Events, single.SequenceNumber)[0];
        }
        catch (Exception exception) when (IsInvalidEvidence(exception))
        {
            throw Park(delivery);
        }

        ReadModelEntry<ApprovedTimeLedgerSnapshot> selected = await coordinator
            .ReadGenerationAsync<ApprovedTimeLedgerSnapshot>(CreateScope(request.TenantId), generation,
                ApprovedTimeLedgerEpoch.SlotName, cancellationToken).ConfigureAwait(false);
        try
        {
            ApprovedTimeLedgerSnapshot snapshot = selected.Value ?? Empty(request.TenantId);
            ValidateSnapshot(snapshot, request.TenantId);
            snapshot.Owners.TryGetValue(request.AggregateId, out JsonObject? owner);
            JsonObject? next;
            if (owner is null)
            {
                object? payload = TimeEntryEvidenceProjectionHandler.Decode(item);
                if (payload is null)
                {
                    return [];
                }

                TimeEntryEvidenceReadModel evidence = _evidence.Fold(request, out string? digest)
                    ?? throw new InvalidOperationException("Ledger delivery lacks a complete Time Entry prefix.");
                next = TimeEntryEvidenceCompactStorage.Pack(evidence, digest);
            }
            else
            {
                next = _evidence.AppendSelectedSuffix(owner, request, item);
            }

            snapshot.Owners[request.AggregateId] = next;
            return [Write(snapshot with { Position = Math.Max(snapshot.Position, item.GlobalPosition) })];
        }
        catch (Exception exception) when (IsInvalidEvidence(exception))
        {
            throw Park(delivery);
        }
    }

    /// <inheritdoc />
    public Task<DomainSharedProjectionRebuildCandidate> CreateEmptyCandidateAsync(
        DomainSharedProjectionRebuildIdentity identity, CancellationToken cancellationToken)
    {
        Validate(identity);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ToCandidate(Empty(identity.TenantId)));
    }

    /// <inheritdoc />
    public Task<DomainSharedProjectionRebuildCandidate> AccumulateAsync(
        DomainSharedProjectionRebuildIdentity identity, DomainSharedProjectionRebuildCandidate candidate,
        ProjectionRequest aggregateHistory, CancellationToken cancellationToken)
    {
        Validate(identity);
        Validate(aggregateHistory.Domain, aggregateHistory.TenantId);
        if (aggregateHistory.TenantId != identity.TenantId)
        {
            throw new InvalidOperationException("Ledger rebuild has an owner outside its tenant.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        ApprovedTimeLedgerSnapshot snapshot = FromCandidate(identity, candidate);
        TimeEntryEvidenceReadModel? evidence = _evidence.Fold(aggregateHistory, out string? digest);
        if (evidence is null)
        {
            return Task.FromResult(candidate);
        }

        if (!snapshot.Owners.TryAdd(aggregateHistory.AggregateId, TimeEntryEvidenceCompactStorage.Pack(evidence, digest)))
        {
            throw new InvalidOperationException("Ledger rebuild has duplicate Time Entry owners.");
        }

        return Task.FromResult(ToCandidate(snapshot with
        {
            Position = Math.Max(snapshot.Position, aggregateHistory.Events.Max(item => item.GlobalPosition))
        }));
    }

    /// <inheritdoc />
    public Task<DomainProjectionRebuildPlan> FinalizeAsync(
        DomainSharedProjectionRebuildIdentity identity, DomainSharedProjectionRebuildCandidate candidate,
        CancellationToken cancellationToken)
    {
        Validate(identity);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new DomainProjectionRebuildPlan(RebuildStoreName, [Write(FromCandidate(identity, candidate))]));
    }

    private static ApprovedTimeLedgerSnapshot Empty(string tenantId)
        => new(tenantId, 0, new(StringComparer.Ordinal));

    private static ReadModelBatchOperation Write(ApprovedTimeLedgerSnapshot snapshot)
        => ReadModelBatchOperation.Write(ApprovedTimeLedgerEpoch.SlotName, snapshot, ReadModelBatchConcurrency.LastWrite);

    private static DomainSharedProjectionRebuildCandidate ToCandidate(ApprovedTimeLedgerSnapshot snapshot)
        => new(JsonSerializer.SerializeToUtf8Bytes(snapshot, s_json));

    private static ApprovedTimeLedgerSnapshot FromCandidate(
        DomainSharedProjectionRebuildIdentity identity, DomainSharedProjectionRebuildCandidate candidate)
    {
        ApprovedTimeLedgerSnapshot snapshot = JsonSerializer.Deserialize<ApprovedTimeLedgerSnapshot>(candidate.State.Span, s_json)
            ?? throw new InvalidOperationException("Ledger rebuild evidence is missing.");
        ValidateSnapshot(snapshot, identity.TenantId);
        return snapshot;
    }

    private static void ValidateSnapshot(ApprovedTimeLedgerSnapshot snapshot, string tenantId)
    {
        if (snapshot.TenantId != tenantId || snapshot.Position < 0 || snapshot.Owners is null)
        {
            throw new InvalidOperationException("Ledger evidence does not match its tenant scope.");
        }
    }

    private static void Validate(DomainSharedProjectionRebuildIdentity identity)
    {
        Validate(identity.Domain, identity.TenantId);
        if (identity.ProjectionType != ApprovedTimeLedgerEpoch.ProjectionName)
        {
            throw new InvalidOperationException("The rebuild targets a different projection.");
        }
    }

    private static void Validate(string domain, string tenantId)
    {
        if (domain != TimesheetsDomain.Name || string.IsNullOrWhiteSpace(tenantId))
        {
            throw new InvalidOperationException("The request does not match the ledger scope.");
        }
    }

    private static bool IsInvalidEvidence(Exception exception)
        => exception is InvalidOperationException or ArgumentException or JsonException or ProjectionFoldException;

    private static SharedProjectionFoldFailureException Park(SharedProjectionDelivery delivery)
    {
        string suffix = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            FormattableString.Invariant($"{delivery.SourceStream}\0{delivery.EnvelopePosition}"))));
        return new(delivery.SourceStream, delivery.EnvelopePosition, 2,
            attempts => [ReadModelBatchOperation.Write("ledger:parked:" + suffix, new { attempts }, ReadModelBatchConcurrency.LastWrite)]);
    }
}
