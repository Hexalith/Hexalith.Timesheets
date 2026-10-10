using System.Diagnostics;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks.Commands;

using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

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
            Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
        await gateway.DidNotReceive().ReadWorkloadStreamAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Streams.StreamReadRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stalled_status_transport_cannot_exceed_the_polling_deadline()
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
        await gateway.DidNotReceive().SubmitWorkloadCommandAsync(
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
