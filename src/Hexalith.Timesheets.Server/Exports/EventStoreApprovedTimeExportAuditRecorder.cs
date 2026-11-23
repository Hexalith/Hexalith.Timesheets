using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Timesheets.Contracts.Events.Exports;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.Runtime;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Timesheets.Server.Exports;

/// <summary>Records and verifies a single immutable receipt on the authorized request's EventStore stream.</summary>
public sealed class EventStoreApprovedTimeExportAuditRecorder(IServiceScopeFactory scopeFactory)
    : IApprovedTimeExportAuditRecorder
{
    private static readonly JsonSerializerOptions _receiptJson = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
        AllowDuplicateProperties = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    /// <inheritdoc/>
    public async ValueTask<TimesheetsDomainResult> RecordAcceptedExportAsync(
        ApprovedTimeExported evidence, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        cancellationToken.ThrowIfCancellationRequested();
        if (!ApprovedTimeExportReceipt.TryGetOwnerTenant(evidence, out string tenant))
        {
            return TimesheetsDomainResult.NoOp();
        }

        string owner = ApprovedTimeExportReceipt.Owner(tenant, evidence);
        StreamReadRequest request = new(tenant, TimesheetsEventStoreIntegration.DomainName, owner, FromSequence: 0, PageSize: 2);
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            var submission = scope.ServiceProvider.GetRequiredService<MagicLinkDurableSubmissionService>();
            // Recover from the owner stream even if the transient command status has expired.
            ApprovedTimeExported? stored = await ReadAsync(submission, request, cancellationToken).ConfigureAwait(false);
            if (stored is null)
            {
                if (!ApprovedTimeExportReceipt.IsValid(evidence, out _))
                {
                    return TimesheetsDomainResult.NoOp();
                }

                // A denied attempt can leave a terminal zero-event status. A later authorized
                // attempt has fresh transport lineage; the tenant/request owner still permits one event.
                string attemptId = UniqueIdHelper.GenerateSortableUniqueStringId();
                string messageId = ApprovedTimeExportReceipt.MessageId(evidence, attemptId);
                var status = await submission.SubmitAndReadStatusAsync(messageId, tenant, owner,
                    new CommitApprovedTimeExport(evidence, attemptId), typeof(CommitApprovedTimeExport), cancellationToken).ConfigureAwait(false);
                if (!MagicLinkDurableSubmissionService.HasCommittedStatus(status, messageId, tenant, owner, 1)
                    || status!.CommittedEventSequence != 1)
                {
                    // A simultaneous attempt can win on the same owner. An uncertain or rejected
                    // submission releases nothing; a subsequent call can recover the winning receipt.
                    return TimesheetsDomainResult.NoOp();
                }

                stored = await ReadAsync(submission, request, cancellationToken).ConfigureAwait(false);
                if (stored != evidence)
                {
                    return TimesheetsDomainResult.NoOp();
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            return stored is not null && stored == evidence with
                {
                    RequestedAtUtc = stored.RequestedAtUtc, GeneratedAtUtc = stored.GeneratedAtUtc,
                    CorrelationId = evidence.RequestId is null ? evidence.CorrelationId : stored.CorrelationId
                }
                ? TimesheetsDomainResult.Success([stored]) : TimesheetsDomainResult.NoOp();
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return TimesheetsDomainResult.NoOp();
        }
    }

    private static async Task<ApprovedTimeExported?> ReadAsync(
        MagicLinkDurableSubmissionService submission, StreamReadRequest request, CancellationToken cancellationToken)
    {
        StreamReadPage page;
        try
        {
            page = await submission.ReadStoredEventsAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (EventStoreGatewayException exception) when (
            exception.StatusCode == 404 && exception.ReasonCode == StreamReplayReasonCodes.MissingStream)
        {
            return null;
        }

        if (page.Tenant != request.Tenant || page.AggregateId != request.AggregateId || page.Domain != request.Domain
            || page.Metadata is not { FromSequence: 0, ToSequence: null, IsTruncated: false, NextContinuationToken: null })
        {
            throw new InvalidDataException("The export receipt stream could not be verified.");
        }

        if (page is { Events: [], Metadata: { LastSequenceReturned: null, LatestSequence: 0, EventCount: 0 } })
        {
            return null;
        }

        if (page.Metadata is not { LastSequenceReturned: 1, LatestSequence: 1, EventCount: 1 }
            || page.Events is not [{ SequenceNumber: 1, MetadataVersion: 1 } item]
            || item.EventTypeName != typeof(StoredApprovedTimeExported).FullName
            || !string.Equals(item.SerializationFormat, "json", StringComparison.OrdinalIgnoreCase)
            || !MessageIdGrammar.IsValid(item.MessageId, version: 2) || item.MessageId == item.CausationId
            || item.PayloadVersion is not (null or 1))
        {
            throw new InvalidDataException("The export receipt stream could not be verified.");
        }

        var stored = JsonSerializer.Deserialize<StoredApprovedTimeExported>(item.Payload, _receiptJson);
        if (stored?.Event is not { } receipt || !MessageIdGrammar.IsValid(stored.AttemptId, 2) || !ApprovedTimeExportReceipt.IsValid(receipt, out string tenant)
            || tenant != request.Tenant || ApprovedTimeExportReceipt.Owner(tenant, receipt) != request.AggregateId
            || receipt.Requester!.PartyId != item.UserId || item.CorrelationId != item.CausationId
            || item.CausationId != ApprovedTimeExportReceipt.MessageId(receipt, stored.AttemptId))
        {
            throw new InvalidDataException("The persisted export receipt identity could not be verified.");
        }

        return receipt;
    }
}
