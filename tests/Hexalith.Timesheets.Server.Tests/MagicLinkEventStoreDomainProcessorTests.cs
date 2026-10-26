using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Projections;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Timesheets.Contracts.Commands.ExternalContributions;
using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks.Commands;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.Policies;
using Hexalith.Timesheets.Server.Runtime;
using Hexalith.Timesheets.Server.TimeEntries;
using Hexalith.Timesheets.Server.TimeEntries.Commands;

using Shouldly;
using NSubstitute;

using CapabilityFold = Hexalith.Timesheets.Server.MagicLinks.MagicLinkCapabilityState;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class MagicLinkEventStoreDomainProcessorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SystemExpiryRequiresProcessingClockToReachExpiryEvenWithAFutureSubmittedInstant(bool atExpiry)
    {
        var (loader, service, external, _) = ExpiryDependencies(partyAllowed: true);
        IMagicLinkSystemExpiryAuthorizer system = Substitute.For<IMagicLinkSystemExpiryAuthorizer>();
        system.AuthorizeAsync("timesheets", new("tenant-1"), new("capability-1"), new("entry-1"),
            Recorded().Target, Arg.Any<CancellationToken>()).Returns(true);
        TimeProvider clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(Issued().ExpiresAtUtc.AddTicks(atExpiry ? 0 : -1));
        var processor = new MagicLinkEventStoreDomainProcessor(loader, service, external, clock, system);

        DomainResult result = await processor.ProcessAsync(ExpiryEnvelope(), null, TestContext.Current.CancellationToken);

        result.IsRejection.ShouldBe(!atExpiry);
        if (atExpiry)
        {
            result.Events.ShouldHaveSingleItem().ShouldBeOfType<StoredMagicLinkExpired>()
                .Event.ExpiredAtUtc.ShouldBe(DateTimeOffset.UnixEpoch.AddDays(3));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfirmationCommitRejectsFreshCatalogWithoutRecordedActivityType(bool hasRecordedType)
    {
        var catalog = new ActivityTypeCatalogReadModel([new(
            new(hasRecordedType ? "activity-1" : "other-activity"), ActivityTypeScope.Tenant, null,
            "Delivery", true, BillableState.Billable)], ProjectionFreshnessMetadata.Fresh);
        var (loader, service, external, _) = ExpiryDependencies(partyAllowed: true, catalog: catalog);
        TimeProvider clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(DateTimeOffset.UnixEpoch.AddDays(1));
        var processor = new MagicLinkEventStoreDomainProcessor(loader, service, external, clock);
        var intent = new CommitMagicLinkUse(new("capability-1"), new("tenant-1"), new("entry-1"),
            new("hash-1"), MagicLinkUseAction.Confirm, null, DateTimeOffset.UnixEpoch.AddDays(1));
        CommandEnvelope envelope = Envelope(typeof(CommitMagicLinkUse).FullName!) with
        {
            AggregateId = "entry-1",
            Payload = JsonSerializer.SerializeToUtf8Bytes(intent, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };

        DomainResult result = await processor.ProcessAsync(envelope, new object[] { Recorded() }, TestContext.Current.CancellationToken);

        catalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        result.IsRejection.ShouldBe(!hasRecordedType);
        if (!hasRecordedType)
            result.Events.ShouldHaveSingleItem().ShouldBeOfType<MagicLinkCommitRejected>();
        else
            result.Events.OfType<StoredTimeEntryConfirmed>().ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SystemExpiryRequiresTheMatchingRecordedOwner(bool mismatchedOwner)
    {
        var (loader, service, external, _) = ExpiryDependencies(partyAllowed: true);
        IMagicLinkSystemExpiryAuthorizer system = Substitute.For<IMagicLinkSystemExpiryAuthorizer>();
        system.AuthorizeAsync("timesheets", new("tenant-1"), new("capability-1"), new("entry-1"),
            Recorded().Target, Arg.Any<CancellationToken>()).Returns(true);
        var processor = new MagicLinkEventStoreDomainProcessor(loader, service, external, TimeProvider.System, system);
        TimeEntryRecorded owner = Recorded() with { TimeEntryId = new(mismatchedOwner ? "entry-2" : "entry-1") };

        DomainResult result = await processor.ProcessAsync(ExpiryEnvelope(), new object[] { owner }, TestContext.Current.CancellationToken);

        result.IsRejection.ShouldBe(mismatchedOwner);
        if (mismatchedOwner)
            await system.DidNotReceiveWithAnyArgs().AuthorizeAsync(default!, default!, default!, default!, default!, TestContext.Current.CancellationToken);
        else
            result.Events.ShouldHaveSingleItem().ShouldBeOfType<StoredMagicLinkExpired>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SystemExpiryBindsTheExactWorkReferenceRatherThanAnotherGrantedTarget(bool grantsAnotherWork)
    {
        var target = TimeEntryTargetReference.ForWork(new WorkReference("work-1"));
        var (loader, service, external, guard) = ExpiryDependencies(partyAllowed: true, issuance: Issued() with
        { TargetKind = MagicLinkTargetKind.ProposedTimeEntry, Target = target });
        IMagicLinkSystemExpiryAuthorizer system = Substitute.For<IMagicLinkSystemExpiryAuthorizer>();
        system.AuthorizeAsync("timesheets", new("tenant-1"), new("capability-1"), new("entry-1"),
            TimeEntryTargetReference.ForWork(new(grantsAnotherWork ? "work-2" : "work-1")),
            Arg.Any<CancellationToken>()).Returns(true);
        var processor = new MagicLinkEventStoreDomainProcessor(loader, service, external, TimeProvider.System, system);

        DomainResult result = await processor.ProcessAsync(ExpiryEnvelope(),
            new object[] { Recorded() with { Target = target } }, TestContext.Current.CancellationToken);

        result.IsRejection.ShouldBe(grantsAnotherWork);
        await system.Received(1).AuthorizeAsync("timesheets", new("tenant-1"), new("capability-1"), new("entry-1"),
            target, Arg.Any<CancellationToken>());
        await guard.DidNotReceive().AuthorizeAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false, "timesheets")]
    [InlineData(false, "actor-1")]
    [InlineData(true, "timesheets")]
    [InlineData(true, "actor-1")]
    public async Task ActorlessExpiryUsesScopedWorkloadAuthorityWithoutConsultingPartyAuthority(bool allow, string userId)
    {
        var (loader, service, external, guard) = ExpiryDependencies(partyAllowed: true);
        IMagicLinkSystemExpiryAuthorizer system = Substitute.For<IMagicLinkSystemExpiryAuthorizer>();
        system.AuthorizeAsync("timesheets", new("tenant-1"), new("capability-1"), new("entry-1"),
            Recorded().Target, Arg.Any<CancellationToken>()).Returns(allow);
        var processor = new MagicLinkEventStoreDomainProcessor(loader, service, external, TimeProvider.System, system);

        DomainResult result = await processor.ProcessAsync(ExpiryEnvelope() with { UserId = userId },
            null, TestContext.Current.CancellationToken);

        result.IsRejection.ShouldBe(!allow);
        if (allow)
        {
            var expired = result.Events.ShouldHaveSingleItem().ShouldBeOfType<StoredMagicLinkExpired>().Event;
            expired.TimeEntryId.ShouldBe(new("entry-1"));
            expired.Tenant.ShouldBe(new("tenant-1"));
        }
        await system.Received(1).AuthorizeAsync("timesheets", new("tenant-1"), new("capability-1"), new("entry-1"),
            Recorded().Target, Arg.Any<CancellationToken>());
        await guard.DidNotReceive().AuthorizeAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActorlessExpiryDeniesByDefaultEvenWhenTheWorkloadNameMatchesAnAuthorizedParty()
    {
        var (loader, service, external, guard) = ExpiryDependencies(partyAllowed: true);
        var processor = new MagicLinkEventStoreDomainProcessor(loader, service, external, TimeProvider.System);
        DomainResult result = await processor.ProcessAsync(ExpiryEnvelope() with { UserId = "actor-1" },
            null, TestContext.Current.CancellationToken);
        result.Events.ShouldHaveSingleItem().ShouldBeOfType<MagicLinkCommitRejected>();
        await guard.DidNotReceive().AuthorizeAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("missing-origin")]
    [InlineData("forged-origin")]
    [InlineData("foreign-tenant")]
    [InlineData("foreign-owner")]
    [InlineData("forged-actor")]
    public async Task ExpiryRejectsMissingForgedOrForeignEvidenceBeforeSystemAuthority(string scenario)
    {
        var (loader, service, external, _) = ExpiryDependencies(partyAllowed: true);
        IMagicLinkSystemExpiryAuthorizer system = Substitute.For<IMagicLinkSystemExpiryAuthorizer>();
        var processor = new MagicLinkEventStoreDomainProcessor(loader, service, external, TimeProvider.System, system);
        CommandEnvelope envelope = ExpiryEnvelope(scenario == "forged-actor" ? new("actor-2") : null);
        envelope = scenario switch
        {
            "missing-origin" => envelope with { Extensions = null },
            "forged-origin" => envelope with { Extensions = new Dictionary<string, string>
                { [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "forged-workload" } },
            "foreign-tenant" => envelope with { TenantId = "tenant-2" },
            "foreign-owner" => envelope with { AggregateId = "entry-2" },
            _ => envelope
        };
        DomainResult result = await processor.ProcessAsync(envelope, null, TestContext.Current.CancellationToken);
        result.Events.ShouldHaveSingleItem().ShouldBeOfType<MagicLinkCommitRejected>();
        await system.DidNotReceiveWithAnyArgs().AuthorizeAsync(default!, default!, default!, default!, default!, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitExpiryActorUsesPartyManagementGateIndependentlyOfSystemAuthority(bool partyAllowed)
    {
        var (loader, service, external, guard) = ExpiryDependencies(partyAllowed);
        IMagicLinkSystemExpiryAuthorizer system = Substitute.For<IMagicLinkSystemExpiryAuthorizer>();
        var processor = new MagicLinkEventStoreDomainProcessor(loader, service, external, TimeProvider.System, system);
        DomainResult result = await processor.ProcessAsync(ExpiryEnvelope(new("actor-1")),
            null, TestContext.Current.CancellationToken);
        result.IsRejection.ShouldBe(!partyAllowed);
        await guard.Received(1).AuthorizeAsync(Arg.Is<TimesheetsAuthorizationRequest>(request =>
            request.Context.Actor == new PartyReference("actor-1")), Arg.Any<CancellationToken>());
        await system.DidNotReceiveWithAnyArgs().AuthorizeAsync(default!, default!, default!, default!, default!, TestContext.Current.CancellationToken);
    }

    private static (EventStoreMagicLinkConfirmationCapabilityStateLoader Loader,
        MagicLinkConfirmationCapabilityCommandService Service, ExternalContributionCommandService External,
        ITimesheetsAccessGuard Guard) ExpiryDependencies(bool partyAllowed,
            MagicLinkConfirmationCapabilityIssued? issuance = null, ActivityTypeCatalogReadModel? catalog = null,
            Func<ActivityTypeCatalogReadModel?>? readCatalog = null)
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        var issued = issuance ?? Issued() with { TargetKind = MagicLinkTargetKind.ProposedTimeEntry };
        var streamEvent = new StreamReadEvent(1, typeof(MagicLinkConfirmationCapabilityIssued).FullName!,
            JsonSerializer.SerializeToUtf8Bytes(issued, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            "json", 1, "issue-1", "correlation-1", null, DateTimeOffset.UnixEpoch, "actor-1");
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(new StreamReadPage("tenant-1", "timesheets", "capability-1", [streamEvent],
                new(0, null, 1, 1, 1, false, null)));
        var tokens = new CryptographicMagicLinkTokenGenerator();
        IReadModelStore readModels = Substitute.For<IReadModelStore>();
        readModels.GetAsync<ActivityTypeCatalogReadModel>(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ReadModelEntry<ActivityTypeCatalogReadModel>(readCatalog is null ? catalog : readCatalog(), "etag-1"));
        var loader = new EventStoreMagicLinkConfirmationCapabilityStateLoader(gateway,
            readModels, tokens, Substitute.For<ITimesheetsTrustedContextAccessor>(), allowLegacyIndexRead: true);
        ITimesheetsAccessGuard guard = Substitute.For<ITimesheetsAccessGuard>();
        guard.AuthorizeAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(partyAllowed ? TimesheetsAuthorizationDecision.Allowed()
                : TimesheetsAuthorizationDecision.Denied(TimesheetsDenialCategory.UnconfiguredPolicy, "Denied"));
        var external = new ExternalContributionCommandService(new(guard), new(guard), guard,
            ExternalContributionPolicyOptions.Default);
        return (loader, new(guard, tokens, external), external, guard);
    }

    private static CommandEnvelope ExpiryEnvelope(PartyReference? actor = null)
    {
        var intent = new CommitMagicLinkTransition(new("capability-1"), new("tenant-1"), new("entry-1"),
            actor, MagicLinkTransitionAction.Expire, new("timesheets", "expiry-1"), DateTimeOffset.UnixEpoch.AddDays(3));
        return Envelope(typeof(CommitMagicLinkTransition).FullName!) with
        {
            AggregateId = "entry-1",
            Payload = JsonSerializer.SerializeToUtf8Bytes(intent, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };
    }

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
    public void VerifiedOriginGuardAcceptsOnlyTheGatewayStampedTimesheetsOrigin(string commandName)
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
    public void ManagementActorGuardRequiresTheVerifiedActorToMatchTheEnvelope(string commandName)
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

    [Theory]
    [InlineData("issue", false)]
    [InlineData("adjust", false)]
    [InlineData("confirm", false)]
    [InlineData("issue", true)]
    [InlineData("adjust", true)]
    [InlineData("confirm", true)]
    public async Task AwaitedAuthorizationUsesProcessingCatalogSnapshotAndNextDecisionObservesUpdate(string action, bool policyOnly)
    {
        var catalog = new ActivityTypeCatalogReadModel([new(new("activity-1"), ActivityTypeScope.Tenant,
            null, "Delivery", true, BillableState.Billable)], ProjectionFreshnessMetadata.Fresh)
        {
            ProjectPolicy = new(new("tenant-1"), true, [])
        };
        var (loader, service, external, guard) = ExpiryDependencies(true, issuance: Issued() with
        {
            TargetKind = MagicLinkTargetKind.ProposedTimeEntry,
            AllowedAction = action == "adjust" ? MagicLinkAllowedAction.Adjust : MagicLinkAllowedAction.Confirm
        }, readCatalog: () => catalog);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        guard.AuthorizeAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<TimesheetsAuthorizationDecision>(AuthorizeAsync()));
        async Task<TimesheetsAuthorizationDecision> AuthorizeAsync()
        {
            entered.TrySetResult();
            await resume.Task.WaitAsync(TestContext.Current.CancellationToken);
            return TimesheetsAuthorizationDecision.Allowed();
        }
        TimeProvider clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(DateTimeOffset.UnixEpoch.AddDays(1));
        var processor = new MagicLinkEventStoreDomainProcessor(loader, service, external, clock);
        object intent = action == "issue"
            ? new CommitMagicLinkIssue(new IssueMagicLinkConfirmationCapability(new("capability-1"),
                new MagicLinkConfirmationScope(new("actor-1"), Recorded().Target, new("activity-1"), new("entry-1"),
                    MagicLinkTargetKind.ProposedTimeEntry), MagicLinkAllowedAction.Confirm, Issued().ExpiresAtUtc,
                new("timesheets", "snapshot-issue")), new("tenant-1"), new("actor-1"), new("hash-1"),
                DateTimeOffset.UnixEpoch.AddDays(1))
            : new CommitMagicLinkUse(new("capability-1"), new("tenant-1"), new("entry-1"), new("hash-1"),
                action == "adjust" ? MagicLinkUseAction.Adjust : MagicLinkUseAction.Confirm,
                action == "adjust" ? new(new(2026, 6, 19), 90, new("activity-1"), BillableState.Billable) : null,
                DateTimeOffset.UnixEpoch.AddDays(1));
        CommandEnvelope command = Envelope(intent.GetType().FullName!) with
        {
            AggregateId = action == "issue" ? "capability-1" : "entry-1",
            Payload = JsonSerializer.SerializeToUtf8Bytes(intent, intent.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };
        object? owner = action == "issue" ? null : new object[] { Recorded() };
        Task<DomainResult> first = processor.ProcessAsync(command, owner, TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        catalog = catalog with
        {
            Items = policyOnly ? catalog.Items : [catalog.Items[0] with { IsActive = false, IsAvailableForCapture = false }],
            ProjectPolicy = new(new("tenant-1"), true, [new("policy-1", new("project-1"), true, [], [])])
        };
        if (policyOnly)
            catalog.Items.ShouldHaveSingleItem().IsAvailableForCapture.ShouldBeTrue();
        resume.SetResult();
        (await first).IsRejection.ShouldBeFalse();
        DomainResult later = await processor.ProcessAsync(command with { MessageId = "later-decision" }, owner,
            TestContext.Current.CancellationToken);
        later.IsRejection.ShouldBe(action != "confirm");
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
