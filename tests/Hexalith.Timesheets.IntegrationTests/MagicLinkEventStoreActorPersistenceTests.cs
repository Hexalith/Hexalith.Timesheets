using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks.Commands;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed partial class MagicLinkConfirmationHttpBoundaryTests
{
    [Fact]
    public async Task Valid_use_requires_verified_origin_at_the_processor_entry()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId("capability-origin-use");
        var entryId = new TimeEntryId("entry-origin-use");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm, capabilityId, entryId);
        using IServiceScope scope = factory.Services.CreateScope();
        MagicLinkEventStoreDomainProcessor processor = scope.ServiceProvider.GetRequiredService<MagicLinkEventStoreDomainProcessor>();
        var intent = new CommitMagicLinkUse(capabilityId, Tenant(), entryId,
            new MagicLinkTokenHash(Hash(ValidConfirmToken())), MagicLinkUseAction.Confirm, null, ObservedAtUtc);
        CommandEnvelope valid = ActorCommand(typeof(CommitMagicLinkUse).FullName!, "origin-use", entryId,
            JsonSerializer.SerializeToUtf8Bytes(intent, JsonOptions));
        object state = RecordedExternalState(timeEntryId: entryId);

        (await processor.ProcessAsync(valid with { Extensions = null }, state, TestContext.Current.CancellationToken))
            .IsRejection.ShouldBeTrue();
        (await processor.ProcessAsync(valid, state, TestContext.Current.CancellationToken))
            .Events.OfType<StoredMagicLinkUsed>().ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Valid_management_command_requires_verified_origin_and_actor_at_processor_entry(bool revoke)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, useMismatchedClaims: false);
        using HttpClient client = factory.CreateClient();
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            new MagicLinkCapabilityId("capability-guard-seed"), new TimeEntryId("entry-guard-seed"));
        var capabilityId = new MagicLinkCapabilityId(revoke ? "capability-guard-revoke" : "capability-guard-issue");
        var entryId = new TimeEntryId(revoke ? "entry-guard-revoke" : "entry-guard-issue");
        if (revoke)
        {
            await factory.ProjectValidStateAsync(client, "guard-revoke-token", MagicLinkAllowedAction.Confirm,
                capabilityId, entryId);
        }
        using IServiceScope scope = factory.Services.CreateScope();
        MagicLinkEventStoreDomainProcessor processor = scope.ServiceProvider.GetRequiredService<MagicLinkEventStoreDomainProcessor>();
        CommandEnvelope valid = revoke
            ? ActorCommand(typeof(CommitMagicLinkTransition).FullName!, "guard-revoke", entryId,
                JsonSerializer.SerializeToUtf8Bytes(new CommitMagicLinkTransition(
                    capabilityId, Tenant(), entryId, Operator(), MagicLinkTransitionAction.Revoke,
                    new MagicLinkAuditMetadata("timesheets", "guard-revoke"), ObservedAtUtc), JsonOptions))
            : CreateIssueEnvelope(CreateIssueIntent(capabilityId, entryId), "guard-issue");
        valid = valid with
        {
            UserId = Operator().PartyId,
            Extensions = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "timesheets",
                [EventStoreGatewayVerifiedOrigin.ActorExtensionKey] = Operator().PartyId
            }
        };

        (await processor.ProcessAsync(valid with { Extensions = null }, null, TestContext.Current.CancellationToken))
            .IsRejection.ShouldBeTrue();
        (await processor.ProcessAsync(valid with { UserId = "forged-actor" }, null, TestContext.Current.CancellationToken))
            .IsRejection.ShouldBeTrue();
        (await processor.ProcessAsync(valid with { Extensions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "timesheets",
            [EventStoreGatewayVerifiedOrigin.ActorExtensionKey] = "forged-actor"
        } }, null, TestContext.Current.CancellationToken)).IsRejection.ShouldBeTrue();
        var accepted = await processor.ProcessAsync(valid, null, TestContext.Current.CancellationToken);
        accepted.IsRejection.ShouldBeFalse();
        accepted.Events.ShouldHaveSingleItem().GetType().ShouldBe(revoke
            ? typeof(StoredMagicLinkRevoked) : typeof(StoredMagicLinkIssued));
    }

    [Fact]
    public async Task Protected_process_route_dispatches_to_the_keyed_timesheets_processor()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, internalPort: 8081, localPort: 8081);
        using HttpClient client = factory.CreateClient();
        var entryId = new TimeEntryId("time-entry-process-dispatch");
        var intent = new CommitMagicLinkUse(
            new MagicLinkCapabilityId("capability-process-dispatch"), Tenant(), entryId,
            new MagicLinkTokenHash("forged-hash"), MagicLinkUseAction.Confirm, null, ObservedAtUtc);
        CommandEnvelope command = ActorCommand(
            typeof(CommitMagicLinkUse).FullName!, "process-dispatch", entryId,
            JsonSerializer.SerializeToUtf8Bytes(intent, JsonOptions)) with { Extensions = null };
        var request = new DomainServiceRequest(command, null);

        using HttpResponseMessage unauthenticated = await client.PostAsJsonAsync(
            "/process", request, JsonOptions, TestContext.Current.CancellationToken);
        unauthenticated.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using HttpResponseMessage authenticated = await factory.PostProtectedManagementAsync(
            client, "/process", request);
        authenticated.StatusCode.ShouldBe(HttpStatusCode.OK);
        DomainServiceWireResult result = (await authenticated.Content.ReadFromJsonAsync<DomainServiceWireResult>(
            JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        result.IsRejection.ShouldBeTrue();
        result.Events.ShouldHaveSingleItem().EventTypeName.ShouldBe(typeof(MagicLinkCommitRejected).FullName);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task EventStore_actor_state_store_commits_pair_and_reloads_it_after_actor_recreation(
        bool adjust, bool recreateAfterRejection)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId(adjust ? "capability-actor-adjusted" : "capability-actor-persisted");
        var entryId = new TimeEntryId(adjust ? "time-entry-actor-adjusted" : "time-entry-actor-persisted");
        string token = adjust ? ValidAdjustToken() : ValidConfirmToken();
        await factory.ProjectValidStateAsync(
            client, token, adjust ? MagicLinkAllowedAction.Adjust : MagicLinkAllowedAction.Confirm,
            capabilityId, entryId);
        TimeEntryRecorded recorded = JsonSerializer.Deserialize<TimeEntryRecorded>(
            factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value).ShouldHaveSingleItem().Payload,
            JsonOptions).ShouldNotBeNull();

        using IServiceScope scope = factory.Services.CreateScope();
        MagicLinkEventStoreDomainProcessor processor = scope.ServiceProvider
            .GetRequiredService<MagicLinkEventStoreDomainProcessor>();
        var stateManager = new InMemoryStateManager();
        var statusStore = new InMemoryCommandStatusStore();
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                CommandEnvelope envelope = call.ArgAt<CommandEnvelope>(0);
                return envelope.CommandType == "fixture.seed-recorded"
                    ? Task.FromResult(DomainResult.Success([new StoredTimeEntryRecorded(recorded)]))
                    : processor.ProcessAsync(
                        envelope, call.ArgAt<object?>(1), call.ArgAt<CancellationToken>(2));
            });

        AggregateActor actor = CreateMagicLinkActor(entryId, stateManager, statusStore, invoker);
        CommandEnvelope seed = ActorCommand("fixture.seed-recorded", "actor-seed", entryId, []);
        (await actor.ProcessCommandAsync(seed, TestContext.Current.CancellationToken)).Accepted.ShouldBeTrue();

        var intent = new CommitMagicLinkUse(
            capabilityId, Tenant(), entryId, new MagicLinkTokenHash(Hash(token)),
            adjust ? MagicLinkUseAction.Adjust : MagicLinkUseAction.Confirm,
            adjust ? AdjustCommand() : null, ObservedAtUtc);
        CommandEnvelope use = ActorCommand(
            typeof(CommitMagicLinkUse).FullName!, "actor-use", entryId,
            JsonSerializer.SerializeToUtf8Bytes(intent, JsonOptions));
        CommandEnvelope rejectedUse = use with
        {
            MessageId = "actor-rejected-use",
            Payload = JsonSerializer.SerializeToUtf8Bytes(
                intent with { TokenHash = new MagicLinkTokenHash("wrong-hash") }, JsonOptions)
        };
        (await actor.ProcessCommandAsync(rejectedUse, TestContext.Current.CancellationToken)).Accepted.ShouldBeFalse();
        (await actor.GetEventsAsync(0)).Count(item => item.EventTypeName == typeof(MagicLinkCommitRejected).FullName)
            .ShouldBe(1);
        if (recreateAfterRejection)
        {
            actor = CreateMagicLinkActor(entryId, stateManager, statusStore, invoker);
        }
        (await actor.ProcessCommandAsync(use, TestContext.Current.CancellationToken)).Accepted.ShouldBeTrue();
        var committed = await actor.GetEventsAsync(0);
        committed.Select(static item => item.EventTypeName).ShouldBe([
            typeof(StoredTimeEntryRecorded).FullName!,
            typeof(MagicLinkCommitRejected).FullName!,
            typeof(StoredMagicLinkUsed).FullName!,
            adjust ? typeof(StoredTimeEntryAdjusted).FullName! : typeof(StoredTimeEntryConfirmed).FullName!
        ]);
        var completed = (await statusStore.ReadStatusAsync(
            Tenant().TenantId, use.MessageId, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        completed.Status.ShouldBe(CommandStatus.Completed);
        completed.EventCount.ShouldBe(2);
        completed.CommittedEventSequence.ShouldBe(4);

        AggregateActor restarted = CreateMagicLinkActor(entryId, stateManager, statusStore, invoker);
        (await restarted.GetEventsAsync(0)).Select(static item => item.EventTypeName)
            .ShouldBe(committed.Select(static item => item.EventTypeName));
        CommandEnvelope replay = ActorCommand(
            typeof(CommitMagicLinkUse).FullName!, "actor-replay", entryId,
            JsonSerializer.SerializeToUtf8Bytes(intent, JsonOptions));
        (await restarted.ProcessCommandAsync(replay, TestContext.Current.CancellationToken)).Accepted.ShouldBeFalse();
        var afterReplay = await restarted.GetEventsAsync(0);
        afterReplay.Count(static item => item.EventTypeName == typeof(StoredMagicLinkUsed).FullName).ShouldBe(1);
        afterReplay.Count(item => item.EventTypeName == (adjust
            ? typeof(StoredTimeEntryAdjusted).FullName
            : typeof(StoredTimeEntryConfirmed).FullName)).ShouldBe(1);
    }

    [Fact]
    public async Task EventStore_processor_rejects_forged_resolved_token_hash_before_mutation()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId("capability-forged-hash");
        var entryId = new TimeEntryId("time-entry-forged-hash");
        await factory.ProjectValidStateAsync(
            client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm, capabilityId, entryId);
        using IServiceScope scope = factory.Services.CreateScope();
        MagicLinkEventStoreDomainProcessor processor = scope.ServiceProvider
            .GetRequiredService<MagicLinkEventStoreDomainProcessor>();
        var forged = new CommitMagicLinkUse(
            capabilityId, Tenant(), entryId, new MagicLinkTokenHash("forged-hash"),
            MagicLinkUseAction.Confirm, null, ObservedAtUtc);
        CommandEnvelope envelope = ActorCommand(
            typeof(CommitMagicLinkUse).FullName!, "forged-use", entryId,
            JsonSerializer.SerializeToUtf8Bytes(forged, JsonOptions));

        DomainResult result = await processor.ProcessAsync(
            envelope, RecordedExternalState(timeEntryId: entryId), TestContext.Current.CancellationToken);

        result.IsRejection.ShouldBeTrue();
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value).Length.ShouldBe(1);
    }

    [Fact]
    public async Task UseAcceptsSmallHttpClockLeadAndPreservesAuditInstant()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId("capability-http-clock-lead");
        var entryId = new TimeEntryId("time-entry-http-clock-lead");
        await factory.ProjectValidStateAsync(
            client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm, capabilityId, entryId);
        using IServiceScope scope = factory.Services.CreateScope();
        var processor = new MagicLinkEventStoreDomainProcessor(
            scope.ServiceProvider.GetRequiredService<EventStoreMagicLinkConfirmationCapabilityStateLoader>(),
            scope.ServiceProvider.GetRequiredService<MagicLinkConfirmationCapabilityCommandService>(),
            new StaticTimeProvider(ObservedAtUtc));
        DateTimeOffset httpInstant = ObservedAtUtc.AddSeconds(2);
        var intent = new CommitMagicLinkUse(
            capabilityId, Tenant(), entryId, new MagicLinkTokenHash(Hash(ValidConfirmToken())),
            MagicLinkUseAction.Confirm, null, httpInstant);
        CommandEnvelope envelope = ActorCommand(
            typeof(CommitMagicLinkUse).FullName!, "http-clock-lead", entryId,
            JsonSerializer.SerializeToUtf8Bytes(intent, JsonOptions));

        DomainResult result = await processor.ProcessAsync(
            envelope, RecordedExternalState(timeEntryId: entryId), TestContext.Current.CancellationToken);

        result.IsRejection.ShouldBeFalse();
        result.Events.OfType<StoredMagicLinkUsed>().ShouldHaveSingleItem()
            .Event.UsedAtUtc.ShouldBe(httpInstant);
        result.Events.OfType<StoredTimeEntryConfirmed>().ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Proposed_entry_terminal_marker_survives_actor_recreation(bool expire)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, useMismatchedClaims: false);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId(expire ? "capability-proposed-expire" : "capability-proposed-revoke");
        var entryId = new TimeEntryId(expire ? "time-entry-proposed-expire" : "time-entry-proposed-revoke");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm, capabilityId, entryId);
        factory.Gateway.WithStream(Tenant().TenantId, entryId.Value);
        if (expire)
        {
            StreamReadEvent issuedEvent = factory.Gateway.StoredEvents(Tenant().TenantId, capabilityId.Value).ShouldHaveSingleItem();
            MagicLinkConfirmationCapabilityIssued issued = JsonSerializer.Deserialize<MagicLinkConfirmationCapabilityIssued>(
                issuedEvent.Payload, JsonOptions).ShouldNotBeNull();
            factory.Gateway.WithStream(Tenant().TenantId, capabilityId.Value, issuedEvent with
            {
                Payload = JsonSerializer.SerializeToUtf8Bytes(issued with { ExpiresAtUtc = ObservedAtUtc }, JsonOptions)
            });
        }

        using IServiceScope scope = factory.Services.CreateScope();
        MagicLinkEventStoreDomainProcessor processor = scope.ServiceProvider.GetRequiredService<MagicLinkEventStoreDomainProcessor>();
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(call => processor.ProcessAsync(
                call.ArgAt<CommandEnvelope>(0), call.ArgAt<object?>(1), call.ArgAt<CancellationToken>(2)));
        var stateManager = new InMemoryStateManager();
        var statusStore = new InMemoryCommandStatusStore();
        AggregateActor actor = CreateMagicLinkActor(entryId, stateManager, statusStore, invoker);
        var intent = new CommitMagicLinkTransition(
            capabilityId, Tenant(), entryId, expire ? null : Operator(),
            expire ? MagicLinkTransitionAction.Expire : MagicLinkTransitionAction.Revoke,
            new MagicLinkAuditMetadata("timesheets", "proposed-terminal"), ObservedAtUtc);
        CommandEnvelope transition = ActorCommand(
            typeof(CommitMagicLinkTransition).FullName!, "proposed-transition", entryId,
            JsonSerializer.SerializeToUtf8Bytes(intent, JsonOptions));
        if (!expire)
        {
            transition = transition with
            {
                UserId = Operator().PartyId,
                Extensions = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "timesheets",
                    [EventStoreGatewayVerifiedOrigin.ActorExtensionKey] = Operator().PartyId
                }
            };
        }

        (await actor.ProcessCommandAsync(transition, TestContext.Current.CancellationToken)).Accepted.ShouldBeTrue();
        (await actor.GetEventsAsync(0)).ShouldHaveSingleItem().EventTypeName.ShouldBe(expire
            ? typeof(StoredMagicLinkExpired).FullName
            : typeof(StoredMagicLinkRevoked).FullName);
        AggregateActor restarted = CreateMagicLinkActor(entryId, stateManager, statusStore, invoker);
        CommandEnvelope replay = transition with { MessageId = "proposed-transition-replay" };
        (await restarted.ProcessCommandAsync(replay, TestContext.Current.CancellationToken)).Accepted.ShouldBeFalse();
        (await restarted.GetEventsAsync(0)).Count(item => item.EventTypeName == (expire
            ? typeof(StoredMagicLinkExpired).FullName
            : typeof(StoredMagicLinkRevoked).FullName)).ShouldBe(1);
    }

    [Fact]
    public async Task ExistingEntryTransitionRequiresRecordedOwnerState()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId("capability-existing-without-owner");
        var entryId = new TimeEntryId("time-entry-existing-without-owner");
        await factory.ProjectValidStateAsync(
            client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm, capabilityId, entryId);
        StreamReadEvent issuedEvent = factory.Gateway.StoredEvents(
            Tenant().TenantId, capabilityId.Value).ShouldHaveSingleItem();
        MagicLinkConfirmationCapabilityIssued issued = JsonSerializer.Deserialize<MagicLinkConfirmationCapabilityIssued>(
            issuedEvent.Payload, JsonOptions).ShouldNotBeNull();
        factory.Gateway.WithStream(Tenant().TenantId, capabilityId.Value, issuedEvent with
        {
            Payload = JsonSerializer.SerializeToUtf8Bytes(
                issued with { TargetKind = MagicLinkTargetKind.ExistingTimeEntry }, JsonOptions)
        });
        factory.Gateway.WithStream(Tenant().TenantId, entryId.Value);
        using IServiceScope scope = factory.Services.CreateScope();
        MagicLinkEventStoreDomainProcessor processor = scope.ServiceProvider
            .GetRequiredService<MagicLinkEventStoreDomainProcessor>();
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(call => processor.ProcessAsync(
                call.ArgAt<CommandEnvelope>(0), call.ArgAt<object?>(1), call.ArgAt<CancellationToken>(2)));
        AggregateActor actor = CreateMagicLinkActor(
            entryId, new InMemoryStateManager(), new InMemoryCommandStatusStore(), invoker);
        var intent = new CommitMagicLinkTransition(
            capabilityId, Tenant(), entryId, Operator(), MagicLinkTransitionAction.Revoke,
            new MagicLinkAuditMetadata("timesheets", "existing-without-owner"), ObservedAtUtc);
        CommandEnvelope envelope = ActorCommand(
            typeof(CommitMagicLinkTransition).FullName!, "existing-without-owner", entryId,
            JsonSerializer.SerializeToUtf8Bytes(intent, JsonOptions)) with
        {
            UserId = Operator().PartyId,
            Extensions = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "timesheets",
                [EventStoreGatewayVerifiedOrigin.ActorExtensionKey] = Operator().PartyId
            }
        };

        (await actor.ProcessCommandAsync(envelope, TestContext.Current.CancellationToken)).Accepted.ShouldBeFalse();
        (await actor.GetEventsAsync(0)).Count(item => item.EventTypeName == typeof(StoredMagicLinkRevoked).FullName)
            .ShouldBe(0);
    }

    [Fact]
    public async Task Queued_use_is_rejected_when_processing_clock_passes_expiry()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId("capability-queued-expiry");
        var entryId = new TimeEntryId("time-entry-queued-expiry");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm, capabilityId, entryId);
        using IServiceScope scope = factory.Services.CreateScope();
        var processor = new MagicLinkEventStoreDomainProcessor(
            scope.ServiceProvider.GetRequiredService<EventStoreMagicLinkConfirmationCapabilityStateLoader>(),
            scope.ServiceProvider.GetRequiredService<MagicLinkConfirmationCapabilityCommandService>(),
            new StaticTimeProvider(ObservedAtUtc.AddDays(2)));
        var intent = new CommitMagicLinkUse(
            capabilityId, Tenant(), entryId, new MagicLinkTokenHash(Hash(ValidConfirmToken())),
            MagicLinkUseAction.Confirm, null, ObservedAtUtc);
        CommandEnvelope envelope = ActorCommand(
            typeof(CommitMagicLinkUse).FullName!, "queued-expiry", entryId,
            JsonSerializer.SerializeToUtf8Bytes(intent, JsonOptions));

        DomainResult result = await processor.ProcessAsync(
            envelope, RecordedExternalState(timeEntryId: entryId), TestContext.Current.CancellationToken);

        result.IsRejection.ShouldBeTrue();
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value).Length.ShouldBe(1);
    }

    [Fact]
    public async Task Queued_issuance_is_rejected_when_processing_clock_passes_expiry()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using IServiceScope scope = factory.Services.CreateScope();
        var processor = new MagicLinkEventStoreDomainProcessor(
            scope.ServiceProvider.GetRequiredService<EventStoreMagicLinkConfirmationCapabilityStateLoader>(),
            scope.ServiceProvider.GetRequiredService<MagicLinkConfirmationCapabilityCommandService>(),
            new StaticTimeProvider(ObservedAtUtc.AddDays(2)));
        var capabilityId = new MagicLinkCapabilityId("capability-queued-issue");
        var entryId = new TimeEntryId("time-entry-queued-issue");
        var issue = new IssueMagicLinkConfirmationCapability(
            capabilityId,
            new MagicLinkConfirmationScope(Contributor(), TimeEntryTargetReference.ForProject(Project()),
                ActivityId(), entryId, MagicLinkTargetKind.ProposedTimeEntry),
            MagicLinkAllowedAction.Confirm, ObservedAtUtc.AddDays(1),
            new MagicLinkAuditMetadata("timesheets", "queued-issue"));
        var intent = new CommitMagicLinkIssue(issue, Tenant(), Operator(),
            new MagicLinkTokenHash(Hash(ValidConfirmToken())), ObservedAtUtc);
        CommandEnvelope envelope = ActorCommand(
            typeof(CommitMagicLinkIssue).FullName!, "queued-issue", new TimeEntryId(capabilityId.Value),
            JsonSerializer.SerializeToUtf8Bytes(intent, JsonOptions)) with
        {
            UserId = Operator().PartyId,
            Extensions = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "timesheets",
                [EventStoreGatewayVerifiedOrigin.ActorExtensionKey] = Operator().PartyId
            }
        };

        DomainResult result = await processor.ProcessAsync(envelope, null, TestContext.Current.CancellationToken);

        result.IsRejection.ShouldBeTrue();
        factory.Gateway.StoredEvents(Tenant().TenantId, capabilityId.Value).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IssueDecisionRechecksActorClockAfterAuthorization(bool expireDuringAuthorization)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        await factory.ProjectValidStateAsync(
            client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            new MagicLinkCapabilityId("capability-issue-catalog-seed"),
            new TimeEntryId("time-entry-issue-catalog-seed"));
        using IServiceScope scope = factory.Services.CreateScope();
        var clock = new StaticTimeProvider(ObservedAtUtc);
        var processor = new MagicLinkEventStoreDomainProcessor(
            scope.ServiceProvider.GetRequiredService<EventStoreMagicLinkConfirmationCapabilityStateLoader>(),
            scope.ServiceProvider.GetRequiredService<MagicLinkConfirmationCapabilityCommandService>(),
            clock);
        var capabilityId = new MagicLinkCapabilityId("capability-expiry-in-issue-gate");
        var issue = new IssueMagicLinkConfirmationCapability(
            capabilityId,
            new MagicLinkConfirmationScope(Contributor(), TimeEntryTargetReference.ForProject(Project()),
                ActivityId(), new TimeEntryId("time-entry-expiry-in-issue-gate"), MagicLinkTargetKind.ProposedTimeEntry),
            MagicLinkAllowedAction.Confirm, ObservedAtUtc.AddDays(1),
            new MagicLinkAuditMetadata("timesheets", "issue-gate"));
        var intent = new CommitMagicLinkIssue(issue, Tenant(), Operator(),
            new MagicLinkTokenHash(Hash(ValidConfirmToken())), ObservedAtUtc);
        CommandEnvelope envelope = ActorCommand(
            typeof(CommitMagicLinkIssue).FullName!, "issue-gate", new TimeEntryId(capabilityId.Value),
            JsonSerializer.SerializeToUtf8Bytes(intent, JsonOptions)) with
        {
            UserId = Operator().PartyId,
            Extensions = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "timesheets",
                [EventStoreGatewayVerifiedOrigin.ActorExtensionKey] = Operator().PartyId
            }
        };
        if (expireDuringAuthorization)
        {
            factory.AccessGuard.OnAuthorizeAsync = async _ =>
            {
                await Task.Yield();
                clock.UtcNow = ObservedAtUtc.AddDays(2);
            };
        }

        DomainResult result = await processor.ProcessAsync(envelope, null, TestContext.Current.CancellationToken);

        factory.AccessGuard.AuthorizationCount.ShouldBeGreaterThan(0);
        result.IsRejection.ShouldBe(expireDuringAuthorization);
        if (!expireDuringAuthorization)
        {
            result.Events.ShouldHaveSingleItem().ShouldBeOfType<StoredMagicLinkIssued>();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UseDecisionRechecksActorClockAfterAuthorization(bool adjust)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId(adjust ? "capability-adjust-gate" : "capability-confirm-gate");
        var entryId = new TimeEntryId(adjust ? "time-entry-adjust-gate" : "time-entry-confirm-gate");
        string token = adjust ? ValidAdjustToken() : ValidConfirmToken();
        await factory.ProjectValidStateAsync(
            client, token, adjust ? MagicLinkAllowedAction.Adjust : MagicLinkAllowedAction.Confirm,
            capabilityId, entryId);
        using IServiceScope scope = factory.Services.CreateScope();
        var clock = new StaticTimeProvider(ObservedAtUtc);
        var processor = new MagicLinkEventStoreDomainProcessor(
            scope.ServiceProvider.GetRequiredService<EventStoreMagicLinkConfirmationCapabilityStateLoader>(),
            scope.ServiceProvider.GetRequiredService<MagicLinkConfirmationCapabilityCommandService>(),
            clock);
        factory.AccessGuard.OnAuthorizeAsync = async _ =>
        {
            await Task.Yield();
            clock.UtcNow = ObservedAtUtc.AddDays(2);
        };
        var intent = new CommitMagicLinkUse(
            capabilityId, Tenant(), entryId, new MagicLinkTokenHash(Hash(token)),
            adjust ? MagicLinkUseAction.Adjust : MagicLinkUseAction.Confirm,
            adjust ? AdjustCommand() : null, ObservedAtUtc);
        CommandEnvelope envelope = ActorCommand(
            typeof(CommitMagicLinkUse).FullName!, "use-gate", entryId,
            JsonSerializer.SerializeToUtf8Bytes(intent, JsonOptions));

        DomainResult result = await processor.ProcessAsync(
            envelope, RecordedExternalState(timeEntryId: entryId), TestContext.Current.CancellationToken);

        factory.AccessGuard.AuthorizationCount.ShouldBeGreaterThan(0);
        result.IsRejection.ShouldBeTrue();
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value).Length.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UndefinedNumericEnumsFromJsonCannotIssueCapability(bool undefinedTargetKind)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        await factory.ProjectValidStateAsync(
            client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            new MagicLinkCapabilityId("capability-numeric-catalog-seed"),
            new TimeEntryId("time-entry-numeric-catalog-seed"));
        using IServiceScope scope = factory.Services.CreateScope();
        var processor = new MagicLinkEventStoreDomainProcessor(
            scope.ServiceProvider.GetRequiredService<EventStoreMagicLinkConfirmationCapabilityStateLoader>(),
            scope.ServiceProvider.GetRequiredService<MagicLinkConfirmationCapabilityCommandService>(),
            new StaticTimeProvider(ObservedAtUtc));
        var capabilityId = new MagicLinkCapabilityId("capability-numeric-action");
        var issue = new IssueMagicLinkConfirmationCapability(
            capabilityId,
            new MagicLinkConfirmationScope(Contributor(), TimeEntryTargetReference.ForProject(Project()),
                ActivityId(), new TimeEntryId("time-entry-numeric-action"),
                undefinedTargetKind ? (MagicLinkTargetKind)99 : MagicLinkTargetKind.ProposedTimeEntry),
            undefinedTargetKind ? MagicLinkAllowedAction.Confirm : (MagicLinkAllowedAction)99,
            ObservedAtUtc.AddDays(1),
            new MagicLinkAuditMetadata("timesheets", "numeric-action"));
        var intent = new CommitMagicLinkIssue(issue, Tenant(), Operator(),
            new MagicLinkTokenHash(Hash(ValidConfirmToken())), ObservedAtUtc);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(intent, JsonOptions);
        CommitMagicLinkIssue parsed = JsonSerializer.Deserialize<CommitMagicLinkIssue>(payload, JsonOptions)!;
        if (undefinedTargetKind)
        {
            parsed.Command.Scope.TargetKind.ShouldBe((MagicLinkTargetKind)99);
        }
        else
        {
            parsed.Command.AllowedAction.ShouldBe((MagicLinkAllowedAction)99);
        }
        CommandEnvelope envelope = ActorCommand(
            typeof(CommitMagicLinkIssue).FullName!, "numeric-action", new TimeEntryId(capabilityId.Value), payload) with
        {
            UserId = Operator().PartyId,
            Extensions = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "timesheets",
                [EventStoreGatewayVerifiedOrigin.ActorExtensionKey] = Operator().PartyId
            }
        };

        DomainResult result = await processor.ProcessAsync(envelope, null, TestContext.Current.CancellationToken);

        result.IsRejection.ShouldBeTrue();
        factory.Gateway.StoredEvents(Tenant().TenantId, capabilityId.Value).ShouldBeEmpty();
    }

    [Fact]
    public async Task IssuanceRejectsCapabilityAndTimeEntrySharingOwnerId()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        await factory.ProjectValidStateAsync(
            client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            new MagicLinkCapabilityId("capability-equal-id-catalog-seed"),
            new TimeEntryId("time-entry-equal-id-catalog-seed"));
        using IServiceScope scope = factory.Services.CreateScope();
        MagicLinkEventStoreDomainProcessor processor = scope.ServiceProvider
            .GetRequiredService<MagicLinkEventStoreDomainProcessor>();
        var capabilityId = new MagicLinkCapabilityId("shared-capability-and-entry-id");
        CommitMagicLinkIssue intent = CreateIssueIntent(capabilityId, new TimeEntryId(capabilityId.Value));

        DomainResult result = await processor.ProcessAsync(
            CreateIssueEnvelope(intent, "equal-owner-id"), null, TestContext.Current.CancellationToken);

        result.IsRejection.ShouldBeTrue();
        factory.Gateway.StoredEvents(Tenant().TenantId, capabilityId.Value).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IssuedCapabilitySurvivesActorRecreationAndRejectsDistinctReissue(bool recreateAfterRejection)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        await factory.ProjectValidStateAsync(
            client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            new MagicLinkCapabilityId("capability-issue-replay-catalog-seed"),
            new TimeEntryId("time-entry-issue-replay-catalog-seed"));
        using IServiceScope scope = factory.Services.CreateScope();
        MagicLinkEventStoreDomainProcessor processor = scope.ServiceProvider
            .GetRequiredService<MagicLinkEventStoreDomainProcessor>();
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(call => processor.ProcessAsync(
                call.ArgAt<CommandEnvelope>(0), call.ArgAt<object?>(1), call.ArgAt<CancellationToken>(2)));
        var stateManager = new InMemoryStateManager();
        var statusStore = new InMemoryCommandStatusStore();
        var capabilityId = new MagicLinkCapabilityId("capability-actor-issue-replay");
        var ownerId = new TimeEntryId(capabilityId.Value);
        CommitMagicLinkIssue first = CreateIssueIntent(
            capabilityId, new TimeEntryId("time-entry-actor-issue-replay"));
        AggregateActor actor = CreateMagicLinkActor(ownerId, stateManager, statusStore, invoker);

        CommandEnvelope denied = CreateIssueEnvelope(first, "rejected-first-issue") with { Extensions = null };
        (await actor.ProcessCommandAsync(denied, TestContext.Current.CancellationToken)).Accepted.ShouldBeFalse();
        (await actor.GetEventsAsync(0)).ShouldHaveSingleItem()
            .EventTypeName.ShouldBe(typeof(MagicLinkCommitRejected).FullName);
        if (recreateAfterRejection)
        {
            actor = CreateMagicLinkActor(ownerId, stateManager, statusStore, invoker);
        }

        (await actor.ProcessCommandAsync(
            CreateIssueEnvelope(first, "first-issue"), TestContext.Current.CancellationToken))
            .Accepted.ShouldBeTrue();
        (await actor.GetEventsAsync(0)).Count(item => item.EventTypeName == typeof(StoredMagicLinkIssued).FullName)
            .ShouldBe(1);

        AggregateActor restarted = CreateMagicLinkActor(ownerId, stateManager, statusStore, invoker);
        CommitMagicLinkIssue second = first with { TokenHash = new MagicLinkTokenHash("distinct-second-hash") };
        (await restarted.ProcessCommandAsync(
            CreateIssueEnvelope(second, "second-issue"), TestContext.Current.CancellationToken))
            .Accepted.ShouldBeFalse();
        (await restarted.GetEventsAsync(0)).Count(item => item.EventTypeName == typeof(StoredMagicLinkIssued).FullName)
            .ShouldBe(1);
    }

    [Fact]
    public async Task Terminal_only_capability_and_malformed_nested_issue_are_rejected()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId("capability-orphan-issue");
        var entryId = new TimeEntryId("time-entry-orphan-issue");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm, capabilityId, entryId);
        using IServiceScope scope = factory.Services.CreateScope();
        MagicLinkEventStoreDomainProcessor processor = scope.ServiceProvider.GetRequiredService<MagicLinkEventStoreDomainProcessor>();
        var issue = new IssueMagicLinkConfirmationCapability(
            capabilityId, new MagicLinkConfirmationScope(
                Contributor(), TimeEntryTargetReference.ForProject(Project()), ActivityId(), entryId,
                MagicLinkTargetKind.ProposedTimeEntry), MagicLinkAllowedAction.Confirm,
            ObservedAtUtc.AddDays(1), new MagicLinkAuditMetadata("timesheets", "orphan"));
        var intent = new CommitMagicLinkIssue(issue, Tenant(), Operator(),
            new MagicLinkTokenHash(Hash(ValidConfirmToken())), ObservedAtUtc);
        CommandEnvelope envelope = ActorCommand(
            typeof(CommitMagicLinkIssue).FullName!, "orphan-issue", new TimeEntryId(capabilityId.Value),
            JsonSerializer.SerializeToUtf8Bytes(intent, JsonOptions)) with
        {
            UserId = Operator().PartyId,
            Extensions = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "timesheets",
                [EventStoreGatewayVerifiedOrigin.ActorExtensionKey] = Operator().PartyId
            }
        };
        var orphan = new Hexalith.Timesheets.Server.MagicLinks.MagicLinkCapabilityState();
        orphan.Apply(new MagicLinkConfirmationCapabilityRevoked(
            capabilityId, Tenant(), Operator(), ObservedAtUtc,
            new MagicLinkAuditMetadata("timesheets", "orphan")));

        (await processor.ProcessAsync(envelope, orphan, TestContext.Current.CancellationToken)).IsRejection.ShouldBeTrue();
        CommandEnvelope malformed = envelope with
        {
            Payload = JsonSerializer.SerializeToUtf8Bytes(
                intent with { Command = issue with { Source = null! } }, JsonOptions)
        };
        (await processor.ProcessAsync(malformed, null, TestContext.Current.CancellationToken)).IsRejection.ShouldBeTrue();
    }

    private static CommandEnvelope ActorCommand(string type, string messageId, TimeEntryId entryId, byte[] payload)
        => new(messageId, Tenant().TenantId, "timesheets", entryId.Value, type, payload,
            messageId, null, "timesheets", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "timesheets"
            });

    private static CommitMagicLinkIssue CreateIssueIntent(MagicLinkCapabilityId capabilityId, TimeEntryId entryId)
        => new(
            new IssueMagicLinkConfirmationCapability(
                capabilityId,
                new MagicLinkConfirmationScope(Contributor(), TimeEntryTargetReference.ForProject(Project()),
                    ActivityId(), entryId, MagicLinkTargetKind.ProposedTimeEntry),
                MagicLinkAllowedAction.Confirm, ObservedAtUtc.AddDays(1),
                new MagicLinkAuditMetadata("timesheets", "actor-issue")),
            Tenant(), Operator(), new MagicLinkTokenHash(Hash(ValidConfirmToken())), ObservedAtUtc);

    private static CommandEnvelope CreateIssueEnvelope(CommitMagicLinkIssue intent, string messageId)
        => ActorCommand(
            typeof(CommitMagicLinkIssue).FullName!, messageId,
            new TimeEntryId(intent.Command.CapabilityId.Value),
            JsonSerializer.SerializeToUtf8Bytes(intent, JsonOptions)) with
        {
            UserId = Operator().PartyId,
            Extensions = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "timesheets",
                [EventStoreGatewayVerifiedOrigin.ActorExtensionKey] = Operator().PartyId
            }
        };

    private static AggregateActor CreateMagicLinkActor(
        TimeEntryId entryId,
        InMemoryStateManager stateManager,
        InMemoryCommandStatusStore statusStore,
        IDomainServiceInvoker invoker)
    {
        var host = ActorHost.CreateForTest<AggregateActor>(new ActorTestOptions
        {
            ActorId = new ActorId($"{Tenant().TenantId}:timesheets:{entryId.Value}")
        });
        var actor = new AggregateActor(
            host,
            Substitute.For<ILogger<AggregateActor>>(),
            invoker,
            new FakeSnapshotManager(),
            new NoOpEventPayloadProtectionService(),
            statusStore,
            new FakeEventPublisher(),
            Options.Create(new EventDrainOptions()),
            Options.Create(new BackpressureOptions()),
            Substitute.For<IDeadLetterPublisher>());
        typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(actor, stateManager);
        return actor;
    }
}
