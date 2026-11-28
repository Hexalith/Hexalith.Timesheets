using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Timesheets.Contracts.Commands.ExternalContributions;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.TimeEntries;
using Hexalith.Timesheets.Server.TimeEntries.Commands;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class TimeEntryConfirmationSubmissionServiceTests
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DateTimeOffset ConfirmedAtUtc = new(2026, 10, 10, 12, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(TimeEntryApprovalState.Draft)]
    [InlineData(TimeEntryApprovalState.Submitted)]
    [InlineData(TimeEntryApprovalState.Approved)]
    public async Task ConfirmSucceedsAcrossApprovalStatesAndLogsCommittedWithTraceId(TimeEntryApprovalState approvalState)
    {
        ConfirmExternalTimeEntry command = Command();
        TimesheetsRequestContext context = Context();
        string expectedMessageId = TimeEntryConfirmationSubmissionService.CreateMessageId(context.Tenant!, context.Actor!, command);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        StreamReadEvent[] initialEvents = OwnerEvents(command, approvalState);
        long committedSequence = initialEvents.Length + 1;
        StreamReadEvent confirmedEvent = StoredConfirmedEvent(committedSequence, command, context.Tenant!, ConfirmedAtUtc, expectedMessageId);

        gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(r => r.FromSequence == 0), Arg.Any<CancellationToken>())
            .Returns(Page(initialEvents, fromSequence: 0));
        gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(r => r.FromSequence == committedSequence - 1), Arg.Any<CancellationToken>())
            .Returns(Page([confirmedEvent], fromSequence: committedSequence - 1, latestSequence: committedSequence));
        int statusChecks = 0;
        gateway.GetWorkloadCommandStatusAsync("tenant-1", expectedMessageId, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<CommandStatusQueryResponse?>(++statusChecks == 1 ? null : CompletedStatus(expectedMessageId, committedSequence)));
        gateway.SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SubmitCommandResponse(expectedMessageId, MessageId: expectedMessageId)));

        var logger = new CapturingLogger<TimeEntryConfirmationSubmissionService>();
        using Activity activity = new Activity("confirm-test").Start();
        TimeEntryConfirmationSubmissionService service = CreateService(gateway, AllowAllGuard(), logger);

        bool accepted = await service.ConfirmAsync(context, command, ConfirmedAtUtc, TestContext.Current.CancellationToken);

        accepted.ShouldBeTrue();
        await gateway.Received(1).SubmitWorkloadCommandAsync(
            Arg.Is<SubmitCommandRequest>(request =>
                request.MessageId == expectedMessageId
                && request.CorrelationId == expectedMessageId
                && request.Tenant == "tenant-1"
                && request.Domain == "timesheets"
                && request.AggregateId == command.TimeEntryId.Value
                && request.CommandType == typeof(CommitTimeEntryConfirmation).FullName),
            Arg.Any<CancellationToken>());
        CapturingLogger<TimeEntryConfirmationSubmissionService>.Entry log = logger.Entries.ShouldHaveSingleItem();
        log.Level.ShouldBe(LogLevel.Information);
        log.Message.ShouldContain("outcome=committed");
        log.Message.ShouldContain($"trace={activity.TraceId.ToHexString()}");
        AssertLogPrivacy(log.Message, context, command);
    }

    [Fact]
    public async Task DuplicateConfirmAfterRestartRecoversFromOwnerStreamWithoutResubmitting()
    {
        ConfirmExternalTimeEntry command = Command();
        TimesheetsRequestContext context = Context();
        string messageId = TimeEntryConfirmationSubmissionService.CreateMessageId(context.Tenant!, context.Actor!, command);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        StreamReadEvent[] owner =
        [
            .. OwnerEvents(command, TimeEntryApprovalState.Approved),
            StoredConfirmedEvent(4, command, context.Tenant!, ConfirmedAtUtc.AddHours(-2), messageId)
        ];
        gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(r => r.FromSequence == 0), Arg.Any<CancellationToken>())
            .Returns(Page(owner, fromSequence: 0));

        ITimesheetsAccessGuard guard = AllowAllGuard();
        var logger = new CapturingLogger<TimeEntryConfirmationSubmissionService>();
        TimeEntryConfirmationSubmissionService service = CreateService(gateway, guard, logger);

        bool accepted = await service.ConfirmAsync(context, command, ConfirmedAtUtc, TestContext.Current.CancellationToken);

        accepted.ShouldBeTrue();
        await guard.Received(2).AuthorizeAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>());
        await gateway.DidNotReceive().GetWorkloadCommandStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await gateway.DidNotReceive().SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
        CapturingLogger<TimeEntryConfirmationSubmissionService>.Entry log = logger.Entries.ShouldHaveSingleItem();
        log.Message.ShouldContain("outcome=already-confirmed");
        AssertLogPrivacy(log.Message, context, command);
    }

    [Fact]
    public async Task DuplicateConfirmFromDifferentActorRefusesWithoutSubmitting()
    {
        ConfirmExternalTimeEntry command = Command();
        TimesheetsRequestContext originalContext = Context();
        TimesheetsRequestContext differentActorContext = originalContext with { Actor = new PartyReference("actor-2") };
        string originalMessageId = TimeEntryConfirmationSubmissionService.CreateMessageId(
            originalContext.Tenant!, originalContext.Actor!, command);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        StreamReadEvent[] owner =
        [
            .. OwnerEvents(command, TimeEntryApprovalState.Draft),
            StoredConfirmedEvent(2, command, originalContext.Tenant!, ConfirmedAtUtc.AddMinutes(-5), originalMessageId)
        ];
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(Page(owner, fromSequence: 0));

        var logger = new CapturingLogger<TimeEntryConfirmationSubmissionService>();
        TimeEntryConfirmationSubmissionService service = CreateService(gateway, AllowAllGuard(), logger);

        bool accepted = await service.ConfirmAsync(
            differentActorContext, command, ConfirmedAtUtc, TestContext.Current.CancellationToken);

        accepted.ShouldBeFalse();
        await gateway.DidNotReceive().SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
        logger.Entries.ShouldHaveSingleItem().Message.ShouldContain("outcome=policy-refusal");
    }

    [Fact]
    public async Task LostSubmitAcknowledgementRecoversViaStatusPollAndReadback()
    {
        ConfirmExternalTimeEntry command = Command();
        TimesheetsRequestContext context = Context();
        string messageId = TimeEntryConfirmationSubmissionService.CreateMessageId(context.Tenant!, context.Actor!, command);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        StreamReadEvent[] initial = OwnerEvents(command, TimeEntryApprovalState.Draft);
        StreamReadEvent confirmed = StoredConfirmedEvent(2, command, context.Tenant!, ConfirmedAtUtc, messageId);

        gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(r => r.FromSequence == 0), Arg.Any<CancellationToken>())
            .Returns(Page(initial, fromSequence: 0));
        int readbackAttempts = 0;
        gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(r => r.FromSequence == 1), Arg.Any<CancellationToken>())
            .Returns(_ => ++readbackAttempts == 1
                ? Task.FromException<StreamReadPage>(new EventStoreGatewayException(503, "Transient readback outage"))
                : Task.FromResult(Page([confirmed], fromSequence: 1, latestSequence: 2)));
        int statusCalls = 0;
        gateway.GetWorkloadCommandStatusAsync("tenant-1", messageId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                statusCalls++;
                return statusCalls switch
                {
                    1 => Task.FromResult<CommandStatusQueryResponse?>(null),
                    2 => Task.FromResult<CommandStatusQueryResponse?>(CompletedStatus(messageId, 2) with
                    {
                        Status = nameof(CommandStatus.Processing),
                        StatusCode = (int)CommandStatus.Processing
                    }),
                    _ => Task.FromResult<CommandStatusQueryResponse?>(CompletedStatus(messageId, 2))
                };
            });
        gateway.SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<SubmitCommandResponse>(new HttpRequestException("Lost submit ack after commit")));

        var logger = new CapturingLogger<TimeEntryConfirmationSubmissionService>();
        TimeEntryConfirmationSubmissionService service = CreateService(gateway, AllowAllGuard(), logger);

        bool accepted = await service.ConfirmAsync(context, command, ConfirmedAtUtc, TestContext.Current.CancellationToken);

        accepted.ShouldBeTrue();
        readbackAttempts.ShouldBe(2);
        await gateway.Received(1).SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
        logger.Entries.ShouldHaveSingleItem().Message.ShouldContain("outcome=committed");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CompletedNoOpStatusRecoversMatchingStreamOrReportsPolicyRefusal(bool matchingStreamExists)
    {
        ConfirmExternalTimeEntry command = Command();
        TimesheetsRequestContext context = Context();
        string messageId = TimeEntryConfirmationSubmissionService.CreateMessageId(context.Tenant!, context.Actor!, command);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        StreamReadEvent[] initial = OwnerEvents(command, TimeEntryApprovalState.Draft);
        StreamReadEvent[] refreshed = matchingStreamExists
            ? [.. initial, StoredConfirmedEvent(2, command, context.Tenant!, ConfirmedAtUtc.AddMinutes(-1), messageId)]
            : initial;
        int streamReads = 0;
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Page(++streamReads == 1 ? initial : refreshed, fromSequence: 0));
        int statusChecks = 0;
        gateway.GetWorkloadCommandStatusAsync("tenant-1", messageId, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<CommandStatusQueryResponse?>(++statusChecks == 1
                ? null
                : CompletedStatus(messageId, 1) with { EventCount = 0, CommittedEventSequence = null }));
        gateway.SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SubmitCommandResponse(messageId, MessageId: messageId)));

        var logger = new CapturingLogger<TimeEntryConfirmationSubmissionService>();
        TimeEntryConfirmationSubmissionService service = CreateService(gateway, AllowAllGuard(), logger);

        bool accepted = await service.ConfirmAsync(context, command, ConfirmedAtUtc, TestContext.Current.CancellationToken);

        accepted.ShouldBe(matchingStreamExists);
        logger.Entries.ShouldHaveSingleItem().Message.ShouldContain(
            matchingStreamExists ? "outcome=already-confirmed" : "outcome=policy-refusal");
    }

    [Theory]
    [InlineData("missing-tenant")]
    [InlineData("missing-actor")]
    [InlineData("non-utc")]
    [InlineData("default-timestamp")]
    [InlineData("denied-coarse-guard")]
    [InlineData("missing-stream")]
    [InlineData("empty-stream")]
    [InlineData("non-external")]
    [InlineData("wrong-contributor")]
    [InlineData("conflicting-source")]
    [InlineData("denied-resource-guard")]
    public async Task PolicyRefusalsLeaveZeroSubmissionsAndLogPolicyRefusal(string scenario)
    {
        ConfirmExternalTimeEntry command = Command();
        TimesheetsRequestContext context = scenario switch
        {
            "missing-tenant" => new TimesheetsRequestContext(null, new PartyReference("actor-1"), "corr-1"),
            "missing-actor" => new TimesheetsRequestContext(new TenantReference("tenant-1"), null, "corr-1"),
            _ => Context()
        };
        DateTimeOffset timestamp = scenario switch
        {
            "non-utc" => new DateTimeOffset(2026, 10, 10, 14, 30, 0, TimeSpan.FromHours(2)),
            "default-timestamp" => default,
            _ => ConfirmedAtUtc
        };

        ITimesheetsAccessGuard guard = Substitute.For<ITimesheetsAccessGuard>();
        guard.AuthorizeAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TimesheetsAuthorizationRequest req = call.ArgAt<TimesheetsAuthorizationRequest>(0);
                if (scenario == "denied-coarse-guard")
                {
                    return ValueTask.FromResult(TimesheetsAuthorizationDecision.Denied(TimesheetsDenialCategory.NonMember, "Denied"));
                }

                if (scenario == "denied-resource-guard" && req.Project is not null)
                {
                    return ValueTask.FromResult(TimesheetsAuthorizationDecision.Denied(TimesheetsDenialCategory.InvalidReference, "Denied"));
                }

                return ValueTask.FromResult(TimesheetsAuthorizationDecision.Allowed());
            });

        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        if (scenario == "missing-stream")
        {
            gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException<StreamReadPage>(
                    new EventStoreGatewayException(404, "Not Found", reasonCode: StreamReplayReasonCodes.MissingStream)));
        }
        else
        {
            StreamReadEvent[] events = scenario switch
            {
                "empty-stream" => [],
                "non-external" => [StoredRecordedEvent(1, command, contributorCategory: ContributorCategory.Employee)],
                "wrong-contributor" => [StoredRecordedEvent(1, command, contributor: new PartyReference("party-other"))],
                "conflicting-source" =>
                [
                    StoredRecordedEvent(1, command),
                    StoredConfirmedEvent(
                        2,
                        command with { Source = new ExternalContributionSource("supplier-api", "other-confirm") },
                        context.Tenant!,
                        ConfirmedAtUtc.AddMinutes(-5),
                        "other-message-id")
                ],
                _ => OwnerEvents(command, TimeEntryApprovalState.Draft)
            };
            gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
                .Returns(Page(events, fromSequence: 0));
        }

        var logger = new CapturingLogger<TimeEntryConfirmationSubmissionService>();
        TimeEntryConfirmationSubmissionService service = CreateService(gateway, guard, logger);

        bool accepted = await service.ConfirmAsync(context, command, timestamp, TestContext.Current.CancellationToken);

        accepted.ShouldBeFalse();
        await gateway.DidNotReceive().SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
        if (scenario is "missing-tenant" or "missing-actor" or "non-utc" or "default-timestamp" or "denied-coarse-guard")
        {
            await gateway.DidNotReceive().ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
        }

        if (scenario is "empty-stream" or "missing-stream")
        {
            await guard.Received(1).AuthorizeAsync(
                Arg.Is<TimesheetsAuthorizationRequest>(request => request.Project == null && request.Work == null),
                Arg.Any<CancellationToken>());
            await guard.Received(1).AuthorizeAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>());
        }

        CapturingLogger<TimeEntryConfirmationSubmissionService>.Entry log = logger.Entries.ShouldHaveSingleItem();
        log.Level.ShouldBe(LogLevel.Warning);
        log.Message.ShouldContain("outcome=policy-refusal");
        AssertLogPrivacy(log.Message, context, command);
    }

    [Theory]
    [InlineData("null-contributor")]
    [InlineData("blank-contributor")]
    [InlineData("null-source")]
    [InlineData("blank-source")]
    public async Task InvalidContributorAndSourceRefuseBeforeGuardOrGateway(string scenario)
    {
        ConfirmExternalTimeEntry valid = Command();
        ConfirmExternalTimeEntry command = scenario switch
        {
            "null-contributor" => valid with { Contributor = null! },
            "blank-contributor" => valid with
            {
                Contributor = (PartyReference)RuntimeHelpers.GetUninitializedObject(typeof(PartyReference))
            },
            "null-source" => valid with { Source = null! },
            _ => valid with { Source = (ExternalContributionSource)RuntimeHelpers.GetUninitializedObject(typeof(ExternalContributionSource)) }
        };
        ITimesheetsAccessGuard guard = AllowAllGuard();
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        var logger = new CapturingLogger<TimeEntryConfirmationSubmissionService>();
        TimeEntryConfirmationSubmissionService service = CreateService(gateway, guard, logger);

        (await service.ConfirmAsync(Context(), command, ConfirmedAtUtc, TestContext.Current.CancellationToken)).ShouldBeFalse();

        guard.ReceivedCalls().ShouldBeEmpty();
        gateway.ReceivedCalls().ShouldBeEmpty();
        logger.Entries.ShouldHaveSingleItem().Message.ShouldContain("outcome=policy-refusal");
    }

    [Theory]
    [InlineData("stream-load-503")]
    [InlineData("status-publish-failed")]
    [InlineData("polling-timeout")]
    [InlineData("readback-503-timeout")]
    [InlineData("tampered-correlation")]
    [InlineData("tampered-causation")]
    [InlineData("non-utc-timestamp")]
    [InlineData("default-timestamp")]
    [InlineData("mismatched-user")]
    [InlineData("mismatched-payload-entry")]
    [InlineData("mismatched-payload-contributor")]
    [InlineData("mismatched-payload-tenant")]
    [InlineData("mismatched-payload-source")]
    [InlineData("mismatched-metadata")]
    [InlineData("unsupported-readback-metadata")]
    [InlineData("unsupported-readback-payload")]
    [InlineData("protected-readback-payload")]
    [InlineData("noop-with-sequence")]
    public async Task FaultsTimeoutsAndTamperedReadbacksLogIndeterminateCommit(string scenario)
    {
        ConfirmExternalTimeEntry command = Command();
        TimesheetsRequestContext context = Context();
        string messageId = TimeEntryConfirmationSubmissionService.CreateMessageId(context.Tenant!, context.Actor!, command);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        StreamReadEvent[] initial = OwnerEvents(command, TimeEntryApprovalState.Draft);

        if (scenario == "stream-load-503")
        {
            gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException<StreamReadPage>(new EventStoreGatewayException(503, "Gateway down")));
        }
        else
        {
            gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(r => r.FromSequence == 0), Arg.Any<CancellationToken>())
                .Returns(Page(initial, fromSequence: 0));
            if (scenario == "readback-503-timeout")
            {
                gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(r => r.FromSequence == 1), Arg.Any<CancellationToken>())
                    .Returns(Task.FromException<StreamReadPage>(new EventStoreGatewayException(503, "Readback down")));
            }
            else
            {
                DateTimeOffset readbackTime = scenario switch
                {
                    "non-utc-timestamp" => new DateTimeOffset(2026, 10, 10, 14, 30, 0, TimeSpan.FromHours(2)),
                    "default-timestamp" => default,
                    _ => ConfirmedAtUtc
                };
                ConfirmExternalTimeEntry payloadCommand = scenario switch
                {
                    "mismatched-payload-entry" => command with { TimeEntryId = new("other-entry") },
                    "mismatched-payload-contributor" => command with { Contributor = new("other-contributor") },
                    "mismatched-payload-source" => command with { Source = new("supplier-api", "other-confirmation") },
                    _ => command
                };
                TenantReference payloadTenant = scenario == "mismatched-payload-tenant" ? new("other-tenant") : context.Tenant!;
                StreamReadEvent confirmed = StoredConfirmedEvent(2, payloadCommand, payloadTenant, readbackTime, messageId) with
                {
                    CorrelationId = scenario == "tampered-correlation" ? "other-correlation" : messageId,
                    CausationId = scenario == "tampered-causation" ? "other-causation" : messageId,
                    UserId = scenario == "mismatched-user" ? "other-actor" : "actor-1",
                    MetadataVersion = scenario == "unsupported-readback-metadata" ? 2 : 1,
                    PayloadVersion = scenario == "unsupported-readback-payload" ? 2 : 1,
                    ProtectionMetadata = scenario == "protected-readback-payload"
                        ? EventStorePayloadProtectionMetadata.Unprotected(PayloadProtectionState.Protected) : null
                };
                StreamReadPage readbackPage = Page([confirmed], fromSequence: 1, latestSequence: 2);
                if (scenario == "mismatched-metadata")
                {
                    readbackPage = readbackPage with
                    {
                        Metadata = readbackPage.Metadata with { FromSequence = 0 }
                    };
                }

                gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(r => r.FromSequence == 1), Arg.Any<CancellationToken>())
                    .Returns(readbackPage);
            }
        }

        int statusCalls = 0;
        gateway.GetWorkloadCommandStatusAsync("tenant-1", messageId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                statusCalls++;
                if (scenario == "polling-timeout")
                {
                    return Task.FromResult<CommandStatusQueryResponse?>(null);
                }

                if (statusCalls == 1)
                {
                    return Task.FromResult<CommandStatusQueryResponse?>(null);
                }

                return Task.FromResult<CommandStatusQueryResponse?>(scenario switch
                {
                    "status-publish-failed" => CompletedStatus(messageId, 2) with
                    {
                        Status = nameof(CommandStatus.PublishFailed),
                        StatusCode = (int)CommandStatus.PublishFailed
                    },
                    "noop-with-sequence" => CompletedStatus(messageId, 2) with
                    {
                        EventCount = 0,
                        CommittedEventSequence = 2
                    },
                    _ => CompletedStatus(messageId, 2)
                });
            });
        gateway.SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SubmitCommandResponse(messageId, MessageId: messageId)));

        var logger = new CapturingLogger<TimeEntryConfirmationSubmissionService>();
        var polling = new MagicLinkSubmissionPollingOptions
        {
            Timeout = TimeSpan.FromMilliseconds(150),
            PollInterval = TimeSpan.FromMilliseconds(15),
            ReadbackTimeout = TimeSpan.FromMilliseconds(100)
        };
        TimeEntryConfirmationSubmissionService service = CreateService(gateway, AllowAllGuard(), logger, polling);

        bool accepted = await service.ConfirmAsync(context, command, ConfirmedAtUtc, TestContext.Current.CancellationToken);

        accepted.ShouldBeFalse();
        CapturingLogger<TimeEntryConfirmationSubmissionService>.Entry log = logger.Entries.ShouldHaveSingleItem();
        log.Level.ShouldBe(LogLevel.Warning);
        log.Message.ShouldContain("outcome=indeterminate-commit");
        AssertLogPrivacy(log.Message, context, command);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EarlierUtcCommittedEventLogsAlreadyConfirmedForPreflightAndConcurrentRace(bool presentAtPreflight)
    {
        ConfirmExternalTimeEntry command = Command();
        TimesheetsRequestContext context = Context();
        string messageId = TimeEntryConfirmationSubmissionService.CreateMessageId(context.Tenant!, context.Actor!, command);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        StreamReadEvent[] initial = OwnerEvents(command, TimeEntryApprovalState.Draft);
        StreamReadEvent earlierConfirmed = StoredConfirmedEvent(2, command, context.Tenant!, ConfirmedAtUtc.AddSeconds(-30), messageId);

        gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(r => r.FromSequence == 0), Arg.Any<CancellationToken>())
            .Returns(Page(initial, fromSequence: 0));
        gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(r => r.FromSequence == 1), Arg.Any<CancellationToken>())
            .Returns(Page([earlierConfirmed], fromSequence: 1, latestSequence: 2));
        int statusCalls = 0;
        gateway.GetWorkloadCommandStatusAsync("tenant-1", messageId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                statusCalls++;
                return Task.FromResult<CommandStatusQueryResponse?>(
                    !presentAtPreflight && statusCalls == 1 ? null : CompletedStatus(messageId, 2));
            });
        gateway.SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SubmitCommandResponse(messageId, MessageId: messageId)));

        var logger = new CapturingLogger<TimeEntryConfirmationSubmissionService>();
        TimeEntryConfirmationSubmissionService service = CreateService(gateway, AllowAllGuard(), logger);

        bool accepted = await service.ConfirmAsync(context, command, ConfirmedAtUtc, TestContext.Current.CancellationToken);

        accepted.ShouldBeTrue();
        await gateway.Received(presentAtPreflight ? 0 : 1)
            .SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
        logger.Entries.ShouldHaveSingleItem().Message.ShouldContain("outcome=already-confirmed");
    }

    [Theory]
    [InlineData("domain", "outcome=policy-refusal")]
    [InlineData("wrong-scope", "outcome=indeterminate-commit")]
    [InlineData("infrastructure", "outcome=indeterminate-commit")]
    [InlineData("retryable", "outcome=indeterminate-commit")]
    public async Task RejectedStatusRequiresMatchingScopeAndCanonicalDomainRejection(
        string scenario, string expectedOutcome)
    {
        ConfirmExternalTimeEntry command = Command();
        TimesheetsRequestContext context = Context();
        string messageId = TimeEntryConfirmationSubmissionService.CreateMessageId(context.Tenant!, context.Actor!, command);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(Page(OwnerEvents(command, TimeEntryApprovalState.Draft), fromSequence: 0));
        int statusCalls = 0;
        gateway.GetWorkloadCommandStatusAsync("tenant-1", messageId, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<CommandStatusQueryResponse?>(++statusCalls == 1
                ? null
                : new CommandStatusQueryResponse(messageId, nameof(CommandStatus.Rejected), (int)CommandStatus.Rejected,
                    scenario == "infrastructure" ? null : typeof(MagicLinkCommitRejected).FullName, messageId)
                {
                    TenantId = scenario == "wrong-scope" ? "tenant-2" : "tenant-1",
                    Domain = "timesheets",
                    AggregateId = "entry-1",
                    FailureReason = scenario == "infrastructure" ? "state-unavailable" : null,
                    Retryable = scenario == "retryable"
                }));
        gateway.SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SubmitCommandResponse(messageId, MessageId: messageId)));

        var logger = new CapturingLogger<TimeEntryConfirmationSubmissionService>();
        TimeEntryConfirmationSubmissionService service = CreateService(gateway, AllowAllGuard(), logger);

        (await service.ConfirmAsync(context, command, ConfirmedAtUtc, TestContext.Current.CancellationToken))
            .ShouldBeFalse();
        logger.Entries.ShouldHaveSingleItem().Message.ShouldContain(expectedOutcome);
    }

    [Fact]
    public async Task CancellationAfterCommittedReadbackPreventsAcknowledgement()
    {
        ConfirmExternalTimeEntry command = Command();
        TimesheetsRequestContext context = Context();
        string messageId = TimeEntryConfirmationSubmissionService.CreateMessageId(context.Tenant!, context.Actor!, command);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(request => request.FromSequence == 0), Arg.Any<CancellationToken>())
            .Returns(Page([StoredRecordedEvent(1, command)], fromSequence: 0));
        gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(request => request.FromSequence == 1), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cancellation.Cancel();
                return Page([StoredConfirmedEvent(2, command, context.Tenant!, ConfirmedAtUtc, messageId)], fromSequence: 1);
            });
        gateway.GetWorkloadCommandStatusAsync("tenant-1", messageId, Arg.Any<CancellationToken>())
            .Returns(CompletedStatus(messageId, 2));
        var logger = new CapturingLogger<TimeEntryConfirmationSubmissionService>();
        TimeEntryConfirmationSubmissionService service = CreateService(gateway, AllowAllGuard(), logger);

        await Should.ThrowAsync<OperationCanceledException>(() => service.ConfirmAsync(context, command, ConfirmedAtUtc, cancellation.Token));
        logger.Entries.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task StalledAuthorizationIsBoundedAtCoarseScopedAndRefreshChecks(int stalledCheck)
    {
        ConfirmExternalTimeEntry command = Command();
        TimesheetsRequestContext context = Context();
        string messageId = TimeEntryConfirmationSubmissionService.CreateMessageId(context.Tenant!, context.Actor!, command);
        StreamReadEvent recorded = StoredRecordedEvent(1, command);
        StreamReadEvent confirmed = StoredConfirmedEvent(2, command, context.Tenant!, ConfirmedAtUtc, messageId);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(Page([recorded], fromSequence: 0), Page([recorded, confirmed], fromSequence: 0));
        gateway.GetWorkloadCommandStatusAsync("tenant-1", messageId, Arg.Any<CancellationToken>())
            .Returns(CompletedStatus(messageId, 2) with { EventCount = 0, CommittedEventSequence = null });

        var stalledAuthorization = new TaskCompletionSource<TimesheetsAuthorizationDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reachedStall = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ITimesheetsAccessGuard guard = Substitute.For<ITimesheetsAccessGuard>();
        int checks = 0;
        guard.AuthorizeAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (Interlocked.Increment(ref checks) != stalledCheck)
            {
                return ValueTask.FromResult(TimesheetsAuthorizationDecision.Allowed());
            }

            reachedStall.SetResult();
            return new ValueTask<TimesheetsAuthorizationDecision>(stalledAuthorization.Task);
        });
        var logger = new CapturingLogger<TimeEntryConfirmationSubmissionService>();
        var polling = new MagicLinkSubmissionPollingOptions { Timeout = TimeSpan.FromMilliseconds(500) };
        TimeEntryConfirmationSubmissionService service = CreateService(gateway, guard, logger, polling);

        Task<bool> pending = service.ConfirmAsync(context, command, ConfirmedAtUtc, TestContext.Current.CancellationToken);
        try
        {
            await reachedStall.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            (await pending.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken)).ShouldBeFalse();
            stalledAuthorization.Task.IsCompleted.ShouldBeFalse();
            await gateway.DidNotReceive().SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
            logger.Entries.ShouldHaveSingleItem().Message.ShouldContain("outcome=indeterminate-commit");
        }
        finally
        {
            stalledAuthorization.TrySetResult(TimesheetsAuthorizationDecision.Allowed());
        }
    }

    [Fact]
    public async Task CoarseGuardExceptionReturnsFalseAndLogsIndeterminateCommit()
    {
        ITimesheetsAccessGuard guard = Substitute.For<ITimesheetsAccessGuard>();
        guard.AuthorizeAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns<ValueTask<TimesheetsAuthorizationDecision>>(_ => throw new InvalidOperationException("Transient guard outage"));
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        var logger = new CapturingLogger<TimeEntryConfirmationSubmissionService>();
        TimeEntryConfirmationSubmissionService service = CreateService(gateway, guard, logger);

        bool accepted = await service.ConfirmAsync(Context(), Command(), ConfirmedAtUtc, TestContext.Current.CancellationToken);

        accepted.ShouldBeFalse();
        await gateway.DidNotReceive().ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
        logger.Entries.ShouldHaveSingleItem().Message.ShouldContain("outcome=indeterminate-commit");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AmbiguousMagicLinkTerminalHistoryLogsIndeterminateCommitOnInitialLoadAndNoOpRefresh(bool onNoOpRefresh)
    {
        ConfirmExternalTimeEntry command = Command();
        TimesheetsRequestContext context = Context();
        string messageId = TimeEntryConfirmationSubmissionService.CreateMessageId(context.Tenant!, context.Actor!, command);
        StreamReadEvent[] ambiguous =
        [
            StoredRecordedEvent(1, command),
            StoredRevokedEvent(2, command, context.Tenant!, "cap-1"),
            StoredRevokedEvent(3, command, context.Tenant!, "cap-1")
        ];
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        int reads = 0;
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                reads++;
                return Page(onNoOpRefresh && reads == 1
                    ? OwnerEvents(command, TimeEntryApprovalState.Draft)
                    : ambiguous, fromSequence: 0);
            });
        int statusCalls = 0;
        gateway.GetWorkloadCommandStatusAsync("tenant-1", messageId, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<CommandStatusQueryResponse?>(++statusCalls == 1
                ? null
                : CompletedStatus(messageId, 1) with { EventCount = 0, CommittedEventSequence = null }));
        gateway.SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SubmitCommandResponse(messageId, MessageId: messageId)));

        var logger = new CapturingLogger<TimeEntryConfirmationSubmissionService>();
        TimeEntryConfirmationSubmissionService service = CreateService(gateway, AllowAllGuard(), logger);

        (await service.ConfirmAsync(context, command, ConfirmedAtUtc, TestContext.Current.CancellationToken))
            .ShouldBeFalse();
        logger.Entries.ShouldHaveSingleItem().Message.ShouldContain("outcome=indeterminate-commit");
    }

    [Fact]
    public async Task NoOpRefreshRevalidatesAuthorityAndRefusesWhenDenied()
    {
        ConfirmExternalTimeEntry command = Command();
        TimesheetsRequestContext context = Context();
        string messageId = TimeEntryConfirmationSubmissionService.CreateMessageId(context.Tenant!, context.Actor!, command);
        StreamReadEvent[] initial = OwnerEvents(command, TimeEntryApprovalState.Draft);
        StreamReadEvent[] refreshed = [.. initial, StoredConfirmedEvent(2, command, context.Tenant!, ConfirmedAtUtc.AddMinutes(-1), messageId)];
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        int reads = 0;
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Page(++reads == 1 ? initial : refreshed, fromSequence: 0));
        int statusCalls = 0;
        gateway.GetWorkloadCommandStatusAsync("tenant-1", messageId, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<CommandStatusQueryResponse?>(++statusCalls == 1
                ? null
                : CompletedStatus(messageId, 1) with { EventCount = 0, CommittedEventSequence = null }));
        gateway.SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SubmitCommandResponse(messageId, MessageId: messageId)));

        int authCalls = 0;
        ITimesheetsAccessGuard guard = Substitute.For<ITimesheetsAccessGuard>();
        guard.AuthorizeAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(++authCalls <= 2
                ? TimesheetsAuthorizationDecision.Allowed()
                : TimesheetsAuthorizationDecision.Denied(TimesheetsDenialCategory.NonMember, "Revoked")));

        var logger = new CapturingLogger<TimeEntryConfirmationSubmissionService>();
        TimeEntryConfirmationSubmissionService service = CreateService(gateway, guard, logger);

        (await service.ConfirmAsync(context, command, ConfirmedAtUtc, TestContext.Current.CancellationToken))
            .ShouldBeFalse();
        authCalls.ShouldBe(3);
        logger.Entries.ShouldHaveSingleItem().Message.ShouldContain("outcome=policy-refusal");
    }

    [Theory]
    [InlineData(nameof(StoredTimeEntryConfirmed))]
    [InlineData(nameof(TimeEntryContributorConfirmed))]
    public async Task ShortNamedConfirmationEventsOnOwnerStreamFailClosed(string shortTypeName)
    {
        ConfirmExternalTimeEntry command = Command();
        TimesheetsRequestContext context = Context();
        string messageId = TimeEntryConfirmationSubmissionService.CreateMessageId(context.Tenant!, context.Actor!, command);
        var confirmed = new TimeEntryContributorConfirmed(
            command.TimeEntryId, command.Contributor, context.Tenant!, ConfirmedAtUtc.AddMinutes(-5), command.Source);
        byte[] payload = shortTypeName == nameof(StoredTimeEntryConfirmed)
            ? JsonSerializer.SerializeToUtf8Bytes(new StoredTimeEntryConfirmed(confirmed), _jsonOptions)
            : JsonSerializer.SerializeToUtf8Bytes(confirmed, _jsonOptions);
        StreamReadEvent shortEvent = new(2, shortTypeName, payload, "json", 1, "event-2", messageId, messageId, ConfirmedAtUtc, "actor-1");

        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(Page([StoredRecordedEvent(1, command), shortEvent], fromSequence: 0));
        var logger = new CapturingLogger<TimeEntryConfirmationSubmissionService>();
        TimeEntryConfirmationSubmissionService service = CreateService(gateway, AllowAllGuard(), logger);

        (await service.ConfirmAsync(context, command, ConfirmedAtUtc, TestContext.Current.CancellationToken))
            .ShouldBeFalse();
        await gateway.DidNotReceive().SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
        logger.Entries.ShouldHaveSingleItem().Message.ShouldContain("outcome=policy-refusal");
    }

    [Fact]
    public async Task MultiPageTruncatedOwnerStreamFoldsAndConfirmsWorkTargetEvenWhenLoggerThrows()
    {
        ConfirmExternalTimeEntry command = Command();
        TimesheetsRequestContext context = Context();
        string messageId = TimeEntryConfirmationSubmissionService.CreateMessageId(context.Tenant!, context.Actor!, command);
        StreamReadEvent page1Event = StoredRecordedEvent(
            1, command, target: TimeEntryTargetReference.ForWork(new WorkReference("work-1")));
        var submitted = new TimeEntrySubmitted(
            command.TimeEntryId, command.Contributor, context.Tenant!, ConfirmedAtUtc.AddMinutes(-10),
            new TimeEntrySubmissionId("sub-1"), TimeEntrySubmissionScope.SelectedEntries, TimeEntryApprovalState.Submitted);
        StreamReadEvent page2Event = new(
            2, typeof(StoredTimeEntrySubmitted).FullName!,
            JsonSerializer.SerializeToUtf8Bytes(new StoredTimeEntrySubmitted(submitted), _jsonOptions),
            "json", 1, "event-2", "corr-sub", "corr-sub", ConfirmedAtUtc.AddMinutes(-10), "actor-1");
        StreamReadEvent confirmedEvent = StoredConfirmedEvent(3, command, context.Tenant!, ConfirmedAtUtc, messageId);

        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(r => r.FromSequence == 0), Arg.Any<CancellationToken>())
            .Returns(new StreamReadPage("tenant-1", "timesheets", "entry-1", [page1Event],
                new StreamReadMetadata(0, null, 1, 2, 1, IsTruncated: true, null)));
        gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(r => r.FromSequence == 1), Arg.Any<CancellationToken>())
            .Returns(new StreamReadPage("tenant-1", "timesheets", "entry-1", [page2Event],
                new StreamReadMetadata(1, null, 2, 2, 1, IsTruncated: false, null)));
        gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(r => r.FromSequence == 2), Arg.Any<CancellationToken>())
            .Returns(Page([confirmedEvent], fromSequence: 2, latestSequence: 3));
        int statusCalls = 0;
        gateway.GetWorkloadCommandStatusAsync("tenant-1", messageId, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<CommandStatusQueryResponse?>(++statusCalls == 1 ? null : CompletedStatus(messageId, 3)));
        gateway.SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SubmitCommandResponse(messageId, MessageId: messageId)));

        ITimesheetsAccessGuard guard = AllowAllGuard();
        TimeEntryConfirmationSubmissionService service = CreateService(gateway, guard, new ThrowingLogger());

        bool accepted = await service.ConfirmAsync(context, command, ConfirmedAtUtc, TestContext.Current.CancellationToken);

        accepted.ShouldBeTrue();
        await guard.Received(1).AuthorizeAsync(
            Arg.Is<TimesheetsAuthorizationRequest>(r => r.Work == new WorkReference("work-1")),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("legacy-rejection")]
    [InlineData("rejected-correction")]
    [InlineData("approved-correction")]
    [InlineData("magic-link-adjustment")]
    [InlineData("wrong-previous-scope")]
    [InlineData("wrong-adjusted-scope")]
    public async Task OwnerHistoryFoldsLegacyRejectionCorrectionsAndAdjustments(string scenario)
    {
        ConfirmExternalTimeEntry command = Command();
        TimesheetsRequestContext context = Context();
        TenantReference tenant = context.Tenant!;
        PartyReference actor = context.Actor!;
        ProjectReference originalProject = new("project-1");
        ProjectReference correctedProject = new("corrected-project");
        DateTimeOffset changedAtUtc = ConfirmedAtUtc.AddMinutes(-5);
        var previous = new TimeEntryCorrectionValues(
            TimeEntryTargetReference.ForProject(originalProject), command.Contributor, new ActivityTypeId("activity-1"),
            new DateOnly(2026, 10, 10), 60, BillableState.Billable, ContributorCategory.ExternalContributor, null)
        { ActivityTypeScope = ActivityTypeScope.Tenant };
        TimeEntryCorrectionValues corrected = previous with { Target = TimeEntryTargetReference.ForProject(correctedProject) };
        var decisionId = new TimeEntryApprovalDecisionId("decision-1");
        var correctionId = new TimeEntryCorrectionId("correction-1");
        var rejectionReason = new TimeEntryRejectionReason("Correct the project");
        const TimeEntryApprovalScope approvalScope = TimeEntryApprovalScope.IndividualEntry;
        var rejectionAuthority = new ApprovalAuthoritySourceAttribution(
            ApprovalAuthorityAction.EntryRejection, ApprovalAuthoritySource.ProjectApprover,
            ApprovalAuthorityDecisionState.Allowed, "policy-key", "v1", ProjectionFreshnessMetadata.Fresh);

        StreamReadEvent[] events = scenario switch
        {
            "legacy-rejection" => [StoredRecordedEvent(1, command), OwnerEvent(2, new MagicLinkCommitRejected())],
            "rejected-correction" =>
            [
                .. OwnerEvents(command, TimeEntryApprovalState.Submitted),
                OwnerEvent(3, new TimeEntryRejected(command.TimeEntryId, actor, tenant, changedAtUtc,
                    decisionId, TimeEntryApprovalState.Rejected, rejectionAuthority, approvalScope, rejectionReason)),
                OwnerEvent(4, new TimeEntryCorrected(command.TimeEntryId, correctionId, tenant, actor, changedAtUtc,
                    previous, corrected, rejectionReason, decisionId, TimeEntryApprovalState.Draft, TimeEntryCorrectionState.Corrected))
            ],
            "approved-correction" =>
            [
                .. OwnerEvents(command, TimeEntryApprovalState.Approved),
                OwnerEvent(4, new TimeEntryApprovedCorrected(command.TimeEntryId, correctionId, tenant, actor, changedAtUtc,
                    previous, corrected, new TimeEntryCorrectionReason("Correct the project"), decisionId, approvalScope,
                    TimeEntryApprovalState.Approved, TimeEntryCorrectionState.Corrected))
            ],
            _ =>
            [
                StoredRecordedEvent(1, command),
                OwnerEvent(2, new StoredTimeEntryAdjusted(new TimeEntryAdjustedThroughMagicLink(
                    command.TimeEntryId, tenant, command.Contributor, changedAtUtc, ActivityTypeScope.Project,
                    scenario == "wrong-previous-scope" ? previous with { ActivityTypeScope = ActivityTypeScope.Project } : previous,
                    previous with
                    {
                        DurationMinutes = 90,
                        ActivityTypeScope = scenario == "wrong-adjusted-scope" ? ActivityTypeScope.Tenant : ActivityTypeScope.Project
                    }, new ExternalContributionSource("timesheets", "adjust-1"))))
            ]
        };

        string messageId = TimeEntryConfirmationSubmissionService.CreateMessageId(tenant, actor, command);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(request => request.FromSequence == 0), Arg.Any<CancellationToken>())
            .Returns(Page(events, fromSequence: 0));
        gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(request => request.FromSequence == events.Length), Arg.Any<CancellationToken>())
            .Returns(Page([StoredConfirmedEvent(events.Length + 1, command, tenant, ConfirmedAtUtc, messageId)], events.Length));
        int statusChecks = 0;
        gateway.GetWorkloadCommandStatusAsync("tenant-1", messageId, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<CommandStatusQueryResponse?>(++statusChecks == 1
                ? null : CompletedStatus(messageId, events.Length + 1)));
        gateway.SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitCommandResponse(messageId, MessageId: messageId));
        ITimesheetsAccessGuard guard = AllowAllGuard();
        var logger = new CapturingLogger<TimeEntryConfirmationSubmissionService>();
        TimeEntryConfirmationSubmissionService service = CreateService(gateway, guard, logger);

        bool accepted = await service.ConfirmAsync(context, command, ConfirmedAtUtc, TestContext.Current.CancellationToken);

        bool valid = scenario is not ("wrong-previous-scope" or "wrong-adjusted-scope");
        ProjectReference expectedProject = scenario is "rejected-correction" or "approved-correction" ? correctedProject : originalProject;
        accepted.ShouldBe(valid);
        await guard.Received(valid ? 1 : 0).AuthorizeAsync(
            Arg.Is<TimesheetsAuthorizationRequest>(request => request.Project == expectedProject), Arg.Any<CancellationToken>());
        await gateway.Received(valid ? 1 : 0).SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
        logger.Entries.ShouldHaveSingleItem().Message.ShouldContain(valid ? "outcome=committed" : "outcome=indeterminate-commit");
    }

    [Theory]
    [InlineData("metadata-version")]
    [InlineData("payload-version")]
    [InlineData("protection-state")]
    [InlineData("protection-version")]
    [InlineData("conflicting-duplicate-payload-version")]
    [InlineData("conflicting-duplicate-global-position")]
    [InlineData("oversized-page")]
    public async Task UnsupportedOwnerEventMetadataIsIndeterminate(string scenario)
    {
        ConfirmExternalTimeEntry command = Command();
        StreamReadEvent recorded = StoredRecordedEvent(1, command);
        StreamReadEvent[] events = scenario switch
        {
            "metadata-version" => [recorded with { MetadataVersion = 2 }],
            "payload-version" => [recorded with { PayloadVersion = 2 }],
            "protection-state" => [recorded with { ProtectionMetadata = EventStorePayloadProtectionMetadata.ProviderOpaque() }],
            "protection-version" =>
                [recorded with { ProtectionMetadata = EventStorePayloadProtectionMetadata.Unprotected() with { MetadataVersion = 2 } }],
            "conflicting-duplicate-global-position" => [recorded, recorded with { GlobalPosition = 2 }],
            "oversized-page" => Enumerable.Repeat(recorded, 501).ToArray(),
            _ => [recorded, recorded with { PayloadVersion = 2 }]
        };
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(Page(events, fromSequence: 0));
        var logger = new CapturingLogger<TimeEntryConfirmationSubmissionService>();
        TimeEntryConfirmationSubmissionService service = CreateService(gateway, AllowAllGuard(), logger);

        (await service.ConfirmAsync(Context(), command, ConfirmedAtUtc, TestContext.Current.CancellationToken)).ShouldBeFalse();

        await gateway.DidNotReceive().SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
        logger.Entries.ShouldHaveSingleItem().Message.ShouldContain("outcome=indeterminate-commit");
    }

    [Theory]
    [InlineData("cross-tenant-page")]
    [InlineData("gapped-sequence")]
    [InlineData("malformed-cursor")]
    public async Task MalformedGappedOrCrossTenantOwnerStreamPagesLogIndeterminateCommit(string scenario)
    {
        ConfirmExternalTimeEntry command = Command();
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        StreamReadEvent recorded = StoredRecordedEvent(1, command);
        StreamReadPage badPage = scenario switch
        {
            "cross-tenant-page" => new StreamReadPage("tenant-2", "timesheets", "entry-1", [recorded],
                new StreamReadMetadata(0, null, 1, 1, 1, false, null)),
            "gapped-sequence" => new StreamReadPage("tenant-1", "timesheets", "entry-1", [recorded with { SequenceNumber = 2 }],
                new StreamReadMetadata(0, null, 2, 2, 1, false, null)),
            _ => new StreamReadPage("tenant-1", "timesheets", "entry-1", [],
                new StreamReadMetadata(0, null, null, 2, 0, IsTruncated: true, null))
        };
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(badPage);

        var logger = new CapturingLogger<TimeEntryConfirmationSubmissionService>();
        TimeEntryConfirmationSubmissionService service = CreateService(gateway, AllowAllGuard(), logger);

        (await service.ConfirmAsync(Context(), command, ConfirmedAtUtc, TestContext.Current.CancellationToken))
            .ShouldBeFalse();
        logger.Entries.ShouldHaveSingleItem().Message.ShouldContain("outcome=indeterminate-commit");
    }

    [Theory]
    [InlineData("foreign-submitted-tenant")]
    [InlineData("foreign-approved-tenant")]
    [InlineData("foreign-recorded-entry")]
    public async Task PreSubmitForeignEventIdentityOnMatchingHeaderPageLogsIndeterminateCommit(string scenario)
    {
        ConfirmExternalTimeEntry command = Command();
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        ITimesheetsAccessGuard guard = AllowAllGuard();
        StreamReadEvent[] events = scenario switch
        {
            "foreign-submitted-tenant" =>
            [
                StoredRecordedEvent(1, command),
                OwnerEvent(2, new StoredTimeEntrySubmitted(new TimeEntrySubmitted(
                    command.TimeEntryId,
                    command.Contributor,
                    new TenantReference("tenant-2"),
                    ConfirmedAtUtc.AddMinutes(-20),
                    new TimeEntrySubmissionId("submit-1"),
                    TimeEntrySubmissionScope.SelectedEntries,
                    TimeEntryApprovalState.Submitted)))
            ],
            "foreign-approved-tenant" =>
            [
                .. OwnerEvents(command, TimeEntryApprovalState.Submitted),
                OwnerEvent(3, new TimeEntryApproved(
                    command.TimeEntryId,
                    new PartyReference("approver-1"),
                    new TenantReference("tenant-2"),
                    ConfirmedAtUtc.AddMinutes(-10),
                    new TimeEntryApprovalDecisionId("decision-1"),
                    TimeEntryApprovalState.Approved,
                    new ApprovalAuthoritySourceAttribution(
                        ApprovalAuthorityAction.EntryApproval,
                        ApprovalAuthoritySource.ProjectApprover,
                        ApprovalAuthorityDecisionState.Allowed,
                        "policy-key",
                        "v1",
                        ProjectionFreshnessMetadata.Fresh),
                    TimeEntryApprovalScope.IndividualEntry))
            ],
            _ => [StoredRecordedEvent(1, command with { TimeEntryId = new TimeEntryId("entry-2") })]
        };
        gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(request => request.FromSequence == 0), Arg.Any<CancellationToken>())
            .Returns(Page(events, fromSequence: 0));
        var logger = new CapturingLogger<TimeEntryConfirmationSubmissionService>();
        TimeEntryConfirmationSubmissionService service = CreateService(gateway, guard, logger);

        (await service.ConfirmAsync(Context(), command, ConfirmedAtUtc, TestContext.Current.CancellationToken))
            .ShouldBeFalse();

        await guard.Received(1).AuthorizeAsync(
            Arg.Is<TimesheetsAuthorizationRequest>(req => req.Project == null && req.Work == null),
            Arg.Any<CancellationToken>());
        await guard.DidNotReceive().AuthorizeAsync(
            Arg.Is<TimesheetsAuthorizationRequest>(req => req.Project != null || req.Work != null),
            Arg.Any<CancellationToken>());
        await gateway.DidNotReceive().SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
        logger.Entries.ShouldHaveSingleItem().Message.ShouldContain("outcome=indeterminate-commit");
    }

    [Theory]
    [InlineData("duplicate-matching")]
    [InlineData("prior-non-matching-then-matching")]
    public async Task MultipleOrMixedStoredConfirmationEventsLogPolicyRefusal(string scenario)
    {
        ConfirmExternalTimeEntry command = Command();
        TimesheetsRequestContext context = Context();
        string messageId = TimeEntryConfirmationSubmissionService.CreateMessageId(context.Tenant!, context.Actor!, command);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        StreamReadEvent firstConfirmation = scenario == "duplicate-matching"
            ? StoredConfirmedEvent(2, command, context.Tenant!, ConfirmedAtUtc.AddMinutes(-2), messageId)
            : StoredConfirmedEvent(
                2,
                command with { Source = new ExternalContributionSource("magic-link", "cap-1") },
                context.Tenant!,
                ConfirmedAtUtc.AddMinutes(-2),
                "other-message-id");
        StreamReadEvent secondConfirmation = StoredConfirmedEvent(3, command, context.Tenant!, ConfirmedAtUtc.AddMinutes(-1), messageId);
        gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(request => request.FromSequence == 0), Arg.Any<CancellationToken>())
            .Returns(Page([StoredRecordedEvent(1, command), firstConfirmation, secondConfirmation], fromSequence: 0));
        var logger = new CapturingLogger<TimeEntryConfirmationSubmissionService>();
        TimeEntryConfirmationSubmissionService service = CreateService(gateway, AllowAllGuard(), logger);

        (await service.ConfirmAsync(context, command, ConfirmedAtUtc, TestContext.Current.CancellationToken))
            .ShouldBeFalse();

        await gateway.DidNotReceive().SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
        logger.Entries.ShouldHaveSingleItem().Message.ShouldContain("outcome=policy-refusal");
    }

    [Theory]
    [InlineData("owner-replay")]
    [InlineData("committed-readback")]
    public async Task DuplicateJsonPropertiesInStoredEventsLogIndeterminateCommit(string stage)
    {
        ConfirmExternalTimeEntry command = Command();
        TimesheetsRequestContext context = Context();
        string messageId = TimeEntryConfirmationSubmissionService.CreateMessageId(context.Tenant!, context.Actor!, command);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        StreamReadEvent recorded = StoredRecordedEvent(1, command);
        if (stage == "owner-replay")
        {
            string rawRecorded = System.Text.Encoding.UTF8.GetString(recorded.Payload);
            byte[] duplicatePayload = System.Text.Encoding.UTF8.GetBytes(
                rawRecorded.Replace("\"event\":{", "\"event\":{\"timeEntryId\":{\"value\":\"evil\"},", StringComparison.Ordinal));
            gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(request => request.FromSequence == 0), Arg.Any<CancellationToken>())
                .Returns(Page([recorded with { Payload = duplicatePayload }], fromSequence: 0));
        }
        else
        {
            StreamReadEvent confirmed = StoredConfirmedEvent(2, command, context.Tenant!, ConfirmedAtUtc, messageId);
            string rawConfirmed = System.Text.Encoding.UTF8.GetString(confirmed.Payload);
            byte[] duplicatePayload = System.Text.Encoding.UTF8.GetBytes(
                rawConfirmed.Replace("\"event\":{", "\"event\":{\"tenant\":{\"tenantId\":\"tenant-2\"},", StringComparison.Ordinal));
            gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(request => request.FromSequence == 0), Arg.Any<CancellationToken>())
                .Returns(Page([recorded], fromSequence: 0));
            gateway.GetWorkloadCommandStatusAsync("tenant-1", messageId, Arg.Any<CancellationToken>())
                .Returns(CompletedStatus(messageId, 2));
            gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(request => request.FromSequence == 1), Arg.Any<CancellationToken>())
                .Returns(Page([confirmed with { Payload = duplicatePayload }], fromSequence: 1));
        }

        var logger = new CapturingLogger<TimeEntryConfirmationSubmissionService>();
        TimeEntryConfirmationSubmissionService service = CreateService(gateway, AllowAllGuard(), logger);

        (await service.ConfirmAsync(context, command, ConfirmedAtUtc, TestContext.Current.CancellationToken))
            .ShouldBeFalse();
        logger.Entries.ShouldHaveSingleItem().Message.ShouldContain("outcome=indeterminate-commit");
    }

    [Fact]
    public async Task TransientOwnerFailureAndStaleCommittedPageAreRetriedUsingValidatedPollingOptions()
    {
        ConfirmExternalTimeEntry command = Command();
        TimesheetsRequestContext context = Context();
        string messageId = TimeEntryConfirmationSubmissionService.CreateMessageId(context.Tenant!, context.Actor!, command);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(request => request.FromSequence == 0), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<StreamReadPage>(new EventStoreGatewayException(503, "Temporary owner read failure")),
                Task.FromResult(Page([StoredRecordedEvent(1, command)], fromSequence: 0)));
        gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(request => request.FromSequence == 1), Arg.Any<CancellationToken>())
            .Returns(Page([], fromSequence: 1, latestSequence: 1),
                Page([StoredConfirmedEvent(2, command, context.Tenant!, ConfirmedAtUtc, messageId)], fromSequence: 1));
        gateway.GetWorkloadCommandStatusAsync("tenant-1", messageId, Arg.Any<CancellationToken>())
            .Returns(CompletedStatus(messageId, 2));
        var logger = new CapturingLogger<TimeEntryConfirmationSubmissionService>();
        var polling = new MagicLinkSubmissionPollingOptions { PollInterval = TimeSpan.FromMilliseconds(15) };
        TimeEntryConfirmationSubmissionService service = CreateService(gateway, AllowAllGuard(), logger, polling);
        polling.PollInterval = TimeSpan.FromMilliseconds(-2);

        (await service.ConfirmAsync(context, command, ConfirmedAtUtc, TestContext.Current.CancellationToken)).ShouldBeTrue();

        await gateway.Received(2).ReadWorkloadStreamAsync(
            Arg.Is<StreamReadRequest>(request => request.FromSequence == 0), Arg.Any<CancellationToken>());
        await gateway.Received(2).ReadWorkloadStreamAsync(
            Arg.Is<StreamReadRequest>(request => request.FromSequence == 1), Arg.Any<CancellationToken>());
        logger.Entries.ShouldHaveSingleItem().Message.ShouldContain("outcome=already-confirmed");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnexpectedOwnerStreamNotFoundIsIndeterminate(bool afterFirstPage)
    {
        ConfirmExternalTimeEntry command = Command();
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        StreamReadPage firstPage = Page([StoredRecordedEvent(1, command)], fromSequence: 0, latestSequence: 2);
        firstPage = firstPage with { Metadata = firstPage.Metadata with { IsTruncated = true } };
        int failedFromSequence = afterFirstPage ? 1 : 0;
        gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(request => request.FromSequence == 0), Arg.Any<CancellationToken>())
            .Returns(firstPage);
        gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(request => request.FromSequence == failedFromSequence), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<StreamReadPage>(new EventStoreGatewayException(404, "Not Found",
                reasonCode: afterFirstPage ? StreamReplayReasonCodes.MissingStream : "other-not-found")));
        var logger = new CapturingLogger<TimeEntryConfirmationSubmissionService>();
        TimeEntryConfirmationSubmissionService service = CreateService(gateway, AllowAllGuard(), logger);

        (await service.ConfirmAsync(Context(), command, ConfirmedAtUtc, TestContext.Current.CancellationToken)).ShouldBeFalse();

        await gateway.Received(afterFirstPage ? 2 : 1).ReadWorkloadStreamAsync(
            Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
        await gateway.DidNotReceive().SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
        logger.Entries.ShouldHaveSingleItem().Message.ShouldContain("outcome=indeterminate-commit");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task CommittedStatusMustPointPastTheLoadedOwnerHistory(long sequence)
    {
        ConfirmExternalTimeEntry command = Command();
        TimesheetsRequestContext context = Context();
        string messageId = TimeEntryConfirmationSubmissionService.CreateMessageId(context.Tenant!, context.Actor!, command);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(request => request.FromSequence == 0), Arg.Any<CancellationToken>())
            .Returns(Page(OwnerEvents(command, TimeEntryApprovalState.Submitted), fromSequence: 0));
        gateway.GetWorkloadCommandStatusAsync("tenant-1", messageId, Arg.Any<CancellationToken>())
            .Returns(CompletedStatus(messageId, sequence));
        var logger = new CapturingLogger<TimeEntryConfirmationSubmissionService>();
        TimeEntryConfirmationSubmissionService service = CreateService(gateway, AllowAllGuard(), logger);

        (await service.ConfirmAsync(context, command, ConfirmedAtUtc, TestContext.Current.CancellationToken)).ShouldBeFalse();

        await gateway.Received(1).ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
        logger.Entries.ShouldHaveSingleItem().Message.ShouldContain("outcome=indeterminate-commit");
    }

    [Fact]
    public async Task CallerCancellationPropagatesWithoutSwallowing()
    {
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                caller.Cancel();
                return Task.FromCanceled<StreamReadPage>(caller.Token);
            });

        var logger = new CapturingLogger<TimeEntryConfirmationSubmissionService>();
        TimeEntryConfirmationSubmissionService service = CreateService(gateway, AllowAllGuard(), logger);

        await Should.ThrowAsync<OperationCanceledException>(() =>
            service.ConfirmAsync(Context(), Command(), ConfirmedAtUtc, caller.Token));
        logger.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void CreateMessageIdDistinguishesActorsTenantsAndNewlineBearingSources()
    {
        ConfirmExternalTimeEntry baseline = Command();
        string baseId = TimeEntryConfirmationSubmissionService.CreateMessageId(
            new("tenant-1"), new("actor-1"), baseline);
        string otherActor = TimeEntryConfirmationSubmissionService.CreateMessageId(
            new("tenant-1"), new("actor-2"), baseline);
        string otherTenant = TimeEntryConfirmationSubmissionService.CreateMessageId(
            new("tenant-2"), new("actor-1"), baseline);
        string newlineA = TimeEntryConfirmationSubmissionService.CreateMessageId(
            new("tenant-1"), new("actor-1"), baseline with { Source = new("a\nb", "c") });
        string newlineB = TimeEntryConfirmationSubmissionService.CreateMessageId(
            new("tenant-1"), new("actor-1"), baseline with { Source = new("a", "b\nc") });

        baseId.ShouldNotBe(otherActor);
        baseId.ShouldNotBe(otherTenant);
        newlineA.ShouldNotBe(newlineB);
    }

    private static TimeEntryConfirmationSubmissionService CreateService(
        IEventStoreGatewayClient gateway,
        ITimesheetsAccessGuard guard,
        ILogger<TimeEntryConfirmationSubmissionService> logger,
        MagicLinkSubmissionPollingOptions? polling = null)
    {
        IOptions<MagicLinkSubmissionPollingOptions> options = Options.Create(
            polling ?? new MagicLinkSubmissionPollingOptions
            {
                Timeout = TimeSpan.FromSeconds(2),
                PollInterval = TimeSpan.FromMilliseconds(15),
                ReadbackTimeout = TimeSpan.FromSeconds(1)
            });
        var durable = new MagicLinkDurableSubmissionService(gateway, options);
        var external = new ExternalContributionCommandService(
            new TimeEntryCommandService(guard),
            new TimeEntrySubmissionCommandService(guard),
            guard,
            ExternalContributionPolicyOptions.Default);
        return new TimeEntryConfirmationSubmissionService(durable, guard, external, options, logger);
    }

    private static ITimesheetsAccessGuard AllowAllGuard()
    {
        ITimesheetsAccessGuard guard = Substitute.For<ITimesheetsAccessGuard>();
        guard.AuthorizeAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(TimesheetsAuthorizationDecision.Allowed()));
        return guard;
    }

    private static TimesheetsRequestContext Context()
        => new(new TenantReference("tenant-1"), new PartyReference("actor-1"), "correlation-1");

    private static ConfirmExternalTimeEntry Command()
        => new(
            new TimeEntryId("entry-1"),
            new PartyReference("contributor-1"),
            new ExternalContributionSource("supplier-api", "confirm-1"));

    private static StreamReadEvent[] OwnerEvents(
        ConfirmExternalTimeEntry command,
        TimeEntryApprovalState approvalState)
    {
        List<StreamReadEvent> events = [StoredRecordedEvent(1, command)];
        if (approvalState is TimeEntryApprovalState.Submitted or TimeEntryApprovalState.Approved)
        {
            var submitted = new TimeEntrySubmitted(
                command.TimeEntryId,
                command.Contributor,
                new TenantReference("tenant-1"),
                ConfirmedAtUtc.AddMinutes(-20),
                new TimeEntrySubmissionId("submit-1"),
                TimeEntrySubmissionScope.SelectedEntries,
                TimeEntryApprovalState.Submitted);
            events.Add(new StreamReadEvent(
                2,
                typeof(StoredTimeEntrySubmitted).FullName!,
                JsonSerializer.SerializeToUtf8Bytes(new StoredTimeEntrySubmitted(submitted), _jsonOptions),
                "json",
                1,
                "event-2",
                "corr-submit",
                "corr-submit",
                ConfirmedAtUtc.AddMinutes(-20),
                "actor-1"));
        }

        if (approvalState == TimeEntryApprovalState.Approved)
        {
            var approved = new TimeEntryApproved(
                command.TimeEntryId,
                new PartyReference("approver-1"),
                new TenantReference("tenant-1"),
                ConfirmedAtUtc.AddMinutes(-10),
                new TimeEntryApprovalDecisionId("decision-1"),
                TimeEntryApprovalState.Approved,
                new ApprovalAuthoritySourceAttribution(
                    ApprovalAuthorityAction.EntryApproval,
                    ApprovalAuthoritySource.ProjectApprover,
                    ApprovalAuthorityDecisionState.Allowed,
                    "policy-key",
                    "v1",
                    ProjectionFreshnessMetadata.Fresh),
                TimeEntryApprovalScope.IndividualEntry);
            events.Add(new StreamReadEvent(
                3,
                typeof(TimeEntryApproved).FullName!,
                JsonSerializer.SerializeToUtf8Bytes(approved, _jsonOptions),
                "json",
                1,
                "event-3",
                "corr-approve",
                "corr-approve",
                ConfirmedAtUtc.AddMinutes(-10),
                "approver-1"));
        }

        return [.. events];
    }

    private static StreamReadEvent StoredRecordedEvent(
        long sequence,
        ConfirmExternalTimeEntry command,
        PartyReference? contributor = null,
        ContributorCategory contributorCategory = ContributorCategory.ExternalContributor,
        TimeEntryTargetReference? target = null)
    {
        var recorded = new TimeEntryRecorded(
            command.TimeEntryId,
            target ?? TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            contributor ?? command.Contributor,
            new ActivityTypeId("activity-1"),
            ActivityTypeScope.Tenant,
            new DateOnly(2026, 10, 10),
            60,
            BillableState.Billable,
            TimeEntryApprovalState.Draft,
            contributorCategory,
            null)
        {
            ExternalSource = new ExternalContributionSource("supplier-api", "record-1")
        };
        return new StreamReadEvent(
            sequence,
            typeof(StoredTimeEntryRecorded).FullName!,
            JsonSerializer.SerializeToUtf8Bytes(new StoredTimeEntryRecorded(recorded), _jsonOptions),
            "json",
            1,
            $"event-{sequence}",
            "corr-record",
            "corr-record",
            ConfirmedAtUtc.AddMinutes(-30),
            "actor-1");
    }

    private static StreamReadEvent OwnerEvent<T>(long sequence, T payload)
        => new(sequence, typeof(T).FullName!, JsonSerializer.SerializeToUtf8Bytes(payload, _jsonOptions),
            "json", 1, $"event-{sequence}", "corr-history", "corr-history", ConfirmedAtUtc.AddMinutes(-5), "actor-1");

    private static StreamReadEvent StoredRevokedEvent(
        long sequence,
        ConfirmExternalTimeEntry command,
        TenantReference tenant,
        string capabilityId)
    {
        var revoked = new MagicLinkConfirmationCapabilityRevoked(
            new MagicLinkCapabilityId(capabilityId),
            tenant,
            new PartyReference("operator-1"),
            ConfirmedAtUtc.AddMinutes(-5),
            new MagicLinkAuditMetadata("timesheets", $"revoke-{sequence}"))
        {
            TimeEntryId = command.TimeEntryId
        };
        return new StreamReadEvent(
            sequence,
            typeof(StoredMagicLinkRevoked).FullName!,
            JsonSerializer.SerializeToUtf8Bytes(new StoredMagicLinkRevoked(revoked), _jsonOptions),
            "json",
            1,
            $"event-revoke-{sequence}",
            "corr-revoke",
            "corr-revoke",
            ConfirmedAtUtc.AddMinutes(-5),
            "operator-1");
    }

    private static StreamReadEvent StoredConfirmedEvent(
        long sequence,
        ConfirmExternalTimeEntry command,
        TenantReference tenant,
        DateTimeOffset confirmedAtUtc,
        string messageId)
    {
        var confirmed = new TimeEntryContributorConfirmed(
            command.TimeEntryId,
            command.Contributor,
            tenant,
            confirmedAtUtc,
            command.Source);
        return new StreamReadEvent(
            sequence,
            typeof(StoredTimeEntryConfirmed).FullName!,
            JsonSerializer.SerializeToUtf8Bytes(new StoredTimeEntryConfirmed(confirmed), _jsonOptions),
            "json",
            1,
            $"event-confirm-{sequence}",
            messageId,
            messageId,
            confirmedAtUtc,
            "actor-1");
    }

    private static CommandStatusQueryResponse CompletedStatus(string messageId, long committedSequence)
        => new(messageId, nameof(CommandStatus.Completed), (int)CommandStatus.Completed, null, messageId)
        {
            TenantId = "tenant-1",
            Domain = "timesheets",
            AggregateId = "entry-1",
            EventCount = 1,
            CommittedEventSequence = committedSequence
        };

    private static StreamReadPage Page(StreamReadEvent[] events, long fromSequence, long? latestSequence = null)
    {
        long? last = events.Length > 0 ? events[^1].SequenceNumber : null;
        long latest = latestSequence ?? last ?? 0;
        return new StreamReadPage(
            "tenant-1",
            "timesheets",
            "entry-1",
            events,
            new StreamReadMetadata(fromSequence, null, last, latest, events.Length, false, null));
    }

    private static void AssertLogPrivacy(
        string message,
        TimesheetsRequestContext context,
        ConfirmExternalTimeEntry command)
    {
        if (context.Tenant is not null)
        {
            message.ShouldNotContain(context.Tenant.TenantId);
        }

        if (context.Actor is not null)
        {
            message.ShouldNotContain(context.Actor.PartyId);
        }

        message.ShouldNotContain(command.TimeEntryId.Value);
        message.ShouldNotContain(command.Contributor.PartyId);
        message.ShouldNotContain(command.Source.SourceSystem);
        message.ShouldNotContain(command.Source.ExternalRequestId);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<Entry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add(new Entry(logLevel, formatter(state, exception)));

        public sealed record Entry(LogLevel Level, string Message);
    }

    private sealed class ThrowingLogger : ILogger<TimeEntryConfirmationSubmissionService>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => throw new InvalidOperationException("Logger sink failure.");
    }
}
