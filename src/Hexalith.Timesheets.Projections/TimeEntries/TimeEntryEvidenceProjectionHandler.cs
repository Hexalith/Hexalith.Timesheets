using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Attributes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.Policies;
using Hexalith.Timesheets.Server.Runtime;

namespace Hexalith.Timesheets.Projections.TimeEntries;

/// <summary>Materializes validated, aggregate-owned Time Entry evidence.</summary>
/// <param name="readModelStore">The SDK read-model store used for owned evidence.</param>
/// <param name="evidencePolicyOptions">Trusted server policy for comment publication.</param>
[EventStoreDomain(TimesheetsEventStoreIntegration.DomainName)]
public sealed class TimeEntryEvidenceProjectionHandler(
    IReadModelStore readModelStore, TimesheetsEvidencePolicyOptions? evidencePolicyOptions = null) :
    IAsyncDomainProjectionRebuildHandler,
    IAsyncDomainProjectionIncrementalReplayHandler,
    IDeclaresProjectionReadModelSlots
{
    private readonly bool _trustedCommentPublicationAllowed = evidencePolicyOptions is
    { CommentSensitivityPolicyConfigured: true, ProjectionCommentsAllowed: true };
    /// <summary>Gets the canonical aggregate-owned evidence slot.</summary>
    public static IReadOnlyList<ProjectionReadModelSlotDeclaration> ProjectionReadModelSlots { get; } =
    [
        new(
            TimesheetsEventStoreIntegration.DomainName,
            TimeEntryEvidenceProjection.ProjectionName,
            TimeEntryEvidenceReadModelAddress.SlotName,
            ProjectionReadModelSlotKind.AggregateOwned,
            declaresCanonicalWriter: true)
    ];

    /// <inheritdoc />
    public string Domain => TimesheetsEventStoreIntegration.DomainName;

    /// <inheritdoc />
    public string ProjectionType => TimeEntryEvidenceProjection.ProjectionName;

    /// <inheritdoc />
    public DomainProjectionRebuildSemantics RebuildSemantics => DomainProjectionRebuildSemantics.FullReplay;

    /// <inheritdoc />
    public string RebuildTransportStoreName => TimeEntryEvidenceReadModelAddress.StateStoreName;

    /// <inheritdoc />
    public async Task<DomainProjectionHandlerResult> ProjectAsync(
        ProjectionRequest request,
        string dispatchId,
        CancellationToken cancellationToken)
    {
        Validate(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(dispatchId);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Events is [{ SequenceNumber: > 1 }])
            return await ProjectSuffixAsync(request, cancellationToken).ConfigureAwait(false);

        TimeEntryEvidenceReadModel? incoming;
        string? effectiveValuesDigest;
        try
        {
            incoming = Fold(request, out effectiveValuesDigest);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or JsonException)
        {
            return DomainProjectionHandlerResult.Failed(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }

        if (incoming is null)
        {
            return DomainProjectionHandlerResult.AlreadyCompleted();
        }

        try
        {
            _ = await ReadModelWritePolicy.UpdateAsync<JsonObject>(
                readModelStore,
                TimeEntryEvidenceReadModelAddress.StateStoreName,
                TimeEntryEvidenceReadModelAddress.StateKey(request.TenantId, incoming.TimeEntryId),
                current => TimeEntryEvidenceCompactStorage.Pack(GuardMerge(
                    current is null ? null : ValidateLegacySelected(current, GuardUnpack(current), request), incoming),
                    effectiveValuesDigest),
                new ReadModelWriteContext(TimeEntryEvidenceReadModelAddress.SlotName, ProjectionType)
                    .WithEventDiagnostics(request.Events),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return DomainProjectionHandlerResult.Completed();
        }
        catch (ProjectionFoldException)
        {
            return DomainProjectionHandlerResult.Failed(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return DomainProjectionHandlerResult.Retryable(ProjectionDispatchReasonCodes.DeliveryStateUnavailable);
        }
    }

    private async Task<DomainProjectionHandlerResult> ProjectSuffixAsync(
        ProjectionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            ProjectionEventDto item = request.Events[0];
            string key = TimeEntryEvidenceReadModelAddress.StateKey(request.TenantId,
                new TimeEntryId(request.AggregateId));
            _ = await ReadModelWritePolicy.UpdateAsync<JsonObject>(readModelStore,
                TimeEntryEvidenceReadModelAddress.StateStoreName, key,
                selected => AppendSelectedSuffix(selected, request, item),
                new ReadModelWriteContext(TimeEntryEvidenceReadModelAddress.SlotName, ProjectionType)
                    .WithEventDiagnostics(request.Events),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return DomainProjectionHandlerResult.Completed();
        }
        catch (ProjectionSuffixReplayRequiredException)
        {
            return DomainProjectionHandlerResult.Retryable(ProjectionDispatchReasonCodes.DeliveryRebuildRequired);
        }
        catch (ProjectionFoldException)
        {
            return DomainProjectionHandlerResult.Failed(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return DomainProjectionHandlerResult.Retryable(ProjectionDispatchReasonCodes.DeliveryStateUnavailable);
        }
    }

    private JsonObject AppendSelectedSuffix(JsonObject? selected, ProjectionRequest request,
        ProjectionEventDto item)
    {
        if (selected is null)
            throw new ProjectionSuffixReplayRequiredException();
        TimeEntryEvidenceReadModel current = GuardUnpack(selected);
        // A disabled selected row has already discarded comment text. Extending it
        // under an enabled policy would assign the new version to an incomplete fold.
        if (_trustedCommentPublicationAllowed && current.CommentPublicationPolicyVersion != 2)
            throw new ProjectionSuffixReplayRequiredException();
        if (!selected.ContainsKey("effectiveValuesDigest"))
            throw new ProjectionSuffixReplayRequiredException();
        if (selected["effectiveValuesDigest"] is not JsonValue digestNode
            || !digestNode.TryGetValue<string>(out string? priorValuesDigest)
            || priorValuesDigest is not { Length: 64 }
            || priorValuesDigest.Any(static character => !char.IsAsciiHexDigitUpper(character)))
            throw new ProjectionFoldException(new InvalidOperationException(
                "The selected Time Entry values digest is malformed."));
        if (current.TimeEntryId.Value != request.AggregateId)
            throw new ProjectionFoldException(new InvalidOperationException("The selected Time Entry owner differs."));
        int acceptedCount = current.EventPayloadLineage.Count;
        if (item.SequenceNumber > acceptedCount + 1)
            throw new ProjectionSuffixReplayRequiredException();
        if (item.SequenceNumber != acceptedCount + 1
            || !long.TryParse(current.ProjectionFreshness.Cursor,
                NumberStyles.None, CultureInfo.InvariantCulture, out long priorPosition)
            || item.GlobalPosition <= priorPosition)
            throw new ProjectionFoldException(new InvalidOperationException("The Time Entry suffix is not contiguous."));
        try
        {
            object? payload = Decode(item);
            string nextValuesDigest = priorValuesDigest;
            TimeEntryEvidenceReadModel next = current;
            var checkpoint = new TimesheetsProjectionCheckpoint(request.TenantId,
                ProjectionType, item.GlobalPosition, ProjectionFreshness.Fresh);
            if (payload is not null)
            {
                ValidateEvidence(payload);
                if (OwnerOf(payload).Value != request.AggregateId
                    || !TimeEntryEvidenceProjection.MatchesTenant(payload, request.TenantId))
                    throw new InvalidOperationException("The Time Entry suffix owner differs.");
                ValidateSuffixTransition(current, payload, priorValuesDigest);
                var projectionEvent = new TimeEntryProjectionEvent(
                    string.IsNullOrWhiteSpace(item.MessageId)
                        ? item.SequenceNumber.ToString(CultureInfo.InvariantCulture) : item.MessageId,
                    item.SequenceNumber, payload, request.TenantId);
                next = new TimeEntryEvidenceProjection().ProjectNext(request.TenantId,
                    current.TimeEntryId, current, projectionEvent, checkpoint)
                    ?? throw new InvalidOperationException("The Time Entry suffix has an invalid transition.");
                next = TimeEntryEvidenceCommentPolicy.Apply(next, _trustedCommentPublicationAllowed);
                nextValuesDigest = payload switch
                {
                    TimeEntryAdjustedThroughMagicLink adjusted => DigestValues(adjusted.AdjustedValues),
                    TimeEntryCorrected corrected => DigestValues(corrected.CorrectedValues with
                    {
                        ActivityTypeScope = corrected.CorrectedValues.ActivityTypeScope
                            ?? current.ActivityTypeScope
                    }),
                    TimeEntryApprovedCorrected corrected => DigestValues(corrected.CorrectedValues with
                    {
                        ActivityTypeScope = corrected.CorrectedValues.ActivityTypeScope
                            ?? current.ActivityTypeScope
                    }),
                    _ => priorValuesDigest
                };
            }
            else
            {
                next = current with
                {
                    ProjectionFreshness = ProjectionFreshnessMetadataMapper.ToMetadata(checkpoint)
                };
            }

            next = next with
            {
                EventPayloadLineage = [.. current.EventPayloadLineage,
                    Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(item)))]
            };
            return TimeEntryEvidenceCompactStorage.Pack(next, nextValuesDigest);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException
            or JsonException or NullReferenceException)
        {
            throw new ProjectionFoldException(exception);
        }
    }

    private static void ValidateSuffixTransition(TimeEntryEvidenceReadModel current,
        object payload, string priorValuesDigest)
    {
        bool correctedBefore = current.CorrectionState != TimeEntryCorrectionState.None;
        if (payload is TimeEntryRecorded
            || payload is TimeEntryContributorConfirmed confirmation
                && (current.ContributorCategory != ContributorCategory.ExternalContributor
                    || confirmation.Contributor != current.Contributor)
            || payload is TimeEntryAdjustedThroughMagicLink adjusted
                && (correctedBefore
                    || current.ContributorCategory != ContributorCategory.ExternalContributor
                    || adjusted.Contributor != current.Contributor
                    || DigestValues(adjusted.PreviousValues) != priorValuesDigest
                    || adjusted.AdjustedValues.Target != current.Target
                    || adjusted.AdjustedValues.Contributor != current.Contributor
                    || adjusted.AdjustedValues.ActivityTypeScope != adjusted.ActivityTypeScope)
            || payload is TimeEntryCorrected correction
                && (correctedBefore
                    || DigestValues(correction.PreviousValues with
                    {
                        ActivityTypeScope = correction.PreviousValues.ActivityTypeScope
                            ?? current.ActivityTypeScope
                    }) != priorValuesDigest
                    || correction.RejectionDecisionId != current.ApprovalDecision?.TimeEntryApprovalDecisionId
                    || correction.RejectionReason != current.ApprovalDecision?.Reason)
            || payload is TimeEntryApprovedCorrected approvedCorrection
                && (correctedBefore
                    || DigestValues(approvedCorrection.PreviousValues with
                    {
                        ActivityTypeScope = approvedCorrection.PreviousValues.ActivityTypeScope
                            ?? current.ActivityTypeScope
                    }) != priorValuesDigest
                    || approvedCorrection.SourceApprovalDecisionId
                        != current.ApprovalDecision?.TimeEntryApprovalDecisionId
                    || approvedCorrection.SourceApprovalScope != current.ApprovalDecision?.ApprovalScope))
            throw new InvalidOperationException("The Time Entry suffix contradicts its preceding state.");
    }

    private sealed class ProjectionSuffixReplayRequiredException : Exception;

    /// <inheritdoc />
    public async Task<DomainProjectionRebuildPlan> PrepareRebuildAsync(
        ProjectionRequest request,
        string operationId,
        CancellationToken cancellationToken)
    {
        Validate(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        cancellationToken.ThrowIfCancellationRequested();

        TimeEntryEvidenceReadModel? rebuilt;
        string? effectiveValuesDigest;
        try
        {
            rebuilt = Fold(request, out effectiveValuesDigest);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or JsonException)
        {
            throw new DomainProjectionRebuildRejectedException(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }
        if (request.RebuildSourceHeadSequence != request.Events.LongLength)
        {
            throw new DomainProjectionRebuildRejectedException(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }
        if (request.RebuildSourceErased != (request.Events.Length == 0))
        {
            throw new DomainProjectionRebuildRejectedException(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }

        string key = TimeEntryEvidenceReadModelAddress.StateKey(request.TenantId, new TimeEntryId(request.AggregateId));
        ReadModelEntry<JsonObject> current = await readModelStore
            .GetAsync<JsonObject>(
                TimeEntryEvidenceReadModelAddress.StateStoreName,
                key,
                cancellationToken).ConfigureAwait(false);
        if (current.Value is { } selected)
        {
            TimeEntryEvidenceReadModel existing;
            try
            {
                existing = ValidateLegacySelected(selected, GuardUnpack(selected), request);
                if (!request.RebuildSourceErased && request.Events.Length < existing.EventPayloadLineage.Count)
                {
                    throw new ProjectionFoldException(new InvalidOperationException(
                        "A shorter owner replay cannot replace selected Time Entry evidence."));
                }

                if (rebuilt is not null)
                {
                    _ = GuardMerge(existing, rebuilt);
                }
            }
            catch (ProjectionFoldException)
            {
                throw new DomainProjectionRebuildRejectedException(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }
        }
        ReadModelBatchConcurrency concurrency = current.ETag is { Length: > 0 }
            ? ReadModelBatchConcurrency.Match(current.ETag)
            : rebuilt is null
                ? ReadModelBatchConcurrency.IdempotentAbsent
                : ReadModelBatchConcurrency.CreateOnly;
        ReadModelBatchOperation operation = rebuilt is null
            ? ReadModelBatchOperation.Delete(
                key,
                concurrency)
            : ReadModelBatchOperation.Write(
                key,
                TimeEntryEvidenceCompactStorage.Pack(rebuilt, effectiveValuesDigest),
                concurrency);
        return new DomainProjectionRebuildPlan(TimeEntryEvidenceReadModelAddress.StateStoreName, [operation]);
    }

    /// <inheritdoc />
    public async Task<DomainProjectionRebuildPlan> PrepareStreamedRebuildAsync(
        ProjectionRequest sourceHeader, IAsyncEnumerable<ProjectionEventDto> events,
        string operationId, CancellationToken cancellationToken)
    {
        Validate(sourceHeader);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        string key = TimeEntryEvidenceReadModelAddress.StateKey(sourceHeader.TenantId,
            new TimeEntryId(sourceHeader.AggregateId));
        ReadModelEntry<JsonObject> selected = await readModelStore.GetAsync<JsonObject>(
            TimeEntryEvidenceReadModelAddress.StateStoreName, key, cancellationToken).ConfigureAwait(false);
        TimeEntryEvidenceReadModel? existing = null;
        bool legacy = false;
        try
        {
            if (selected.Value is { } stored)
            {
                existing = GuardUnpack(stored);
                legacy = !stored.ContainsKey("materializedDigest");
            }
            StreamedFoldResult folded = await FoldStreamedAsync(sourceHeader, events,
                legacy ? existing?.EventPayloadLineage.Count : null, cancellationToken).ConfigureAwait(false);
            TimeEntryEvidenceReadModel? rebuilt = folded.Model;
            if (legacy && existing is not null)
            {
                int prefixLength = existing.EventPayloadLineage.Count;
                if (prefixLength > folded.LegacyPayloadLineage.Count
                    || !existing.EventPayloadLineage.SequenceEqual(
                        folded.LegacyPayloadLineage.Take(prefixLength), StringComparer.Ordinal)
                    || folded.PrefixModel is null
                    || !JsonNode.DeepEquals(TimeEntryEvidenceCompactStorage.Pack(
                            TimeEntryEvidenceCommentPolicy.WithoutPublication(existing with
                            { EventPayloadLineage = folded.PrefixModel.EventPayloadLineage })),
                        TimeEntryEvidenceCompactStorage.Pack(
                            TimeEntryEvidenceCommentPolicy.WithoutPublication(folded.PrefixModel))))
                    throw new InvalidOperationException("Legacy Time Entry evidence differs from its owner prefix.");
                existing = folded.PrefixModel;
            }
            if (existing is not null)
            {
                if (!sourceHeader.RebuildSourceErased && folded.EventCount < existing.EventPayloadLineage.Count)
                    throw new InvalidOperationException("A shorter owner replay cannot replace selected evidence.");
                if (rebuilt is not null) _ = GuardMerge(existing, rebuilt);
            }

            ReadModelBatchConcurrency concurrency = selected.ETag is { Length: > 0 }
                ? ReadModelBatchConcurrency.Match(selected.ETag)
                : rebuilt is null ? ReadModelBatchConcurrency.IdempotentAbsent
                    : ReadModelBatchConcurrency.CreateOnly;
            ReadModelBatchOperation operation = rebuilt is null
                ? ReadModelBatchOperation.Delete(key, concurrency)
                : ReadModelBatchOperation.Write(key,
                    TimeEntryEvidenceCompactStorage.Pack(rebuilt, folded.EffectiveValuesDigest), concurrency);
            return new DomainProjectionRebuildPlan(TimeEntryEvidenceReadModelAddress.StateStoreName, [operation]);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException
            or JsonException or ProjectionFoldException)
        {
            throw new DomainProjectionRebuildRejectedException(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }
    }

    private sealed record StreamedFoldResult(TimeEntryEvidenceReadModel? Model,
        TimeEntryEvidenceReadModel? PrefixModel, List<string> LegacyPayloadLineage, int EventCount,
        string? EffectiveValuesDigest);

    private async Task<StreamedFoldResult> FoldStreamedAsync(ProjectionRequest request,
        IAsyncEnumerable<ProjectionEventDto> events, int? legacyPrefixLength,
        CancellationToken cancellationToken)
    {
        var evidence = new List<TimeEntryProjectionEvent>();
        var payloadLineage = new List<string>();
        var legacyPayloadLineage = new List<string>();
        var messageIds = new HashSet<string>(StringComparer.Ordinal);
        TimeEntryEvidenceReadModel? prefixModel = null;
        TimeEntryApprovalDecisionId? precedingRejectionDecision = null;
        TimeEntryRejectionReason? precedingRejectionReason = null;
        TimeEntryApprovalDecisionId? precedingApprovalDecision = null;
        TimeEntryApprovalScope? precedingApprovalScope = null;
        TimeEntryCorrectionValues? precedingValues = null;
        bool seenRecording = false;
        bool seenCorrection = false;
        int count = 0;
        long position = 0;

        TimeEntryEvidenceReadModel? Materialize(long cursor)
        {
            if (evidence.Count == 0) return null;
            TimeEntryEvidenceReadModel? model = new TimeEntryEvidenceProjection().Project(
                request.TenantId, new TimeEntryId(request.AggregateId), evidence,
                new TimesheetsProjectionCheckpoint(request.TenantId,
                    TimeEntryEvidenceProjection.ProjectionName, cursor, ProjectionFreshness.Fresh));
            if (model is null || model.EventLineage.Count != evidence.Count)
                throw new InvalidOperationException("Time Entry evidence contains an invalid transition.");
            return TimeEntryEvidenceCommentPolicy.Apply(model with
            {
                EventPayloadLineage = [.. payloadLineage]
            }, _trustedCommentPublicationAllowed);
        }

        await foreach (ProjectionEventDto item in events.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            count++;
            if (count > 10_000 || item.SequenceNumber != count || item.GlobalPosition <= position
                || string.IsNullOrWhiteSpace(item.EventTypeName)
                || !string.IsNullOrWhiteSpace(item.MessageId) && !messageIds.Add(item.MessageId))
                throw new InvalidOperationException("The Time Entry owner history is incomplete or ambiguous.");
            position = item.GlobalPosition;
            payloadLineage.Add(Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(item))));
            if (legacyPrefixLength is not null)
            {
                using IncrementalHash legacyHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                legacyHash.AppendData(Encoding.UTF8.GetBytes(item.EventTypeName + "\0"));
                legacyHash.AppendData(item.Payload);
                legacyPayloadLineage.Add(Convert.ToHexString(legacyHash.GetHashAndReset()));
            }

            object? payload = Decode(item);
            if (payload is not null)
            {
                ValidateEvidence(payload);
                TimeEntryId owner = OwnerOf(payload);
                if (!StringComparer.Ordinal.Equals(owner.Value, request.AggregateId)
                    || !TimeEntryEvidenceProjection.MatchesTenant(payload, request.TenantId))
                    throw new InvalidOperationException("Time Entry evidence does not match its owner.");
                if (payload is TimeEntryRecorded recorded)
                {
                    if (seenRecording) throw new InvalidOperationException("Time Entry evidence has a second recording.");
                    seenRecording = true;
                    precedingValues = new TimeEntryCorrectionValues(recorded.Target, recorded.Contributor,
                        recorded.ActivityTypeId, recorded.ServiceDate, recorded.DurationMinutes,
                        recorded.BillableState, recorded.ContributorCategory, recorded.AiMetrics)
                    {
                        ActivityTypeScope = recorded.ActivityTypeScope,
                        Comment = recorded.Comment
                    };
                }
                else if (!seenRecording)
                    throw new InvalidOperationException("Time Entry evidence precedes recording.");

                if (payload is TimeEntryContributorConfirmed confirmation
                    && (precedingValues?.ContributorCategory != ContributorCategory.ExternalContributor
                        || confirmation.Contributor != precedingValues?.Contributor)
                    || payload is TimeEntryAdjustedThroughMagicLink adjustment
                    && (seenCorrection
                        || precedingValues?.ContributorCategory != ContributorCategory.ExternalContributor
                        || adjustment.Contributor != precedingValues?.Contributor
                        || !Equals(adjustment.PreviousValues, precedingValues)
                        || adjustment.AdjustedValues.Target != precedingValues?.Target
                        || adjustment.AdjustedValues.Contributor != precedingValues.Contributor
                        || adjustment.AdjustedValues.ActivityTypeScope != adjustment.ActivityTypeScope)
                    || payload is TimeEntryCorrected correction
                    && (seenCorrection
                        || !MatchesPreviousCorrectionValues(correction.PreviousValues, precedingValues)
                        || correction.RejectionDecisionId != precedingRejectionDecision
                        || correction.RejectionReason != precedingRejectionReason)
                    || payload is TimeEntryApprovedCorrected approvedCorrection
                    && (seenCorrection
                        || !MatchesPreviousCorrectionValues(approvedCorrection.PreviousValues, precedingValues)
                        || approvedCorrection.SourceApprovalDecisionId != precedingApprovalDecision
                        || approvedCorrection.SourceApprovalScope != precedingApprovalScope))
                    throw new InvalidOperationException("Time Entry transition contradicts its preceding state.");

                precedingValues = payload switch
                {
                    TimeEntryAdjustedThroughMagicLink adjusted => adjusted.AdjustedValues,
                    TimeEntryCorrected corrected => corrected.CorrectedValues with
                    {
                        ActivityTypeScope = corrected.CorrectedValues.ActivityTypeScope ?? precedingValues?.ActivityTypeScope
                    },
                    TimeEntryApprovedCorrected corrected => corrected.CorrectedValues with
                    {
                        ActivityTypeScope = corrected.CorrectedValues.ActivityTypeScope ?? precedingValues?.ActivityTypeScope
                    },
                    _ => precedingValues
                };
                switch (payload)
                {
                    case TimeEntryRejected rejected:
                        precedingRejectionDecision = rejected.TimeEntryApprovalDecisionId;
                        precedingRejectionReason = rejected.Reason;
                        break;
                    case TimeEntryApproved approved:
                        precedingApprovalDecision = approved.TimeEntryApprovalDecisionId;
                        precedingApprovalScope = approved.ApprovalScope;
                        break;
                    case TimeEntryCorrected or TimeEntryApprovedCorrected:
                        seenCorrection = true;
                        break;
                }

                evidence.Add(new TimeEntryProjectionEvent(
                    string.IsNullOrWhiteSpace(item.MessageId)
                        ? item.SequenceNumber.ToString(CultureInfo.InvariantCulture) : item.MessageId,
                    item.SequenceNumber, payload, request.TenantId));
            }
            if (legacyPrefixLength == count) prefixModel = Materialize(position);
        }

        if (request.RebuildSourceHeadSequence != count || request.RebuildSourceErased != (count == 0))
            throw new InvalidOperationException("Time Entry replay does not match its authoritative source head.");
        return new StreamedFoldResult(Materialize(position), prefixModel, legacyPayloadLineage, count,
            precedingValues is null ? null : DigestValues(precedingValues));
    }

    private TimeEntryEvidenceReadModel? Fold(ProjectionRequest request,
        out string? effectiveValuesDigest)
    {
        effectiveValuesDigest = null;
        IReadOnlyList<ProjectionEventDto> normalized = ProjectionEventReader.Normalize(request.Events);
        if (normalized.Count > 10_000)
        {
            throw new InvalidOperationException("The Time Entry owner prefix exceeds the supported event limit.");
        }

        long priorGlobalPosition = 0;
        foreach (ProjectionEventDto item in normalized)
        {
            if (item.GlobalPosition <= priorGlobalPosition)
            {
                throw new InvalidOperationException("Time Entry source positions are not positive and increasing.");
            }

            priorGlobalPosition = item.GlobalPosition;
        }

        var evidence = new List<TimeEntryProjectionEvent>();
        List<string> payloadLineage = normalized.Select(static item => Convert.ToHexString(
            SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(item)))).ToList();
        var seenRecording = false;
        var seenCorrection = false;
        TimeEntryApprovalDecisionId? precedingRejectionDecision = null;
        TimeEntryRejectionReason? precedingRejectionReason = null;
        TimeEntryApprovalDecisionId? precedingApprovalDecision = null;
        TimeEntryApprovalScope? precedingApprovalScope = null;
        TimeEntryCorrectionValues? precedingValues = null;
        var entryId = new TimeEntryId(request.AggregateId);
        foreach (ProjectionEventDto item in normalized)
        {
            object? payload = Decode(item);
            if (payload is null)
            {
                continue;
            }

            ValidateEvidence(payload);
            TimeEntryId owner = OwnerOf(payload);
            if (!string.Equals(owner.Value, request.AggregateId, StringComparison.Ordinal)
                || !TimeEntryEvidenceProjection.MatchesTenant(payload, request.TenantId))
            {
                throw new InvalidOperationException("Time Entry evidence does not match its owner.");
            }

            if (payload is TimeEntryRecorded)
            {
                if (seenRecording)
                {
                    throw new InvalidOperationException("Time Entry evidence has a second recording.");
                }

                seenRecording = true;
                var recorded = (TimeEntryRecorded)payload;
                precedingValues = new TimeEntryCorrectionValues(
                    recorded.Target, recorded.Contributor, recorded.ActivityTypeId,
                    recorded.ServiceDate, recorded.DurationMinutes, recorded.BillableState,
                    recorded.ContributorCategory, recorded.AiMetrics)
                {
                    ActivityTypeScope = recorded.ActivityTypeScope,
                    Comment = recorded.Comment
                };
            }
            else if (!seenRecording)
            {
                throw new InvalidOperationException("Time Entry evidence precedes recording.");
            }

            if (payload is TimeEntryContributorConfirmed confirmation
                && (precedingValues?.ContributorCategory != ContributorCategory.ExternalContributor
                    || confirmation.Contributor != precedingValues?.Contributor)
                || payload is TimeEntryAdjustedThroughMagicLink adjustment
                && (seenCorrection
                    || precedingValues?.ContributorCategory != ContributorCategory.ExternalContributor
                    || adjustment.Contributor != precedingValues?.Contributor
                    || !Equals(adjustment.PreviousValues, precedingValues)
                    || adjustment.AdjustedValues.Target != precedingValues?.Target
                    || adjustment.AdjustedValues.Contributor != precedingValues.Contributor
                    || adjustment.AdjustedValues.ActivityTypeScope != adjustment.ActivityTypeScope)
                || payload is TimeEntryCorrected correction
                && (seenCorrection
                    || !MatchesPreviousCorrectionValues(correction.PreviousValues, precedingValues)
                    || correction.RejectionDecisionId != precedingRejectionDecision
                    || correction.RejectionReason != precedingRejectionReason)
                || payload is TimeEntryApprovedCorrected approvedCorrection
                && (seenCorrection
                    || !MatchesPreviousCorrectionValues(approvedCorrection.PreviousValues, precedingValues)
                    || approvedCorrection.SourceApprovalDecisionId != precedingApprovalDecision
                    || approvedCorrection.SourceApprovalScope != precedingApprovalScope))
            {
                throw new InvalidOperationException("Time Entry transition contradicts its preceding state.");
            }

            precedingValues = payload switch
            {
                TimeEntryAdjustedThroughMagicLink adjusted => adjusted.AdjustedValues,
                TimeEntryCorrected corrected => corrected.CorrectedValues with
                {
                    ActivityTypeScope = corrected.CorrectedValues.ActivityTypeScope ?? precedingValues?.ActivityTypeScope
                },
                TimeEntryApprovedCorrected corrected => corrected.CorrectedValues with
                {
                    ActivityTypeScope = corrected.CorrectedValues.ActivityTypeScope ?? precedingValues?.ActivityTypeScope
                },
                _ => precedingValues
            };

            switch (payload)
            {
                case TimeEntryRejected rejected:
                    precedingRejectionDecision = rejected.TimeEntryApprovalDecisionId;
                    precedingRejectionReason = rejected.Reason;
                    break;
                case TimeEntryApproved approved:
                    precedingApprovalDecision = approved.TimeEntryApprovalDecisionId;
                    precedingApprovalScope = approved.ApprovalScope;
                    break;
                case TimeEntryCorrected or TimeEntryApprovedCorrected:
                    seenCorrection = true;
                    break;
            }

            evidence.Add(new TimeEntryProjectionEvent(
                string.IsNullOrWhiteSpace(item.MessageId)
                    ? item.SequenceNumber.ToString(CultureInfo.InvariantCulture)
                    : item.MessageId,
                item.SequenceNumber,
                payload,
                request.TenantId));
        }

        if (evidence.Count == 0)
        {
            return null;
        }

        long cursor = normalized.Max(static item => item.GlobalPosition > 0 ? item.GlobalPosition : item.SequenceNumber);
        TimeEntryEvidenceReadModel? model = new TimeEntryEvidenceProjection().Project(
            request.TenantId,
            entryId,
            evidence,
            new TimesheetsProjectionCheckpoint(
                request.TenantId,
                TimeEntryEvidenceProjection.ProjectionName,
                cursor,
                ProjectionFreshness.Fresh));
        if (model is null || model.EventLineage.Count != evidence.Count)
        {
            throw new InvalidOperationException("Time Entry evidence contains an invalid transition.");
        }

        effectiveValuesDigest = precedingValues is null ? null : DigestValues(precedingValues);
        return TimeEntryEvidenceCommentPolicy.Apply(model with { EventPayloadLineage = payloadLineage },
            _trustedCommentPublicationAllowed);
    }

    private static bool MatchesPreviousCorrectionValues(
        TimeEntryCorrectionValues previous,
        TimeEntryCorrectionValues? effective)
        => effective is not null
            && Equals(previous with
            {
                ActivityTypeScope = previous.ActivityTypeScope ?? effective.ActivityTypeScope
            }, effective);

    private static string DigestValues(TimeEntryCorrectionValues values)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(values)));

    private static object? Decode(ProjectionEventDto item)
    {
        StoredTimeEntryRecorded? recorded = ProjectionEventReader.Deserialize<StoredTimeEntryRecorded>(item);
        if (recorded is not null) return recorded.Event ?? throw new InvalidOperationException("Recorded evidence is missing.");
        StoredTimeEntrySubmitted? submitted = ProjectionEventReader.Deserialize<StoredTimeEntrySubmitted>(item);
        if (submitted is not null) return submitted.Event ?? throw new InvalidOperationException("Submitted evidence is missing.");
        StoredTimeEntryConfirmed? confirmed = ProjectionEventReader.Deserialize<StoredTimeEntryConfirmed>(item);
        if (confirmed is not null) return confirmed.Event ?? throw new InvalidOperationException("Confirmed evidence is missing.");
        StoredTimeEntryAdjusted? adjusted = ProjectionEventReader.Deserialize<StoredTimeEntryAdjusted>(item);
        if (adjusted is not null) return adjusted.Event ?? throw new InvalidOperationException("Adjusted evidence is missing.");
        return (object?)ProjectionEventReader.Deserialize<TimeEntryRecorded>(item)
            ?? ProjectionEventReader.Deserialize<TimeEntrySubmitted>(item)
            ?? ProjectionEventReader.Deserialize<TimeEntryContributorConfirmed>(item)
            ?? ProjectionEventReader.Deserialize<TimeEntryAdjustedThroughMagicLink>(item)
            ?? ProjectionEventReader.Deserialize<TimeEntryApproved>(item)
            ?? ProjectionEventReader.Deserialize<TimeEntryRejected>(item)
            ?? (object?)ProjectionEventReader.Deserialize<TimeEntryCorrected>(item)
            ?? ProjectionEventReader.Deserialize<TimeEntryApprovedCorrected>(item);
    }

    private static TimeEntryId OwnerOf(object payload) => payload switch
    {
        TimeEntryRecorded item => item.TimeEntryId,
        TimeEntrySubmitted item => item.TimeEntryId,
        TimeEntryContributorConfirmed item => item.TimeEntryId,
        TimeEntryAdjustedThroughMagicLink item => item.TimeEntryId,
        TimeEntryApproved item => item.TimeEntryId,
        TimeEntryRejected item => item.TimeEntryId,
        TimeEntryCorrected item => item.TimeEntryId,
        TimeEntryApprovedCorrected item => item.TimeEntryId,
        _ => throw new InvalidOperationException("Unrecognized Time Entry evidence.")
    };

    private static void ValidateEvidence(object payload)
    {
        bool valid = payload switch
        {
            TimeEntryRecorded recorded => recorded.TimeEntryId is not null
                && !string.IsNullOrWhiteSpace(recorded.TimeEntryId.Value)
                && recorded.Target is not null
                && !string.IsNullOrWhiteSpace(recorded.Target.TargetId)
                && recorded.Target.TargetKind is TimeEntryTargetKind.Project or TimeEntryTargetKind.Work
                && !string.IsNullOrWhiteSpace(recorded.Contributor?.PartyId)
                && !string.IsNullOrWhiteSpace(recorded.ActivityTypeId?.Value)
                && recorded.ActivityTypeScope is ActivityTypeScope.Tenant or ActivityTypeScope.Project
                && recorded.ServiceDate != default
                && recorded.DurationMinutes > 0
                && recorded.BillableState is BillableState.Billable or BillableState.NonBillable
                && recorded.ApprovalState == TimeEntryApprovalState.Draft
                && ValidContributorCategory(recorded.ContributorCategory)
                && ValidAiMetrics(recorded.AiMetrics, recorded.ContributorCategory)
                && ValidComment(recorded.Comment),
            TimeEntrySubmitted submitted => ValidOwner(submitted.TimeEntryId)
                && ValidParty(submitted.Submitter) && ValidTenant(submitted.Tenant)
                && submitted.SubmittedAtUtc != default
                && ValidId(submitted.TimeEntrySubmissionId?.Value)
                && submitted.SubmissionScope is TimeEntrySubmissionScope.SelectedEntries or TimeEntrySubmissionScope.TimesheetPeriod
                && submitted.ApprovalState == TimeEntryApprovalState.Submitted,
            TimeEntryContributorConfirmed confirmed => ValidOwner(confirmed.TimeEntryId)
                && ValidParty(confirmed.Contributor) && ValidTenant(confirmed.Tenant)
                && confirmed.ConfirmedAtUtc != default && ValidSource(confirmed.Source),
            TimeEntryAdjustedThroughMagicLink adjusted => ValidOwner(adjusted.TimeEntryId)
                && ValidParty(adjusted.Contributor) && ValidTenant(adjusted.Tenant)
                && adjusted.AdjustedAtUtc != default
                && adjusted.ActivityTypeScope is ActivityTypeScope.Tenant or ActivityTypeScope.Project
                && ValidValues(adjusted.PreviousValues) && ValidValues(adjusted.AdjustedValues)
                && ValidSource(adjusted.Source),
            TimeEntryApproved approved => ValidOwner(approved.TimeEntryId)
                && ValidParty(approved.Approver) && ValidTenant(approved.Tenant)
                && approved.DecidedAtUtc != default
                && ValidId(approved.TimeEntryApprovalDecisionId?.Value)
                && approved.ApprovalScope is TimeEntryApprovalScope.IndividualEntry or TimeEntryApprovalScope.TimesheetPeriod
                && ValidAuthority(approved.AuthoritySource)
                && ValidApprovalAction(approved.AuthoritySource, approved.ApprovalScope, approved.ApprovalState)
                && approved.ApprovalState == TimeEntryApprovalState.Approved,
            TimeEntryRejected rejected => ValidOwner(rejected.TimeEntryId)
                && ValidParty(rejected.Approver) && ValidTenant(rejected.Tenant)
                && rejected.DecidedAtUtc != default
                && ValidId(rejected.TimeEntryApprovalDecisionId?.Value)
                && rejected.ApprovalScope is TimeEntryApprovalScope.IndividualEntry or TimeEntryApprovalScope.TimesheetPeriod
                && ValidAuthority(rejected.AuthoritySource)
                && ValidApprovalAction(rejected.AuthoritySource, rejected.ApprovalScope, rejected.ApprovalState)
                && ValidId(rejected.Reason?.Value)
                && rejected.ApprovalState == TimeEntryApprovalState.Rejected,
            TimeEntryCorrected corrected => ValidOwner(corrected.TimeEntryId)
                && ValidId(corrected.TimeEntryCorrectionId?.Value)
                && ValidTenant(corrected.Tenant) && ValidParty(corrected.CorrectedBy)
                && corrected.CorrectedAtUtc != default
                && ValidValues(corrected.PreviousValues) && ValidValues(corrected.CorrectedValues)
                && ValidId(corrected.RejectionReason?.Value)
                && ValidId(corrected.RejectionDecisionId?.Value)
                && corrected.ApprovalState == TimeEntryApprovalState.Draft
                && corrected.CorrectionState == TimeEntryCorrectionState.Corrected,
            TimeEntryApprovedCorrected corrected => ValidOwner(corrected.TimeEntryId)
                && ValidId(corrected.TimeEntryCorrectionId?.Value)
                && ValidTenant(corrected.Tenant) && ValidParty(corrected.CorrectedBy)
                && corrected.CorrectedAtUtc != default
                && ValidValues(corrected.PreviousValues) && ValidValues(corrected.CorrectedValues)
                && ValidId(corrected.Reason?.Value)
                && ValidId(corrected.SourceApprovalDecisionId?.Value)
                && corrected.SourceApprovalScope is TimeEntryApprovalScope.IndividualEntry or TimeEntryApprovalScope.TimesheetPeriod
                && corrected.ApprovalState == TimeEntryApprovalState.Approved
                && corrected.CorrectionState == TimeEntryCorrectionState.Corrected,
            _ => true
        };
        if (!valid)
        {
            throw new InvalidOperationException("Time Entry evidence contains incomplete or contradictory state.");
        }
    }

    private static bool ValidId(string? value) => !string.IsNullOrWhiteSpace(value);

    private static bool ValidOwner(TimeEntryId? value) => ValidId(value?.Value);

    private static bool ValidTenant(TenantReference? value) => ValidId(value?.TenantId);

    private static bool ValidParty(PartyReference? value) => ValidId(value?.PartyId);

    private static bool ValidSource(ExternalContributionSource? value)
        => ValidId(value?.SourceSystem) && ValidId(value?.ExternalRequestId);

    private static bool ValidValues(TimeEntryCorrectionValues? value)
        => value is not null
            && value.Target is not null && ValidId(value.Target.TargetId)
            && value.Target.TargetKind is TimeEntryTargetKind.Project or TimeEntryTargetKind.Work
            && ValidParty(value.Contributor) && ValidId(value.ActivityTypeId?.Value)
            && value.ServiceDate != default && value.DurationMinutes > 0
            && value.BillableState is BillableState.Billable or BillableState.NonBillable
            && ValidContributorCategory(value.ContributorCategory)
            && (value.ActivityTypeScope is null or ActivityTypeScope.Tenant or ActivityTypeScope.Project)
            && ValidAiMetrics(value.AiMetrics, value.ContributorCategory)
            && ValidComment(value.Comment);

    private static bool ValidContributorCategory(ContributorCategory category)
        => category is ContributorCategory.Employee or ContributorCategory.ExternalContributor
            or ContributorCategory.AutomatedAgent;

    private static bool ValidApprovalAction(ApprovalAuthoritySourceAttribution authority,
        TimeEntryApprovalScope scope, TimeEntryApprovalState decision)
        => authority.Action == ((scope, decision) switch
        {
            (TimeEntryApprovalScope.IndividualEntry, TimeEntryApprovalState.Approved) => ApprovalAuthorityAction.EntryApproval,
            (TimeEntryApprovalScope.IndividualEntry, TimeEntryApprovalState.Rejected) => ApprovalAuthorityAction.EntryRejection,
            (TimeEntryApprovalScope.TimesheetPeriod, TimeEntryApprovalState.Approved) => ApprovalAuthorityAction.PeriodApproval,
            (TimeEntryApprovalScope.TimesheetPeriod, TimeEntryApprovalState.Rejected) => ApprovalAuthorityAction.PeriodRejection,
            _ => ApprovalAuthorityAction.Unknown
        });

    private static bool ValidAiMetrics(AiEffortMetrics? metrics, ContributorCategory category)
    {
        if (metrics is null) return true;
        if (metrics.Availability is not (AiMetricAvailability.Unavailable or AiMetricAvailability.ProviderReported or AiMetricAvailability.Estimated)
            || metrics.TokenAvailability is not (AiTokenMetricAvailability.NotReported or AiTokenMetricAvailability.Unavailable or AiTokenMetricAvailability.ProviderReported)
            || metrics.Source is null
            || metrics.Source.SourceCategory is not (AiEffortMetricSourceCategory.Unavailable or AiEffortMetricSourceCategory.Provider
                or AiEffortMetricSourceCategory.Tool or AiEffortMetricSourceCategory.WorkExecution)
            || metrics.WallClockDurationMilliseconds < 0 || metrics.ModelRuntimeMilliseconds < 0
            || metrics.BillableEffortMinutes < 0 || metrics.ProviderInputTokenCount < 0
            || metrics.ProviderOutputTokenCount < 0 || metrics.ProviderTotalTokenCount < 0)
            return false;

        AiEffortMetricSourceMetadata source = metrics.Source;
        if (source.SourceCategory == AiEffortMetricSourceCategory.Provider && !ValidId(source.ProviderName)
            || source.SourceCategory == AiEffortMetricSourceCategory.Tool && !ValidId(source.ToolName)
            || source.SourceCategory == AiEffortMetricSourceCategory.WorkExecution && !ValidId(source.WorkExecutionId)
            || metrics.Availability is AiMetricAvailability.ProviderReported or AiMetricAvailability.Estimated
                && source.SourceCategory == AiEffortMetricSourceCategory.Unavailable)
            return false;

        bool anyToken = metrics.ProviderInputTokenCount is not null
            || metrics.ProviderOutputTokenCount is not null || metrics.ProviderTotalTokenCount is not null;
        if (metrics.TokenAvailability is AiTokenMetricAvailability.NotReported or AiTokenMetricAvailability.Unavailable
                && anyToken
            || metrics.TokenAvailability == AiTokenMetricAvailability.ProviderReported
                && (metrics.ProviderInputTokenCount is null || metrics.ProviderOutputTokenCount is null
                    || metrics.ProviderTotalTokenCount is null)
            || metrics.Availability == AiMetricAvailability.Unavailable
                && (anyToken || metrics.WallClockDurationMilliseconds is not null
                    || metrics.ModelRuntimeMilliseconds is not null || metrics.BillableEffortMinutes is not null)
            || category != ContributorCategory.AutomatedAgent
                && (metrics.Availability is AiMetricAvailability.ProviderReported or AiMetricAvailability.Estimated
                    || metrics.TokenAvailability == AiTokenMetricAvailability.ProviderReported))
            return false;
        return true;
    }

    private static bool ValidComment(TimeEntryComment? comment)
        => comment is null || !string.IsNullOrWhiteSpace(comment.Text)
            && comment.Text.Length <= TimeEntryComment.MaxLength
            && comment.Policy is { } policy
            && Enum.IsDefined(policy.InternalDisplay) && policy.InternalDisplay != TimesheetsCommentPolicyDecision.Unknown
            && Enum.IsDefined(policy.ExternalConfirmationDisplay) && policy.ExternalConfirmationDisplay != TimesheetsCommentPolicyDecision.Unknown
            && Enum.IsDefined(policy.ProjectionInclusion) && policy.ProjectionInclusion != TimesheetsCommentPolicyDecision.Unknown
            && policy.ServerPublicationVersion is 0 or TimeEntryCommentPublicationIssuer.CurrentVersion
            && Enum.IsDefined(policy.ServerProjectionInclusion)
            && Enum.IsDefined(policy.ServerExternalConfirmationDisplay)
            && (policy.ServerExternalConfirmationDisplay == TimesheetsCommentPolicyDecision.PolicyRequired
                || policy.ServerExternalConfirmationDisplay == policy.ExternalConfirmationDisplay)
            && (policy.ServerPublicationVersion == 0
                || policy.ServerProjectionInclusion is TimesheetsCommentPolicyDecision.Allowed
                    or TimesheetsCommentPolicyDecision.PolicyRequired)
            && (policy.ServerPublicationVersion == 0
                || policy.ServerProjectionInclusion == policy.ProjectionInclusion)
            && Enum.IsDefined(policy.ExportInclusion) && policy.ExportInclusion != TimesheetsCommentPolicyDecision.Unknown
            && Enum.IsDefined(policy.SupportDiagnostics) && policy.SupportDiagnostics != TimesheetsCommentPolicyDecision.Unknown
            && Enum.IsDefined(policy.RedactionRequirement) && policy.RedactionRequirement != TimesheetsCommentRedactionRequirement.Unknown
            && Enum.IsDefined(policy.RetentionCategory) && policy.RetentionCategory != TimesheetsEvidenceRetentionCategory.Unknown;

    private static bool ValidAuthority(ApprovalAuthoritySourceAttribution? value)
        => value is not null
            && Enum.IsDefined(value.Action) && value.Action != ApprovalAuthorityAction.Unknown
            && Enum.IsDefined(value.Source) && value.Source != ApprovalAuthoritySource.Unknown
            && Enum.IsDefined(value.DecisionState) && value.DecisionState == ApprovalAuthorityDecisionState.Allowed
            && ValidId(value.PolicyKey) && ValidId(value.PolicyVersion)
            && value.Freshness is { State: ProjectionFreshnessState.Fresh };

    private static TimeEntryEvidenceReadModel GuardMerge(
        TimeEntryEvidenceReadModel? current,
        TimeEntryEvidenceReadModel incoming)
    {
        try
        {
            if (current is null) return incoming;
            if (current.TimeEntryId != incoming.TimeEntryId
                || current.EventPayloadLineage is null
                || current.EventPayloadLineage.Count < current.EventLineage.Count)
            {
                throw new InvalidOperationException("Stored Time Entry lineage is invalid.");
            }

            int common = Math.Min(current.EventPayloadLineage.Count, incoming.EventPayloadLineage.Count);
            if (!current.EventPayloadLineage.Take(common).SequenceEqual(incoming.EventPayloadLineage.Take(common), StringComparer.Ordinal)
                || !current.EventLineage.Where(item => item.Ordinal <= common)
                    .SequenceEqual(incoming.EventLineage.Where(item => item.Ordinal <= common)))
            {
                throw new InvalidOperationException("Time Entry history does not extend stored evidence.");
            }

            if (current.EventPayloadLineage.Count > incoming.EventPayloadLineage.Count)
            {
                // An older prefix cannot prove the selected row's later decision, lock,
                // or state fields. Keep it selected, but fail this delivery closed.
                throw new InvalidOperationException("An older Time Entry prefix cannot validate selected evidence.");
            }

            bool publicationMigration = current.CommentPublicationPolicyVersion
                != incoming.CommentPublicationPolicyVersion;
            TimeEntryEvidenceReadModel comparedCurrent = publicationMigration
                ? TimeEntryEvidenceCommentPolicy.WithoutPublication(current) : current;
            TimeEntryEvidenceReadModel comparedIncoming = publicationMigration
                ? TimeEntryEvidenceCommentPolicy.WithoutPublication(incoming) : incoming;
            if (current.EventPayloadLineage.Count == incoming.EventPayloadLineage.Count
                && !JsonNode.DeepEquals(
                    TimeEntryEvidenceCompactStorage.Pack(comparedCurrent),
                    TimeEntryEvidenceCompactStorage.Pack(comparedIncoming)))
            {
                throw new InvalidOperationException("Stored Time Entry evidence differs from its canonical owner fold.");
            }

            return incoming.EventPayloadLineage.Count > current.EventPayloadLineage.Count
                || publicationMigration ? incoming : current;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or NullReferenceException)
        {
            throw new ProjectionFoldException(exception);
        }
    }

    private TimeEntryEvidenceReadModel GuardUnpack(JsonObject current)
    {
        try
        {
            return TimeEntryEvidenceCommentPolicy.Apply(TimeEntryEvidenceCompactStorage.Unpack(current),
                _trustedCommentPublicationAllowed, preserveStoredVersion: true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or JsonException)
        {
            throw new ProjectionFoldException(exception);
        }
    }

    private TimeEntryEvidenceReadModel ValidateLegacySelected(
        JsonObject selected, TimeEntryEvidenceReadModel current, ProjectionRequest request)
    {
        if (selected.ContainsKey("materializedDigest")
            || request.RebuildSourceErased && request.Events.Length == 0)
        {
            return current;
        }

        try
        {
            if (current.EventPayloadLineage.Count > request.Events.Length)
            {
                throw new InvalidOperationException("Legacy Time Entry evidence has no complete owner prefix.");
            }

            ProjectionEventDto[] prefix = request.Events.Take(current.EventPayloadLineage.Count).ToArray();
            TimeEntryEvidenceCompactStorage.ValidateLegacyPayloadLineage(current,
                ProjectionEventReader.Normalize(prefix));
            TimeEntryEvidenceReadModel canonical = Fold(request with
            {
                Events = prefix
            }, out _) ?? throw new InvalidOperationException("Legacy Time Entry evidence has no canonical owner fold.");
            if (!JsonNode.DeepEquals(TimeEntryEvidenceCompactStorage.Pack(
                    TimeEntryEvidenceCommentPolicy.WithoutPublication(current with
                    { EventPayloadLineage = canonical.EventPayloadLineage })),
                TimeEntryEvidenceCompactStorage.Pack(
                    TimeEntryEvidenceCommentPolicy.WithoutPublication(canonical))))
            {
                throw new InvalidOperationException("Legacy Time Entry evidence differs from its canonical owner fold.");
            }

            return canonical;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or JsonException)
        {
            throw new ProjectionFoldException(exception);
        }
    }

    private static void Validate(ProjectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(request.Domain, TimesheetsEventStoreIntegration.DomainName, StringComparison.Ordinal))
        {
            throw new ArgumentException("The projection request targets another domain.", nameof(request));
        }

        _ = new AggregateIdentity(request.TenantId, request.Domain, request.AggregateId);
    }
}
