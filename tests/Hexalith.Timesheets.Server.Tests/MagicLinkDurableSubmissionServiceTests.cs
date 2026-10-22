using System.Diagnostics;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks.Commands;

using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using ServerMagicLinkCapabilityState = Hexalith.Timesheets.Server.MagicLinks.MagicLinkCapabilityState;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class MagicLinkDurableSubmissionServiceTests
{
    [Fact]
    public void Polling_options_are_bounded_and_default_to_five_seconds()
    {
        var defaults = new MagicLinkSubmissionPollingOptions();
        defaults.Timeout.ShouldBe(TimeSpan.FromSeconds(5));
        defaults.PollInterval.ShouldBe(TimeSpan.FromMilliseconds(100));
        defaults.IsValid().ShouldBeTrue();
        new MagicLinkSubmissionPollingOptions { Timeout = TimeSpan.Zero }.IsValid().ShouldBeFalse();
        new MagicLinkSubmissionPollingOptions { Timeout = TimeSpan.FromMinutes(1) }.IsValid().ShouldBeFalse();
        new MagicLinkSubmissionPollingOptions { PollInterval = TimeSpan.Zero }.IsValid().ShouldBeFalse();
        new MagicLinkSubmissionPollingOptions { PollInterval = TimeSpan.FromTicks(1) }.IsValid().ShouldBeFalse();
        new MagicLinkSubmissionPollingOptions { PollInterval = TimeSpan.FromMilliseconds(9) }.IsValid().ShouldBeFalse();
        new MagicLinkSubmissionPollingOptions { PollInterval = TimeSpan.FromMilliseconds(10) }.IsValid().ShouldBeTrue();
        new MagicLinkSubmissionPollingOptions { PollInterval = TimeSpan.FromSeconds(6) }.IsValid().ShouldBeFalse();
    }

    [Fact]
    public async Task Processing_status_is_polled_to_the_configured_deadline_and_denied()
    {
        IEventStoreGatewayClient gateway = GatewayWithAcceptedSubmission();
        int reads = 0;
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<CommandStatusQueryResponse?>(Interlocked.Increment(ref reads) == 1
                ? null
                : Status(CommandStatus.Processing)));
        var service = Service(gateway, 80, 10);
        Stopwatch elapsed = Stopwatch.StartNew();

        (await service.SubmitUseAsync(Intent(), "request-1", TestContext.Current.CancellationToken)).ShouldBeFalse();

        elapsed.Elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(70));
        elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));
        reads.ShouldBeGreaterThan(5);
        await gateway.Received(1).SubmitWorkloadCommandAsync(
            Arg.Is<SubmitCommandRequest>(request => request.IdempotencyKey == null),
            Arg.Any<CancellationToken>());
        await gateway.DidNotReceive().ReadWorkloadStreamAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Streams.StreamReadRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stalled_status_transport_submits_once_within_the_polling_deadline()
    {
        IEventStoreGatewayClient gateway = GatewayWithAcceptedSubmission();
        var stalled = new TaskCompletionSource<CommandStatusQueryResponse?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken transportToken = default;
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                transportToken = call.ArgAt<CancellationToken>(2);
                return stalled.Task;
            });
        Stopwatch elapsed = Stopwatch.StartNew();

        (await Service(gateway, 40, 10).SubmitUseAsync(
            Intent(), "request-1", TestContext.Current.CancellationToken)).ShouldBeFalse();

        elapsed.Elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(30));
        elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));
        transportToken.IsCancellationRequested.ShouldBeTrue();
        await gateway.Received(1).SubmitWorkloadCommandAsync(
            Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stalled_submission_receives_the_deadline_cancellation_token()
    {
        IEventStoreGatewayClient gateway = GatewayWithAcceptedSubmission();
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CommandStatusQueryResponse?>(null));
        CancellationToken transportToken = default;
        var stalled = new TaskCompletionSource<SubmitCommandResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                transportToken = call.ArgAt<CancellationToken>(1);
                return stalled.Task;
            });

        (await Service(gateway, 40, 10).SubmitUseAsync(
            Intent(), "request-1", TestContext.Current.CancellationToken)).ShouldBeFalse();

        transportToken.IsCancellationRequested.ShouldBeTrue();
    }

    [Theory]
    [InlineData(CommandStatus.Rejected)]
    [InlineData(CommandStatus.PublishFailed)]
    [InlineData(CommandStatus.TimedOut)]
    public async Task Terminal_non_completed_status_denies_without_readback(CommandStatus status)
    {
        IEventStoreGatewayClient gateway = GatewayWithAcceptedSubmission();
        int reads = 0;
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<CommandStatusQueryResponse?>(Interlocked.Increment(ref reads) == 1
                ? null : Status(status)));

        (await Service(gateway, 100, 10).SubmitUseAsync(
            Intent(), "request-1", TestContext.Current.CancellationToken)).ShouldBeFalse();

        reads.ShouldBe(2);
        await gateway.DidNotReceive().ReadWorkloadStreamAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Streams.StreamReadRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Existing_terminal_status_is_read_without_resubmitting()
    {
        IEventStoreGatewayClient gateway = GatewayWithAcceptedSubmission();
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CommandStatusQueryResponse?>(Status(CommandStatus.Rejected)));

        (await Service(gateway, 100, 10).SubmitUseAsync(
            Intent(), "request-1", TestContext.Current.CancellationToken)).ShouldBeFalse();

        await gateway.DidNotReceive().SubmitWorkloadCommandAsync(
            Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancellation_during_polling_propagates()
    {
        IEventStoreGatewayClient gateway = GatewayWithAcceptedSubmission();
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CommandStatusQueryResponse?>(Status(CommandStatus.Processing)));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));

        await Should.ThrowAsync<OperationCanceledException>(() => Service(gateway, 500, 10)
            .SubmitUseAsync(Intent(), "request-1", cancellation.Token));
    }

    [Fact]
    public async Task Gateway_fault_denies_and_retry_keeps_the_same_message_identity()
    {
        IEventStoreGatewayClient gateway = GatewayWithAcceptedSubmission();
        var submitted = new List<string>();
        gateway.SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                SubmitCommandRequest request = call.ArgAt<SubmitCommandRequest>(0);
                submitted.Add(request.MessageId);
                return Task.FromResult(new SubmitCommandResponse(request.MessageId, MessageId: request.MessageId));
            });
        int reads = 0;
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref reads) % 2 == 1
                ? Task.FromResult<CommandStatusQueryResponse?>(null)
                : Task.FromException<CommandStatusQueryResponse?>(new InvalidOperationException("gateway fault")));
        reads = 0;

        var service = Service(gateway, 500, 10);
        (await service.SubmitUseAsync(Intent(), "request-1", TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await service.SubmitUseAsync(Intent(), "request-1", TestContext.Current.CancellationToken)).ShouldBeFalse();

        submitted.Count.ShouldBe(2);
        reads.ShouldBe(4);
        submitted[0].ShouldBe(submitted[1]);
        submitted[0].ShouldNotBeNullOrWhiteSpace();
        submitted[0].Length.ShouldBe(26);
        submitted[0].ShouldNotContain("hash-1");
    }

    [Theory]
    [InlineData("correlation", null)]
    [InlineData("correlation", "foreign-command")]
    [InlineData("causation", null)]
    [InlineData("causation", "foreign-command")]
    public async Task UseReceiptRejectsMissingOrForeignCommandAttribution(string field, string? value)
    {
        CommitMagicLinkUse intent = Intent();
        IEventStoreGatewayClient gateway = ReceiptGateway(intent, stored => field == "correlation"
            ? stored with { CorrelationId = value! } : stored with { CausationId = value });
        (await Service(gateway, 500, 10).SubmitUseAsync(intent, "request", TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task ValidConfirmationReceiptRetainsSuccess()
    {
        CommitMagicLinkUse intent = Intent();
        (await Service(ReceiptGateway(intent), 500, 10).SubmitUseAsync(intent, "request", TestContext.Current.CancellationToken))
            .ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdjustmentWithoutTrustedPriorValuesIsRefusedBeforeSubmission(bool resolved)
    {
        CommitMagicLinkUse intent = AdjustmentIntent();
        IEventStoreGatewayClient gateway = ReceiptGateway(intent);
        MagicLinkDurableSubmissionService service = Service(gateway, 500, 10);
        bool accepted = resolved
            ? await service.SubmitResolvedUseAsync(ResolvedCapability(intent), intent.Action, intent.Adjustment,
                intent.UsedAtUtc, "request", TestContext.Current.CancellationToken)
            : await service.SubmitUseAsync(intent, "request", TestContext.Current.CancellationToken);
        accepted.ShouldBeFalse();
        await gateway.DidNotReceive().SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CommentedAdjustmentWithoutResolvedCapabilityIsRefusedBeforeSubmission()
    {
        CommitMagicLinkUse intent = AdjustmentIntent() with
        {
            Adjustment = AdjustmentIntent().Adjustment! with { Comment = new("private adjustment", TimeEntryCommentPolicy.SensitiveDefault) }
        };
        IEventStoreGatewayClient gateway = ReceiptGateway(intent);
        (await Service(gateway, 500, 10).SubmitUseAsync(intent, "request", TestContext.Current.CancellationToken, PriorValues()))
            .ShouldBeFalse();
        await gateway.DidNotReceive().SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnsupportedAdjustmentPreservesCallerCancellation(bool missingPrior)
    {
        CommitMagicLinkUse intent = AdjustmentIntent() with
        {
            Adjustment = AdjustmentIntent().Adjustment! with { Comment = new("private adjustment", TimeEntryCommentPolicy.SensitiveDefault) }
        };
        IEventStoreGatewayClient gateway = ReceiptGateway(intent);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => Service(gateway, 500, 10).SubmitUseAsync(
            intent, "request", cancellation.Token, missingPrior ? null : PriorValues()));
        await gateway.DidNotReceive().SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdjustmentReceiptUsesCapturedCompletePriorSnapshot(bool changedPrior)
    {
        CommitMagicLinkUse intent = AdjustmentIntent();
        IEventStoreGatewayClient gateway = ReceiptGateway(intent);
        TimeEntryCorrectionValues trusted = changedPrior ? PriorValues() with { DurationMinutes = 61 } : PriorValues();
        (await Service(gateway, 500, 10).SubmitUseAsync(intent, "request", TestContext.Current.CancellationToken, trusted))
            .ShouldBe(!changedPrior);
    }

    private static ServerMagicLinkCapabilityState ResolvedCapability(CommitMagicLinkUse intent)
    {
        var state = new ServerMagicLinkCapabilityState();
        state.Apply(new MagicLinkConfirmationCapabilityIssued(intent.CapabilityId, intent.Tenant,
            PriorValues().Contributor, PriorValues().Target, PriorValues().ActivityTypeId, intent.TimeEntryId,
            MagicLinkTargetKind.ProposedTimeEntry, MagicLinkAllowedAction.Adjust, intent.TokenHash,
            intent.UsedAtUtc.AddDays(1), new("issuer-1"), intent.UsedAtUtc.AddHours(-1), new("timesheets", "issue"), true));
        return state;
    }

    private static CommitMagicLinkUse AdjustmentIntent() => Intent() with
    {
        Action = MagicLinkUseAction.Adjust,
        Adjustment = new AdjustTimeThroughMagicLink(new(2026, 6, 20), 75, new("activity-2"), BillableState.NonBillable)
    };

    private static TimeEntryCorrectionValues PriorValues() => new(
        TimeEntryTargetReference.ForProject(new("project-1")), new("contributor-1"), new("activity-1"),
        new(2026, 6, 19), 60, BillableState.Billable, ContributorCategory.ExternalContributor, null)
        { ActivityTypeScope = ActivityTypeScope.Tenant };

    private static IEventStoreGatewayClient ReceiptGateway(CommitMagicLinkUse intent,
        Func<StreamReadEvent, StreamReadEvent>? transform = null)
    {
        IEventStoreGatewayClient gateway = GatewayWithAcceptedSubmission();
        string? messageId = null;
        StreamReadEvent[] events = [];
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<CommandStatusQueryResponse?>(messageId is null ? null
                : new(messageId, nameof(CommandStatus.Completed), (int)CommandStatus.Completed, MessageId: messageId)
                { CommittedEventSequence = 3, EventCount = 2, TenantId = "tenant-1", Domain = "timesheets", AggregateId = intent.TimeEntryId.Value }));
        gateway.SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                messageId = call.ArgAt<SubmitCommandRequest>(0).MessageId;
                var contributor = new PartyReference("contributor-1");
                var used = new MagicLinkConfirmationCapabilityUsed(intent.CapabilityId, intent.Tenant, contributor,
                    intent.TimeEntryId, intent.UsedAtUtc, new("magic-link", intent.CapabilityId.Value))
                    { OutcomeCategory = intent.Action == MagicLinkUseAction.Confirm ? "confirmed" : "adjusted" };
                object effect = intent.Action == MagicLinkUseAction.Confirm
                    ? new TimeEntryContributorConfirmed(intent.TimeEntryId, contributor, intent.Tenant, intent.UsedAtUtc,
                        new("magic-link", intent.CapabilityId.Value))
                    : new TimeEntryAdjustedThroughMagicLink(intent.TimeEntryId, intent.Tenant, contributor,
                        intent.UsedAtUtc, ActivityTypeScope.Tenant, PriorValues(), PriorValues() with
                        {
                            ActivityTypeId = intent.Adjustment!.ActivityTypeId, ServiceDate = intent.Adjustment.ServiceDate,
                            DurationMinutes = intent.Adjustment.DurationMinutes, BillableState = intent.Adjustment.BillableState
                        }, new("magic-link", intent.CapabilityId.Value));
                events = new object[] { used, effect }.Select((payload, index) => new StreamReadEvent(index + 2,
                    payload.GetType().FullName!, JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                    "json", 1, $"{messageId}-{index}", messageId, messageId, intent.UsedAtUtc, "fixture"))
                    .Select(stored => transform?.Invoke(stored) ?? stored).ToArray();
                return Task.FromResult(new SubmitCommandResponse(messageId, MessageId: messageId));
            });
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new StreamReadPage("tenant-1", "timesheets", intent.TimeEntryId.Value,
                events, new StreamReadMetadata(1, null, 3, 3, 2, false, null))));
        return gateway;
    }

    private static MagicLinkDurableSubmissionService Service(IEventStoreGatewayClient gateway, int timeoutMs, int intervalMs)
        => new(gateway, Options.Create(new MagicLinkSubmissionPollingOptions
        {
            Timeout = TimeSpan.FromMilliseconds(timeoutMs),
            PollInterval = TimeSpan.FromMilliseconds(intervalMs)
        }));

    private static IEventStoreGatewayClient GatewayWithAcceptedSubmission()
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                SubmitCommandRequest request = call.ArgAt<SubmitCommandRequest>(0);
                return Task.FromResult(new SubmitCommandResponse(request.MessageId, MessageId: request.MessageId));
            });
        return gateway;
    }

    private static CommitMagicLinkUse Intent() => new(
        new MagicLinkCapabilityId("capability-1"), new TenantReference("tenant-1"),
        new TimeEntryId("time-entry-1"), new MagicLinkTokenHash("hash-1"),
        MagicLinkUseAction.Confirm, null, DateTimeOffset.UtcNow);

    private static CommandStatusQueryResponse Status(CommandStatus status)
        => new("correlation", status.ToString(), (int)status, null, "message");
}
