using System.Collections.Concurrent;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Timesheets.Server.MagicLinks;

using NSubstitute;

namespace Hexalith.Timesheets.IntegrationTests;

/// <summary>Bridges the workload transport to a real EventStore actor over retained test storage.</summary>
internal sealed class FinanceReceiptActorGateway(ConcurrentDictionary<(string Tenant, string Owner), InMemoryStateManager> storage, string requester) : IDisposable
{
    private readonly InMemoryCommandStatusStore _statuses = new();
    private readonly SemaphoreSlim _actorTurn = new(1);
    private readonly IDomainServiceInvoker _invoker = Substitute.For<IDomainServiceInvoker>();

    internal IEventStoreGatewayClient Client { get; } = Substitute.For<IEventStoreGatewayClient>();

    internal Func<StreamReadPage, StreamReadPage> Readback { get; set; } = page => page;

    internal Func<CommandEnvelope, CommandEnvelope> BeforeProcess { get; set; } = envelope => envelope;

    internal Func<Task>? OnMissingStream { get; init; }

    internal bool LoseAcknowledgement { get; init; }

    internal CommandEnvelope? Submitted { get; private set; }

    internal void Connect(MagicLinkEventStoreDomainProcessor processor)
    {
        _invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(call => processor.ProcessAsync(call.ArgAt<CommandEnvelope>(0), call.ArgAt<object?>(1),
                call.ArgAt<CancellationToken>(2)));
        Client.SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => SubmitAsync(call.Arg<SubmitCommandRequest>(), call.Arg<CancellationToken>()));
        Client.GetWorkloadCommandStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => StatusAsync(call.ArgAt<string>(0), call.ArgAt<string>(1), call.Arg<CancellationToken>()));
        Client.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => ReadAsync(call.Arg<StreamReadRequest>()));
    }

    internal AggregateActor Actor(string owner, string tenant = "tenant-1")
        => MagicLinkConfirmationHttpBoundaryTests.CreateMagicLinkActor(new(owner),
            storage.GetOrAdd((tenant, owner), static _ => new()), _statuses, _invoker, tenant);

    public void Dispose() => _actorTurn.Dispose();

    private async Task<SubmitCommandResponse> SubmitAsync(SubmitCommandRequest request, CancellationToken token)
    {
        var envelope = new CommandEnvelope(request.MessageId, request.Tenant, request.Domain, request.AggregateId,
            request.CommandType, JsonSerializer.SerializeToUtf8Bytes(request.Payload), request.CorrelationId!,
            request.MessageId, requester, new Dictionary<string, string>
            {
                [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "timesheets",
                [EventStoreGatewayVerifiedOrigin.ActorExtensionKey] = requester
            });
        await _actorTurn.WaitAsync(token);
        try
        {
            Submitted = envelope;
            await Actor(request.AggregateId, request.Tenant).ProcessCommandAsync(BeforeProcess(envelope), token);
        }
        finally
        {
            _actorTurn.Release();
        }

        return LoseAcknowledgement ? throw new HttpRequestException("Lost acknowledgement after commit.")
            : new(request.CorrelationId!, MessageId: request.MessageId);
    }

    private async Task<CommandStatusQueryResponse?> StatusAsync(string tenant, string messageId, CancellationToken token)
    {
        CommandStatusRecord? status = await _statuses.ReadStatusAsync(tenant, messageId, token);
        return status is null ? null : new(status.CorrelationId!, status.Status.ToString(), (int)status.Status,
            status.RejectionEventType, status.MessageId)
        {
            TenantId = tenant, Domain = status.Domain, AggregateId = status.AggregateId, EventCount = status.EventCount,
            CommittedEventSequence = status.CommittedEventSequence, FailureReason = status.FailureReason, Retryable = status.Retryable
        };
    }

    private async Task<StreamReadPage> ReadAsync(StreamReadRequest request)
    {
        EventEnvelope[] events = await Actor(request.AggregateId!, request.Tenant).GetEventsAsync(request.FromSequence);
        if (events.Length == 0)
        {
            if (OnMissingStream is not null) await OnMissingStream();
            throw new EventStoreGatewayException(404, "Not Found", reasonCode: StreamReplayReasonCodes.MissingStream);
        }
        if (LoseAcknowledgement)
        {
            throw new HttpRequestException("Owner readback unavailable after commit.");
        }

        var stored = events[0];
        return Readback(new(stored.TenantId, stored.Domain, stored.AggregateId, events.Select(item =>
            new StreamReadEvent(item.SequenceNumber, item.EventTypeName, item.Payload, item.SerializationFormat,
                item.MetadataVersion, item.MessageId, item.CorrelationId, item.CausationId, item.Timestamp, item.UserId)).ToArray(),
            new(request.FromSequence, request.ToSequence, events[^1].SequenceNumber, events[^1].SequenceNumber, events.Length, false, null)));
    }
}
