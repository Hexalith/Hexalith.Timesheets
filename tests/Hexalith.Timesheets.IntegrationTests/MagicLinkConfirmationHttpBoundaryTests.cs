using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService;
using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.ActivityTypes;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections.ActivityTypes;
using Hexalith.Timesheets.Runtime;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.Runtime;
using Hexalith.Timesheets.Server.TimeEntries;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Shouldly;

using ServerCapabilityState = Hexalith.Timesheets.Server.MagicLinks.MagicLinkCapabilityState;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed class MagicLinkConfirmationHttpBoundaryTests
{
    private static readonly DateTimeOffset ObservedAtUtc = new(2026, 6, 19, 13, 30, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Exercises privacy assertions without HTTP authorization or a running host.
    /// </summary>
    /// <param name="category">The synthetic logger category.</param>
    /// <param name="message">The rendered message or response body.</param>
    /// <param name="state">The rendered structured logger state.</param>
    /// <param name="token">The request token whose material must remain private.</param>
    /// <param name="isResponse">Whether to exercise response-body exclusions.</param>
    /// <param name="shouldReject">Whether the assertion must reject the supplied material.</param>
    [Theory]
    [MemberData(nameof(PrivacyAssertionCases))]
    public void PrivacyAssertionsRespectCategoryAndResponseScope(
        string category,
        string message,
        string[] state,
        string token,
        bool isResponse,
        bool shouldReject)
    {
        Action assertion = isResponse
            ? () => AssertSensitiveMaterialAbsent(message, token)
            : () => AssertSensitiveDiagnosticsAbsent(new LogRecord(category, message, state), token);

        if (shouldReject)
        {
            Should.Throw<ShouldAssertException>(assertion);
        }
        else
        {
            assertion();
        }
    }

    /// <summary>
    /// Supplies independent positive and negative privacy cases for every captured field.
    /// </summary>
    /// <returns>Logger and response cases with their expected assertion outcome.</returns>
    public static IEnumerable<object[]> PrivacyAssertionCases()
    {
        const string ProbeToken = "query-secret-probe";
        string[] categories =
        [
            "Microsoft.Hosting.Lifetime",
            "Microsoft.AspNetCore.Hosting.Diagnostics",
            "ThirdParty.Diagnostics",
            "Hexalith.Timesheets.Tests"
        ];
        string[] genericTerms =
        [
            "comment", "token", "Delivery", "durationMinutes", "60", "Draft", "RecoveryPath",
            "revoked", "used", "unauthorized", "cross-tenant", "wrong-recipient", "wrong-action",
            "stale-catalog", "project-owned", "repeated-token"
        ];
        string contentRootMessage = $"Content root path: /tmp/{string.Join('-', genericTerms)}";

        foreach (string category in categories[..3])
        {
            foreach (string token in new[] { ProbeToken, string.Empty, "   " })
            {
                yield return [category, contentRootMessage, Array.Empty<string>(), token, false, false];
                yield return [$"{category}.{string.Join('.', genericTerms)}", "safe", Array.Empty<string>(), token, false, false];
                yield return [category, "safe", new[] { $"ContentRoot={contentRootMessage}" }, token, false, false];
            }
        }

        string[] protectedValues =
        [
            ProbeToken, Hash(ProbeToken), "party-1", "party-2", "project-1", "work-1",
            "time-entry-1", "time-entry-2", "sensitive customer comment"
        ];
        foreach (string protectedValue in protectedValues)
        {
            // Uppercase probes also pin the case-insensitive exclusions.
            string material = protectedValue.ToUpperInvariant();
            foreach (string category in categories)
            {
                yield return [$"{category}.{material}", "safe", Array.Empty<string>(), ProbeToken, false, true];
                yield return [category, material, Array.Empty<string>(), ProbeToken, false, true];
                yield return [category, "safe", new[] { $"Value={material}" }, ProbeToken, false, true];
            }

            yield return ["ResponseBody", material, Array.Empty<string>(), ProbeToken, true, true];
        }

        foreach (string token in new[] { string.Empty, "   " })
        {
            foreach (string protectedValue in protectedValues[2..])
            {
                foreach (string category in categories)
                {
                    yield return [$"{category}.{protectedValue}", "safe", Array.Empty<string>(), token, false, true];
                    yield return [category, protectedValue, Array.Empty<string>(), token, false, true];
                    yield return [category, "safe", new[] { $"Value={protectedValue}" }, token, false, true];
                }

                yield return ["ResponseBody", protectedValue, Array.Empty<string>(), token, true, true];
            }
        }

        const string SecondToken = "separate-query-secret";
        foreach (string material in new[] { SecondToken, Hash(SecondToken) })
        {
            foreach (string category in categories)
            {
                yield return [$"{category}.{material}", "safe", Array.Empty<string>(), SecondToken, false, true];
                yield return [category, material, Array.Empty<string>(), SecondToken, false, true];
                yield return [category, "safe", new[] { $"Value={material}" }, SecondToken, false, true];
            }

            yield return ["ResponseBody", material, Array.Empty<string>(), SecondToken, true, true];
        }

        foreach (string genericTerm in genericTerms)
        {
            string material = genericTerm.ToUpperInvariant();
            yield return [$"Hexalith.Timesheets.Tests.{material}", "safe", Array.Empty<string>(), ProbeToken, false, true];
            yield return ["Hexalith.Timesheets.Tests", material, Array.Empty<string>(), ProbeToken, false, true];
            yield return ["Hexalith.Timesheets.Tests", "safe", new[] { $"Value={material}" }, ProbeToken, false, true];
            yield return ["ResponseBody", material, Array.Empty<string>(), ProbeToken, true, true];
        }

        foreach (string tenant in new[] { "tenant-1", "tenant-2" })
        {
            yield return ["ResponseBody", tenant, Array.Empty<string>(), ProbeToken, true, true];
        }

        yield return
        [
            "Hexalith.Timesheets.MagicLinkBoundary",
            "External link denial emitted with category Unknown at 2026-06-19T13:30:00+00:00 for correlation request-1.",
            new[] { "Category=Unknown", "TimestampUtc=2026-06-19T13:30:00+00:00", "CorrelationId=request-1" },
            ProbeToken, false, false
        ];
        string denialBody = JsonSerializer.Serialize(new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Title = MagicLinkInvalidLinkDenial.Default.Title,
            Detail = MagicLinkInvalidLinkDenial.Default.Detail,
            Status = (int)HttpStatusCode.Forbidden
        }, JsonOptions);
        yield return ["ResponseBody", denialBody, Array.Empty<string>(), ProbeToken, true, false];
    }

    [Fact]
    public async Task Invalid_magic_link_http_boundary_responses_are_equivalent_and_opaque()
    {
        using MagicLinkHttpBoundaryFactory factory = new();
        using HttpClient client = factory.CreateClient();

        List<CapturedFailure> failures = [];

        foreach (ExternalRoute route in ExternalRoutes())
        {
            foreach (string caseName in InvalidCaseNames())
            {
                string token = Token(route, caseName);
                using HttpResponseMessage response = await SendAsync(client, route, token);

                CapturedFailure failure = await CaptureFailureAsync(
                    response,
                    $"{route.Name}:{caseName}",
                    token);

                failures.Add(failure);
            }
        }

        CapturedFailure baseline = failures[0];
        foreach (CapturedFailure failure in failures)
        {
            failure.StatusCode.ShouldBe(baseline.StatusCode, failure.Name);
            failure.ContentType.ShouldBe(baseline.ContentType, failure.Name);
            failure.NormalizedBody.ShouldBe(baseline.NormalizedBody, failure.Name);
            failure.Headers.ShouldBe(baseline.Headers, failure.Name);
            RawByteLength(failure.RawBody).ShouldBe(RawByteLength(baseline.RawBody), failure.Name);
        }
    }

    [Fact]
    public async Task Valid_magic_link_http_boundary_requests_are_distinguishable_from_invalid_denials()
    {
        using MagicLinkHttpBoundaryFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage confirmDisplay = await client
            .GetAsync($"/api/timesheets/magic-links/confirm?t={ValidConfirmToken()}", TestContext.Current.CancellationToken);
        using HttpResponseMessage confirmSubmit = await client
            .PostAsJsonAsync(
                $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}",
                new ConfirmTimeThroughMagicLink(),
                TestContext.Current.CancellationToken);
        using HttpResponseMessage adjustDisplay = await client
            .GetAsync($"/api/timesheets/magic-links/adjust?t={ValidAdjustToken()}", TestContext.Current.CancellationToken);
        using HttpResponseMessage adjustSubmit = await client
            .PostAsJsonAsync(
                $"/api/timesheets/magic-links/adjust/submit?t={ValidAdjustToken()}",
                AdjustCommand(),
                TestContext.Current.CancellationToken);

        confirmDisplay.StatusCode.ShouldBe(HttpStatusCode.OK);
        confirmSubmit.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        adjustDisplay.StatusCode.ShouldBe(HttpStatusCode.OK);
        adjustSubmit.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        MagicLinkConfirmationDisplayResponse confirmation = (await confirmDisplay.Content
            .ReadFromJsonAsync<MagicLinkConfirmationDisplayResponse>(JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        confirmation.DurationMinutes.ShouldBe(60);
        confirmation.ActivityTypeLabel.ShouldBe("Delivery");

        MagicLinkAdjustmentDisplayResponse adjustment = (await adjustDisplay.Content
            .ReadFromJsonAsync<MagicLinkAdjustmentDisplayResponse>(JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        adjustment.EditableFields.ShouldContain("durationMinutes");
        adjustment.ReadOnlyFields.ShouldContain("tenant");
    }

    [Fact]
    public async Task Concrete_loader_reaches_all_valid_http_routes_after_projection_delivery_without_read_model_seeding()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        await factory.ProjectValidStateAsync(
            client,
            ValidConfirmToken(),
            MagicLinkAllowedAction.Confirm,
            new MagicLinkCapabilityId("capability-confirm"),
            new TimeEntryId("time-entry-confirm"));
        await factory.ProjectValidStateAsync(
            client,
            ValidAdjustToken(),
            MagicLinkAllowedAction.Adjust,
            new MagicLinkCapabilityId("capability-adjust"),
            new TimeEntryId("time-entry-adjust"));

        using HttpResponseMessage confirmDisplay = await client.GetAsync(
            $"/api/timesheets/magic-links/confirm?t={ValidConfirmToken()}",
            TestContext.Current.CancellationToken);
        using HttpResponseMessage confirmSubmit = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}",
            new ConfirmTimeThroughMagicLink(),
            TestContext.Current.CancellationToken);
        using HttpResponseMessage adjustDisplay = await client.GetAsync(
            $"/api/timesheets/magic-links/adjust?t={ValidAdjustToken()}",
            TestContext.Current.CancellationToken);
        using HttpResponseMessage adjustSubmit = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/adjust/submit?t={ValidAdjustToken()}",
            AdjustCommand(),
            TestContext.Current.CancellationToken);

        confirmDisplay.StatusCode.ShouldBe(HttpStatusCode.OK);
        confirmSubmit.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        adjustDisplay.StatusCode.ShouldBe(HttpStatusCode.OK);
        adjustSubmit.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        factory.Store.DirectIndexSeedCount.ShouldBe(0);
        factory.Store.DirectCatalogSeedCount.ShouldBe(0);
        factory.Store.Get<MagicLinkTokenHashCapabilityIndexReadModel>(
            MagicLinkTokenHashCapabilityIndexProjection.StateKey).Entries.Count.ShouldBe(2);
        factory.Store.Get<ActivityTypeCatalogReadModel>(
                MagicLinkActivityTypeCatalogReadModelAddress.StateKey(Tenant()))
            .ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
    }

    /// <summary>Exercises the deactivation decision through projection delivery and all four HTTP routes.</summary>
    [Fact]
    public async Task DeactivatedRecordedTypeCanBeDisplayedAndConfirmedWhileAdjustmentStaysDenied()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            new MagicLinkCapabilityId("capability-confirm-inactive"), new TimeEntryId("time-entry-confirm-inactive"),
            deactivateActivityType: true);
        await factory.ProjectValidStateAsync(client, ValidAdjustToken(), MagicLinkAllowedAction.Adjust,
            new MagicLinkCapabilityId("capability-adjust-inactive"), new TimeEntryId("time-entry-adjust-inactive"));
        ActivityTypeCatalogItem item = factory.Store
            .Get<ActivityTypeCatalogReadModel>(MagicLinkActivityTypeCatalogReadModelAddress.StateKey(Tenant()))
            .Items.ShouldHaveSingleItem();
        item.IsActive.ShouldBeFalse();
        item.IsAvailableForCapture.ShouldBeFalse();

        using HttpResponseMessage confirmDisplay = await client.GetAsync(
            $"/api/timesheets/magic-links/confirm?t={ValidConfirmToken()}", TestContext.Current.CancellationToken);
        using HttpResponseMessage confirmSubmit = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);
        using HttpResponseMessage adjustDisplay = await client.GetAsync(
            $"/api/timesheets/magic-links/adjust?t={ValidAdjustToken()}", TestContext.Current.CancellationToken);
        using HttpResponseMessage adjustSubmit = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/adjust/submit?t={ValidAdjustToken()}",
            AdjustCommand(), TestContext.Current.CancellationToken);

        confirmDisplay.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await confirmDisplay.Content.ReadFromJsonAsync<MagicLinkConfirmationDisplayResponse>(
            JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull().ActivityTypeLabel.ShouldBe("Delivery");
        confirmSubmit.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        CapturedFailure displayDenial = await CaptureFailureAsync(adjustDisplay, "inactive-adjust-display", ValidAdjustToken());
        CapturedFailure submitDenial = await CaptureFailureAsync(adjustSubmit, "inactive-adjust-submit", ValidAdjustToken());
        displayDenial.NormalizedBody.ShouldBe(submitDenial.NormalizedBody);
    }

    /// <summary>Proves configuration-bound internal delivery persists the loader's read models.</summary>
    [Fact]
    public async Task ConfiguredInternalPortDeliversProjectionsAndResolvesAValidLink()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, internalPort: 8081, localPort: 8081);
        using HttpClient client = factory.CreateClient();
        factory.Services.GetRequiredService<IOptions<InternalSurfaceOptions>>().Value.AllowOnAnyPort.ShouldBeFalse();
        factory.Services.GetRequiredService<IOptions<InternalSurfaceOptions>>().Value.Port.ShouldBe(8081);

        var capabilityId = new MagicLinkCapabilityId("capability-internal-port");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            capabilityId, new TimeEntryId("time-entry-internal-port"));
        await factory.ProjectValidStateAsync(client, ValidAdjustToken(), MagicLinkAllowedAction.Adjust,
            new MagicLinkCapabilityId("capability-adjust-public-port"), new TimeEntryId("time-entry-adjust-public-port"));

        MagicLinkTokenHashCapabilityIndexEntry candidate = factory.Store
            .Get<MagicLinkTokenHashCapabilityIndexReadModel>(MagicLinkTokenHashCapabilityIndexProjection.StateKey)
            .Entries[Hash(ValidConfirmToken())];
        candidate.CapabilityId.ShouldBe(capabilityId);
        candidate.Tenant.ShouldBe(Tenant());
        factory.Store.Get<ActivityTypeCatalogReadModel>(MagicLinkActivityTypeCatalogReadModelAddress.StateKey(Tenant()))
            .ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        factory.LocalPort = 8080;
        foreach (ExternalRoute route in ExternalRoutes())
        {
            string token = route.Action == MagicLinkAllowedAction.Confirm ? ValidConfirmToken() : ValidAdjustToken();
            using HttpResponseMessage response = await SendAsync(client, route, token);
            response.StatusCode.ShouldBe(route.Method == HttpMethod.Get ? HttpStatusCode.OK : HttpStatusCode.Accepted);
        }
    }

    /// <summary>Proves the configured port denies projection writes arriving on the public port.</summary>
    /// <param name="projectionPath">The projection route spelling used on the public listener.</param>
    [Theory]
    [InlineData("/project/v2")]
    [InlineData("/PROJECT/v2")]
    public async Task ConfiguredInternalPortRefusesProjectionWritesOnThePublicPort(string projectionPath)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, internalPort: 8081, localPort: 8081);
        using HttpClient client = factory.CreateClient();
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            new MagicLinkCapabilityId("capability-internal-seed"), new TimeEntryId("time-entry-internal-seed"));
        ProjectionDispatchRequest admitted = factory.LastIndexDispatch.ShouldNotBeNull();
        ProjectionEventDto admittedEvent = admitted.Request.Events.ShouldHaveSingleItem();
        MagicLinkConfirmationCapabilityIssued issued = JsonSerializer.Deserialize<MagicLinkConfirmationCapabilityIssued>(
            admittedEvent.Payload, JsonOptions).ShouldNotBeNull() with
        {
            CapabilityId = new MagicLinkCapabilityId("capability-refused"),
            TokenHash = new MagicLinkTokenHash(Hash("refused-token"))
        };
        ProjectionDispatchRequest dispatch = admitted with
        {
            DispatchId = "dispatch-refused",
            Request = admitted.Request with
            {
                AggregateId = issued.CapabilityId.Value,
                Events = [admittedEvent with
                {
                    Payload = JsonSerializer.SerializeToUtf8Bytes(issued, JsonOptions),
                    MessageId = "projection-refused"
                }]
            }
        };
        string indexBefore = JsonSerializer.Serialize(factory.Store.Get<MagicLinkTokenHashCapabilityIndexReadModel>(
            MagicLinkTokenHashCapabilityIndexProjection.StateKey), JsonOptions);
        string catalogBefore = JsonSerializer.Serialize(factory.Store.Get<ActivityTypeCatalogReadModel>(
            MagicLinkActivityTypeCatalogReadModelAddress.StateKey(Tenant())), JsonOptions);
        factory.LocalPort = 8080;
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            projectionPath, dispatch, JsonOptions, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        JsonSerializer.Serialize(factory.Store.Get<MagicLinkTokenHashCapabilityIndexReadModel>(
            MagicLinkTokenHashCapabilityIndexProjection.StateKey), JsonOptions).ShouldBe(indexBefore);
        JsonSerializer.Serialize(factory.Store.Get<ActivityTypeCatalogReadModel>(
            MagicLinkActivityTypeCatalogReadModelAddress.StateKey(Tenant())), JsonOptions).ShouldBe(catalogBefore);

        factory.LocalPort = 8081;
        using HttpResponseMessage admittedResponse = await client.PostAsJsonAsync(
            projectionPath, dispatch, JsonOptions, TestContext.Current.CancellationToken);
        admittedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admittedResponse.Content.ReadFromJsonAsync<ProjectionDispatchResponse>(
            JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull()
            .Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        factory.Store.Get<MagicLinkTokenHashCapabilityIndexReadModel>(MagicLinkTokenHashCapabilityIndexProjection.StateKey)
            .Entries[issued.TokenHash.Value].CapabilityId.ShouldBe(issued.CapabilityId);
    }

    /// <summary>Exercises the host's registered accessor through tenant-scoped administrator loads.</summary>
    /// <param name="useMismatchedClaims">Whether HTTP claims name a tenant without the projected catalog.</param>
    /// <param name="claimVariant">The prioritized or fallback claim set to exercise.</param>
    [Theory]
    [InlineData(false, "standard")]
    [InlineData(true, "standard")]
    [InlineData(false, "tenant-fallback")]
    [InlineData(false, "actor-fallback")]
    [InlineData(false, "conflicting")]
    public async Task AdminStateLoadsUseTheHostsHttpClaimsAccessor(bool useMismatchedClaims, string claimVariant)
    {
        Claim[]? claims = claimVariant switch
        {
            "standard" => null,
            "tenant-fallback" => [new Claim("tenant", Tenant().TenantId), new Claim("party_id", Contributor().PartyId)],
            "actor-fallback" => [new Claim("tenant_id", Tenant().TenantId), new Claim(ClaimTypes.NameIdentifier, Contributor().PartyId)],
            "conflicting" =>
            [
                new Claim("tenant", OtherTenant().TenantId),
                new Claim("tenant_id", Tenant().TenantId),
                new Claim(ClaimTypes.NameIdentifier, OtherContributor().PartyId),
                new Claim("party_id", Contributor().PartyId)
            ],
            _ => throw new InvalidOperationException($"Unknown claim variant '{claimVariant}'.")
        };
        using MagicLinkHttpBoundaryFactory factory = new(
            useConcreteLoader: true, useMismatchedClaims: useMismatchedClaims, claims: claims);
        using HttpClient client = factory.CreateClient();
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            new MagicLinkCapabilityId("capability-existing"), new TimeEntryId("time-entry-existing"));
        factory.Store.ReadKeys.Clear();
        factory.Gateway.Requests.Clear();
        var capabilityId = new MagicLinkCapabilityId("capability-admin-new");
        var command = new IssueMagicLinkConfirmationCapability(
            capabilityId,
            new MagicLinkConfirmationScope(Contributor(), TimeEntryTargetReference.ForProject(Project()),
                ActivityId(), new TimeEntryId("time-entry-admin-new"), MagicLinkTargetKind.ProposedTimeEntry),
            MagicLinkAllowedAction.Confirm, ObservedAtUtc.AddDays(1), new MagicLinkAuditMetadata("timesheets", "admin-issue"));

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/timesheets/magic-links/confirmation-capabilities", command, JsonOptions, TestContext.Current.CancellationToken);

        TenantReference expectedTenant = useMismatchedClaims ? OtherTenant() : Tenant();
        PartyReference expectedActor = useMismatchedClaims ? OtherContributor() : Contributor();
        factory.ObservedTrustedContext.Tenant.ShouldBe(expectedTenant);
        factory.ObservedTrustedContext.Actor.ShouldBe(expectedActor);
        factory.ObservedTrustedContext.CorrelationId.ShouldNotBeNullOrWhiteSpace();
        TimesheetsRequestContext authorizedContext = factory.AccessGuard.LastAuthorizationRequest.ShouldNotBeNull().Context;
        authorizedContext.Tenant.ShouldBe(expectedTenant);
        authorizedContext.Actor.ShouldBe(expectedActor);
        authorizedContext.CorrelationId.ShouldBe(factory.ObservedTrustedContext.CorrelationId);
        factory.Store.ReadKeys.ShouldHaveSingleItem().ShouldBe(MagicLinkActivityTypeCatalogReadModelAddress.StateKey(expectedTenant));
        StreamReadRequest request = factory.Gateway.Requests.ShouldHaveSingleItem();
        request.Tenant.ShouldBe(expectedTenant.TenantId);
        request.AggregateId.ShouldBe(capabilityId.Value);
        response.StatusCode.ShouldBe(useMismatchedClaims ? HttpStatusCode.Forbidden : HttpStatusCode.Accepted);

        // Revoking an existing capability requires the authoritative stream to be folded, whereas
        // issuance of a new id correctly succeeds when that stream is absent.
        factory.Gateway.Requests.Clear();
        factory.Store.ReadKeys.Clear();
        var existingId = new MagicLinkCapabilityId("capability-existing");
        using HttpResponseMessage revocation = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirmation-capabilities/{existingId.Value}/revoke",
            new RevokeMagicLinkConfirmationCapability(existingId, new MagicLinkAuditMetadata("timesheets", "admin-revoke")),
            JsonOptions, TestContext.Current.CancellationToken);
        revocation.StatusCode.ShouldBe(useMismatchedClaims ? HttpStatusCode.Forbidden : HttpStatusCode.Accepted);
        StreamReadRequest existingRequest = factory.Gateway.Requests.ShouldHaveSingleItem();
        existingRequest.Tenant.ShouldBe(expectedTenant.TenantId);
        existingRequest.AggregateId.ShouldBe(existingId.Value);
        factory.Store.ReadKeys.ShouldBeEmpty();
        TimesheetsRequestContext revocationContext = factory.AccessGuard.LastAuthorizationRequest.ShouldNotBeNull().Context;
        revocationContext.Tenant.ShouldBe(expectedTenant);
        revocationContext.Actor.ShouldBe(expectedActor);
        revocationContext.CorrelationId.ShouldBe(factory.ObservedTrustedContext.CorrelationId);
    }

    /// <summary>Refuses missing issuance identifiers before loading state, authorizing or generating material.</summary>
    /// <param name="omitIdentifier">Whether the identifier is omitted rather than explicitly JSON-null.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IssuanceWithMissingCapabilityIdIsOpaqueAndPerformsNoProtectedWork(bool omitIdentifier)
    {
        var generator = new CountingMagicLinkTokenGenerator();
        using MagicLinkHttpBoundaryFactory factory = new(
            useConcreteLoader: true, useMismatchedClaims: false, tokenGenerator: generator);
        using HttpClient client = factory.CreateClient();
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            new MagicLinkCapabilityId("capability-existing"), new TimeEntryId("time-entry-existing"));
        var command = new IssueMagicLinkConfirmationCapability(
            new MagicLinkCapabilityId("capability-admin-new"),
            new MagicLinkConfirmationScope(Contributor(), TimeEntryTargetReference.ForProject(Project()),
                ActivityId(), new TimeEntryId("time-entry-admin-new"), MagicLinkTargetKind.ProposedTimeEntry),
            MagicLinkAllowedAction.Confirm, ObservedAtUtc.AddDays(1), new MagicLinkAuditMetadata("timesheets", "admin-issue"));
        using HttpResponseMessage valid = await client.PostAsJsonAsync(
            "/api/timesheets/magic-links/confirmation-capabilities", command, JsonOptions, TestContext.Current.CancellationToken);
        valid.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        generator.GenerationCount.ShouldBe(1);
        factory.ObservedTrustedContext.Tenant.ShouldBe(Tenant());
        factory.ObservedTrustedContext.Actor.ShouldBe(Contributor());
        factory.Store.ReadKeys.Clear();
        factory.Gateway.Requests.Clear();
        int authorizationBefore = factory.AccessGuard.AuthorizationCount;
        int trustedWorkBefore = factory.AccessGuard.TrustedWorkExecutionCount;
        string indexBefore = JsonSerializer.Serialize(factory.Store.Get<MagicLinkTokenHashCapabilityIndexReadModel>(
            MagicLinkTokenHashCapabilityIndexProjection.StateKey), JsonOptions);
        string catalogBefore = JsonSerializer.Serialize(factory.Store.Get<ActivityTypeCatalogReadModel>(
            MagicLinkActivityTypeCatalogReadModelAddress.StateKey(Tenant())), JsonOptions);
        JsonObject body = JsonSerializer.SerializeToNode(command, JsonOptions).ShouldNotBeNull().AsObject();
        if (omitIdentifier)
        {
            body.Remove("capabilityId").ShouldBeTrue();
        }
        else
        {
            body["capabilityId"] = null;
        }

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/timesheets/magic-links/confirmation-capabilities", body, JsonOptions, TestContext.Current.CancellationToken);

        CapturedFailure failure = await CaptureFailureAsync(response, omitIdentifier ? "missing-id" : "null-id", string.Empty);
        failure.RawBody.ShouldNotContain("oneTimeToken", Case.Insensitive);
        failure.RawBody.ShouldNotContain("capabilityId", Case.Insensitive);
        factory.Store.ReadKeys.ShouldBeEmpty();
        factory.Gateway.Requests.ShouldBeEmpty();
        factory.AccessGuard.AuthorizationCount.ShouldBe(authorizationBefore);
        factory.AccessGuard.TrustedWorkExecutionCount.ShouldBe(trustedWorkBefore);
        generator.GenerationCount.ShouldBe(1);
        JsonSerializer.Serialize(factory.Store.Get<MagicLinkTokenHashCapabilityIndexReadModel>(
            MagicLinkTokenHashCapabilityIndexProjection.StateKey), JsonOptions).ShouldBe(indexBefore);
        JsonSerializer.Serialize(factory.Store.Get<ActivityTypeCatalogReadModel>(
            MagicLinkActivityTypeCatalogReadModelAddress.StateKey(Tenant())), JsonOptions).ShouldBe(catalogBefore);
    }

    [Fact]
    public async Task Concrete_loader_cross_tenant_candidate_is_indistinguishable_from_an_unknown_token_on_every_route()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        ExternalRoute baselineRoute = ExternalRoutes()[0];
        string unknownToken = $"{baselineRoute.Name}-concrete-unknown";
        using HttpResponseMessage unknownResponse = await SendAsync(client, baselineRoute, unknownToken);
        CapturedFailure baseline = await CaptureFailureAsync(
            unknownResponse,
            "baseline:concrete-unknown",
            unknownToken);
        int trustedWorkExecutionCount = factory.AccessGuard.TrustedWorkExecutionCount;

        foreach (ExternalRoute route in ExternalRoutes())
        {
            string token = $"{route.Name}-concrete-cross-tenant";
            await factory.ProjectCrossTenantCandidateAsync(
                client,
                token,
                route.Action,
                new MagicLinkCapabilityId($"capability-{route.Name}-cross-tenant"),
                new TimeEntryId($"time-entry-{route.Name}-cross-tenant"));
            MagicLinkTokenHashCapabilityIndexEntry candidate = factory.Store
                .Get<MagicLinkTokenHashCapabilityIndexReadModel>(MagicLinkTokenHashCapabilityIndexProjection.StateKey)
                .Entries[Hash(token)];
            candidate.Tenant.ShouldBe(Tenant());
            int gatewayRequestCount = factory.Gateway.Requests.Count;

            using HttpResponseMessage response = await SendAsync(client, route, token);
            CapturedFailure failure = await CaptureFailureAsync(response, route.Name, token);
            StreamReadRequest request = factory.Gateway.Requests
                .Skip(gatewayRequestCount)
                .ShouldHaveSingleItem();

            failure.StatusCode.ShouldBe(baseline.StatusCode, failure.Name);
            failure.ContentType.ShouldBe(baseline.ContentType, failure.Name);
            failure.NormalizedBody.ShouldBe(baseline.NormalizedBody, failure.Name);
            failure.Headers.ShouldBe(baseline.Headers, failure.Name);
            RawByteLength(failure.RawBody).ShouldBe(RawByteLength(baseline.RawBody), failure.Name);
            request.Tenant.ShouldBe(Tenant().TenantId);
            request.AggregateId.ShouldBe(candidate.CapabilityId.Value);
        }

        factory.AccessGuard.TrustedWorkExecutionCount.ShouldBe(trustedWorkExecutionCount);
        factory.Store.DirectIndexSeedCount.ShouldBe(0);
        factory.Store.DirectCatalogSeedCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("missing-sequence")]
    [InlineData("identity-conflict")]
    [InlineData("unreadable")]
    public async Task Rejected_catalog_delivery_keeps_confirm_submit_opaque_without_capability_use(string scenario)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        string token = $"rejected-catalog-{scenario}";
        await factory.ProjectRejectedCatalogStateAsync(client, token, scenario);
        int authorizationCount = factory.AccessGuard.AuthorizationCount;

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={token}",
            new ConfirmTimeThroughMagicLink(),
            TestContext.Current.CancellationToken);

        _ = await CaptureFailureAsync(response, scenario, token);
        factory.AccessGuard.AuthorizationCount.ShouldBe(authorizationCount);
        factory.Store.Contains(MagicLinkActivityTypeCatalogReadModelAddress.StateKey(Tenant())).ShouldBeFalse();
    }

    [Fact]
    public async Task Empty_magic_link_token_uses_the_same_opaque_boundary_denial()
    {
        using MagicLinkHttpBoundaryFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client
            .GetAsync("/api/timesheets/magic-links/confirm?t=%20%20", TestContext.Current.CancellationToken);

        CapturedFailure failure = await CaptureFailureAsync(response, "empty-token", "  ");

        failure.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        failure.ContentType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Repeated_invalid_magic_link_attempts_are_byte_equivalent_without_throttling_headers()
    {
        using MagicLinkHttpBoundaryFactory factory = new();
        using HttpClient client = factory.CreateClient();
        string token = Token(ExternalRoutes()[0], "repeated-token");

        CapturedFailure[] repeatedFailures = [];
        for (int i = 0; i < 3; i++)
        {
            using HttpResponseMessage response = await client
                .GetAsync($"/api/timesheets/magic-links/confirm?t={token}", TestContext.Current.CancellationToken);
            repeatedFailures = [.. repeatedFailures, await CaptureFailureAsync(response, $"repeat-{i}", token)];
        }

        foreach (CapturedFailure failure in repeatedFailures.Skip(1))
        {
            failure.RawBody.ShouldBe(repeatedFailures[0].RawBody, failure.Name);
            failure.Headers.ShouldBe(repeatedFailures[0].Headers, failure.Name);
        }

        repeatedFailures[0].Headers.ShouldNotContain(static header => header.StartsWith("Retry-After:", StringComparison.Ordinal));
        repeatedFailures[0].Headers.ShouldNotContain(static header => header.StartsWith("X-RateLimit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Invalid_magic_link_http_boundary_emits_no_timesheets_sensitive_diagnostics()
    {
        using MagicLinkHttpBoundaryFactory factory = new();
        using HttpClient client = factory.CreateClient();
        List<string> tokens = [];
        foreach (ExternalRoute route in ExternalRoutes())
        {
            string invalidToken = Token(route, "unknown");
            string validToken = route.Action == MagicLinkAllowedAction.Confirm ? ValidConfirmToken() : ValidAdjustToken();
            tokens.Add(invalidToken);
            tokens.Add(validToken);
            using HttpResponseMessage denial = await SendAsync(client, route, invalidToken);
            denial.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            using HttpResponseMessage success = await SendAsync(client, route, validToken);
            success.StatusCode.ShouldBe(route.Method == HttpMethod.Get ? HttpStatusCode.OK : HttpStatusCode.Accepted);
        }

        LogRecord[] records = factory.Logs.Records.ToArray();

        records.ShouldNotBeEmpty();
        string.Join(' ', records.Select(static record => string.Join(' ', record.State))).ShouldContain("Category=Unknown");

        foreach (LogRecord record in records)
        {
            foreach (string token in tokens)
            {
                AssertSensitiveDiagnosticsAbsent(record, token);
            }
        }
    }

    /// <summary>Proves the host suppresses query-bearing Information request logs while retaining warnings.</summary>
    [Fact]
    public void HostingDiagnosticsFilterSuppressesInformationAndKeepsWarnings()
    {
        using MagicLinkHttpBoundaryFactory factory = new();
        using HttpClient client = factory.CreateClient();
        ILogger logger = factory.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics");

        logger.IsEnabled(LogLevel.Information).ShouldBeFalse();
        logger.IsEnabled(LogLevel.Warning).ShouldBeTrue();
        logger.LogInformation("hosting-filter-information-probe");
        logger.LogWarning("hosting-filter-warning-probe");
        factory.Logs.Records.ShouldNotContain(record => record.Message.Contains("hosting-filter-information-probe", StringComparison.Ordinal));
        factory.Logs.Records.ShouldContain(record => record.Category == "Microsoft.AspNetCore.Hosting.Diagnostics"
            && record.Message == "hosting-filter-warning-probe");
    }

    /// <summary>Enforces request-log privacy through provider overrides while retaining unrelated log levels.</summary>
    /// <param name="configuredCategory">The provider default or hosting category configured at Information.</param>
    [Theory]
    [InlineData("Default")]
    [InlineData("Microsoft.AspNetCore.Hosting.Diagnostics")]
    public async Task HostingDiagnosticsFilterSurvivesProviderSpecificInformationConfiguration(string configuredCategory)
    {
        using MagicLinkHttpBoundaryFactory factory = new(providerLoggingCategory: configuredCategory);
        using HttpClient client = factory.CreateClient();
        ILoggerFactory loggers = factory.Services.GetRequiredService<ILoggerFactory>();
        ILogger hosting = loggers.CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics");
        ILogger unrelated = loggers.CreateLogger("Hexalith.Timesheets.Tests.LoggingProbe");
        hosting.IsEnabled(LogLevel.Information).ShouldBeFalse();
        hosting.IsEnabled(LogLevel.Warning).ShouldBeTrue();
        unrelated.IsEnabled(LogLevel.Debug).ShouldBeTrue();
        unrelated.LogDebug("unrelated-logging-probe");
        factory.Logs.Records.ShouldContain(record => record.Message == "unrelated-logging-probe");
        foreach (ExternalRoute route in ExternalRoutes())
        {
            string token = Token(route, "unknown");
            using HttpResponseMessage response = await SendAsync(client, route, token);
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            foreach (LogRecord record in factory.Logs.Records)
            {
                AssertSensitiveDiagnosticsAbsent(record, token);
            }
        }
    }

    [Fact]
    public async Task Confirm_submit_reports_one_indistinguishable_category_for_every_non_fresh_catalog()
    {
        ExternalRoute route = ExternalRoutes().Single(static candidate => candidate.Name == "confirm-submit");

        // A non-Fresh catalog behind an otherwise resolvable token, and a token that never resolved,
        // must be reported identically. The loader collapses stale, rebuilding, degraded, absent and
        // unreadable catalogs into one Unavailable state and discards the bundle with it, so no code
        // can tell those apart — a StaleCatalog category here would assert a distinction that does
        // not exist, and previously read as reachable only because a scripted loader could fake it.
        using (MagicLinkHttpBoundaryFactory staleFactory = new())
        {
            using HttpClient staleClient = staleFactory.CreateClient();
            using HttpResponseMessage staleResponse = await SendAsync(staleClient, route, Token(route, "stale-catalog"));

            staleResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            string staleDiagnostics = RenderedDiagnostics(staleFactory);
            staleDiagnostics.ShouldContain("Category=Unknown");
            staleDiagnostics.ShouldNotContain("Category=StaleCatalog");
        }

        using MagicLinkHttpBoundaryFactory unresolvedFactory = new();
        using HttpClient unresolvedClient = unresolvedFactory.CreateClient();
        using HttpResponseMessage unresolvedResponse = await SendAsync(unresolvedClient, route, Token(route, "unresolved"));

        unresolvedResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        string unresolvedDiagnostics = RenderedDiagnostics(unresolvedFactory);
        unresolvedDiagnostics.ShouldContain("Category=Unknown");
        unresolvedDiagnostics.ShouldNotContain("Category=StaleCatalog");
    }

    [Fact]
    public async Task Empty_or_missing_magic_link_token_is_indistinguishable_from_an_invalid_state_denial_on_every_route()
    {
        using MagicLinkHttpBoundaryFactory factory = new();
        using HttpClient client = factory.CreateClient();

        // Capture a known invalid-state denial once as the equivalence baseline.
        ExternalRoute baselineRoute = ExternalRoutes()[0];
        string baselineToken = Token(baselineRoute, "unknown");
        using HttpResponseMessage baselineResponse = await SendAsync(client, baselineRoute, baselineToken);
        CapturedFailure baseline = await CaptureFailureAsync(baselineResponse, "baseline:unknown", baselineToken);

        // The blank-token branch (empty, whitespace, and a completely absent ?t=) must collapse into the
        // exact same opaque 403 across every external route — not merely "a 403", but byte-for-byte the same
        // ProblemDetails body and header set as a genuine invalid-state denial.
        List<CapturedFailure> boundaryFailures = [];
        foreach (ExternalRoute route in ExternalRoutes())
        {
            foreach ((string query, string label) in BlankTokenQueries())
            {
                using HttpResponseMessage response = await SendWithRawQueryAsync(client, route, query);
                boundaryFailures.Add(await CaptureFailureAsync(response, $"{route.Name}:{label}", string.Empty));
            }
        }

        foreach (CapturedFailure failure in boundaryFailures)
        {
            failure.StatusCode.ShouldBe(baseline.StatusCode, failure.Name);
            failure.ContentType.ShouldBe(baseline.ContentType, failure.Name);
            failure.NormalizedBody.ShouldBe(baseline.NormalizedBody, failure.Name);
            failure.Headers.ShouldBe(baseline.Headers, failure.Name);
            RawByteLength(failure.RawBody).ShouldBe(RawByteLength(baseline.RawBody), failure.Name);
        }
    }

    [Fact]
    public async Task Valid_magic_link_display_responses_do_not_disclose_internal_sensitive_material()
    {
        using MagicLinkHttpBoundaryFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage confirmDisplay = await client
            .GetAsync($"/api/timesheets/magic-links/confirm?t={ValidConfirmToken()}", TestContext.Current.CancellationToken);
        using HttpResponseMessage adjustDisplay = await client
            .GetAsync($"/api/timesheets/magic-links/adjust?t={ValidAdjustToken()}", TestContext.Current.CancellationToken);

        confirmDisplay.StatusCode.ShouldBe(HttpStatusCode.OK);
        adjustDisplay.StatusCode.ShouldBe(HttpStatusCode.OK);

        // The success path is distinguishable from the uniform denial (proven elsewhere), but it must still
        // honour no-disclosure: the authorized display surface exposes the contributor's own proposed entry
        // (duration, activity-type label, billable state) and must never leak the internal time-entry comment
        // text, the upstream ExternalSource provenance, the internal approval state, or any raw identifier.
        foreach (HttpResponseMessage display in new[] { confirmDisplay, adjustDisplay })
        {
            string body = await display.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            foreach (string forbidden in InternalDisclosureMaterial())
            {
                body.ShouldNotContain(forbidden, Case.Insensitive);
            }
        }
    }

    [Fact]
    public async Task Malformed_submit_body_neither_dispatches_nor_discloses_on_magic_link_submit_routes()
    {
        using MagicLinkHttpBoundaryFactory factory = new();
        using HttpClient client = factory.CreateClient();

        // A malformed JSON command body must fail closed at the request-binding boundary: binding fails before
        // the handler runs, so the request can never be coerced into a dispatch (202), and the response must
        // never echo the query token value or any business state (none was even loaded). The framework returns
        // 400 in every environment; only the body verbosity differs (the in-process test host runs in the
        // Development environment, which renders the developer exception page — that page legitimately mentions
        // framework type names such as CancellationToken, so this asserts the token VALUE and business material
        // are absent rather than the generic word "token").
        foreach (ExternalRoute route in ExternalRoutes().Where(static candidate => candidate.Method == HttpMethod.Post))
        {
            string token = Token(route, "unknown");
            using StringContent malformedBody = new("{ this is not valid json", System.Text.Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await client
                .PostAsync($"{route.Path}?t={token}", malformedBody, TestContext.Current.CancellationToken);

            response.StatusCode.ShouldNotBe(HttpStatusCode.Accepted, route.Name);
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, route.Name);

            string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            body.ShouldNotContain(token, Case.Insensitive, route.Name);
            foreach (string forbidden in InternalDisclosureMaterial())
            {
                body.ShouldNotContain(forbidden, Case.Insensitive, route.Name);
            }
        }
    }

    [Fact]
    public async Task Empty_magic_link_token_records_the_malformed_outcome_category_without_sensitive_diagnostics()
    {
        using MagicLinkHttpBoundaryFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client
            .GetAsync("/api/timesheets/magic-links/confirm?t=%20%20", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        LogRecord[] records = factory.Logs.Records.ToArray();

        // The blank-token boundary records the distinct Malformed outcome category (vs Unknown for a resolved
        // but-rejected token) while staying privacy-safe — proving every emitted category is internal-only.
        records.ShouldNotBeEmpty();
        string.Join(' ', records.Select(static record => string.Join(' ', record.State))).ShouldContain("Category=Malformed");

        foreach (LogRecord record in records)
        {
            AssertSensitiveDiagnosticsAbsent(record, string.Empty);
        }
    }

    private static string RenderedDiagnostics(MagicLinkHttpBoundaryFactory factory)
        => string.Join(
            ' ',
            factory.Logs.Records
                .Where(static record => record.Category.StartsWith("Hexalith.Timesheets", StringComparison.Ordinal))
                .Select(static record => string.Join(' ', record.State)));

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, ExternalRoute route, string token)
    {
        if (route.Method == HttpMethod.Get)
        {
            return await client.GetAsync($"{route.Path}?t={token}", TestContext.Current.CancellationToken)
                .ConfigureAwait(false);
        }

        object command = route.Action == MagicLinkAllowedAction.Confirm
            ? new ConfirmTimeThroughMagicLink()
            : AdjustCommand();
        return await client.PostAsJsonAsync($"{route.Path}?t={token}", command, TestContext.Current.CancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<HttpResponseMessage> SendWithRawQueryAsync(HttpClient client, ExternalRoute route, string rawQuery)
    {
        string url = rawQuery.Length == 0 ? route.Path : $"{route.Path}?{rawQuery}";
        if (route.Method == HttpMethod.Get)
        {
            return await client.GetAsync(url, TestContext.Current.CancellationToken).ConfigureAwait(false);
        }

        object command = route.Action == MagicLinkAllowedAction.Confirm
            ? new ConfirmTimeThroughMagicLink()
            : AdjustCommand();
        return await client.PostAsJsonAsync(url, command, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private static async Task<CapturedFailure> CaptureFailureAsync(
        HttpResponseMessage response,
        string name,
        string token)
    {
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
            .ConfigureAwait(false);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, name);
        response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("application/problem+json", name);

        JsonObject problem = JsonNode.Parse(body).ShouldNotBeNull().AsObject();
        problem["title"]?.GetValue<string>().ShouldBe(MagicLinkInvalidLinkDenial.Default.Title, name);
        problem["detail"]?.GetValue<string>().ShouldBe(MagicLinkInvalidLinkDenial.Default.Detail, name);
        problem.ContainsKey("errors").ShouldBeFalse(name);
        problem.ContainsKey("recoveryPath").ShouldBeFalse(name);
        problem.ContainsKey("RecoveryPath").ShouldBeFalse(name);

        AssertSensitiveMaterialAbsent(body, token);

        return new CapturedFailure(
            name,
            response.StatusCode,
            response.Content.Headers.ContentType.MediaType!,
            NormalizeProblemJson(problem),
            HeaderSet(response),
            body);
    }

    private static void AssertSensitiveDiagnosticsAbsent(LogRecord record, string token)
    {
        string rendered = $"{record.Category} {record.Message} {string.Join(' ', record.State)}";
        AssertProtectedMaterialAbsent(rendered, token);
        if (record.Category.StartsWith("Hexalith.Timesheets", StringComparison.Ordinal))
        {
            AssertSensitiveMaterialAbsent(rendered, token);
        }
    }

    private static void AssertProtectedMaterialAbsent(string content, string token)
    {
        string[] forbiddenTerms =
        [
            token,
            string.IsNullOrWhiteSpace(token) ? string.Empty : Hash(token),
            "party-1",
            "party-2",
            "project-1",
            "work-1",
            "time-entry-1",
            "time-entry-2",
            "sensitive customer comment"
        ];

        foreach (string forbiddenTerm in forbiddenTerms.Where(static value => !string.IsNullOrWhiteSpace(value)))
        {
            content.ShouldNotContain(forbiddenTerm, Case.Insensitive);
        }
    }

    private static void AssertSensitiveMaterialAbsent(string content, string token)
    {
        AssertProtectedMaterialAbsent(content, token);
        string[] forbiddenTerms =
        [
            "tenant-1",
            "tenant-2",
            "comment",
            "token",
            "Delivery",
            "durationMinutes",
            "60",
            "Draft",
            "RecoveryPath",
            "revoked",
            "used",
            "unauthorized",
            "cross-tenant",
            "wrong-recipient",
            "wrong-action",
            "stale-catalog",
            "project-owned",
            "repeated-token"
        ];

        foreach (string forbiddenTerm in forbiddenTerms.Where(static value => !string.IsNullOrWhiteSpace(value)))
        {
            content.ShouldNotContain(forbiddenTerm, Case.Insensitive);
        }
    }

    private static string NormalizeProblemJson(JsonObject problem)
    {
        JsonObject normalized = [];
        foreach ((string key, JsonNode? value) in problem.OrderBy(static property => property.Key, StringComparer.Ordinal))
        {
            if (key.Equals("traceId", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            normalized[key] = value?.DeepClone();
        }

        return normalized.ToJsonString(JsonOptions);
    }

    // Raw bodies retain traceId. These TestServer responses use fixed-width W3C Activity identifiers;
    // revisit this assertion if the host falls back to variable-width HttpContext.TraceIdentifier values.
    private static int RawByteLength(string body) => Encoding.UTF8.GetByteCount(body);

    private static string[] HeaderSet(HttpResponseMessage response)
        => response.Headers
            .Concat(response.Content.Headers)
            .Where(static header => !header.Key.Equals("Date", StringComparison.OrdinalIgnoreCase))
            .Select(static header => $"{header.Key}:{string.Join(",", header.Value)}")
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static (string Query, string Label)[] BlankTokenQueries()
        =>
        [
            ("t=", "empty"),
            ("t=%20%20", "whitespace"),
            (string.Empty, "missing")
        ];

    private static string[] InternalDisclosureMaterial()
        =>
        [
            "sensitive customer comment",
            "supplier-api",
            "request-1",
            "Draft",
            "party-1",
            "party-2",
            "time-entry-1",
            "tenant-1"
        ];

    private static string[] InvalidCaseNames()
        =>
        [
            "malformed",
            "unknown",
            "expired",
            "used",
            "revoked",
            "unauthorized",
            "cross-tenant",
            "wrong-recipient",
            "wrong-action",
            "stale-catalog",
            "project-owned",
            "repeated-token"
        ];

    private static ExternalRoute[] ExternalRoutes()
        =>
        [
            new("confirm-get", HttpMethod.Get, "/api/timesheets/magic-links/confirm", MagicLinkAllowedAction.Confirm),
            new("confirm-submit", HttpMethod.Post, "/api/timesheets/magic-links/confirm/submit", MagicLinkAllowedAction.Confirm),
            new("adjust-get", HttpMethod.Get, "/api/timesheets/magic-links/adjust", MagicLinkAllowedAction.Adjust),
            new("adjust-submit", HttpMethod.Post, "/api/timesheets/magic-links/adjust/submit", MagicLinkAllowedAction.Adjust)
        ];

    private static string Token(ExternalRoute route, string caseName) => $"{route.Name}-{caseName}";

    private static string ValidConfirmToken() => "valid-confirm-token";

    private static string ValidAdjustToken() => "valid-adjust-token";

    private static string Hash(string token) => new CryptographicMagicLinkTokenGenerator().DeriveHash(token).Value;

    private static AdjustTimeThroughMagicLink AdjustCommand()
        => new(
            new DateOnly(2026, 6, 20),
            75,
            ActivityId(),
            BillableState.NonBillable);

    private static ServerCapabilityState IssuedState(
        string token,
        MagicLinkAllowedAction allowedAction,
        TenantReference? tenant = null,
        PartyReference? contributor = null,
        DateTimeOffset? expiresAtUtc = null)
    {
        ServerCapabilityState state = new();
        state.Apply(new MagicLinkConfirmationCapabilityIssued(
            new MagicLinkCapabilityId($"capability-{token}"),
            tenant ?? Tenant(),
            contributor ?? Contributor(),
            TimeEntryTargetReference.ForProject(Project()),
            ActivityId(),
            TimeEntryId(),
            MagicLinkTargetKind.ProposedTimeEntry,
            allowedAction,
            new MagicLinkTokenHash(Hash(token)),
            expiresAtUtc ?? ObservedAtUtc.AddDays(1),
            Operator(),
            ObservedAtUtc.AddHours(-1),
            new MagicLinkAuditMetadata("timesheets", "issue-1"),
            true));
        return state;
    }

    private static TimeEntryState RecordedExternalState(
        PartyReference? contributor = null,
        TimeEntryId? timeEntryId = null,
        ActivityTypeScope activityTypeScope = ActivityTypeScope.Tenant)
    {
        TimeEntryState state = new();
        state.Apply(new TimeEntryRecorded(
            timeEntryId ?? TimeEntryId(),
            TimeEntryTargetReference.ForProject(Project()),
            contributor ?? Contributor(),
            ActivityId(),
            activityTypeScope,
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            TimeEntryApprovalState.Draft,
            ContributorCategory.ExternalContributor,
            null)
        {
            Comment = new TimeEntryComment("sensitive customer comment", TimeEntryCommentPolicy.SensitiveDefault),
            ExternalSource = new ExternalContributionSource("supplier-api", "request-1")
        });
        return state;
    }

    private static ActivityTypeCatalogReadModel FreshCatalog()
        => new(
            [
                new(
                    ActivityId(),
                    ActivityTypeScope.Tenant,
                    null,
                    "Delivery",
                    true,
                    BillableState.Billable)
            ],
            ProjectionFreshnessMetadata.Fresh);

    private static ActivityTypeCreated ActivityCreated(ActivityTypeId? activityTypeId = null)
        => new(
            activityTypeId ?? ActivityId(),
            ActivityTypeScope.Tenant,
            null,
            "Delivery",
            BillableState.Billable);

    private static ActivityTypeCatalogReadModel StaleCatalog()
        => new([], ProjectionFreshnessMetadata.Stale());

    // Mirrors the concrete loader's UnavailableTokenState(): no capability and no catalog.
    private static ActivityTypeCatalogReadModel UnavailableCatalog()
        => new([], ProjectionFreshnessMetadata.Unavailable());

    private static TenantReference Tenant() => new("tenant-1");

    private static TenantReference OtherTenant() => new("tenant-2");

    private static ProjectReference Project() => new("project-1");

    private static PartyReference Contributor() => new("party-1");

    private static PartyReference OtherContributor() => new("party-2");

    private static PartyReference UnauthorizedContributor() => new("party-unauthorized");

    private static PartyReference Operator() => new("operator-1");

    private static ActivityTypeId ActivityId() => new("activity-type-1");

    private static TimeEntryId TimeEntryId() => new("time-entry-1");

    private sealed record ExternalRoute(
        string Name,
        HttpMethod Method,
        string Path,
        MagicLinkAllowedAction Action);

    private sealed record CapturedFailure(
        string Name,
        HttpStatusCode StatusCode,
        string ContentType,
        string NormalizedBody,
        string[] Headers,
        string RawBody);

    private sealed class MagicLinkHttpBoundaryFactory(
        bool useConcreteLoader = false,
        int? internalPort = null,
        int? localPort = null,
        bool? useMismatchedClaims = null,
        Claim[]? claims = null,
        string? providerLoggingCategory = null,
        IMagicLinkTokenGenerator? tokenGenerator = null) : WebApplicationFactory<Program>
    {
        public ScriptedAccessGuard AccessGuard { get; } = new();

        public CapturingLoggerProvider Logs { get; } = new();

        public ProjectionBackedReadModelStore Store { get; } = new();

        public ScriptedEventStoreGateway Gateway { get; } = new();

        /// <summary>Gets the context observed from the actual host accessor during the latest request.</summary>
        public (TenantReference? Tenant, PartyReference? Actor, string? CorrelationId) ObservedTrustedContext { get; private set; }

        /// <summary>Gets or sets the simulated local port for subsequent requests to this host.</summary>
        public int? LocalPort { get; set; } = localPort;

        /// <summary>Gets the latest index dispatch using the host's admitted route fingerprint.</summary>
        public ProjectionDispatchRequest? LastIndexDispatch { get; private set; }

        public async Task ProjectValidStateAsync(
            HttpClient client,
            string token,
            MagicLinkAllowedAction action,
            MagicLinkCapabilityId capabilityId,
            TimeEntryId timeEntryId,
            bool deactivateActivityType = false)
        {
            MagicLinkConfirmationCapabilityIssued issued = new(
                capabilityId,
                Tenant(),
                Contributor(),
                TimeEntryTargetReference.ForProject(Project()),
                ActivityId(),
                timeEntryId,
                MagicLinkTargetKind.ProposedTimeEntry,
                action,
                new MagicLinkTokenHash(Hash(token)),
                ObservedAtUtc.AddDays(1),
                Operator(),
                ObservedAtUtc.AddHours(-1),
                new MagicLinkAuditMetadata("timesheets", "issue-1"),
                true);
            TimeEntryRecorded recorded = RecordedExternalState(timeEntryId: timeEntryId).IsRecorded
                ? new TimeEntryRecorded(
                    timeEntryId,
                    TimeEntryTargetReference.ForProject(Project()),
                    Contributor(),
                    ActivityId(),
                    ActivityTypeScope.Tenant,
                    new DateOnly(2026, 6, 19),
                    60,
                    BillableState.Billable,
                    TimeEntryApprovalState.Draft,
                    ContributorCategory.ExternalContributor,
                    null)
                : throw new InvalidOperationException();

            ProjectionEventDto issuedProjectionEvent = ProjectionEvent(1, issued);
            using IServiceScope scope = Services.CreateScope();
            DomainProjectionIdentityOptions projectionIdentity = scope.ServiceProvider
                .GetRequiredService<IOptions<DomainProjectionIdentityOptions>>()
                .Value;
            ProjectionDispatchRoute[] routes = scope.ServiceProvider
                .GetServices<IAsyncDomainProjectionHandler>()
                .Where(static handler => string.Equals(handler.Domain, "timesheets", StringComparison.Ordinal))
                .Select(static handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))
                .ToArray();
            string fingerprint = ProjectionRouteCatalogFingerprint.Compute(
                projectionIdentity.AppId,
                projectionIdentity.ServiceVersion,
                routes);
            await DispatchProjectionAsync(
                client,
                new ProjectionRequest(Tenant().TenantId, "timesheets", capabilityId.Value, [issuedProjectionEvent]),
                MagicLinkTokenHashCapabilityIndexProjection.ProjectionName,
                $"dispatch-{capabilityId.Value}",
                fingerprint);

            if (!Store.Contains(MagicLinkActivityTypeCatalogReadModelAddress.StateKey(Tenant())))
            {
                await DispatchProjectionAsync(
                    client,
                    new ProjectionRequest(
                        Tenant().TenantId,
                        "timesheets",
                        ActivityId().Value,
                        deactivateActivityType
                            ? [ProjectionEvent(1, ActivityCreated()), ProjectionEvent(2, new ActivityTypeDeactivated(ActivityId()))]
                            : [ProjectionEvent(1, ActivityCreated())]),
                    TenantActivityTypeCatalogProjection.ProjectionName,
                    "dispatch-catalog-live",
                    fingerprint);
                Store.Get<ActivityTypeCatalogReadModel>(
                        MagicLinkActivityTypeCatalogReadModelAddress.StateKey(Tenant()))
                    .ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
            }

            Gateway.WithStream(Tenant().TenantId, capabilityId.Value, StreamEvent(1, issued));
            Gateway.WithStream(Tenant().TenantId, timeEntryId.Value, StreamEvent(1, recorded));
        }

        public async Task ProjectCrossTenantCandidateAsync(
            HttpClient client,
            string token,
            MagicLinkAllowedAction action,
            MagicLinkCapabilityId capabilityId,
            TimeEntryId timeEntryId)
        {
            await ProjectValidStateAsync(client, token, action, capabilityId, timeEntryId);
            var mismatched = new MagicLinkConfirmationCapabilityIssued(
                capabilityId,
                OtherTenant(),
                Contributor(),
                TimeEntryTargetReference.ForProject(Project()),
                ActivityId(),
                timeEntryId,
                MagicLinkTargetKind.ProposedTimeEntry,
                action,
                new MagicLinkTokenHash(Hash(token)),
                ObservedAtUtc.AddDays(1),
                Operator(),
                ObservedAtUtc.AddHours(-1),
                new MagicLinkAuditMetadata("timesheets", "issue-1"),
                true);
            Gateway.WithStream(Tenant().TenantId, capabilityId.Value, StreamEvent(1, mismatched));
        }

        public async Task ProjectRejectedCatalogStateAsync(HttpClient client, string token, string scenario)
        {
            var capabilityId = new MagicLinkCapabilityId($"capability-{scenario}");
            var timeEntryId = new TimeEntryId($"time-entry-{scenario}");
            MagicLinkConfirmationCapabilityIssued issued = new(
                capabilityId,
                Tenant(),
                Contributor(),
                TimeEntryTargetReference.ForProject(Project()),
                ActivityId(),
                timeEntryId,
                MagicLinkTargetKind.ProposedTimeEntry,
                MagicLinkAllowedAction.Confirm,
                new MagicLinkTokenHash(Hash(token)),
                ObservedAtUtc.AddDays(1),
                Operator(),
                ObservedAtUtc.AddHours(-1),
                new MagicLinkAuditMetadata("timesheets", "issue-1"),
                true);
            var recorded = new TimeEntryRecorded(
                timeEntryId,
                TimeEntryTargetReference.ForProject(Project()),
                Contributor(),
                ActivityId(),
                ActivityTypeScope.Tenant,
                new DateOnly(2026, 6, 19),
                60,
                BillableState.Billable,
                TimeEntryApprovalState.Draft,
                ContributorCategory.ExternalContributor,
                null);
            ProjectionEventDto[] rejectedCatalogEvents = scenario switch
            {
                "missing-sequence" =>
                [
                    ProjectionEvent(1, ActivityCreated()),
                    ProjectionEvent(3, new ActivityTypeRenamed(ActivityId(), "Renamed"))
                ],
                "identity-conflict" =>
                [ProjectionEvent(1, ActivityCreated(new ActivityTypeId("activity-type-other")))],
                "unreadable" =>
                [
                    ProjectionEvent(1, ActivityCreated()),
                    ProjectionEvent(2, new ActivityTypeRenamed(ActivityId(), "Renamed")) with
                    {
                        Payload = "{"u8.ToArray()
                    }
                ],
                _ => throw new InvalidOperationException($"Unknown rejected catalog scenario '{scenario}'.")
            };

            using IServiceScope scope = Services.CreateScope();
            DomainProjectionIdentityOptions projectionIdentity = scope.ServiceProvider
                .GetRequiredService<IOptions<DomainProjectionIdentityOptions>>()
                .Value;
            ProjectionDispatchRoute[] routes = scope.ServiceProvider
                .GetServices<IAsyncDomainProjectionHandler>()
                .Where(static handler => string.Equals(handler.Domain, "timesheets", StringComparison.Ordinal))
                .Select(static handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))
                .ToArray();
            string fingerprint = ProjectionRouteCatalogFingerprint.Compute(
                projectionIdentity.AppId,
                projectionIdentity.ServiceVersion,
                routes);
            await DispatchProjectionAsync(
                client,
                new ProjectionRequest(
                    Tenant().TenantId,
                    "timesheets",
                    capabilityId.Value,
                    [ProjectionEvent(1, issued)]),
                MagicLinkTokenHashCapabilityIndexProjection.ProjectionName,
                $"dispatch-{capabilityId.Value}",
                fingerprint);
            await DispatchProjectionAsync(
                client,
                new ProjectionRequest(
                    Tenant().TenantId,
                    "timesheets",
                    ActivityId().Value,
                    rejectedCatalogEvents),
                TenantActivityTypeCatalogProjection.ProjectionName,
                $"dispatch-catalog-{scenario}",
                fingerprint,
                ProjectionDispatchStatus.Failed,
                ProjectionDispatchReasonCodes.DeliveryIdentityConflict);

            Gateway.WithStream(Tenant().TenantId, capabilityId.Value, StreamEvent(1, issued));
            Gateway.WithStream(Tenant().TenantId, timeEntryId.Value, StreamEvent(1, recorded));
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            if (providerLoggingCategory is not null)
            {
                string providerName = typeof(CapturingLoggerProvider).FullName!;
                builder.UseSetting($"Logging:{providerName}:LogLevel:{providerLoggingCategory}", "Information");
                builder.UseSetting($"Logging:{providerName}:LogLevel:Hexalith.Timesheets.Tests.LoggingProbe", "Debug");
            }

            if (internalPort is { } port)
            {
                builder.UseSetting("Timesheets:InternalSurface:Port", port.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddProvider(Logs);
            });

            builder.ConfigureServices(services =>
            {
                // Port-specific tests set Connection.LocalPort before the guard; other in-process
                // journeys explicitly open the projection route without claiming listener coverage.
                services.Configure<InternalSurfaceOptions>(options => options.AllowOnAnyPort = internalPort is null);
                if (!useConcreteLoader)
                {
                    services.RemoveAll<IMagicLinkConfirmationCapabilityStateLoader>();
                    services.AddScoped<IMagicLinkConfirmationCapabilityStateLoader, ScriptedMagicLinkStateLoader>();
                }

                services.RemoveAll<ITimesheetsAccessGuard>();
                services.AddSingleton<ITimesheetsAccessGuard>(AccessGuard);

                if (tokenGenerator is not null)
                {
                    services.RemoveAll<IMagicLinkTokenGenerator>();
                    services.AddSingleton<IMagicLinkTokenGenerator>(tokenGenerator);
                }

                services.RemoveAll<IReadModelStore>();
                services.RemoveAll<DaprReadModelStore>();
                services.AddSingleton<IReadModelStore>(useConcreteLoader ? Store : new UnavailableReadModelStore());

                if (useConcreteLoader)
                {
                    services.RemoveAll<IEventStoreGatewayClient>();
                    services.AddSingleton<IEventStoreGatewayClient>(Gateway);
                }

                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new StaticTimeProvider(ObservedAtUtc));

                services.AddSingleton<IStartupFilter>(new ClaimsStartupFilter(
                    useMismatchedClaims ?? useConcreteLoader,
                    () => LocalPort,
                    accessor => ObservedTrustedContext = (accessor.CurrentTenant, accessor.CurrentActor, accessor.CurrentCorrelationId),
                    claims));
            });
        }

        private async Task<ProjectionDispatchOutcome> DispatchProjectionAsync(
            HttpClient client,
            ProjectionRequest request,
            string projectionType,
            string dispatchId,
            string fingerprint,
            ProjectionDispatchStatus expectedStatus = ProjectionDispatchStatus.Completed,
            string? expectedReasonCode = null)
        {
            var dispatch = new ProjectionDispatchRequest(request, [projectionType], dispatchId, fingerprint);
            if (projectionType == MagicLinkTokenHashCapabilityIndexProjection.ProjectionName)
            {
                LastIndexDispatch = dispatch;
            }
            using HttpResponseMessage projectionResponse = await client.PostAsJsonAsync(
                "/project/v2",
                dispatch,
                JsonOptions,
                TestContext.Current.CancellationToken);
            projectionResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            ProjectionDispatchOutcome outcome = (await projectionResponse.Content
                    .ReadFromJsonAsync<ProjectionDispatchResponse>(JsonOptions, TestContext.Current.CancellationToken))
                .ShouldNotBeNull()
                .Outcomes
                .ShouldHaveSingleItem();
            outcome.ProjectionType.ShouldBe(projectionType);
            outcome.Status.ShouldBe(expectedStatus);
            outcome.ReasonCode.ShouldBe(expectedReasonCode);
            return outcome;
        }

        private static ProjectionEventDto ProjectionEvent(long sequence, object payload)
            => new(
                payload.GetType().FullName!,
                JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions),
                "json",
                sequence,
                ObservedAtUtc,
                "correlation-1",
                $"projection-{sequence}",
                Operator().PartyId,
                sequence);

        private static StreamReadEvent StreamEvent(long sequence, object payload)
            => new(
                sequence,
                payload.GetType().FullName!,
                JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions),
                "json",
                1,
                $"stream-{sequence}",
                "correlation-1",
                null,
                ObservedAtUtc,
                Operator().PartyId);
    }

    private sealed class ClaimsStartupFilter(
        bool useMismatchedClaims,
        Func<int?> localPort,
        Action<ITimesheetsTrustedContextAccessor> captureContext,
        Claim[]? claims) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            => app =>
            {
                app.Use(async (context, following) =>
                {
                    if (localPort() is { } port)
                    {
                        context.Connection.LocalPort = port;
                    }

                    context.User = new ClaimsPrincipal(new ClaimsIdentity(claims ??
                    [
                        new Claim("tenant_id", useMismatchedClaims ? OtherTenant().TenantId : Tenant().TenantId),
                        new Claim("party_id", useMismatchedClaims ? OtherContributor().PartyId : Contributor().PartyId),
                        new Claim(
                            ClaimTypes.NameIdentifier,
                            useMismatchedClaims ? OtherContributor().PartyId : Contributor().PartyId)
                    ], "TestAuth"));

                    captureContext(context.RequestServices.GetRequiredService<ITimesheetsTrustedContextAccessor>());

                    await following(context).ConfigureAwait(false);
                });
                next(app);
            };
    }

    private sealed class ScriptedMagicLinkStateLoader : IMagicLinkConfirmationCapabilityStateLoader
    {
        public ValueTask<ActivityTypeCatalogReadModel> LoadActivityTypeCatalogAsync(CancellationToken cancellationToken)
            => ValueTask.FromResult(FreshCatalog());

        public ValueTask<ServerCapabilityState?> LoadCapabilityAsync(
            MagicLinkCapabilityId capabilityId,
            CancellationToken cancellationToken)
            => ValueTask.FromResult<ServerCapabilityState?>(null);

        public ValueTask<MagicLinkEndpointTokenState> LoadTokenStateAsync(
            string oneTimeToken,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(StateFor(oneTimeToken));

        private static MagicLinkEndpointTokenState StateFor(string token)
        {
            if (token == ValidConfirmToken())
            {
                return new(IssuedState(token, MagicLinkAllowedAction.Confirm), RecordedExternalState(), FreshCatalog());
            }

            if (token == ValidAdjustToken())
            {
                return new(IssuedState(token, MagicLinkAllowedAction.Adjust), RecordedExternalState(), FreshCatalog());
            }

            ExternalRoute route = ExternalRoutes().Single(candidate => token.StartsWith($"{candidate.Name}-", StringComparison.Ordinal));
            string caseName = token[(route.Name.Length + 1)..];
            MagicLinkAllowedAction action = caseName == "wrong-action"
                ? route.Action == MagicLinkAllowedAction.Confirm ? MagicLinkAllowedAction.Adjust : MagicLinkAllowedAction.Confirm
                : route.Action;

            ServerCapabilityState? state = caseName switch
            {
                "malformed" or "unknown" or "unresolved" => null,
                "expired" => IssuedState(token, action, expiresAtUtc: ObservedAtUtc),
                // The concrete loader rejects a candidate/capability tenant mismatch before a
                // capability state can reach the HTTP boundary.
                "cross-tenant" => null,
                "wrong-recipient" => IssuedState(token, action, contributor: OtherContributor()),
                "unauthorized" => IssuedState(token, action, contributor: UnauthorizedContributor()),
                _ => IssuedState(token, action)
            };

            if (state is not null && caseName is "used" or "repeated-token")
            {
                state.Apply(new MagicLinkConfirmationCapabilityUsed(
                    state.CapabilityId!,
                    state.Tenant!,
                    state.Contributor!,
                    state.TimeEntryId!,
                    ObservedAtUtc.AddMinutes(-1),
                    new MagicLinkAuditMetadata("magic-link", state.CapabilityId!.Value)));
            }

            if (state is not null && caseName == "revoked")
            {
                state.Apply(new MagicLinkConfirmationCapabilityRevoked(
                    state.CapabilityId!,
                    state.Tenant!,
                    Operator(),
                    ObservedAtUtc.AddMinutes(-1),
                    new MagicLinkAuditMetadata("timesheets", "revoke-1")));
            }

            return new(
                state,
                caseName switch
                {
                    "wrong-recipient" => RecordedExternalState(),
                    "project-owned" => RecordedExternalState(state?.Contributor, activityTypeScope: ActivityTypeScope.Project),
                    _ => RecordedExternalState(state?.Contributor)
                },
                caseName switch
                {
                    "stale-catalog" => StaleCatalog(),
                    "unresolved" => UnavailableCatalog(),
                    _ => FreshCatalog()
                });
        }
    }

    private sealed class ScriptedAccessGuard : ITimesheetsAccessGuard
    {
        private int _authorizationCount;
        private int _trustedWorkExecutionCount;

        public int AuthorizationCount => Volatile.Read(ref _authorizationCount);

        public int TrustedWorkExecutionCount => Volatile.Read(ref _trustedWorkExecutionCount);

        /// <summary>Gets the last request passed to the service's authorization boundary.</summary>
        public TimesheetsAuthorizationRequest? LastAuthorizationRequest { get; private set; }

        public ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
            TimesheetsAuthorizationRequest request,
            CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref _authorizationCount);
            LastAuthorizationRequest = request;
            return ValueTask.FromResult(Decision(request));
        }

        public async ValueTask<TimesheetsAuthorizationDecision> ExecuteIfAuthorizedAsync(
            TimesheetsAuthorizationRequest request,
            Func<CancellationToken, ValueTask> trustedWork,
            CancellationToken cancellationToken)
        {
            TimesheetsAuthorizationDecision decision = Decision(request);
            if (decision.IsAuthorized)
            {
                _ = Interlocked.Increment(ref _trustedWorkExecutionCount);
                await trustedWork(cancellationToken).ConfigureAwait(false);
            }

            return decision;
        }

        public ValueTask<TimesheetsUiActionPolicyOutcome> EvaluateUiActionAsync(
            TimesheetsAuthorizationRequest request,
            TimesheetsUiActionVisibility deniedVisibility,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(TimesheetsUiActionPolicyOutcome.FromDecision(
                request.UiAction ?? TimesheetsUiAction.Capture,
                Decision(request),
                deniedVisibility));

        private static TimesheetsAuthorizationDecision Decision(TimesheetsAuthorizationRequest request)
            => request.Contributor == UnauthorizedContributor()
                ? TimesheetsAuthorizationDecision.Denied(TimesheetsDenialCategory.UnconfiguredPolicy, "Denied by test access guard.")
                : TimesheetsAuthorizationDecision.Allowed();
    }

    private sealed class StaticTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class ProjectionBackedReadModelStore : IReadModelStore
    {
        private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _versions = new(StringComparer.Ordinal);

        public int DirectCatalogSeedCount { get; private set; }

        public int DirectIndexSeedCount { get; private set; }

        /// <summary>Gets the read-model keys requested through the loader's store seam.</summary>
        public List<string> ReadKeys { get; } = [];

        public bool Contains(string key) => _values.ContainsKey(key);

        public T Get<T>(string key)
            where T : class
            => (T)_values[key];

        public Task<ReadModelEntry<TValue>> GetAsync<TValue>(
            string storeName,
            string key,
            CancellationToken cancellationToken = default)
            where TValue : class
        {
            ReadKeys.Add(key);
            return Task.FromResult(new ReadModelEntry<TValue>(
                _values.TryGetValue(key, out object? value) ? value as TValue : null,
                _versions.TryGetValue(key, out int version)
                    ? version.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : null));
        }

        // Direct, unconditional seeding is refused outright rather than merely counted. The counters
        // alone could never fail: every production write reaches this store through TrySaveAsync, so
        // a ShouldBe(0) assertion on them passed by construction and would not have caught the direct
        // seed it was added to catch. Throwing makes the journey prove itself through the projection
        // route, because a fixture that tried to shortcut it would fail loudly here.
        public Task SaveAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            CancellationToken cancellationToken = default)
            where TValue : class
        {
            if (typeof(TValue) == typeof(MagicLinkTokenHashCapabilityIndexReadModel))
            {
                DirectIndexSeedCount++;
                throw new InvalidOperationException(
                    "The magic-link candidate index must be established through the projection route, not seeded directly.");
            }

            if (typeof(TValue) == typeof(ActivityTypeCatalogReadModel))
            {
                DirectCatalogSeedCount++;
                throw new InvalidOperationException(
                    "The Activity Type catalog must be established through the projection route, not seeded directly.");
            }

            _values[key] = value;
            _versions[key] = _versions.GetValueOrDefault(key) + 1;
            return Task.CompletedTask;
        }

        public Task<bool> TrySaveAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            string etag,
            CancellationToken cancellationToken = default)
            where TValue : class
        {
            string currentEtag = _versions.TryGetValue(key, out int version)
                ? version.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : string.Empty;
            if (!string.Equals(currentEtag, etag, StringComparison.Ordinal))
            {
                return Task.FromResult(false);
            }

            _values[key] = value;
            _versions[key] = version + 1;
            return Task.FromResult(true);
        }
    }

    private sealed class ScriptedEventStoreGateway : IEventStoreGatewayClient
    {

        // The magic-link loader never queries command status; this member exists only to satisfy
        // IEventStoreGatewayClient.
        public Task<CommandStatusQueryResponse?> GetCommandStatusAsync(
            string messageId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        private readonly Dictionary<(string Tenant, string Aggregate), StreamReadEvent[]> _streams = [];

        public List<StreamReadRequest> Requests { get; } = [];

        public void WithStream(string tenant, string aggregate, params StreamReadEvent[] events)
            => _streams[(tenant, aggregate)] = events;

        public Task<SubmitCommandResponse> SubmitCommandAsync(
            SubmitCommandRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<EventStoreQueryResult> SubmitQueryAsync(
            SubmitQueryRequest request,
            string? ifNoneMatch = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<EventStoreQueryResult<T>> SubmitQueryAsync<T>(
            SubmitQueryRequest request,
            string? ifNoneMatch = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<StreamReadPage> ReadStreamAsync(
            StreamReadRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            StreamReadEvent[] all = request.AggregateId is not null
                && _streams.TryGetValue((request.Tenant, request.AggregateId), out StreamReadEvent[]? stream)
                    ? stream
                    : [];
            StreamReadEvent[] page = all
                .Where(item => item.SequenceNumber > request.FromSequence)
                .OrderBy(static item => item.SequenceNumber)
                .Take(request.PageSize)
                .ToArray();
            long latest = all.Select(static item => item.SequenceNumber).DefaultIfEmpty(0).Max();
            long? last = page.Length == 0 ? null : page[^1].SequenceNumber;
            return Task.FromResult(new StreamReadPage(
                request.Tenant,
                request.Domain,
                request.AggregateId,
                page,
                new StreamReadMetadata(
                    request.FromSequence,
                    request.ToSequence,
                    last,
                    latest,
                    page.Length,
                    last is not null && last < latest,
                    null)));
        }
    }

    private sealed class UnavailableReadModelStore : IReadModelStore
    {
        public Task<ReadModelEntry<TValue>> GetAsync<TValue>(
            string storeName,
            string key,
            CancellationToken cancellationToken = default)
            where TValue : class
            => Task.FromResult(new ReadModelEntry<TValue>(null, null));

        public Task SaveAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            CancellationToken cancellationToken = default)
            where TValue : class
            => Task.CompletedTask;

        public Task<bool> TrySaveAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            string etag,
            CancellationToken cancellationToken = default)
            where TValue : class
            => Task.FromResult(true);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly object _sync = new();
        private readonly List<LogRecord> _records = [];

        public IReadOnlyList<LogRecord> Records
        {
            get
            {
                lock (_sync)
                {
                    return [.. _records];
                }
            }
        }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _records, _sync);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(
        string category,
        List<LogRecord> records,
        object sync) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            string[] structuredState = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.Select(static value => $"{value.Key}={value.Value}").ToArray()
                : [];

            lock (sync)
            {
                records.Add(new LogRecord(category, formatter(state, exception), structuredState));
            }
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }

    private sealed record LogRecord(string Category, string Message, string[] State);
}
