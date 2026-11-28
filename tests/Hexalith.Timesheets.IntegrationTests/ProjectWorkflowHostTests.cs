using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Timesheets.Contracts.Commands.TimeEntries;
using Hexalith.Timesheets.Contracts.Events.ActivityTypes;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections.ActivityTypes;
using Hexalith.Timesheets.Projections.ApprovedTimeLedger;
using Hexalith.Timesheets.ReadModels.ApprovedTimeLedger;
using Hexalith.Timesheets.ReadModels.TimeEntries;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.Policies;
using Hexalith.Timesheets.Server.TimeEntries.Commands;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.Runtime;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using NSubstitute;
using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

/// <summary>Exercises the release composition, mapped SDK endpoints and owner event store together.</summary>
public sealed class ProjectWorkflowHostTests
{
    private const string Tenant = "workflow-tenant";
    private const string Entry = "workflow-entry";
    private const string Project = "workflow-project";
    private const string ExistingApprovedEntry = "existing-approved-entry";
    private const string Activity = "workflow-activity";
    private const string SecretHeader = "workflow-private-transport";
    private static readonly byte[] s_signingKey = Encoding.UTF8.GetBytes(new string('w', 32));
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>Verifies durable owner order and current and superseded lineage through both ledger surfaces.</summary>
    [Fact]
    public async Task HumanCommandsCommitOneOwnerHistoryAndSdkDeliverySuppliesLedgerAsync()
    {
        using var root = new WebApplicationFactory<Program>();
        var catalog = new HttpRouteCatalogFixture();
        var store = new InMemoryReadModelStore();
        var gateway = Substitute.For<IEventStoreGatewayClient>();
        using WebApplicationFactory<Program> host = CreateHost(root, catalog, store, gateway, unavailable: null);
        using HttpClient client = host.CreateClient();
        bool observeTimeout = true;
        AggregateActor actor = ConnectOwner(host, client, catalog, gateway, () => observeTimeout);
        await SeedCatalogAsync(host);
        var coordinator = host.Services.GetRequiredService<SharedProjectionEpochCoordinator>();
        _ = await coordinator.RegisterWriterAsync(ApprovedTimeLedgerEpoch.CreateScope(Tenant),
            ApprovedTimeLedgerEpoch.OrdinaryWriterId, Cancellation);
        host.Services.GetRequiredService<TimesheetsEvidencePolicyOptions>().AllowsProjectionPublication().ShouldBeFalse();
        RecordTimeEntry capture = Capture();
        await AssertSdkCommandRefusedAsync(host, client, catalog, capture, "different-tenant", "contributor", "contributor");
        await AssertSdkCommandRefusedAsync(host, client, catalog, capture, Tenant, "contributor", "approver");
        (await actor.GetEventsAsync(0)).ShouldBeEmpty();

        const string capturePath = "/api/timesheets/time-entries";
        using HttpResponseMessage uncertain = await PostHumanAsync(client, capturePath, capture, "contributor",
            Tenant, $"workflow-{capturePath}");
        uncertain.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        uncertain.Headers.RetryAfter?.Delta.ShouldBe(TimeSpan.FromSeconds(1));
        (await uncertain.Content.ReadAsStringAsync(Cancellation)).ShouldBeEmpty();
        (await actor.GetEventsAsync(0)).Length.ShouldBe(1);
        observeTimeout = false;
        await SubmitAsync(client, capturePath, capture, "contributor");
        (await actor.GetEventsAsync(0)).Length.ShouldBe(1);
        await gateway.Received(1).SubmitWorkloadCommandAsync(
            Arg.Is<SubmitCommandRequest>(request => request.CommandType == typeof(CommitProjectTimeEntryRecord).FullName),
            Arg.Any<CancellationToken>());
        await SubmitAsync(client, $"/api/timesheets/time-entries/{Entry}/submit",
            new SubmitTimeEntriesForApproval(new("submission"), [new(Entry)], TimeEntrySubmissionScope.SelectedEntries), "contributor");
        var approval = new ApproveTimeEntry(new(Entry), new("decision"));
        string approvalPath = $"/api/timesheets/time-entries/{Entry}/approve";
        using HttpResponseMessage nonApprover = await PostHumanAsync(client, approvalPath, approval, "contributor",
            Tenant, "unapproved-actor");
        nonApprover.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await nonApprover.Content.ReadAsStringAsync(Cancellation)).ShouldBeEmpty();
        (await actor.GetEventsAsync(0)).Length.ShouldBe(2);
        await SubmitAsync(client, approvalPath, approval, "approver");
        await SubmitAsync(client, $"/api/timesheets/time-entries/{Entry}/corrections",
            new CorrectApprovedTimeEntry(new(Entry), new("correction"), capture.Target, capture.Contributor, capture.ActivityTypeId,
                capture.ServiceDate, 75, capture.BillableState, capture.ContributorCategory, null, new("Duration correction")), "approver");

        var stored = await actor.GetEventsAsync(0);
        stored.Select(item => item.SequenceNumber).ShouldBe([1L, 2L, 3L, 4L]);
        stored.Select(item => item.EventTypeName).ShouldBe([
            typeof(StoredTimeEntryRecorded).FullName!, typeof(StoredTimeEntrySubmitted).FullName!,
            typeof(StoredTimeEntryApproved).FullName!, typeof(StoredTimeEntryApprovedCorrected).FullName!]);
        stored.ShouldAllBe(item => !Encoding.UTF8.GetString(item.Payload).Contains("Sibling ", StringComparison.Ordinal));

        ProjectionEventDto[] delivery = stored.Select(item => new ProjectionEventDto(item.EventTypeName, item.Payload, item.SerializationFormat,
            item.SequenceNumber, item.Timestamp, item.CorrelationId, item.MessageId, item.UserId, item.SequenceNumber + 10)).ToArray();
        using IServiceScope scope = host.Services.CreateScope();
        DomainProjectionIdentityOptions identity = scope.ServiceProvider.GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(identity.AppId, identity.ServiceVersion,
            scope.ServiceProvider.GetServices<IAsyncDomainProjectionHandler>()
                .Select(handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType)));
        var project = new ProjectionDispatchRequest(new(Tenant, "timesheets", Entry, delivery),
            [ApprovedTimeLedgerEpoch.ProjectionName], "workflow-ledger", fingerprint);
        using HttpResponseMessage projectionResponse = await PostWorkloadAsync(host, client, catalog, "/project/v2",
            EventStoreWorkloadOperations.DomainServiceProject, project);
        projectionResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await projectionResponse.Content.ReadFromJsonAsync<ProjectionDispatchResponse>(Cancellation)).ShouldNotBeNull()
            .Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed);

        var query = new QueryApprovedTimeLedger { IncludeSupersededRows = true };
        using HttpResponseMessage human = await PostHumanAsync(client, "/api/timesheets/approved-ledger", query, "approver");
        human.StatusCode.ShouldBe(HttpStatusCode.OK);
        ApprovedTimeLedgerReadModel page = (await human.Content.ReadFromJsonAsync<ApprovedTimeLedgerReadModel>(Cancellation)).ShouldNotBeNull();
        AssertLedger(page);

        QueryEnvelope envelope = new(Tenant, "timesheets", Project, nameof(QueryApprovedTimeLedger),
            JsonSerializer.SerializeToUtf8Bytes(query, s_json), "workflow-query", "approver", null, false,
            null, "approver", "timesheets-test-human", false, null, null)
        {
            CallerAuthority = new QueryCallerAuthority("authenticated-caller", [], [])
        };
        QueryEnvelope pagedEnvelope = envelope with { Paging = new(PageSize: 1) };
        ApprovedTimeLedgerReadModel first = ReadSuccessfulPage(await QuerySdkAsync(host, client, catalog, pagedEnvelope));
        first.Items.Count.ShouldBe(1);
        first.NextCursor.ShouldNotBeNullOrWhiteSpace();
        first.NextCursor.ShouldNotBe("1");
        QueryEnvelope continuation = pagedEnvelope with { Paging = new(PageSize: 1, Cursor: first.NextCursor) };
        ApprovedTimeLedgerReadModel second = ReadSuccessfulPage(await QuerySdkAsync(host, client, catalog, continuation));
        second.Items.Count.ShouldBe(1);
        second.NextCursor.ShouldBeNull();
        AssertLedger(first with { Items = [.. first.Items, .. second.Items] });

        foreach (QueryEnvelope denied in new[]
        {
            envelope with { AggregateId = "different-project" },
            envelope with { EntityId = Entry },
            envelope with { Payload = JsonSerializer.SerializeToUtf8Bytes(query with { Project = new("different-project") }, s_json) },
            envelope with { TenantId = "different-tenant" },
            continuation with { UserId = "contributor", OriginalActorId = "contributor" }
        })
        {
            QueryResult refusal = await QuerySdkAsync(host, client, catalog, denied);
            refusal.Success.ShouldBeFalse();
            refusal.PayloadBytes.ShouldBeNull();
        }

        await RebuildLedgerAsync(host, Entry, delivery);
        AssertLedger(ReadSuccessfulPage(await QuerySdkAsync(host, client, catalog, envelope)));
        QueryResult supersededCursor = await QuerySdkAsync(host, client, catalog, continuation);
        supersededCursor.Success.ShouldBeFalse();
        supersededCursor.PayloadBytes.ShouldBeNull();
        (await actor.GetEventsAsync(0)).Length.ShouldBe(4);

        using HttpResponseMessage foreign = await PostHumanAsync(client, "/api/timesheets/approved-ledger", query, "approver", "different-tenant");
        foreign.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await foreign.Content.ReadAsStringAsync(Cancellation)).ShouldBeEmpty();
    }

    /// <summary>Verifies that a missing workflow authority leaves the owner stream empty and withholds reads.</summary>
    [Theory]
    [InlineData("tenants")]
    [InlineData("tenant-lifecycle-unknown")]
    [InlineData("tenant-membership-disabled")]
    [InlineData("tenant-membership-unknown-role")]
    [InlineData("tenant-membership-wrong-scope")]
    [InlineData("tenant-membership-incomplete")]
    [InlineData("tenant-membership-paging-missing")]
    [InlineData("party")]
    [InlineData("projects")]
    [InlineData("project-lifecycle-missing")]
    [InlineData("project-admission-missing")]
    [InlineData("catalog")]
    [InlineData("approval")]
    [InlineData("policy")]
    [InlineData("ledger")]
    [InlineData("ledger-unenrolled")]
    [InlineData("ledger-offboarded")]
    [InlineData("ledger-catching-up")]
    [InlineData("ledger-degraded")]
    [InlineData("opting-out")]
    [InlineData("cross-tenant")]
    public async Task RefusedWorkflowDoesNotCommitOrDiscloseAsync(string unavailable)
    {
        using var root = new WebApplicationFactory<Program>();
        var catalog = new HttpRouteCatalogFixture();
        var store = new InMemoryReadModelStore();
        var gateway = Substitute.For<IEventStoreGatewayClient>();
        using WebApplicationFactory<Program> host = CreateHost(root, catalog, store, gateway, unavailable);
        using HttpClient client = host.CreateClient();
        AggregateActor actor = ConnectOwner(host, client, catalog, gateway);
        if (unavailable != "catalog")
        {
            await SeedCatalogAsync(host);
        }

        if (unavailable != "ledger-unenrolled")
        {
            await SeedLedgerAsync(host, requireOwnerHandoff: unavailable == "ledger-catching-up");
        }

        var coordinator = host.Services.GetRequiredService<SharedProjectionEpochCoordinator>();
        SharedProjectionScope ledgerScope = ApprovedTimeLedgerEpoch.CreateScope(Tenant);
        if (unavailable == "ledger-offboarded")
        {
            await coordinator.OffboardTenantAsync(ledgerScope, "workflow-offboarding-fixture", Cancellation);
        }
        else if (unavailable == "ledger-degraded")
        {
            using IServiceScope scope = host.Services.CreateScope();
            var handler = scope.ServiceProvider.GetServices<IAsyncDomainProjectionHandler>()
                .OfType<ApprovedTimeLedgerProjectionHandler>().Single();
            SharedProjectionLease lease = await coordinator.RegisterWriterAsync(
                ledgerScope, handler.OrdinaryWriterId, Cancellation);
            var invalid = new SharedProjectionDelivery("unreadable-ledger-owner", 100, "{}"u8.ToArray(), [100], OwnerSequenceNumber: 1);
            (await coordinator.JournalAsync(ledgerScope, lease, invalid, Cancellation)).ShouldBe(SharedProjectionJournalResult.Journaled);
            await Should.ThrowAsync<SharedProjectionRetryableFoldException>(() => coordinator.CatchUpAsync(ledgerScope,
                (delivery, generation, token) => handler.FoldCatchUpAsync(delivery, generation, coordinator, token), Cancellation));
            _ = await coordinator.CatchUpAsync(ledgerScope,
                (delivery, generation, token) => handler.FoldCatchUpAsync(delivery, generation, coordinator, token), Cancellation);
            var degraded = await coordinator.ReadAsync<JsonObject>(ledgerScope, ApprovedTimeLedgerEpoch.SlotName, Cancellation);
            degraded.Value.ShouldNotBeNull();
            degraded.IsStale.ShouldBeFalse();
            degraded.IsDegraded.ShouldBeTrue();
        }

        string tenant = unavailable == "cross-tenant" ? "different-tenant" : Tenant;
        using HttpResponseMessage submission = await PostHumanAsync(client, "/api/timesheets/time-entries",
            Capture(), "contributor", tenant, "refused-command");
        submission.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await submission.Content.ReadAsStringAsync(Cancellation)).ShouldBeEmpty();
        (await actor.GetEventsAsync(0)).ShouldBeEmpty();

        using HttpResponseMessage read = await PostHumanAsync(client, "/api/timesheets/approved-ledger",
            new QueryApprovedTimeLedger { IncludeSupersededRows = true }, "contributor", tenant);
        read.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await read.Content.ReadAsStringAsync(Cancellation)).ShouldBeEmpty();
    }

    private static async Task AssertSdkCommandRefusedAsync(
        WebApplicationFactory<Program> host, HttpClient client, HttpRouteCatalogFixture catalog, RecordTimeEntry capture,
        string payloadTenant, string payloadActor, string verifiedActor)
    {
        var intent = new CommitProjectTimeEntryRecord(capture, new(payloadTenant), new(payloadActor));
        var command = new CommandEnvelope("mismatched-admission", Tenant, "timesheets", Entry, intent.GetType().FullName!,
            JsonSerializer.SerializeToUtf8Bytes(intent, s_json), "workflow-command-mismatch", null, verifiedActor, new()
            {
                [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "timesheets",
                [EventStoreGatewayVerifiedOrigin.ActorExtensionKey] = verifiedActor
            });
        using HttpResponseMessage response = await PostWorkloadAsync(host, client, catalog, "/process",
            EventStoreWorkloadOperations.DomainServiceProcess, new DomainServiceRequest(command, null));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<DomainServiceWireResult>(s_json, Cancellation)).ShouldNotBeNull()
            .Events.ShouldBeEmpty();
    }

    private static async Task<QueryResult> QuerySdkAsync(
        WebApplicationFactory<Program> host, HttpClient client, HttpRouteCatalogFixture catalog, QueryEnvelope envelope)
    {
        using HttpResponseMessage response = await PostWorkloadAsync(host, client, catalog, "/query",
            EventStoreWorkloadOperations.DomainServiceQuery, envelope, QueryCallerAuthorityBinding.Bind(envelope));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<QueryResult>(Cancellation)).ShouldNotBeNull();
    }

    private static ApprovedTimeLedgerReadModel ReadSuccessfulPage(QueryResult result)
    {
        result.Success.ShouldBeTrue(result.ErrorMessage);
        return result.GetPayload().Deserialize<ApprovedTimeLedgerReadModel>(s_json).ShouldNotBeNull();
    }

    private static void AssertLedger(ApprovedTimeLedgerReadModel page)
    {
        page.Items.Count.ShouldBe(2);
        ApprovedTimeLedgerRowReadModel current = page.Items.Single(row => row.RowState == ApprovedTimeLedgerRowState.Current);
        ApprovedTimeLedgerRowReadModel original = page.Items.Single(row => row.RowState == ApprovedTimeLedgerRowState.Superseded);
        current.DurationMinutes.ShouldBe(75);
        original.DurationMinutes.ShouldBe(60);
        foreach (ApprovedTimeLedgerRowReadModel row in page.Items)
        {
            row.ApprovalDecision.TimeEntryApprovalDecisionId.Value.ShouldBe("decision");
            row.ApprovalDecision.Tenant.TenantId.ShouldBe(Tenant);
            row.ApprovedCorrection.ShouldNotBeNull().TimeEntryCorrectionId.Value.ShouldBe("correction");
            row.ApprovedCorrection.SourceApprovalDecisionId.Value.ShouldBe("decision");
            row.EventLineage.Select(item => item.Ordinal).ShouldBe([1L, 2L, 3L, 4L]);
        }
    }

    private static RecordTimeEntry Capture() => new(new(Entry), TimeEntryTargetReference.ForProject(new(Project)),
        new("contributor"), new(Activity), new(2026, 11, 28), 60, BillableState.Billable, ContributorCategory.Employee, null);

    private static async Task SubmitAsync(HttpClient client, string path, object command, string actor)
    {
        using HttpResponseMessage response = await PostHumanAsync(client, path, command, actor, Tenant, $"workflow-{path}");
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Cancellation));
    }

    private static async Task<HttpResponseMessage> PostHumanAsync(HttpClient client, string path, object payload, string actor,
        string tenant = Tenant, string? idempotency = null)
    {
        string token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "workflow-test", Audience = "workflow-human",
            Claims = new Dictionary<string, object> { ["sub"] = actor, ["tenant_id"] = tenant },
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(s_signingKey), SecurityAlgorithms.HmacSha256),
            Expires = DateTime.UtcNow.AddMinutes(10)
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(payload, options: s_json) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (idempotency is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotency);
        }

        return await client.SendAsync(request, Cancellation);
    }

    private static async Task<HttpResponseMessage> PostWorkloadAsync<T>(
        WebApplicationFactory<Program> host, HttpClient client, HttpRouteCatalogFixture catalog, string path, string operation, T payload,
        IReadOnlyDictionary<string, string>? bindings = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(payload, options: s_json) };
        var issuer = host.Services.GetRequiredService<IWorkloadAssertionIssuer>();
        string assertion = (await issuer.IssueAsync(new WorkloadAssertionRequest("timesheets", operation, bindings), Cancellation)).ShouldNotBeNull();
        request.Headers.Add(DaprAppChannelToken.HeaderName, SecretHeader);
        request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);
        await catalog.AddHeadersAsync(host.Services, request, Cancellation);
        return await client.SendAsync(request, Cancellation);
    }

    private static WebApplicationFactory<Program> CreateHost(WebApplicationFactory<Program> root,
        HttpRouteCatalogFixture catalog, InMemoryReadModelStore store, IEventStoreGatewayClient gateway, string? unavailable)
        => root.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(DaprAppChannelToken.ConfigurationKey, SecretHeader);
            builder.UseSetting("Timesheets:InternalSurface:Port", "0");
            builder.UseSetting("EventStore:DomainService:AppId", "timesheets");
            builder.UseSetting("Authentication:JwtBearer:Issuer", "workflow-test");
            builder.UseSetting("Authentication:JwtBearer:Audience", "workflow-human");
            builder.UseSetting("Authentication:JwtBearer:SigningKey", Encoding.UTF8.GetString(s_signingKey));
            builder.UseSetting("Authentication:JwtBearer:AllowedAlgorithms:0", SecurityAlgorithms.HmacSha256);
            builder.UseSetting("Authentication:JwtBearer:RequireHttpsMetadata", "false");
            builder.UseSetting("Authentication:WorkloadIssuer:Workload", "eventstore");
            builder.UseSetting("Timesheets:ProjectWorkflow:Enabled", (unavailable != "opting-out").ToString());
            builder.UseSetting("Timesheets:ProjectWorkflow:TenantId", Tenant);
            builder.UseSetting("Timesheets:ProjectWorkflow:ProjectId", Project);
            builder.UseSetting("Timesheets:ProjectWorkflow:AuthorityGatewayEndpoint", "https://workflow-gateway.example");
            if (unavailable != "approval")
            {
                builder.UseSetting("Timesheets:ProjectWorkflow:ApproverPartyIds:0", "approver");
            }
            builder.UseSetting("Timesheets:ProjectWorkflow:EvidencePolicy:ProjectionCommentsAllowed", "true");
            builder.UseSetting("Timesheets:ProjectWorkflow:EvidencePolicy:LegalHoldPolicyConfigured", "true");
            builder.UseSetting("Timesheets:ProjectWorkflow:EvidencePolicy:TenantRetentionOverridePolicyConfigured", "true");
            builder.UseSetting("Timesheets:ProjectWorkflow:EvidencePolicy:CommentSensitivityPolicyConfigured", (unavailable != "policy").ToString());
            builder.UseSetting("EventStore:Authentication:Authority", "https://workflow-authority.example/root");
            builder.UseSetting("EventStore:Authentication:TokenEndpoint", "https://workflow-authority.example/root/token");
            builder.UseSetting("EventStore:Authentication:ClientId", "workflow-authority");
            builder.UseSetting("EventStore:Authentication:ClientSecret", "isolated-test-credential");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                catalog.Configure(services);
                services.RemoveAll<IReadModelStore>();
                services.RemoveAll<IReadModelBatchStore>();
                services.RemoveAll<IEventStoreGatewayClient>();
                services.AddSingleton<IReadModelStore>(store);
                services.AddSingleton<IReadModelBatchStore>(store);
                services.AddSingleton(gateway);
                if (unavailable == "ledger")
                {
                    services.RemoveAll<IApprovedTimeLedgerProjectionReader>();
                    services.AddSingleton<IApprovedTimeLedgerProjectionReader, UnavailableApprovedTimeLedgerProjectionReader>();
                }

                services.AddHttpClient("EventStoreGatewayAuthorityToken")
                    .ConfigurePrimaryHttpMessageHandler(() => new ProjectWorkflowSiblingTransport(unavailable));
                services.AddHttpClient("TimesheetsProjectWorkflowAuthority")
                    .ConfigurePrimaryHttpMessageHandler(() => new ProjectWorkflowSiblingTransport(unavailable));
            });
        });

    private static async Task SeedCatalogAsync(WebApplicationFactory<Program> host)
    {
        using IServiceScope scope = host.Services.CreateScope();
        var coordinator = scope.ServiceProvider.GetRequiredService<SharedProjectionEpochCoordinator>();
        var handler = scope.ServiceProvider.GetServices<IAsyncDomainProjectionHandler>()
            .OfType<TenantActivityTypeCatalogProjectionHandler>().Single();
        var identity = new DomainSharedProjectionRebuildIdentity(Tenant, "timesheets", handler.ProjectionType,
            "workflow-activity-rebuild", "isolated-catalog");
        var activity = new ActivityTypeCreated(new(Activity), ActivityTypeScope.Tenant, null, "Delivery", BillableState.Billable);
        ProjectionEventDto[] events = [new(typeof(ActivityTypeCreated).FullName!, JsonSerializer.SerializeToUtf8Bytes(activity, s_json),
            "json", 1, DateTimeOffset.UtcNow, "activity-seed", "activity-created", "operator", 1)];
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(identity, Cancellation);
        candidate = await handler.AccumulateAsync(identity, candidate, new(Tenant, "timesheets", Activity, events), Cancellation);
        DomainProjectionRebuildPlan plan = await handler.FinalizeAsync(identity, candidate, Cancellation);
        SharedProjectionScope projectionScope = handler.CreateScope(Tenant);
        _ = await coordinator.RegisterWriterAsync(projectionScope, handler.OrdinaryWriterId, Cancellation);
        _ = await coordinator.BeginAsync(projectionScope, identity.OperationId, "workflow-activity-inventory",
            new Dictionary<string, long> { [Activity] = 1 }, Cancellation);
        _ = await coordinator.StageAsync(projectionScope, identity.OperationId, plan.Operations, Cancellation);
        await coordinator.CommitAsync(projectionScope, identity.OperationId, Cancellation);
        _ = await coordinator.CatchUpAsync(projectionScope,
            (delivery, generation, token) => handler.FoldCatchUpAsync(delivery, generation, coordinator, token), Cancellation);
    }

    private static async Task SeedLedgerAsync(WebApplicationFactory<Program> host, bool requireOwnerHandoff)
    {
        var entry = new TimeEntryId(ExistingApprovedEntry);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        IEventPayload[] ownerEvents =
        [
            new StoredTimeEntryRecorded(new(entry, TimeEntryTargetReference.ForProject(new(Project)), new("contributor"),
                new(Activity), ActivityTypeScope.Tenant, new(2026, 11, 28), 60, BillableState.Billable,
                TimeEntryApprovalState.Draft, ContributorCategory.Employee, null)),
            new StoredTimeEntrySubmitted(new(entry, new("contributor"), new(Tenant), now, new("existing-submission"),
                TimeEntrySubmissionScope.SelectedEntries, TimeEntryApprovalState.Submitted)),
            new StoredTimeEntryApproved(new(entry, new("approver"), new(Tenant), now, new("existing-decision"),
                TimeEntryApprovalState.Approved, new(ApprovalAuthorityAction.EntryApproval, ApprovalAuthoritySource.ProjectApprover,
                    ApprovalAuthorityDecisionState.Allowed, "fixture-policy", "v1", ProjectionFreshnessMetadata.Fresh),
                TimeEntryApprovalScope.IndividualEntry))
        ];
        ProjectionEventDto[] events = ownerEvents.Select((item, index) => new ProjectionEventDto(item.GetType().FullName!,
            JsonSerializer.SerializeToUtf8Bytes(item, item.GetType(), s_json), "json", index + 1, now,
            "existing-ledger-correlation", $"existing-ledger-event-{index}", "approver", index + 10)).ToArray();
        await RebuildLedgerAsync(host, ExistingApprovedEntry, events, requireOwnerHandoff);
    }

    private static async Task RebuildLedgerAsync(
        WebApplicationFactory<Program> host, string owner, ProjectionEventDto[] events, bool requireOwnerHandoff = false)
    {
        using IServiceScope scope = host.Services.CreateScope();
        var coordinator = scope.ServiceProvider.GetRequiredService<SharedProjectionEpochCoordinator>();
        var handler = scope.ServiceProvider.GetServices<IAsyncDomainProjectionHandler>()
            .OfType<ApprovedTimeLedgerProjectionHandler>().Single();
        var identity = new DomainSharedProjectionRebuildIdentity(Tenant, "timesheets", handler.ProjectionType,
            "workflow-ledger-rebuild", "isolated-catalog");
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(identity, Cancellation);
        candidate = await handler.AccumulateAsync(identity, candidate, new(Tenant, "timesheets", owner, events), Cancellation);
        DomainProjectionRebuildPlan plan = await handler.FinalizeAsync(identity, candidate, Cancellation);
        SharedProjectionScope projectionScope = handler.CreateScope(Tenant);
        _ = await coordinator.RegisterWriterAsync(projectionScope, handler.OrdinaryWriterId, Cancellation);
        _ = await coordinator.BeginAsync(projectionScope, identity.OperationId, "workflow-ledger-inventory",
            new Dictionary<string, long> { [owner] = events[^1].GlobalPosition }, Cancellation);
        _ = await coordinator.StageAsync(projectionScope, identity.OperationId, plan.Operations, Cancellation);
        await coordinator.CommitAsync(projectionScope, identity.OperationId, Cancellation, requireOwnerSequenceHandoff: requireOwnerHandoff);
        if (!requireOwnerHandoff)
        {
            _ = await coordinator.CatchUpAsync(projectionScope,
                (delivery, generation, token) => handler.FoldCatchUpAsync(delivery, generation, coordinator, token), Cancellation);
        }
    }

    private static AggregateActor ConnectOwner(
        WebApplicationFactory<Program> host, HttpClient client, HttpRouteCatalogFixture catalog, IEventStoreGatewayClient gateway,
        Func<bool>? observeTimeout = null)
    {
        var statuses = new InMemoryCommandStatusStore();
        var invoker = Substitute.For<IDomainServiceInvoker>();
        invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                CommandEnvelope command = call.ArgAt<CommandEnvelope>(0);
                using HttpResponseMessage response = await PostWorkloadAsync(host, client, catalog, "/process",
                    EventStoreWorkloadOperations.DomainServiceProcess, new DomainServiceRequest(command, call.ArgAt<object?>(1)));
                response.EnsureSuccessStatusCode();
                DomainServiceWireResult wire = (await response.Content.ReadFromJsonAsync<DomainServiceWireResult>(s_json, Cancellation)).ShouldNotBeNull();
                return new DomainResult([.. wire.Events.Select(item => new SerializedDomainEventPayload(item.EventTypeName,
                    item.Payload, item.SerializationFormat, item.MetadataVersion, item.EventContractType, item.PayloadVersion))]);
            });

        AggregateActor actor = MagicLinkConfirmationHttpBoundaryTests.CreateMagicLinkActor(new TimeEntryId(Entry),
            new InMemoryStateManager(), statuses, invoker, Tenant);
        gateway.SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                SubmitCommandRequest request = call.ArgAt<SubmitCommandRequest>(0);
                string person = host.Services.GetRequiredService<ITimesheetsTrustedContextAccessor>().CurrentActor.ShouldNotBeNull().PartyId;
                var envelope = new CommandEnvelope(request.MessageId, request.Tenant, request.Domain, request.AggregateId,
                    request.CommandType, Encoding.UTF8.GetBytes(request.Payload.GetRawText()), request.MessageId, request.MessageId,
                    person, new()
                    {
                        [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "timesheets",
                        [EventStoreGatewayVerifiedOrigin.ActorExtensionKey] = person
                    });
                _ = await actor.ProcessCommandAsync(envelope, call.ArgAt<CancellationToken>(1));
                return new SubmitCommandResponse(request.MessageId, MessageId: request.MessageId);
            });
        gateway.GetWorkloadCommandStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                string tenant = call.ArgAt<string>(0);
                string message = call.ArgAt<string>(1);
                var status = await statuses.ReadStatusAsync(tenant, message, call.ArgAt<CancellationToken>(2));
                CommandStatus observed = observeTimeout?.Invoke() == true ? CommandStatus.TimedOut : status?.Status ?? default;
                return status is null ? null : new CommandStatusQueryResponse(status.CorrelationId!,
                    observed.ToString(), (int)observed, status.RejectionEventType, status.MessageId)
                {
                    TenantId = tenant, Domain = status.Domain, AggregateId = status.AggregateId, EventCount = status.EventCount,
                    CommittedEventSequence = status.CommittedEventSequence, FailureReason = status.FailureReason, Retryable = status.Retryable
                };
            });
        return actor;
    }
}
