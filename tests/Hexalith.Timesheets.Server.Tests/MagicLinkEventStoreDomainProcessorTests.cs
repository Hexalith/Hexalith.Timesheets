using System.Reflection;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Events;
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
    [InlineData(TimeEntryApprovalState.Draft, false)]
    [InlineData(TimeEntryApprovalState.Submitted, false)]
    [InlineData(TimeEntryApprovalState.Approved, false)]
    [InlineData(TimeEntryApprovalState.Draft, true)]
    public async Task ConfirmationCommitSucceedsAcrossApprovalStatesWithoutAlteringApproval(
        TimeEntryApprovalState approvalState, bool workTarget)
    {
        var (loader, service, external, guard) = ExpiryDependencies(partyAllowed: true);
        var processor = new MagicLinkEventStoreDomainProcessor(loader, service, external, TimeProvider.System);
        WorkReference? work = workTarget ? new WorkReference("work-1") : null;
        ProjectReference? project = workTarget ? null : new ProjectReference("project-1");
        TimeEntryRecorded recorded = Recorded() with
        {
            Target = work is not null ? TimeEntryTargetReference.ForWork(work) : TimeEntryTargetReference.ForProject(project!)
        };
        List<object> history = [new StoredTimeEntryRecorded(recorded)];
        if (approvalState is TimeEntryApprovalState.Submitted or TimeEntryApprovalState.Approved)
        {
            history.Add(new StoredTimeEntrySubmitted(new TimeEntrySubmitted(
                Recorded().TimeEntryId,
                Recorded().Contributor,
                new("tenant-1"),
                DateTimeOffset.UnixEpoch.AddMinutes(5),
                new("submission-1"),
                TimeEntrySubmissionScope.SelectedEntries,
                TimeEntryApprovalState.Submitted)));
        }

        if (approvalState == TimeEntryApprovalState.Approved)
        {
            history.Add(new TimeEntryApproved(
                Recorded().TimeEntryId,
                new("approver-1"),
                new("tenant-1"),
                DateTimeOffset.UnixEpoch.AddMinutes(10),
                new("decision-1"),
                TimeEntryApprovalState.Approved,
                new(
                    ApprovalAuthorityAction.EntryApproval,
                    ApprovalAuthoritySource.ProjectApprover,
                    ApprovalAuthorityDecisionState.Allowed,
                    "policy-key",
                    "v1",
                    ProjectionFreshnessMetadata.Fresh),
                TimeEntryApprovalScope.IndividualEntry));
        }

        DomainResult result = await processor.ProcessAsync(
            ConfirmationEnvelope(),
            history.ToArray(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        StoredTimeEntryConfirmed stored = result.Events.ShouldHaveSingleItem()
            .ShouldBeOfType<StoredTimeEntryConfirmed>();
        stored.Event.TimeEntryId.ShouldBe(Recorded().TimeEntryId);
        stored.Event.Contributor.ShouldBe(Recorded().Contributor);
        stored.Event.Tenant.ShouldBe(new TenantReference("tenant-1"));
        stored.Event.Source.ShouldBe(new ExternalContributionSource("supplier-api", "confirm-1"));
        await guard.Received(1).AuthorizeAsync(
            Arg.Is<TimesheetsAuthorizationRequest>(request =>
                request.Operation == TimesheetsOperation.Confirmation
                && request.Contributor == Recorded().Contributor
                && request.Project == project
                && request.Work == work),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("default-timestamp")]
    [InlineData("missing-timestamp")]
    [InlineData("duplicate-timestamp")]
    [InlineData("malformed-base64")]
    public async Task ConfirmationCommitRejectsInvalidIntentOrReplayBeforeAuthorization(string scenario)
    {
        var (loader, service, external, guard) = ExpiryDependencies(partyAllowed: true);
        var processor = new MagicLinkEventStoreDomainProcessor(loader, service, external, TimeProvider.System);
        CommitTimeEntryConfirmation intent = ConfirmationIntent();
        CommandEnvelope envelope = scenario switch
        {
            "default-timestamp" => ConfirmationEnvelope(intent with { ConfirmedAtUtc = default }),
            "missing-timestamp" => ConfirmationEnvelope() with
            {
                Payload = JsonSerializer.SerializeToUtf8Bytes(new { intent.Command, intent.Tenant, intent.Actor }, JsonSerializerOptions.Web)
            },
            "duplicate-timestamp" => ConfirmationEnvelope() with
            {
                Payload = Encoding.UTF8.GetBytes("{\"confirmedAtUtc\":\"1970-01-01T01:00:00+00:00\"," +
                    Encoding.UTF8.GetString(ConfirmationEnvelope().Payload)[1..])
            },
            _ => ConfirmationEnvelope()
        };
        using JsonDocument malformedReplay = JsonDocument.Parse(
            """[{"eventTypeName":"StoredTimeEntryRecorded","payload":"AAA"}]""");
        object currentState = scenario == "malformed-base64"
            ? malformedReplay.RootElement
            : new object[] { new StoredTimeEntryRecorded(Recorded()) };

        DomainResult result = await processor.ProcessAsync(envelope, currentState, TestContext.Current.CancellationToken);

        result.IsNoOp.ShouldBeTrue();
        result.Events.ShouldBeEmpty();
        await guard.DidNotReceive().AuthorizeAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("bad-domain")]
    [InlineData("missing-origin")]
    [InlineData("forged-origin")]
    [InlineData("forged-actor-user")]
    [InlineData("forged-actor-extension")]
    [InlineData("foreign-tenant")]
    [InlineData("foreign-owner")]
    [InlineData("non-utc")]
    [InlineData("unrecorded")]
    [InlineData("wrong-contributor")]
    [InlineData("non-external")]
    [InlineData("conflicting-source")]
    [InlineData("already-confirmed-same-source")]
    [InlineData("denied-guard")]
    [InlineData("corrupt-payload")]
    public async Task ConfirmationCommitReturnsNoOpWithoutPollutionOnRefusal(string scenario)
    {
        var (loader, service, external, _) = ExpiryDependencies(partyAllowed: scenario != "denied-guard");
        var processor = new MagicLinkEventStoreDomainProcessor(loader, service, external, TimeProvider.System);
        CommitTimeEntryConfirmation intent = ConfirmationIntent();
        if (scenario == "non-utc")
        {
            intent = intent with { ConfirmedAtUtc = new DateTimeOffset(2026, 6, 19, 14, 0, 0, TimeSpan.FromHours(2)) };
        }
        else if (scenario == "wrong-contributor")
        {
            intent = intent with { Command = intent.Command with { Contributor = new PartyReference("party-other") } };
        }

        CommandEnvelope envelope = ConfirmationEnvelope(intent);
        envelope = scenario switch
        {
            "bad-domain" => envelope with { Domain = "foreign" },
            "missing-origin" => envelope with { Extensions = null },
            "forged-origin" => envelope with
            {
                Extensions = new Dictionary<string, string>
                {
                    [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "forged",
                    [EventStoreGatewayVerifiedOrigin.ActorExtensionKey] = "actor-1"
                }
            },
            "forged-actor-user" => envelope with { UserId = "actor-2" },
            "forged-actor-extension" => envelope with
            {
                Extensions = new Dictionary<string, string>
                {
                    [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "timesheets",
                    [EventStoreGatewayVerifiedOrigin.ActorExtensionKey] = "actor-2"
                }
            },
            "foreign-tenant" => envelope with { TenantId = "tenant-2" },
            "foreign-owner" => envelope with { AggregateId = "entry-2" },
            "corrupt-payload" => envelope with { Payload = "{"u8.ToArray() },
            _ => envelope
        };

        object? state = scenario switch
        {
            "unrecorded" => null,
            "non-external" => new object[]
            {
                new StoredTimeEntryRecorded(Recorded() with { ContributorCategory = ContributorCategory.Employee })
            },
            "conflicting-source" => new object[]
            {
                new StoredTimeEntryRecorded(Recorded()),
                new StoredTimeEntryConfirmed(new TimeEntryContributorConfirmed(
                    Recorded().TimeEntryId,
                    Recorded().Contributor,
                    new("tenant-1"),
                    DateTimeOffset.UnixEpoch.AddMinutes(1),
                    new ExternalContributionSource("supplier-api", "other-confirm")))
            },
            "already-confirmed-same-source" => new object[]
            {
                new StoredTimeEntryRecorded(Recorded()),
                new StoredTimeEntryConfirmed(new TimeEntryContributorConfirmed(
                    Recorded().TimeEntryId,
                    Recorded().Contributor,
                    new("tenant-1"),
                    DateTimeOffset.UnixEpoch.AddMinutes(1),
                    intent.Command.Source))
            },
            _ => new object[] { new StoredTimeEntryRecorded(Recorded()) }
        };

        DomainResult result = await processor.ProcessAsync(envelope, state, TestContext.Current.CancellationToken);

        result.IsNoOp.ShouldBeTrue();
        result.IsRejection.ShouldBeFalse();
        result.Events.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfirmationCommitReturnsNoOpOnCorruptReplayWithoutConsultingDependencies(bool wrapped)
    {
        object[] history = ReplayHistory(nameof(CommitTimeEntryConfirmation), wrapped);
        var processor = new MagicLinkEventStoreDomainProcessor(null!, null!, null!, TimeProvider.System);

        DomainResult result = await processor.ProcessAsync(
            ConfirmationEnvelope(),
            history,
            TestContext.Current.CancellationToken);

        result.IsNoOp.ShouldBeTrue();
        result.IsRejection.ShouldBeFalse();
        result.Events.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfirmationCommitPropagatesCancellation(bool duringReplay)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        object[] history = ReplayHistory(nameof(CommitTimeEntryConfirmation), wrapped: true);
        if (!duringReplay)
        {
            await cancellation.CancelAsync();
        }

        var processor = new MagicLinkEventStoreDomainProcessor(null!, null!, null!, TimeProvider.System);
        object source = duringReplay ? CancelDuringReplay(history[0], cancellation) : history;

        OperationCanceledException exception = await Should.ThrowAsync<OperationCanceledException>(() =>
            processor.ProcessAsync(ConfirmationEnvelope(), source, cancellation.Token));

        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

    [Theory]
    [InlineData("ambiguous-terminal-history")]
    [InlineData("uninitialized-target")]
    [InlineData("payload-evolution-exception")]
    public async Task ConfirmationCommitReturnsNoOpWithoutGuardCallOnAmbiguousUninitializedOrEvolutionFailure(string scenario)
    {
        var (loader, service, external, guard) = ExpiryDependencies(partyAllowed: true);
        var processor = new MagicLinkEventStoreDomainProcessor(loader, service, external, TimeProvider.System);
        object currentState = scenario switch
        {
            "ambiguous-terminal-history" => new object[]
            {
                new StoredTimeEntryRecorded(Recorded()),
                new StoredMagicLinkRevoked(new MagicLinkConfirmationCapabilityRevoked(
                    new("cap-1"), new("tenant-1"), new("operator-1"), DateTimeOffset.UnixEpoch, new("timesheets", "r1"))
                    { TimeEntryId = Recorded().TimeEntryId }),
                new StoredMagicLinkRevoked(new MagicLinkConfirmationCapabilityRevoked(
                    new("cap-1"), new("tenant-1"), new("operator-1"), DateTimeOffset.UnixEpoch, new("timesheets", "r2"))
                    { TimeEntryId = Recorded().TimeEntryId })
            },
            "uninitialized-target" => new object[]
            {
                new StoredTimeEntryRecorded(Recorded() with
                {
                    Target = (TimeEntryTargetReference)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(TimeEntryTargetReference))
                })
            },
            _ => ThrowEvolutionDuringReplay()
        };

        DomainResult result = await processor.ProcessAsync(
            ConfirmationEnvelope(), currentState, TestContext.Current.CancellationToken);

        result.IsNoOp.ShouldBeTrue();
        result.IsRejection.ShouldBeFalse();
        result.Events.ShouldBeEmpty();
        await guard.DidNotReceive().AuthorizeAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>());
    }

    private static IEnumerable<object> ThrowEvolutionDuringReplay()
    {
        yield return new StoredTimeEntryRecorded(Recorded());
        throw new EventPayloadEvolutionException("StoredTimeEntryRecorded", 1, 1, "Simulated schema evolution error.");
    }

    [Theory]
    [InlineData("foreign-submission-tenant")]
    [InlineData("foreign-submission-owner")]
    [InlineData("foreign-correction-tenant")]
    [InlineData("foreign-correction-owner")]
    [InlineData("same-owner-pre-record-terminal")]
    [InlineData("foreign-owner-pre-record-terminal")]
    public async Task ConfirmationCommitChecksScopeOfReplayedEventsBeforeAuthorization(string scenario)
    {
        var (loader, service, external, guard) = ExpiryDependencies(partyAllowed: true);
        var processor = new MagicLinkEventStoreDomainProcessor(loader, service, external, TimeProvider.System);
        TimeEntryRecorded recorded = Recorded();
        var tenant = new TenantReference("tenant-1");
        var foreignTenant = new TenantReference("tenant-2");
        var foreignOwner = new TimeEntryId("foreign-entry");
        List<object> history = [new StoredTimeEntryRecorded(recorded)];
        if (scenario.EndsWith("pre-record-terminal", StringComparison.Ordinal))
        {
            history.Insert(0, new StoredMagicLinkRevoked(new MagicLinkConfirmationCapabilityRevoked(
                new("old-capability"), tenant, new("operator-1"), DateTimeOffset.UnixEpoch, new("timesheets", "revoke-1"))
            {
                TimeEntryId = scenario == "foreign-owner-pre-record-terminal" ? foreignOwner : recorded.TimeEntryId
            }));
        }

        history.Add(new StoredTimeEntrySubmitted(new TimeEntrySubmitted(
            scenario == "foreign-submission-owner" ? foreignOwner : recorded.TimeEntryId,
            recorded.Contributor, scenario == "foreign-submission-tenant" ? foreignTenant : tenant,
            DateTimeOffset.UnixEpoch.AddMinutes(1), new("submission-1"), TimeEntrySubmissionScope.SelectedEntries,
            TimeEntryApprovalState.Submitted)));
        if (scenario.StartsWith("foreign-correction", StringComparison.Ordinal))
        {
            var approver = new PartyReference("approver-1");
            var decisionId = new TimeEntryApprovalDecisionId("decision-1");
            const TimeEntryApprovalScope scope = TimeEntryApprovalScope.IndividualEntry;
            var authority = new ApprovalAuthoritySourceAttribution(ApprovalAuthorityAction.EntryApproval,
                ApprovalAuthoritySource.ProjectApprover, ApprovalAuthorityDecisionState.Allowed, "policy-key", "v1",
                ProjectionFreshnessMetadata.Fresh);
            history.Add(new TimeEntryApproved(recorded.TimeEntryId, approver, tenant, DateTimeOffset.UnixEpoch.AddMinutes(2),
                decisionId, TimeEntryApprovalState.Approved, authority, scope));
            var previous = new TimeEntryCorrectionValues(recorded.Target, recorded.Contributor, recorded.ActivityTypeId,
                recorded.ServiceDate, recorded.DurationMinutes, recorded.BillableState, recorded.ContributorCategory, recorded.AiMetrics)
            { ActivityTypeScope = recorded.ActivityTypeScope };
            history.Add(new TimeEntryApprovedCorrected(
                scenario == "foreign-correction-owner" ? foreignOwner : recorded.TimeEntryId,
                new("correction-1"), scenario == "foreign-correction-tenant" ? foreignTenant : tenant, approver,
                DateTimeOffset.UnixEpoch.AddMinutes(3), previous, previous with { DurationMinutes = 90 }, new("Correction"),
                decisionId, scope, TimeEntryApprovalState.Approved, TimeEntryCorrectionState.Corrected));
        }

        DomainResult result = await processor.ProcessAsync(ConfirmationEnvelope(), history.ToArray(), TestContext.Current.CancellationToken);

        bool valid = scenario == "same-owner-pre-record-terminal";
        result.IsSuccess.ShouldBe(valid);
        result.IsNoOp.ShouldBe(!valid);
        await guard.Received(valid ? 1 : 0).AuthorizeAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>());
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

    private static CommitTimeEntryConfirmation ConfirmationIntent() => new(
        new ConfirmExternalTimeEntry(
            Recorded().TimeEntryId,
            Recorded().Contributor,
            new ExternalContributionSource("supplier-api", "confirm-1")),
        new("tenant-1"),
        new("actor-1"),
        DateTimeOffset.UnixEpoch.AddHours(1));

    private static CommandEnvelope ConfirmationEnvelope(CommitTimeEntryConfirmation? intent = null)
    {
        CommitTimeEntryConfirmation payload = intent ?? ConfirmationIntent();
        return Envelope(typeof(CommitTimeEntryConfirmation).FullName!) with
        {
            AggregateId = payload.Command.TimeEntryId.Value,
            Payload = JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };
    }

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
