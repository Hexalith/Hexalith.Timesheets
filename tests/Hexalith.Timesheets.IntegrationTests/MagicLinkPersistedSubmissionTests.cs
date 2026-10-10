using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Runtime;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed partial class MagicLinkConfirmationHttpBoundaryTests
{
    [Fact]
    public async Task Composed_host_registers_authenticated_resource_binding_assertion_source()
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
    public async Task Composed_host_gateway_sends_bound_assertion_on_workload_submit_route()
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
    public async Task Production_gateway_assertion_source_binds_tenant_domain_and_authenticated_actor()
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
    public async Task Production_gateway_assertion_uses_name_identifier_when_party_claim_is_blank()
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
    public async Task Confirm_and_adjust_commit_use_with_matching_effect_in_target_stream()
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
            AdjustCommand(), TestContext.Current.CancellationToken);

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
        factory.Gateway.StoredEvents(Tenant().TenantId, confirmId.Value).Length.ShouldBe(1);
        factory.Gateway.StoredEvents(Tenant().TenantId, adjustId.Value).Length.ShouldBe(1);
    }

    [Fact]
    public async Task Delayed_completed_status_still_accepts_exact_stored_batch()
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
    public async Task Use_receipt_rejects_changed_stored_event_fields(string field)
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
    public async Task Confirmation_receipt_rejects_changed_stored_effect_fields(string field)
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
    public async Task Accepted_message_id_mismatch_is_opaque_even_when_events_were_stored()
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
    public async Task Completed_status_with_wrong_receipt_fields_is_opaque_denial(string field)
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
    public async Task Issue_returns_token_only_after_issuance_is_stored_in_capability_stream()
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
    public async Task Issue_receipt_rejects_changed_issuance_fields(string changedField)
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
    public async Task Adjustment_receipt_rejects_changed_effect_fields(string changedField)
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
    public async Task Transition_receipt_rejects_changed_audit_source()
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

    [Theory]
    [InlineData("capability")]
    [InlineData("tenant")]
    [InlineData("timeEntry")]
    [InlineData("expiredAt")]
    [InlineData("source")]
    public async Task Expire_receipt_rejects_changed_stored_event_fields(string field)
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
    public async Task Concurrent_and_replayed_confirmation_have_one_stored_terminal_batch()
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

    [Theory]
    [InlineData(CommandStatus.Received)]
    [InlineData(CommandStatus.EventsStored)]
    [InlineData(CommandStatus.PublishFailed)]
    public async Task Non_completed_status_is_an_opaque_denial_without_claiming_a_commit(CommandStatus status)
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
    public async Task Legacy_capability_stream_terminal_cannot_reopen_the_link(string terminalKind)
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
    [InlineData(false)]
    [InlineData(true)]
    public async Task Management_terminal_transition_is_stored_in_target_stream_and_denies_later_use(bool expire)
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
        using HttpResponseMessage transition = expire
            ? await factory.PostProtectedManagementAsync(
                client, $"/api/timesheets/magic-links/confirmation-capabilities/{capabilityId.Value}/expire",
                new ExpireMagicLinkConfirmationCapability(capabilityId, source))
            : await factory.PostProtectedManagementAsync(
                client, $"/api/timesheets/magic-links/confirmation-capabilities/{capabilityId.Value}/revoke",
                new RevokeMagicLinkConfirmationCapability(capabilityId, source));
        transition.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        StreamReadEvent[] target = factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value);
        target.Length.ShouldBe(2);
        target[1].EventTypeName.ShouldBe(expire
            ? typeof(StoredMagicLinkExpired).FullName
            : typeof(StoredMagicLinkRevoked).FullName);
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
    public async Task Proposed_entry_can_be_revoked_or_expired_when_EventStore_reports_missing_owner_stream(bool expire)
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

    [Fact]
    public async Task Expiry_without_a_human_actor_still_commits_the_terminal_event()
    {
        using MagicLinkHttpBoundaryFactory factory = new(
            useConcreteLoader: true, useMismatchedClaims: false,
            claims: [new Claim("tenant_id", Tenant().TenantId)]);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId("capability-expire-no-actor");
        var entryId = new TimeEntryId("time-entry-expire-no-actor");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            capabilityId, entryId);
        StreamReadEvent seeded = factory.Gateway.StoredEvents(Tenant().TenantId, capabilityId.Value).ShouldHaveSingleItem();
        MagicLinkConfirmationCapabilityIssued issued = Persisted<MagicLinkConfirmationCapabilityIssued>(seeded);
        factory.Gateway.WithStream(Tenant().TenantId, capabilityId.Value, seeded with
        {
            Payload = JsonSerializer.SerializeToUtf8Bytes(
                issued with { ExpiresAtUtc = ObservedAtUtc }, JsonOptions)
        });

        using HttpResponseMessage response = await factory.PostProtectedManagementAsync(
            client, $"/api/timesheets/magic-links/confirmation-capabilities/{capabilityId.Value}/expire",
            new ExpireMagicLinkConfirmationCapability(
                capabilityId, new MagicLinkAuditMetadata("timesheets", "expiry-no-actor")));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value)[1].EventTypeName
            .ShouldBe(typeof(StoredMagicLinkExpired).FullName);
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
