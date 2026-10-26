using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks.Commands;
using Hexalith.Timesheets.Server.Policies;
using Hexalith.Timesheets.Runtime;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed partial class MagicLinkConfirmationHttpBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorkloadSubjectIsNeverAnImplicitPartyOrOutboundActorBinding(bool explicitParty)
    {
        const string workloadSubject = "workload:eventstore";
        List<Claim> claims = [new(ClaimTypes.NameIdentifier, workloadSubject),
            new(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "eventstore"),
            new("tenant_id", "tenant-a")];
        if (explicitParty)
        {
            claims.Add(new("party_id", workloadSubject));
        }
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, EventStoreWorkloadAuthenticationDefaults.WorkloadScheme))
            }
        };
        var context = new HttpContextTimesheetsTrustedContextAccessor(accessor);
        context.CurrentActor.ShouldBe(explicitParty ? new PartyReference(workloadSubject) : null);
        context.CurrentTenant.ShouldBe(new("tenant-a"));
        IWorkloadAssertionIssuer issuer = Substitute.For<IWorkloadAssertionIssuer>();
        issuer.CanBindResources.Returns(true);
        issuer.IssueAsync(Arg.Any<WorkloadAssertionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string?>("signed"));
        var source = new EventStoreGatewayWorkloadAssertionSource(issuer, accessor);

        await source.IssueAsync(EventStoreWorkloadOperations.GatewayCommandSubmit, "tenant-a", TestContext.Current.CancellationToken);

        await issuer.Received(1).IssueAsync(Arg.Is<WorkloadAssertionRequest>(request =>
            request.Bindings != null
            && request.Bindings.ContainsKey(EventStoreWorkloadAuthenticationDefaults.ActorBindingClaimType) == explicitParty
            && (!explicitParty || request.Bindings[EventStoreWorkloadAuthenticationDefaults.ActorBindingClaimType] == workloadSubject)),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CatalogReplacementAfterValidPreflightRefusesUseWithoutDomainEffects(bool adjust)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId($"catalog-race-{adjust}");
        var entryId = new TimeEntryId($"catalog-race-entry-{adjust}");
        string token = adjust ? ValidAdjustToken() : ValidConfirmToken();
        string route = adjust ? "adjust" : "confirm";
        await factory.ProjectValidStateAsync(client, token,
            adjust ? MagicLinkAllowedAction.Adjust : MagicLinkAllowedAction.Confirm, capabilityId, entryId);
        using HttpResponseMessage display = await client.GetAsync(
            $"/api/timesheets/magic-links/{route}?t={token}", TestContext.Current.CancellationToken);
        display.StatusCode.ShouldBe(HttpStatusCode.OK);
        bool changedAfterPreflight = false;
        factory.Gateway.BeforeSubmitAsync = async (request, cancellationToken) =>
        {
            request.CommandType.ShouldBe(typeof(CommitMagicLinkUse).FullName);
            string key = MagicLinkActivityTypeCatalogReadModelAddress.StateKey(Tenant());
            string store = MagicLinkActivityTypeCatalogReadModelAddress.StateStoreName;
            ReadModelEntry<ActivityTypeCatalogReadModel> current = await factory.Store
                .InspectSelectedAsync<ActivityTypeCatalogReadModel>(store, key, cancellationToken);
            var replacement = new ActivityTypeCatalogReadModel(
                [current.Value!.Items.Single() with { ActivityTypeId = new("other-active-type") }],
                ProjectionFreshnessMetadata.Fresh);
            (await factory.Store.MutateSelectedAsync(store, key, replacement, current.ETag!, cancellationToken)).ShouldBeTrue();
            changedAfterPreflight = true;
        };

        using HttpResponseMessage response = adjust
            ? await client.PostAsJsonAsync($"/api/timesheets/magic-links/{route}/submit?t={token}", AdjustCommand(), TestContext.Current.CancellationToken)
            : await client.PostAsJsonAsync($"/api/timesheets/magic-links/{route}/submit?t={token}", new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);
        CapturedFailure denial = await CaptureFailureAsync(response, "catalog-race", token);
        factory.Gateway.BeforeSubmitAsync = null;
        using HttpResponseMessage unknown = adjust
            ? await client.PostAsJsonAsync($"/api/timesheets/magic-links/{route}/submit?t=unknown-catalog-race", AdjustCommand(), TestContext.Current.CancellationToken)
            : await client.PostAsJsonAsync($"/api/timesheets/magic-links/{route}/submit?t=unknown-catalog-race", new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);
        CapturedFailure baseline = await CaptureFailureAsync(unknown, "unknown", "unknown-catalog-race");
        changedAfterPreflight.ShouldBeTrue();
        denial.NormalizedBody.ShouldBe(baseline.NormalizedBody);
        denial.Headers.ShouldBe(baseline.Headers);
        StreamReadEvent[] stored = factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value);
        stored.Count(item => item.EventTypeName == typeof(TimeEntryRecorded).FullName).ShouldBe(1);
        stored.ShouldNotContain(item => item.EventTypeName == typeof(StoredMagicLinkUsed).FullName
            || item.EventTypeName == typeof(StoredTimeEntryConfirmed).FullName
            || item.EventTypeName == typeof(StoredTimeEntryAdjusted).FullName);
    }

    [Fact]
    public async Task ComposedHostRegistersAuthenticatedResourceBindingAssertionSource()
    {
        IWorkloadAssertionIssuer issuer = Substitute.For<IWorkloadAssertionIssuer>();
        issuer.CanBindResources.Returns(true);
        issuer.IssueAsync(Arg.Any<WorkloadAssertionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string?>("host-signed"));
        using MagicLinkHttpBoundaryFactory factory = new(workloadAssertionIssuer: issuer);
        using HttpClient client = factory.CreateClient();
        IHttpContextAccessor accessor = factory.Services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("party_id", " "), new Claim(ClaimTypes.NameIdentifier, "operator-host")], "test"))
        };

        IEventStoreGatewayWorkloadAssertionSource source = factory.Services
            .GetRequiredService<IEventStoreGatewayWorkloadAssertionSource>();
        source.ShouldBeOfType<EventStoreGatewayWorkloadAssertionSource>();
        (await source.IssueAsync(EventStoreWorkloadOperations.GatewayCommandSubmit,
            "tenant-host", TestContext.Current.CancellationToken)).ShouldBe("host-signed");
        await issuer.Received(1).IssueAsync(Arg.Is<WorkloadAssertionRequest>(request =>
            request.Operation == EventStoreWorkloadOperations.GatewayCommandSubmit
            && request.Bindings != null
            && request.Bindings[EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] == "tenant-host"
            && request.Bindings[EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] == "timesheets"
            && request.Bindings[EventStoreWorkloadAuthenticationDefaults.ActorBindingClaimType] == "operator-host"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ComposedHostGatewaySendsBoundAssertionOnWorkloadSubmitRoute()
    {
        IWorkloadAssertionIssuer issuer = Substitute.For<IWorkloadAssertionIssuer>();
        issuer.CanBindResources.Returns(true);
        issuer.IssueAsync(Arg.Any<WorkloadAssertionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string?>("host-signed"));
        var transport = new CapturingWorkloadGatewayHandler();
        using MagicLinkHttpBoundaryFactory factory = new(
            workloadAssertionIssuer: issuer, workloadGatewayHandler: transport);
        using HttpClient client = factory.CreateClient();
        factory.Services.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("party_id", " "), new Claim(ClaimTypes.NameIdentifier, "operator-host")], "test"))
        };
        IEventStoreGatewayClient gateway = factory.Services.GetRequiredService<IEventStoreGatewayClient>();
        gateway.ShouldBeOfType<EventStoreGatewayClient>();

        SubmitCommandResponse response = await gateway.SubmitWorkloadCommandAsync(new SubmitCommandRequest(
            "host-message", "tenant-host", "timesheets", "capability-host", "test.command",
            JsonSerializer.SerializeToElement(new { value = 1 }), CorrelationId: "host-message"),
            TestContext.Current.CancellationToken);

        response.MessageId.ShouldBe("host-message");
        transport.Path.ShouldBe("/api/v1/commands/workload");
        transport.Assertion.ShouldBe("host-signed");
        await issuer.Received(1).IssueAsync(Arg.Is<WorkloadAssertionRequest>(request =>
            request.Operation == EventStoreWorkloadOperations.GatewayCommandSubmit
            && request.Bindings != null
            && request.Bindings[EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] == "tenant-host"
            && request.Bindings[EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] == "timesheets"
            && request.Bindings[EventStoreWorkloadAuthenticationDefaults.ActorBindingClaimType] == "operator-host"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProductionGatewayAssertionSourceBindsTenantDomainAndAuthenticatedActor()
    {
        IWorkloadAssertionIssuer issuer = Substitute.For<IWorkloadAssertionIssuer>();
        issuer.CanBindResources.Returns(true);
        issuer.IssueAsync(Arg.Any<WorkloadAssertionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string?>("signed"));
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("party_id", "operator-1")], "test"))
            }
        };
        var source = new EventStoreGatewayWorkloadAssertionSource(issuer, accessor);

        (await source.IssueAsync(EventStoreWorkloadOperations.GatewayCommandSubmit,
            "tenant-a", TestContext.Current.CancellationToken)).ShouldBe("signed");
        await issuer.Received(1).IssueAsync(Arg.Is<WorkloadAssertionRequest>(request =>
            request.Audience == "eventstore"
            && request.Operation == EventStoreWorkloadOperations.GatewayCommandSubmit
            && request.Bindings != null
            && request.Bindings[EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] == "tenant-a"
            && request.Bindings[EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] == "timesheets"
            && request.Bindings[EventStoreWorkloadAuthenticationDefaults.ActorBindingClaimType] == "operator-1"),
            Arg.Any<CancellationToken>());

        issuer.CanBindResources.Returns(false);
        (await source.IssueAsync(EventStoreWorkloadOperations.GatewayCommandStatus,
            "tenant-a", TestContext.Current.CancellationToken)).ShouldBeNull();
        await issuer.Received(1).IssueAsync(Arg.Any<WorkloadAssertionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProductionGatewayAssertionUsesNameIdentifierWhenPartyClaimIsBlank()
    {
        IWorkloadAssertionIssuer issuer = Substitute.For<IWorkloadAssertionIssuer>();
        issuer.CanBindResources.Returns(true);
        issuer.IssueAsync(Arg.Any<WorkloadAssertionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string?>("signed"));
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("party_id", " "), new Claim(ClaimTypes.NameIdentifier, "operator-2")], "test"))
            }
        };
        var source = new EventStoreGatewayWorkloadAssertionSource(issuer, accessor);

        (await source.IssueAsync(EventStoreWorkloadOperations.GatewayCommandSubmit,
            "tenant-a", TestContext.Current.CancellationToken)).ShouldBe("signed");
        await issuer.Received(1).IssueAsync(Arg.Is<WorkloadAssertionRequest>(request =>
            request.Bindings != null
            && request.Bindings[EventStoreWorkloadAuthenticationDefaults.ActorBindingClaimType] == "operator-2"),
            Arg.Any<CancellationToken>());
    }
    [Fact]
    public async Task ConfirmAndAdjustCommitUseWithMatchingEffectInTargetStream()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var confirmId = new MagicLinkCapabilityId("capability-persist-confirm");
        var confirmEntryId = new TimeEntryId("time-entry-persist-confirm");
        var adjustId = new MagicLinkCapabilityId("capability-persist-adjust");
        var adjustEntryId = new TimeEntryId("time-entry-persist-adjust");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm, confirmId, confirmEntryId);
        await factory.ProjectValidStateAsync(client, ValidAdjustToken(), MagicLinkAllowedAction.Adjust, adjustId, adjustEntryId);

        using HttpResponseMessage confirmed = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);
        using HttpResponseMessage adjusted = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/adjust/submit?t={ValidAdjustToken()}",
            AdjustCommand() with
            {
                Comment = new TimeEntryComment("adjusted private note", TimeEntryCommentPolicy.SensitiveDefault with
                {
                    ProjectionInclusion = TimesheetsCommentPolicyDecision.Allowed,
                    ExternalConfirmationDisplay = TimesheetsCommentPolicyDecision.Allowed
                })
            }, TestContext.Current.CancellationToken);

        confirmed.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        adjusted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        StreamReadEvent[] confirmEvents = factory.Gateway.StoredEvents(Tenant().TenantId, confirmEntryId.Value);
        StreamReadEvent[] adjustEvents = factory.Gateway.StoredEvents(Tenant().TenantId, adjustEntryId.Value);
        confirmEvents.Length.ShouldBe(3);
        adjustEvents.Length.ShouldBe(3);
        Persisted<StoredMagicLinkUsed>(confirmEvents[1]).Event.CapabilityId.ShouldBe(confirmId);
        Persisted<StoredTimeEntryConfirmed>(confirmEvents[2]).Event.TimeEntryId.ShouldBe(confirmEntryId);
        Persisted<StoredMagicLinkUsed>(adjustEvents[1]).Event.CapabilityId.ShouldBe(adjustId);
        TimeEntryAdjustedThroughMagicLink adjustment = Persisted<StoredTimeEntryAdjusted>(adjustEvents[2]).Event;
        adjustment.TimeEntryId.ShouldBe(adjustEntryId);
        adjustment.AdjustedValues.DurationMinutes.ShouldBe(AdjustCommand().DurationMinutes);
        adjustment.AdjustedValues.Comment.ShouldNotBeNull().Text.ShouldBe("adjusted private note");
        adjustment.AdjustedValues.Comment.Policy.ServerPublicationVersion
            .ShouldBe(TimeEntryCommentPublicationIssuer.CurrentVersion);
        adjustment.AdjustedValues.Comment.Policy.ProjectionInclusion
            .ShouldBe(TimesheetsCommentPolicyDecision.PolicyRequired);
        adjustment.AdjustedValues.Comment.Policy.ExternalConfirmationDisplay
            .ShouldBe(TimesheetsCommentPolicyDecision.Excluded);
        adjustment.AdjustedValues.Comment.Policy.ServerExternalConfirmationDisplay
            .ShouldBe(TimesheetsCommentPolicyDecision.Excluded);
        factory.Gateway.StoredEvents(Tenant().TenantId, confirmId.Value).Length.ShouldBe(1);
        factory.Gateway.StoredEvents(Tenant().TenantId, adjustId.Value).Length.ShouldBe(1);
    }

    [Fact]
    public async Task DelayedCompletedStatusStillAcceptsExactStoredBatch()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId("capability-delayed-completion");
        var entryId = new TimeEntryId("entry-delayed-completion");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            capabilityId, entryId);
        factory.Gateway.CompletedStatusReadsToDelay = 7;

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        factory.Gateway.CompletedStatusReadsToDelay.ShouldBe(0);
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value)
            .Count(static item => item.EventTypeName == typeof(StoredMagicLinkUsed).FullName).ShouldBe(1);
    }

    [Theory]
    [InlineData("capability")]
    [InlineData("tenant")]
    [InlineData("contributor")]
    [InlineData("timeEntry")]
    [InlineData("usedAt")]
    [InlineData("source")]
    [InlineData("outcome")]
    public async Task UseReceiptRejectsChangedStoredEventFields(string field)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId($"capability-use-receipt-{field}");
        var entryId = new TimeEntryId($"entry-use-receipt-{field}");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm, capabilityId, entryId);
        factory.Gateway.StoredPayloadTransform = payload => payload is StoredMagicLinkUsed stored
            ? new StoredMagicLinkUsed(field switch
            {
                "capability" => stored.Event with { CapabilityId = new MagicLinkCapabilityId("forged-capability") },
                "tenant" => stored.Event with { Tenant = OtherTenant() },
                "contributor" => stored.Event with { Contributor = OtherContributor() },
                "timeEntry" => stored.Event with { TimeEntryId = new TimeEntryId("forged-entry") },
                "usedAt" => stored.Event with { UsedAtUtc = stored.Event.UsedAtUtc.AddSeconds(1) },
                "source" => stored.Event with { Source = new MagicLinkAuditMetadata("forged", "source") },
                "outcome" => stored.Event with { OutcomeCategory = "adjusted" },
                _ => throw new ArgumentOutOfRangeException(nameof(field))
            }) : payload;

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value).Length.ShouldBe(3);
    }

    [Theory]
    [InlineData("timeEntry")]
    [InlineData("tenant")]
    [InlineData("contributor")]
    [InlineData("confirmedAt")]
    [InlineData("source")]
    public async Task ConfirmationReceiptRejectsChangedStoredEffectFields(string field)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId($"capability-confirm-receipt-{field}");
        var entryId = new TimeEntryId($"entry-confirm-receipt-{field}");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm, capabilityId, entryId);
        factory.Gateway.StoredPayloadTransform = payload => payload is StoredTimeEntryConfirmed stored
            ? new StoredTimeEntryConfirmed(field switch
            {
                "timeEntry" => stored.Event with { TimeEntryId = new TimeEntryId("forged-entry") },
                "tenant" => stored.Event with { Tenant = OtherTenant() },
                "contributor" => stored.Event with { Contributor = OtherContributor() },
                "confirmedAt" => stored.Event with { ConfirmedAtUtc = stored.Event.ConfirmedAtUtc.AddSeconds(1) },
                "source" => stored.Event with { Source = new ExternalContributionSource("forged", "source") },
                _ => throw new ArgumentOutOfRangeException(nameof(field))
            }) : payload;

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value).Length.ShouldBe(3);
    }

    [Fact]
    public async Task AcceptedMessageIdMismatchIsOpaqueEvenWhenEventsWereStored()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId("capability-accepted-message-mismatch");
        var entryId = new TimeEntryId("entry-accepted-message-mismatch");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm, capabilityId, entryId);
        factory.Gateway.AcceptedMessageIdTransform = _ => "forged-message-id";

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value).Length.ShouldBe(3);
    }

    [Fact]
    public async Task UseReceiptRejectsDuplicatePersistedEventIdentity()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId("capability-duplicate-event-id");
        var entryId = new TimeEntryId("entry-duplicate-event-id");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            capabilityId, entryId);
        factory.Gateway.StoredEventTransform = stored => stored with { MessageId = "same-event-id" };

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value).Length.ShouldBe(3);
    }

    public static TheoryData<string, int, string, string?> ReceiptAttributionCases()
    {
        var cases = new TheoryData<string, int, string, string?>();
        foreach ((string action, int position) in new (string, int)[]
            { ("issue", 0), ("revoke", 0), ("expire", 0), ("confirm", 0), ("confirm", 1), ("adjust", 0), ("adjust", 1) })
            foreach (string field in new[] { "correlation", "causation" })
                foreach (string? value in new string?[] { null, "foreign-command" })
                    cases.Add(action, position, field, value);
        return cases;
    }

    [Theory]
    [MemberData(nameof(ReceiptAttributionCases))]
    public async Task EveryReceiptRequiresSubmittedCommandCorrelationAndCausation(
        string action, int position, string field, string? value)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, useMismatchedClaims: false);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId("capability-receipt-attribution");
        var entryId = new TimeEntryId("entry-receipt-attribution");
        bool adjust = action == "adjust";
        string token = adjust ? ValidAdjustToken() : ValidConfirmToken();
        await factory.ProjectValidStateAsync(client, token,
            adjust ? MagicLinkAllowedAction.Adjust : MagicLinkAllowedAction.Confirm, capabilityId, entryId);
        if (action == "expire")
        {
            StreamReadEvent seed = factory.Gateway.StoredEvents(Tenant().TenantId, capabilityId.Value).Single();
            MagicLinkConfirmationCapabilityIssued issued = Persisted<MagicLinkConfirmationCapabilityIssued>(seed);
            factory.Gateway.WithStream(Tenant().TenantId, capabilityId.Value, seed with
            {
                Payload = JsonSerializer.SerializeToUtf8Bytes(issued with { ExpiresAtUtc = ObservedAtUtc }, JsonOptions)
            });
        }
        int eventPosition = 0;
        factory.Gateway.StoredEventTransform = stored => eventPosition++ == position
            ? field == "correlation" ? stored with { CorrelationId = value! } : stored with { CausationId = value }
            : stored;
        using HttpResponseMessage response = action switch
        {
            "issue" => await factory.PostProtectedManagementAsync(client, "/api/timesheets/magic-links/confirmation-capabilities",
                new IssueMagicLinkConfirmationCapability(new("issued-receipt-attribution"),
                    new(Contributor(), TimeEntryTargetReference.ForProject(Project()), ActivityId(),
                        new("proposed-receipt-attribution"), MagicLinkTargetKind.ProposedTimeEntry),
                    MagicLinkAllowedAction.Confirm, ObservedAtUtc.AddDays(1), new("timesheets", "issue"))),
            "revoke" => await factory.PostProtectedManagementAsync(client,
                $"/api/timesheets/magic-links/confirmation-capabilities/{capabilityId.Value}/revoke",
                new RevokeMagicLinkConfirmationCapability(capabilityId, new("timesheets", "revoke"))),
            "expire" => await factory.PostProtectedManagementAsync(client,
                $"/api/timesheets/magic-links/confirmation-capabilities/{capabilityId.Value}/expire",
                new ExpireMagicLinkConfirmationCapability(capabilityId, new("timesheets", "expire"))),
            "adjust" => await client.PostAsJsonAsync($"/api/timesheets/magic-links/adjust/submit?t={token}",
                AdjustCommand(), TestContext.Current.CancellationToken),
            _ => await client.PostAsJsonAsync($"/api/timesheets/magic-links/confirm/submit?t={token}",
                new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken)
        };
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        StreamReadEvent[] stored = factory.Gateway.StoredEvents(Tenant().TenantId,
            action == "issue" ? "issued-receipt-attribution" : entryId.Value);
        stored.Length.ShouldBe(action == "issue" ? 1 : action is "revoke" or "expire" ? 2 : 3);
        StreamReadEvent changed = stored[action == "issue" ? position : position + 1];
        (field == "correlation" ? changed.CorrelationId : changed.CausationId).ShouldBe(value);
        if (action is "confirm" or "adjust")
        {
            CapturedFailure denial = await CaptureFailureAsync(response, "receipt", token);
            using HttpResponseMessage unknown = await client.PostAsJsonAsync(
                $"/api/timesheets/magic-links/{action}/submit?t=unknown-attribution-token",
                adjust ? (object)AdjustCommand() : new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);
            CapturedFailure baseline = await CaptureFailureAsync(unknown, "unknown", "unknown-attribution-token");
            denial.NormalizedBody.ShouldBe(baseline.NormalizedBody);
            denial.Headers.ShouldBe(baseline.Headers);
        }
    }

    [Theory]
    [InlineData("target")]
    [InlineData("contributor")]
    [InlineData("activity")]
    [InlineData("scope")]
    [InlineData("date")]
    [InlineData("duration")]
    [InlineData("billable")]
    [InlineData("category")]
    [InlineData("aiMetrics")]
    [InlineData("comment")]
    public async Task AdjustmentReceiptRequiresEveryTrustedPriorValue(string field)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId($"capability-prior-{field}");
        var entryId = new TimeEntryId($"entry-prior-{field}");
        await factory.ProjectValidStateAsync(client, ValidAdjustToken(), MagicLinkAllowedAction.Adjust, capabilityId, entryId);
        factory.Gateway.StoredPayloadTransform = payload => payload is StoredTimeEntryAdjusted stored
            ? stored with { Event = stored.Event with { PreviousValues = field switch
            {
                "target" => stored.Event.PreviousValues with { Target = TimeEntryTargetReference.ForProject(new("other-project")) },
                "contributor" => stored.Event.PreviousValues with { Contributor = OtherContributor() },
                "activity" => stored.Event.PreviousValues with { ActivityTypeId = new("other-activity") },
                "scope" => stored.Event.PreviousValues with { ActivityTypeScope = ActivityTypeScope.Project },
                "date" => stored.Event.PreviousValues with { ServiceDate = stored.Event.PreviousValues.ServiceDate.AddDays(1) },
                "duration" => stored.Event.PreviousValues with { DurationMinutes = stored.Event.PreviousValues.DurationMinutes + 1 },
                "billable" => stored.Event.PreviousValues with { BillableState = BillableState.NonBillable },
                "category" => stored.Event.PreviousValues with { ContributorCategory = ContributorCategory.Employee },
                "aiMetrics" => stored.Event.PreviousValues with { AiMetrics = AiEffortMetrics.Unavailable },
                "comment" => stored.Event.PreviousValues with { Comment = new("Forged prior evidence", TimeEntryCommentPolicy.SensitiveDefault) },
                _ => throw new ArgumentOutOfRangeException(nameof(field))
            } } } : payload;
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/adjust/submit?t={ValidAdjustToken()}", AdjustCommand(), TestContext.Current.CancellationToken);
        CapturedFailure denial = await CaptureFailureAsync(response, field, ValidAdjustToken());
        using HttpResponseMessage unknown = await client.PostAsJsonAsync(
            "/api/timesheets/magic-links/adjust/submit?t=unknown-prior-token", AdjustCommand(), TestContext.Current.CancellationToken);
        CapturedFailure baseline = await CaptureFailureAsync(unknown, "unknown", "unknown-prior-token");
        denial.NormalizedBody.ShouldBe(baseline.NormalizedBody);
        denial.Headers.ShouldBe(baseline.Headers);
        StreamReadEvent[] persisted = factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value);
        persisted.Length.ShouldBe(3);
        TimeEntryRecorded recording = Persisted<TimeEntryRecorded>(persisted[0]);
        TimeEntryAdjustedThroughMagicLink adjustment = Persisted<StoredTimeEntryAdjusted>(persisted[2]).Event;
        TimeEntryCorrectionValues trusted = new(recording.Target, recording.Contributor, recording.ActivityTypeId,
            recording.ServiceDate, recording.DurationMinutes, recording.BillableState, recording.ContributorCategory, recording.AiMetrics)
            { ActivityTypeScope = recording.ActivityTypeScope, Comment = recording.Comment };
        adjustment.PreviousValues.ShouldNotBe(trusted);
    }

    [Theory]
    [InlineData("matching")]
    [InlineData("removed")]
    [InlineData("text")]
    [InlineData("policy")]
    public async Task AdjustmentReceiptVerifiesExistingPriorComment(string change)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId($"capability-prior-comment-{change}");
        var entryId = new TimeEntryId($"entry-prior-comment-{change}");
        await factory.ProjectValidStateAsync(client, ValidAdjustToken(), MagicLinkAllowedAction.Adjust, capabilityId, entryId);
        var priorComment = new TimeEntryComment("existing private evidence", TimeEntryCommentPolicy.SensitiveDefault);
        StreamReadEvent seed = factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value).Single();
        TimeEntryRecorded recording = Persisted<TimeEntryRecorded>(seed) with { Comment = priorComment };
        factory.Gateway.WithStream(Tenant().TenantId, entryId.Value, seed with
        {
            Payload = JsonSerializer.SerializeToUtf8Bytes(recording, JsonOptions)
        });
        TimeEntryComment? storedComment = change switch
        {
            "removed" => null,
            "text" => new TimeEntryComment("forged private evidence", priorComment.Policy),
            "policy" => new TimeEntryComment(priorComment.Text, priorComment.Policy with { ExportInclusion = TimesheetsCommentPolicyDecision.Allowed }),
            _ => priorComment
        };
        factory.Gateway.StoredPayloadTransform = payload => payload is StoredTimeEntryAdjusted stored
            ? stored with { Event = stored.Event with { PreviousValues = stored.Event.PreviousValues with { Comment = storedComment } } }
            : payload;
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/adjust/submit?t={ValidAdjustToken()}", AdjustCommand(), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(change == "matching" ? HttpStatusCode.Accepted : HttpStatusCode.Forbidden);
        StreamReadEvent[] persisted = factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value);
        persisted.Length.ShouldBe(3);
        Persisted<TimeEntryRecorded>(persisted[0]).ShouldBe(recording);
        TimeEntryCorrectionValues trusted = new(recording.Target, recording.Contributor, recording.ActivityTypeId,
            recording.ServiceDate, recording.DurationMinutes, recording.BillableState, recording.ContributorCategory, recording.AiMetrics)
            { ActivityTypeScope = recording.ActivityTypeScope, Comment = priorComment };
        TimeEntryCorrectionValues storedPrior = Persisted<StoredTimeEntryAdjusted>(persisted[2]).Event.PreviousValues;
        storedPrior.ShouldBe(trusted with { Comment = storedComment });
        if (change == "matching")
        {
            storedPrior.ShouldBe(trusted);
        }
        else
        {
            storedPrior.ShouldNotBe(trusted);
            CapturedFailure denial = await CaptureFailureAsync(response, change, ValidAdjustToken());
            using HttpResponseMessage unknown = await client.PostAsJsonAsync(
                "/api/timesheets/magic-links/adjust/submit?t=unknown-prior-comment-token", AdjustCommand(), TestContext.Current.CancellationToken);
            CapturedFailure baseline = await CaptureFailureAsync(unknown, "unknown", "unknown-prior-comment-token");
            denial.NormalizedBody.ShouldBe(baseline.NormalizedBody);
            denial.Headers.ShouldBe(baseline.Headers);
        }
    }

    [Theory]
    [InlineData("text")]
    [InlineData("serverProjection")]
    [InlineData("serverDisplay")]
    public async Task AdjustmentReceiptRejectsAPersistedCommentDifferentFromTheServerDecision(string field)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId($"capability-adjust-comment-{field}");
        var entryId = new TimeEntryId($"entry-adjust-comment-{field}");
        await factory.ProjectValidStateAsync(client, ValidAdjustToken(), MagicLinkAllowedAction.Adjust,
            capabilityId, entryId);
        factory.Gateway.StoredPayloadTransform = payload => payload is StoredTimeEntryAdjusted stored
            ? new StoredTimeEntryAdjusted(stored.Event with
            {
                AdjustedValues = stored.Event.AdjustedValues with
                {
                    Comment = field switch
                    {
                        "text" => new TimeEntryComment(
                            "forged private note", stored.Event.AdjustedValues.Comment!.Policy),
                        "serverProjection" => new TimeEntryComment(
                            stored.Event.AdjustedValues.Comment!.Text,
                            stored.Event.AdjustedValues.Comment!.Policy with
                            {
                                ProjectionInclusion = TimesheetsCommentPolicyDecision.Allowed,
                                ServerProjectionInclusion = TimesheetsCommentPolicyDecision.Allowed
                            }),
                        "serverDisplay" => new TimeEntryComment(
                            stored.Event.AdjustedValues.Comment!.Text,
                            stored.Event.AdjustedValues.Comment!.Policy with
                            {
                                ExternalConfirmationDisplay = TimesheetsCommentPolicyDecision.Allowed,
                                ServerExternalConfirmationDisplay = TimesheetsCommentPolicyDecision.Allowed
                            }),
                        _ => throw new ArgumentOutOfRangeException(nameof(field))
                    }
                }
            }) : payload;

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/adjust/submit?t={ValidAdjustToken()}",
            AdjustCommand() with
            {
                Comment = new TimeEntryComment("adjusted private note", TimeEntryCommentPolicy.SensitiveDefault with
                {
                    ProjectionInclusion = TimesheetsCommentPolicyDecision.Allowed,
                    ExternalConfirmationDisplay = TimesheetsCommentPolicyDecision.Allowed
                })
            }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value).Length.ShouldBe(3);
    }

    [Fact]
    public async Task AdjustmentReceiptKeepsThePreSubmitCommentDecisionWhenAuthorityChanges()
    {
        bool allowPublication = false;
        ITimeEntryCommentPublicationAuthority authority = Substitute.For<ITimeEntryCommentPublicationAuthority>();
        authority.MayPublish(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryComment>())
            .Returns(_ => allowPublication);
        authority.MayDisplayExternally(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryComment>())
            .Returns(false);
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, commentAuthority: authority);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId("capability-adjust-authority-change");
        var entryId = new TimeEntryId("entry-adjust-authority-change");
        await factory.ProjectValidStateAsync(client, ValidAdjustToken(), MagicLinkAllowedAction.Adjust,
            capabilityId, entryId);
        factory.Gateway.StoredPayloadTransform = payload =>
        {
            if (payload is StoredTimeEntryAdjusted) allowPublication = true;
            return payload;
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/adjust/submit?t={ValidAdjustToken()}",
            AdjustCommand() with
            {
                Comment = new TimeEntryComment("private adjustment", TimeEntryCommentPolicy.SensitiveDefault)
            }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        allowPublication.ShouldBeTrue();
        TimeEntryAdjustedThroughMagicLink adjusted = Persisted<StoredTimeEntryAdjusted>(
            factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value)[2]).Event;
        adjusted.AdjustedValues.Comment.ShouldNotBeNull().Policy.ServerProjectionInclusion
            .ShouldBe(TimesheetsCommentPolicyDecision.PolicyRequired);
    }

    [Theory]
    [InlineData("message")]
    [InlineData("count")]
    [InlineData("sequence")]
    [InlineData("tenant")]
    [InlineData("domain")]
    [InlineData("aggregate")]
    [InlineData("failure")]
    [InlineData("rejection")]
    [InlineData("retryable")]
    public async Task CompletedStatusWithWrongReceiptFieldsIsOpaqueDenial(string field)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId($"capability-bad-status-{field}");
        var entryId = new TimeEntryId($"entry-bad-status-{field}");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            capabilityId, entryId);
        factory.Gateway.StatusTransform = status => field switch
        {
            "message" => status with { MessageId = "wrong-message" },
            "count" => status with { EventCount = 1 },
            "sequence" => status with { CommittedEventSequence = 1 },
            "tenant" => status with { TenantId = "tenant-2" },
            "domain" => status with { Domain = "other-domain" },
            "aggregate" => status with { AggregateId = "other-entry" },
            "failure" => status with { FailureReason = "failure" },
            "rejection" => status with { RejectionEventType = "forged.rejection" },
            "retryable" => status with { Retryable = true },
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);
        CapturedFailure denial = await CaptureFailureAsync(response, field, ValidConfirmToken());
        using HttpResponseMessage unknown = await client.PostAsJsonAsync(
            "/api/timesheets/magic-links/confirm/submit?t=unknown-status-token",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);
        CapturedFailure baseline = await CaptureFailureAsync(unknown, "unknown", "unknown-status-token");

        denial.NormalizedBody.ShouldBe(baseline.NormalizedBody);
        denial.Headers.ShouldBe(baseline.Headers);
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value).Length.ShouldBe(3);
    }

    [Fact]
    public async Task ExistingTimeEntryIssueProducesNoTokenAndNoDurableSubmission()
    {
        var generator = new CountingMagicLinkTokenGenerator();
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, useMismatchedClaims: false, tokenGenerator: generator);
        using HttpClient client = factory.CreateClient();
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            new MagicLinkCapabilityId("catalog-seed"), new TimeEntryId("entry-seed"));
        var capabilityId = new MagicLinkCapabilityId("existing-issue-denied");
        var command = new IssueMagicLinkConfirmationCapability(capabilityId,
            new MagicLinkConfirmationScope(Contributor(), TimeEntryTargetReference.ForProject(Project()), ActivityId(),
                new("entry-existing-denied"), MagicLinkTargetKind.ExistingTimeEntry), MagicLinkAllowedAction.Confirm,
            ObservedAtUtc.AddDays(1), new("timesheets", "existing-issue"));
        int submitted = factory.Gateway.SubmissionCount;
        using HttpResponseMessage response = await factory.PostProtectedManagementAsync(client,
            "/api/timesheets/magic-links/confirmation-capabilities", command);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        generator.GenerationCount.ShouldBe(0);
        factory.Gateway.SubmissionCount.ShouldBe(submitted);
        factory.Gateway.StoredEvents(Tenant().TenantId, capabilityId.Value).ShouldBeEmpty();
    }

    [Fact]
    public async Task IssueReturnsTokenOnlyAfterIssuanceIsStoredInCapabilityStream()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, useMismatchedClaims: false);
        using HttpClient client = factory.CreateClient();
        await factory.ProjectValidStateAsync(
            client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            new MagicLinkCapabilityId("capability-catalog-seed"), new TimeEntryId("time-entry-catalog-seed"));
        var capabilityId = new MagicLinkCapabilityId("capability-issued-persisted");
        var command = new IssueMagicLinkConfirmationCapability(
            capabilityId,
            new MagicLinkConfirmationScope(
                Contributor(), TimeEntryTargetReference.ForProject(Project()), ActivityId(),
                new TimeEntryId("time-entry-issued-persisted"), MagicLinkTargetKind.ProposedTimeEntry),
            MagicLinkAllowedAction.Confirm, ObservedAtUtc.AddDays(1),
            new MagicLinkAuditMetadata("timesheets", "issue-persisted"));

        using HttpResponseMessage response = await factory.PostProtectedManagementAsync(
            client, "/api/timesheets/magic-links/confirmation-capabilities", command);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        MagicLinkIssueResponse issued = (await response.Content.ReadFromJsonAsync<MagicLinkIssueResponse>(
            JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        StreamReadEvent[] stored = factory.Gateway.StoredEvents(Tenant().TenantId, capabilityId.Value);
        MagicLinkConfirmationCapabilityIssued eventData = Persisted<StoredMagicLinkIssued>(
            stored.ShouldHaveSingleItem()).Event;
        eventData.TokenHash.Value.ShouldBe(Hash(issued.OneTimeToken));
        JsonSerializer.Serialize(stored, JsonOptions).ShouldNotContain(issued.OneTimeToken);
    }

    [Fact]
    public async Task IssueRecoversCommittedEventWhenSubmitResponseIsLost()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, useMismatchedClaims: false);
        using HttpClient client = factory.CreateClient();
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            new MagicLinkCapabilityId("catalog-seed-lost-issue"), new TimeEntryId("entry-seed-lost-issue"));
        CancellationToken pendingSubmitToken = default;
        factory.Gateway.AfterStoredBeforeResponseAsync = (request, token) =>
        {
            if (request.CommandType != typeof(CommitMagicLinkIssue).FullName)
            {
                return Task.CompletedTask;
            }

            pendingSubmitToken = token;
            return Task.Delay(Timeout.InfiniteTimeSpan, token);
        };
        var capabilityId = new MagicLinkCapabilityId("capability-lost-issue-receipt");
        var command = new IssueMagicLinkConfirmationCapability(
            capabilityId,
            new MagicLinkConfirmationScope(Contributor(), TimeEntryTargetReference.ForProject(Project()),
                ActivityId(), new TimeEntryId("entry-lost-issue-receipt"), MagicLinkTargetKind.ProposedTimeEntry),
            MagicLinkAllowedAction.Confirm, ObservedAtUtc.AddDays(1),
            new MagicLinkAuditMetadata("timesheets", "lost-issue"));

        using HttpResponseMessage response = await factory.PostProtectedManagementAsync(
            client, "/api/timesheets/magic-links/confirmation-capabilities", command);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        MagicLinkIssueResponse issued = (await response.Content.ReadFromJsonAsync<MagicLinkIssueResponse>(
            JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        StreamReadEvent stored = factory.Gateway.StoredEvents(Tenant().TenantId, capabilityId.Value).ShouldHaveSingleItem();
        Persisted<StoredMagicLinkIssued>(stored).Event.TokenHash.Value.ShouldBe(Hash(issued.OneTimeToken));
        pendingSubmitToken.IsCancellationRequested.ShouldBeTrue();
    }

    [Theory]
    [InlineData(503)]
    [InlineData(408)]
    public async Task IssueRetriesTransientStoredReadAfterExactCompletion(int transientStatusCode)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, useMismatchedClaims: false);
        using HttpClient client = factory.CreateClient();
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            new MagicLinkCapabilityId("catalog-seed-issue-read-retry"), new TimeEntryId("entry-seed-issue-read-retry"));
        factory.Gateway.TransientIssueReadFailures = 1;
        factory.Gateway.TransientReadFailureStatusCode = transientStatusCode;
        factory.Gateway.StaleIssueReadbacks = 1;
        var capabilityId = new MagicLinkCapabilityId("capability-issue-read-retry");
        var command = new IssueMagicLinkConfirmationCapability(
            capabilityId,
            new MagicLinkConfirmationScope(Contributor(), TimeEntryTargetReference.ForProject(Project()),
                ActivityId(), new TimeEntryId("entry-issue-read-retry"), MagicLinkTargetKind.ProposedTimeEntry),
            MagicLinkAllowedAction.Confirm, ObservedAtUtc.AddDays(1),
            new MagicLinkAuditMetadata("timesheets", "issue-read-retry"));

        using HttpResponseMessage response = await factory.PostProtectedManagementAsync(
            client, "/api/timesheets/magic-links/confirmation-capabilities", command);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        MagicLinkIssueResponse issued = (await response.Content.ReadFromJsonAsync<MagicLinkIssueResponse>(
            JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        factory.Gateway.TransientIssueReadFailures.ShouldBe(0);
        factory.Gateway.StaleIssueReadbacks.ShouldBe(0);
        factory.Gateway.StoredEvents(Tenant().TenantId, capabilityId.Value).ShouldHaveSingleItem();
        Persisted<StoredMagicLinkIssued>(factory.Gateway.StoredEvents(Tenant().TenantId, capabilityId.Value)[0])
            .Event.TokenHash.Value.ShouldBe(Hash(issued.OneTimeToken));
    }

    [Fact]
    public async Task IssueReceiptRejectsEventFromADifferentCommandMessage()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, useMismatchedClaims: false);
        using HttpClient client = factory.CreateClient();
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            new MagicLinkCapabilityId("catalog-seed-forged-issue"), new TimeEntryId("entry-seed-forged-issue"));
        factory.Gateway.StoredEventTransform = stored => stored with { CorrelationId = "forged-command" };
        var capabilityId = new MagicLinkCapabilityId("capability-forged-issue-receipt");
        var command = new IssueMagicLinkConfirmationCapability(
            capabilityId,
            new MagicLinkConfirmationScope(Contributor(), TimeEntryTargetReference.ForProject(Project()),
                ActivityId(), new TimeEntryId("entry-forged-issue-receipt"), MagicLinkTargetKind.ProposedTimeEntry),
            MagicLinkAllowedAction.Confirm, ObservedAtUtc.AddDays(1),
            new MagicLinkAuditMetadata("timesheets", "forged-issue"));

        using HttpResponseMessage response = await factory.PostProtectedManagementAsync(
            client, "/api/timesheets/magic-links/confirmation-capabilities", command);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UseReceiptRejectsAnotherCommandsCorrelationForConfirmAndAdjust(bool adjust)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId(adjust ? "capability-adjust-wrong-correlation" : "capability-confirm-wrong-correlation");
        var entryId = new TimeEntryId(adjust ? "entry-adjust-wrong-correlation" : "entry-confirm-wrong-correlation");
        string token = adjust ? ValidAdjustToken() : ValidConfirmToken();
        await factory.ProjectValidStateAsync(client, token,
            adjust ? MagicLinkAllowedAction.Adjust : MagicLinkAllowedAction.Confirm, capabilityId, entryId);
        factory.Gateway.StoredEventTransform = stored => stored with { CorrelationId = "another-command" };

        using HttpResponseMessage response = adjust
            ? await client.PostAsJsonAsync($"/api/timesheets/magic-links/adjust/submit?t={token}",
                AdjustCommand(), TestContext.Current.CancellationToken)
            : await client.PostAsJsonAsync($"/api/timesheets/magic-links/confirm/submit?t={token}",
                new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        StreamReadEvent[] stored = factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value);
        stored.Length.ShouldBe(3);
        stored[1].MessageId.ShouldNotBeNullOrWhiteSpace();
        stored[2].MessageId.ShouldNotBeNullOrWhiteSpace();
        stored[1].MessageId.ShouldNotBe(stored[2].MessageId);
    }

    [Theory]
    [InlineData("contributor")]
    [InlineData("target")]
    [InlineData("activity")]
    [InlineData("targetKind")]
    [InlineData("action")]
    [InlineData("expiry")]
    [InlineData("source")]
    [InlineData("singleUse")]
    [InlineData("hash")]
    [InlineData("issuer")]
    [InlineData("issuedAt")]
    [InlineData("timeEntry")]
    public async Task IssueReceiptRejectsChangedIssuanceFields(string changedField)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, useMismatchedClaims: false);
        using HttpClient client = factory.CreateClient();
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            new MagicLinkCapabilityId("catalog-seed-receipt"), new TimeEntryId("entry-seed-receipt"));
        factory.Gateway.StoredPayloadTransform = payload => payload is StoredMagicLinkIssued stored
            ? new StoredMagicLinkIssued(changedField switch
            {
                "contributor" => stored.Event with { Contributor = OtherContributor() },
                "target" => stored.Event with { Target = TimeEntryTargetReference.ForProject(new ProjectReference("project-other")) },
                "activity" => stored.Event with { ActivityTypeId = new ActivityTypeId("activity-other") },
                "targetKind" => stored.Event with { TargetKind = MagicLinkTargetKind.ExistingTimeEntry },
                "action" => stored.Event with { AllowedAction = MagicLinkAllowedAction.Adjust },
                "expiry" => stored.Event with { ExpiresAtUtc = stored.Event.ExpiresAtUtc.AddHours(1) },
                "source" => stored.Event with { Source = new MagicLinkAuditMetadata("forged", "source") },
                "singleUse" => stored.Event with { IsSingleUse = false },
                "hash" => stored.Event with { TokenHash = new MagicLinkTokenHash("forged-hash") },
                "issuer" => stored.Event with { Issuer = OtherContributor() },
                "issuedAt" => stored.Event with { IssuedAtUtc = stored.Event.IssuedAtUtc.AddSeconds(1) },
                "timeEntry" => stored.Event with { TimeEntryId = new TimeEntryId("forged-entry") },
                _ => throw new ArgumentOutOfRangeException(nameof(changedField))
            })
            : payload;
        var capabilityId = new MagicLinkCapabilityId($"capability-receipt-{changedField}");
        var command = new IssueMagicLinkConfirmationCapability(
            capabilityId,
            new MagicLinkConfirmationScope(
                Contributor(), TimeEntryTargetReference.ForProject(Project()), ActivityId(),
                new TimeEntryId($"entry-receipt-{changedField}"), MagicLinkTargetKind.ProposedTimeEntry),
            MagicLinkAllowedAction.Confirm, ObservedAtUtc.AddDays(1),
            new MagicLinkAuditMetadata("timesheets", "receipt"));

        using HttpResponseMessage response = await factory.PostProtectedManagementAsync(
            client, "/api/timesheets/magic-links/confirmation-capabilities", command);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.Gateway.StoredEvents(Tenant().TenantId, capabilityId.Value).ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("target")]
    [InlineData("scope")]
    [InlineData("category")]
    [InlineData("metrics")]
    [InlineData("priorContributor")]
    [InlineData("auditSource")]
    [InlineData("serviceDate")]
    [InlineData("duration")]
    [InlineData("activityType")]
    [InlineData("billable")]
    [InlineData("comment")]
    public async Task AdjustmentReceiptRejectsChangedEffectFields(string changedField)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId($"capability-adjust-receipt-{changedField}");
        var entryId = new TimeEntryId($"entry-adjust-receipt-{changedField}");
        await factory.ProjectValidStateAsync(client, ValidAdjustToken(), MagicLinkAllowedAction.Adjust, capabilityId, entryId);
        factory.Gateway.StoredPayloadTransform = payload => payload is StoredTimeEntryAdjusted stored
            ? new StoredTimeEntryAdjusted(changedField switch
            {
                "target" => stored.Event with { AdjustedValues = stored.Event.AdjustedValues with
                    { Target = TimeEntryTargetReference.ForProject(new ProjectReference("project-other")) } },
                "scope" => stored.Event with { ActivityTypeScope = ActivityTypeScope.Project },
                "category" => stored.Event with { AdjustedValues = stored.Event.AdjustedValues with
                    { ContributorCategory = ContributorCategory.Employee } },
                "metrics" => stored.Event with { AdjustedValues = stored.Event.AdjustedValues with
                    { AiMetrics = AiEffortMetrics.Unavailable with { BillableEffortMinutes = 1 } } },
                "priorContributor" => stored.Event with { PreviousValues = stored.Event.PreviousValues with
                    { Contributor = OtherContributor() } },
                "auditSource" => stored.Event with { Source = new ExternalContributionSource("forged", "source") },
                "serviceDate" => stored.Event with { AdjustedValues = stored.Event.AdjustedValues with
                    { ServiceDate = stored.Event.AdjustedValues.ServiceDate.AddDays(1) } },
                "duration" => stored.Event with { AdjustedValues = stored.Event.AdjustedValues with
                    { DurationMinutes = stored.Event.AdjustedValues.DurationMinutes + 1 } },
                "activityType" => stored.Event with { AdjustedValues = stored.Event.AdjustedValues with
                    { ActivityTypeId = new ActivityTypeId("other-activity") } },
                "billable" => stored.Event with { AdjustedValues = stored.Event.AdjustedValues with
                    { BillableState = stored.Event.AdjustedValues.BillableState == BillableState.Billable
                        ? BillableState.NonBillable : BillableState.Billable } },
                "comment" => stored.Event with { AdjustedValues = stored.Event.AdjustedValues with
                    { Comment = new TimeEntryComment("forged edit", TimeEntryCommentPolicy.SensitiveDefault) } },
                _ => throw new ArgumentOutOfRangeException(nameof(changedField))
            })
            : payload;

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/adjust/submit?t={ValidAdjustToken()}",
            AdjustCommand(), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value).Length.ShouldBe(3);
    }

    [Fact]
    public async Task TransitionReceiptRejectsChangedAuditSource()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, useMismatchedClaims: false);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId("capability-transition-receipt-source");
        var entryId = new TimeEntryId("entry-transition-receipt-source");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm, capabilityId, entryId);
        factory.Gateway.StoredPayloadTransform = payload => payload is StoredMagicLinkRevoked stored
            ? new StoredMagicLinkRevoked(stored.Event with
            {
                Source = new MagicLinkAuditMetadata("forged", "source")
            })
            : payload;

        using HttpResponseMessage response = await factory.PostProtectedManagementAsync(
            client, $"/api/timesheets/magic-links/confirmation-capabilities/{capabilityId.Value}/revoke",
            new RevokeMagicLinkConfirmationCapability(
                capabilityId, new MagicLinkAuditMetadata("timesheets", "expected")));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value).Length.ShouldBe(2);
    }

    [Fact]
    public async Task TransitionReceiptRejectsMissingPersistedEventIdentity()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, useMismatchedClaims: false);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId("capability-transition-missing-event-id");
        var entryId = new TimeEntryId("entry-transition-missing-event-id");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm, capabilityId, entryId);
        factory.Gateway.StoredEventTransform = stored => stored with { MessageId = "" };

        using HttpResponseMessage response = await factory.PostProtectedManagementAsync(
            client, $"/api/timesheets/magic-links/confirmation-capabilities/{capabilityId.Value}/revoke",
            new RevokeMagicLinkConfirmationCapability(
                capabilityId, new MagicLinkAuditMetadata("timesheets", "missing-event-id")));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("capability")]
    [InlineData("tenant")]
    [InlineData("timeEntry")]
    [InlineData("expiredAt")]
    [InlineData("source")]
    public async Task ExpireReceiptRejectsChangedStoredEventFields(string field)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, useMismatchedClaims: false);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId($"capability-expire-receipt-{field}");
        var entryId = new TimeEntryId($"entry-expire-receipt-{field}");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm, capabilityId, entryId);
        StreamReadEvent seeded = factory.Gateway.StoredEvents(Tenant().TenantId, capabilityId.Value).ShouldHaveSingleItem();
        MagicLinkConfirmationCapabilityIssued issued = Persisted<MagicLinkConfirmationCapabilityIssued>(seeded);
        factory.Gateway.WithStream(Tenant().TenantId, capabilityId.Value, seeded with
        {
            Payload = JsonSerializer.SerializeToUtf8Bytes(issued with { ExpiresAtUtc = ObservedAtUtc }, JsonOptions)
        });
        factory.Gateway.StoredPayloadTransform = payload => payload is StoredMagicLinkExpired stored
            ? new StoredMagicLinkExpired(field switch
            {
                "capability" => stored.Event with { CapabilityId = new MagicLinkCapabilityId("forged-capability") },
                "tenant" => stored.Event with { Tenant = OtherTenant() },
                "timeEntry" => stored.Event with { TimeEntryId = new TimeEntryId("forged-entry") },
                "expiredAt" => stored.Event with { ExpiredAtUtc = stored.Event.ExpiredAtUtc.AddSeconds(1) },
                "source" => stored.Event with { Source = new MagicLinkAuditMetadata("forged", "source") },
                _ => throw new ArgumentOutOfRangeException(nameof(field))
            }) : payload;

        using HttpResponseMessage response = await factory.PostProtectedManagementAsync(
            client, $"/api/timesheets/magic-links/confirmation-capabilities/{capabilityId.Value}/expire",
            new ExpireMagicLinkConfirmationCapability(capabilityId, new MagicLinkAuditMetadata("timesheets", "expire-receipt")));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value).Length.ShouldBe(2);
    }

    [Fact]
    public async Task ConcurrentAndReplayedConfirmationHaveOneStoredTerminalBatch()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId("capability-concurrent");
        var entryId = new TimeEntryId("time-entry-concurrent");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm, capabilityId, entryId);

        Task<HttpResponseMessage> first = client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);
        Task<HttpResponseMessage> second = client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);
        HttpResponseMessage[] responses = await Task.WhenAll(first, second);
        responses.Count(static response => response.StatusCode == HttpStatusCode.Accepted).ShouldBe(1);
        responses.Count(static response => response.StatusCode == HttpStatusCode.Forbidden).ShouldBe(1);
        foreach (HttpResponseMessage response in responses)
        {
            response.Dispose();
        }

        using HttpResponseMessage replay = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);
        _ = await CaptureFailureAsync(replay, "replay", ValidConfirmToken());
        StreamReadEvent[] stored = factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value);
        int rejectedCommands = stored.Count(static item => item.EventTypeName == typeof(MagicLinkCommitRejected).FullName);
        rejectedCommands.ShouldBeLessThanOrEqualTo(1);
        stored.Length.ShouldBe(3 + rejectedCommands);
        stored.Count(static item => item.EventTypeName == typeof(StoredMagicLinkUsed).FullName).ShouldBe(1);
        stored.Count(static item => item.EventTypeName == typeof(StoredTimeEntryConfirmed).FullName).ShouldBe(1);
    }

    [Fact]
    public async Task ConfirmRaceReadsTheCompletedBatchWhenBothSubmitResponsesStall()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId("capability-delayed-submit-receipt");
        var entryId = new TimeEntryId("time-entry-delayed-submit-receipt");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            capabilityId, entryId);
        // Both requests must load the unused capability before either commits. Starting
        // two HTTP tasks alone can let the first commit finish before the second loader.
        TaskCompletionSource bothCommitsEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int commitCount = 0;
        factory.Gateway.BeforeSubmitAsync = async (request, cancellationToken) =>
        {
            if (request.CommandType != typeof(CommitMagicLinkUse).FullName)
            {
                return;
            }

            if (Interlocked.Increment(ref commitCount) == 2)
            {
                bothCommitsEntered.TrySetResult();
            }

            await bothCommitsEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        };
        factory.Gateway.AfterStoredBeforeResponseAsync = (request, cancellationToken) =>
            request.CommandType == typeof(CommitMagicLinkUse).FullName
                ? Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
                : Task.CompletedTask;

        Task<HttpResponseMessage> first = client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);
        Task<HttpResponseMessage> second = client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);
        HttpResponseMessage[] responses = await Task.WhenAll(first, second);
        responses.Count(static response => response.StatusCode == HttpStatusCode.Accepted).ShouldBe(1);
        responses.Count(static response => response.StatusCode == HttpStatusCode.Forbidden).ShouldBe(1);
        CapturedFailure loser = await CaptureFailureAsync(
            responses.Single(static response => response.StatusCode == HttpStatusCode.Forbidden),
            "delayed-submit-race-loser", ValidConfirmToken());
        foreach (HttpResponseMessage response in responses)
        {
            response.Dispose();
        }

        using HttpResponseMessage replay = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);
        CapturedFailure replayDenial = await CaptureFailureAsync(replay, "delayed-submit-race-replay", ValidConfirmToken());
        loser.ContentType.ShouldBe(replayDenial.ContentType);
        loser.NormalizedBody.ShouldBe(replayDenial.NormalizedBody);
        loser.Headers.ShouldBe(replayDenial.Headers);
        using HttpResponseMessage unknown = await client.PostAsJsonAsync(
            "/api/timesheets/magic-links/confirm/submit?t=unknown-delayed-submit-token",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);
        CapturedFailure baseline = await CaptureFailureAsync(
            unknown, "delayed-submit-race-unknown", "unknown-delayed-submit-token");
        loser.ContentType.ShouldBe(baseline.ContentType);
        loser.NormalizedBody.ShouldBe(baseline.NormalizedBody);
        loser.Headers.ShouldBe(baseline.Headers);

        StreamReadEvent[] stored = factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value);
        stored.Count(static item => item.EventTypeName == typeof(StoredMagicLinkUsed).FullName).ShouldBe(1);
        stored.Count(static item => item.EventTypeName == typeof(StoredTimeEntryConfirmed).FullName).ShouldBe(1);
        stored.Count(static item => item.EventTypeName == typeof(MagicLinkCommitRejected).FullName).ShouldBe(1);
    }

    [Fact]
    public async Task ConfirmAcceptsAnExactCommittedBatchAfterSubmitTransportFault()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId("capability-lost-submit-receipt");
        var entryId = new TimeEntryId("time-entry-lost-submit-receipt");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            capabilityId, entryId);
        factory.Gateway.AfterStoredBeforeResponseAsync = (request, _) =>
            request.CommandType == typeof(CommitMagicLinkUse).FullName
                ? Task.FromException(new InvalidOperationException("submit response lost after commit"))
                : Task.CompletedTask;

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        StreamReadEvent[] stored = factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value);
        stored.Count(static item => item.EventTypeName == typeof(StoredMagicLinkUsed).FullName).ShouldBe(1);
        stored.Count(static item => item.EventTypeName == typeof(StoredTimeEntryConfirmed).FullName).ShouldBe(1);
    }

    [Theory]
    [InlineData(429)]
    [InlineData(408)]
    public async Task ConfirmRetriesTransientStatusAndBatchReadFaultsWithinItsDeadline(int transientStatusCode)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId("capability-transient-commit-probes");
        var entryId = new TimeEntryId("time-entry-transient-commit-probes");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            capabilityId, entryId);
        factory.Gateway.TransientWorkloadStatusFailures = 1;
        factory.Gateway.TransientCommitBatchReadFailures = 1;
        factory.Gateway.TransientReadFailureStatusCode = transientStatusCode;
        factory.Gateway.StaleCommitBatchReadbacks = 1;

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        factory.Gateway.TransientWorkloadStatusFailures.ShouldBe(0);
        factory.Gateway.TransientCommitBatchReadFailures.ShouldBe(0);
        factory.Gateway.StaleCommitBatchReadbacks.ShouldBe(0);
        factory.Gateway.SubmissionCount.ShouldBe(1);

        StreamReadEvent[] stored = factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value);
        stored.Count(static item => item.EventTypeName == typeof(StoredMagicLinkUsed).FullName).ShouldBe(1);
        stored.Count(static item => item.EventTypeName == typeof(StoredTimeEntryConfirmed).FullName).ShouldBe(1);
    }

    [Fact]
    public async Task ConfirmSubmitsOnceWhenStatusPreflightIsUnavailableUntilSubmission()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId("capability-preflight-unavailable");
        var entryId = new TimeEntryId("time-entry-preflight-unavailable");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            capabilityId, entryId);
        factory.Gateway.StatusUnavailableUntilSubmission = true;

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        factory.Gateway.SubmissionCount.ShouldBe(1);
        StreamReadEvent[] stored = factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value);
        stored.Count(static item => item.EventTypeName == typeof(StoredMagicLinkUsed).FullName).ShouldBe(1);
        stored.Count(static item => item.EventTypeName == typeof(StoredTimeEntryConfirmed).FullName).ShouldBe(1);
    }

    [Theory]
    [InlineData(CommandStatus.Received)]
    [InlineData(CommandStatus.EventsStored)]
    [InlineData(CommandStatus.PublishFailed)]
    public async Task NonCompletedStatusIsAnOpaqueDenialWithoutClaimingACommit(CommandStatus status)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId($"capability-status-{status}");
        var entryId = new TimeEntryId($"time-entry-status-{status}");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            capabilityId, entryId);
        factory.Gateway.OverrideSuccessfulStatus = status;

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);
        CapturedFailure denial = await CaptureFailureAsync(response, status.ToString(), ValidConfirmToken());
        using HttpResponseMessage unknown = await client.PostAsJsonAsync(
            "/api/timesheets/magic-links/confirm/submit?t=unknown-status-token",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);
        CapturedFailure baseline = await CaptureFailureAsync(unknown, "unknown", "unknown-status-token");

        denial.NormalizedBody.ShouldBe(baseline.NormalizedBody);
        denial.Headers.ShouldBe(baseline.Headers);
        StreamReadEvent[] stored = factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value);
        stored.Length.ShouldBe(status == CommandStatus.Received ? 1 : 3);
        if (status is CommandStatus.EventsStored or CommandStatus.PublishFailed)
        {
            stored.Count(static item => item.EventTypeName == typeof(StoredMagicLinkUsed).FullName).ShouldBe(1);
            stored.Count(static item => item.EventTypeName == typeof(StoredTimeEntryConfirmed).FullName).ShouldBe(1);
        }
    }

    [Theory]
    [InlineData("used")]
    [InlineData("revoked")]
    [InlineData("expired")]
    public async Task LegacyCapabilityStreamTerminalCannotReopenTheLink(string terminalKind)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId($"capability-legacy-{terminalKind}");
        var entryId = new TimeEntryId($"time-entry-legacy-{terminalKind}");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm, capabilityId, entryId);
        StreamReadEvent issued = factory.Gateway.StoredEvents(Tenant().TenantId, capabilityId.Value).ShouldHaveSingleItem();
        object legacyTerminal = terminalKind switch
        {
            "used" => new MagicLinkConfirmationCapabilityUsed(
                capabilityId, Tenant(), Contributor(), entryId, ObservedAtUtc.AddMinutes(-1),
                new MagicLinkAuditMetadata("magic-link", capabilityId.Value)),
            "revoked" => new MagicLinkConfirmationCapabilityRevoked(
                capabilityId, Tenant(), Operator(), ObservedAtUtc.AddMinutes(-1),
                new MagicLinkAuditMetadata("timesheets", "legacy-revoke")),
            "expired" => new MagicLinkConfirmationCapabilityExpired(
                capabilityId, Tenant(), ObservedAtUtc.AddMinutes(-1),
                new MagicLinkAuditMetadata("timesheets", "legacy-expire")),
            _ => throw new ArgumentOutOfRangeException(nameof(terminalKind))
        };
        StreamReadEvent terminalEvent = new(
            2, legacyTerminal.GetType().FullName!,
            JsonSerializer.SerializeToUtf8Bytes(legacyTerminal, legacyTerminal.GetType(), JsonOptions),
            "json", 1, $"legacy-{terminalKind}", "legacy", null, ObservedAtUtc, "fixture");
        factory.Gateway.WithStream(Tenant().TenantId, capabilityId.Value, issued, terminalEvent);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);
        CapturedFailure denial = await CaptureFailureAsync(response, $"legacy-{terminalKind}", ValidConfirmToken());
        using HttpResponseMessage unknown = await client.PostAsJsonAsync(
            "/api/timesheets/magic-links/confirm/submit?t=unknown-legacy-token",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);
        CapturedFailure baseline = await CaptureFailureAsync(unknown, "unknown", "unknown-legacy-token");
        denial.StatusCode.ShouldBe(baseline.StatusCode);
        denial.ContentType.ShouldBe(baseline.ContentType);
        denial.NormalizedBody.ShouldBe(baseline.NormalizedBody);
        denial.Headers.ShouldBe(baseline.Headers);
        RawByteLength(denial.RawBody).ShouldBe(RawByteLength(baseline.RawBody));

        StreamReadEvent[] capabilityEvents = factory.Gateway.StoredEvents(Tenant().TenantId, capabilityId.Value);
        capabilityEvents.Length.ShouldBe(2);
        capabilityEvents[1].EventTypeName.ShouldBe(terminalEvent.EventTypeName);
        capabilityEvents[1].Payload.SequenceEqual(terminalEvent.Payload).ShouldBeTrue();
        StreamReadEvent[] targetEvents = factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value);
        targetEvents.Length.ShouldBe(1);
        factory.Gateway.SubmissionCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public async Task ManagementTerminalTransitionIsStoredInTargetStreamAndDeniesLaterUse(
        bool expire, bool loseResponse, bool corruptReceipt)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, useMismatchedClaims: false);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId(expire ? "capability-persist-expire" : "capability-persist-revoke");
        var entryId = new TimeEntryId(expire ? "time-entry-persist-expire" : "time-entry-persist-revoke");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm, capabilityId, entryId);
        if (expire)
        {
            StreamReadEvent seeded = factory.Gateway.StoredEvents(Tenant().TenantId, capabilityId.Value).ShouldHaveSingleItem();
            MagicLinkConfirmationCapabilityIssued issued = Persisted<MagicLinkConfirmationCapabilityIssued>(seeded);
            factory.Gateway.WithStream(Tenant().TenantId, capabilityId.Value, seeded with
            {
                Payload = JsonSerializer.SerializeToUtf8Bytes(
                    issued with { ExpiresAtUtc = ObservedAtUtc }, JsonOptions)
            });
        }

        var source = new MagicLinkAuditMetadata("timesheets", expire ? "expire" : "revoke");
        if (loseResponse)
        {
            factory.Gateway.AfterStoredBeforeResponseAsync = (request, _) =>
                request.CommandType == typeof(CommitMagicLinkTransition).FullName
                    ? Task.FromException(new HttpRequestException("transition response lost after commit"))
                    : Task.CompletedTask;
        }

        if (corruptReceipt)
        {
            factory.Gateway.StoredPayloadTransform = payload => payload switch
            {
                StoredMagicLinkRevoked revoked => revoked with
                {
                    Event = revoked.Event with { Source = new MagicLinkAuditMetadata("forged", "source") }
                },
                StoredMagicLinkExpired expired => expired with
                {
                    Event = expired.Event with { Source = new MagicLinkAuditMetadata("forged", "source") }
                },
                _ => payload
            };
        }

        using HttpResponseMessage transition = expire
            ? await factory.PostProtectedManagementAsync(
                client, $"/api/timesheets/magic-links/confirmation-capabilities/{capabilityId.Value}/expire",
                new ExpireMagicLinkConfirmationCapability(capabilityId, source))
            : await factory.PostProtectedManagementAsync(
                client, $"/api/timesheets/magic-links/confirmation-capabilities/{capabilityId.Value}/revoke",
                new RevokeMagicLinkConfirmationCapability(capabilityId, source));
        transition.StatusCode.ShouldBe(corruptReceipt ? HttpStatusCode.Forbidden : HttpStatusCode.Accepted);
        StreamReadEvent[] target = factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value);
        target.Length.ShouldBe(2);
        target[1].EventTypeName.ShouldBe(expire
            ? typeof(StoredMagicLinkExpired).FullName
            : typeof(StoredMagicLinkRevoked).FullName);
        MagicLinkAuditMetadata expectedSource = corruptReceipt ? new("forged", "source") : source;
        if (expire)
        {
            MagicLinkConfirmationCapabilityExpired expired = Persisted<StoredMagicLinkExpired>(target[1]).Event;
            expired.CapabilityId.ShouldBe(capabilityId);
            expired.Tenant.ShouldBe(Tenant());
            expired.TimeEntryId.ShouldBe(entryId);
            expired.ExpiredAtUtc.ShouldBe(ObservedAtUtc);
            expired.Source.ShouldBe(expectedSource);
        }
        else
        {
            MagicLinkConfirmationCapabilityRevoked revoked = Persisted<StoredMagicLinkRevoked>(target[1]).Event;
            revoked.CapabilityId.ShouldBe(capabilityId);
            revoked.Tenant.ShouldBe(Tenant());
            revoked.TimeEntryId.ShouldBe(entryId);
            revoked.RevokedBy.ShouldBe(Contributor());
            revoked.RevokedAtUtc.ShouldBe(ObservedAtUtc);
            revoked.Source.ShouldBe(expectedSource);
        }

        factory.Gateway.SubmissionCount.ShouldBe(1);
        factory.Gateway.StoredEvents(Tenant().TenantId, capabilityId.Value).Length.ShouldBe(1);

        using HttpResponseMessage laterUse = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);
        CapturedFailure denial = await CaptureFailureAsync(laterUse, "terminal", ValidConfirmToken());
        using HttpResponseMessage unknown = await client.PostAsJsonAsync(
            "/api/timesheets/magic-links/confirm/submit?t=unknown-terminal-token",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);
        CapturedFailure baseline = await CaptureFailureAsync(unknown, "unknown", "unknown-terminal-token");
        denial.NormalizedBody.ShouldBe(baseline.NormalizedBody);
        denial.Headers.ShouldBe(baseline.Headers);
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value).Length.ShouldBe(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProposedEntryCanBeRevokedOrExpiredWhenEventStoreReportsMissingOwnerStream(bool expire)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, useMismatchedClaims: false);
        using HttpClient client = factory.CreateClient();
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            new MagicLinkCapabilityId("catalog-seed-proposed"), new TimeEntryId("catalog-seed-entry"));
        var capabilityId = new MagicLinkCapabilityId(expire ? "proposed-expire" : "proposed-revoke");
        var entryId = new TimeEntryId(expire ? "unrecorded-expire" : "unrecorded-revoke");
        var issue = new IssueMagicLinkConfirmationCapability(
            capabilityId,
            new MagicLinkConfirmationScope(Contributor(), TimeEntryTargetReference.ForProject(Project()),
                ActivityId(), entryId, MagicLinkTargetKind.ProposedTimeEntry),
            MagicLinkAllowedAction.Confirm, ObservedAtUtc.AddDays(1),
            new MagicLinkAuditMetadata("timesheets", "proposed-issue"));
        using HttpResponseMessage issued = await factory.PostProtectedManagementAsync(
            client, "/api/timesheets/magic-links/confirmation-capabilities", issue);
        issued.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value).ShouldBeEmpty();
        factory.Gateway.ReportMissingStream(Tenant().TenantId, entryId.Value,
            StreamReplayReasonCodes.MissingStream);
        if (expire)
        {
            StreamReadEvent seed = factory.Gateway.StoredEvents(Tenant().TenantId, capabilityId.Value).ShouldHaveSingleItem();
            MagicLinkConfirmationCapabilityIssued eventData = Persisted<StoredMagicLinkIssued>(seed).Event;
            factory.Gateway.WithStream(Tenant().TenantId, capabilityId.Value, seed with
            {
                Payload = JsonSerializer.SerializeToUtf8Bytes(
                    new StoredMagicLinkIssued(eventData with { ExpiresAtUtc = ObservedAtUtc }), JsonOptions)
            });
        }

        var source = new MagicLinkAuditMetadata("timesheets", "proposed-transition");
        using HttpResponseMessage response = expire
            ? await factory.PostProtectedManagementAsync(client,
                $"/api/timesheets/magic-links/confirmation-capabilities/{capabilityId.Value}/expire",
                new ExpireMagicLinkConfirmationCapability(capabilityId, source))
            : await factory.PostProtectedManagementAsync(client,
                $"/api/timesheets/magic-links/confirmation-capabilities/{capabilityId.Value}/revoke",
                new RevokeMagicLinkConfirmationCapability(capabilityId, source));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value)
            .ShouldHaveSingleItem().EventTypeName.ShouldBe(expire
                ? typeof(StoredMagicLinkExpired).FullName
                : typeof(StoredMagicLinkRevoked).FullName);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    public async Task HttpExpirySeparatesWorkloadAndExplicitPartyAuthority(bool systemAllowed, bool explicitParty, bool tenantAllowed)
    {
        const string sameNamedParty = "workload:eventstore";
        var capabilityId = new MagicLinkCapabilityId("capability-expire-no-actor");
        var entryId = new TimeEntryId("time-entry-expire-no-actor");
        IMagicLinkSystemExpiryAuthorizer system = Substitute.For<IMagicLinkSystemExpiryAuthorizer>();
        system.AuthorizeAsync("timesheets", Tenant(), capabilityId, entryId,
            TimeEntryTargetReference.ForProject(Project()), Arg.Any<CancellationToken>()).Returns(true);
        Claim[] claims = explicitParty
            ? [new("tenant_id", Tenant().TenantId), new("party_id", sameNamedParty)]
            : [new("tenant_id", Tenant().TenantId)];
        using MagicLinkHttpBoundaryFactory factory = new(
            useConcreteLoader: true, useMismatchedClaims: false,
            claims: claims, systemExpiryAuthorizer: systemAllowed ? system : null);
        using HttpClient client = factory.CreateClient();
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            capabilityId, entryId);
        StreamReadEvent seeded = factory.Gateway.StoredEvents(Tenant().TenantId, capabilityId.Value).ShouldHaveSingleItem();
        MagicLinkConfirmationCapabilityIssued issued = Persisted<MagicLinkConfirmationCapabilityIssued>(seeded);
        factory.Gateway.WithStream(Tenant().TenantId, capabilityId.Value, seeded with
        {
            Payload = JsonSerializer.SerializeToUtf8Bytes(
                issued with { ExpiresAtUtc = ObservedAtUtc }, JsonOptions)
        });
        factory.AccessGuard.DecisionOverride = request => request.Context.Actor == new PartyReference(sameNamedParty)
            ? TimesheetsAuthorizationDecision.Allowed()
            : TimesheetsAuthorizationDecision.Denied(TimesheetsDenialCategory.UnconfiguredPolicy, "Party denied");
        if (!tenantAllowed)
        {
            factory.TenantAccessValidator.ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimesheetsOperation>(), Arg.Any<CancellationToken>())
                .Returns(TimesheetsTenantAccessResult.Denied(TimesheetsTenantAccessState.UnconfiguredPolicy, "Tenant denied"));
        }
        int partyChecks = factory.AccessGuard.AuthorizationCount;
        CommitMagicLinkTransition? submitted = null;
        factory.Gateway.BeforeSubmitAsync = (request, _) =>
        {
            submitted = request.Payload.Deserialize<CommitMagicLinkTransition>(JsonOptions);
            return Task.CompletedTask;
        };

        using HttpResponseMessage response = await factory.PostProtectedManagementAsync(
            client, $"/api/timesheets/magic-links/confirmation-capabilities/{capabilityId.Value}/expire",
            new ExpireMagicLinkConfirmationCapability(
                capabilityId, new MagicLinkAuditMetadata("timesheets", "expiry-no-actor")));

        bool allowed = tenantAllowed && (systemAllowed || explicitParty);
        response.StatusCode.ShouldBe(allowed ? HttpStatusCode.Accepted : HttpStatusCode.Forbidden);
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value)
            .Count(item => item.EventTypeName == typeof(StoredMagicLinkExpired).FullName).ShouldBe(allowed ? 1 : 0);
        if (tenantAllowed)
        {
            submitted.ShouldNotBeNull().Actor.ShouldBe(explicitParty ? new PartyReference(sameNamedParty) : null);
        }
        else
        {
            factory.Gateway.SubmissionCount.ShouldBe(0);
            submitted.ShouldBeNull();
        }
        if (!explicitParty)
        {
            factory.AccessGuard.AuthorizationCount.ShouldBe(partyChecks);
        }
        if (systemAllowed && tenantAllowed && !explicitParty)
        {
            await system.Received(1).AuthorizeAsync("timesheets", Tenant(), capabilityId, entryId,
                TimeEntryTargetReference.ForProject(Project()), Arg.Any<CancellationToken>());
        }
        else
        {
            await system.DidNotReceiveWithAnyArgs().AuthorizeAsync(default!, default!, default!, default!, default!, TestContext.Current.CancellationToken);
        }
    }

    private static T Persisted<T>(StreamReadEvent streamEvent)
        where T : class
    {
        streamEvent.EventTypeName.ShouldBe(typeof(T).FullName);
        return JsonSerializer.Deserialize<T>(streamEvent.Payload, JsonOptions).ShouldNotBeNull();
    }

    private sealed class CapturingWorkloadGatewayHandler : HttpMessageHandler
    {
        public string? Path { get; private set; }

        public string? Assertion { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri?.AbsolutePath;
            Assertion = request.Headers.TryGetValues("X-Hexalith-Workload-Assertion", out IEnumerable<string>? values)
                ? values.Single() : null;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new SubmitCommandResponse("host-message", MessageId: "host-message"))
            });
        }
    }
}
