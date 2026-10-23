using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.Timesheets.Contracts.Commands.ExternalContributions;
using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks.Commands;
using Hexalith.Timesheets.Server.Runtime;
using Hexalith.Timesheets.Server.TimeEntries;
using Hexalith.Timesheets.Server.TimeEntries.Commands;

using Shouldly;

using CapabilityFold = Hexalith.Timesheets.Server.MagicLinks.MagicLinkCapabilityState;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class MagicLinkEventStoreDomainProcessorTests
{
    [Fact]
    public async Task DurableExistingTimeEntryIssueRejectsBeforeConsultingDependencies()
    {
        var processor = new MagicLinkEventStoreDomainProcessor(null!, null!, null!, TimeProvider.System);
        DomainResult result = await processor.ProcessAsync(ReplayEnvelope(nameof(CommitMagicLinkIssue)), null,
            TestContext.Current.CancellationToken);
        result.IsRejection.ShouldBeTrue();
        result.Events.ShouldHaveSingleItem().ShouldBeOfType<MagicLinkCommitRejected>();
        CapabilityFold historical = DomainStateReplay.Rehydrate<CapabilityFold>(new object[] { new StoredMagicLinkIssued(Issued()) })!;
        historical.TargetKind.ShouldBe(MagicLinkTargetKind.ExistingTimeEntry);
        historical.Exists.ShouldBeTrue();
    }

    [Theory]
    [InlineData(nameof(CommitMagicLinkIssue))]
    [InlineData(nameof(CommitMagicLinkUse))]
    [InlineData(nameof(CommitMagicLinkTransition))]
    public void Verified_origin_guard_accepts_only_the_gateway_stamped_timesheets_origin(string commandName)
    {
        CommandEnvelope valid = Envelope(commandName);

        HasVerifiedOrigin(valid).ShouldBeTrue();
        HasVerifiedOrigin(valid with { Extensions = null }).ShouldBeFalse();
        HasVerifiedOrigin(valid with { Extensions = new Dictionary<string, string>
        {
            [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "other-domain"
        } }).ShouldBeFalse();
    }

    [Theory]
    [InlineData(nameof(CommitMagicLinkIssue))]
    [InlineData(nameof(CommitMagicLinkTransition))]
    public void Management_actor_guard_requires_the_verified_actor_to_match_the_envelope(string commandName)
    {
        CommandEnvelope valid = Envelope(commandName);

        HasVerifiedActor(valid, "actor-1").ShouldBeTrue();
        HasVerifiedActor(valid with { UserId = "actor-2" }, "actor-1").ShouldBeFalse();
        HasVerifiedActor(valid with { Extensions = new Dictionary<string, string>
        {
            [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "timesheets",
            [EventStoreGatewayVerifiedOrigin.ActorExtensionKey] = "actor-2"
        } }, "actor-1").ShouldBeFalse();
        HasVerifiedActor(valid, " ").ShouldBeFalse();
    }

    [Theory]
    [InlineData(nameof(CommitMagicLinkIssue), false)]
    [InlineData(nameof(CommitMagicLinkIssue), true)]
    [InlineData(nameof(CommitMagicLinkUse), false)]
    [InlineData(nameof(CommitMagicLinkUse), true)]
    [InlineData(nameof(CommitMagicLinkTransition), false)]
    [InlineData(nameof(CommitMagicLinkTransition), true)]
    [InlineData(nameof(CommitTimeEntryRecord), false)]
    [InlineData(nameof(CommitTimeEntryRecord), true)]
    public async Task FoldIntegrityFailureFromActualReplayReturnsOnlyOpaqueRejection(string commandName, bool wrapped)
    {
        object[] history = ReplayHistory(commandName, wrapped);
        TargetInvocationException replayException = Should.Throw<TargetInvocationException>(() =>
        {
            if (commandName == nameof(CommitMagicLinkIssue))
                _ = DomainStateReplay.Rehydrate<CapabilityFold>(history);
            else
                _ = DomainStateReplay.Rehydrate<TimeEntryState>(history);
        });
        replayException.InnerException.ShouldBeOfType<InvalidOperationException>();
        // Replay must reject before consulting any authority or command-service dependency.
        var processor = new MagicLinkEventStoreDomainProcessor(null!, null!, null!, TimeProvider.System);

        DomainResult result = await processor.ProcessAsync(ReplayEnvelope(commandName), history,
            TestContext.Current.CancellationToken);

        result.IsRejection.ShouldBeTrue();
        result.Events.ShouldHaveSingleItem().ShouldBeOfType<MagicLinkCommitRejected>();
    }

    [Theory]
    [InlineData(nameof(CommitMagicLinkIssue), false)]
    [InlineData(nameof(CommitMagicLinkIssue), true)]
    [InlineData(nameof(CommitMagicLinkUse), false)]
    [InlineData(nameof(CommitMagicLinkUse), true)]
    [InlineData(nameof(CommitMagicLinkTransition), false)]
    [InlineData(nameof(CommitMagicLinkTransition), true)]
    [InlineData(nameof(CommitTimeEntryRecord), false)]
    [InlineData(nameof(CommitTimeEntryRecord), true)]
    public async Task ReplayRejectionBoundariesPropagateCancellation(string commandName, bool duringReplay)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        object[] history = ReplayHistory(commandName, wrapped: true);
        if (!duringReplay) await cancellation.CancelAsync();
        var processor = new MagicLinkEventStoreDomainProcessor(null!, null!, null!, TimeProvider.System);
        object source = duringReplay ? CancelDuringReplay(history[0], cancellation) : history;

        OperationCanceledException exception = await Should.ThrowAsync<OperationCanceledException>(() =>
            processor.ProcessAsync(ReplayEnvelope(commandName), source, cancellation.Token));

        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

    private static IEnumerable<object> CancelDuringReplay(object first, CancellationTokenSource cancellation)
    {
        yield return first;
        cancellation.Cancel();
        cancellation.Token.ThrowIfCancellationRequested();
    }

    private static object[] ReplayHistory(string commandName, bool wrapped)
    {
        if (commandName == nameof(CommitMagicLinkIssue))
        {
            var used = new MagicLinkConfirmationCapabilityUsed(new("capability-1"), new("tenant-1"),
                new("actor-1"), new("entry-1"), DateTimeOffset.UnixEpoch.AddHours(1), new("magic-link", "use-1"));
            return wrapped
                ? [new StoredMagicLinkIssued(Issued()), new StoredMagicLinkUsed(used), new StoredMagicLinkIssued(Issued())]
                : [Issued(), used, Issued()];
        }
        return wrapped ? [new StoredTimeEntryRecorded(Recorded()), new StoredTimeEntryRecorded(Recorded())]
            : [Recorded(), Recorded()];
    }

    private static CommandEnvelope ReplayEnvelope(string commandName)
    {
        TimeEntryRecorded recorded = Recorded();
        object intent = commandName switch
        {
            nameof(CommitMagicLinkIssue) => new CommitMagicLinkIssue(new IssueMagicLinkConfirmationCapability(
                new("capability-1"), new MagicLinkConfirmationScope(recorded.Contributor, recorded.Target,
                    recorded.ActivityTypeId, recorded.TimeEntryId, MagicLinkTargetKind.ExistingTimeEntry),
                MagicLinkAllowedAction.Confirm, DateTimeOffset.UtcNow.AddDays(1), new("timesheets", "issue-1")),
                new("tenant-1"), new("actor-1"), new("hash-1"), DateTimeOffset.UtcNow),
            nameof(CommitMagicLinkUse) => new CommitMagicLinkUse(new("capability-1"), new("tenant-1"),
                recorded.TimeEntryId, new("hash-1"), MagicLinkUseAction.Confirm, null, DateTimeOffset.UtcNow),
            nameof(CommitMagicLinkTransition) => new CommitMagicLinkTransition(new("capability-1"), new("tenant-1"),
                recorded.TimeEntryId, new("actor-1"), MagicLinkTransitionAction.Revoke, new("timesheets", "revoke-1"), DateTimeOffset.UtcNow),
            nameof(CommitTimeEntryRecord) => new CommitTimeEntryRecord(new SubmitExternalTimeEntry(recorded.TimeEntryId,
                recorded.Target, recorded.Contributor, recorded.ActivityTypeId, recorded.ServiceDate, recorded.DurationMinutes,
                recorded.BillableState, new("external", "source-1")), new("tenant-1"), new("actor-1"), DateTimeOffset.UtcNow),
            _ => throw new InvalidOperationException()
        };
        return Envelope(intent.GetType().FullName!) with
        {
            AggregateId = commandName == nameof(CommitMagicLinkIssue) ? "capability-1" : "entry-1",
            Payload = JsonSerializer.SerializeToUtf8Bytes(intent, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };
    }

    private static TimeEntryRecorded Recorded() => new(new("entry-1"),
        TimeEntryTargetReference.ForProject(new ProjectReference("project-1")), new("actor-1"), new("activity-1"),
        ActivityTypeScope.Tenant, new(2026, 6, 19), 60, BillableState.Billable,
        TimeEntryApprovalState.Draft, ContributorCategory.ExternalContributor, null);

    private static MagicLinkConfirmationCapabilityIssued Issued() => new(new("capability-1"), new("tenant-1"),
        new("actor-1"), Recorded().Target, new("activity-1"), new("entry-1"), MagicLinkTargetKind.ExistingTimeEntry,
        MagicLinkAllowedAction.Confirm, new("hash-1"), DateTimeOffset.UnixEpoch.AddDays(2), new("actor-1"),
        DateTimeOffset.UnixEpoch, new("timesheets", "issue-1"), true);

    private static CommandEnvelope Envelope(string commandName)
        => new("message-1", "tenant-1", "timesheets", "owner-1", commandName, [],
            "correlation-1", null, "actor-1", new Dictionary<string, string>
            {
                [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "timesheets",
                [EventStoreGatewayVerifiedOrigin.ActorExtensionKey] = "actor-1"
            });

    private static bool HasVerifiedOrigin(CommandEnvelope envelope)
        => (bool)typeof(MagicLinkEventStoreDomainProcessor)
            .GetMethod("HasVerifiedWorkloadOrigin", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [envelope])!;

    private static bool HasVerifiedActor(CommandEnvelope envelope, string actor)
        => (bool)typeof(MagicLinkEventStoreDomainProcessor)
            .GetMethod("HasVerifiedActor", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [envelope, actor])!;
}
