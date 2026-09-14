using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
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
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.MagicLinks;
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
        string token = Token(ExternalRoutes()[0], "unknown");

        using HttpResponseMessage response = await client
            .GetAsync($"/api/timesheets/magic-links/confirm?t={token}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        LogRecord[] records = factory.Logs.Records
            .Where(static record => record.Category.StartsWith("Hexalith.Timesheets", StringComparison.Ordinal))
            .ToArray();

        records.ShouldNotBeEmpty();
        string.Join(' ', records.Select(static record => string.Join(' ', record.State))).ShouldContain("Category=Unknown");

        foreach (LogRecord record in records)
        {
            string rendered = $"{record.Category} {record.Message} {string.Join(' ', record.State)}";
            AssertSensitiveMaterialAbsent(rendered, token);
        }
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

        LogRecord[] records = factory.Logs.Records
            .Where(static record => record.Category.StartsWith("Hexalith.Timesheets", StringComparison.Ordinal))
            .ToArray();

        // The blank-token boundary records the distinct Malformed outcome category (vs Unknown for a resolved
        // but-rejected token) while staying privacy-safe — proving every emitted category is internal-only.
        records.ShouldNotBeEmpty();
        string.Join(' ', records.Select(static record => string.Join(' ', record.State))).ShouldContain("Category=Malformed");

        foreach (LogRecord record in records)
        {
            string rendered = $"{record.Category} {record.Message} {string.Join(' ', record.State)}";
            AssertSensitiveMaterialAbsent(rendered, string.Empty);
        }
    }

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

    private static void AssertSensitiveMaterialAbsent(string content, string token)
    {
        string[] forbiddenTerms =
        [
            token,
            string.IsNullOrWhiteSpace(token) ? string.Empty : Hash(token),
            "comment",
            "token",
            "party-1",
            "party-2",
            "project-1",
            "work-1",
            "time-entry-1",
            "time-entry-2",
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

    private static TimeEntryState RecordedExternalState(PartyReference? contributor = null, TimeEntryId? timeEntryId = null)
    {
        TimeEntryState state = new();
        state.Apply(new TimeEntryRecorded(
            timeEntryId ?? TimeEntryId(),
            TimeEntryTargetReference.ForProject(Project()),
            contributor ?? Contributor(),
            ActivityId(),
            ActivityTypeScope.Tenant,
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

    private sealed class MagicLinkHttpBoundaryFactory(bool useConcreteLoader = false) : WebApplicationFactory<Program>
    {
        public ScriptedAccessGuard AccessGuard { get; } = new();

        public CapturingLoggerProvider Logs { get; } = new();

        public ProjectionBackedReadModelStore Store { get; } = new();

        public ScriptedEventStoreGateway Gateway { get; } = new();

        public async Task ProjectValidStateAsync(
            HttpClient client,
            string token,
            MagicLinkAllowedAction action,
            MagicLinkCapabilityId capabilityId,
            TimeEntryId timeEntryId)
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
                        [ProjectionEvent(1, ActivityCreated())]),
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
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddProvider(Logs);
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMagicLinkConfirmationCapabilityStateLoader>();
                if (useConcreteLoader)
                {
                    services.AddScoped<IMagicLinkConfirmationCapabilityStateLoader, EventStoreMagicLinkConfirmationCapabilityStateLoader>();
                }
                else
                {
                    services.AddScoped<IMagicLinkConfirmationCapabilityStateLoader, ScriptedMagicLinkStateLoader>();
                }

                services.RemoveAll<ITimesheetsAccessGuard>();
                services.AddSingleton<ITimesheetsAccessGuard>(AccessGuard);

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

                services.AddSingleton<IStartupFilter>(new ClaimsStartupFilter(useConcreteLoader));
            });
        }

        private static async Task<ProjectionDispatchOutcome> DispatchProjectionAsync(
            HttpClient client,
            ProjectionRequest request,
            string projectionType,
            string dispatchId,
            string fingerprint,
            ProjectionDispatchStatus expectedStatus = ProjectionDispatchStatus.Completed,
            string? expectedReasonCode = null)
        {
            var dispatch = new ProjectionDispatchRequest(request, [projectionType], dispatchId, fingerprint);
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

    private sealed class ClaimsStartupFilter(bool useMismatchedClaims) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            => app =>
            {
                app.Use(async (context, following) =>
                {
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("tenant_id", useMismatchedClaims ? OtherTenant().TenantId : Tenant().TenantId),
                        new Claim("party_id", useMismatchedClaims ? OtherContributor().PartyId : Contributor().PartyId),
                        new Claim(
                            ClaimTypes.NameIdentifier,
                            useMismatchedClaims ? OtherContributor().PartyId : Contributor().PartyId)
                    ], "TestAuth"));

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
                "malformed" or "unknown" => null,
                "stale-catalog" when route.Name == "confirm-submit" => null,
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
                caseName == "wrong-recipient" ? RecordedExternalState() : RecordedExternalState(state?.Contributor),
                caseName == "stale-catalog" ? StaleCatalog() : FreshCatalog());
        }
    }

    private sealed class ScriptedAccessGuard : ITimesheetsAccessGuard
    {
        private int _authorizationCount;

        public int AuthorizationCount => Volatile.Read(ref _authorizationCount);

        public ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
            TimesheetsAuthorizationRequest request,
            CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref _authorizationCount);
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

        public bool Contains(string key) => _values.ContainsKey(key);

        public T Get<T>(string key)
            where T : class
            => (T)_values[key];

        public Task<ReadModelEntry<TValue>> GetAsync<TValue>(
            string storeName,
            string key,
            CancellationToken cancellationToken = default)
            where TValue : class
            => Task.FromResult(new ReadModelEntry<TValue>(
                _values.TryGetValue(key, out object? value) ? value as TValue : null,
                _versions.TryGetValue(key, out int version)
                    ? version.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : null));

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
            }
            else if (typeof(TValue) == typeof(ActivityTypeCatalogReadModel))
            {
                DirectCatalogSeedCount++;
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
        private readonly Dictionary<(string Tenant, string Aggregate), StreamReadEvent[]> _streams = [];

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
