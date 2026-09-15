using System.Text.Json;

using Hexalith.EventStore.Client.Attributes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.Runtime;

using Microsoft.Extensions.Logging;

namespace Hexalith.Timesheets.Projections.MagicLinks;

/// <summary>Persists and rebuilds the non-authoritative magic-link token-hash candidate index.</summary>
/// <remarks>
/// The explicit domain attribute is what makes this handler discoverable by the SDK's domain-telemetry
/// scan: that scan only instantiates handlers exposing a parameterless constructor, which this
/// store-injected handler deliberately does not.
/// </remarks>
[EventStoreDomain(TimesheetsEventStoreIntegration.DomainName)]
public sealed class MagicLinkTokenHashCapabilityIndexProjectionHandler(
    IReadModelStore readModelStore,
    ILoggerFactory? loggerFactory = null) :
    IAsyncDomainSharedProjectionRebuildHandler,
    IDeclaresProjectionReadModelSlots
{
    /// <summary>The logical read-model slot this handler is the canonical writer for.</summary>
    public const string SlotName = "index";

    // An explicit category rather than ILogger<T>: this type's own name carries the word the
    // DiagnosticsPrivacyTests source scan forbids on any logging line, and that guard is worth
    // keeping strict. Nothing token-derived is ever logged; the category is a fixed literal.
    private const string LogCategory = "Hexalith.Timesheets.Projections.CandidateIndexProjection";

    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger? _logger = loggerFactory?.CreateLogger(LogCategory);

    /// <summary>Gets the canonical shared slot declaration.</summary>
    public static IReadOnlyList<ProjectionReadModelSlotDeclaration> ProjectionReadModelSlots { get; } =
    [
        new(
            TimesheetsEventStoreIntegration.DomainName,
            MagicLinkTokenHashCapabilityIndexProjection.ProjectionName,
            SlotName,
            ProjectionReadModelSlotKind.Shared,
            declaresCanonicalWriter: true)
    ];

    /// <inheritdoc />
    public string Domain => TimesheetsEventStoreIntegration.DomainName;

    /// <inheritdoc />
    public string ProjectionType => MagicLinkTokenHashCapabilityIndexProjection.ProjectionName;

    /// <inheritdoc />
    public string RebuildStoreName => MagicLinkTokenHashCapabilityIndexProjection.StateStoreName;

    /// <inheritdoc />
    public async Task<DomainProjectionHandlerResult> ProjectAsync(
        ProjectionRequest request,
        string dispatchId,
        CancellationToken cancellationToken)
    {
        Validate(request, dispatchId);
        cancellationToken.ThrowIfCancellationRequested();

        MagicLinkConfirmationCapabilityIssued? issued;
        try
        {
            issued = FoldIssuance(request);
        }
        catch (InvalidOperationException)
        {
            return DomainProjectionHandlerResult.Failed(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }

        if (issued is null)
        {
            return DomainProjectionHandlerResult.AlreadyCompleted();
        }

        try
        {
            _ = await ReadModelWritePolicy.UpdateAsync<MagicLinkTokenHashCapabilityIndexReadModel>(
                readModelStore,
                RebuildStoreName,
                MagicLinkTokenHashCapabilityIndexProjection.StateKey,
                current => Guarded(() => ApplyChecked(current, issued)),
                new ReadModelWriteContext(SlotName, ProjectionType).WithEventDiagnostics(request.Events),
                _logger,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return DomainProjectionHandlerResult.Completed();
        }
        catch (IndexCandidateConflictException)
        {
            return DomainProjectionHandlerResult.Failed(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }
        catch (ProjectionFoldException)
        {
            // An apply defect is deterministic: reporting it as a transient store outage would have
            // the coordinator retry the same failing delivery forever. Store failures and an
            // exhausted retry budget stay Retryable below, even though they share CLR types.
            cancellationToken.ThrowIfCancellationRequested();
            return DomainProjectionHandlerResult.Failed(ProjectionDispatchReasonCodes.HandlerFailure);
        }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return DomainProjectionHandlerResult.Retryable(ProjectionDispatchReasonCodes.DeliveryStateUnavailable);
        }
    }

    /// <inheritdoc />
    public Task<DomainSharedProjectionRebuildCandidate> CreateEmptyCandidateAsync(
        DomainSharedProjectionRebuildIdentity identity,
        CancellationToken cancellationToken)
    {
        Validate(identity);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ToCandidate(new IndexRebuildCandidate([])));
    }

    /// <inheritdoc />
    public Task<DomainSharedProjectionRebuildCandidate> AccumulateAsync(
        DomainSharedProjectionRebuildIdentity identity,
        DomainSharedProjectionRebuildCandidate candidate,
        ProjectionRequest aggregateHistory,
        CancellationToken cancellationToken)
    {
        Validate(identity);
        ArgumentNullException.ThrowIfNull(candidate);
        Validate(aggregateHistory, identity.OperationId);
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(identity.TenantId, aggregateHistory.TenantId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The aggregate history is outside the rebuild tenant.");
        }

        IndexRebuildCandidate current = FromCandidate(candidate);
        MagicLinkConfirmationCapabilityIssued? issued = FoldIssuance(aggregateHistory);
        if (issued is null)
        {
            return Task.FromResult(candidate);
        }

        return Task.FromResult(ToCandidate(AddCandidate(current, issued)));
    }

    /// <inheritdoc />
    public async Task<DomainProjectionRebuildPlan> FinalizeAsync(
        DomainSharedProjectionRebuildIdentity identity,
        DomainSharedProjectionRebuildCandidate candidate,
        CancellationToken cancellationToken)
    {
        Validate(identity);
        ArgumentNullException.ThrowIfNull(candidate);
        cancellationToken.ThrowIfCancellationRequested();

        ReadModelEntry<MagicLinkTokenHashCapabilityIndexReadModel> current = await readModelStore
            .GetAsync<MagicLinkTokenHashCapabilityIndexReadModel>(
                RebuildStoreName,
                MagicLinkTokenHashCapabilityIndexProjection.StateKey,
                cancellationToken)
            .ConfigureAwait(false);
        MagicLinkTokenHashCapabilityIndexReadModel merged = MagicLinkTokenHashCapabilityIndexProjection.ReplaceTenant(
            current.Value,
            identity.TenantId,
            ToLoaderVisibleIndex(FromCandidate(candidate), identity.TenantId));

        return new DomainProjectionRebuildPlan(
            RebuildStoreName,
            [ReadModelBatchOperation.Write(
                MagicLinkTokenHashCapabilityIndexProjection.StateKey,
                merged,
                ReadModelRebuildConcurrency.For(current))]);
    }

    private static MagicLinkTokenHashCapabilityIndexReadModel ApplyChecked(
        MagicLinkTokenHashCapabilityIndexReadModel? current,
        MagicLinkConfirmationCapabilityIssued issued)
    {
        if (current?.Entries.TryGetValue(
                issued.TokenHash.Value,
                out MagicLinkTokenHashCapabilityIndexEntry? existing) == true
            && existing != new MagicLinkTokenHashCapabilityIndexEntry(issued.Tenant, issued.CapabilityId))
        {
            throw new IndexCandidateConflictException();
        }

        return MagicLinkTokenHashCapabilityIndexProjection.Apply(current, issued);
    }

    private static MagicLinkTokenHashCapabilityIndexReadModel Guarded(
        Func<MagicLinkTokenHashCapabilityIndexReadModel> apply)
    {
        try
        {
            return apply();
        }
        catch (Exception exception)
            when (exception is InvalidOperationException or ArgumentException or NullReferenceException)
        {
            throw new ProjectionFoldException(exception);
        }
    }

    private static MagicLinkConfirmationCapabilityIssued? FoldIssuance(ProjectionRequest request)
    {
        MagicLinkConfirmationCapabilityIssued[] issuances = ProjectionEventReader.Normalize(request.Events)
            .Select(ProjectionEventReader.Deserialize<MagicLinkConfirmationCapabilityIssued>)
            .Where(static item => item is not null)
            .Cast<MagicLinkConfirmationCapabilityIssued>()
            .ToArray();
        if (issuances.Length == 0)
        {
            return null;
        }

        if (issuances.Length != 1)
        {
            throw new InvalidOperationException("The issuance event does not match its projection scope.");
        }

        MagicLinkConfirmationCapabilityIssued issuance = issuances[0];
        if (issuance.Tenant is null
            || string.IsNullOrWhiteSpace(issuance.Tenant.TenantId)
            || issuance.CapabilityId is null
            || string.IsNullOrWhiteSpace(issuance.CapabilityId.Value)
            || issuance.TokenHash is null
            || string.IsNullOrWhiteSpace(issuance.TokenHash.Value)
            || !string.Equals(issuance.Tenant.TenantId, request.TenantId, StringComparison.Ordinal)
            || !string.Equals(issuance.CapabilityId.Value, request.AggregateId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The issuance event does not match its projection scope.");
        }

        return issuance;
    }

    private static IndexRebuildCandidate AddCandidate(
        IndexRebuildCandidate current,
        MagicLinkConfirmationCapabilityIssued issued)
        => CanonicalCandidate(current.Entries.Append(new IndexRebuildCandidateEntry(
            issued.TokenHash.Value,
            new MagicLinkTokenHashCapabilityIndexEntry(issued.Tenant, issued.CapabilityId))));

    private static IndexRebuildCandidate CanonicalCandidate(IEnumerable<IndexRebuildCandidateEntry> entries)
        => new(entries
            .Distinct()
            .OrderBy(static entry => entry.TokenHash, StringComparer.Ordinal)
            .ThenBy(static entry => entry.Candidate.Tenant.TenantId, StringComparer.Ordinal)
            .ThenBy(static entry => entry.Candidate.CapabilityId.Value, StringComparer.Ordinal)
            .ToArray());

    private static MagicLinkTokenHashCapabilityIndexReadModel ToLoaderVisibleIndex(
        IndexRebuildCandidate candidate,
        string tenantId)
    {
        if (candidate.Entries.Any(entry =>
                !string.Equals(entry.Candidate.Tenant.TenantId, tenantId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("The magic-link index candidate contains another tenant's entry.");
        }

        Dictionary<string, MagicLinkTokenHashCapabilityIndexEntry> uniqueEntries = candidate.Entries
            .GroupBy(static entry => entry.TokenHash, StringComparer.Ordinal)
            .Select(static group => new
            {
                TokenHash = group.Key,
                Candidates = group.Select(static entry => entry.Candidate).Distinct().ToArray()
            })
            .Where(static group => group.Candidates.Length == 1)
            .OrderBy(static group => group.TokenHash, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.TokenHash,
                static group => group.Candidates[0],
                StringComparer.Ordinal);
        return new MagicLinkTokenHashCapabilityIndexReadModel(uniqueEntries);
    }

    private static DomainSharedProjectionRebuildCandidate ToCandidate(IndexRebuildCandidate candidate)
        => new(JsonSerializer.SerializeToUtf8Bytes(candidate, s_jsonOptions));

    private static IndexRebuildCandidate FromCandidate(
        DomainSharedProjectionRebuildCandidate candidate)
        => CanonicalCandidate(JsonSerializer.Deserialize<IndexRebuildCandidate>(
                candidate.State.Span,
                s_jsonOptions)
            ?.Entries
            ?? throw new InvalidOperationException("The magic-link index rebuild candidate is malformed."));

    private static void Validate(ProjectionRequest request, string dispatchId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(dispatchId);
        if (!string.Equals(request.Domain, TimesheetsEventStoreIntegration.DomainName, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(request.TenantId)
            || string.IsNullOrWhiteSpace(request.AggregateId))
        {
            throw new ArgumentException("The projection request is outside the Timesheets route.", nameof(request));
        }
    }

    private static void Validate(DomainSharedProjectionRebuildIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (!string.Equals(identity.Domain, TimesheetsEventStoreIntegration.DomainName, StringComparison.Ordinal)
            || !string.Equals(identity.ProjectionType, MagicLinkTokenHashCapabilityIndexProjection.ProjectionName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The shared rebuild identity targets a different projection.");
        }
    }
}
