using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks;

using Hexalith.Timesheets.Server.Authorization;
using Microsoft.Extensions.DependencyInjection;

using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Server.Policies;
using NSubstitute;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed partial class MagicLinkConfirmationHttpBoundaryTests
{
    /// <summary>Rejects invalid retry keys without consuming the capability and accepts the documented maximum length.</summary>
    /// <param name="adjust">Whether to exercise adjustment instead of confirmation.</param>
    /// <param name="scenario">The invalid header boundary.</param>
    [Theory]
    [InlineData(false, "empty")]
    [InlineData(true, "empty")]
    [InlineData(false, "whitespace")]
    [InlineData(true, "whitespace")]
    [InlineData(false, "control")]
    [InlineData(true, "control")]
    [InlineData(false, "too-long")]
    [InlineData(true, "too-long")]
    public async Task InvalidIdempotencyKeyPreservesCapabilityAndMaximumLengthKeySucceeds(bool adjust, string scenario)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        string token = adjust ? ValidAdjustToken() : ValidConfirmToken();
        var capability = new MagicLinkCapabilityId("key-boundary-capability");
        var entry = new TimeEntryId("key-boundary-entry");
        await factory.ProjectValidStateAsync(client, token,
            adjust ? MagicLinkAllowedAction.Adjust : MagicLinkAllowedAction.Confirm, capability, entry);
        string before = JsonSerializer.Serialize(factory.Gateway.StoredEvents(Tenant().TenantId, entry.Value));
        async Task<HttpResponseMessage> Send(string key)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"/api/timesheets/magic-links/{(adjust ? "adjust" : "confirm")}/submit?t={token}")
            {
                Content = JsonContent.Create(adjust ? (object)AdjustCommand() : new ConfirmTimeThroughMagicLink(), options: JsonOptions)
            };
            request.Headers.TryAddWithoutValidation("Idempotency-Key", key).ShouldBeTrue();
            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }
        string invalid = scenario switch
        {
            "empty" => "",
            "whitespace" => "   ",
            "control" => "key\tvalue",
            "too-long" => new string('k', 257),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        HttpStatusCode refusedStatus;
        if (scenario == "empty")
        {
            // HttpClient omits an empty header; preserve explicit header presence at the mapped TestServer boundary.
            using var body = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(
                adjust ? (object)AdjustCommand() : new ConfirmTimeThroughMagicLink(), JsonOptions));
            var context = await factory.Server.SendAsync(context =>
            {
                context.Request.Method = "POST";
                context.Request.Path = $"/api/timesheets/magic-links/{(adjust ? "adjust" : "confirm")}/submit";
                context.Request.QueryString = new Microsoft.AspNetCore.Http.QueryString($"?t={token}");
                context.Request.Headers["Idempotency-Key"] = string.Empty;
                context.Request.ContentType = "application/json";
                context.Request.ContentLength = body.Length;
                context.Request.Body = body;
                context.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpRequestBodyDetectionFeature>(new HttpBoundaryRequestBodyDetectionFeature());
            }, TestContext.Current.CancellationToken);
            refusedStatus = (HttpStatusCode)context.Response.StatusCode;
        }
        else
        {
            using HttpResponseMessage refused = await Send(invalid);
            refusedStatus = refused.StatusCode;
        }
        refusedStatus.ShouldBe(HttpStatusCode.Forbidden);
        factory.Gateway.SubmissionCount.ShouldBe(0);
        JsonSerializer.Serialize(factory.Gateway.StoredEvents(Tenant().TenantId, entry.Value)).ShouldBe(before);
        using HttpResponseMessage accepted = await Send(new string('k', 256));
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        factory.Gateway.SubmissionCount.ShouldBe(1);
        factory.Gateway.StoredEvents(Tenant().TenantId, entry.Value).Length.ShouldBe(3);
    }

    /// <summary>Denies a keyed receipt whose stored comment differs from unchanged caller intent.</summary>
    /// <param name="reconcile">Whether the first response is opaque and verification occurs on retry.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KeyedAdjustmentReceiptRejectsMismatchingStoredComment(bool reconcile)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capability = new MagicLinkCapabilityId("keyed-comment-tamper-capability");
        var entry = new TimeEntryId("keyed-comment-tamper-entry");
        await factory.ProjectValidStateAsync(client, ValidAdjustToken(), MagicLinkAllowedAction.Adjust, capability, entry);
        factory.Gateway.StoredPayloadTransform = payload => payload is StoredTimeEntryAdjusted stored
            ? new StoredTimeEntryAdjusted(stored.Event with
            {
                AdjustedValues = stored.Event.AdjustedValues with
                {
                    Comment = new TimeEntryComment("different persisted note", stored.Event.AdjustedValues.Comment!.Policy)
                }
            }) : payload;
        var command = AdjustCommand() with
        {
            Comment = new TimeEntryComment("unchanged caller note", TimeEntryCommentPolicy.SensitiveDefault)
        };
        async Task<HttpResponseMessage> Send()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"/api/timesheets/magic-links/adjust/submit?t={ValidAdjustToken()}")
            { Content = JsonContent.Create(command, options: JsonOptions) };
            request.Headers.Add("Idempotency-Key", "unchanged-comment-key");
            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }
        if (reconcile)
        {
            factory.Gateway.StatusTransform = status => status with
            { Status = nameof(CommandStatus.Processing), StatusCode = (int)CommandStatus.Processing };
        }
        using HttpResponseMessage first = await Send();
        first.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        StreamReadEvent[] committed = factory.Gateway.StoredEvents(Tenant().TenantId, entry.Value);
        committed.Length.ShouldBe(3);
        JsonSerializer.Deserialize<StoredTimeEntryAdjusted>(committed[2].Payload, JsonOptions)!
            .Event.AdjustedValues.Comment!.Text.ShouldBe("different persisted note");
        factory.Gateway.StatusTransform = null;
        using HttpResponseMessage retry = await Send();
        CapturedFailure denied = await CaptureFailureAsync(retry, "stored-comment-mismatch", ValidAdjustToken());
        using HttpResponseMessage unknown = await client.PostAsJsonAsync(
            "/api/timesheets/magic-links/adjust/submit?t=unknown-comment-retry", command, TestContext.Current.CancellationToken);
        CapturedFailure baseline = await CaptureFailureAsync(unknown, "unknown", "unknown-comment-retry");
        denied.NormalizedBody.ShouldBe(baseline.NormalizedBody);
        denied.Headers.ShouldBe(baseline.Headers);
        factory.Gateway.SubmissionCount.ShouldBe(1);
        JsonSerializer.Serialize(factory.Gateway.StoredEvents(Tenant().TenantId, entry.Value)).ShouldBe(JsonSerializer.Serialize(committed));
    }

    /// <summary>Rejects ambiguous retry identities before consuming an otherwise valid capability.</summary>
    /// <param name="adjust">Whether to exercise adjustment instead of confirmation.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MultipleIdempotencyKeyValuesDenyWithoutSubmission(bool adjust)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        string token = adjust ? ValidAdjustToken() : ValidConfirmToken();
        var capability = new MagicLinkCapabilityId("ambiguous-retry-capability");
        var entry = new TimeEntryId("ambiguous-retry-entry");
        await factory.ProjectValidStateAsync(client, token,
            adjust ? MagicLinkAllowedAction.Adjust : MagicLinkAllowedAction.Confirm, capability, entry);
        string before = JsonSerializer.Serialize(factory.Gateway.StoredEvents(Tenant().TenantId, entry.Value));
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/timesheets/magic-links/{(adjust ? "adjust" : "confirm")}/submit?t={token}")
        {
            Content = JsonContent.Create(adjust ? (object)AdjustCommand() : new ConfirmTimeThroughMagicLink(),
                options: JsonOptions)
        };
        request.Headers.Add("Idempotency-Key", new[] { "first-key", "second-key" });

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.Gateway.SubmissionCount.ShouldBe(0);
        JsonSerializer.Serialize(factory.Gateway.StoredEvents(Tenant().TenantId, entry.Value)).ShouldBe(before);

        using HttpResponseMessage valid = await client.PostAsJsonAsync(
            $"/api/timesheets/magic-links/{(adjust ? "adjust" : "confirm")}/submit?t={token}",
            adjust ? (object)AdjustCommand() : new ConfirmTimeThroughMagicLink(), JsonOptions,
            TestContext.Current.CancellationToken);
        valid.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        factory.Gateway.SubmissionCount.ShouldBe(1);
        factory.Gateway.StoredEvents(Tenant().TenantId, entry.Value).Length.ShouldBe(3);
    }

    /// <summary>Reconciles original comment decisions and replacement activity after an opaque commit.</summary>
    /// <param name="replacement">Whether the committed adjustment changes Activity Type.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KeyedAdjustmentReconcilesReplacementActivityAndOriginalCommentAcrossPolicyChange(bool replacement)
    {
        bool allowPublication = true;
        var authority = Substitute.For<ITimeEntryCommentPublicationAuthority>();
        authority.MayPublish(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryComment>()).Returns(_ => allowPublication);
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true, commentAuthority: authority);
        using HttpClient client = factory.CreateClient();
        var capability = new MagicLinkCapabilityId("retry-comment-capability");
        var entry = new TimeEntryId("retry-comment-entry");
        var activity = new ActivityTypeId("retry-replacement-activity");
        await factory.ProjectValidStateAsync(client, ValidAdjustToken(), MagicLinkAllowedAction.Adjust,
            capability, entry, replacementActivityTypeId: replacement ? activity : null);
        var command = AdjustCommand() with
        {
            ActivityTypeId = replacement ? activity : ActivityId(),
            Comment = new TimeEntryComment("original private retry note", TimeEntryCommentPolicy.SensitiveDefault)
        };
        async Task<HttpResponseMessage> Send(AdjustTimeThroughMagicLink body, string token = "valid-adjust-token")
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/timesheets/magic-links/adjust/submit?t={token}")
            { Content = JsonContent.Create(body, options: JsonOptions) };
            request.Headers.Add("Idempotency-Key", "retained-comment-key");
            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }
        factory.Gateway.StatusTransform = status => status with
        { Status = nameof(CommandStatus.Processing), StatusCode = (int)CommandStatus.Processing };
        using HttpResponseMessage opaque = await Send(command);
        opaque.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        StreamReadEvent[] committed = factory.Gateway.StoredEvents(Tenant().TenantId, entry.Value);
        committed.Length.ShouldBe(3);
        var effect = JsonSerializer.Deserialize<StoredTimeEntryAdjusted>(committed[2].Payload, JsonOptions)!.Event;
        effect.AdjustedValues.ActivityTypeId.ShouldBe(command.ActivityTypeId);
        effect.AdjustedValues.Comment!.Policy.ServerProjectionInclusion.ShouldBe(TimesheetsCommentPolicyDecision.Allowed);
        allowPublication = false;
        authority.ClearReceivedCalls();
        factory.Gateway.StatusTransform = null;
        using HttpResponseMessage retry = await Send(command);
        retry.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        authority.DidNotReceive().MayPublish(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryComment>());
        factory.Gateway.SubmissionCount.ShouldBe(1);
        JsonSerializer.Serialize(factory.Gateway.StoredEvents(Tenant().TenantId, entry.Value)).ShouldBe(JsonSerializer.Serialize(committed));
        using HttpResponseMessage conflict = await Send(command with { Comment = new TimeEntryComment("different private note", command.Comment!.Policy) });
        conflict.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using HttpResponseMessage policyConflict = await Send(command with
        {
            Comment = new TimeEntryComment(command.Comment!.Text,
                command.Comment.Policy with { ExportInclusion = TimesheetsCommentPolicyDecision.Allowed })
        });
        policyConflict.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        JsonSerializer.Serialize(factory.Gateway.StoredEvents(Tenant().TenantId, entry.Value)).ShouldBe(JsonSerializer.Serialize(committed));
        using HttpResponseMessage newUse = await client.PostAsJsonAsync($"/api/timesheets/magic-links/adjust/submit?t={ValidAdjustToken()}", command, TestContext.Current.CancellationToken);
        newUse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.Gateway.SubmissionCount.ShouldBe(1);
        // A new authorized write still obtains today's server-issued publication policy.
        const string NextToken = "fresh-comment-policy-token";
        var nextEntry = new TimeEntryId("fresh-comment-policy-entry");
        await factory.ProjectValidStateAsync(client, NextToken, MagicLinkAllowedAction.Adjust,
            new("fresh-comment-policy-capability"), nextEntry);
        using HttpResponseMessage fresh = await Send(command with { ActivityTypeId = ActivityId() }, NextToken);
        fresh.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var nextEffect = JsonSerializer.Deserialize<StoredTimeEntryAdjusted>(factory.Gateway.StoredEvents(Tenant().TenantId, nextEntry.Value)[2].Payload, JsonOptions)!.Event;
        nextEffect.AdjustedValues.Comment!.Policy.ServerProjectionInclusion.ShouldBe(TimesheetsCommentPolicyDecision.PolicyRequired);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KeyedHttpRetryReconcilesOpaqueCommittedOriginalEffects(bool adjust)
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        string token = adjust ? ValidAdjustToken() : ValidConfirmToken();
        var capability = new MagicLinkCapabilityId("retry-capability");
        var entry = new TimeEntryId("retry-entry");
        await factory.ProjectValidStateAsync(client, token,
            adjust ? MagicLinkAllowedAction.Adjust : MagicLinkAllowedAction.Confirm, capability, entry);
        factory.Gateway.StatusTransform = status => status with
        { Status = nameof(CommandStatus.Processing), StatusCode = (int)CommandStatus.Processing };
        async Task<HttpResponseMessage> Send(string key, object? body = null, string? suppliedToken = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"/api/timesheets/magic-links/{(adjust ? "adjust" : "confirm")}/submit?t={suppliedToken ?? token}")
            { Content = JsonContent.Create(body ?? (adjust ? (object)AdjustCommand() : new ConfirmTimeThroughMagicLink()), options: JsonOptions) };
            request.Headers.Add("Idempotency-Key", key);
            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }
        using HttpResponseMessage opaque = await Send("retained-key");
        opaque.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        StreamReadEvent[] committed = factory.Gateway.StoredEvents(Tenant().TenantId, entry.Value);
        committed.Length.ShouldBe(3);
        factory.Gateway.StatusTransform = null;
        ((StaticTimeProvider)factory.Services.GetRequiredService<TimeProvider>()).UtcNow = ObservedAtUtc.AddHours(2);
        using HttpResponseMessage retry = await Send("retained-key");
        retry.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        factory.Gateway.SubmissionCount.ShouldBe(1);
        JsonSerializer.Serialize(factory.Gateway.StoredEvents(Tenant().TenantId, entry.Value))
            .ShouldBe(JsonSerializer.Serialize(committed));
        var used = JsonSerializer.Deserialize<StoredMagicLinkUsed>(committed[1].Payload, JsonOptions)!.Event;
        used.ShouldNotBeNull().CapabilityId.ShouldBe(capability);
        if (adjust)
        {
            var effect = JsonSerializer.Deserialize<StoredTimeEntryAdjusted>(committed[2].Payload, JsonOptions)!.Event;
            effect.AdjustedAtUtc.ShouldBe(used.UsedAtUtc);
            effect.PreviousValues.DurationMinutes.ShouldBe(60);
            effect.AdjustedValues.DurationMinutes.ShouldBe(AdjustCommand().DurationMinutes);
            using HttpResponseMessage conflict = await Send("retained-key", AdjustCommand() with { DurationMinutes = 999 });
            conflict.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            foreach (AdjustTimeThroughMagicLink changed in new[]
            {
                AdjustCommand() with { ServiceDate = AdjustCommand().ServiceDate.AddDays(1) },
                AdjustCommand() with { ActivityTypeId = new ActivityTypeId("different-retry-activity") },
                AdjustCommand() with { BillableState = BillableState.Billable }
            })
            {
                using HttpResponseMessage changedIntent = await Send("retained-key", changed);
                changedIntent.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
                JsonSerializer.Serialize(factory.Gateway.StoredEvents(Tenant().TenantId, entry.Value)).ShouldBe(JsonSerializer.Serialize(committed));
            }
        }
        using var oppositeRequest = new HttpRequestMessage(HttpMethod.Post,
            $"/api/timesheets/magic-links/{(adjust ? "confirm" : "adjust")}/submit?t={token}")
        { Content = JsonContent.Create(adjust ? (object)new ConfirmTimeThroughMagicLink() : AdjustCommand(), options: JsonOptions) };
        oppositeRequest.Headers.Add("Idempotency-Key", "retained-key");
        using HttpResponseMessage oppositeAction = await client.SendAsync(oppositeRequest, TestContext.Current.CancellationToken);
        oppositeAction.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        JsonSerializer.Serialize(factory.Gateway.StoredEvents(Tenant().TenantId, entry.Value)).ShouldBe(JsonSerializer.Serialize(committed));
        using HttpResponseMessage foreignKey = await Send("changed-key");
        foreignKey.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using HttpResponseMessage foreignToken = await Send("retained-key", suppliedToken: "unknown-token");
        foreignToken.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.AccessGuard.DecisionOverride = _ => TimesheetsAuthorizationDecision.Denied(TimesheetsDenialCategory.UnconfiguredPolicy, "Denied");
        using HttpResponseMessage denied = await Send("retained-key");
        denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.Gateway.StoredEvents(Tenant().TenantId, entry.Value).Length.ShouldBe(3);
    }

    [Theory]
    [InlineData("used")]
    [InlineData("repeated-token")]
    [InlineData("revoked")]
    [InlineData("expired")]
    public async Task ClientKeyNeverCreatesSuccessForTerminalCapabilityWithoutOriginalReceipt(string terminal)
    {
        using MagicLinkHttpBoundaryFactory factory = new();
        using HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/timesheets/magic-links/confirm/submit?t=confirm-submit-{terminal}")
        { Content = JsonContent.Create(new ConfirmTimeThroughMagicLink()) };
        request.Headers.Add("Idempotency-Key", "uncommitted-key");
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.Gateway.SubmissionCount.ShouldBe(0);
    }

    [Fact]
    public async Task ConcurrentIdenticalKeyedConfirmationsHaveExactlyOnePersistedEffect()
    {
        using MagicLinkHttpBoundaryFactory factory = new(useConcreteLoader: true);
        using HttpClient client = factory.CreateClient();
        var capability = new MagicLinkCapabilityId("retry-concurrent-capability");
        var entry = new TimeEntryId("retry-concurrent-entry");
        await factory.ProjectValidStateAsync(client, ValidConfirmToken(), MagicLinkAllowedAction.Confirm, capability, entry);
        async Task<HttpResponseMessage> Send()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"/api/timesheets/magic-links/confirm/submit?t={ValidConfirmToken()}")
            { Content = JsonContent.Create(new ConfirmTimeThroughMagicLink()) };
            request.Headers.Add("Idempotency-Key", "same-concurrent-key");
            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }
        HttpResponseMessage[] responses = await Task.WhenAll(Send(), Send());
        foreach (HttpResponseMessage response in responses)
        { using (response) response.StatusCode.ShouldBe(HttpStatusCode.Accepted); }
        factory.Gateway.StoredEvents(Tenant().TenantId, entry.Value).Length.ShouldBe(3);
    }
}
