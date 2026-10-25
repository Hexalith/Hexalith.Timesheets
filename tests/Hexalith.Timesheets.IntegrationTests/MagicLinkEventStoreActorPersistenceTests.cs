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
using Hexalith.Timesheets.Contracts.Commands.ExternalContributions;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks.Commands;
using Hexalith.Timesheets.Server.Policies;
using Hexalith.Timesheets.Server.TimeEntries;
using Hexalith.Timesheets.Server.TimeEntries.Commands;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed partial class MagicLinkConfirmationHttpBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SystemExpiryPersistsOnlyWithScopedWorkloadAuthorityAndSurvivesActorRecreation(bool allow)
    {
        var capabilityId = new MagicLinkCapabilityId("capability-system-expiry");
        var entryId = new TimeEntryId("entry-system-expiry");
        IMagicLinkSystemExpiryAuthorizer system = Substitute.For<IMagicLinkSystemExpiryAuthorizer>();
        system.AuthorizeAsync("timesheets", Tenant(), capabilityId, entryId,
            TimeEntryTargetReference.ForProject(Project()), Arg.Any<CancellationToken>()).Returns(allow);
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, systemExpiryAuthorizer: system);
        using HttpClient client = factory.CreateClient();
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            capabilityId, entryId);
        StreamReadEvent seed = factory.Gateway.StoredEvents(Tenant().TenantId, capabilityId.Value).Single();
        MagicLinkConfirmationCapabilityIssued issued = JsonSerializer.Deserialize<MagicLinkConfirmationCapabilityIssued>(seed.Payload, JsonOptions)!;
        factory.Gateway.WithStream(Tenant().TenantId, capabilityId.Value, seed with
        {
            Payload = JsonSerializer.SerializeToUtf8Bytes(issued with { ExpiresAtUtc = ObservedAtUtc }, JsonOptions)
        });
        using IServiceScope scope = factory.Services.CreateScope();
        MagicLinkEventStoreDomainProcessor processor = scope.ServiceProvider.GetRequiredService<MagicLinkEventStoreDomainProcessor>();
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(call => processor.ProcessAsync(call.ArgAt<CommandEnvelope>(0), call.ArgAt<object?>(1), call.ArgAt<CancellationToken>(2)));
        var stateManager = new InMemoryStateManager();
        var statusStore = new InMemoryCommandStatusStore();
        AggregateActor actor = CreateMagicLinkActor(entryId, stateManager, statusStore, invoker);
        var intent = new CommitMagicLinkTransition(capabilityId, Tenant(), entryId, null,
            MagicLinkTransitionAction.Expire, new("timesheets", "system-expiry"), ObservedAtUtc);
        // The same text is an authorized Party in this fixture; it must supply no authority for actorless expiry.
        CommandEnvelope envelope = ActorCommand(typeof(CommitMagicLinkTransition).FullName!, "system-expiry", entryId,
            JsonSerializer.SerializeToUtf8Bytes(intent, JsonOptions)) with { UserId = Operator().PartyId };
        int partyChecks = factory.AccessGuard.AuthorizationCount;

        (await actor.ProcessCommandAsync(envelope, TestContext.Current.CancellationToken)).Accepted.ShouldBe(allow);

        factory.AccessGuard.AuthorizationCount.ShouldBe(partyChecks);
        var persisted = await actor.GetEventsAsync(0);
        persisted.ShouldHaveSingleItem().EventTypeName.ShouldBe(allow
            ? typeof(StoredMagicLinkExpired).FullName : typeof(MagicLinkCommitRejected).FullName);
        AggregateActor restarted = CreateMagicLinkActor(entryId, stateManager, statusStore, invoker);
        (await restarted.GetEventsAsync(0)).Select(item => item.EventTypeName)
            .ShouldBe(persisted.Select(item => item.EventTypeName));
        if (allow)
        {
            (await restarted.ProcessCommandAsync(envelope with { MessageId = "system-expiry-replay" },
                TestContext.Current.CancellationToken)).Accepted.ShouldBeFalse();
            (await restarted.GetEventsAsync(0)).Count(item => item.EventTypeName == typeof(StoredMagicLinkExpired).FullName)
                .ShouldBe(1);
            await system.Received(1).AuthorizeAsync("timesheets", Tenant(), capabilityId, entryId,
                TimeEntryTargetReference.ForProject(Project()), Arg.Any<CancellationToken>());
        }
    }

    [Theory]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, true)]
    public async Task AdjustmentHttpDisplayRequiresServerIssuedCommentDecision(
        bool serverAllowsDisplay, bool serverClearsRedaction, bool callerClaimsNoRedaction,
        bool displaysComment)
    {
        ITimeEntryCommentPublicationAuthority authority = Substitute.For<ITimeEntryCommentPublicationAuthority>();
        authority.MayDisplayExternally(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryComment>())
            .Returns(serverAllowsDisplay);
        authority.MayDisplayUnredactedExternally(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryComment>())
            .Returns(serverClearsRedaction);
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true,
            useMismatchedClaims: false, commentAuthority: authority);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId(
            $"capability-display-{serverAllowsDisplay}-{serverClearsRedaction}-{callerClaimsNoRedaction}");
        var entryId = new TimeEntryId(
            $"entry-display-{serverAllowsDisplay}-{serverClearsRedaction}-{callerClaimsNoRedaction}");
        string token = ValidAdjustToken();
        await factory.ProjectValidStateAsync(client, token, MagicLinkAllowedAction.Adjust,
            capabilityId, entryId);
        factory.Gateway.WithStream(Tenant().TenantId, entryId.Value);
        var command = new SubmitExternalTimeEntry(entryId,
            TimeEntryTargetReference.ForProject(Project()), Contributor(), ActivityId(),
            new DateOnly(2026, 6, 19), 60, BillableState.Billable,
            new ExternalContributionSource("supplier-api", "display-request"))
        {
            Comment = new TimeEntryComment("exact permitted note", TimeEntryCommentPolicy.SensitiveDefault with
            {
                ExternalConfirmationDisplay = TimesheetsCommentPolicyDecision.Allowed,
                RedactionRequirement = callerClaimsNoRedaction
                    ? TimesheetsCommentRedactionRequirement.NotRequired
                    : TimesheetsCommentRedactionRequirement.RequiredBeforeExternalDisclosure
            })
        };

        using HttpResponseMessage recorded = await factory.PostProtectedManagementAsync(client,
            "/api/timesheets/external-contributions/", command);
        recorded.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        TimeEntryRecorded stored = JsonSerializer.Deserialize<StoredTimeEntryRecorded>(
            factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value)
                .ShouldHaveSingleItem().Payload, JsonOptions).ShouldNotBeNull().Event;
        stored.Comment.ShouldNotBeNull().Policy.ServerPublicationVersion
            .ShouldBe(TimeEntryCommentPublicationIssuer.CurrentVersion);
        stored.Comment.Policy.ServerExternalConfirmationDisplay.ShouldBe(displaysComment
            ? TimesheetsCommentPolicyDecision.Allowed : TimesheetsCommentPolicyDecision.Excluded);
        stored.Comment.Policy.RedactionRequirement.ShouldBe(displaysComment
            ? TimesheetsCommentRedactionRequirement.NotRequired
            : TimesheetsCommentRedactionRequirement.RequiredBeforeExternalDisclosure);

        using HttpResponseMessage display = await client.GetAsync(
            $"/api/timesheets/magic-links/adjust?t={token}", TestContext.Current.CancellationToken);
        display.StatusCode.ShouldBe(HttpStatusCode.OK);
        MagicLinkAdjustmentDisplayResponse response = (await display.Content.ReadFromJsonAsync<
            MagicLinkAdjustmentDisplayResponse>(JsonOptions, TestContext.Current.CancellationToken))
            .ShouldNotBeNull();
        response.Comment.ShouldBe(displaysComment ? "exact permitted note" : null);
    }

    [Fact]
    public async Task AdjustmentHttpDisplayShowsClearedReplacementCommentThroughSecondLink()
    {
        ITimeEntryCommentPublicationAuthority authority = Substitute.For<ITimeEntryCommentPublicationAuthority>();
        authority.MayDisplayExternally(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryComment>())
            .Returns(true);
        authority.MayDisplayUnredactedExternally(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryComment>())
            .Returns(true);
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true,
            useMismatchedClaims: false, commentAuthority: authority);
        using HttpClient client = factory.CreateClient();
        var entryId = new TimeEntryId("entry-display-replacement");
        string firstToken = ValidAdjustToken();
        string secondToken = "valid-adjust-token-second";
        await factory.ProjectValidStateAsync(client, firstToken, MagicLinkAllowedAction.Adjust,
            new MagicLinkCapabilityId("capability-display-replacement-first"), entryId);
        await factory.ProjectValidStateAsync(client, secondToken, MagicLinkAllowedAction.Adjust,
            new MagicLinkCapabilityId("capability-display-replacement-second"), entryId);
        factory.Gateway.WithStream(Tenant().TenantId, entryId.Value);
        TimeEntryCommentPolicy submittedPolicy = TimeEntryCommentPolicy.SensitiveDefault;
        var recording = new SubmitExternalTimeEntry(entryId,
            TimeEntryTargetReference.ForProject(Project()), Contributor(), ActivityId(),
            new DateOnly(2026, 6, 19), 60, BillableState.Billable,
            new ExternalContributionSource("supplier-api", "replacement-record"))
        {
            Comment = new TimeEntryComment("previous note", submittedPolicy)
        };
        using HttpResponseMessage recorded = await factory.PostProtectedManagementAsync(client,
            "/api/timesheets/external-contributions/", recording);
        recorded.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        using HttpResponseMessage adjusted = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/adjust/submit?t={firstToken}",
            AdjustCommand() with { Comment = new TimeEntryComment("replacement note", submittedPolicy) },
            JsonOptions, TestContext.Current.CancellationToken);
        adjusted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        using HttpResponseMessage display = await client.GetAsync(
            $"/api/timesheets/magic-links/adjust?t={secondToken}", TestContext.Current.CancellationToken);
        display.StatusCode.ShouldBe(HttpStatusCode.OK);
        MagicLinkAdjustmentDisplayResponse response = (await display.Content.ReadFromJsonAsync<
            MagicLinkAdjustmentDisplayResponse>(JsonOptions, TestContext.Current.CancellationToken))
            .ShouldNotBeNull();
        response.Comment.ShouldBe("replacement note");
    }

    [Fact]
    public async Task ExternalRecordingRouteCommitsOwnerStateBeforeMagicLinkUse()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, useMismatchedClaims: false);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId("capability-record-route");
        var entryId = new TimeEntryId("entry-record-route");
        string token = ValidConfirmToken();
        await factory.ProjectValidStateAsync(client, token, MagicLinkAllowedAction.Confirm, capabilityId, entryId);
        factory.Gateway.WithStream(Tenant().TenantId, entryId.Value);
        var command = new SubmitExternalTimeEntry(
            entryId, TimeEntryTargetReference.ForProject(Project()), Contributor(), ActivityId(),
            new DateOnly(2026, 6, 19), 60, BillableState.Billable,
            new ExternalContributionSource("supplier-api", "request-record-route"))
        {
            Comment = new TimeEntryComment("recorded private note", TimeEntryCommentPolicy.SensitiveDefault with
            {
                ProjectionInclusion = TimesheetsCommentPolicyDecision.Allowed,
                ExternalConfirmationDisplay = TimesheetsCommentPolicyDecision.Allowed
            })
        };

        using HttpResponseMessage recorded = await factory.PostProtectedManagementAsync(
            client, "/api/timesheets/external-contributions/", command);
        factory.Gateway.SubmissionCount.ShouldBe(1);
        recorded.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        StreamReadEvent storedRecording = factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value)
            .ShouldHaveSingleItem();
        storedRecording.EventTypeName.ShouldBe(typeof(StoredTimeEntryRecorded).FullName);
        TimeEntryRecorded storedFacts = JsonSerializer.Deserialize<StoredTimeEntryRecorded>(
            storedRecording.Payload, JsonOptions).ShouldNotBeNull().Event;
        storedFacts.TimeEntryId.ShouldBe(command.TimeEntryId);
        storedFacts.Target.ShouldBe(command.Target);
        storedFacts.Contributor.ShouldBe(command.Contributor);
        storedFacts.ActivityTypeId.ShouldBe(command.ActivityTypeId);
        storedFacts.ActivityTypeScope.ShouldBe(ActivityTypeScope.Tenant);
        storedFacts.ServiceDate.ShouldBe(command.ServiceDate);
        storedFacts.DurationMinutes.ShouldBe(command.DurationMinutes);
        storedFacts.BillableState.ShouldBe(command.BillableState);
        storedFacts.ExternalSource.ShouldBe(command.Source);
        storedFacts.Comment.ShouldNotBeNull().Text.ShouldBe("recorded private note");
        storedFacts.Comment.Policy.ServerPublicationVersion
            .ShouldBe(TimeEntryCommentPublicationIssuer.CurrentVersion);
        storedFacts.Comment.Policy.ProjectionInclusion.ShouldBe(TimesheetsCommentPolicyDecision.PolicyRequired);
        storedFacts.Comment.Policy.ExternalConfirmationDisplay.ShouldBe(TimesheetsCommentPolicyDecision.Excluded);
        storedFacts.Comment.Policy.ServerExternalConfirmationDisplay.ShouldBe(TimesheetsCommentPolicyDecision.Excluded);

        using HttpResponseMessage described = await client.GetAsync(
            $"/api/timesheets/magic-links/confirm?t={token}", TestContext.Current.CancellationToken);
        described.StatusCode.ShouldBe(HttpStatusCode.OK);
        using HttpResponseMessage used = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={token}",
            new ConfirmTimeThroughMagicLink(), JsonOptions, TestContext.Current.CancellationToken);
        used.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        using HttpResponseMessage replay = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={token}",
            new ConfirmTimeThroughMagicLink(), JsonOptions, TestContext.Current.CancellationToken);
        replay.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value)
            .Count(item => item.EventTypeName == typeof(StoredTimeEntryRecorded).FullName).ShouldBe(1);
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value)
            .Count(item => item.EventTypeName == typeof(StoredTimeEntryConfirmed).FullName).ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExternalRecordingRecoversCommittedBatchAfterSubmitResponseFault(bool submitForApproval)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, useMismatchedClaims: false,
            externalContributionPolicy: new ExternalContributionPolicyOptions
            {
                InitialApprovalState = submitForApproval ? TimeEntryApprovalState.Submitted : TimeEntryApprovalState.Draft
            });
        using HttpClient client = factory.CreateClient();
        var entryId = new TimeEntryId($"entry-record-lost-response-{submitForApproval}");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            new MagicLinkCapabilityId($"capability-record-lost-response-{submitForApproval}"), entryId);
        factory.Gateway.WithStream(Tenant().TenantId, entryId.Value);
        factory.Gateway.AfterStoredBeforeResponseAsync = (request, _) =>
            request.CommandType == typeof(CommitTimeEntryRecord).FullName
                ? Task.FromException(new HttpRequestException("submit response lost after commit"))
                : Task.CompletedTask;
        SubmitExternalTimeEntry command = RecordingCommand(entryId, "request-lost-record-response");

        using HttpResponseMessage first = await SubmitRecordingAsync(factory, client, command);
        first.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        StreamReadEvent[] stored = factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value);
        stored.Length.ShouldBe(submitForApproval ? 2 : 1);
        stored[0].EventTypeName.ShouldBe(typeof(StoredTimeEntryRecorded).FullName);
        TimeEntryRecorded recorded = JsonSerializer.Deserialize<StoredTimeEntryRecorded>(
            stored[0].Payload, JsonOptions).ShouldNotBeNull().Event;
        recorded.TimeEntryId.ShouldBe(command.TimeEntryId);
        recorded.Target.ShouldBe(command.Target);
        recorded.Contributor.ShouldBe(command.Contributor);
        recorded.ActivityTypeId.ShouldBe(command.ActivityTypeId);
        recorded.ServiceDate.ShouldBe(command.ServiceDate);
        recorded.DurationMinutes.ShouldBe(command.DurationMinutes);
        recorded.BillableState.ShouldBe(command.BillableState);
        recorded.ExternalSource.ShouldBe(command.Source);
        recorded.ApprovalState.ShouldBe(TimeEntryApprovalState.Draft);
        if (submitForApproval)
        {
            stored[1].EventTypeName.ShouldBe(typeof(StoredTimeEntrySubmitted).FullName);
            TimeEntrySubmitted submitted = JsonSerializer.Deserialize<StoredTimeEntrySubmitted>(
                stored[1].Payload, JsonOptions).ShouldNotBeNull().Event;
            submitted.TimeEntryId.ShouldBe(command.TimeEntryId);
            submitted.Tenant.ShouldBe(Tenant());
            submitted.Submitter.ShouldBe(Contributor());
            submitted.TimeEntrySubmissionId.Value.ShouldBe(command.Source.ExternalRequestId);
            submitted.ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
        }

        using HttpResponseMessage retry = await SubmitRecordingAsync(factory, client, command);
        retry.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        factory.Gateway.SubmissionCount.ShouldBe(1);
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value).ShouldBe(stored);
    }

    [Fact]
    public async Task ExternalRecordingRetriesAndConcurrentSubmissionsCommitOneRecording()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, useMismatchedClaims: false);
        using HttpClient client = factory.CreateClient();
        var entryId = new TimeEntryId("entry-record-concurrent");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            new MagicLinkCapabilityId("capability-record-concurrent"), entryId);
        factory.Gateway.WithStream(Tenant().TenantId, entryId.Value);
        SubmitExternalTimeEntry command = RecordingCommand(entryId, "request-concurrent");

        Task<HttpResponseMessage> first = SubmitRecordingAsync(factory, client, command);
        Task<HttpResponseMessage> second = SubmitRecordingAsync(factory, client, command);
        using HttpResponseMessage firstResponse = await first;
        using HttpResponseMessage secondResponse = await second;
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        using HttpResponseMessage retry = await SubmitRecordingAsync(factory, client, command);
        retry.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value)
            .Count(item => item.EventTypeName == typeof(StoredTimeEntryRecorded).FullName).ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnverifiedRecordingStatusOrReceiptDeniesAndRetryDoesNotAppend(bool receiptMismatch)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, useMismatchedClaims: false);
        using HttpClient client = factory.CreateClient();
        var entryId = new TimeEntryId(receiptMismatch ? "entry-record-receipt" : "entry-record-status");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            new MagicLinkCapabilityId(receiptMismatch ? "capability-record-receipt" : "capability-record-status"), entryId);
        factory.Gateway.WithStream(Tenant().TenantId, entryId.Value);
        if (receiptMismatch)
        {
            factory.Gateway.StoredPayloadTransform = payload => payload is StoredTimeEntryRecorded stored
                ? stored with { Event = stored.Event with { DurationMinutes = 99 } }
                : payload;
        }
        else
        {
            factory.Gateway.OverrideSuccessfulStatus = CommandStatus.PublishFailed;
        }

        SubmitExternalTimeEntry command = RecordingCommand(entryId, "request-uncertain");
        using HttpResponseMessage first = await SubmitRecordingAsync(factory, client, command);
        using HttpResponseMessage retry = await SubmitRecordingAsync(factory, client, command);
        first.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        retry.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldBe(await retry.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        factory.Gateway.SubmissionCount.ShouldBe(1);
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value)
            .Count(item => item.EventTypeName == typeof(StoredTimeEntryRecorded).FullName).ShouldBe(1);
    }

    [Theory]
    [InlineData("unauthorized")]
    [InlineData("unavailable-catalog")]
    [InlineData("conflict")]
    public async Task RejectedRecordingDoesNotReplaceExistingOwnerEvidence(string scenario)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, useMismatchedClaims: false);
        using HttpClient client = factory.CreateClient();
        var entryId = new TimeEntryId($"entry-record-{scenario}");
        if (scenario == "unavailable-catalog")
        {
            await factory.ProjectRejectedCatalogStateAsync(client, ValidConfirmToken(), "missing-sequence");
        }
        else
        {
            await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
                new MagicLinkCapabilityId($"capability-record-{scenario}"), entryId);
        }

        if (scenario != "conflict")
        {
            factory.Gateway.WithStream(Tenant().TenantId, entryId.Value);
        }

        SubmitExternalTimeEntry command = RecordingCommand(entryId, $"request-{scenario}");
        if (scenario == "unauthorized")
        {
            command = command with { Contributor = UnauthorizedContributor() };
        }

        using HttpResponseMessage response = await SubmitRecordingAsync(factory, client, command);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldNotContain(entryId.Value, Case.Insensitive);
        body.ShouldNotContain(command.Source.ExternalRequestId, Case.Insensitive);
        foreach (LogRecord record in factory.Logs.Records)
        {
            string diagnostics = $"{record.Category} {record.Message} {string.Join(' ', record.State)}";
            diagnostics.ShouldNotContain(entryId.Value, Case.Insensitive);
            diagnostics.ShouldNotContain(command.Source.ExternalRequestId, Case.Insensitive);
        }
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value)
            .Count(item => item.EventTypeName == typeof(StoredTimeEntryRecorded).FullName).ShouldBe(0);
    }

    private static SubmitExternalTimeEntry RecordingCommand(TimeEntryId entryId, string requestId)
        => new(entryId, TimeEntryTargetReference.ForProject(Project()), Contributor(), ActivityId(),
            new DateOnly(2026, 6, 19), 60, BillableState.Billable,
            new ExternalContributionSource("supplier-api", requestId));

    private static Task<HttpResponseMessage> SubmitRecordingAsync(
        MagicLinkHttpBoundaryFactory factory,
        HttpClient client,
        SubmitExternalTimeEntry command)
        => factory.PostProtectedManagementAsync(
            client, "/api/timesheets/external-contributions/", command);

    [Fact]
    public async Task SubmittedPolicyCommitsRecordAndSubmissionAsOneVerifiedBatch()
    {
        using MagicLinkHttpBoundaryFactory factory = new(
            useConcreteLoader: true,
            useMismatchedClaims: false,
            externalContributionPolicy: new ExternalContributionPolicyOptions
            {
                InitialApprovalState = TimeEntryApprovalState.Submitted
            });
        using HttpClient client = factory.CreateClient();
        var entryId = new TimeEntryId("entry-record-submitted");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            new MagicLinkCapabilityId("capability-record-submitted"), entryId);
        factory.Gateway.WithStream(Tenant().TenantId, entryId.Value);

        SubmitExternalTimeEntry command = RecordingCommand(entryId, "request-submitted");
        using HttpResponseMessage response = await SubmitRecordingAsync(factory, client, command);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value)
            .Select(static item => item.EventTypeName).ShouldBe([
                typeof(StoredTimeEntryRecorded).FullName!,
                typeof(StoredTimeEntrySubmitted).FullName!
            ]);
        TimeEntrySubmitted submitted = JsonSerializer.Deserialize<StoredTimeEntrySubmitted>(
            factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value)[1].Payload,
            JsonOptions).ShouldNotBeNull().Event;
        submitted.TimeEntryId.ShouldBe(entryId);
        submitted.Tenant.ShouldBe(Tenant());
        submitted.Submitter.ShouldBe(Contributor());
        submitted.ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);

        // The HTTP route supplies a fresh UTC instant on every request. A completed retry
        // must verify the existing batch without requiring that new instant to match it.
        await Task.Delay(TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);
        using HttpResponseMessage retry = await SubmitRecordingAsync(factory, client, command);
        retry.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        factory.Gateway.SubmissionCount.ShouldBe(1);
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value)
            .Select(static item => item.EventTypeName).ShouldBe([
                typeof(StoredTimeEntryRecorded).FullName!,
                typeof(StoredTimeEntrySubmitted).FullName!
            ]);
    }

    [Fact]
    public async Task SubmittedPolicyDeniesNewCommandWithTamperedSubmissionInstant()
    {
        using MagicLinkHttpBoundaryFactory factory = new(
            useConcreteLoader: true,
            useMismatchedClaims: false,
            externalContributionPolicy: new ExternalContributionPolicyOptions
            {
                InitialApprovalState = TimeEntryApprovalState.Submitted
            });
        using HttpClient client = factory.CreateClient();
        var entryId = new TimeEntryId("entry-record-submitted-tampered");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            new MagicLinkCapabilityId("capability-record-submitted-tampered"), entryId);
        factory.Gateway.WithStream(Tenant().TenantId, entryId.Value);
        factory.Gateway.StoredPayloadTransform = payload => payload is StoredTimeEntrySubmitted stored
            ? stored with { Event = stored.Event with { SubmittedAtUtc = stored.Event.SubmittedAtUtc.AddMinutes(1) } }
            : payload;

        using HttpResponseMessage response = await SubmitRecordingAsync(
            factory, client, RecordingCommand(entryId, "request-submitted-tampered"));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.Gateway.SubmissionCount.ShouldBe(1);
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value)
            .Select(static item => item.EventTypeName).ShouldBe([
                typeof(StoredTimeEntryRecorded).FullName!,
                typeof(StoredTimeEntrySubmitted).FullName!
            ]);
    }

    [Fact]
    public async Task ValidUseRequiresVerifiedOriginAtTheProcessorEntry()
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
    public async Task ValidManagementCommandRequiresVerifiedOriginAndActorAtProcessorEntry(bool revoke)
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
    public async Task ProtectedProcessRouteDispatchesToTheKeyedTimesheetsProcessor()
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
    public async Task EventStoreActorStateStoreCommitsPairAndReloadsItAfterActorRecreation(
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
        factory.Gateway.WithStream(Tenant().TenantId, entryId.Value);

        using IServiceScope scope = factory.Services.CreateScope();
        MagicLinkEventStoreDomainProcessor processor = scope.ServiceProvider
            .GetRequiredService<MagicLinkEventStoreDomainProcessor>();
        var stateManager = new InMemoryStateManager();
        var statusStore = new InMemoryCommandStatusStore();
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(call => processor.ProcessAsync(
                call.ArgAt<CommandEnvelope>(0), call.ArgAt<object?>(1), call.ArgAt<CancellationToken>(2)));

        AggregateActor actor = CreateMagicLinkActor(entryId, stateManager, statusStore, invoker);
        var recording = new CommitTimeEntryRecord(
            new SubmitExternalTimeEntry(
                entryId, TimeEntryTargetReference.ForProject(Project()), Contributor(), ActivityId(),
                new DateOnly(2026, 6, 19), 60, BillableState.Billable,
                new ExternalContributionSource("supplier-api", "request-actor-seed")),
            Tenant(), Contributor(), ObservedAtUtc);
        CommandEnvelope seed = ActorCommand(
            typeof(CommitTimeEntryRecord).FullName!, "actor-seed", entryId,
            JsonSerializer.SerializeToUtf8Bytes(recording, JsonOptions)) with
        {
            UserId = Contributor().PartyId,
            Extensions = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "timesheets",
                [EventStoreGatewayVerifiedOrigin.ActorExtensionKey] = Contributor().PartyId
            }
        };
        (await actor.ProcessCommandAsync(seed, TestContext.Current.CancellationToken)).Accepted.ShouldBeTrue();
        (await actor.GetEventsAsync(0)).ShouldHaveSingleItem().EventTypeName.ShouldBe(typeof(StoredTimeEntryRecorded).FullName);

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EventStoreProcessorRejectsRecordingWithoutMatchingVerifiedActor(bool mismatched)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var entryId = new TimeEntryId(mismatched ? "entry-actor-mismatch" : "entry-actor-missing");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            new MagicLinkCapabilityId(mismatched ? "capability-actor-mismatch" : "capability-actor-missing"), entryId);
        factory.Gateway.WithStream(Tenant().TenantId, entryId.Value);
        using IServiceScope scope = factory.Services.CreateScope();
        MagicLinkEventStoreDomainProcessor processor = scope.ServiceProvider
            .GetRequiredService<MagicLinkEventStoreDomainProcessor>();
        var intent = new CommitTimeEntryRecord(
            new SubmitExternalTimeEntry(entryId, TimeEntryTargetReference.ForProject(Project()), Contributor(), ActivityId(),
                new DateOnly(2026, 6, 19), 60, BillableState.Billable,
                new ExternalContributionSource("supplier-api", "actor-metadata")),
            Tenant(), Contributor(), ObservedAtUtc);
        CommandEnvelope envelope = ActorCommand(typeof(CommitTimeEntryRecord).FullName!, "actor-metadata", entryId,
            JsonSerializer.SerializeToUtf8Bytes(intent, JsonOptions)) with
        {
            UserId = Contributor().PartyId,
            Extensions = mismatched
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "timesheets",
                    [EventStoreGatewayVerifiedOrigin.ActorExtensionKey] = Operator().PartyId
                }
                : new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "timesheets"
                }
        };

        DomainResult result = await processor.ProcessAsync(envelope, null, TestContext.Current.CancellationToken);

        result.IsRejection.ShouldBeTrue();
        result.Events.OfType<StoredTimeEntryRecorded>().ShouldBeEmpty();
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value).ShouldBeEmpty();
    }

    [Fact]
    public async Task EventStoreProcessorRejectsForgedResolvedTokenHashBeforeMutation()
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
            scope.ServiceProvider.GetRequiredService<ExternalContributionCommandService>(),
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
    public async Task ProposedEntryTerminalMarkerSurvivesActorRecreation(bool expire)
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
            capabilityId, Tenant(), entryId, Operator(),
            expire ? MagicLinkTransitionAction.Expire : MagicLinkTransitionAction.Revoke,
            new MagicLinkAuditMetadata("timesheets", "proposed-terminal"), ObservedAtUtc);
        CommandEnvelope transition = ActorCommand(
            typeof(CommitMagicLinkTransition).FullName!, "proposed-transition", entryId,
            JsonSerializer.SerializeToUtf8Bytes(intent, JsonOptions)) with
        {
            UserId = Operator().PartyId,
            Extensions = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "timesheets",
                [EventStoreGatewayVerifiedOrigin.ActorExtensionKey] = Operator().PartyId
            }
        };

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

        var recording = new CommitTimeEntryRecord(
            new SubmitExternalTimeEntry(entryId, TimeEntryTargetReference.ForProject(Project()), Contributor(), ActivityId(),
                new DateOnly(2026, 6, 19), 60, BillableState.Billable,
                new ExternalContributionSource("supplier-api", "after-terminal")),
            Tenant(), Contributor(), ObservedAtUtc);
        CommandEnvelope record = ActorCommand(typeof(CommitTimeEntryRecord).FullName!, "record-after-terminal", entryId,
            JsonSerializer.SerializeToUtf8Bytes(recording, JsonOptions)) with
        {
            UserId = Contributor().PartyId,
            Extensions = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "timesheets",
                [EventStoreGatewayVerifiedOrigin.ActorExtensionKey] = Contributor().PartyId
            }
        };
        (await restarted.ProcessCommandAsync(record, TestContext.Current.CancellationToken)).Accepted.ShouldBeTrue();
        AggregateActor afterRecording = CreateMagicLinkActor(entryId, stateManager, statusStore, invoker);
        (await afterRecording.GetEventsAsync(0)).Select(static item => item.EventTypeName).ShouldBe([
            expire ? typeof(StoredMagicLinkExpired).FullName! : typeof(StoredMagicLinkRevoked).FullName!,
            typeof(MagicLinkCommitRejected).FullName!,
            typeof(StoredTimeEntryRecorded).FullName!
        ]);
        var use = new CommitMagicLinkUse(capabilityId, Tenant(), entryId,
            new MagicLinkTokenHash(Hash(ValidConfirmToken())), MagicLinkUseAction.Confirm, null, ObservedAtUtc);
        CommandEnvelope useEnvelope = ActorCommand(typeof(CommitMagicLinkUse).FullName!,
            "use-after-terminal-record", entryId, JsonSerializer.SerializeToUtf8Bytes(use, JsonOptions));
        (await afterRecording.ProcessCommandAsync(useEnvelope, TestContext.Current.CancellationToken))
            .Accepted.ShouldBeFalse();
        (await afterRecording.GetEventsAsync(0)).Count(item => item.EventTypeName == typeof(StoredMagicLinkUsed).FullName)
            .ShouldBe(0);
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
    public async Task QueuedUseIsRejectedWhenProcessingClockPassesExpiry()
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
            scope.ServiceProvider.GetRequiredService<ExternalContributionCommandService>(),
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
    public async Task QueuedIssuanceIsRejectedWhenProcessingClockPassesExpiry()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using IServiceScope scope = factory.Services.CreateScope();
        var processor = new MagicLinkEventStoreDomainProcessor(
            scope.ServiceProvider.GetRequiredService<EventStoreMagicLinkConfirmationCapabilityStateLoader>(),
            scope.ServiceProvider.GetRequiredService<MagicLinkConfirmationCapabilityCommandService>(),
            scope.ServiceProvider.GetRequiredService<ExternalContributionCommandService>(),
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
            scope.ServiceProvider.GetRequiredService<ExternalContributionCommandService>(),
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
            scope.ServiceProvider.GetRequiredService<ExternalContributionCommandService>(),
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
            scope.ServiceProvider.GetRequiredService<ExternalContributionCommandService>(),
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
    public async Task TerminalOnlyCapabilityAndMalformedNestedIssueAreRejected()
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
        var revoked = new MagicLinkConfirmationCapabilityRevoked(
            capabilityId, Tenant(), Operator(), ObservedAtUtc,
            new MagicLinkAuditMetadata("timesheets", "orphan"));
        object[][] orphanHistories = [[revoked], [new StoredMagicLinkRevoked(revoked)]];

        foreach (object[] orphan in orphanHistories)
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
