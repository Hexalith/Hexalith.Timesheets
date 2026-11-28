using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Timesheets.Contracts.Commands.ExternalContributions;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.TimeEntries;

using Shouldly;

using ComposedConfirmationHost = Hexalith.Timesheets.IntegrationTests.ExternalContributionConfirmationPersistenceTests.ComposedConfirmationHost;
using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed class ExternalContributionRecordingPersistenceTests
{
    private const string Route = "/api/timesheets/external-contributions/";
    private const string Tenant = "tenant-1";
    private const string Actor = "party-1";
    private const string PrivateNote = "confidential work description";
    private static readonly DateTimeOffset _firstUtc = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(TimeEntryApprovalState.Draft)]
    [InlineData(TimeEntryApprovalState.Submitted)]
    public async Task RecordingReadbackTimeoutIsOpaqueAndRestartRecoversOneBatch(TimeEntryApprovalState initialState)
    {
        var storage = new ConcurrentDictionary<(string Tenant, string Owner), InMemoryStateManager>();
        SubmitExternalTimeEntry command = RecordCommand("delayed-recording");
        string messageId = CreateMessageId(command);
        SubmitExternalTimeEntry rejectedCommand = command with { Source = new("supplier-api", "private-rejected-request") };
        string rejectedMessageId = CreateMessageId(rejectedCommand);
        var policy = new ExternalContributionPolicyOptions { InitialApprovalState = initialState };
        var polling = new MagicLinkSubmissionPollingOptions
        {
            Timeout = TimeSpan.FromMilliseconds(700),
            ReadbackTimeout = TimeSpan.FromMilliseconds(100),
            PollInterval = TimeSpan.FromMilliseconds(20)
        };
        // The stopwatch includes TestServer request dispatch, authentication, and response serialization.
        TimeSpan transportTolerance = TimeSpan.FromMilliseconds(750);
        EventEnvelope[] original;
        using (var host = new ComposedConfirmationHost(storage, policy, polling))
        using (HttpClient client = host.CreateClient())
        {
            host.Clock.UtcNow = _firstUtc;
            await host.SeedFreshCatalogAsync(client, Tenant);
            host.SetSignedBindings("timesheets", actor: null);

            using HttpResponseMessage refusal = await host.PostWorkloadAsync(client, Route, command);
            await AssertOpaqueRefusalAsync(refusal, command, messageId);
            host.BoundaryCalls.ShouldBe((0, 0, 0));
            storage.ShouldBeEmpty();
            host.SetSignedBindings("timesheets", Actor);
            host.AllowAuthorization = false;
            using HttpResponseMessage rejected = await host.PostWorkloadAsync(client, Route, rejectedCommand);
            await AssertOpaqueRefusalAsync(rejected, rejectedCommand, rejectedMessageId);
            (await host.ReadStatusAsync(Tenant, rejectedMessageId)).ShouldNotBeNull().Status.ShouldBe(CommandStatus.Rejected);
            EventEnvelope rejection = (await host.ActorEventsAsync(Tenant, command.TimeEntryId.Value)).ShouldHaveSingleItem();
            rejection.EventTypeName.ShouldBe(typeof(MagicLinkCommitRejected).FullName);
            rejection.CausationId.ShouldBe(rejectedMessageId);
            host.AllowAuthorization = true;

            var pendingRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var readTokens = new ConcurrentQueue<CancellationToken>();
            host.BeforeStreamReadAsync = token =>
            {
                readTokens.Enqueue(token);
                return pendingRead.Task;
            };
            try
            {
                Stopwatch elapsed = Stopwatch.StartNew();
                using HttpResponseMessage timedOut = await host.PostWorkloadAsync(client, Route, command)
                    .WaitAsync(polling.Timeout + transportTolerance, TestContext.Current.CancellationToken);
                elapsed.Elapsed.ShouldBeInRange(polling.Timeout - TimeSpan.FromMilliseconds(25), polling.Timeout + transportTolerance);
                await AssertOpaqueRefusalAsync(timedOut, command, messageId);
                readTokens.ShouldNotBeEmpty();
                readTokens.ShouldAllBe(token => token.IsCancellationRequested);
                host.BoundaryCalls.Submissions.ShouldBe(2);
                (await host.ReadStatusAsync(Tenant, messageId)).ShouldNotBeNull().Status.ShouldBe(CommandStatus.Completed);
                original = await host.ActorEventsAsync(Tenant, command.TimeEntryId.Value);
                AssertRecordingBatch(original[1..], initialState, command, messageId, firstSequence: 2);
                AssertOutcomes(host, command, messageId, "policy-refusal", "policy-refusal", "indeterminate-commit");
            }
            finally
            {
                pendingRead.TrySetCanceled(TestContext.Current.CancellationToken);
            }
        }

        using var restarted = new ComposedConfirmationHost(storage, policy);
        using HttpClient restartedClient = restarted.CreateClient();
        restarted.Clock.UtcNow = _firstUtc.AddHours(3);
        restarted.EmitMissingStatusPlaceholder = initialState == TimeEntryApprovalState.Submitted;
        (await restarted.ReadStatusAsync(Tenant, messageId)).ShouldBeNull();

        using HttpResponseMessage recovered = await restarted.PostWorkloadAsync(restartedClient, Route, command);

        recovered.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await recovered.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
        restarted.BoundaryCalls.ShouldBe((0, 1, 1));
        CommandStatusRecord? advisory = await restarted.ReadStatusAsync(Tenant, messageId);
        if (advisory is { Status: CommandStatus.Completed, Domain: "timesheets" })
        {
            advisory.CommittedEventSequence.ShouldBeNull();
        }
        else
        {
            (advisory?.Status).ShouldBe(restarted.EmitMissingStatusPlaceholder ? CommandStatus.Received : null);
            (advisory?.Domain).ShouldBeNull();
            (advisory?.CommittedEventSequence).ShouldBeNull();
        }
        restarted.Publisher.PublishCalls.ShouldBeEmpty();
        EventEnvelope[] afterRestart = await restarted.ActorEventsAsync(Tenant, command.TimeEntryId.Value);
        AssertRecordingBatch(afterRestart[1..], initialState, command, messageId, firstSequence: 2);
        afterRestart.Select(item => item.MessageId).ShouldBe(original.Select(item => item.MessageId));
        AssertOutcomes(restarted, command, messageId, "committed");
    }

    [Fact]
    public async Task RecordingWithFailedPublicationRecoversOwnerBatchAfterRestartWhenStatusIsLost()
    {
        var storage = new ConcurrentDictionary<(string Tenant, string Owner), InMemoryStateManager>();
        SubmitExternalTimeEntry command = RecordCommand("unpublished-recording");
        string messageId = CreateMessageId(command);
        var polling = new MagicLinkSubmissionPollingOptions { Timeout = TimeSpan.FromSeconds(2) };
        EventEnvelope original;
        using (var host = new ComposedConfirmationHost(storage, pollingOptions: polling))
        using (HttpClient client = host.CreateClient())
        {
            await host.SeedFreshCatalogAsync(client, Tenant);
            host.Publisher.SetupFailure();

            using HttpResponseMessage failed = await host.PostWorkloadAsync(client, Route, command);

            await AssertOpaqueRefusalAsync(failed, command, messageId);
            (await host.ReadStatusAsync(Tenant, messageId)).ShouldNotBeNull().Status
                .ShouldBeOneOf(CommandStatus.PublishFailed, CommandStatus.Processing);
            EventEnvelope[] events = await host.ActorEventsAsync(Tenant, command.TimeEntryId.Value);
            AssertRecordingBatch(events, TimeEntryApprovalState.Draft, command, messageId);
            original = events.ShouldHaveSingleItem();
            host.Publisher.TotalEventsPublished.ShouldBe(0);
            AssertOutcomes(host, command, messageId, "indeterminate-commit");
        }

        using var restarted = new ComposedConfirmationHost(storage, pollingOptions: polling) { EmitMissingStatusPlaceholder = true };
        using HttpClient restartedClient = restarted.CreateClient();
        (await restarted.ReadStatusAsync(Tenant, messageId)).ShouldBeNull();

        using HttpResponseMessage retry = await restarted.PostWorkloadAsync(restartedClient, Route, command);

        retry.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await retry.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
        restarted.BoundaryCalls.ShouldBe((0, 1, 1));
        CommandStatusRecord? advisory = await restarted.ReadStatusAsync(Tenant, messageId);
        if (advisory is { Status: CommandStatus.Completed, Domain: "timesheets" })
        {
            advisory.CommittedEventSequence.ShouldBeNull();
        }
        else
        {
            advisory.ShouldNotBeNull().Status.ShouldBe(CommandStatus.Received);
            advisory.Domain.ShouldBeNull();
        }
        EventEnvelope[] afterRestart = await restarted.ActorEventsAsync(Tenant, command.TimeEntryId.Value);
        AssertRecordingBatch(afterRestart, TimeEntryApprovalState.Draft, command, messageId);
        afterRestart.ShouldHaveSingleItem().MessageId.ShouldBe(original.MessageId);
        restarted.Publisher.PublishCalls.ShouldBeEmpty();
        AssertOutcomes(restarted, command, messageId, "committed");
    }

    [Fact]
    public async Task CallerCancellationCancelsComposedReadbackWithoutLoggingAnIndeterminateCommit()
    {
        var storage = new ConcurrentDictionary<(string Tenant, string Owner), InMemoryStateManager>();
        SubmitExternalTimeEntry command = RecordCommand("canceled-recording");
        string messageId = CreateMessageId(command);
        var requestCompleted = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var host = new ComposedConfirmationHost(storage)
        {
            RecordingRequestCompleted = failure => requestCompleted.TrySetResult(failure)
        };
        using HttpClient client = host.CreateClient();
        await host.SeedFreshCatalogAsync(client, Tenant);
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var readStarted = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readEnded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.BeforeStreamReadAsync = async token =>
        {
            readStarted.TrySetResult(token);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            finally
            {
                readEnded.TrySetResult();
            }
        };

        Task<HttpResponseMessage> pending = host.PostWorkloadAsync(client, Route, command, caller.Token);
        CancellationToken transport = await readStarted.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        caller.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() => pending);
        await readEnded.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Exception? serverFailure = await requestCompleted.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        serverFailure.ShouldBeAssignableTo<OperationCanceledException>();
        transport.IsCancellationRequested.ShouldBeTrue();
        AssertOutcomes(host, command, messageId);
        AssertRecordingBatch(await host.ActorEventsAsync(Tenant, command.TimeEntryId.Value),
            TimeEntryApprovalState.Draft, command, messageId);
    }

    [Theory]
    [InlineData("null-source-id")]
    [InlineData("blank-source-id")]
    [InlineData("missing-service-date")]
    [InlineData("null-service-date")]
    [InlineData("default-service-date")]
    [InlineData("missing-duration")]
    [InlineData("zero-duration")]
    [InlineData("negative-duration")]
    [InlineData("missing-billable-state")]
    [InlineData("unknown-billable-state")]
    [InlineData("undefined-billable-state")]
    public async Task InvalidRequiredRecordingValuesAreOpaquePolicyRefusals(string scenario)
    {
        var storage = new ConcurrentDictionary<(string Tenant, string Owner), InMemoryStateManager>();
        var command = RecordCommand("invalid-recording");
        string messageId = CreateMessageId(command);
        JsonObject body = JsonSerializer.SerializeToNode(command, _jsonOptions).ShouldBeOfType<JsonObject>();
        switch (scenario)
        {
            case "null-source-id": body["source"]!["externalRequestId"] = null; break;
            case "blank-source-id": body["source"]!["externalRequestId"] = " "; break;
            case "missing-service-date": body.Remove("serviceDate"); break;
            case "null-service-date": body["serviceDate"] = null; break;
            case "default-service-date": body["serviceDate"] = "0001-01-01"; break;
            case "missing-duration": body.Remove("durationMinutes"); break;
            case "zero-duration": body["durationMinutes"] = 0; break;
            case "negative-duration": body["durationMinutes"] = -1; break;
            case "missing-billable-state": body.Remove("billableState"); break;
            case "unknown-billable-state": body["billableState"] = "unknown"; break;
            case "undefined-billable-state": body["billableState"] = 99; break;
        }
        using var host = new ComposedConfirmationHost(storage);
        using HttpClient client = host.CreateClient();

        using HttpResponseMessage refusal = await host.PostWorkloadRawJsonAsync(client, Route, body.ToJsonString());

        await AssertOpaqueRefusalAsync(refusal, command, messageId);
        AssertOutcomes(host, command, messageId, "policy-refusal");
        host.BoundaryCalls.ShouldBe((0, 0, 0));
        storage.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("null", "application/json")]
    [InlineData("[]", "application/json")]
    [InlineData("{", "application/json")]
    [InlineData("{", "application/vnd.timesheets+json")]
    [InlineData("""{"source":{"sourceSystem":"supplier-api","externalRequestId":""},""", "application/json")]
    [InlineData("""{"serviceDate":"2026-11-20","serviceDate":"2026-11-21"}""", "application/json")]
    [InlineData("""{"source":{"sourceSystem":"supplier-api","sourceSystem":"supplier-api"}}""", "application/json")]
    public async Task AuthenticatedMalformedRecordingBodyReturnsOpaqueBadRequest(string body, string mediaType)
    {
        var storage = new ConcurrentDictionary<(string Tenant, string Owner), InMemoryStateManager>();
        SubmitExternalTimeEntry command = RecordCommand("invalid-recording");
        string messageId = CreateMessageId(command);
        using var host = new ComposedConfirmationHost(storage);
        using HttpClient client = host.CreateClient();

        using HttpResponseMessage response = await host.PostWorkloadRawJsonAsync(client, Route, body, mediaType);

        await AssertOpaqueRefusalAsync(response, command, messageId, HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        AssertOutcomes(host, command, messageId);
        host.BoundaryCalls.ShouldBe((0, 0, 0));
        storage.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{")]
    public async Task MissingRecordingIdentityTakesPrecedenceOverBodyDeserialization(string body)
    {
        var storage = new ConcurrentDictionary<(string Tenant, string Owner), InMemoryStateManager>();
        SubmitExternalTimeEntry command = RecordCommand("invalid-recording");
        string messageId = CreateMessageId(command);
        using var host = new ComposedConfirmationHost(storage);
        using HttpClient client = host.CreateClient();
        host.SetSignedBindings("timesheets", actor: null);

        using HttpResponseMessage response = await host.PostWorkloadRawJsonAsync(client, Route, body);

        await AssertOpaqueRefusalAsync(response, command, messageId);
        AssertOutcomes(host, command, messageId, "policy-refusal");
        host.BoundaryCalls.ShouldBe((0, 0, 0));
        storage.ShouldBeEmpty();
    }

    private static SubmitExternalTimeEntry RecordCommand(string entryId)
        => new(new TimeEntryId(entryId),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            new PartyReference(Actor), new ActivityTypeId("activity-1"), new DateOnly(2026, 11, 20),
            60, BillableState.Billable, new ExternalContributionSource("supplier-api", "private-record-request"))
        {
            Comment = new TimeEntryComment(PrivateNote, TimeEntryCommentPolicy.SensitiveDefault)
        };

    private static string CreateMessageId(SubmitExternalTimeEntry command)
        => ExternalContributionConfirmationPersistenceTests.ExpectedMessageId(JsonSerializer.Serialize(new[]
        {
            "record", Tenant, Actor, command.TimeEntryId.Value, command.Source.SourceSystem, command.Source.ExternalRequestId
        }));

    private static void AssertRecordingBatch(
        EventEnvelope[] events, TimeEntryApprovalState initialState, SubmitExternalTimeEntry command, string messageId,
        long firstSequence = 1)
    {
        bool submitted = initialState == TimeEntryApprovalState.Submitted;
        events.Select(item => item.EventTypeName).ShouldBe(submitted
            ? [typeof(StoredTimeEntryRecorded).FullName!, typeof(StoredTimeEntrySubmitted).FullName!]
            : [typeof(StoredTimeEntryRecorded).FullName!]);
        events.Select(item => item.SequenceNumber).ShouldBe(submitted ? [firstSequence, firstSequence + 1] : [firstSequence]);
        events.ShouldAllBe(item => item.CorrelationId == messageId && item.CausationId == messageId);
        var recorded = JsonSerializer.Deserialize<StoredTimeEntryRecorded>(events[0].Payload, _jsonOptions).ShouldNotBeNull().Event;
        recorded.TimeEntryId.ShouldBe(command.TimeEntryId);
        recorded.ExternalSource.ShouldBe(command.Source);
        recorded.ApprovalState.ShouldBe(TimeEntryApprovalState.Draft);
        if (submitted)
        {
            var receipt = JsonSerializer.Deserialize<StoredTimeEntrySubmitted>(events[1].Payload, _jsonOptions).ShouldNotBeNull().Event;
            receipt.SubmittedAtUtc.ShouldBe(_firstUtc);
            receipt.SubmittedAtUtc.Offset.ShouldBe(TimeSpan.Zero);
            receipt.ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
            receipt.Submitter.ShouldBe(new PartyReference(Actor));
            receipt.Tenant.ShouldBe(new TenantReference(Tenant));
        }
    }

    private static async Task AssertOpaqueRefusalAsync(
        HttpResponseMessage response, SubmitExternalTimeEntry command, string messageId,
        HttpStatusCode statusCode = HttpStatusCode.Forbidden)
    {
        response.StatusCode.ShouldBe(statusCode);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("External contribution request was not accepted.");
        body.ShouldNotContain("policy-refusal");
        body.ShouldNotContain("indeterminate-commit");
        AssertNoSensitiveData(body, command, messageId);
    }

    private static void AssertOutcomes(ComposedConfirmationHost host, SubmitExternalTimeEntry command, string messageId, params string[] outcomes)
    {
        string[] logs = host.Logs.MessagesFor(typeof(TimeEntryRecordSubmissionService).FullName!);
        logs.Length.ShouldBe(outcomes.Length);
        for (int index = 0; index < logs.Length; index++)
        {
            logs[index].ShouldContain($"outcome={outcomes[index]}");
            Regex.IsMatch(logs[index], @"trace=[0-9a-f]{32}\b").ShouldBeTrue(logs[index]);
            AssertNoSensitiveData(logs[index], command, messageId);
        }
    }

    private static void AssertNoSensitiveData(string text, SubmitExternalTimeEntry command, string messageId)
    {
        foreach (string sensitive in new[]
        {
            Tenant, Actor, command.TimeEntryId.Value, command.Target.TargetId,
            command.Source.SourceSystem, command.Source.ExternalRequestId, PrivateNote, messageId,
            "token", "bearer", "secret"
        })
        {
            text.ShouldNotContain(sensitive, Case.Insensitive);
        }
    }
}
