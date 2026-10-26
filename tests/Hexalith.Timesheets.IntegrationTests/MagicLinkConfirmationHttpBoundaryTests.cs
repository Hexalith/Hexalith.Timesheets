using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.Testing.Fakes;
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
using Hexalith.Timesheets.ReadModels.ActivityTypes;
using Hexalith.Timesheets.Projections.MagicLinks;
using Hexalith.Timesheets.Projections.TimeEntries;
using Hexalith.Timesheets.Runtime;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks.Commands;
using Hexalith.Timesheets.Server.Policies;
using Hexalith.Timesheets.Server.Runtime;
using Hexalith.Timesheets.Server.TimeEntries;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using ServerCapabilityState = Hexalith.Timesheets.Server.MagicLinks.MagicLinkCapabilityState;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed partial class MagicLinkConfirmationHttpBoundaryTests
{
    private static readonly DateTimeOffset ObservedAtUtc = new(2026, 6, 19, 13, 30, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("contributor")]
    [InlineData("target")]
    [InlineData("activity-type")]
    [InlineData("activity-scope")]
    public async Task CorrectedScopeChangesRefuseTheOldConfirmationLinkWithoutChangingHistory(string changedScope)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId($"capability-corrected-scope-{changedScope}");
        var entryId = new TimeEntryId($"entry-corrected-scope-{changedScope}");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm, capabilityId, entryId);
        StreamReadEvent seed = factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value).Single();
        var recorded = JsonSerializer.Deserialize<TimeEntryRecorded>(seed.Payload, JsonOptions)!;
        object[] history = ConfirmationLifecycleHistory(recorded, approved: true, corrected: true);
        var correction = (TimeEntryApprovedCorrected)history[^1];
        TimeEntryCorrectionValues values = correction.CorrectedValues;
        values = changedScope switch
        {
            "contributor" => values with { Contributor = new("corrected-party") },
            "target" => values with { Target = TimeEntryTargetReference.ForProject(new("corrected-project")) },
            "activity-type" => values with { ActivityTypeId = new("corrected-activity") },
            "activity-scope" => values with { ActivityTypeScope = ActivityTypeScope.Project },
            _ => throw new InvalidOperationException()
        };
        history[^1] = correction with { CorrectedValues = values };
        DomainStateReplay.Rehydrate<TimeEntryState>(history).ShouldNotBeNull().CorrectionState
            .ShouldBe(TimeEntryCorrectionState.Corrected);
        factory.Gateway.WithStream(Tenant().TenantId, entryId.Value, history.Select((item, index) => seed with
        {
            SequenceNumber = index + 1,
            MessageId = $"corrected-scope-event-{index + 1}",
            EventTypeName = item.GetType().FullName!,
            Payload = JsonSerializer.SerializeToUtf8Bytes(item, JsonOptions)
        }).ToArray());
        StreamReadEvent[] before = factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value);

        using HttpResponseMessage display = await client.GetAsync(
            $"/api/timesheets/magic-links/confirm?t={ValidConfirmToken()}", TestContext.Current.CancellationToken);
        using HttpResponseMessage submit = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}", new ConfirmTimeThroughMagicLink(),
            TestContext.Current.CancellationToken);
        using HttpResponseMessage unknown = await client.GetAsync(
            "/api/timesheets/magic-links/confirm?t=unknown-corrected-scope", TestContext.Current.CancellationToken);
        CapturedFailure baseline = await CaptureFailureAsync(unknown, "unknown", "unknown-corrected-scope");
        foreach (HttpResponseMessage response in new[] { display, submit })
        {
            CapturedFailure denial = await CaptureFailureAsync(response, changedScope, ValidConfirmToken());
            denial.NormalizedBody.ShouldBe(baseline.NormalizedBody);
            denial.Headers.ShouldBe(baseline.Headers);
        }
        factory.Gateway.SubmissionCount.ShouldBe(0);
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value).ShouldBe(before);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ConfirmationAfterApprovalOrCorrectionPreservesLifecycleAndPersistedEvidence(bool approved, bool corrected)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capabilityId = new MagicLinkCapabilityId($"capability-lifecycle-{approved}-{corrected}");
        var entryId = new TimeEntryId($"entry-lifecycle-{approved}-{corrected}");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            capabilityId, entryId, deactivateActivityType: true);
        StreamReadEvent seed = factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value).Single();
        TimeEntryRecorded recorded = JsonSerializer.Deserialize<TimeEntryRecorded>(seed.Payload, JsonOptions)!;
        object[] history = ConfirmationLifecycleHistory(recorded, approved, corrected);
        factory.Gateway.WithStream(Tenant().TenantId, entryId.Value, history.Select((item, index) => seed with
        {
            SequenceNumber = index + 1,
            MessageId = $"lifecycle-event-{index + 1}",
            EventTypeName = item.GetType().FullName!,
            Payload = JsonSerializer.SerializeToUtf8Bytes(item, JsonOptions)
        }).ToArray());
        TimeEntryState before = DomainStateReplay.Rehydrate<TimeEntryState>(history)!;

        using HttpResponseMessage display = await client.GetAsync(
            $"/api/timesheets/magic-links/confirm?t={ValidConfirmToken()}", TestContext.Current.CancellationToken);
        using HttpResponseMessage submit = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);

        display.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await display.Content.ReadFromJsonAsync<MagicLinkConfirmationDisplayResponse>(JsonOptions,
            TestContext.Current.CancellationToken)).ShouldNotBeNull().DurationMinutes.ShouldBe(corrected ? 75 : 60);
        submit.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        StreamReadEvent[] stored = factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value);
        stored.Length.ShouldBe(history.Length + 2);
        StoredMagicLinkUsed used = JsonSerializer.Deserialize<StoredMagicLinkUsed>(stored[^2].Payload, JsonOptions)!;
        StoredTimeEntryConfirmed confirmed = JsonSerializer.Deserialize<StoredTimeEntryConfirmed>(stored[^1].Payload, JsonOptions)!;
        object[] confirmedHistory = [.. history, used, confirmed];
        TimeEntryState after = DomainStateReplay.Rehydrate<TimeEntryState>(confirmedHistory)!;
        after.ApprovalState.ShouldBe(before.ApprovalState);
        after.CorrectionState.ShouldBe(before.CorrectionState);
        after.LockState.ShouldBe(before.LockState);
        after.CorrectedValues.ShouldBe(before.CorrectedValues);
        after.TimeEntryApprovalDecisionId.ShouldBe(before.TimeEntryApprovalDecisionId);
        after.TimeEntryCorrectionId.ShouldBe(before.TimeEntryCorrectionId);
        after.ConfirmedContributor.ShouldBe(Contributor());
        after.ContributorConfirmationSource.ShouldBe(new("magic-link", capabilityId.Value));
        using HttpResponseMessage replay = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);
        replay.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.Gateway.StoredEvents(Tenant().TenantId, entryId.Value).Length.ShouldBe(stored.Length);
    }

    private static object[] ConfirmationLifecycleHistory(TimeEntryRecorded recorded, bool approved, bool corrected)
    {
        var decisionId = new TimeEntryApprovalDecisionId("lifecycle-decision");
        var authority = new ApprovalAuthoritySourceAttribution(ApprovalAuthorityAction.EntryApproval,
            ApprovalAuthoritySource.TenantAdministrator, ApprovalAuthorityDecisionState.Allowed,
            "policy-1", "1", ProjectionFreshnessMetadata.Fresh);
        List<object> history = [recorded, new TimeEntrySubmitted(recorded.TimeEntryId, Contributor(), Tenant(),
            ObservedAtUtc.AddMinutes(-30), new("lifecycle-submission"), TimeEntrySubmissionScope.SelectedEntries,
            TimeEntryApprovalState.Submitted)];
        history.Add(approved
            ? new TimeEntryApproved(recorded.TimeEntryId, Operator(), Tenant(), ObservedAtUtc.AddMinutes(-20),
                decisionId, TimeEntryApprovalState.Approved, authority, TimeEntryApprovalScope.IndividualEntry)
            : new TimeEntryRejected(recorded.TimeEntryId, Operator(), Tenant(), ObservedAtUtc.AddMinutes(-20),
                decisionId, TimeEntryApprovalState.Rejected, authority, TimeEntryApprovalScope.IndividualEntry, new("Correction required")));
        if (corrected)
        {
            var previous = new TimeEntryCorrectionValues(recorded.Target, recorded.Contributor, recorded.ActivityTypeId,
                recorded.ServiceDate, recorded.DurationMinutes, recorded.BillableState, recorded.ContributorCategory, null)
            { ActivityTypeScope = recorded.ActivityTypeScope };
            TimeEntryCorrectionValues values = previous with { DurationMinutes = 75 };
            history.Add(approved
                ? new TimeEntryApprovedCorrected(recorded.TimeEntryId, new("lifecycle-correction"), Tenant(), Operator(),
                    ObservedAtUtc.AddMinutes(-10), previous, values, new("Corrected duration"), decisionId,
                    TimeEntryApprovalScope.IndividualEntry, TimeEntryApprovalState.Approved, TimeEntryCorrectionState.Corrected)
                : new TimeEntryCorrected(recorded.TimeEntryId, new("lifecycle-correction"), Tenant(), Operator(),
                    ObservedAtUtc.AddMinutes(-10), previous, values, new("Correction required"), decisionId,
                    TimeEntryApprovalState.Draft, TimeEntryCorrectionState.Corrected));
        }
        return [.. history];
    }

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
            ? () => AssertSensitiveResponseMaterialAbsent(message, token)
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
            "comment", "token", "Delivery", "durationMinutes", "Draft", "RecoveryPath",
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
            // Pin both literal and case-insensitive response exclusions without increasing the case count.
            yield return ["ResponseBody", tenant == "tenant-2" ? tenant.ToUpperInvariant() : tenant, Array.Empty<string>(), ProbeToken, true, true];
            foreach (string category in new[] { "Hexalith.Timesheets.Tests", "Microsoft.Hosting.Lifetime" })
            {
                yield return [$"{category}.{tenant}", "safe", Array.Empty<string>(), ProbeToken, false, false];
                yield return [category, tenant, Array.Empty<string>(), ProbeToken, false, false];
                yield return [category, "safe", new[] { $"StateKey={tenant}" }, ProbeToken, false, false];
            }
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

    [Theory]
    [InlineData("confirm-get")]
    [InlineData("adjust-get")]
    [InlineData("confirm-submit")]
    [InlineData("adjust-submit")]
    public async Task PublicTokenResponsesDisableCachingAndReferrers(string routeName)
    {
        using MagicLinkHttpBoundaryFactory factory = new();
        using HttpClient client = factory.CreateClient();
        ExternalRoute route = ExternalRoutes().Single(candidate => candidate.Name == routeName);
        using HttpResponseMessage response = await SendAsync(client, route, "%20%20");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        AssertPrivateLinkHeaders(response);
    }

    [Fact]
    public async Task InvalidMagicLinkHttpBoundaryResponsesAreEquivalentAndOpaque()
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

                AssertPrivateLinkHeaders(response);
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
    public async Task ValidMagicLinkGetDisplaysAndPostDenialsMatchInvalidDenials()
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
        adjustDisplay.StatusCode.ShouldBe(HttpStatusCode.OK);
        CapturedFailure confirmDenial = await CaptureFailureAsync(confirmSubmit, "valid-confirm-submit", ValidConfirmToken());
        CapturedFailure adjustDenial = await CaptureFailureAsync(adjustSubmit, "valid-adjust-submit", ValidAdjustToken());
        using HttpResponseMessage invalidResponse = await client.PostAsJsonAsync(
            "/api/timesheets/magic-links/confirm/submit?t=confirm-submit-unknown",
            new ConfirmTimeThroughMagicLink(),
            TestContext.Current.CancellationToken);
        CapturedFailure invalidDenial = await CaptureFailureAsync(invalidResponse, "unknown-confirm-submit", "confirm-submit-unknown");
        foreach (CapturedFailure denial in new[] { confirmDenial, adjustDenial })
        {
            denial.ContentType.ShouldBe(invalidDenial.ContentType);
            denial.NormalizedBody.ShouldBe(invalidDenial.NormalizedBody);
            denial.Headers.ShouldBe(invalidDenial.Headers);
            RawByteLength(denial.RawBody).ShouldBe(RawByteLength(invalidDenial.RawBody));
        }
        factory.Gateway.SubmissionCount.ShouldBe(2);

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
    public async Task ConcreteLoaderCommitsBothActionsAfterProjectionDelivery()
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
        adjustDisplay.StatusCode.ShouldBe(HttpStatusCode.OK);
        confirmSubmit.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        adjustSubmit.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        foreach (HttpResponseMessage response in new[] { confirmDisplay, adjustDisplay, confirmSubmit, adjustSubmit })
        {
            AssertPrivateLinkHeaders(response);
        }
        factory.Gateway.SubmissionCount.ShouldBe(2);
        using HttpResponseMessage confirmAfterPost = await client.GetAsync(
            $"/api/timesheets/magic-links/confirm?t={ValidConfirmToken()}", TestContext.Current.CancellationToken);
        using HttpResponseMessage adjustAfterPost = await client.GetAsync(
            $"/api/timesheets/magic-links/adjust?t={ValidAdjustToken()}", TestContext.Current.CancellationToken);
        confirmAfterPost.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        adjustAfterPost.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.Store.DirectIndexSeedCount.ShouldBe(0);
        factory.Store.DirectCatalogSeedCount.ShouldBe(0);
        factory.Store.GetSelectedIndex().Entries.Count.ShouldBe(2);
        factory.Store.GetSelectedCatalog(Tenant())
            .ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NamedProjectRestrictionDeliveryControlsPublicSelectionAndPreservesHistoricalConfirmation(bool allowsType)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, useMismatchedClaims: false);
        using HttpClient client = factory.CreateClient();
        var confirmationOwner = new TimeEntryId("entry-restriction-confirm");
        var adjustmentOwner = new TimeEntryId("entry-restriction-adjust");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            new("capability-restriction-confirm"), confirmationOwner);
        await factory.ProjectValidStateAsync(client, ValidAdjustToken(), MagicLinkAllowedAction.Adjust,
            new("capability-restriction-adjust"), adjustmentOwner);
        string beforeForeignDelivery = JsonSerializer.Serialize(factory.Store.GetSelectedCatalog(Tenant()), JsonOptions);
        await factory.DeliverProjectRestrictionAsync(client, OtherTenant(), []);
        JsonSerializer.Serialize(factory.Store.GetSelectedCatalog(Tenant()), JsonOptions).ShouldBe(beforeForeignDelivery);
        using HttpResponseMessage allowedAfterForeign = await factory.PostProtectedManagementAsync(client,
            "/api/timesheets/magic-links/confirmation-capabilities", Issue("after-foreign-policy"));
        allowedAfterForeign.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        await factory.DeliverProjectRestrictionAsync(client, Tenant(), allowsType ? [ActivityId()] : []);
        ActivityTypeCatalogReadModel catalog = factory.Store.GetSelectedCatalog(Tenant());
        catalog.Items.ShouldHaveSingleItem().IsActive.ShouldBeTrue();
        catalog.ProjectPolicy.ShouldNotBeNull().Tenant.ShouldBe(Tenant());
        catalog.ProjectPolicy.IsComplete.ShouldBeTrue();
        ProjectActivityTypeCatalogRestrictionSnapshot restriction = catalog.ProjectPolicy.Restrictions.ShouldHaveSingleItem();
        restriction.IsRestricted.ShouldBeTrue();
        restriction.Project.ShouldBe(Project());
        restriction.AllowedTenantActivityTypeIds.ShouldBe(allowsType ? [ActivityId()] : []);
        using HttpResponseMessage issued = await factory.PostProtectedManagementAsync(client,
            "/api/timesheets/magic-links/confirmation-capabilities", Issue("after-own-policy"));
        issued.StatusCode.ShouldBe(allowsType ? HttpStatusCode.Accepted : HttpStatusCode.Forbidden);
        factory.Gateway.StoredEvents(Tenant().TenantId, "after-own-policy")
            .Count(row => row.EventTypeName == typeof(StoredMagicLinkIssued).FullName).ShouldBe(allowsType ? 1 : 0);
        using HttpResponseMessage adjusted = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/adjust/submit?t={ValidAdjustToken()}", AdjustCommand(), TestContext.Current.CancellationToken);
        adjusted.StatusCode.ShouldBe(allowsType ? HttpStatusCode.Accepted : HttpStatusCode.Forbidden);
        if (!allowsType)
            _ = await CaptureFailureAsync(adjusted, "restricted-selection", ValidAdjustToken());
        StreamReadEvent[] adjustedEvents = factory.Gateway.StoredEvents(Tenant().TenantId, adjustmentOwner.Value);
        adjustedEvents.Count(row => row.EventTypeName == typeof(StoredMagicLinkUsed).FullName).ShouldBe(allowsType ? 1 : 0);
        adjustedEvents.Count(row => row.EventTypeName == typeof(StoredTimeEntryAdjusted).FullName).ShouldBe(allowsType ? 1 : 0);
        using HttpResponseMessage confirmed = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}", new ConfirmTimeThroughMagicLink(),
            TestContext.Current.CancellationToken);
        confirmed.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        factory.Gateway.StoredEvents(Tenant().TenantId, confirmationOwner.Value)
            .Count(row => row.EventTypeName == typeof(StoredTimeEntryConfirmed).FullName).ShouldBe(1);

        static IssueMagicLinkConfirmationCapability Issue(string id) => new(new(id),
            new(Contributor(), TimeEntryTargetReference.ForProject(Project()), ActivityId(), new("entry-" + id),
                MagicLinkTargetKind.ProposedTimeEntry), MagicLinkAllowedAction.Confirm, ObservedAtUtc.AddDays(1),
            new("timesheets", id));
    }

    [Fact]
    public async Task FencedCatalogMigrationPreservesDirectBytesWithUntranslatedStoreSemantics()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        string key = MagicLinkActivityTypeCatalogReadModelAddress.StateKey(Tenant());
        string store = MagicLinkActivityTypeCatalogReadModelAddress.StateStoreName;
        var legacy = new ActivityTypeCatalogReadModel([], ProjectionFreshnessMetadata.Fresh)
        {
            ProjectPolicy = new(Tenant(), true, null!)
        };
        (await factory.Store.TrySaveAsync(store, key, legacy, string.Empty, TestContext.Current.CancellationToken)).ShouldBeTrue();
        ReadModelEntry<ActivityTypeCatalogReadModel> directBefore = await factory.Store.GetAsync<ActivityTypeCatalogReadModel>(
            store, key, TestContext.Current.CancellationToken);
        byte[] originalBytes = factory.Store.InspectRawBytes(store, key).ShouldNotBeNull();
        var handler = new TenantActivityTypeCatalogProjectionHandler(factory.Store);
        var coordinator = new SharedProjectionEpochCoordinator(factory.Store, factory.Store);
        SharedProjectionScope catalogScope = handler.CreateScope(Tenant().TenantId);
        await coordinator.RegisterWriterAsync(catalogScope, handler.OrdinaryWriterId, TestContext.Current.CancellationToken);
        var identity = new DomainSharedProjectionRebuildIdentity(Tenant().TenantId, "timesheets", handler.ProjectionType,
            "legacy-migration", "catalog");
        await coordinator.BeginAsync(catalogScope, identity.OperationId, "certified-empty-inventory",
            new Dictionary<string, long>(), TestContext.Current.CancellationToken);
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(identity, TestContext.Current.CancellationToken);
        DomainProjectionRebuildPlan plan = await handler.FinalizeAsync(identity, candidate, TestContext.Current.CancellationToken);
        await coordinator.StageAsync(catalogScope, identity.OperationId, plan.Operations, TestContext.Current.CancellationToken);
        await coordinator.CommitAsync(catalogScope, identity.OperationId, TestContext.Current.CancellationToken);
        ReadModelEntry<ActivityTypeCatalogReadModel> directAfter = await factory.Store.GetAsync<ActivityTypeCatalogReadModel>(
            store, key, TestContext.Current.CancellationToken);
        JsonSerializer.Serialize(directAfter.Value, JsonOptions).ShouldBe(JsonSerializer.Serialize(directBefore.Value, JsonOptions));
        directAfter.ETag.ShouldBe(directBefore.ETag);
        factory.Store.InspectRawBytes(store, key).ShouldNotBeNull().ShouldBe(originalBytes);
        SharedProjectionReadResult<ActivityTypeCatalogReadModel> selected = await coordinator.ReadAsync<ActivityTypeCatalogReadModel>(
            catalogScope, key, TestContext.Current.CancellationToken);
        selected.IsStale.ShouldBeFalse();
        selected.IsDegraded.ShouldBeFalse();
        selected.Value.ShouldNotBeNull().ProjectPolicy.ShouldNotBeNull().Restrictions.ShouldBeEmpty();
        selected.Value.ProjectPolicy.ShouldNotBeNull().IsComplete.ShouldBeTrue();
    }

    /// <summary>Exercises the deactivation decision through projection delivery and all four HTTP routes.</summary>
    [Fact]
    public async Task DeactivatedRecordedTypeCanBeConfirmedOrAdjustedToAnActiveReplacement()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm,
            new MagicLinkCapabilityId("capability-confirm-inactive"), new TimeEntryId("time-entry-confirm-inactive"),
            deactivateActivityType: true);
        await factory.ProjectValidStateAsync(client, ValidAdjustToken(), MagicLinkAllowedAction.Adjust,
            new MagicLinkCapabilityId("capability-adjust-inactive"), new TimeEntryId("time-entry-adjust-inactive"),
            replacementActivityTypeId: new("active-replacement"));
        ActivityTypeCatalogItem item = factory.Store
            .GetSelectedCatalog(Tenant())
            .Items.Single(item => item.ActivityTypeId == ActivityId());
        item.IsActive.ShouldBeFalse();
        item.IsAvailableForCapture.ShouldBeFalse();

        using HttpResponseMessage confirmDisplay = await client.GetAsync(
            $"/api/timesheets/magic-links/confirm?t={ValidConfirmToken()}", TestContext.Current.CancellationToken);
        using HttpResponseMessage confirmSubmit = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}",
            new ConfirmTimeThroughMagicLink(), TestContext.Current.CancellationToken);
        using HttpResponseMessage adjustDisplay = await client.GetAsync(
            $"/api/timesheets/magic-links/adjust?t={ValidAdjustToken()}", TestContext.Current.CancellationToken);
        using HttpResponseMessage inactiveReplacement = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/adjust/submit?t={ValidAdjustToken()}",
            AdjustCommand(), TestContext.Current.CancellationToken);
        CapturedFailure retiredDenial = await CaptureFailureAsync(inactiveReplacement, "inactive-replacement", ValidAdjustToken());
        using HttpResponseMessage unknownReplacement = await client.PostAsJsonAsync(
            "/api/timesheets/magic-links/adjust/submit?t=unknown-retired-replacement", AdjustCommand(), TestContext.Current.CancellationToken);
        CapturedFailure baseline = await CaptureFailureAsync(unknownReplacement, "unknown", "unknown-retired-replacement");
        retiredDenial.NormalizedBody.ShouldBe(baseline.NormalizedBody);
        retiredDenial.Headers.ShouldBe(baseline.Headers);
        RawByteLength(retiredDenial.RawBody).ShouldBe(RawByteLength(baseline.RawBody));
        factory.Gateway.StoredEvents(Tenant().TenantId, "time-entry-adjust-inactive").Length.ShouldBe(1);
        using HttpResponseMessage adjustSubmit = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/adjust/submit?t={ValidAdjustToken()}",
            AdjustCommand() with { ActivityTypeId = new("active-replacement") }, TestContext.Current.CancellationToken);

        confirmDisplay.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await confirmDisplay.Content.ReadFromJsonAsync<MagicLinkConfirmationDisplayResponse>(
            JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull().ActivityTypeLabel.ShouldBe("Delivery");
        confirmSubmit.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        adjustDisplay.StatusCode.ShouldBe(HttpStatusCode.OK);
        inactiveReplacement.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        adjustSubmit.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        StreamReadEvent[] persisted = factory.Gateway.StoredEvents(Tenant().TenantId, "time-entry-adjust-inactive");
        persisted.Length.ShouldBe(3);
        var adjusted = JsonSerializer.Deserialize<StoredTimeEntryAdjusted>(persisted[2].Payload, JsonOptions)
            .ShouldNotBeNull().Event;
        adjusted.PreviousValues.ActivityTypeId.ShouldBe(ActivityId());
        adjusted.AdjustedValues.ActivityTypeId.ShouldBe(new("active-replacement"));
        using HttpResponseMessage replay = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/adjust/submit?t={ValidAdjustToken()}",
            AdjustCommand() with { ActivityTypeId = new("active-replacement") }, TestContext.Current.CancellationToken);
        replay.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.Gateway.StoredEvents(Tenant().TenantId, "time-entry-adjust-inactive").Length.ShouldBe(3);
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
            .GetSelectedIndex()
            .Entries[Hash(ValidConfirmToken())];
        candidate.CapabilityId.ShouldBe(capabilityId);
        candidate.Tenant.ShouldBe(Tenant());
        factory.Store.GetSelectedCatalog(Tenant())
            .ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        factory.LocalPort = 8080;
        foreach (ExternalRoute route in ExternalRoutes())
        {
            string token = route.Action == MagicLinkAllowedAction.Confirm ? ValidConfirmToken() : ValidAdjustToken();
            using HttpResponseMessage response = await SendAsync(client, route, token);
            response.StatusCode.ShouldBe(route.Method == HttpMethod.Get
                ? HttpStatusCode.OK
                : HttpStatusCode.Accepted);
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
        string indexBefore = JsonSerializer.Serialize(factory.Store.GetSelectedIndex(), JsonOptions);
        string catalogBefore = JsonSerializer.Serialize(factory.Store.GetSelectedCatalog(Tenant()), JsonOptions);
        string assertion = await factory.IssueAssertionAsync(EventStoreWorkloadOperations.DomainServiceProject);
        factory.LocalPort = 8080;
        using HttpResponseMessage response = await factory.PostProjectionAsync(client, projectionPath, dispatch, assertion);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        JsonSerializer.Serialize(factory.Store.GetSelectedIndex(), JsonOptions).ShouldBe(indexBefore);
        JsonSerializer.Serialize(factory.Store.GetSelectedCatalog(Tenant()), JsonOptions).ShouldBe(catalogBefore);

        factory.LocalPort = 8081;
        using HttpResponseMessage admittedResponse = await factory.PostProjectionAsync(client, projectionPath, dispatch, assertion);
        admittedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admittedResponse.Content.ReadFromJsonAsync<ProjectionDispatchResponse>(
            JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull()
            .Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        factory.Store.GetSelectedIndex()
            .Entries[issued.TokenHash.Value].CapabilityId.ShouldBe(issued.CapabilityId);
    }

    /// <summary>Full Time Entry replay commits through the composed host despite its shared routes.</summary>
    [Fact]
    public async Task ComposedNamedRebuildCommitsAggregateOwnedTimeEntryEvidence()
    {
        var store = new InMemoryReadModelStore();
        using MagicLinkHttpBoundaryFactory factory = new(batchStore: store);
        using HttpClient client = factory.CreateClient();
        using IServiceScope scope = factory.Services.CreateScope();
        string[] routeNames = scope.ServiceProvider.GetServices<IAsyncDomainProjectionHandler>()
            .Where(static handler => handler.Domain == "timesheets")
            .Select(static handler => handler.ProjectionType)
            .ToArray();
        routeNames.ShouldContain(TimeEntryEvidenceProjection.ProjectionName);
        routeNames.ShouldContain("magic-link-token-hash-capability-index");
        routeNames.ShouldContain("magic-link-confirmation-capabilities");

        DomainProjectionIdentityOptions identity = scope.ServiceProvider
            .GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
        AdminOperationalIndexMetadata.DomainMetadata mappedDomain =
            AdminOperationalIndexMetadata.Create(
                scope.ServiceProvider.GetRequiredService<DiscoveryResult>(),
                ["timesheets"],
                scope.ServiceProvider.GetServices<IDomainQueryHandler>(),
                scope.ServiceProvider.GetServices<IAsyncDomainProjectionHandler>(),
                identity.AppId, identity.ServiceVersion,
                scope.ServiceProvider.GetRequiredService<IOptions<ProjectionDispatchOptions>>().Value,
                ["timesheets"],
                scope.ServiceProvider.GetServices<IDomainProjectionHandler>())
            .Domains.Single(domain => domain.Domain == "timesheets");
        mappedDomain.MappedOnlyStreamedProjectionTypes.ShouldNotBeNull().ShouldContain(
            TimeEntryEvidenceProjection.ProjectionName);
        mappedDomain.IncrementalReplayProjectionTypes.ShouldNotBeNull().ShouldContain(
            TimeEntryEvidenceProjection.ProjectionName);
        mappedDomain.SharedEpochProjectionTypes.ShouldNotBeNull().ShouldContain(
            MagicLinkConfirmationCapabilityProjection.ProjectionName);
        mappedDomain.SharedEpochProjectionTypes.ShouldNotBeNull().ShouldContain(
            MagicLinkTokenHashCapabilityIndexProjection.ProjectionName);
        ProjectionDispatchRoute[] routes = scope.ServiceProvider.GetServices<IAsyncDomainProjectionHandler>()
            .Where(static handler => handler.Domain == "timesheets")
            .Select(static handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))
            .ToArray();
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(
            identity.AppId, identity.ServiceVersion, routes);
        var entryId = new TimeEntryId("time-entry-rebuild");
        var recorded = new TimeEntryRecorded(
            entryId,
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
        var recordedEvent = new ProjectionEventDto(
            typeof(TimeEntryRecorded).FullName!,
            JsonSerializer.SerializeToUtf8Bytes(recorded, JsonOptions),
            "json", 1, ObservedAtUtc, "rebuild-correlation", "rebuild-message",
            Operator().PartyId, 1);
        var dispatch = new ProjectionDispatchRequest(
            new ProjectionRequest(Tenant().TenantId, "timesheets", entryId.Value, [recordedEvent]) {
                RebuildSourceHeadSequence = 1,
            },
            [TimeEntryEvidenceProjection.ProjectionName],
            "composed-time-entry-rebuild",
            fingerprint);

        using HttpResponseMessage response = await factory.PostProjectionAsync(
            client, "/project/rebuild/v1", dispatch);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ProjectionDispatchOutcome outcome = (await response.Content.ReadFromJsonAsync<ProjectionDispatchResponse>(
            JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull()
            .Outcomes.ShouldHaveSingleItem();
        outcome.ProjectionType.ShouldBe(TimeEntryEvidenceProjection.ProjectionName);
        outcome.Status.ShouldBe(ProjectionDispatchStatus.Completed);

        string key = TimeEntryEvidenceReadModelAddress.StateKey(Tenant().TenantId, entryId);
        var persisted = await store.GetAsync<TimeEntryEvidenceReadModel>(
            TimeEntryEvidenceReadModelAddress.StateStoreName, key, TestContext.Current.CancellationToken);
        persisted.Value.ShouldNotBeNull().TimeEntryId.ShouldBe(entryId);
        persisted.Value.DurationMinutes.ShouldBe(60);
        persisted.Value.EventPayloadLineage.Count.ShouldBe(1);
        persisted.Value.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);

        var unrelated = recordedEvent with
        {
            EventTypeName = "UnrelatedOwnerEvent",
            Payload = JsonSerializer.SerializeToUtf8Bytes(new { ignored = true }, JsonOptions),
            MessageId = "unrelated-rebuild-message"
        };
        var prune = dispatch with
        {
            Request = new ProjectionRequest(Tenant().TenantId, "timesheets", entryId.Value, [unrelated]),
            DispatchId = "composed-time-entry-prune"
        };
        using HttpResponseMessage unprovedResponse = await factory.PostProjectionAsync(
            client, "/project/rebuild/v1", prune);
        unprovedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await unprovedResponse.Content.ReadFromJsonAsync<ProjectionDispatchResponse>(
            JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull()
            .Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Failed);
        (await store.GetAsync<TimeEntryEvidenceReadModel>(
            TimeEntryEvidenceReadModelAddress.StateStoreName, key, TestContext.Current.CancellationToken))
            .Value.ShouldNotBeNull();

        var stale = prune with
        {
            Request = prune.Request with { RebuildSourceHeadSequence = 2 },
            DispatchId = "composed-time-entry-prune-stale"
        };
        using HttpResponseMessage staleResponse = await factory.PostProjectionAsync(
            client, "/project/rebuild/v1", stale);
        staleResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await staleResponse.Content.ReadFromJsonAsync<ProjectionDispatchResponse>(
            JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull()
            .Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Failed);
        (await store.GetAsync<TimeEntryEvidenceReadModel>(
            TimeEntryEvidenceReadModelAddress.StateStoreName, key, TestContext.Current.CancellationToken))
            .Value.ShouldNotBeNull();

        prune = prune with
        {
            Request = prune.Request with { RebuildSourceHeadSequence = 1 },
            DispatchId = "composed-time-entry-prune-proved"
        };
        using HttpResponseMessage pruneResponse = await factory.PostProjectionAsync(
            client, "/project/rebuild/v1", prune);
        pruneResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await pruneResponse.Content.ReadFromJsonAsync<ProjectionDispatchResponse>(
            JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull()
            .Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await store.GetAsync<TimeEntryEvidenceReadModel>(
            TimeEntryEvidenceReadModelAddress.StateStoreName, key, TestContext.Current.CancellationToken))
            .Value.ShouldBeNull();
    }

    /// <summary>Mapped replay retains a full high-entropy owner prefix and an invalid retry leaves it intact.</summary>
    [Fact]
    public async Task ComposedNamedRebuildRetainsTenThousandEventTimeEntryHistory()
    {
        var store = new InMemoryReadModelStore();
        using MagicLinkHttpBoundaryFactory factory = new(batchStore: store);
        using HttpClient client = factory.CreateClient();
        using IServiceScope scope = factory.Services.CreateScope();
        DomainProjectionIdentityOptions identity = scope.ServiceProvider
            .GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
        ProjectionDispatchRoute[] routes = scope.ServiceProvider.GetServices<IAsyncDomainProjectionHandler>()
            .Where(static handler => handler.Domain == "timesheets")
            .Select(static handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))
            .ToArray();
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(
            identity.AppId, identity.ServiceVersion, routes);
        var entryId = new TimeEntryId("time-entry-large-rebuild");
        var recorded = new TimeEntryRecorded(
            entryId, TimeEntryTargetReference.ForProject(Project()), Contributor(), ActivityId(),
            ActivityTypeScope.Tenant, new DateOnly(2026, 6, 19), 60, BillableState.Billable,
            TimeEntryApprovalState.Draft, ContributorCategory.ExternalContributor, null);
        ProjectionEventDto Event(long position, object payload) => new(
            payload.GetType().FullName!, JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions),
            "json", position, ObservedAtUtc, "large-rebuild-correlation", $"large-rebuild-message-{position}",
            Operator().PartyId, position);
        var events = new ProjectionEventDto[10_000];
        events[0] = Event(1, recorded);
        for (int index = 1; index < events.Length; index++)
        {
            string requestId = Convert.ToHexString(SHA256.HashData(BitConverter.GetBytes(index)));
            events[index] = Event(index + 1, new TimeEntryContributorConfirmed(
                entryId, Contributor(), Tenant(), ObservedAtUtc,
                new ExternalContributionSource("magic-link", requestId)));
        }

        var dispatch = new ProjectionDispatchRequest(
            new ProjectionRequest(Tenant().TenantId, "timesheets", entryId.Value, events) {
                RebuildSourceHeadSequence = events.LongLength,
            },
            [TimeEntryEvidenceProjection.ProjectionName], "composed-large-rebuild", fingerprint);
        byte[] complete = JsonSerializer.SerializeToUtf8Bytes(dispatch.Request.Events, JsonOptions);
        complete.Length.ShouldBeGreaterThan(ProjectionRebuildTransportReference.PartBytes);
        int partCount = (complete.Length + ProjectionRebuildTransportReference.PartBytes - 1)
            / ProjectionRebuildTransportReference.PartBytes;
        var header = dispatch with
        {
            Request = dispatch.Request with { Events = [] },
            RebuildTransport = new(partCount, Convert.ToHexString(SHA256.HashData(complete)))
        };
        for (int ordinal = 0; ordinal < partCount; ordinal++)
        {
            int offset = ordinal * ProjectionRebuildTransportReference.PartBytes;
            byte[] data = complete.AsSpan(offset,
                Math.Min(ProjectionRebuildTransportReference.PartBytes, complete.Length - offset)).ToArray();
            using HttpResponseMessage uploaded = await factory.PostProjectionPartAsync(client,
                new ProjectionRebuildTransportPartRequest(header, ordinal, data));
            uploaded.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        using HttpResponseMessage response = await factory.PostProjectionAsync(
            client, "/project/rebuild/stage/v1", header);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ProjectionDispatchResponse>(
            JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull()
            .Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);

        string key = TimeEntryEvidenceReadModelAddress.StateKey(Tenant().TenantId, entryId);
        (await store.GetAsync<TimeEntryEvidenceReadModel>(TimeEntryEvidenceReadModelAddress.StateStoreName,
            key, TestContext.Current.CancellationToken)).Value.ShouldBeNull();
        using HttpResponseMessage committed = await factory.PostProjectionAsync(
            client, "/project/rebuild/commit/v1", header);
        committed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await committed.Content.ReadFromJsonAsync<ProjectionDispatchResponse>(
            JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull()
            .Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        using HttpResponseMessage restaged = await factory.PostProjectionAsync(
            client, "/project/rebuild/stage/v1", header);
        restaged.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await restaged.Content.ReadFromJsonAsync<ProjectionDispatchResponse>(
            JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull()
            .Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        using HttpResponseMessage verified = await factory.PostProjectionAsync(
            client, "/project/rebuild/verify/v1", header);
        verified.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await verified.Content.ReadFromJsonAsync<ProjectionDispatchResponse>(
            JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull()
            .Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        for (int ordinal = 0; ordinal < partCount; ordinal++)
        {
            int offset = ordinal * ProjectionRebuildTransportReference.PartBytes;
            byte[] data = complete.AsSpan(offset,
                Math.Min(ProjectionRebuildTransportReference.PartBytes, complete.Length - offset)).ToArray();
            using HttpResponseMessage reuploaded = await factory.PostProjectionPartAsync(client,
                new ProjectionRebuildTransportPartRequest(header, ordinal, data));
            reuploaded.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        using HttpResponseMessage reconciled = await factory.PostProjectionAsync(
            client, "/project/rebuild/stage/v1", header);
        reconciled.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await reconciled.Content.ReadFromJsonAsync<ProjectionDispatchResponse>(
            JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull()
            .Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        TimeEntryEvidenceReadModel published = (await store.GetAsync<TimeEntryEvidenceReadModel>(
            TimeEntryEvidenceReadModelAddress.StateStoreName, key, TestContext.Current.CancellationToken))
            .Value.ShouldNotBeNull();
        published.EventLineage.Count.ShouldBe(10_000);
        published.EventPayloadLineage.Count.ShouldBe(10_000);

        var partial = header with { DispatchId = "composed-large-rebuild-partial" };
        using HttpResponseMessage firstPart = await factory.PostProjectionPartAsync(client,
            new ProjectionRebuildTransportPartRequest(partial, 0,
                complete.AsSpan(0, ProjectionRebuildTransportReference.PartBytes).ToArray()));
        firstPart.StatusCode.ShouldBe(HttpStatusCode.OK);
        using HttpResponseMessage incomplete = await factory.PostProjectionAsync(
            client, "/project/rebuild/stage/v1", partial);
        incomplete.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await store.GetAsync<TimeEntryEvidenceReadModel>(TimeEntryEvidenceReadModelAddress.StateStoreName,
            key, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull()
            .EventPayloadLineage.ShouldBe(published.EventPayloadLineage);

        var malformed = dispatch with
        {
            Request = new ProjectionRequest(Tenant().TenantId, "timesheets", entryId.Value,
                [Event(1, recorded), Event(2, new TimeEntryContributorConfirmed(
                    entryId, new PartyReference("wrong-contributor"), Tenant(), ObservedAtUtc,
                    new ExternalContributionSource("magic-link", "invalid")))]) {
                RebuildSourceHeadSequence = 2,
            },
            DispatchId = "composed-large-rebuild-invalid"
        };
        using HttpResponseMessage failedResponse = await factory.PostProjectionAsync(
            client, "/project/rebuild/v1", malformed);
        failedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await failedResponse.Content.ReadFromJsonAsync<ProjectionDispatchResponse>(
            JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull()
            .Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Failed);
        TimeEntryEvidenceReadModel retained = (await store.GetAsync<TimeEntryEvidenceReadModel>(
            TimeEntryEvidenceReadModelAddress.StateStoreName, key, TestContext.Current.CancellationToken))
            .Value.ShouldNotBeNull();
        retained.EventPayloadLineage.ShouldBe(published.EventPayloadLineage);
    }

    /// <summary>Normal composed projection delivery persists a selected capability after issuance and use.</summary>
    [Fact]
    public async Task ComposedNamedCapabilityRoutePersistsIssuedAndUsedSelectedRow()
    {
        var store = new InMemoryReadModelStore();
        using MagicLinkHttpBoundaryFactory factory = new(batchStore: store);
        using HttpClient client = factory.CreateClient();
        using IServiceScope scope = factory.Services.CreateScope();
        DomainProjectionIdentityOptions identity = scope.ServiceProvider
            .GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
        ProjectionDispatchRoute[] routes = scope.ServiceProvider.GetServices<IAsyncDomainProjectionHandler>()
            .Where(static handler => handler.Domain == "timesheets")
            .Select(static handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))
            .ToArray();
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(
            identity.AppId, identity.ServiceVersion, routes);
        var capabilityId = new MagicLinkCapabilityId("composed-capability");
        var entryId = new TimeEntryId("composed-entry");
        var issued = new MagicLinkConfirmationCapabilityIssued(
            capabilityId, Tenant(), Contributor(), TimeEntryTargetReference.ForProject(Project()),
            ActivityId(), entryId, MagicLinkTargetKind.ProposedTimeEntry, MagicLinkAllowedAction.Confirm,
            new MagicLinkTokenHash("private-test-hash"), ObservedAtUtc.AddDays(1), Operator(),
            ObservedAtUtc.AddHours(-1), new MagicLinkAuditMetadata("timesheets", "composed-issue"), true);
        var used = new MagicLinkConfirmationCapabilityUsed(
            capabilityId, Tenant(), Contributor(), entryId, ObservedAtUtc,
            new MagicLinkAuditMetadata("timesheets", "composed-use"));
        ProjectionEventDto Event(long position, object payload) => new(
            payload.GetType().FullName!, JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions),
            "json", 1, ObservedAtUtc, "composed-correlation", $"composed-message-{position}",
            Operator().PartyId, position);
        foreach ((string owner, ProjectionEventDto item) in new[]
        {
            (capabilityId.Value, Event(1, new StoredMagicLinkIssued(issued))),
            (entryId.Value, Event(2, new StoredMagicLinkUsed(used)))
        })
        {
            var dispatch = new ProjectionDispatchRequest(
                new ProjectionRequest(Tenant().TenantId, "timesheets", owner, [item]),
                [MagicLinkConfirmationCapabilityProjection.ProjectionName,
                    MagicLinkTokenHashCapabilityIndexProjection.ProjectionName],
                $"composed-capability-{owner}", fingerprint);
            using HttpResponseMessage response = await factory.PostProjectionAsync(client, "/project/v2", dispatch);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            ProjectionDispatchOutcome[] outcomes = [.. (await response.Content.ReadFromJsonAsync<ProjectionDispatchResponse>(
                JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull().Outcomes];
            outcomes.Length.ShouldBe(2);
            outcomes.ShouldAllBe(outcome => outcome.Status == ProjectionDispatchStatus.Completed
                || outcome.Status == ProjectionDispatchStatus.AlreadyCompleted);
        }

        var handler = scope.ServiceProvider.GetServices<IAsyncDomainProjectionHandler>()
            .OfType<MagicLinkConfirmationCapabilityProjectionHandler>().Single();
        var coordinator = scope.ServiceProvider.GetRequiredService<SharedProjectionEpochCoordinator>();
        SharedProjectionReadResult<MagicLinkConfirmationCapabilityReadModel> selected = await handler.ReadObservedAsync(
            Tenant().TenantId, capabilityId, ObservedAtUtc, coordinator, TestContext.Current.CancellationToken);
        selected.IsAvailable.ShouldBeTrue();
        selected.Value.ShouldNotBeNull().State.ShouldBe(Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Used);
        JsonSerializer.Serialize(selected.Value, JsonOptions).ShouldNotContain("private-test-hash");
        SharedProjectionScope indexScope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
        MagicLinkTokenHashCapabilityIndexTreeRoot indexRoot = (await coordinator
            .ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(indexScope,
                MagicLinkTokenHashCapabilityIndexTreeReader.RootKey,
                TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        var indexReader = new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, indexScope);
        (await indexReader.FindCandidatesAsync(indexRoot.TokenRoot, "private-test-hash",
            TestContext.Current.CancellationToken)).ShouldBe([
                new MagicLinkTokenHashCapabilityIndexEntry(Tenant(), capabilityId)]);
    }

    /// <summary>Malformed recognized evidence fails normal dispatch and mapped replay without publishing a row.</summary>
    /// <param name="correction">Whether the malformed effect is a correction rather than a confirmation.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MalformedTimeEntryEffectFailsDispatchAndMappedRebuild(bool correction)
    {
        var store = new InMemoryReadModelStore();
        using MagicLinkHttpBoundaryFactory factory = new(batchStore: store);
        using HttpClient client = factory.CreateClient();
        using IServiceScope scope = factory.Services.CreateScope();
        DomainProjectionIdentityOptions identity = scope.ServiceProvider
            .GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
        ProjectionDispatchRoute[] routes = scope.ServiceProvider.GetServices<IAsyncDomainProjectionHandler>()
            .Where(static handler => handler.Domain == "timesheets")
            .Select(static handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))
            .ToArray();
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(
            identity.AppId, identity.ServiceVersion, routes);
        var entryId = new TimeEntryId(correction ? "time-entry-bad-correction" : "time-entry-bad-confirmation");
        var recorded = new TimeEntryRecorded(
            entryId, TimeEntryTargetReference.ForProject(Project()), Contributor(), ActivityId(),
            ActivityTypeScope.Tenant, new DateOnly(2026, 6, 19), 60, BillableState.Billable,
            TimeEntryApprovalState.Draft, ContributorCategory.ExternalContributor, null);
        var correctedValues = new TimeEntryCorrectionValues(
            TimeEntryTargetReference.ForProject(Project()), Contributor(), ActivityId(),
            new DateOnly(2026, 6, 19), 60, BillableState.Billable,
            ContributorCategory.ExternalContributor, null);
        object malformed = correction
            ? new TimeEntryCorrected(
                entryId, new TimeEntryCorrectionId("correction-1"), Tenant(), Operator(),
                ObservedAtUtc, null!, correctedValues, new TimeEntryRejectionReason("Rejected"),
                new TimeEntryApprovalDecisionId("decision-1"), TimeEntryApprovalState.Draft,
                TimeEntryCorrectionState.Corrected)
            : new TimeEntryContributorConfirmed(
                entryId, null!, Tenant(), ObservedAtUtc,
                new ExternalContributionSource("magic-link", "capability-1"));
        ProjectionEventDto Event(long sequence, object payload) => new(
            payload.GetType().FullName!, JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions),
            "json", sequence, ObservedAtUtc, "malformed-correlation", $"malformed-{sequence}",
            Operator().PartyId, sequence);
        var request = new ProjectionRequest(
            Tenant().TenantId, "timesheets", entryId.Value, [Event(1, recorded), Event(2, malformed)]);

        foreach (string path in new[] { "/project/v2", "/project/rebuild/v1" })
        {
            var dispatch = new ProjectionDispatchRequest(
                request, [TimeEntryEvidenceProjection.ProjectionName],
                $"malformed-{(correction ? "correction" : "confirmation")}-{path}", fingerprint);
            using HttpResponseMessage response = await factory.PostProjectionAsync(client, path, dispatch);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            ProjectionDispatchOutcome outcome = (await response.Content.ReadFromJsonAsync<ProjectionDispatchResponse>(
                JsonOptions, TestContext.Current.CancellationToken)).ShouldNotBeNull()
                .Outcomes.ShouldHaveSingleItem();
            outcome.Status.ShouldBe(ProjectionDispatchStatus.Failed);
            outcome.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);

            string key = TimeEntryEvidenceReadModelAddress.StateKey(Tenant().TenantId, entryId);
            (await store.GetAsync<TimeEntryEvidenceReadModel>(
                TimeEntryEvidenceReadModelAddress.StateStoreName, key, TestContext.Current.CancellationToken))
                .Value.ShouldBeNull();
        }
    }

    /// <summary>Projection delivery on the internal port still requires both configured credentials.</summary>
    [Fact]
    public async Task InternalProjectionRouteRejectsMissingAndForgedCredentials()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, internalPort: 8081, localPort: 8081);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage missing = await client.PostAsJsonAsync(
            "/project/v2", new { }, JsonOptions, TestContext.Current.CancellationToken);
        using HttpRequestMessage forgedRequest = new(HttpMethod.Post, "/project/v2")
        {
            Content = JsonContent.Create(new { }, options: JsonOptions)
        };
        forgedRequest.Headers.Add(DaprAppChannelToken.HeaderName, "timesheets-test-app-channel-token");
        forgedRequest.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, "forged-assertion");
        using HttpResponseMessage forged = await client.SendAsync(forgedRequest, TestContext.Current.CancellationToken);
        string validAssertion = await factory.IssueAssertionAsync(EventStoreWorkloadOperations.DomainServiceProject);
        using HttpRequestMessage missingChannelRequest = new(HttpMethod.Post, "/project/v2")
        {
            Content = JsonContent.Create(new { }, options: JsonOptions)
        };
        missingChannelRequest.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, validAssertion);
        using HttpResponseMessage missingChannel = await client.SendAsync(missingChannelRequest, TestContext.Current.CancellationToken);
        using HttpRequestMessage wrongOperationRequest = new(HttpMethod.Post, "/project/v2")
        {
            Content = JsonContent.Create(new { }, options: JsonOptions)
        };
        wrongOperationRequest.Headers.Add(DaprAppChannelToken.HeaderName, "timesheets-test-app-channel-token");
        wrongOperationRequest.Headers.Add(
            EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName,
            await factory.IssueAssertionAsync(EventStoreWorkloadOperations.DomainServiceQuery));
        using HttpResponseMessage wrongOperation = await client.SendAsync(wrongOperationRequest, TestContext.Current.CancellationToken);

        missing.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        forged.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        missingChannel.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        wrongOperation.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.Store.Contains(MagicLinkTokenHashCapabilityIndexProjection.StateKey).ShouldBeFalse();
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

        using HttpResponseMessage response = await factory.PostProtectedManagementAsync(
            client, "/api/timesheets/magic-links/confirmation-capabilities", command);

        TenantReference expectedTenant = useMismatchedClaims ? OtherTenant() : Tenant();
        PartyReference expectedActor = useMismatchedClaims ? OtherContributor() : Contributor();
        factory.ObservedTrustedContext.Tenant.ShouldBe(expectedTenant);
        factory.ObservedTrustedContext.Actor.ShouldBe(expectedActor);
        factory.ObservedTrustedContext.CorrelationId.ShouldNotBeNullOrWhiteSpace();
        TimesheetsRequestContext authorizedContext = factory.AccessGuard.LastAuthorizationRequest.ShouldNotBeNull().Context;
        authorizedContext.Tenant.ShouldBe(expectedTenant);
        authorizedContext.Actor.ShouldBe(expectedActor);
        authorizedContext.CorrelationId.ShouldNotBeNullOrWhiteSpace();
        string catalogKey = MagicLinkActivityTypeCatalogReadModelAddress.StateKey(expectedTenant);
        SharedProjectionScope catalogScope = TenantActivityTypeCatalogEpoch.CreateScope(expectedTenant.TenantId);
        string epochKey = "shared-epoch:" + catalogScope.ComputeHash();
        string physicalPrefix = "shared-generation:" + catalogScope.ComputeHash() + ":";
        factory.Store.ReadKeys.ShouldNotContain(catalogKey);
        factory.Store.ReadKeys.ShouldContain(epochKey);
        factory.Store.ReadKeys.Count(key => key.StartsWith(physicalPrefix, StringComparison.Ordinal))
            .ShouldBe(useMismatchedClaims ? 0 : 2);
        factory.Store.ReadKeys.ShouldAllBe(key => key == epochKey || key.StartsWith(physicalPrefix, StringComparison.Ordinal));
        StreamReadRequest request = factory.Gateway.Requests[0];
        request.Tenant.ShouldBe(expectedTenant.TenantId);
        request.AggregateId.ShouldBe(capabilityId.Value);
        response.StatusCode.ShouldBe(useMismatchedClaims ? HttpStatusCode.Forbidden : HttpStatusCode.Accepted);

        // Revoking an existing capability requires the authoritative stream to be folded, whereas
        // issuance of a new id correctly succeeds when that stream is absent.
        factory.Gateway.Requests.Clear();
        factory.Store.ReadKeys.Clear();
        var existingId = new MagicLinkCapabilityId("capability-existing");
        using HttpResponseMessage revocation = await factory.PostProtectedManagementAsync(
            client,
            $"/api/timesheets/magic-links/confirmation-capabilities/{existingId.Value}/revoke",
            new RevokeMagicLinkConfirmationCapability(existingId, new MagicLinkAuditMetadata("timesheets", "admin-revoke")));
        revocation.StatusCode.ShouldBe(useMismatchedClaims ? HttpStatusCode.Forbidden : HttpStatusCode.Accepted);
        StreamReadRequest existingRequest = factory.Gateway.Requests[0];
        existingRequest.Tenant.ShouldBe(expectedTenant.TenantId);
        existingRequest.AggregateId.ShouldBe(existingId.Value);
        factory.Store.ReadKeys.ShouldBeEmpty();
        TimesheetsRequestContext revocationContext = factory.AccessGuard.LastAuthorizationRequest.ShouldNotBeNull().Context;
        revocationContext.Tenant.ShouldBe(expectedTenant);
        revocationContext.Actor.ShouldBe(expectedActor);
        revocationContext.CorrelationId.ShouldNotBeNullOrWhiteSpace();
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
        using HttpResponseMessage valid = await factory.PostProtectedManagementAsync(
            client, "/api/timesheets/magic-links/confirmation-capabilities", command);
        valid.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        generator.GenerationCount.ShouldBe(1);
        factory.ObservedTrustedContext.Tenant.ShouldBe(Tenant());
        factory.ObservedTrustedContext.Actor.ShouldBe(Contributor());
        factory.Store.ReadKeys.Clear();
        factory.Gateway.Requests.Clear();
        int authorizationBefore = factory.AccessGuard.AuthorizationCount;
        int trustedWorkBefore = factory.AccessGuard.TrustedWorkExecutionCount;
        string indexBefore = JsonSerializer.Serialize(factory.Store.GetSelectedIndex(), JsonOptions);
        string catalogBefore = JsonSerializer.Serialize(factory.Store.GetSelectedCatalog(Tenant()), JsonOptions);
        JsonObject body = JsonSerializer.SerializeToNode(command, JsonOptions).ShouldNotBeNull().AsObject();
        if (omitIdentifier)
        {
            body.Remove("capabilityId").ShouldBeTrue();
        }
        else
        {
            body["capabilityId"] = null;
        }

        using HttpResponseMessage response = await factory.PostProtectedManagementAsync(
            client, "/api/timesheets/magic-links/confirmation-capabilities", body);

        CapturedFailure failure = await CaptureFailureAsync(response, omitIdentifier ? "missing-id" : "null-id", string.Empty);
        failure.RawBody.ShouldNotContain("oneTimeToken", Case.Insensitive);
        failure.RawBody.ShouldNotContain("capabilityId", Case.Insensitive);
        factory.Store.ReadKeys.ShouldBeEmpty();
        factory.Gateway.Requests.ShouldBeEmpty();
        factory.AccessGuard.AuthorizationCount.ShouldBe(authorizationBefore);
        factory.AccessGuard.TrustedWorkExecutionCount.ShouldBe(trustedWorkBefore);
        generator.GenerationCount.ShouldBe(1);
        JsonSerializer.Serialize(factory.Store.GetSelectedIndex(), JsonOptions).ShouldBe(indexBefore);
        JsonSerializer.Serialize(factory.Store.GetSelectedCatalog(Tenant()), JsonOptions).ShouldBe(catalogBefore);
    }

    [Fact]
    public async Task ConcreteLoaderCrossTenantCandidateIsIndistinguishableFromAnUnknownTokenOnEveryRoute()
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
                .GetSelectedIndex()
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
    public async Task RejectedCatalogDeliveryKeepsConfirmSubmitOpaqueWithoutCapabilityUse(string scenario)
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
        // An earlier valid event may already be selected, but unresolved journal evidence prevents use.
        factory.Store.ContainsSelectedCatalog(Tenant())
            .ShouldBe(scenario == "unreadable");
    }

    [Fact]
    public async Task EmptyMagicLinkTokenUsesTheSameOpaqueBoundaryDenial()
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
    public async Task RepeatedInvalidMagicLinkAttemptsAreByteEquivalentWithoutThrottlingHeaders()
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
    public async Task InvalidMagicLinkHttpBoundaryEmitsNoTimesheetsSensitiveDiagnostics()
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
            using HttpResponseMessage validResponse = await SendAsync(client, route, validToken);
            validResponse.StatusCode.ShouldBe(route.Method == HttpMethod.Get ? HttpStatusCode.OK : HttpStatusCode.Forbidden);
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
    public async Task ConfirmSubmitReportsOneIndistinguishableCategoryForEveryNonFreshCatalog()
    {
        ExternalRoute route = ExternalRoutes().Single(static candidate => candidate.Name == "confirm-submit");

        // A non-Fresh catalog behind an otherwise resolvable token and an unresolved token
        // must produce identical public diagnostics. The concrete loader retains status only
        // internally; this boundary never publishes the distinction.
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
    public async Task EmptyOrMissingMagicLinkTokenIsIndistinguishableFromAnInvalidStateDenialOnEveryRoute()
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
    public async Task ValidMagicLinkDisplayResponsesDoNotDiscloseInternalSensitiveMaterial()
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
    public async Task MalformedSubmitBodyNeitherDispatchesNorDisclosesOnMagicLinkSubmitRoutes()
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

            string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            AssertProtectedResponseMaterialAbsent(body, token);
            AssertSensitiveResponseHeadersAbsent(response, token);
            foreach (LogRecord record in factory.Logs.Records)
            {
                AssertSensitiveDiagnosticsAbsent(record, token);
            }

            response.StatusCode.ShouldNotBe(HttpStatusCode.Accepted, route.Name);
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, route.Name);

            body.ShouldNotContain(token, Case.Insensitive, route.Name);
            foreach (string forbidden in InternalDisclosureMaterial())
            {
                body.ShouldNotContain(forbidden, Case.Insensitive, route.Name);
            }
        }

        factory.Gateway.SubmissionCount.ShouldBe(0);
        factory.Logs.Records.ShouldContain(record => record.Level >= LogLevel.Warning
            && record.Exception != null);
    }

    [Fact]
    public async Task EmptyMagicLinkTokenRecordsTheMalformedOutcomeCategoryWithoutSensitiveDiagnostics()
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

        AssertSensitiveResponseMaterialAbsent(body, token);
        AssertSensitiveResponseHeadersAbsent(response, token);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, name);
        response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("application/problem+json", name);

        JsonObject problem = JsonNode.Parse(body).ShouldNotBeNull().AsObject();
        problem["title"]?.GetValue<string>().ShouldBe(MagicLinkInvalidLinkDenial.Default.Title, name);
        problem["detail"]?.GetValue<string>().ShouldBe(MagicLinkInvalidLinkDenial.Default.Detail, name);
        problem.ContainsKey("errors").ShouldBeFalse(name);
        problem.ContainsKey("recoveryPath").ShouldBeFalse(name);
        problem.ContainsKey("RecoveryPath").ShouldBeFalse(name);

        return new CapturedFailure(
            name,
            response.StatusCode,
            response.Content.Headers.ContentType.MediaType!,
            NormalizeProblemJson(problem),
            HeaderSet(response),
            body);
    }

    private static void AssertPrivateLinkHeaders(HttpResponseMessage response)
    {
        response.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
        response.Headers.GetValues("Referrer-Policy").ShouldBe(["no-referrer"]);
    }

    private static void AssertSensitiveDiagnosticsAbsent(LogRecord record, string token)
    {
        string rendered = $"{record.Category} {record.Message} {string.Join(' ', record.State)} {string.Join(' ', record.Scopes)} {record.Exception}";
        AssertProtectedMaterialAbsent(rendered, token);
        if (record.Category.StartsWith("Hexalith.Timesheets", StringComparison.Ordinal))
        {
            AssertSensitiveMaterialAbsent(rendered, token);
            AssertDiagnosticDurationEvidenceAbsent(record);
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
            "comment",
            "token",
            "Delivery",
            "durationMinutes",
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

    private static void AssertSensitiveResponseMaterialAbsent(string content, string token)
    {
        AssertProtectedResponseMaterialAbsent(content, token);
        AssertSensitiveMaterialAbsent(content, token);
        AssertResponseDurationEvidenceAbsent(content);
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
            ProjectionFreshnessMetadata.Fresh)
        {
            ProjectPolicy = new(new TenantReference("tenant-1"), true, [])
        };

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
        IMagicLinkTokenGenerator? tokenGenerator = null,
        IWorkloadAssertionIssuer? workloadAssertionIssuer = null,
        HttpMessageHandler? workloadGatewayHandler = null,
        ExternalContributionPolicyOptions? externalContributionPolicy = null,
        ITimeEntryCommentPublicationAuthority? commentAuthority = null,
        InMemoryReadModelStore? batchStore = null,
        IMagicLinkConfirmationCapabilityStateLoader? stateLoader = null,
        bool useLiveFixtureAdmission = false,
        IMagicLinkSystemExpiryAuthorizer? systemExpiryAuthorizer = null,
        IReadModelStore? readModelStore = null) : WebApplicationFactory<Program>
    {
        private const string ChannelToken = "timesheets-test-app-channel-token";

        public ScriptedAccessGuard AccessGuard { get; } = new();

        /// <summary>Gets the tenant/role admission seam used before transition state reads.</summary>
        public ITimesheetsTenantAccessValidator TenantAccessValidator { get; } = CreateTenantAccessValidator();

        public CapturingLoggerProvider Logs { get; } = new();

        public ProjectionBackedReadModelStore Store { get; } = new();

        private ScriptedEventStoreGateway? _gateway;

        public ScriptedEventStoreGateway Gateway => _gateway ??= new ScriptedEventStoreGateway(() => Services);

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
            bool deactivateActivityType = false,
            ActivityTypeId? replacementActivityTypeId = null)
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

            if (!Store.ContainsSelectedCatalog(Tenant()))
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
                Store.GetSelectedCatalog(Tenant())
                    .ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
                var catalogHandler = new TenantActivityTypeCatalogProjectionHandler(Store);
                var catalogCoordinator = new SharedProjectionEpochCoordinator(Store, Store);
                SharedProjectionScope catalogScope = catalogHandler.CreateScope(Tenant().TenantId);
                var rebuildIdentity = new DomainSharedProjectionRebuildIdentity(Tenant().TenantId, "timesheets",
                    catalogHandler.ProjectionType, "fixture-catalog-rebuild", fingerprint);
                ProjectionRequest completeCatalogHistory = new(Tenant().TenantId, "timesheets", ActivityId().Value,
                    deactivateActivityType
                        ? [ProjectionEvent(1, ActivityCreated()), ProjectionEvent(2, new ActivityTypeDeactivated(ActivityId()))]
                        : [ProjectionEvent(1, ActivityCreated())]);
                await catalogCoordinator.BeginAsync(catalogScope, rebuildIdentity.OperationId, "fixture-inventory",
                    new Dictionary<string, long> { [ActivityId().Value] = completeCatalogHistory.Events[^1].GlobalPosition });
                DomainSharedProjectionRebuildCandidate catalogCandidate = await catalogHandler.CreateEmptyCandidateAsync(
                    rebuildIdentity, TestContext.Current.CancellationToken);
                catalogCandidate = await catalogHandler.AccumulateAsync(rebuildIdentity, catalogCandidate,
                    completeCatalogHistory, TestContext.Current.CancellationToken);
                DomainProjectionRebuildPlan catalogPlan = await catalogHandler.FinalizeAsync(rebuildIdentity,
                    catalogCandidate, TestContext.Current.CancellationToken);
                await catalogCoordinator.StageAsync(catalogScope, rebuildIdentity.OperationId, catalogPlan.Operations);
                await catalogCoordinator.CommitAsync(catalogScope, rebuildIdentity.OperationId);
                await catalogCoordinator.CatchUpAsync(catalogScope,
                    (delivery, generation, cancellation) => catalogHandler.FoldCatchUpAsync(
                        delivery, generation, catalogCoordinator, cancellation));
            }

            Gateway.WithStream(Tenant().TenantId, capabilityId.Value, StreamEvent(1, issued));
            Gateway.WithStream(Tenant().TenantId, timeEntryId.Value, StreamEvent(1, recorded));
            if (replacementActivityTypeId is not null)
            {
                await DispatchProjectionAsync(client,
                    new ProjectionRequest(Tenant().TenantId, "timesheets", replacementActivityTypeId.Value,
                        [ProjectionEvent(1, ActivityCreated(replacementActivityTypeId))]),
                    TenantActivityTypeCatalogProjection.ProjectionName, "dispatch-active-replacement", fingerprint);
            }
        }

        public Task<ProjectionDispatchOutcome> DeliverProjectRestrictionAsync(
            HttpClient client, TenantReference tenant, ActivityTypeId[] allowed)
            => DispatchProjectionAsync(client, new ProjectionRequest(tenant.TenantId, "timesheets", "project-policy-source",
                [ProjectionEvent(1, new ProjectActivityTypeCatalogRestrictionConfigured(Project(), true, allowed, []))]),
                TenantActivityTypeCatalogProjection.ProjectionName, "restriction-" + tenant.TenantId,
                LastIndexDispatch!.CatalogFingerprint);

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
                scenario == "missing-sequence" ? ProjectionDispatchStatus.Failed : ProjectionDispatchStatus.Retryable,
                scenario == "missing-sequence" ? ProjectionDispatchReasonCodes.MalformedOutcome
                    : ProjectionDispatchReasonCodes.PartialRetry);

            Gateway.WithStream(Tenant().TenantId, capabilityId.Value, StreamEvent(1, issued));
            Gateway.WithStream(Tenant().TenantId, timeEntryId.Value, StreamEvent(1, recorded));
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            if (useLiveFixtureAdmission)
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("Timesheets:LiveDaprFixture:Enabled", "true");
            }

            builder.UseSetting(DaprAppChannelToken.ConfigurationKey, ChannelToken);
            builder.UseSetting("EventStore:DomainService:AppId", "timesheets");
            builder.UseSetting("Authentication:JwtBearer:Issuer", "timesheets-test-issuer");
            builder.UseSetting("Authentication:JwtBearer:Audience", "hexalith-eventstore");
            builder.UseSetting("Authentication:JwtBearer:SigningKey", Convert.ToBase64String(Encoding.UTF8.GetBytes(new string('k', 32))));
            builder.UseSetting("Authentication:JwtBearer:AllowedAlgorithms:0", SecurityAlgorithms.HmacSha256);
            builder.UseSetting("Authentication:JwtBearer:RequireHttpsMetadata", "false");
            builder.UseSetting("Authentication:WorkloadIssuer:Workload", "eventstore");

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
                if (stateLoader is not null)
                {
                    services.RemoveAll<IMagicLinkConfirmationCapabilityStateLoader>();
                    services.AddSingleton(stateLoader);
                }
                else if (!useConcreteLoader)
                {
                    services.RemoveAll<IMagicLinkConfirmationCapabilityStateLoader>();
                    services.AddScoped<IMagicLinkConfirmationCapabilityStateLoader, ScriptedMagicLinkStateLoader>();
                }

                if (!useLiveFixtureAdmission)
                {
                    services.RemoveAll<ITimesheetsAccessGuard>();
                    services.AddSingleton<ITimesheetsAccessGuard>(AccessGuard);
                    services.RemoveAll<ITimesheetsTenantAccessValidator>();
                    services.AddSingleton(TenantAccessValidator);
                }

                if (externalContributionPolicy is not null)
                {
                    services.RemoveAll<ExternalContributionPolicyOptions>();
                    services.AddSingleton(externalContributionPolicy);
                }

                if (commentAuthority is not null)
                {
                    services.RemoveAll<ITimeEntryCommentPublicationAuthority>();
                    services.AddSingleton(commentAuthority);
                }

                if (systemExpiryAuthorizer is not null)
                {
                    services.RemoveAll<IMagicLinkSystemExpiryAuthorizer>();
                    services.AddSingleton(systemExpiryAuthorizer);
                }

                if (tokenGenerator is not null)
                {
                    services.RemoveAll<IMagicLinkTokenGenerator>();
                    services.AddSingleton<IMagicLinkTokenGenerator>(tokenGenerator);
                }

                services.RemoveAll<IReadModelStore>();
                services.RemoveAll<DaprReadModelStore>();
                IReadModelStore projectionStore = readModelStore ?? (batchStore is not null
                    ? batchStore
                    : useConcreteLoader ? Store : new UnavailableReadModelStore());
                services.AddSingleton(projectionStore);
                services.RemoveAll<IReadModelBatchStore>();
                services.AddSingleton<IReadModelBatchStore>(batchStore is not null
                    ? batchStore : Store);

                if (workloadGatewayHandler is null)
                {
                    services.RemoveAll<IEventStoreGatewayClient>();
                    services.AddSingleton<IEventStoreGatewayClient>(Gateway);
                }
                else
                {
                    services.Configure<HttpClientFactoryOptions>(nameof(IEventStoreGatewayClient),
                        options => options.HttpMessageHandlerBuilderActions.Add(
                            handlerBuilder => handlerBuilder.PrimaryHandler = workloadGatewayHandler));
                }

                if (workloadAssertionIssuer is not null)
                {
                    services.RemoveAll<IWorkloadAssertionIssuer>();
                    services.AddSingleton(workloadAssertionIssuer);
                }

                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new StaticTimeProvider(ObservedAtUtc));

                Claim[] adminClaims = claims ??
                [
                    new Claim("tenant_id", (useMismatchedClaims ?? useConcreteLoader) ? OtherTenant().TenantId : Tenant().TenantId),
                    new Claim("party_id", (useMismatchedClaims ?? useConcreteLoader) ? OtherContributor().PartyId : Contributor().PartyId),
                    new Claim(ClaimTypes.NameIdentifier,
                        (useMismatchedClaims ?? useConcreteLoader) ? OtherContributor().PartyId : Contributor().PartyId)
                ];
                services.AddSingleton<IClaimsTransformation>(provider =>
                    new MagicLinkFixtureClaimsTransformation(provider.GetRequiredService<IHttpContextAccessor>(), adminClaims));

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
            using HttpResponseMessage projectionResponse = await PostProjectionAsync(client, "/project/v2", dispatch);
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

        public async Task<HttpResponseMessage> PostProjectionAsync(
            HttpClient client,
            string route,
            ProjectionDispatchRequest dispatch,
            string? assertion = null)
        {
            using HttpRequestMessage request = new(HttpMethod.Post, route)
            {
                Content = JsonContent.Create(dispatch, options: JsonOptions)
            };
            await AddWorkloadHeadersAsync(request, EventStoreWorkloadOperations.DomainServiceProject, assertion);

            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        public async Task<HttpResponseMessage> PostProjectionPartAsync(
            HttpClient client, ProjectionRebuildTransportPartRequest part)
        {
            using HttpRequestMessage request = new(HttpMethod.Post, "/project/rebuild/part/v1")
            {
                Content = JsonContent.Create(part, options: JsonOptions)
            };
            await AddWorkloadHeadersAsync(request, EventStoreWorkloadOperations.DomainServiceProject);
            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        public async Task<HttpResponseMessage> PostSharedProjectionAsync(
            HttpClient client, DomainSharedProjectionRebuildRequest rebuild,
            CancellationToken cancellationToken)
        {
            using HttpRequestMessage request = new(HttpMethod.Post, "/project/rebuild/shared/v1")
            {
                Content = JsonContent.Create(rebuild, options: JsonOptions)
            };
            await AddWorkloadHeadersAsync(request, EventStoreWorkloadOperations.DomainServiceProject);
            return await client.SendAsync(request, cancellationToken);
        }

        public async Task<HttpResponseMessage> ForwardCoordinatorProjectionAsync(
            HttpClient client, HttpRequestMessage source, CancellationToken cancellationToken)
        {
            string path = source.RequestUri?.AbsolutePath
                ?? throw new InvalidOperationException("The coordinator request has no route.");
            int routeStart = path.IndexOf("/project/", StringComparison.Ordinal);
            if (routeStart < 0)
            {
                throw new InvalidOperationException("The coordinator request is outside projection routes.");
            }

            using HttpRequestMessage request = new(source.Method, path[routeStart..]);
            if (source.Content is not null)
            {
                request.Content = new ByteArrayContent(
                    await source.Content.ReadAsByteArrayAsync(cancellationToken));
                request.Content.Headers.ContentType = source.Content.Headers.ContentType;
            }

            await AddWorkloadHeadersAsync(request, EventStoreWorkloadOperations.DomainServiceProject);
            return await client.SendAsync(request, cancellationToken);
        }

        public async Task<HttpResponseMessage> PostProtectedManagementAsync<T>(HttpClient client, string route, T body,
            string operation = EventStoreWorkloadOperations.DomainServiceProcess)
        {
            using HttpRequestMessage request = new(HttpMethod.Post, route)
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            };
            await AddWorkloadHeadersAsync(request, operation);
            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        private static ITimesheetsTenantAccessValidator CreateTenantAccessValidator()
        {
            ITimesheetsTenantAccessValidator validator = Substitute.For<ITimesheetsTenantAccessValidator>();
            validator.ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimesheetsOperation>(), Arg.Any<CancellationToken>())
                .Returns(TimesheetsTenantAccessResult.Authorized());
            return validator;
        }

        private async Task AddWorkloadHeadersAsync(HttpRequestMessage request, string operation, string? assertion = null)
        {
            assertion ??= await IssueAssertionAsync(operation);
            request.Headers.Add(DaprAppChannelToken.HeaderName, ChannelToken);
            request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);
        }

        public async Task<string> IssueAssertionAsync(string operation)
        {
            DomainProjectionIdentityOptions identity = Services
                .GetRequiredService<IOptions<DomainProjectionIdentityOptions>>()
                .Value;
            return (await Services.GetRequiredService<IWorkloadAssertionIssuer>()
                .IssueAsync(new WorkloadAssertionRequest(identity.AppId, operation),
                    TestContext.Current.CancellationToken)).ShouldNotBeNull();
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

        public Func<CancellationToken, ValueTask>? OnAuthorizeAsync { get; set; }

        /// <summary>Overrides the gate decision to distinguish coarse and resource-level refusals.</summary>
        public Func<TimesheetsAuthorizationRequest, TimesheetsAuthorizationDecision>? DecisionOverride { get; set; }

        /// <summary>Gets the last request passed to the service's authorization boundary.</summary>
        public TimesheetsAuthorizationRequest? LastAuthorizationRequest { get; private set; }

        public async ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
            TimesheetsAuthorizationRequest request,
            CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref _authorizationCount);
            LastAuthorizationRequest = request;
            if (OnAuthorizeAsync is { } gate)
            {
                await gate(cancellationToken).ConfigureAwait(false);
            }

            return Decision(request);
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

        private TimesheetsAuthorizationDecision Decision(TimesheetsAuthorizationRequest request)
            => DecisionOverride?.Invoke(request) ?? (request.Contributor == UnauthorizedContributor()
                ? TimesheetsAuthorizationDecision.Denied(TimesheetsDenialCategory.UnconfiguredPolicy, "Denied by test access guard.")
                : TimesheetsAuthorizationDecision.Allowed());
    }

    private sealed class StaticTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class ProjectionBackedReadModelStore : IReadModelStore, IReadModelBatchStore
    {
        private readonly InMemoryReadModelStore _inner = new();

        public int DirectCatalogSeedCount { get; private set; }

        public int DirectIndexSeedCount { get; private set; }

        /// <summary>Gets the read-model keys requested through the loader's store seam.</summary>
        public List<string> ReadKeys { get; } = [];

        public bool Contains(string key)
            => _inner.GetAsync<object>(MagicLinkTokenHashCapabilityIndexProjection.StateStoreName, key)
                .GetAwaiter().GetResult().Value is not null;

        public T Get<T>(string key)
            where T : class
            => _inner.GetAsync<T>(MagicLinkTokenHashCapabilityIndexProjection.StateStoreName, key)
                .GetAwaiter().GetResult().Value ?? throw new KeyNotFoundException(key);

        public byte[]? InspectRawBytes(string storeName, string key) => _inner.SnapshotRaw(storeName, key);

        public bool ContainsSelectedCatalog(TenantReference tenant)
            => InspectSelectedAsync<ActivityTypeCatalogReadModel>(MagicLinkActivityTypeCatalogReadModelAddress.StateStoreName,
                MagicLinkActivityTypeCatalogReadModelAddress.StateKey(tenant)).GetAwaiter().GetResult().Value is not null;

        public ActivityTypeCatalogReadModel GetSelectedCatalog(TenantReference tenant)
            => InspectSelectedAsync<ActivityTypeCatalogReadModel>(MagicLinkActivityTypeCatalogReadModelAddress.StateStoreName,
                MagicLinkActivityTypeCatalogReadModelAddress.StateKey(tenant)).GetAwaiter().GetResult().Value
                ?? throw new KeyNotFoundException("The selected catalog is missing.");

        public Task<ReadModelEntry<TValue>> InspectSelectedAsync<TValue>(string storeName, string logicalKey,
            CancellationToken cancellationToken = default) where TValue : class
            => _inner.GetAsync<TValue>(storeName, SelectedKey(logicalKey), cancellationToken);

        public Task<bool> MutateSelectedAsync<TValue>(string storeName, string logicalKey, TValue value, string etag,
            CancellationToken cancellationToken = default) where TValue : class
            => _inner.TrySaveAsync(storeName, SelectedKey(logicalKey), value, etag, cancellationToken);

        // Test inspection/mutation helpers accept the canonical logical address and resolve the
        // same selected generation as production. Epoch infrastructure reads already use physical keys.
        private string SelectedKey(string key)
        {
            const string prefix = "timesheets:magic-links:activity-type-catalog:";
            if (!key.StartsWith(prefix, StringComparison.Ordinal) || !key.EndsWith(":v1", StringComparison.Ordinal))
                return key;
            string tenantId = key[prefix.Length..^3];
            SharedProjectionScope scope = TenantActivityTypeCatalogEpoch.CreateScope(tenantId);
            string hash = scope.ComputeHash();
            JsonObject? state = _inner.GetAsync<JsonObject>(scope.StoreName,
                "shared-epoch:" + hash).GetAwaiter().GetResult().Value;
            return state is null ? key : $"shared-generation:{hash}:{state["activeGeneration"]!.GetValue<long>()}:"
                + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        }

        public MagicLinkTokenHashCapabilityIndexReadModel GetSelectedIndex()
        {
            var coordinator = new SharedProjectionEpochCoordinator(_inner, _inner);
            SharedProjectionScope scope = MagicLinkTokenHashCapabilityIndexProjection.GlobalScope();
            SharedProjectionReadResult<MagicLinkTokenHashCapabilityIndexTreeRoot> tree = coordinator
                .ReadAsync<MagicLinkTokenHashCapabilityIndexTreeRoot>(scope,
                    MagicLinkTokenHashCapabilityIndexTreeReader.RootKey)
                .GetAwaiter().GetResult();
            tree.IsAvailable.ShouldBeTrue();
            if (tree.Value is not null)
            {
                var reader = new MagicLinkTokenHashCapabilityIndexTreeReader(coordinator, scope);
                var rows = new List<KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>>();
                var visited = new HashSet<(long Generation, long NodeId)>();
                void Visit(MagicLinkTokenHashCapabilityIndexNodeReference? reference)
                {
                    if (reference is null) return;
                    visited.Add((reference.Generation, reference.NodeId)).ShouldBeTrue();
                    MagicLinkTokenHashCapabilityIndexTreeNode node = reader.ReadNodeAsync(reference)
                        .GetAwaiter().GetResult();
                    if (node.Row is not null)
                        rows.Add(new KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>(
                            node.Row.TokenHash, node.Row.Candidate));
                    foreach (MagicLinkTokenHashCapabilityIndexNodeReference child in node.Children.Values)
                        Visit(child);
                }
                Visit(tree.Value.TokenRoot);
                return MagicLinkTokenHashCapabilityIndexProjection.FromContributions(rows);
            }
            var contributions = new List<KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>>();
            for (int shard = 0; shard < MagicLinkTokenHashCapabilityIndexProjection.ShardCount; shard++)
            {
                SharedProjectionReadResult<MagicLinkTokenHashCapabilityIndexReadModel> selected = coordinator
                    .ReadAsync<MagicLinkTokenHashCapabilityIndexReadModel>(
                        MagicLinkTokenHashCapabilityIndexProjection.GlobalScope(),
                        MagicLinkTokenHashCapabilityIndexProjection.ShardKey(shard))
                    .GetAwaiter().GetResult();
                selected.IsAvailable.ShouldBeTrue();
                contributions.AddRange(MagicLinkTokenHashCapabilityIndexProjection.Expand(selected.Value));
                if (selected.Value is null) continue;
                foreach ((string hash, int pageCount) in selected.Value.CandidatePageCounts)
                {
                    IReadOnlyList<int> pageIds = selected.Value.CandidatePageIds[hash];
                    pageIds.Count.ShouldBe(pageCount);
                    foreach (int pageId in pageIds)
                    {
                        SharedProjectionReadResult<MagicLinkTokenHashCapabilityIndexPage> selectedPage =
                            coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexPage>(
                                MagicLinkTokenHashCapabilityIndexProjection.GlobalScope(),
                                MagicLinkTokenHashCapabilityIndexProjection.CandidatePageKey(shard, pageId))
                            .GetAwaiter().GetResult();
                        selectedPage.IsAvailable.ShouldBeTrue();
                        selectedPage.Value.ShouldNotBeNull().TokenHash.ShouldBe(hash);
                        contributions.AddRange(selectedPage.Value.ShouldNotBeNull().Candidates.Select(candidate =>
                            new KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>(hash, candidate)));
                    }
                }
                foreach (MagicLinkTokenHashCapabilityIndexDirectoryReference reference in selected.Value.DirectoryPages)
                {
                    SharedProjectionReadResult<MagicLinkTokenHashCapabilityIndexDirectoryPage> selectedPage =
                        coordinator.ReadAsync<MagicLinkTokenHashCapabilityIndexDirectoryPage>(
                            MagicLinkTokenHashCapabilityIndexProjection.GlobalScope(),
                            MagicLinkTokenHashCapabilityIndexProjection.DirectoryPageKey(shard, reference.PageId))
                        .GetAwaiter().GetResult();
                    selectedPage.IsAvailable.ShouldBeTrue();
                    MagicLinkTokenHashCapabilityIndexDirectoryEntry[] rows = [.. selectedPage.Value
                        .ShouldNotBeNull().Entries];
                    rows[0].TokenHash.ShouldBe(reference.FirstHash);
                    rows[^1].TokenHash.ShouldBe(reference.LastHash);
                    contributions.AddRange(rows.Select(static row =>
                        new KeyValuePair<string, MagicLinkTokenHashCapabilityIndexEntry>(
                            row.TokenHash, row.Candidate)));
                }
            }

            return MagicLinkTokenHashCapabilityIndexProjection.FromContributions(contributions);
        }

        public Task<ReadModelEntry<TValue>> GetAsync<TValue>(
            string storeName,
            string key,
            CancellationToken cancellationToken = default)
            where TValue : class
        {
            ReadKeys.Add(key);
            return _inner.GetAsync<TValue>(storeName, key, cancellationToken);
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

            return _inner.SaveAsync(storeName, key, value, cancellationToken);
        }

        public Task<bool> TrySaveAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            string etag,
            CancellationToken cancellationToken = default)
            where TValue : class
        {
            return _inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
        }

        public Task<ReadModelBatchResult> ExecuteAsync(ReadModelBatch batch,
            CancellationToken cancellationToken = default)
            => _inner.ExecuteAsync(batch, cancellationToken);
    }

    private sealed class ScriptedEventStoreGateway(Func<IServiceProvider> services) : IEventStoreGatewayClient
    {
        private readonly SemaphoreSlim _submissionGate = new(1, 1);
        private readonly object _stateGate = new();
        private readonly Dictionary<string, CommandStatusQueryResponse> _statuses = new(StringComparer.Ordinal);
        private readonly Dictionary<(string Tenant, string Aggregate), StreamReadEvent[]> _streams = [];
        private readonly Dictionary<(string Tenant, string Aggregate), string> _missingStreamReasons = [];

        public CommandStatus? OverrideSuccessfulStatus { get; set; }

        public int CompletedStatusReadsToDelay { get; set; }

        public Func<CommandStatusQueryResponse, CommandStatusQueryResponse>? StatusTransform { get; set; }

        public Func<string, string>? AcceptedMessageIdTransform { get; set; }

        public Func<object, object>? StoredPayloadTransform { get; set; }

        public Func<StreamReadEvent, StreamReadEvent>? StoredEventTransform { get; set; }

        /// <summary>Optionally coordinates a test's submissions before any events are stored.</summary>
        public Func<SubmitCommandRequest, CancellationToken, Task>? BeforeSubmitAsync { get; set; }

        public Func<SubmitCommandRequest, CancellationToken, Task>? AfterStoredBeforeResponseAsync { get; set; }

        public int TransientWorkloadStatusFailures { get; set; }

        public bool StatusUnavailableUntilSubmission { get; set; }

        public int TransientCommitBatchReadFailures { get; set; }

        public int TransientIssueReadFailures { get; set; }

        public int? TransientReadFailureStatusCode { get; set; }

        public int StaleIssueReadbacks { get; set; }

        public int StaleCommitBatchReadbacks { get; set; }

        public Task<CommandStatusQueryResponse?> GetCommandStatusAsync(
            string messageId,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Magic-link submissions must use the workload status route.");

        public Task<CommandStatusQueryResponse?> GetWorkloadCommandStatusAsync(
            string tenant,
            string messageId,
            CancellationToken cancellationToken = default)
        {
            lock (_stateGate)
            {
                if (StatusUnavailableUntilSubmission && SubmissionCount == 0)
                {
                    return Task.FromException<CommandStatusQueryResponse?>(
                        new EventStoreGatewayException(503, "Status preflight unavailable"));
                }

                if (TransientWorkloadStatusFailures > 0)
                {
                    TransientWorkloadStatusFailures--;
                    return Task.FromException<CommandStatusQueryResponse?>(
                        new EventStoreGatewayException(500, "Transient status fault"));
                }

                CommandStatusQueryResponse? status = _statuses.GetValueOrDefault(messageId);
                if (status?.Status == nameof(CommandStatus.Completed) && CompletedStatusReadsToDelay > 0)
                {
                    CompletedStatusReadsToDelay--;
                    status = status with
                    {
                        Status = nameof(CommandStatus.Processing),
                        StatusCode = (int)CommandStatus.Processing
                    };
                }
                return Task.FromResult(status?.TenantId == tenant
                    ? StatusTransform?.Invoke(status) ?? status
                    : null);
            }
        }

        public List<StreamReadRequest> Requests { get; } = [];

        private int _submissionCount;

        public int SubmissionCount => Volatile.Read(ref _submissionCount);

        public void WithStream(string tenant, string aggregate, params StreamReadEvent[] events)
        {
            lock (_stateGate)
            {
                _streams[(tenant, aggregate)] = events;
            }
        }

        public void ReportMissingStream(string tenant, string aggregate, string reasonCode)
        {
            lock (_stateGate)
            {
                _streams.Remove((tenant, aggregate));
                _missingStreamReasons[(tenant, aggregate)] = reasonCode;
            }
        }

        public StreamReadEvent[] StoredEvents(string tenant, string aggregate)
        {
            lock (_stateGate)
            {
                return _streams.GetValueOrDefault((tenant, aggregate))?.ToArray() ?? [];
            }
        }

        public Task<SubmitCommandResponse> SubmitCommandAsync(
            SubmitCommandRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Magic-link submissions must use the workload command route.");

        private async Task<SubmitCommandResponse> SubmitScriptedCommandAsync(
            SubmitCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            _ = Interlocked.Increment(ref _submissionCount);
            await _submissionGate.WaitAsync(cancellationToken);
            try
            {
                if (_statuses.ContainsKey(request.MessageId))
                {
                    return new SubmitCommandResponse(request.CorrelationId ?? request.MessageId, MessageId: request.MessageId);
                }

                using IServiceScope scope = services().CreateScope();
                MagicLinkEventStoreDomainProcessor processor = scope.ServiceProvider
                    .GetRequiredService<MagicLinkEventStoreDomainProcessor>();
                object? currentState = CurrentState(request);
                string? actor = scope.ServiceProvider.GetRequiredService<ITimesheetsTrustedContextAccessor>()
                    .CurrentActor?.PartyId;
                Dictionary<string, string> verifiedExtensions = new(StringComparer.Ordinal)
                {
                    [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "timesheets"
                };
                if (!string.IsNullOrWhiteSpace(actor))
                {
                    verifiedExtensions[EventStoreGatewayVerifiedOrigin.ActorExtensionKey] = actor;
                }
                var envelope = new CommandEnvelope(
                    request.MessageId,
                    request.Tenant,
                    request.Domain,
                    request.AggregateId,
                    request.CommandType,
                    Encoding.UTF8.GetBytes(request.Payload.GetRawText()),
                    request.CorrelationId ?? request.MessageId,
                    null,
                    actor ?? "timesheets",
                    verifiedExtensions);
                var result = await processor.ProcessAsync(envelope, currentState, cancellationToken);
                var key = (request.Tenant, request.AggregateId);
                StreamReadEvent[] before = StoredEvents(request.Tenant, request.AggregateId);
                CommandStatus status = result.IsSuccess
                    ? OverrideSuccessfulStatus ?? CommandStatus.Completed
                    : CommandStatus.Rejected;
                bool eventsStored = result.IsRejection
                    || (result.IsSuccess && status is CommandStatus.Completed or CommandStatus.EventsStored or CommandStatus.PublishFailed);
                if (eventsStored)
                {
                    List<StreamReadEvent> stored = [.. before];
                    for (int index = 0; index < result.Events.Count; index++)
                    {
                        object payload = StoredPayloadTransform?.Invoke(result.Events[index]) ?? result.Events[index];
                        StreamReadEvent persisted = new(
                            stored.Count + 1,
                            payload.GetType().FullName!,
                            JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType(), JsonOptions),
                            "json",
                            1,
                            $"{request.MessageId}-{index}",
                            request.CorrelationId ?? request.MessageId,
                            request.MessageId,
                            ObservedAtUtc,
                            "fixture");
                        stored.Add(StoredEventTransform?.Invoke(persisted) ?? persisted);
                    }

                    lock (_stateGate)
                    {
                        _streams[key] = [.. stored];
                    }
                }

                CommandStatusQueryResponse commandStatus = new(
                    request.CorrelationId ?? request.MessageId,
                    status.ToString(),
                    (int)status,
                    result.IsRejection ? result.Events[0].GetType().FullName : null,
                    request.MessageId)
                {
                    CommittedEventSequence = eventsStored
                        ? before.Length + result.Events.Count : null,
                    EventCount = eventsStored
                        ? result.Events.Count : 0,
                    TenantId = request.Tenant,
                    Domain = request.Domain,
                    AggregateId = request.AggregateId
                };
                lock (_stateGate)
                {
                    _statuses[request.MessageId] = commandStatus;
                }
                return new SubmitCommandResponse(request.CorrelationId ?? request.MessageId,
                    MessageId: AcceptedMessageIdTransform?.Invoke(request.MessageId) ?? request.MessageId);
            }
            finally
            {
                _submissionGate.Release();
            }
        }

        public async Task<SubmitCommandResponse> SubmitWorkloadCommandAsync(
            SubmitCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            if (BeforeSubmitAsync is { } beforeSubmit)
            {
                await beforeSubmit(request, cancellationToken);
            }

            SubmitCommandResponse receipt = await SubmitScriptedCommandAsync(request, cancellationToken);
            if (AfterStoredBeforeResponseAsync is { } afterStored)
            {
                await afterStored(request, cancellationToken);
            }

            return receipt;
        }

        private object? CurrentState(SubmitCommandRequest request)
        {
            StreamReadEvent[] events = StoredEvents(request.Tenant, request.AggregateId);
            if (events.Length == 0)
            {
                return null;
            }

            // Match the EventStore actor's snapshot-aware domain-service handoff, rather than
            // handing the processor a pre-folded state from this fixture.
            EventEnvelope[] envelopes = events.Select(item => new EventEnvelope(
                new EventMetadata(
                    item.MessageId, request.AggregateId, "timesheets", request.Tenant, request.Domain,
                    item.SequenceNumber, item.SequenceNumber, item.Timestamp,
                    item.CorrelationId ?? request.MessageId, item.CausationId ?? request.MessageId,
                    item.UserId ?? "fixture", "fixture", item.EventTypeName,
                    item.MetadataVersion, item.SerializationFormat),
                item.Payload, null)).ToArray();
            return new DomainServiceCurrentState(null, envelopes, 0, events[^1].SequenceNumber);
        }

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
            => throw new InvalidOperationException("Magic-link reads must use the workload stream route.");

        private Task<StreamReadPage> ReadScriptedStreamAsync(
            StreamReadRequest request,
            CancellationToken cancellationToken = default)
        {
            StreamReadEvent[] all;
            lock (_stateGate)
            {
                Requests.Add(request);
                all = request.AggregateId is not null
                    && _streams.TryGetValue((request.Tenant, request.AggregateId), out StreamReadEvent[]? stream)
                        ? stream
                        : [];
            }
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

        public Task<StreamReadPage> ReadWorkloadStreamAsync(
            StreamReadRequest request,
            CancellationToken cancellationToken = default)
        {
            lock (_stateGate)
            {
                if (request.PageSize == 1 && TransientIssueReadFailures > 0)
                {
                    TransientIssueReadFailures--;
                    return Task.FromException<StreamReadPage>(
                        new EventStoreGatewayException(TransientReadFailureStatusCode ?? 503, "Transient issue read fault"));
                }

                if (request.PageSize == 1 && StaleIssueReadbacks > 0)
                {
                    StaleIssueReadbacks--;
                    return SuppressReadbackEventsAsync(request, cancellationToken);
                }

                if (request.FromSequence >= 1 && request.PageSize == 2
                    && TransientCommitBatchReadFailures > 0)
                {
                    TransientCommitBatchReadFailures--;
                    return Task.FromException<StreamReadPage>(
                        new EventStoreGatewayException(TransientReadFailureStatusCode ?? 429, "Transient batch read fault"));
                }

                if (request.FromSequence >= 1 && request.PageSize == 2
                    && StaleCommitBatchReadbacks > 0)
                {
                    StaleCommitBatchReadbacks--;
                    return SuppressReadbackEventsAsync(request, cancellationToken);
                }

                if (request.AggregateId is not null
                    && !_streams.ContainsKey((request.Tenant, request.AggregateId))
                    && _missingStreamReasons.TryGetValue((request.Tenant, request.AggregateId), out string? reason))
                {
                    return Task.FromException<StreamReadPage>(
                        new EventStoreGatewayException(404, "Not Found", reasonCode: reason));
                }
            }

            return ReadScriptedStreamAsync(request, cancellationToken);
        }

        private async Task<StreamReadPage> SuppressReadbackEventsAsync(
            StreamReadRequest request, CancellationToken cancellationToken)
        {
            StreamReadPage page = await ReadScriptedStreamAsync(request, cancellationToken).ConfigureAwait(false);
            return page with { Events = [] };
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

    private sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private readonly object _sync = new();
        private readonly List<LogRecord> _records = [];
        private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();

        public bool ThrowBoundaryDiagnostics { get; set; }

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

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _records, _sync, () => _scopeProvider, () => ThrowBoundaryDiagnostics);

        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(
        string category,
        List<LogRecord> records,
        object sync,
        Func<IExternalScopeProvider> scopeProvider, Func<bool> throwBoundaryDiagnostics) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => scopeProvider().Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (throwBoundaryDiagnostics() && category == "Hexalith.Timesheets.MagicLinkBoundary")
                throw new IOException("Diagnostic provider unavailable.");
            string[] structuredState = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.Select(static value => $"{value.Key}={value.Value}").ToArray()
                : [];
            List<string> scopes = [];
            scopeProvider().ForEachScope(static (scope, captured) =>
            {
                if (scope is IEnumerable<KeyValuePair<string, object?>> fields)
                {
                    captured.AddRange(fields.Select(static field => $"{field.Key}={field.Value}"));
                }
                else
                {
                    captured.Add(scope?.ToString() ?? string.Empty);
                }
            }, scopes);

            lock (sync)
            {
                records.Add(new LogRecord(category, formatter(state, exception), structuredState)
                {
                    Scopes = [.. scopes],
                    Exception = exception?.ToString(),
                    Level = logLevel
                });
            }
        }
    }

    private sealed record LogRecord(string Category, string Message, string[] State)
    {
        public string[] Scopes { get; init; } = [];

        public string? Exception { get; init; }

        public LogLevel Level { get; init; }
    }
}
