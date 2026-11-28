using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Numerics;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Timesheets.Contracts.Commands.ExternalContributions;
using Hexalith.Timesheets.Contracts.Events.ActivityTypes;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections.ActivityTypes;
using Hexalith.Timesheets.Runtime;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.Runtime;
using Hexalith.Timesheets.Server.TimeEntries;
using Hexalith.Timesheets.Server.TimeEntries.Commands;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using NSubstitute;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed class ExternalContributionConfirmationPersistenceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DateTimeOffset BaseUtc = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RecordConfirmAndRestartRecoveryPersistsExactlyOneConfirmationWithoutSensitiveLogs()
    {
        var storage = new ConcurrentDictionary<(string Tenant, string Owner), InMemoryStateManager>();
        var entryId = new TimeEntryId("confirm-http-entry-1");
        ConfirmExternalTimeEntry confirm = ConfirmCommand(entryId);
        string expectedMessageId = ExpectedConfirmMessageId("tenant-1", "party-1", confirm);

        using (var host = new ComposedConfirmationHost(storage))
        using (HttpClient client = host.CreateClient())
        {
            await host.SeedFreshCatalogAsync(client, "tenant-1");

            using HttpResponseMessage recorded = await host.PostWorkloadAsync(
                client, "/api/timesheets/external-contributions/", RecordCommand(entryId));
            recorded.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            (await host.ActorEventsAsync("tenant-1", entryId.Value)).Select(e => e.EventTypeName)
                .ShouldBe([typeof(StoredTimeEntryRecorded).FullName]);

            using HttpResponseMessage firstConfirm = await host.PostWorkloadAsync(
                client, $"/api/timesheets/external-contributions/{entryId.Value}/confirm", confirm);
            firstConfirm.StatusCode.ShouldBe(HttpStatusCode.Accepted);

            EventEnvelope[] afterFirst = await host.ActorEventsAsync("tenant-1", entryId.Value);
            afterFirst.Select(e => e.EventTypeName).ShouldBe(
            [
                typeof(StoredTimeEntryRecorded).FullName,
                typeof(StoredTimeEntryConfirmed).FullName
            ]);
            afterFirst[1].CorrelationId.ShouldBe(expectedMessageId);
            afterFirst[1].CausationId.ShouldBe(expectedMessageId);
            StoredTimeEntryConfirmed stored = JsonSerializer.Deserialize<StoredTimeEntryConfirmed>(
                afterFirst[1].Payload, JsonOptions).ShouldNotBeNull();
            stored.Event.TimeEntryId.ShouldBe(entryId);
            stored.Event.Contributor.ShouldBe(confirm.Contributor);
            stored.Event.Tenant.ShouldBe(new TenantReference("tenant-1"));
            stored.Event.Source.ShouldBe(confirm.Source);
            stored.Event.ConfirmedAtUtc.ShouldBe(BaseUtc);
            stored.Event.ConfirmedAtUtc.Offset.ShouldBe(TimeSpan.Zero);

            // Duplicate before restart: returns 202 and appends zero additional events.
            host.Clock.UtcNow = BaseUtc.AddMinutes(5);
            using HttpResponseMessage duplicateBeforeRestart = await host.PostWorkloadAsync(
                client, $"/api/timesheets/external-contributions/{entryId.Value}/confirm", confirm);
            duplicateBeforeRestart.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            EventEnvelope[] afterRetry = await host.ActorEventsAsync("tenant-1", entryId.Value);
            afterRetry.Length.ShouldBe(2);
            Rehydrate(afterRetry).ConfirmedAtUtc.ShouldBe(BaseUtc);

            string[] confirmationLogs = host.Logs.MessagesFor(typeof(TimeEntryConfirmationSubmissionService).FullName!);
            confirmationLogs.Length.ShouldBe(2);
            confirmationLogs[0].ShouldContain("outcome=committed");
            System.Text.RegularExpressions.Regex.IsMatch(confirmationLogs[0], @"trace=[0-9a-f]{32}").ShouldBeTrue(confirmationLogs[0]);
            confirmationLogs[1].ShouldContain("outcome=already-confirmed");
            System.Text.RegularExpressions.Regex.IsMatch(confirmationLogs[1], @"trace=[0-9a-f]{32}").ShouldBeTrue(confirmationLogs[1]);
            foreach (string log in confirmationLogs)
            {
                AssertLogHasNoProtectedData(log, "tenant-1", "party-1", entryId.Value, confirm.Source);
            }
        }

        // Simulate host + actor restart with a brand-new empty InMemoryCommandStatusStore over retained actor storage.
        using (var restarted = new ComposedConfirmationHost(storage))
        using (HttpClient restartedClient = restarted.CreateClient())
        {
            restarted.Clock.UtcNow = BaseUtc.AddHours(3);
            using HttpResponseMessage duplicateAfterRestart = await restarted.PostWorkloadAsync(
                restartedClient, $"/api/timesheets/external-contributions/{entryId.Value}/confirm", confirm);
            duplicateAfterRestart.StatusCode.ShouldBe(HttpStatusCode.Accepted);

            EventEnvelope[] afterRestart = await restarted.ActorEventsAsync("tenant-1", entryId.Value);
            afterRestart.Select(e => e.EventTypeName).ShouldBe(
            [
                typeof(StoredTimeEntryRecorded).FullName,
                typeof(StoredTimeEntryConfirmed).FullName
            ]);
            Rehydrate(afterRestart).ConfirmedAtUtc.ShouldBe(BaseUtc);
            string[] restartLogs = restarted.Logs.MessagesFor(typeof(TimeEntryConfirmationSubmissionService).FullName!);
            restartLogs.ShouldHaveSingleItem().ShouldContain("outcome=already-confirmed");
            AssertLogHasNoProtectedData(restartLogs[0], "tenant-1", "party-1", entryId.Value, confirm.Source);
        }
    }

    [Fact]
    public async Task ConfirmationPreservesSubmittedAndApprovedLockedStateAcrossRestart()
    {
        var storage = new ConcurrentDictionary<(string Tenant, string Owner), InMemoryStateManager>();
        var submittedEntryId = new TimeEntryId("confirm-submitted-entry");
        var approvedEntryId = new TimeEntryId("confirm-approved-entry");

        using (var host = new ComposedConfirmationHost(
            storage,
            externalPolicy: new ExternalContributionPolicyOptions
            {
                InitialApprovalState = TimeEntryApprovalState.Submitted
            }))
        using (HttpClient client = host.CreateClient())
        {
            await host.SeedFreshCatalogAsync(client, "tenant-1");

            // 1. Submitted entry recorded via HTTP, then confirmed via HTTP.
            using HttpResponseMessage submittedRecord = await host.PostWorkloadAsync(
                client, "/api/timesheets/external-contributions/", RecordCommand(submittedEntryId, "rec-submitted"));
            submittedRecord.StatusCode.ShouldBe(HttpStatusCode.Accepted);

            using HttpResponseMessage submittedConfirm = await host.PostWorkloadAsync(
                client,
                $"/api/timesheets/external-contributions/{submittedEntryId.Value}/confirm",
                ConfirmCommand(submittedEntryId, "conf-submitted"));
            submittedConfirm.StatusCode.ShouldBe(HttpStatusCode.Accepted);

            TimeEntryState submittedState = Rehydrate(await host.ActorEventsAsync("tenant-1", submittedEntryId.Value));
            submittedState.ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
            submittedState.ContributorConfirmationSource.ShouldBe(new ExternalContributionSource("supplier-api", "conf-submitted"));

            // 2. Approved (locked) entry recorded via HTTP, advanced to Approved, then confirmed via HTTP.
            using HttpResponseMessage approvedRecord = await host.PostWorkloadAsync(
                client, "/api/timesheets/external-contributions/", RecordCommand(approvedEntryId, "rec-approved"));
            approvedRecord.StatusCode.ShouldBe(HttpStatusCode.Accepted);

            await host.AppendApprovedEventAsync("tenant-1", approvedEntryId);

            using HttpResponseMessage approvedConfirm = await host.PostWorkloadAsync(
                client,
                $"/api/timesheets/external-contributions/{approvedEntryId.Value}/confirm",
                ConfirmCommand(approvedEntryId, "conf-approved"));
            approvedConfirm.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }

        // Verify state after actor recreation preserves approval, lock, and confirmation.
        using var restarted = new ComposedConfirmationHost(storage);
        EventEnvelope[] persistedApproved = await restarted.ActorEventsAsync("tenant-1", approvedEntryId.Value);
        persistedApproved.Select(e => e.EventTypeName).ShouldBe(
        [
            typeof(StoredTimeEntryRecorded).FullName,
            typeof(StoredTimeEntrySubmitted).FullName,
            typeof(TimeEntryApproved).FullName,
            typeof(StoredTimeEntryConfirmed).FullName
        ]);
        TimeEntryState rehydrated = Rehydrate(persistedApproved);
        rehydrated.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved);
        rehydrated.LockState.ShouldBe(TimeEntryLockState.LockedFromDirectEdit);
        rehydrated.Approver.ShouldBe(new PartyReference("approver-1"));
        rehydrated.TimeEntryApprovalDecisionId.ShouldBe(new TimeEntryApprovalDecisionId("decision-approved-1"));
        rehydrated.ContributorConfirmationSource.ShouldBe(new ExternalContributionSource("supplier-api", "conf-approved"));
    }

    [Fact]
    public async Task LostSubmitAcknowledgementRecoversViaStatusPollAndReadback()
    {
        var storage = new ConcurrentDictionary<(string Tenant, string Owner), InMemoryStateManager>();
        var entryId = new TimeEntryId("confirm-lost-ack-entry");
        using var host = new ComposedConfirmationHost(storage);
        using HttpClient client = host.CreateClient();
        await host.SeedFreshCatalogAsync(client, "tenant-1");

        using HttpResponseMessage recorded = await host.PostWorkloadAsync(
            client, "/api/timesheets/external-contributions/", RecordCommand(entryId));
        recorded.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        host.LoseNextSubmitAck = true;
        using HttpResponseMessage confirmed = await host.PostWorkloadAsync(
            client,
            $"/api/timesheets/external-contributions/{entryId.Value}/confirm",
            ConfirmCommand(entryId));

        confirmed.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await host.ActorEventsAsync("tenant-1", entryId.Value)).Select(e => e.EventTypeName).ShouldBe(
        [
            typeof(StoredTimeEntryRecorded).FullName,
            typeof(StoredTimeEntryConfirmed).FullName
        ]);
    }

    [Fact]
    public async Task RefusalsValidationErrorsAndAuthFailuresLeaveZeroEventsAndOpaqueResponses()
    {
        var storage = new ConcurrentDictionary<(string Tenant, string Owner), InMemoryStateManager>();
        var entryId = new TimeEntryId("confirm-refusal-entry");
        using var host = new ComposedConfirmationHost(storage);
        using HttpClient client = host.CreateClient();
        await host.SeedFreshCatalogAsync(client, "tenant-1");

        using HttpResponseMessage recorded = await host.PostWorkloadAsync(
            client, "/api/timesheets/external-contributions/", RecordCommand(entryId));
        recorded.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        int initialTenant1Count = (await host.ActorEventsAsync("tenant-1", entryId.Value)).Length;
        initialTenant1Count.ShouldBe(1);

        // 1. Anonymous & human bearer -> 401 Unauthorized.
        using HttpResponseMessage anonymous = await client.PostAsJsonAsync(
            $"/api/timesheets/external-contributions/{entryId.Value}/confirm",
            ConfirmCommand(entryId),
            JsonOptions,
            TestContext.Current.CancellationToken);
        anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var humanRequest = new HttpRequestMessage(
            HttpMethod.Post, $"/api/timesheets/external-contributions/{entryId.Value}/confirm")
        {
            Content = JsonContent.Create(ConfirmCommand(entryId), options: JsonOptions)
        };
        humanRequest.Headers.Authorization = new("Bearer", HttpBoundaryHumanCredential.Create("party-1"));
        using HttpResponseMessage human = await client.SendAsync(humanRequest, TestContext.Current.CancellationToken);
        human.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // 2. Route/body mismatch and null TimeEntryId -> 400 opaque ProblemDetails.
        using HttpResponseMessage mismatch = await host.PostWorkloadAsync(
            client, "/api/timesheets/external-contributions/other-id/confirm", ConfirmCommand(entryId));
        mismatch.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await mismatch.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldContain("External contribution request was not accepted.");

        using HttpResponseMessage nullId = await host.PostWorkloadRawJsonAsync(
            client,
            $"/api/timesheets/external-contributions/{entryId.Value}/confirm",
            """{"timeEntryId":null,"contributor":{"partyId":"party-1"},"source":{"sourceSystem":"supplier-api","externalRequestId":"confirm-1"}}""");
        nullId.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await nullId.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldContain("External contribution request was not accepted.");

        using HttpResponseMessage nullBody = await host.PostWorkloadRawJsonAsync(
            client,
            $"/api/timesheets/external-contributions/{entryId.Value}/confirm",
            "null");
        nullBody.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await nullBody.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldContain("External contribution request was not accepted.");

        // 3. Wrong tenant -> 403 opaque ProblemDetails, zero events on any stream.
        host.SetRequestClaims(tenantId: "tenant-2", partyId: "party-1");
        using HttpResponseMessage wrongTenant = await host.PostWorkloadAsync(
            client, $"/api/timesheets/external-contributions/{entryId.Value}/confirm", ConfirmCommand(entryId));
        wrongTenant.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        string wrongTenantBody = await wrongTenant.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        wrongTenantBody.ShouldContain("External contribution request was not accepted.");
        wrongTenantBody.ShouldNotContain("tenant-1");
        wrongTenantBody.ShouldNotContain("tenant-2");
        storage.ContainsKey(("tenant-2", entryId.Value)).ShouldBeFalse();

        // 4. Synthetic workload: subject without party_id -> 403 opaque ProblemDetails, zero events.
        host.SetSyntheticWorkloadClaims(tenantId: "tenant-1", subject: "workload:timesheets");
        var callsBeforeWorkloadSubject = host.BoundaryCalls;
        using HttpResponseMessage workloadSubject = await host.PostWorkloadAsync(
            client, $"/api/timesheets/external-contributions/{entryId.Value}/confirm", ConfirmCommand(entryId));
        workloadSubject.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await workloadSubject.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldContain("External contribution request was not accepted.");
        host.BoundaryCalls.ShouldBe(callsBeforeWorkloadSubject);

        // 5. Wrong contributor -> 403 opaque ProblemDetails, zero events.
        host.SetRequestClaims(tenantId: "tenant-1", partyId: "party-1");
        using HttpResponseMessage wrongContributor = await host.PostWorkloadAsync(
            client,
            $"/api/timesheets/external-contributions/{entryId.Value}/confirm",
            ConfirmCommand(entryId) with { Contributor = new PartyReference("party-2") });
        wrongContributor.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // 6. Unrecorded entry -> 403 opaque ProblemDetails, zero events.
        var missingEntryId = new TimeEntryId("confirm-missing-entry");
        using HttpResponseMessage missingEntry = await host.PostWorkloadAsync(
            client,
            $"/api/timesheets/external-contributions/{missingEntryId.Value}/confirm",
            ConfirmCommand(missingEntryId));
        missingEntry.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        storage.ContainsKey(("tenant-1", missingEntryId.Value)).ShouldBeFalse();

        // Still only the original recording event on tenant-1.
        (await host.ActorEventsAsync("tenant-1", entryId.Value)).Length.ShouldBe(initialTenant1Count);

        // 7. Confirm validly once, then attempt conflicting source -> 403, still 1 confirmation event.
        host.SetRequestClaims(tenantId: "tenant-1", partyId: "party-1", subjectOnly: true);
        using HttpResponseMessage validConfirm = await host.PostWorkloadAsync(
            client, $"/api/timesheets/external-contributions/{entryId.Value}/confirm", ConfirmCommand(entryId, "confirm-1"));
        validConfirm.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        using HttpResponseMessage conflictingSource = await host.PostWorkloadAsync(
            client, $"/api/timesheets/external-contributions/{entryId.Value}/confirm", ConfirmCommand(entryId, "confirm-2"));
        conflictingSource.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await host.ActorEventsAsync("tenant-1", entryId.Value)).Select(e => e.EventTypeName).ShouldBe(
        [
            typeof(StoredTimeEntryRecorded).FullName,
            typeof(StoredTimeEntryConfirmed).FullName
        ]);

        string[] logs = host.Logs.MessagesFor(typeof(TimeEntryConfirmationSubmissionService).FullName!);
        logs.Count(msg => msg.Contains("outcome=policy-refusal", StringComparison.Ordinal)).ShouldBe(5);
        foreach (string log in logs)
        {
            AssertLogHasNoProtectedData(log, "tenant-1", "party-1", entryId.Value, ConfirmCommand(entryId).Source);
            log.ShouldNotContain("tenant-2");
            log.ShouldNotContain("party-2");
            log.ShouldNotContain("workload:timesheets");
        }
    }

    [Fact]
    public async Task ConflictingIncompleteAndForeignSignedIdentityBindingsFailBeforeDispatch()
    {
        var storage = new ConcurrentDictionary<(string Tenant, string Owner), InMemoryStateManager>();
        var entryId = new TimeEntryId("confirm-binding-entry");
        using var host = new ComposedConfirmationHost(storage);
        using HttpClient client = host.CreateClient();
        var initialCalls = host.BoundaryCalls;
        (string Domain, string? Actor, Claim[] ExtraClaims, Dictionary<string, string>? ExtraBindings)[] refusedBindings =
        [
            ("timesheets", null, [], null),
            ("foreign", "party-1", [], null),
            ("timesheets", "workload:eventstore", [], null),
            ("timesheets", "party-1", [new Claim("tenant_id", "tenant-2")], null),
            ("timesheets", "party-1", [new Claim("party_id", "party-2")], null),
            ("timesheets", "party-1", [new Claim(ClaimTypes.NameIdentifier, "party-2")], null),
            ("timesheets", "party-1", [], new Dictionary<string, string>
            {
                [EventStoreWorkloadAuthenticationDefaults.ProjectionTypeBindingClaimType] = "unexpected-projection"
            })
        ];

        foreach (var (domain, actor, extraClaims, extraBindings) in refusedBindings)
        {
            host.SetSignedBindings(domain, actor, extraClaims, extraBindings);
            using HttpResponseMessage record = await host.PostWorkloadAsync(
                client, "/api/timesheets/external-contributions/", RecordCommand(entryId));
            using HttpResponseMessage confirm = await host.PostWorkloadAsync(
                client, $"/api/timesheets/external-contributions/{entryId.Value}/confirm", ConfirmCommand(entryId));
            record.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            confirm.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        host.BoundaryCalls.ShouldBe(initialCalls);
        storage.ShouldBeEmpty();
        string[] logs = host.Logs.MessagesFor(typeof(TimeEntryConfirmationSubmissionService).FullName!);
        logs.Count(msg => msg.Contains("outcome=policy-refusal", StringComparison.Ordinal)).ShouldBe(refusedBindings.Length);
    }

    [Fact]
    public async Task ConflictingOrBlankNonBindingClaimsFailBeforeDispatch()
    {
        var storage = new ConcurrentDictionary<(string Tenant, string Owner), InMemoryStateManager>();
        var entryId = new TimeEntryId("confirm-nonbinding-conflict-entry");
        using var host = new ComposedConfirmationHost(storage);
        using HttpClient client = host.CreateClient();
        var initialCalls = host.BoundaryCalls;
        Claim[][] refusedClaims =
        [
            [new Claim("tenant_id", "tenant-1"), new Claim("tenant", "tenant-2"), new Claim("party_id", "party-1")],
            [new Claim("tenant_id", "tenant-1"), new Claim("party_id", "party-1"), new Claim("party_id", "party-2")],
            [new Claim("tenant_id", "tenant-1"), new Claim(ClaimTypes.NameIdentifier, "party-1"), new Claim(ClaimTypes.NameIdentifier, "party-2")],
            [new Claim("tenant_id", "tenant-1"), new Claim("party_id", " "), new Claim("party_id", "party-1")],
            [new Claim("tenant_id", "tenant-1"), new Claim("party_id", " "), new Claim(ClaimTypes.NameIdentifier, "party-1")],
            [new Claim("tenant_id", "tenant-1"), new Claim("party_id", "party-1"), new Claim(ClaimTypes.NameIdentifier, "party-2")]
        ];

        foreach (Claim[] claims in refusedClaims)
        {
            host.SetRawRequestClaims(claims);
            using HttpResponseMessage record = await host.PostWorkloadAsync(
                client, "/api/timesheets/external-contributions/", RecordCommand(entryId));
            using HttpResponseMessage confirm = await host.PostWorkloadAsync(
                client, $"/api/timesheets/external-contributions/{entryId.Value}/confirm", ConfirmCommand(entryId));
            record.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            confirm.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        host.BoundaryCalls.ShouldBe(initialCalls);
        storage.ShouldBeEmpty();
        string[] logs = host.Logs.MessagesFor(typeof(TimeEntryConfirmationSubmissionService).FullName!);
        logs.Count(msg => msg.Contains("outcome=policy-refusal", StringComparison.Ordinal)).ShouldBe(refusedClaims.Length);
    }

    [Fact]
    public async Task NullAndBlankContributorOrSourceAreOpaqueBeforeGuardOrGateway()
    {
        var storage = new ConcurrentDictionary<(string Tenant, string Owner), InMemoryStateManager>();
        using var host = new ComposedConfirmationHost(storage);
        using HttpClient client = host.CreateClient();
        var initialCalls = host.BoundaryCalls;
        (string Payload, HttpStatusCode Status)[] invalidPayloads =
        [
            ("""{"timeEntryId":{"value":"confirm-invalid-entry"},"contributor":null,"source":{"sourceSystem":"supplier-api","externalRequestId":"confirm-1"}}""", HttpStatusCode.Forbidden),
            ("""{"timeEntryId":{"value":"confirm-invalid-entry"},"contributor":{"partyId":" "},"source":{"sourceSystem":"supplier-api","externalRequestId":"confirm-1"}}""", HttpStatusCode.BadRequest),
            ("""{"timeEntryId":{"value":"confirm-invalid-entry"},"contributor":{"partyId":"party-1"},"source":null}""", HttpStatusCode.Forbidden),
            ("""{"timeEntryId":{"value":"confirm-invalid-entry"},"contributor":{"partyId":"party-1"},"source":{"sourceSystem":" ","externalRequestId":"confirm-1"}}""", HttpStatusCode.BadRequest),
            ("""{"timeEntryId":{"value":"confirm-invalid-entry"},"contributor":{"partyId":"party-1"},"source":{"sourceSystem":"supplier-api","externalRequestId":" "}}""", HttpStatusCode.BadRequest),
            ("[]", HttpStatusCode.BadRequest),
            ("""{"timeEntryId":123}""", HttpStatusCode.BadRequest),
            ("""{"timeEntryId":{"value":"confirm-invalid-entry"},"timeEntryId":{"value":"confirm-invalid-entry"},"contributor":{"partyId":"party-1"},"source":{"sourceSystem":"supplier-api","externalRequestId":"confirm-1"}}""", HttpStatusCode.BadRequest)
        ];

        foreach (var (payload, status) in invalidPayloads)
        {
            using HttpResponseMessage response = await host.PostWorkloadRawJsonAsync(
                client, "/api/timesheets/external-contributions/confirm-invalid-entry/confirm", payload);
            response.StatusCode.ShouldBe(status);
            string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            body.ShouldContain("External contribution request was not accepted.");
            body.ShouldNotContain("supplier-api");
            body.ShouldNotContain("party-1");
            body.ShouldNotContain("confirm-1");
        }

        host.BoundaryCalls.ShouldBe(initialCalls);
        storage.ShouldBeEmpty();
    }

    [Fact]
    public async Task GatewayFaultWithholdsAcceptanceLogsIndeterminateCommitAndRetryRecovers()
    {
        var storage = new ConcurrentDictionary<(string Tenant, string Owner), InMemoryStateManager>();
        var entryId = new TimeEntryId("confirm-fault-retry-entry");
        ConfirmExternalTimeEntry confirm = ConfirmCommand(entryId);
        using var host = new ComposedConfirmationHost(
            storage,
            pollingOptions: new MagicLinkSubmissionPollingOptions
            {
                Timeout = TimeSpan.FromSeconds(2),
                PollInterval = TimeSpan.FromMilliseconds(15),
                ReadbackTimeout = TimeSpan.FromMilliseconds(100)
            });
        using HttpClient client = host.CreateClient();
        await host.SeedFreshCatalogAsync(client, "tenant-1");

        using HttpResponseMessage recorded = await host.PostWorkloadAsync(
            client, "/api/timesheets/external-contributions/", RecordCommand(entryId));
        recorded.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        host.FailReadbackAfterCommit = true;
        using HttpResponseMessage failed = await host.PostWorkloadAsync(
            client, $"/api/timesheets/external-contributions/{entryId.Value}/confirm", confirm);
        failed.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        string failedBody = await failed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        failedBody.ShouldContain("External contribution request was not accepted.");

        string[] logsAfterFault = host.Logs.MessagesFor(typeof(TimeEntryConfirmationSubmissionService).FullName!);
        logsAfterFault.ShouldHaveSingleItem().ShouldContain("outcome=indeterminate-commit");
        AssertLogHasNoProtectedData(logsAfterFault[0], "tenant-1", "party-1", entryId.Value, confirm.Source);

        host.FailReadbackAfterCommit = false;
        using HttpResponseMessage recovered = await host.PostWorkloadAsync(
            client, $"/api/timesheets/external-contributions/{entryId.Value}/confirm", confirm);
        recovered.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        (await host.ActorEventsAsync("tenant-1", entryId.Value)).Select(e => e.EventTypeName).ShouldBe(
        [
            typeof(StoredTimeEntryRecorded).FullName,
            typeof(StoredTimeEntryConfirmed).FullName
        ]);
        string[] allLogs = host.Logs.MessagesFor(typeof(TimeEntryConfirmationSubmissionService).FullName!);
        allLogs.Length.ShouldBe(2);
        allLogs[1].ShouldContain("outcome=already-confirmed");
        AssertLogHasNoProtectedData(allLogs[1], "tenant-1", "party-1", entryId.Value, confirm.Source);
    }

    [Theory]
    [InlineData("forged-origin")]
    [InlineData("forged-actor")]
    [InlineData("wrong-tenant")]
    [InlineData("wrong-aggregate")]
    [InlineData("corrupt-replay")]
    [InlineData("denied-guard")]
    public async Task DirectProcessReceiverRefusalsReturnNoOpAndAppendZeroEvents(string scenario)
    {
        var storage = new ConcurrentDictionary<(string Tenant, string Owner), InMemoryStateManager>();
        var entryId = new TimeEntryId($"confirm-process-{scenario}");
        using var host = new ComposedConfirmationHost(storage);
        using HttpClient client = host.CreateClient();
        await host.SeedFreshCatalogAsync(client, "tenant-1");

        using HttpResponseMessage recorded = await host.PostWorkloadAsync(
            client, "/api/timesheets/external-contributions/", RecordCommand(entryId));
        recorded.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        if (scenario == "denied-guard")
        {
            host.AllowAuthorization = false;
        }

        var intent = new CommitTimeEntryConfirmation(
            ConfirmCommand(entryId),
            new TenantReference("tenant-1"),
            new PartyReference("party-1"),
            BaseUtc);
        var envelope = new CommandEnvelope(
            UniqueIdHelper.GenerateSortableUniqueStringId(),
            scenario == "wrong-tenant" ? "tenant-2" : "tenant-1",
            "timesheets",
            scenario == "wrong-aggregate" ? "other-entry" : entryId.Value,
            typeof(CommitTimeEntryConfirmation).FullName!,
            JsonSerializer.SerializeToUtf8Bytes(intent, JsonOptions),
            "corr-direct",
            "corr-direct",
            scenario == "forged-actor" ? "party-2" : "party-1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [EventStoreGatewayVerifiedOrigin.ExtensionKey] = scenario == "forged-origin" ? "forged" : "timesheets",
                [EventStoreGatewayVerifiedOrigin.ActorExtensionKey] = "party-1"
            });

        var recordedPayload = new StoredTimeEntryRecorded(new TimeEntryRecorded(
            entryId,
            TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            new PartyReference("party-1"),
            new ActivityTypeId("activity-1"),
            ActivityTypeScope.Tenant,
            new DateOnly(2026, 11, 20),
            60,
            BillableState.Billable,
            TimeEntryApprovalState.Draft,
            ContributorCategory.ExternalContributor,
            null));
        object state = scenario == "corrupt-replay"
            ? new object[] { recordedPayload, recordedPayload }
            : new object[] { recordedPayload };

        using HttpResponseMessage response = await host.PostProcessAsync(client, new DomainServiceRequest(envelope, state));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        DomainServiceWireResult wire = (await response.Content.ReadFromJsonAsync<DomainServiceWireResult>(
            JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        wire.IsRejection.ShouldBeFalse();
        wire.Events.ShouldBeEmpty();

        // Also run through the real AggregateActor for non-corrupt cases to verify zero stream writes.
        if (scenario != "corrupt-replay")
        {
            await host.ProcessThroughActorAsync(envelope);
            (await host.ActorEventsAsync("tenant-1", entryId.Value)).Select(e => e.EventTypeName)
                .ShouldBe([typeof(StoredTimeEntryRecorded).FullName]);
            if (scenario == "wrong-tenant")
            {
                (await host.ActorEventsAsync("tenant-2", entryId.Value)).ShouldBeEmpty();
            }
            else if (scenario == "wrong-aggregate")
            {
                (await host.ActorEventsAsync("tenant-1", "other-entry")).ShouldBeEmpty();
            }
        }
    }

    private static SubmitExternalTimeEntry RecordCommand(TimeEntryId entryId, string requestId = "record-1")
        => new(
            entryId,
            TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            new PartyReference("party-1"),
            new ActivityTypeId("activity-1"),
            new DateOnly(2026, 11, 20),
            60,
            BillableState.Billable,
            new ExternalContributionSource("supplier-api", requestId));

    private static ConfirmExternalTimeEntry ConfirmCommand(TimeEntryId entryId, string requestId = "confirm-1")
        => new(
            entryId,
            new PartyReference("party-1"),
            new ExternalContributionSource("supplier-api", requestId));

    private static string ExpectedConfirmMessageId(
        string tenantId,
        string actorPartyId,
        ConfirmExternalTimeEntry command)
        => ExpectedMessageId(JsonSerializer.Serialize(new[]
        {
            "confirm",
            tenantId,
            actorPartyId,
            command.TimeEntryId.Value,
            command.Contributor.PartyId,
            command.Source.SourceSystem,
            command.Source.ExternalRequestId
        }));

    internal static string ExpectedMessageId(string stableInput)
    {
        const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(stableInput));
        BigInteger number = new(digest.AsSpan(0, 16), isUnsigned: true, isBigEndian: true);
        char[] encoded = new char[26];
        for (int index = encoded.Length - 1; index >= 0; index--)
        {
            encoded[index] = CrockfordAlphabet[(int)(number & 31)];
            number >>= 5;
        }

        return new string(encoded);
    }

    private static TimeEntryState Rehydrate(EventEnvelope[] events)
    {
        TimeEntryState state = new();
        foreach (EventEnvelope item in events)
        {
            string typeName = item.EventTypeName.Split(',', 2)[0];
            if (typeName == typeof(StoredTimeEntryRecorded).FullName)
            {
                state.Apply(JsonSerializer.Deserialize<StoredTimeEntryRecorded>(item.Payload, JsonOptions)!);
            }
            else if (typeName == typeof(StoredTimeEntrySubmitted).FullName)
            {
                state.Apply(JsonSerializer.Deserialize<StoredTimeEntrySubmitted>(item.Payload, JsonOptions)!);
            }
            else if (typeName == typeof(TimeEntryApproved).FullName)
            {
                state.Apply(JsonSerializer.Deserialize<TimeEntryApproved>(item.Payload, JsonOptions)!);
            }
            else if (typeName == typeof(StoredTimeEntryConfirmed).FullName)
            {
                state.Apply(JsonSerializer.Deserialize<StoredTimeEntryConfirmed>(item.Payload, JsonOptions)!);
            }
            else
            {
                throw new InvalidOperationException($"Unexpected event type: {item.EventTypeName}");
            }
        }

        return state;
    }

    private static void AssertLogHasNoProtectedData(
        string message,
        string tenantId,
        string actorId,
        string entryId,
        ExternalContributionSource source)
    {
        message.ShouldNotContain(tenantId);
        message.ShouldNotContain(actorId);
        message.ShouldNotContain(entryId);
        message.ShouldNotContain(source.SourceSystem);
        message.ShouldNotContain(source.ExternalRequestId);
        message.ShouldNotContain("token", Case.Insensitive);
        message.ShouldNotContain("bearer", Case.Insensitive);
        message.ShouldNotContain("secret", Case.Insensitive);
    }

    internal sealed class ComposedConfirmationHost : WebApplicationFactory<Program>
    {
        private const string ChannelToken = "timesheets-confirm-test-channel-token";
        private readonly ConcurrentDictionary<(string Tenant, string Owner), InMemoryStateManager> _storage;
        private readonly ExternalContributionPolicyOptions _externalPolicy;
        private readonly MagicLinkSubmissionPollingOptions? _pollingOptions;
        private readonly HttpRouteCatalogFixture _routeCatalog = new();
        private readonly InMemoryCommandStatusStore _statuses = new();
        private readonly InMemoryReadModelStore _readModels = new();
        private readonly SemaphoreSlim _actorTurn = new(1, 1);
        private HttpClient? _internalClient;
        private int _guardChecks;
        private int _streamReads;
        private int _submissions;
        private Claim[] _requestClaims = [];
        private IReadOnlyDictionary<string, string>? _requestBindings = SignedBindings("timesheets", "party-1");

        public ComposedConfirmationHost(
            ConcurrentDictionary<(string Tenant, string Owner), InMemoryStateManager> storage,
            ExternalContributionPolicyOptions? externalPolicy = null,
            MagicLinkSubmissionPollingOptions? pollingOptions = null)
        {
            _storage = storage;
            _externalPolicy = externalPolicy ?? ExternalContributionPolicyOptions.Default;
            _pollingOptions = pollingOptions;
        }

        public IntegrationTestTimeProvider Clock { get; } = new(BaseUtc);

        public TestLoggerProvider Logs { get; } = new();

        public bool AllowAuthorization { get; set; } = true;

        public bool LoseNextSubmitAck { get; set; }

        public bool FailReadbackAfterCommit { get; set; }

        public Func<CancellationToken, Task>? BeforeStreamReadAsync { get; set; }

        public Action<Exception?>? RecordingRequestCompleted { get; init; }

        public bool EmitMissingStatusPlaceholder { get; set; }

        public FakeEventPublisher Publisher { get; } = new();

        public (int GuardChecks, int StreamReads, int Submissions) BoundaryCalls
            => (Volatile.Read(ref _guardChecks), Volatile.Read(ref _streamReads), Volatile.Read(ref _submissions));

        public void SetRequestClaims(string tenantId, string partyId, bool subjectOnly = false)
        {
            _requestBindings = null;
            Claim tenant = new("tenant_id", tenantId);
            Claim subject = new(ClaimTypes.NameIdentifier, partyId);
            _requestClaims = subjectOnly ? [tenant, subject] : [tenant, new Claim("party_id", partyId), subject];
        }

        public void SetSyntheticWorkloadClaims(string tenantId, string subject)
        {
            _requestBindings = null;
            _requestClaims =
            [
                new Claim("tenant_id", tenantId),
                new Claim(ClaimTypes.NameIdentifier, subject)
            ];
        }

        public void SetRawRequestClaims(params Claim[] claims)
        {
            _requestBindings = null;
            _requestClaims = claims;
        }

        public void SetSignedBindings(
            string domain,
            string? actor,
            Claim[]? extraClaims = null,
            Dictionary<string, string>? extraBindings = null)
        {
            Dictionary<string, string> bindings = SignedBindings(domain, actor);
            if (extraBindings is not null)
            {
                foreach ((string key, string value) in extraBindings)
                {
                    bindings[key] = value;
                }
            }

            _requestBindings = bindings;
            _requestClaims = extraClaims ?? [];
        }

        private static Dictionary<string, string> SignedBindings(string domain, string? actor)
        {
            var bindings = new Dictionary<string, string>
            {
                [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = "tenant-1",
                [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = domain
            };
            if (actor is not null)
            {
                bindings[EventStoreWorkloadAuthenticationDefaults.ActorBindingClaimType] = actor;
            }

            return bindings;
        }

        public async Task SeedFreshCatalogAsync(HttpClient client, string tenantId)
        {
            var activityCreated = new ActivityTypeCreated(
                new ActivityTypeId("activity-1"),
                ActivityTypeScope.Tenant,
                null,
                "Delivery",
                BillableState.Billable);
            var projectionEvent = new ProjectionEventDto(
                activityCreated.GetType().FullName!,
                JsonSerializer.SerializeToUtf8Bytes(activityCreated, JsonOptions),
                "json",
                1,
                BaseUtc,
                "seed-corr",
                "seed-1",
                "operator-1",
                1);
            using IServiceScope scope = Services.CreateScope();
            DomainProjectionIdentityOptions identity = scope.ServiceProvider
                .GetRequiredService<IOptions<DomainProjectionIdentityOptions>>()
                .Value;
            ProjectionDispatchRoute[] routes = scope.ServiceProvider
                .GetServices<IAsyncDomainProjectionHandler>()
                .Where(static handler => string.Equals(handler.Domain, "timesheets", StringComparison.Ordinal))
                .Select(static handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))
                .ToArray();
            string fingerprint = ProjectionRouteCatalogFingerprint.Compute(identity.AppId, identity.ServiceVersion, routes);
            var dispatch = new ProjectionDispatchRequest(
                new ProjectionRequest(tenantId, "timesheets", "activity-1", [projectionEvent]),
                [TenantActivityTypeCatalogProjection.ProjectionName],
                $"seed-catalog-{tenantId}",
                fingerprint);
            using var request = new HttpRequestMessage(HttpMethod.Post, "/project/v2")
            {
                Content = JsonContent.Create(dispatch, options: JsonOptions)
            };
            await AddWorkloadHeadersAsync(request, EventStoreWorkloadOperations.DomainServiceProject);
            using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var catalogHandler = new TenantActivityTypeCatalogProjectionHandler(_readModels);
            var coordinator = new SharedProjectionEpochCoordinator(_readModels, _readModels);
            SharedProjectionScope scopeEpoch = catalogHandler.CreateScope(tenantId);
            var rebuildIdentity = new DomainSharedProjectionRebuildIdentity(
                tenantId,
                "timesheets",
                catalogHandler.ProjectionType,
                $"seed-rebuild-{tenantId}",
                fingerprint);
            var completeHistory = new ProjectionRequest(tenantId, "timesheets", "activity-1", [projectionEvent]);
            await coordinator.BeginAsync(scopeEpoch, rebuildIdentity.OperationId, "fixture-inventory",
                new Dictionary<string, long> { ["activity-1"] = 1 });
            DomainSharedProjectionRebuildCandidate candidate = await catalogHandler.CreateEmptyCandidateAsync(
                rebuildIdentity, TestContext.Current.CancellationToken);
            candidate = await catalogHandler.AccumulateAsync(
                rebuildIdentity, candidate, completeHistory, TestContext.Current.CancellationToken);
            DomainProjectionRebuildPlan plan = await catalogHandler.FinalizeAsync(
                rebuildIdentity, candidate, TestContext.Current.CancellationToken);
            await coordinator.StageAsync(scopeEpoch, rebuildIdentity.OperationId, plan.Operations);
            await coordinator.CommitAsync(scopeEpoch, rebuildIdentity.OperationId);
            await coordinator.CatchUpAsync(scopeEpoch,
                (delivery, generation, cancellation) => catalogHandler.FoldCatchUpAsync(
                    delivery, generation, coordinator, cancellation));
        }

        public async Task AppendApprovedEventAsync(string tenantId, TimeEntryId entryId)
        {
            TimeEntryState current = Rehydrate(await ActorEventsAsync(tenantId, entryId.Value));
            current.ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
            var approved = new TimeEntryApproved(
                entryId,
                new PartyReference("approver-1"),
                new TenantReference(tenantId),
                BaseUtc.AddMinutes(1),
                new TimeEntryApprovalDecisionId("decision-approved-1"),
                TimeEntryApprovalState.Approved,
                new ApprovalAuthoritySourceAttribution(
                    ApprovalAuthorityAction.EntryApproval,
                    ApprovalAuthoritySource.ProjectApprover,
                    ApprovalAuthorityDecisionState.Allowed,
                    "policy-key",
                    "v1",
                    ProjectionFreshnessMetadata.Fresh),
                TimeEntryApprovalScope.IndividualEntry);
            string messageId = UniqueIdHelper.GenerateSortableUniqueStringId();
            var command = new CommandEnvelope(
                messageId,
                tenantId,
                "timesheets",
                entryId.Value,
                "approve-time-entry-fixture",
                "{}"u8.ToArray(),
                messageId,
                messageId,
                approved.Approver.PartyId,
                null);
            var payload = new SerializedDomainEventPayload(
                typeof(TimeEntryApproved).FullName!,
                JsonSerializer.SerializeToUtf8Bytes(approved, JsonOptions),
                "json",
                1,
                null,
                null);
            IDomainServiceInvoker approvalInvoker = Substitute.For<IDomainServiceInvoker>();
            approvalInvoker.InvokeAsync(command, Arg.Any<object?>(), Arg.Any<CancellationToken>())
                .Returns(DomainResult.Success([payload]));

            CommandProcessingResult result = await ProcessThroughActorAsync(command, approvalInvoker);
            result.Accepted.ShouldBeTrue();
            result.EventCount.ShouldBe(1);
        }

        public Task<EventEnvelope[]> ActorEventsAsync(string tenantId, string aggregateId)
            => CreateActor(tenantId, aggregateId).GetEventsAsync(0);

        public Task<CommandStatusRecord?> ReadStatusAsync(string tenantId, string messageId)
            => _statuses.ReadStatusAsync(tenantId, messageId, TestContext.Current.CancellationToken);

        public async Task<CommandProcessingResult> ProcessThroughActorAsync(
            CommandEnvelope envelope, IDomainServiceInvoker? invoker = null)
        {
            await _actorTurn.WaitAsync(TestContext.Current.CancellationToken);
            try
            {
                return await CreateActor(envelope.TenantId, envelope.AggregateId, invoker)
                    .ProcessCommandAsync(envelope, TestContext.Current.CancellationToken);
            }
            finally
            {
                _actorTurn.Release();
            }
        }

        public async Task<HttpResponseMessage> PostWorkloadAsync<T>(
            HttpClient client, string route, T body, CancellationToken? cancellationToken = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, route)
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            };
            CancellationToken caller = cancellationToken ?? TestContext.Current.CancellationToken;
            await AddWorkloadHeadersAsync(request, EventStoreWorkloadOperations.DomainServiceProcess, caller, _requestBindings);
            return await client.SendAsync(request, caller);
        }

        public async Task<HttpResponseMessage> PostWorkloadRawJsonAsync(
            HttpClient client, string route, string rawJson, string mediaType = "application/json")
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, route)
            {
                Content = new StringContent(rawJson, Encoding.UTF8, mediaType)
            };
            await AddWorkloadHeadersAsync(request, EventStoreWorkloadOperations.DomainServiceProcess, bindings: _requestBindings);
            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        public async Task<HttpResponseMessage> PostWorkloadWithoutBodyAsync(HttpClient client, string route)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, route);
            await AddWorkloadHeadersAsync(request, EventStoreWorkloadOperations.DomainServiceProcess, bindings: _requestBindings);
            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        public async Task<HttpResponseMessage> PostProcessAsync(HttpClient client, DomainServiceRequest payload)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/process")
            {
                Content = JsonContent.Create(payload, options: JsonOptions)
            };
            await AddWorkloadHeadersAsync(request, EventStoreWorkloadOperations.DomainServiceProcess);
            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting(DaprAppChannelToken.ConfigurationKey, ChannelToken);
            builder.UseSetting("EventStore:DomainService:AppId", "timesheets");
            builder.UseSetting("Authentication:JwtBearer:Issuer", "timesheets-test-issuer");
            builder.UseSetting("Authentication:JwtBearer:Audience", "hexalith-eventstore");
            builder.UseSetting("Authentication:JwtBearer:SigningKey", Convert.ToBase64String(Encoding.UTF8.GetBytes(new string('k', 32))));
            builder.UseSetting("Authentication:JwtBearer:AllowedAlgorithms:0", SecurityAlgorithms.HmacSha256);
            builder.UseSetting("Authentication:JwtBearer:RequireHttpsMetadata", "false");
            builder.UseSetting("Authentication:WorkloadIssuer:Workload", "eventstore");

            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddProvider(Logs);
            });

            builder.ConfigureServices(services =>
            {
                _routeCatalog.Configure(services);
                services.Configure<InternalSurfaceOptions>(options => options.AllowOnAnyPort = true);
                if (RecordingRequestCompleted is { } onCompleted)
                {
                    services.AddSingleton<IStartupFilter>(new RecordingRequestCompletionFilter(onCompleted));
                }

                services.RemoveAll<ExternalContributionPolicyOptions>();
                services.AddSingleton(_externalPolicy);

                services.RemoveAll<ITimesheetsAccessGuard>();
                services.AddSingleton<ITimesheetsAccessGuard>(new ConfigurableAccessGuard(this));

                services.RemoveAll<IReadModelStore>();
                services.RemoveAll<DaprReadModelStore>();
                services.AddSingleton<IReadModelStore>(_readModels);
                services.RemoveAll<IReadModelBatchStore>();
                services.AddSingleton<IReadModelBatchStore>(_readModels);

                services.RemoveAll<IEventStoreGatewayClient>();
                services.AddSingleton<IEventStoreGatewayClient>(new ActorBackedGatewayClient(this));

                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Clock);

                if (_pollingOptions is not null)
                {
                    services.PostConfigure<MagicLinkSubmissionPollingOptions>(options =>
                    {
                        options.Timeout = _pollingOptions.Timeout;
                        options.PollInterval = _pollingOptions.PollInterval;
                        options.ReadbackTimeout = _pollingOptions.ReadbackTimeout;
                    });
                }

                services.AddSingleton<IClaimsTransformation>(provider =>
                    new DynamicClaimsTransformation(
                        provider.GetRequiredService<IHttpContextAccessor>(),
                        () => _requestClaims));
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _internalClient?.Dispose();
                _actorTurn.Dispose();
            }

            base.Dispose(disposing);
        }

        private AggregateActor CreateActor(string tenantId, string aggregateId, IDomainServiceInvoker? invoker = null)
        {
            if (invoker is null)
            {
                invoker = Substitute.For<IDomainServiceInvoker>();
                invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
                    .Returns(call => InvokeProcessEndpointAsync(
                        call.ArgAt<CommandEnvelope>(0),
                        call.ArgAt<object?>(1),
                        call.ArgAt<CancellationToken>(2)));
            }

            InMemoryStateManager stateManager = _storage.GetOrAdd((tenantId, aggregateId), static _ => new());
            return MagicLinkConfirmationHttpBoundaryTests.CreateMagicLinkActor(
                new TimeEntryId(aggregateId), stateManager, _statuses, invoker, tenantId, Publisher);
        }

        private async Task<DomainResult> InvokeProcessEndpointAsync(
            CommandEnvelope command,
            object? currentState,
            CancellationToken cancellationToken)
        {
            _internalClient ??= CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, "/process")
            {
                Content = JsonContent.Create(new DomainServiceRequest(command, currentState), options: JsonOptions)
            };
            await AddWorkloadHeadersAsync(request, EventStoreWorkloadOperations.DomainServiceProcess, cancellationToken);
            using HttpResponseMessage response = await _internalClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            DomainServiceWireResult wire = (await response.Content.ReadFromJsonAsync<DomainServiceWireResult>(
                JsonOptions, cancellationToken)).ShouldNotBeNull();
            if (wire.Events.Count == 0)
            {
                return DomainResult.NoOp();
            }

            IEventPayload[] events = wire.Events
                .Select<DomainServiceWireEvent, IEventPayload>(wireEvent => wire.IsRejection
                    ? new SerializedDomainRejectionEventPayload(
                        wireEvent.EventTypeName,
                        wireEvent.Payload,
                        wireEvent.SerializationFormat,
                        wireEvent.MetadataVersion,
                        wireEvent.EventContractType,
                        wireEvent.PayloadVersion)
                    : new SerializedDomainEventPayload(
                        wireEvent.EventTypeName,
                        wireEvent.Payload,
                        wireEvent.SerializationFormat,
                        wireEvent.MetadataVersion,
                        wireEvent.EventContractType,
                        wireEvent.PayloadVersion))
                .ToArray();
            return new DomainResult(events);
        }

        private async Task AddWorkloadHeadersAsync(
            HttpRequestMessage request,
            string operation,
            CancellationToken cancellationToken = default,
            IReadOnlyDictionary<string, string>? bindings = null)
        {
            DomainProjectionIdentityOptions identity = Services
                .GetRequiredService<IOptions<DomainProjectionIdentityOptions>>()
                .Value;
            string assertion = (await Services.GetRequiredService<IWorkloadAssertionIssuer>()
                .IssueAsync(new WorkloadAssertionRequest(identity.AppId, operation, bindings), cancellationToken)).ShouldNotBeNull();
            request.Headers.Add(DaprAppChannelToken.HeaderName, ChannelToken);
            request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);
            await _routeCatalog.AddHeadersAsync(Services, request, cancellationToken);
        }

        private sealed class ActorBackedGatewayClient(ComposedConfirmationHost host) : IEventStoreGatewayClient
        {
            public Task<SubmitCommandResponse> SubmitCommandAsync(SubmitCommandRequest request, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("Must use workload command route.");

            public Task<CommandStatusQueryResponse?> GetCommandStatusAsync(string messageId, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("Must use workload status route.");

            public Task<StreamReadPage> ReadStreamAsync(StreamReadRequest request, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("Must use workload stream route.");

            public Task<EventStoreQueryResult> SubmitQueryAsync(
                SubmitQueryRequest request,
                string? ifNoneMatch = null,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException("Queries are not used in this test.");

            public Task<EventStoreQueryResult<T>> SubmitQueryAsync<T>(
                SubmitQueryRequest request,
                string? ifNoneMatch = null,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException("Queries are not used in this test.");

            public async Task<SubmitCommandResponse> SubmitWorkloadCommandAsync(
                SubmitCommandRequest request,
                CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref host._submissions);
                string assertion = (await host.Services.GetRequiredService<IEventStoreGatewayWorkloadAssertionSource>()
                    .IssueAsync(EventStoreWorkloadOperations.GatewayCommandSubmit, request.Tenant, cancellationToken)).ShouldNotBeNull();
                var signed = new JsonWebToken(assertion);
                signed.GetClaim(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType).Value.ShouldBe(request.Tenant);
                signed.GetClaim(EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType).Value.ShouldBe(request.Domain);
                string actor = signed.GetClaim(EventStoreWorkloadAuthenticationDefaults.ActorBindingClaimType).Value;
                var envelope = new CommandEnvelope(
                    request.MessageId,
                    request.Tenant,
                    request.Domain,
                    request.AggregateId,
                    request.CommandType,
                    Encoding.UTF8.GetBytes(request.Payload.GetRawText()),
                    request.CorrelationId ?? request.MessageId,
                    request.MessageId,
                    actor,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "timesheets",
                        [EventStoreGatewayVerifiedOrigin.ActorExtensionKey] = actor
                    });

                if (host.EmitMissingStatusPlaceholder
                    && await host._statuses.ReadStatusAsync(request.Tenant, request.MessageId, cancellationToken) is null)
                {
                    // Mirror the gateway's advisory Received row before dispatching to the owner.
                    await host._statuses.WriteStatusAsync(request.Tenant, request.MessageId, new CommandStatusRecord(
                        CommandStatus.Received, DateTimeOffset.UtcNow, request.AggregateId,
                        EventCount: null, RejectionEventType: null, FailureReason: null, TimeoutDuration: null,
                        MessageId: request.MessageId, CorrelationId: request.CorrelationId), cancellationToken);
                }

                CommandProcessingResult result;
                await host._actorTurn.WaitAsync(cancellationToken);
                try
                {
                    result = await host.CreateActor(request.Tenant, request.AggregateId)
                        .ProcessCommandAsync(envelope, cancellationToken);
                }
                finally
                {
                    host._actorTurn.Release();
                }

                if (!result.Accepted && request.CommandType == typeof(CommitTimeEntryRecord).FullName)
                {
                    throw new EventStoreGatewayException(
                        result.RejectionEventType is null ? 500 : 422, "Recording command was refused.");
                }

                if (host.LoseNextSubmitAck)
                {
                    host.LoseNextSubmitAck = false;
                    throw new HttpRequestException("Simulated lost submit acknowledgement after actor commit.");
                }

                return new SubmitCommandResponse(request.CorrelationId ?? request.MessageId, MessageId: request.MessageId);
            }

            public async Task<CommandStatusQueryResponse?> GetWorkloadCommandStatusAsync(
                string tenant,
                string messageId,
                CancellationToken cancellationToken = default)
            {
                CommandStatusRecord? status = await host._statuses.ReadStatusAsync(tenant, messageId, cancellationToken);
                // Workload status is visible only when the stored row matches the bound domain and message.
                return status is null || status.Domain != "timesheets" || status.MessageId != messageId
                    ? null
                    : new CommandStatusQueryResponse(
                        status.CorrelationId!,
                        status.Status.ToString(),
                        (int)status.Status,
                        status.RejectionEventType,
                        status.MessageId)
                    {
                        TenantId = tenant,
                        Domain = status.Domain,
                        AggregateId = status.AggregateId,
                        EventCount = status.EventCount,
                        CommittedEventSequence = status.CommittedEventSequence,
                        FailureReason = status.FailureReason,
                        Retryable = status.Retryable
                    };
            }

            public async Task<StreamReadPage> ReadWorkloadStreamAsync(
                StreamReadRequest request,
                CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref host._streamReads);
                request.PageSize.ShouldBeInRange(1, 1_000);
                if (host.BeforeStreamReadAsync is { } beforeRead)
                {
                    await beforeRead(cancellationToken);
                }

                if (host.FailReadbackAfterCommit && request.FromSequence > 0)
                {
                    throw new EventStoreGatewayException(503, "Simulated transient readback failure after commit.");
                }

                if (!host._storage.ContainsKey((request.Tenant, request.AggregateId!)))
                {
                    throw new EventStoreGatewayException(404, "Not Found", reasonCode: StreamReplayReasonCodes.MissingStream);
                }

                EventEnvelope[] all = await host.CreateActor(request.Tenant, request.AggregateId!).GetEventsAsync(0);
                if (all.Length == 0)
                {
                    throw new EventStoreGatewayException(404, "Not Found", reasonCode: StreamReplayReasonCodes.MissingStream);
                }

                EventEnvelope[] filtered = all
                    .Where(item => item.SequenceNumber > request.FromSequence
                        && (request.ToSequence is null || item.SequenceNumber <= request.ToSequence))
                    .Take(request.PageSize)
                    .ToArray();
                long? lastReturned = filtered.Length > 0 ? filtered[^1].SequenceNumber : null;
                long latestSequence = all[^1].SequenceNumber;
                bool isTruncated = lastReturned is { } last && last < (request.ToSequence ?? latestSequence)
                    && last < latestSequence;
                return new StreamReadPage(
                    request.Tenant,
                    request.Domain,
                    request.AggregateId!,
                    filtered.Select(item => new StreamReadEvent(
                        item.SequenceNumber,
                        item.EventTypeName,
                        item.Payload,
                        item.SerializationFormat,
                        item.MetadataVersion,
                        item.MessageId,
                        item.CorrelationId,
                        item.CausationId,
                        item.Timestamp,
                        item.UserId,
                        EventStorePayloadProtectionMetadataCarrier.Read(item.Extensions))
                    {
                        GlobalPosition = item.GlobalPosition,
                        PayloadVersion = item.PayloadVersion
                    }).ToArray(),
                    new StreamReadMetadata(
                        request.FromSequence,
                        request.ToSequence,
                        lastReturned,
                        latestSequence,
                        filtered.Length,
                        isTruncated,
                        null));
            }
        }

        private sealed class ConfigurableAccessGuard(ComposedConfirmationHost host) : ITimesheetsAccessGuard
        {
            public ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
                TimesheetsAuthorizationRequest request,
                CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref host._guardChecks);
                return ValueTask.FromResult(host.AllowAuthorization
                    ? TimesheetsAuthorizationDecision.Allowed()
                    : TimesheetsAuthorizationDecision.Denied(TimesheetsDenialCategory.NonMember, "Denied"));
            }

            public async ValueTask<TimesheetsAuthorizationDecision> ExecuteIfAuthorizedAsync(
                TimesheetsAuthorizationRequest request,
                Func<CancellationToken, ValueTask> trustedWork,
                CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref host._guardChecks);
                if (!host.AllowAuthorization)
                {
                    return TimesheetsAuthorizationDecision.Denied(TimesheetsDenialCategory.NonMember, "Denied");
                }

                await trustedWork(cancellationToken).ConfigureAwait(false);
                return TimesheetsAuthorizationDecision.Allowed();
            }

            public ValueTask<TimesheetsUiActionPolicyOutcome> EvaluateUiActionAsync(
                TimesheetsAuthorizationRequest request,
                TimesheetsUiActionVisibility deniedVisibility,
                CancellationToken cancellationToken)
                => ValueTask.FromResult(TimesheetsUiActionPolicyOutcome.Allowed(request.UiAction ?? TimesheetsUiAction.Capture));
        }

        private sealed class DynamicClaimsTransformation(
            IHttpContextAccessor accessor,
            Func<Claim[]> getClaims) : IClaimsTransformation
        {
            public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
            {
                if (accessor.HttpContext?.Request.Path.StartsWithSegments(
                    "/api/timesheets/external-contributions", StringComparison.OrdinalIgnoreCase) != true)
                {
                    return Task.FromResult(principal);
                }

                Claim[] claims = getClaims();
                return Task.FromResult(claims.Length == 0 ? principal : new ClaimsPrincipal(
                    principal.Identities.Append(new ClaimsIdentity(claims))));
            }
        }
    }

    internal sealed class TestLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<(string Category, string Message)> _entries = new();

        public ILogger CreateLogger(string categoryName) => new Sink(categoryName, _entries);

        public string[] MessagesFor(string categoryName)
            => _entries
                .Where(entry => string.Equals(entry.Category, categoryName, StringComparison.Ordinal))
                .Select(entry => entry.Message)
                .ToArray();

        public void Dispose()
        {
        }

        private sealed class Sink(string category, ConcurrentQueue<(string Category, string Message)> entries) : ILogger
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
                => entries.Enqueue((category, formatter(state, exception)));
        }
    }
}
