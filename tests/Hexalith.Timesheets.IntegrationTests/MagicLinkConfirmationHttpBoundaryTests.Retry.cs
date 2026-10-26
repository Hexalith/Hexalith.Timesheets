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
        }
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
