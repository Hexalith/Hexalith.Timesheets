using System.Diagnostics;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Timesheets.Contracts.Commands.ExternalContributions;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.Policies;
using Hexalith.Timesheets.Server.TimeEntries;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class TimeEntryRecordSubmissionServiceTests
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task CompletedStatusRequiresMatchingStoredRecording(bool corruptReceipt, bool expected)
    {
        SubmitExternalTimeEntry command = Command();
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        string? messageId = null;
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                messageId = call.ArgAt<string>(1);
                return Task.FromResult<CommandStatusQueryResponse?>(Status(messageId));
            });
        TimeEntryRecorded recorded = Recorded(command) with
        {
            DurationMinutes = corruptReceipt ? 99 : command.DurationMinutes
        };
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(recorded, messageId!)));

        (await Service(gateway).SubmitAsync(Context(), command, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken)).ShouldBe(expected);
        await gateway.DidNotReceive().SubmitWorkloadCommandAsync(
            Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompletedRecordingRejectsCommentDecisionNotIssuedByServer()
    {
        SubmitExternalTimeEntry command = Command() with
        {
            Comment = new TimeEntryComment("private note", TimeEntryCommentPolicy.SensitiveDefault)
        };
        string? messageId = null;
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                messageId = call.ArgAt<string>(1);
                return Task.FromResult<CommandStatusQueryResponse?>(Status(messageId));
            });
        TimeEntryComment expected = new TimeEntryCommentPublicationIssuer()
            .Issue(Context(), command.Comment).ShouldNotBeNull();
        TimeEntryComment forged = new(expected.Text, expected.Policy with
        {
            ProjectionInclusion = TimesheetsCommentPolicyDecision.Allowed,
            ServerProjectionInclusion = TimesheetsCommentPolicyDecision.Allowed
        });
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(Recorded(command) with { Comment = forged }, messageId!)));

        (await Service(gateway).SubmitAsync(Context(), command, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken)).ShouldBeFalse();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(Recorded(command) with { Comment = expected }, messageId!)));
        (await Service(gateway).SubmitAsync(Context(), command, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task CompletedRecordingUsesPreSubmitCommentDecisionWhenAuthorityChanges()
    {
        SubmitExternalTimeEntry command = Command() with
        {
            Comment = new TimeEntryComment("private note", TimeEntryCommentPolicy.SensitiveDefault)
        };
        bool allowPublication = false;
        ITimeEntryCommentPublicationAuthority authority = Substitute.For<ITimeEntryCommentPublicationAuthority>();
        authority.MayPublish(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryComment>())
            .Returns(_ => allowPublication);
        var issuer = new TimeEntryCommentPublicationIssuer(authority);
        TimeEntryComment expected = issuer.Issue(Context(), command.Comment).ShouldNotBeNull();
        string? messageId = null;
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                allowPublication = true;
                messageId = call.ArgAt<string>(1);
                return Task.FromResult<CommandStatusQueryResponse?>(Status(messageId));
            });
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(Recorded(command) with { Comment = expected }, messageId!)));
        var service = new TimeEntryRecordSubmissionService(gateway,
            new MagicLinkDurableSubmissionService(gateway, Options.Create(new MagicLinkSubmissionPollingOptions())),
            ExternalContributionPolicyOptions.Default, issuer);

        (await service.SubmitAsync(Context(), command, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null, false, true)]
    [InlineData(TimesheetsCommentPolicyDecision.Allowed, false, true)]
    [InlineData(TimesheetsCommentPolicyDecision.Allowed, true, false)]
    [InlineData(TimesheetsCommentPolicyDecision.PolicyRequired, false, false)]
    public async Task ExistingRecordingUsesOnlyValidRetainedFinanceDecisions(
        TimesheetsCommentPolicyDecision? retainedDecision, bool requiresExportRedaction, bool expected)
    {
        SubmitExternalTimeEntry command = Command() with
        {
            Comment = new TimeEntryComment("private note", TimeEntryCommentPolicy.SensitiveDefault with
            {
                ExportInclusion = TimesheetsCommentPolicyDecision.Allowed,
                RedactionRequirement = requiresExportRedaction ? TimesheetsCommentRedactionRequirement.RequiredBeforeExport
                    : TimesheetsCommentRedactionRequirement.RequiredBeforeExternalDisclosure
            })
        };
        var issued = new TimeEntryCommentPublicationIssuer().Issue(Context(), command.Comment).ShouldNotBeNull();
        var stored = new TimeEntryComment(issued.Text, issued.Policy with
        {
            ServerPublicationVersion = retainedDecision is null ? 2 : 3,
            ServerExportInclusion = retainedDecision
        });
        string? messageId = null;
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                messageId = call.ArgAt<string>(1);
                return Task.FromResult<CommandStatusQueryResponse?>(Status(messageId));
            });
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(Recorded(command) with { Comment = stored }, messageId!)));

        var authority = Substitute.For<ITimeEntryCommentPublicationAuthority>();
        authority.MayExportUnredacted(Arg.Any<TimesheetsRequestContext>(), Arg.Any<string>())
            .Returns(_ => throw new InvalidOperationException("Finance authority is unavailable."));
        var service = new TimeEntryRecordSubmissionService(gateway,
            new MagicLinkDurableSubmissionService(gateway, Options.Create(new MagicLinkSubmissionPollingOptions())),
            ExternalContributionPolicyOptions.Default, new TimeEntryCommentPublicationIssuer(authority));
        (await service.SubmitAsync(Context(), command, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken)).ShouldBe(expected);
        authority.DidNotReceive().MayExportUnredacted(Arg.Any<TimesheetsRequestContext>(), Arg.Any<string>());
        await gateway.DidNotReceive().SubmitWorkloadCommandAsync(
            Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("", "event-message-2")]
    [InlineData("event-message-1", "event-message-1")]
    [InlineData("event-message-1", " ")]
    public async Task CompletedTwoEventRecordingRequiresDistinctNonblankEventIds(
        string firstId, string secondId)
    {
        SubmitExternalTimeEntry command = Command();
        string? messageId = null;
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                messageId = call.ArgAt<string>(1);
                return Task.FromResult<CommandStatusQueryResponse?>(Status(messageId) with
                {
                    EventCount = 2,
                    CommittedEventSequence = 2
                });
            });
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(Recorded(command), messageId!,
                Submitted(command, DateTimeOffset.UnixEpoch), recordedMessageId: firstId,
                submittedMessageId: secondId)));

        (await Service(gateway, new ExternalContributionPolicyOptions
        {
            InitialApprovalState = TimeEntryApprovalState.Submitted
        }).SubmitAsync(Context(), command, DateTimeOffset.UnixEpoch,
            TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("record-correlation")]
    [InlineData("submitted-at")]
    [InlineData("submission-correlation")]
    public async Task CompletedStatusRequiresExactRecordAndSubmissionReceipt(string mismatch)
    {
        SubmitExternalTimeEntry command = Command();
        DateTimeOffset submittedAtUtc = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
        string? messageId = null;
        int statusQueries = 0;
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                messageId = call.ArgAt<string>(1);
                return Task.FromResult<CommandStatusQueryResponse?>(++statusQueries == 1 ? null : Status(messageId) with
                {
                    EventCount = 2,
                    CommittedEventSequence = 2
                });
            });
        gateway.SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                SubmitCommandRequest request = call.ArgAt<SubmitCommandRequest>(0);
                return Task.FromResult(new SubmitCommandResponse(request.CorrelationId!, MessageId: request.MessageId));
            });
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(
                Recorded(command) with
                {
                    ActivityTypeScope = mismatch == "scope" ? ActivityTypeScope.Project : ActivityTypeScope.Tenant
                },
                messageId!,
                Submitted(command, submittedAtUtc.AddMinutes(mismatch == "submitted-at" ? 1 : 0)),
                mismatch == "record-correlation" ? "other-command" : null,
                mismatch == "submission-correlation" ? "other-command" : null)));

        (await Service(gateway, new ExternalContributionPolicyOptions
        {
            InitialApprovalState = TimeEntryApprovalState.Submitted
        }).SubmitAsync(Context(), command, submittedAtUtc, TestContext.Current.CancellationToken)).ShouldBeFalse();
        await gateway.Received(1).SubmitWorkloadCommandAsync(
            Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DistinctTrustedActorRetryHasDistinctCommandIdentity()
    {
        SubmitExternalTimeEntry command = Command();
        List<string> queriedIds = [];
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                string id = call.ArgAt<string>(1);
                queriedIds.Add(id);
                return Task.FromResult<CommandStatusQueryResponse?>(queriedIds.Count == 1
                    ? Status(id)
                    : Status(id) with { Status = nameof(CommandStatus.Rejected), StatusCode = (int)CommandStatus.Rejected });
            });
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(Recorded(command), queriedIds[0])));

        (await Service(gateway).SubmitAsync(Context(), command, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken)).ShouldBeTrue();
        TimesheetsRequestContext otherActor = new(Context().Tenant, new PartyReference("contributor-2"), "correlation-2");
        (await Service(gateway).SubmitAsync(otherActor, command, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken)).ShouldBeFalse();
        queriedIds.Count.ShouldBe(2);
        queriedIds[0].ShouldNotBe(queriedIds[1]);
    }

    [Fact]
    public async Task NewlineBearingSourceFieldsHaveDistinctCommandIdentities()
    {
        List<string> queriedIds = [];
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                string id = call.ArgAt<string>(1);
                queriedIds.Add(id);
                return Task.FromResult<CommandStatusQueryResponse?>(Status(id) with
                {
                    Status = nameof(CommandStatus.Rejected), StatusCode = (int)CommandStatus.Rejected
                });
            });

        (await Service(gateway).SubmitAsync(Context(), Command() with
        {
            Source = new ExternalContributionSource("a\nb", "c")
        }, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await Service(gateway).SubmitAsync(Context(), Command() with
        {
            Source = new ExternalContributionSource("a", "b\nc")
        }, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken)).ShouldBeFalse();
        queriedIds.Count.ShouldBe(2);
        queriedIds[0].ShouldNotBe(queriedIds[1]);
    }

    [Fact]
    public async Task UncertainStatusDeniesWithoutReadbackOrDuplicateSubmission()
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<CommandStatusQueryResponse?>(Status(call.ArgAt<string>(1)) with
            {
                Status = nameof(CommandStatus.PublishFailed),
                StatusCode = (int)CommandStatus.PublishFailed
            }));

        (await Service(gateway).SubmitAsync(Context(), Command(), DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken)).ShouldBeFalse();
        await gateway.DidNotReceive().SubmitWorkloadCommandAsync(
            Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
        await gateway.DidNotReceive().ReadWorkloadStreamAsync(
            Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false, "completed", true)]
    [InlineData(true, "completed", true)]
    [InlineData(false, "receipt-mismatch", false)]
    [InlineData(true, "timestamp-mismatch", false)]
    [InlineData(false, "rejected", false)]
    [InlineData(false, "publish-failed", false)]
    [InlineData(false, "transient-status", true)]
    [InlineData(false, "status-denied", false)]
    [InlineData(false, "delayed", true)]
    [InlineData(false, "transport-timeout", true)]
    public async Task LostSubmitResponseRequiresCompletedStatusAndExactStoredRecording(
        bool submitForApproval, string outcome, bool expected)
    {
        SubmitExternalTimeEntry command = Command();
        DateTimeOffset submittedAtUtc = DateTimeOffset.UnixEpoch;
        string? messageId = null;
        int statusQueries = 0;
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        CommandStatus terminalStatus = outcome switch
        {
            "rejected" => CommandStatus.Rejected,
            "publish-failed" => CommandStatus.PublishFailed,
            _ => CommandStatus.Completed
        };
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                messageId = call.ArgAt<string>(1);
                statusQueries++;
                if (statusQueries == 2 && outcome is "transient-status" or "status-denied")
                {
                    return Task.FromException<CommandStatusQueryResponse?>(new EventStoreGatewayException(
                        outcome == "transient-status" ? 503 : 403, "Status query unavailable"));
                }

                if (statusQueries == 1 || (outcome == "delayed" && statusQueries == 2))
                {
                    return Task.FromResult<CommandStatusQueryResponse?>(null);
                }

                CommandStatus observedStatus = outcome == "delayed" && statusQueries == 3
                    ? CommandStatus.Processing : terminalStatus;
                return Task.FromResult<CommandStatusQueryResponse?>(Status(messageId) with
                {
                    Status = observedStatus.ToString(),
                    StatusCode = (int)observedStatus,
                    EventCount = submitForApproval ? 2 : 1,
                    CommittedEventSequence = submitForApproval ? 2 : 1
                });
            });
        gateway.SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(outcome == "transport-timeout"
                ? Task.FromCanceled<SubmitCommandResponse>(new CancellationToken(true))
                : Task.FromException<SubmitCommandResponse>(new HttpRequestException("submit response lost")));
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(Recorded(command) with
            {
                DurationMinutes = outcome == "receipt-mismatch" ? 99 : command.DurationMinutes
            }, messageId!, submitForApproval
                ? Submitted(command, submittedAtUtc.AddMinutes(outcome == "timestamp-mismatch" ? 1 : 0))
                : null)));

        (await Service(gateway, new ExternalContributionPolicyOptions
        {
            InitialApprovalState = submitForApproval ? TimeEntryApprovalState.Submitted : TimeEntryApprovalState.Draft
        }).SubmitAsync(Context(), command, submittedAtUtc, TestContext.Current.CancellationToken)).ShouldBe(expected);
        statusQueries.ShouldBe(outcome switch
        {
            "transient-status" => 3,
            "delayed" => 4,
            _ => 2
        });
        await gateway.Received(1).SubmitWorkloadCommandAsync(
            Arg.Is<SubmitCommandRequest>(request => request.MessageId == messageId && request.CorrelationId == messageId),
            Arg.Any<CancellationToken>());
        await gateway.Received(terminalStatus == CommandStatus.Completed && outcome != "status-denied" ? 1 : 0).ReadWorkloadStreamAsync(
            Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LostSubmitResponseWithoutTerminalStatusDeniesWithinPollingDeadline()
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        CancellationToken submissionToken = default;
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CommandStatusQueryResponse?>(null));
        gateway.SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                submissionToken = call.ArgAt<CancellationToken>(1);
                await Task.Delay(TimeSpan.FromMilliseconds(800), submissionToken);
                throw new HttpRequestException("submit response lost");
            });

        Stopwatch elapsed = Stopwatch.StartNew();
        Task<bool> outcome = Service(gateway, polling: new MagicLinkSubmissionPollingOptions
        {
            Timeout = TimeSpan.FromSeconds(1),
            PollInterval = TimeSpan.FromMilliseconds(10)
        }).SubmitAsync(Context(), Command(), DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);

        (await outcome.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).ShouldBeFalse();
        elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromMilliseconds(1_500));
        submissionToken.IsCancellationRequested.ShouldBeTrue();
        await gateway.Received(1).SubmitWorkloadCommandAsync(
            Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
        await gateway.DidNotReceive().ReadWorkloadStreamAsync(
            Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordingSubmitCallerCancellationPropagatesWithoutReadback()
    {
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        var logger = new RecordingOutcomeTestLogger();
        CancellationToken transportToken = default;
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CommandStatusQueryResponse?>(null));
        gateway.SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                transportToken = call.ArgAt<CancellationToken>(1);
                caller.Cancel();
                return Task.FromCanceled<SubmitCommandResponse>(caller.Token);
            });

        await Should.ThrowAsync<OperationCanceledException>(() => Service(gateway, logger: logger).SubmitAsync(
            Context(), Command(), DateTimeOffset.UnixEpoch, caller.Token));
        transportToken.IsCancellationRequested.ShouldBeTrue();
        logger.Messages.ShouldBeEmpty();
        await gateway.Received(1).GetWorkloadCommandStatusAsync(
            "tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>());
        await gateway.DidNotReceive().ReadWorkloadStreamAsync(
            Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordingSubmitMismatchedAcceptedIdentityDeniesWithoutReadback()
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<CommandStatusQueryResponse?>(null),
                call => Task.FromResult<CommandStatusQueryResponse?>(Status(call.ArgAt<string>(1))));
        gateway.SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SubmitCommandResponse("other-command", MessageId: "other-command")));

        (await Service(gateway).SubmitAsync(Context(), Command(), DateTimeOffset.UnixEpoch,
            TestContext.Current.CancellationToken)).ShouldBeFalse();
        await gateway.Received(1).GetWorkloadCommandStatusAsync(
            "tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>());
        await gateway.DidNotReceive().ReadWorkloadStreamAsync(
            Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("stalled")]
    [InlineData("stale")]
    [InlineData("408")]
    [InlineData("429")]
    [InlineData("missing-stream")]
    [InlineData("501")]
    [InlineData("503")]
    [InlineData("http")]
    [InlineData("timeout")]
    [InlineData("canceled")]
    public async Task TransientReadbackRetriesAtPollIntervalWithoutResubmitting(string scenario)
    {
        var clock = new PollingTestTimeProvider();
        var logger = new RecordingOutcomeTestLogger();
        SubmitExternalTimeEntry command = Command();
        IEventStoreGatewayClient gateway = GatewayWithCompletedStatus(out string messageId, preflight: false);
        StreamReadPage committed = Page(Recorded(command), messageId);
        var stalled = new TaskCompletionSource<StreamReadPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken firstReadToken = default;
        int readCount = 0;
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                if (++readCount > 1)
                {
                    return Task.FromResult(committed);
                }

                firstReadToken = call.ArgAt<CancellationToken>(1);
                return scenario switch
                {
                    "stalled" => stalled.Task,
                    "stale" => Task.FromResult(committed with
                    {
                        Events = [],
                        Metadata = new StreamReadMetadata(0, null, null, 0, 0, false, null)
                    }),
                    "http" => Task.FromException<StreamReadPage>(new HttpRequestException("transient")),
                    "timeout" => Task.FromException<StreamReadPage>(new TimeoutException("transient")),
                    "canceled" => Task.FromCanceled<StreamReadPage>(new CancellationToken(true)),
                    "missing-stream" => Task.FromException<StreamReadPage>(new EventStoreGatewayException(404, "Not Found",
                        reasonCode: StreamReplayReasonCodes.MissingStream)),
                    _ => Task.FromException<StreamReadPage>(new EventStoreGatewayException(int.Parse(scenario), "transient"))
                };
            });

        Task<bool> pending = Service(gateway, polling: Polling(100, 25), logger: logger, clock: clock).SubmitAsync(
            Context(), command, DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);

        readCount.ShouldBe(1);
        if (scenario == "stalled")
        {
            firstReadToken.IsCancellationRequested.ShouldBeFalse();
            clock.Advance(TimeSpan.FromMilliseconds(25));
            firstReadToken.IsCancellationRequested.ShouldBeTrue();
        }

        await clock.WaitForPollAsync(TestContext.Current.CancellationToken);
        readCount.ShouldBe(1);
        clock.Advance(TimeSpan.FromMilliseconds(10));
        (await pending.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)).ShouldBeTrue();
        stalled.TrySetResult(committed);
        readCount.ShouldBe(2);
        logger.Messages.ShouldHaveSingleItem().ShouldContain("outcome=committed");
        await gateway.Received(1).SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReadbackStopsAtTheDeadlineStartedBeforeSubmissionAndUsesSnapshottedLimits()
    {
        var clock = new PollingTestTimeProvider();
        var logger = new RecordingOutcomeTestLogger();
        SubmitExternalTimeEntry command = Command();
        IEventStoreGatewayClient gateway = GatewayWithCompletedStatus(out string messageId, preflight: false);
        var accepted = new TaskCompletionSource<SubmitCommandResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>()).Returns(accepted.Task);
        var stalled = new TaskCompletionSource<StreamReadPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readStarted = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                readStarted.TrySetResult(call.ArgAt<CancellationToken>(1));
                return stalled.Task;
            });

        MagicLinkSubmissionPollingOptions limits = Polling(100, 250);
        TimeEntryRecordSubmissionService service = Service(gateway, polling: limits, logger: logger, clock: clock);
        limits.Timeout = TimeSpan.FromSeconds(5);
        limits.ReadbackTimeout = TimeSpan.FromMilliseconds(10);
        Task<bool> pending = service.SubmitAsync(Context(), command, DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(70));
        accepted.SetResult(new SubmitCommandResponse(messageId, MessageId: messageId));
        CancellationToken readToken = await readStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(29));
        pending.IsCompleted.ShouldBeFalse();
        readToken.IsCancellationRequested.ShouldBeFalse();
        clock.Advance(TimeSpan.FromMilliseconds(1));

        (await pending.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)).ShouldBeFalse();
        readToken.IsCancellationRequested.ShouldBeTrue();
        stalled.TrySetResult(Page(Recorded(command), messageId));
        logger.Messages.ShouldHaveSingleItem().ShouldContain("outcome=indeterminate-commit");
        await gateway.Received(1).SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("submit")]
    [InlineData("status")]
    [InlineData("readback")]
    public async Task CallerCancellationDuringGatewayWorkCancelsTransportWithoutLogging(string stage)
    {
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var logger = new RecordingOutcomeTestLogger();
        IEventStoreGatewayClient gateway = GatewayWithCompletedStatus(out string messageId, preflight: false);
        var transport = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stalledSubmit = new TaskCompletionSource<SubmitCommandResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stalledStatus = new TaskCompletionSource<CommandStatusQueryResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stalledRead = new TaskCompletionSource<StreamReadPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (stage == "submit")
        {
            gateway.SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    transport.TrySetResult(call.ArgAt<CancellationToken>(1));
                    return stalledSubmit.Task;
                });
        }
        else if (stage == "readback")
        {
            gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    transport.TrySetResult(call.ArgAt<CancellationToken>(1));
                    return stalledRead.Task;
                });
        }
        else
        {
            gateway.GetWorkloadCommandStatusAsync("tenant-1", messageId, Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromResult<CommandStatusQueryResponse?>(null), call =>
                {
                    transport.TrySetResult(call.ArgAt<CancellationToken>(2));
                    return stalledStatus.Task;
                });
        }

        Task<bool> pending = Service(gateway, logger: logger).SubmitAsync(Context(), Command(), DateTimeOffset.UnixEpoch, caller.Token);
        CancellationToken transportToken = await transport.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        caller.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        transportToken.IsCancellationRequested.ShouldBeTrue();
        stalledSubmit.TrySetResult(new SubmitCommandResponse(messageId, MessageId: messageId));
        stalledStatus.TrySetResult(null);
        stalledRead.TrySetResult(Page(Recorded(Command()), messageId));
        logger.Messages.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("missing-tenant")]
    [InlineData("missing-actor")]
    [InlineData("missing-entry")]
    [InlineData("missing-target")]
    [InlineData("missing-contributor")]
    [InlineData("missing-activity")]
    [InlineData("missing-source")]
    [InlineData("default-service-date")]
    [InlineData("zero-duration")]
    [InlineData("negative-duration")]
    [InlineData("unknown-billable-state")]
    [InlineData("undefined-billable-state")]
    [InlineData("default-instant")]
    [InlineData("non-utc-instant")]
    public async Task MissingIdentityAndInvalidInputLogARefusalWithoutGatewayCalls(string scenario)
    {
        TimesheetsRequestContext context = Context();
        SubmitExternalTimeEntry command = Command();
        DateTimeOffset utc = DateTimeOffset.UnixEpoch;
        switch (scenario)
        {
            case "missing-tenant": context = context with { Tenant = null }; break;
            case "missing-actor": context = context with { Actor = null }; break;
            case "missing-entry": command = command with { TimeEntryId = null! }; break;
            case "missing-target": command = command with { Target = null! }; break;
            case "missing-contributor": command = command with { Contributor = null! }; break;
            case "missing-activity": command = command with { ActivityTypeId = null! }; break;
            case "missing-source": command = command with { Source = null! }; break;
            case "default-service-date": command = command with { ServiceDate = default }; break;
            case "zero-duration": command = command with { DurationMinutes = 0 }; break;
            case "negative-duration": command = command with { DurationMinutes = -1 }; break;
            case "unknown-billable-state": command = command with { BillableState = BillableState.Unknown }; break;
            case "undefined-billable-state": command = command with { BillableState = (BillableState)99 }; break;
            case "default-instant": utc = default; break;
            case "non-utc-instant": utc = utc.ToOffset(TimeSpan.FromHours(1)); break;
        }

        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        var logger = new RecordingOutcomeTestLogger();
        using Activity activity = new Activity("recording").Start();

        (await Service(gateway, logger: logger).SubmitAsync(context, command, utc, TestContext.Current.CancellationToken)).ShouldBeFalse();

        string message = logger.Messages.ShouldHaveSingleItem();
        message.ShouldContain("outcome=policy-refusal");
        message.ShouldContain($"trace={activity.TraceId.ToHexString()}");
        gateway.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task PublicationAuthorityFailureIsAPolicyRefusalBeforeGatewayWork()
    {
        SubmitExternalTimeEntry command = Command() with
        {
            Comment = new TimeEntryComment("private note", TimeEntryCommentPolicy.SensitiveDefault)
        };
        ITimeEntryCommentPublicationAuthority authority = Substitute.For<ITimeEntryCommentPublicationAuthority>();
        authority.MayPublish(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryComment>())
            .Returns(_ => throw new InvalidOperationException("Private authority failure"));
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        var logger = new RecordingOutcomeTestLogger();
        var service = new TimeEntryRecordSubmissionService(gateway,
            new MagicLinkDurableSubmissionService(gateway, Options.Create(new MagicLinkSubmissionPollingOptions())),
            ExternalContributionPolicyOptions.Default, new TimeEntryCommentPublicationIssuer(authority), logger: logger);

        (await service.SubmitAsync(Context(), command, DateTimeOffset.UnixEpoch,
            TestContext.Current.CancellationToken)).ShouldBeFalse();

        logger.Messages.ShouldHaveSingleItem().ShouldContain("outcome=policy-refusal");
        logger.Entries.ShouldHaveSingleItem().Exception.ShouldBeNull();
        gateway.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("domain-rejection", "policy-refusal")]
    [InlineData("infra-rejection", "indeterminate-commit")]
    [InlineData("wrong-message", "indeterminate-commit")]
    [InlineData("wrong-correlation", "indeterminate-commit")]
    [InlineData("wrong-tenant", "indeterminate-commit")]
    [InlineData("wrong-domain", "indeterminate-commit")]
    [InlineData("wrong-owner", "indeterminate-commit")]
    [InlineData("publish-failed", "indeterminate-commit")]
    [InlineData("timed-out", "indeterminate-commit")]
    public async Task RejectedStatusIsAPolicyRefusalOnlyWhenAuthoritative(string scenario, string outcome)
    {
        IEventStoreGatewayClient gateway = GatewayWithCompletedStatus(out string messageId);
        CommandStatus terminal = scenario switch
        {
            "publish-failed" => CommandStatus.PublishFailed,
            "timed-out" => CommandStatus.TimedOut,
            _ => CommandStatus.Rejected
        };
        gateway.GetWorkloadCommandStatusAsync("tenant-1", messageId, Arg.Any<CancellationToken>())
            .Returns(Status(messageId) with
            {
                Status = terminal.ToString(),
                StatusCode = (int)terminal,
                RejectionEventType = scenario == "infra-rejection" ? null : "private-domain-failure",
                FailureReason = scenario == "infra-rejection" ? "private-infra-failure" : null,
                MessageId = scenario == "wrong-message" ? "other-message" : messageId,
                CorrelationId = scenario == "wrong-correlation" ? "other-correlation" : messageId,
                TenantId = scenario == "wrong-tenant" ? "other-tenant" : "tenant-1",
                Domain = scenario == "wrong-domain" ? "other-domain" : "timesheets",
                AggregateId = scenario == "wrong-owner" ? "other-owner" : "entry-1"
            });
        var logger = new RecordingOutcomeTestLogger();
        using Activity activity = new Activity("recording").Start();

        (await Service(gateway, logger: logger).SubmitAsync(Context(), Command(), DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken))
            .ShouldBeFalse();

        string message = logger.Messages.ShouldHaveSingleItem();
        message.ShouldContain($"outcome={outcome} trace={activity.TraceId.ToHexString()}");
        foreach (string sensitive in new[] { "tenant-1", "contributor-1", "entry-1", "request-1", "supplier", "private-", messageId })
        {
            message.ShouldNotContain(sensitive);
        }

        await gateway.DidNotReceive().SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
        await gateway.DidNotReceive().ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("missing-page-metadata")]
    [InlineData("wrong-page-range")]
    [InlineData("wrong-page-count")]
    [InlineData("wrong-latest-sequence")]
    [InlineData("wrong-truncation")]
    [InlineData("premature-end-of-stream")]
    [InlineData("unexpected-continuation")]
    [InlineData("unexpected-truncated-continuation")]
    [InlineData("wrong-causation")]
    [InlineData("wrong-metadata-version")]
    [InlineData("wrong-payload-version")]
    [InlineData("wrong-protection")]
    [InlineData("noncanonical-unprotected")]
    [InlineData("duplicate-json")]
    [InlineData("missing-required-json")]
    public async Task PreflightCompletedRequiresStrictVerifiedEvidenceAndDoesNotSubmit(string scenario)
    {
        SubmitExternalTimeEntry command = Command();
        IEventStoreGatewayClient gateway = GatewayWithCompletedStatus(out string messageId);
        StreamReadPage page = Page(Recorded(command), messageId);
        StreamReadEvent stored = page.Events[0];
        string json = Encoding.UTF8.GetString(stored.Payload);
        stored = scenario switch
        {
            "wrong-causation" => stored with { CausationId = "other-command" },
            "wrong-metadata-version" => stored with { MetadataVersion = 2 },
            "wrong-payload-version" => stored with { PayloadVersion = 2 },
            "wrong-protection" => stored with { ProtectionMetadata = EventStorePayloadProtectionMetadata.ProviderOpaque("test") },
            "noncanonical-unprotected" => stored with { ProtectionMetadata = EventStorePayloadProtectionMetadata.Unprotected() with { Scheme = "test" } },
            "duplicate-json" => stored with { Payload = Encoding.UTF8.GetBytes(json.Replace("\"durationMinutes\":60", "\"durationMinutes\":60,\"durationMinutes\":60", StringComparison.Ordinal)) },
            "missing-required-json" => stored with { Payload = Encoding.UTF8.GetBytes(json.Replace(",\"aiMetrics\":null", "", StringComparison.Ordinal)) },
            _ => stored with { PayloadVersion = 1, ProtectionMetadata = EventStorePayloadProtectionMetadata.Unprotected() }
        };
        page = page with
        {
            Events = [stored],
            Metadata = scenario switch
            {
                "missing-page-metadata" => null!,
                "wrong-page-range" => page.Metadata with { FromSequence = 1 },
                "wrong-page-count" => page.Metadata with { EventCount = 0 },
                "wrong-latest-sequence" => page.Metadata with { LatestSequence = 0 },
                "wrong-truncation" => page.Metadata with { IsTruncated = true },
                "premature-end-of-stream" => page.Metadata with { LatestSequence = 2 },
                "unexpected-continuation" => page.Metadata with { NextContinuationToken = new("unexpected") },
                "unexpected-truncated-continuation" => page.Metadata with
                {
                    IsTruncated = true, LatestSequence = 2, NextContinuationToken = new("unexpected")
                },
                _ => page.Metadata
            }
        };
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(page);
        var logger = new RecordingOutcomeTestLogger();

        bool accepted = await Service(gateway, logger: logger).SubmitAsync(Context(), command, DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);

        accepted.ShouldBe(scenario == "valid");
        logger.Messages.ShouldHaveSingleItem().ShouldContain(accepted ? "outcome=already-recorded" : "outcome=indeterminate-commit");
        await gateway.Received(1).ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
        await gateway.DidNotReceive().SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("restored-status", true, true)]
    [InlineData("missing-status", true, true)]
    [InlineData("unrelated-later-event", true, true)]
    [InlineData("lost-acknowledgement", false, false)]
    [InlineData("wrong-acknowledgement-correlation", false, false)]
    [InlineData("wrong-stored-actor", false, true)]
    [InlineData("extra-event-from-command", false, true)]
    [InlineData("extra-event-causation-only", false, true)]
    [InlineData("extra-event-correlation-only", false, true)]
    [InlineData("status-fault", false, false)]
    [InlineData("observed-processing", false, false)]
    [InlineData("processing-then-restored", true, true)]
    [InlineData("observed-received", false, false)]
    [InlineData("publish-failed-status", false, false)]
    [InlineData("publish-failed-status-after-readback", false, true)]
    public async Task RecoveryRequiresExactOwnerEvidenceAndRespectsObservedStatus(
        string scenario, bool expected, bool shouldRead)
    {
        SubmitExternalTimeEntry command = Command();
        IEventStoreGatewayClient gateway = GatewayWithCompletedStatus(out string messageId, preflight: false);
        if (scenario == "lost-acknowledgement")
        {
            gateway.SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException<SubmitCommandResponse>(new HttpRequestException("acknowledgement lost")));
        }
        else if (scenario == "wrong-acknowledgement-correlation")
        {
            gateway.SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
                .Returns(new SubmitCommandResponse("other-correlation", MessageId: messageId));
        }
        CommandStatusQueryResponse inProgress = Status(messageId) with
        {
            Status = nameof(CommandStatus.Received), StatusCode = (int)CommandStatus.Received,
            EventCount = null, CommittedEventSequence = null
        };
        int statusChecks = 0;
        gateway.GetWorkloadCommandStatusAsync("tenant-1", messageId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (++statusChecks == 1)
                {
                    return Task.FromResult<CommandStatusQueryResponse?>(null);
                }

                return scenario switch
                {
                    "status-fault" => Task.FromException<CommandStatusQueryResponse?>(new EventStoreGatewayException(503, "status unavailable")),
                    "observed-received" => Task.FromResult<CommandStatusQueryResponse?>(inProgress),
                    "observed-processing" or "processing-then-restored" when statusChecks == 2 => Task.FromResult<CommandStatusQueryResponse?>(inProgress with
                    {
                        Status = nameof(CommandStatus.Processing), StatusCode = (int)CommandStatus.Processing, Domain = "timesheets"
                    }),
                    "missing-status" => Task.FromResult<CommandStatusQueryResponse?>(null),
                    "publish-failed-status-after-readback" when statusChecks == 2 => Task.FromResult<CommandStatusQueryResponse?>(null),
                    "publish-failed-status" or "publish-failed-status-after-readback" => Task.FromResult<CommandStatusQueryResponse?>(inProgress with
                    {
                        Status = nameof(CommandStatus.PublishFailed), StatusCode = (int)CommandStatus.PublishFailed, Domain = "timesheets"
                    }),
                    _ when shouldRead => Task.FromResult<CommandStatusQueryResponse?>(Status(messageId) with { CommittedEventSequence = null }),
                    _ => Task.FromResult<CommandStatusQueryResponse?>(null)
                };
            });
        StreamReadEvent first = Page(Recorded(command), messageId).Events[0] with
        {
            UserId = scenario == "wrong-stored-actor" ? "another-actor" : Context().Actor!.PartyId
        };
        StreamReadEvent[] events = scenario switch
        {
            "extra-event-from-command" => [first, first with { SequenceNumber = 2, MessageId = "extra-event" }],
            "extra-event-causation-only" => [first, first with { SequenceNumber = 2, MessageId = "extra-event", CorrelationId = "another-command" }],
            "extra-event-correlation-only" => [first, first with { SequenceNumber = 2, MessageId = "extra-event", CausationId = "another-command" }],
            "unrelated-later-event" => [first, first with
            {
                SequenceNumber = 2, MessageId = "later-event", CausationId = "another-command", CorrelationId = "another-command"
            }],
            _ => [first]
        };
        StreamReadPage page = new("tenant-1", "timesheets", "entry-1", events,
            new StreamReadMetadata(0, null, events.Length, events.Length, events.Length, false, null));
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(page);
        var logger = new RecordingOutcomeTestLogger();

        bool accepted = await Service(gateway, polling: Polling(150, 50), logger: logger).SubmitAsync(
            Context(), command, DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);

        accepted.ShouldBe(expected);
        logger.Messages.ShouldHaveSingleItem().ShouldContain(expected ? "outcome=committed" : "outcome=indeterminate-commit");
        await gateway.Received(1).SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
        if (scenario is "missing-status" or "publish-failed-status-after-readback")
        {
            statusChecks.ShouldBe(3);
        }

        if (shouldRead)
        {
            await gateway.Received().ReadWorkloadStreamAsync(
                Arg.Is<StreamReadRequest>(request => request.FromSequence == 0 && request.ToSequence == null && request.PageSize == 100),
                Arg.Any<CancellationToken>());
        }
        else
        {
            await gateway.DidNotReceive().ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
        }
    }

    [Theory]
    [InlineData("stalled", false)]
    [InlineData("503", false)]
    [InlineData("missing-stream", false)]
    [InlineData("stale", false)]
    [InlineData("stalled", true)]
    [InlineData("503", true)]
    [InlineData("missing-stream", true)]
    [InlineData("stale", true)]
    public async Task RecoveryRetriesTransientOwnerReadsWithoutResubmitting(string scenario, bool restoredStatus)
    {
        var clock = new PollingTestTimeProvider();
        var logger = new RecordingOutcomeTestLogger();
        SubmitExternalTimeEntry command = Command();
        IEventStoreGatewayClient gateway = GatewayWithRecoveryStatus(restoredStatus, out string messageId);
        StreamReadPage committed = RecoveryPage(command, messageId);
        var stalled = new TaskCompletionSource<StreamReadPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken firstReadToken = default;
        int readCount = 0;
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (++readCount > 1)
            {
                return Task.FromResult(committed);
            }

            firstReadToken = call.ArgAt<CancellationToken>(1);
            return scenario switch
            {
                "stalled" => stalled.Task,
                "503" => Task.FromException<StreamReadPage>(new EventStoreGatewayException(503, "Unavailable")),
                "missing-stream" => Task.FromException<StreamReadPage>(new EventStoreGatewayException(404, "Not Found",
                    reasonCode: StreamReplayReasonCodes.MissingStream)),
                _ => Task.FromResult(committed with
                {
                    Events = [],
                    Metadata = new StreamReadMetadata(0, null, null, 0, 0, false, null)
                })
            };
        });

        CancellationToken caller = TestContext.Current.CancellationToken;
        Task<bool> pending = Service(gateway, polling: Polling(100, 25), logger: logger, clock: clock).SubmitAsync(
            Context(), command, DateTimeOffset.UnixEpoch, caller);

        readCount.ShouldBe(1);
        if (scenario == "stalled")
        {
            clock.Advance(TimeSpan.FromMilliseconds(24));
            firstReadToken.IsCancellationRequested.ShouldBeFalse();
            clock.Advance(TimeSpan.FromMilliseconds(1));
            firstReadToken.IsCancellationRequested.ShouldBeTrue();
        }

        await clock.WaitForPollAsync(caller);
        readCount.ShouldBe(1);
        clock.Advance(TimeSpan.FromMilliseconds(10));

        (await pending.WaitAsync(TimeSpan.FromSeconds(2), caller)).ShouldBeTrue();
        stalled.TrySetResult(committed);
        readCount.ShouldBe(2);
        logger.Messages.ShouldHaveSingleItem().ShouldContain("outcome=committed");
        await gateway.Received(1).SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("missing-metadata", false)]
    [InlineData("premature-end-of-stream", false)]
    [InlineData("sequence-gap", false)]
    [InlineData("missing-metadata", true)]
    [InlineData("premature-end-of-stream", true)]
    [InlineData("sequence-gap", true)]
    public async Task RecoveryRefusesMalformedOwnerPages(string scenario, bool restoredStatus)
    {
        SubmitExternalTimeEntry command = Command();
        IEventStoreGatewayClient gateway = GatewayWithRecoveryStatus(restoredStatus, out string messageId);
        StreamReadPage page = RecoveryPage(command, messageId);
        page = scenario switch
        {
            "missing-metadata" => page with { Metadata = null! },
            "sequence-gap" => page with
            {
                Events = [page.Events[0] with { SequenceNumber = 2 }],
                Metadata = page.Metadata with { LastSequenceReturned = 2, LatestSequence = 2 }
            },
            _ => page with { Metadata = page.Metadata with { LatestSequence = 2 } }
        };
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(page);
        var logger = new RecordingOutcomeTestLogger();

        (await Service(gateway, logger: logger).SubmitAsync(Context(), command, DateTimeOffset.UnixEpoch,
            TestContext.Current.CancellationToken)).ShouldBeFalse();

        logger.Messages.ShouldHaveSingleItem().ShouldContain("outcome=indeterminate-commit");
        await gateway.Received(1).ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
        await gateway.Received(1).SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MissingStatusRecoveryStopsImmediatelyOnDefinitiveOwnerMismatch()
    {
        var clock = new PollingTestTimeProvider();
        SubmitExternalTimeEntry command = Command();
        IEventStoreGatewayClient gateway = GatewayWithRecoveryStatus(restoredStatus: false, out string messageId);
        StreamReadPage page = RecoveryPage(command, messageId);
        page = page with { Events = [page.Events[0] with { UserId = "another-actor" }] };
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(page);
        var logger = new RecordingOutcomeTestLogger();

        Task<bool> pending = Service(gateway, polling: Polling(5_000, 50), logger: logger, clock: clock)
            .SubmitAsync(Context(), command, DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);

        (await pending.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)).ShouldBeFalse();
        logger.Messages.ShouldHaveSingleItem().ShouldContain("outcome=indeterminate-commit");
        await gateway.Received(1).ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
        await gateway.Received(1).SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompletedSequenceReadbackFailsFastOnGappedNonTruncatedPage()
    {
        var clock = new PollingTestTimeProvider();
        SubmitExternalTimeEntry command = Command();
        IEventStoreGatewayClient gateway = GatewayWithCompletedStatus(out string messageId);
        gateway.GetWorkloadCommandStatusAsync("tenant-1", messageId, Arg.Any<CancellationToken>())
            .Returns(Status(messageId) with { EventCount = 2, CommittedEventSequence = 2 });
        StreamReadEvent secondOnly = Page(Recorded(command), messageId, Submitted(command, DateTimeOffset.UnixEpoch)).Events[1];
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(new StreamReadPage("tenant-1", "timesheets", "entry-1", [secondOnly],
                new StreamReadMetadata(0, null, 2, 2, 1, false, null)));
        var logger = new RecordingOutcomeTestLogger();
        var policy = new ExternalContributionPolicyOptions { InitialApprovalState = TimeEntryApprovalState.Submitted };

        Task<bool> pending = Service(gateway, policy: policy, polling: Polling(5_000, 50), logger: logger, clock: clock)
            .SubmitAsync(Context(), command, DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);

        (await pending.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)).ShouldBeFalse();
        logger.Messages.ShouldHaveSingleItem().ShouldContain("outcome=indeterminate-commit");
        await gateway.Received(1).ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestoredStatusRecoveryRequiresConsistentLookaheadWhenRecordingEndsFirstPage(bool regressedHead)
    {
        SubmitExternalTimeEntry command = Command();
        IEventStoreGatewayClient gateway = GatewayWithRecoveryStatus(restoredStatus: true, out string messageId);
        StreamReadEvent recorded = RecoveryPage(command, messageId).Events[0];
        StreamReadEvent LaterOrRejectedEvent(int sequence) => recorded with
        {
            SequenceNumber = sequence, EventTypeName = typeof(MagicLinkCommitRejected).FullName!,
            MessageId = $"event-{sequence}", CorrelationId = $"command-{sequence}", CausationId = $"command-{sequence}"
        };
        StreamReadEvent[] firstPage = [.. Enumerable.Range(1, 99).Select(LaterOrRejectedEvent), recorded with { SequenceNumber = 100 }];
        gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(request => request.FromSequence == 0), Arg.Any<CancellationToken>())
            .Returns(new StreamReadPage("tenant-1", "timesheets", "entry-1", firstPage,
                new StreamReadMetadata(0, null, 100, 101, 100, true, null)));
        gateway.ReadWorkloadStreamAsync(Arg.Is<StreamReadRequest>(request => request.FromSequence == 100), Arg.Any<CancellationToken>())
            .Returns(regressedHead
                ? new StreamReadPage("tenant-1", "timesheets", "entry-1", [],
                    new StreamReadMetadata(100, null, null, 100, 0, false, null))
                : new StreamReadPage("tenant-1", "timesheets", "entry-1", [LaterOrRejectedEvent(101)],
                    new StreamReadMetadata(100, null, 101, 101, 1, false, null)));

        (await Service(gateway).SubmitAsync(Context(), command, DateTimeOffset.UnixEpoch,
            TestContext.Current.CancellationToken)).ShouldBe(!regressedHead);

        await gateway.Received(1).ReadWorkloadStreamAsync(
            Arg.Is<StreamReadRequest>(request => request.FromSequence == 100 && request.PageSize == 100), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(1, 0, 64)]
    [InlineData(100, 0, 51)]
    [InlineData(1, 8 * 1024 * 1024, 3)]
    public async Task RecoveryRefusesReadsBeyondPageEventOrPayloadAllowance(int eventsPerPage, int bytesPerEvent, int reads)
    {
        SubmitExternalTimeEntry command = Command();
        IEventStoreGatewayClient gateway = GatewayWithRecoveryStatus(restoredStatus: true, out string messageId);
        StreamReadEvent unrelated = RecoveryPage(command, messageId).Events[0] with
        {
            CorrelationId = "previous-command",
            CausationId = "previous-command",
            Payload = new byte[bytesPerEvent]
        };
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            long start = call.ArgAt<StreamReadRequest>(0).FromSequence;
            StreamReadEvent[] events = [.. Enumerable.Range(1, eventsPerPage).Select(offset => unrelated with
            {
                SequenceNumber = start + offset
            })];
            return Task.FromResult(new StreamReadPage("tenant-1", "timesheets", "entry-1", events,
                new StreamReadMetadata(start, null, start + events.Length, 10_001, events.Length, true, null)));
        });
        var logger = new RecordingOutcomeTestLogger();

        (await Service(gateway, logger: logger, clock: new PollingTestTimeProvider()).SubmitAsync(
            Context(), command, DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken)).ShouldBeFalse();

        logger.Messages.ShouldHaveSingleItem().ShouldContain("outcome=indeterminate-commit");
        await gateway.Received(reads).ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("completed", true, 0)]
    [InlineData("missing", true, 1)]
    [InlineData("unavailable", false, 0)]
    public async Task RecordingRetriesTransientPreflightBeforeDecidingToSubmit(string scenario, bool accepted, int submits)
    {
        var clock = new PollingTestTimeProvider();
        SubmitExternalTimeEntry command = Command();
        IEventStoreGatewayClient gateway = GatewayWithCompletedStatus(out string messageId, preflight: false);
        int statusChecks = 0;
        gateway.GetWorkloadCommandStatusAsync("tenant-1", messageId, Arg.Any<CancellationToken>())
            .Returns(_ => ++statusChecks == 1 || scenario == "unavailable"
                ? Task.FromException<CommandStatusQueryResponse?>(new EventStoreGatewayException(503, "status unavailable"))
                : Task.FromResult<CommandStatusQueryResponse?>(scenario == "missing" && statusChecks == 2 ? null : Status(messageId)));
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(Page(Recorded(command), messageId));

        Task<bool> pending = Service(gateway, polling: Polling(100, 50), clock: clock).SubmitAsync(
            Context(), command, DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);
        await clock.WaitForPollAsync(TestContext.Current.CancellationToken);
        await gateway.DidNotReceive().SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
        clock.Advance(TimeSpan.FromMilliseconds(scenario == "unavailable" ? 100 : 10));

        (await pending.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)).ShouldBe(accepted);
        await gateway.Received(submits).SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompletedStatusWithWrongCorrelationCannotAuthorizeReadback()
    {
        IEventStoreGatewayClient gateway = GatewayWithCompletedStatus(out string messageId);
        gateway.GetWorkloadCommandStatusAsync("tenant-1", messageId, Arg.Any<CancellationToken>())
            .Returns(Status(messageId) with { CorrelationId = "other-correlation" });
        var logger = new RecordingOutcomeTestLogger();

        (await Service(gateway, logger: logger).SubmitAsync(Context(), Command(), DateTimeOffset.UnixEpoch,
            TestContext.Current.CancellationToken)).ShouldBeFalse();

        logger.Messages.ShouldHaveSingleItem().ShouldContain("outcome=indeterminate-commit");
        await gateway.DidNotReceive().ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
        await gateway.DidNotReceive().SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TransportFailureIsOmittedFromStructuredRecordingDiagnostics()
    {
        const string sensitive = "private provider credential and command body";
        IEventStoreGatewayClient gateway = GatewayWithCompletedStatus(out _);
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<StreamReadPage>(new EventStoreGatewayException(404, sensitive,
                reasonCode: "unrelated", innerException: new HttpRequestException(sensitive))));
        var logger = new RecordingOutcomeTestLogger();
        using Activity activity = new Activity("recording-transport").Start();

        (await Service(gateway, logger: logger).SubmitAsync(Context(), Command(), DateTimeOffset.UnixEpoch,
            TestContext.Current.CancellationToken)).ShouldBeFalse();

        var entry = logger.Entries.ShouldHaveSingleItem();
        entry.Exception.ShouldBeNull();
        entry.EventId.Id.ShouldBe(1404);
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Fields.Select(field => field.Key).ShouldBe(["Outcome", "TraceId", "{OriginalFormat}"], ignoreOrder: true);
        entry.Fields.Single(field => field.Key == "Outcome").Value.ShouldBe("indeterminate-commit");
        entry.Fields.Single(field => field.Key == "TraceId").Value.ShouldBe(activity.TraceId.ToHexString());
        entry.Fields.ShouldAllBe(field => !field.Value!.ToString()!.Contains(sensitive, StringComparison.Ordinal));
        logger.Messages.ShouldHaveSingleItem().ShouldNotContain(sensitive);
        await gateway.Received(1).ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("original", true)]
    [InlineData("same", true)]
    [InlineData("future", false)]
    [InlineData("default", false)]
    [InlineData("non-utc", false)]
    [InlineData("wrong-causation", false)]
    public async Task SubmittedReceiptWeighsItsOriginalUtcInstantAndIdentityAgainstTheRetry(string scenario, bool expected)
    {
        SubmitExternalTimeEntry command = Command();
        IEventStoreGatewayClient gateway = GatewayWithCompletedStatus(out string messageId, preflight: false, count: 2);
        DateTimeOffset requestedAt = DateTimeOffset.UnixEpoch.AddHours(1);
        DateTimeOffset storedAt = scenario switch
        {
            "original" => DateTimeOffset.UnixEpoch,
            "future" => requestedAt.AddMinutes(1),
            "default" => default,
            "non-utc" => requestedAt.ToOffset(TimeSpan.FromHours(1)),
            _ => requestedAt
        };
        StreamReadPage page = Page(Recorded(command), messageId, Submitted(command, storedAt));
        if (scenario == "wrong-causation")
        {
            page = page with { Events = [page.Events[0], page.Events[1] with { CausationId = "other-command" }] };
        }

        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(page);
        var policy = new ExternalContributionPolicyOptions { InitialApprovalState = TimeEntryApprovalState.Submitted };

        (await Service(gateway, policy).SubmitAsync(Context(), command, requestedAt, TestContext.Current.CancellationToken)).ShouldBe(expected);
    }

    [Fact]
    public async Task LoggingFailureDoesNotReplaceOutcome()
    {
        IEventStoreGatewayClient gateway = GatewayWithCompletedStatus(out string messageId);
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(Page(Recorded(Command()), messageId));
        var logger = new RecordingOutcomeTestLogger { ThrowOnWrite = true };
        TimeEntryRecordSubmissionService service = Service(gateway, logger: logger);

        (await service.SubmitAsync(Context(), Command(), DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await service.SubmitAsync(Context(), Command(), default, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    private static IEventStoreGatewayClient GatewayWithCompletedStatus(out string identity, bool preflight = true, int count = 1)
    {
        SubmitExternalTimeEntry command = Command();
        string messageId = MagicLinkDurableSubmissionService.CreateMessageId(JsonSerializer.Serialize(new[]
        {
            "record", "tenant-1", "contributor-1", command.TimeEntryId.Value, command.Source.SourceSystem, command.Source.ExternalRequestId
        }));
        identity = messageId;
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        CommandStatusQueryResponse status = Status(messageId) with { EventCount = count, CommittedEventSequence = count };
        int queries = 0;
        gateway.GetWorkloadCommandStatusAsync("tenant-1", messageId, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<CommandStatusQueryResponse?>(!preflight && ++queries == 1 ? null : status));
        gateway.SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitCommandResponse(messageId, MessageId: messageId));
        return gateway;
    }

    private static IEventStoreGatewayClient GatewayWithRecoveryStatus(bool restoredStatus, out string messageId)
    {
        IEventStoreGatewayClient gateway = GatewayWithCompletedStatus(out messageId, preflight: false);
        CommandStatusQueryResponse? restored = restoredStatus ? Status(messageId) with { CommittedEventSequence = null } : null;
        gateway.GetWorkloadCommandStatusAsync("tenant-1", messageId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CommandStatusQueryResponse?>(null), Task.FromResult(restored));
        return gateway;
    }

    private static StreamReadPage RecoveryPage(SubmitExternalTimeEntry command, string messageId)
    {
        StreamReadPage page = Page(Recorded(command), messageId);
        return page with { Events = [page.Events[0] with { UserId = Context().Actor!.PartyId }] };
    }

    private static MagicLinkSubmissionPollingOptions Polling(int timeoutMs, int readbackMs)
        => new()
        {
            Timeout = TimeSpan.FromMilliseconds(timeoutMs),
            ReadbackTimeout = TimeSpan.FromMilliseconds(readbackMs),
            PollInterval = TimeSpan.FromMilliseconds(10)
        };

    private static TimeEntryRecordSubmissionService Service(
        IEventStoreGatewayClient gateway, ExternalContributionPolicyOptions? policy = null,
        MagicLinkSubmissionPollingOptions? polling = null, ILogger<TimeEntryRecordSubmissionService>? logger = null,
        TimeProvider? clock = null)
        => new(gateway, new MagicLinkDurableSubmissionService(
            gateway, Options.Create(new MagicLinkSubmissionPollingOptions())),
            policy ?? ExternalContributionPolicyOptions.Default,
            pollingOptions: polling is null ? null : Options.Create(polling), logger: logger, clock: clock);

    private static TimesheetsRequestContext Context()
        => new(new TenantReference("tenant-1"), new PartyReference("contributor-1"), "correlation-1");

    private static SubmitExternalTimeEntry Command()
        => new(new TimeEntryId("entry-1"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            new PartyReference("contributor-1"), new ActivityTypeId("activity-1"),
            new DateOnly(2026, 10, 10), 60, BillableState.Billable,
            new ExternalContributionSource("supplier", "request-1"));

    private static TimeEntryRecorded Recorded(SubmitExternalTimeEntry command)
        => new(command.TimeEntryId, command.Target, command.Contributor, command.ActivityTypeId,
            ActivityTypeScope.Tenant, command.ServiceDate, command.DurationMinutes,
            command.BillableState, TimeEntryApprovalState.Draft, ContributorCategory.ExternalContributor, null)
        {
            ExternalSource = command.Source
        };

    private static CommandStatusQueryResponse Status(string messageId)
        => new(messageId, nameof(CommandStatus.Completed), (int)CommandStatus.Completed, null, messageId)
        {
            EventCount = 1,
            CommittedEventSequence = 1,
            TenantId = "tenant-1",
            Domain = "timesheets",
            AggregateId = "entry-1"
        };

    private static TimeEntrySubmitted Submitted(SubmitExternalTimeEntry command, DateTimeOffset submittedAtUtc)
        => new(command.TimeEntryId, Context().Actor!, Context().Tenant!, submittedAtUtc,
            new TimeEntrySubmissionId(command.Source.ExternalRequestId),
            TimeEntrySubmissionScope.SelectedEntries, TimeEntryApprovalState.Submitted);

    private static StreamReadPage Page(
        TimeEntryRecorded recorded, string messageId, TimeEntrySubmitted? submitted = null,
        string? recordedCorrelation = null, string? submittedCorrelation = null,
        string? recordedMessageId = null, string? submittedMessageId = null)
    {
        StoredTimeEntryRecorded stored = new(recorded);
        List<StreamReadEvent> events =
        [
            new(1, typeof(StoredTimeEntryRecorded).FullName!,
                JsonSerializer.SerializeToUtf8Bytes(stored, _jsonOptions), "json", 1,
                recordedMessageId ?? "event-message-1", recordedCorrelation ?? messageId, messageId, DateTimeOffset.UtcNow, "actor-1")
        ];
        if (submitted is not null)
        {
            events.Add(new StreamReadEvent(2, typeof(StoredTimeEntrySubmitted).FullName!,
                JsonSerializer.SerializeToUtf8Bytes(new StoredTimeEntrySubmitted(submitted), _jsonOptions), "json", 1,
                submittedMessageId ?? "event-message-2", submittedCorrelation ?? messageId, messageId, DateTimeOffset.UtcNow, "actor-1"));
        }
        return new StreamReadPage("tenant-1", "timesheets", "entry-1",
            events, new StreamReadMetadata(0, null, events.Count, events.Count, events.Count, false, null));
    }
}
