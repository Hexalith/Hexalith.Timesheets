using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Timesheets.Contracts.Commands.ExternalContributions;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.Policies;
using Hexalith.Timesheets.Server.TimeEntries;

using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class TimeEntryRecordSubmissionServiceTests
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Completed_status_requires_matching_stored_recording(bool corruptReceipt, bool expected)
    {
        SubmitExternalTimeEntry command = Command();
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        string? messageId = null;
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                messageId = call.ArgAt<string>(1);
                return Task.FromResult<CommandStatusQueryResponse?>(Status(messageId));
            });
        TimeEntryRecorded recorded = Recorded(command) with
        {
            DurationMinutes = corruptReceipt ? 99 : command.DurationMinutes
        };
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(recorded, messageId!)));

        (await Service(gateway).SubmitAsync(Context(), command, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken)).ShouldBe(expected);
        await gateway.DidNotReceive().SubmitWorkloadCommandAsync(
            Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Completed_recording_rejects_comment_decision_not_issued_by_server()
    {
        SubmitExternalTimeEntry command = Command() with
        {
            Comment = new TimeEntryComment("private note", TimeEntryCommentPolicy.SensitiveDefault)
        };
        string? messageId = null;
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                messageId = call.ArgAt<string>(1);
                return Task.FromResult<CommandStatusQueryResponse?>(Status(messageId));
            });
        TimeEntryComment expected = new TimeEntryCommentPublicationIssuer()
            .Issue(Context(), command.Comment).ShouldNotBeNull();
        TimeEntryComment forged = new(expected.Text, expected.Policy with
        {
            ProjectionInclusion = TimesheetsCommentPolicyDecision.Allowed,
            ServerProjectionInclusion = TimesheetsCommentPolicyDecision.Allowed
        });
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(Recorded(command) with { Comment = forged }, messageId!)));

        (await Service(gateway).SubmitAsync(Context(), command, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken)).ShouldBeFalse();
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(Recorded(command) with { Comment = expected }, messageId!)));
        (await Service(gateway).SubmitAsync(Context(), command, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task Completed_recording_uses_pre_submit_comment_decision_when_authority_changes()
    {
        SubmitExternalTimeEntry command = Command() with
        {
            Comment = new TimeEntryComment("private note", TimeEntryCommentPolicy.SensitiveDefault)
        };
        bool allowPublication = false;
        ITimeEntryCommentPublicationAuthority authority = Substitute.For<ITimeEntryCommentPublicationAuthority>();
        authority.MayPublish(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryComment>())
            .Returns(_ => allowPublication);
        var issuer = new TimeEntryCommentPublicationIssuer(authority);
        TimeEntryComment expected = issuer.Issue(Context(), command.Comment).ShouldNotBeNull();
        string? messageId = null;
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                allowPublication = true;
                messageId = call.ArgAt<string>(1);
                return Task.FromResult<CommandStatusQueryResponse?>(Status(messageId));
            });
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(Recorded(command) with { Comment = expected }, messageId!)));
        var service = new TimeEntryRecordSubmissionService(gateway,
            new MagicLinkDurableSubmissionService(gateway, Options.Create(new MagicLinkSubmissionPollingOptions())),
            ExternalContributionPolicyOptions.Default, issuer);

        (await service.SubmitAsync(Context(), command, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("", "event-message-2")]
    [InlineData("event-message-1", "event-message-1")]
    [InlineData("event-message-1", " ")]
    public async Task Completed_two_event_recording_requires_distinct_nonblank_event_ids(
        string firstId, string secondId)
    {
        SubmitExternalTimeEntry command = Command();
        string? messageId = null;
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                messageId = call.ArgAt<string>(1);
                return Task.FromResult<CommandStatusQueryResponse?>(Status(messageId) with
                {
                    EventCount = 2,
                    CommittedEventSequence = 2
                });
            });
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(Recorded(command), messageId!,
                Submitted(command, DateTimeOffset.UnixEpoch), recordedMessageId: firstId,
                submittedMessageId: secondId)));

        (await Service(gateway, new ExternalContributionPolicyOptions
        {
            InitialApprovalState = TimeEntryApprovalState.Submitted
        }).SubmitAsync(Context(), command, DateTimeOffset.UnixEpoch,
            TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("record-correlation")]
    [InlineData("submitted-at")]
    [InlineData("submission-correlation")]
    public async Task Completed_status_requires_exact_record_and_submission_receipt(string mismatch)
    {
        SubmitExternalTimeEntry command = Command();
        DateTimeOffset submittedAtUtc = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
        string? messageId = null;
        int statusQueries = 0;
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                messageId = call.ArgAt<string>(1);
                return Task.FromResult<CommandStatusQueryResponse?>(++statusQueries == 1 ? null : Status(messageId) with
                {
                    EventCount = 2,
                    CommittedEventSequence = 2
                });
            });
        gateway.SubmitWorkloadCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                SubmitCommandRequest request = call.ArgAt<SubmitCommandRequest>(0);
                return Task.FromResult(new SubmitCommandResponse(request.CorrelationId!, MessageId: request.MessageId));
            });
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(
                Recorded(command) with
                {
                    ActivityTypeScope = mismatch == "scope" ? ActivityTypeScope.Project : ActivityTypeScope.Tenant
                },
                messageId!,
                Submitted(command, submittedAtUtc.AddMinutes(mismatch == "submitted-at" ? 1 : 0)),
                mismatch == "record-correlation" ? "other-command" : null,
                mismatch == "submission-correlation" ? "other-command" : null)));

        (await Service(gateway, new ExternalContributionPolicyOptions
        {
            InitialApprovalState = TimeEntryApprovalState.Submitted
        }).SubmitAsync(Context(), command, submittedAtUtc, TestContext.Current.CancellationToken)).ShouldBeFalse();
        await gateway.Received(1).SubmitWorkloadCommandAsync(
            Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Distinct_trusted_actor_retry_has_distinct_command_identity()
    {
        SubmitExternalTimeEntry command = Command();
        List<string> queriedIds = [];
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                string id = call.ArgAt<string>(1);
                queriedIds.Add(id);
                return Task.FromResult<CommandStatusQueryResponse?>(queriedIds.Count == 1
                    ? Status(id)
                    : Status(id) with { Status = nameof(CommandStatus.Rejected), StatusCode = (int)CommandStatus.Rejected });
            });
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(Recorded(command), queriedIds[0])));

        (await Service(gateway).SubmitAsync(Context(), command, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken)).ShouldBeTrue();
        TimesheetsRequestContext otherActor = new(Context().Tenant, new PartyReference("contributor-2"), "correlation-2");
        (await Service(gateway).SubmitAsync(otherActor, command, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken)).ShouldBeFalse();
        queriedIds.Count.ShouldBe(2);
        queriedIds[0].ShouldNotBe(queriedIds[1]);
    }

    [Fact]
    public async Task Newline_bearing_source_fields_have_distinct_command_identities()
    {
        List<string> queriedIds = [];
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                string id = call.ArgAt<string>(1);
                queriedIds.Add(id);
                return Task.FromResult<CommandStatusQueryResponse?>(Status(id) with
                {
                    Status = nameof(CommandStatus.Rejected), StatusCode = (int)CommandStatus.Rejected
                });
            });

        (await Service(gateway).SubmitAsync(Context(), Command() with
        {
            Source = new ExternalContributionSource("a\nb", "c")
        }, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await Service(gateway).SubmitAsync(Context(), Command() with
        {
            Source = new ExternalContributionSource("a", "b\nc")
        }, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken)).ShouldBeFalse();
        queriedIds.Count.ShouldBe(2);
        queriedIds[0].ShouldNotBe(queriedIds[1]);
    }

    [Fact]
    public async Task Uncertain_status_denies_without_readback_or_duplicate_submission()
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.GetWorkloadCommandStatusAsync("tenant-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<CommandStatusQueryResponse?>(Status(call.ArgAt<string>(1)) with
            {
                Status = nameof(CommandStatus.PublishFailed),
                StatusCode = (int)CommandStatus.PublishFailed
            }));

        (await Service(gateway).SubmitAsync(Context(), Command(), DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken)).ShouldBeFalse();
        await gateway.DidNotReceive().SubmitWorkloadCommandAsync(
            Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>());
        await gateway.DidNotReceive().ReadWorkloadStreamAsync(
            Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
    }

    private static TimeEntryRecordSubmissionService Service(
        IEventStoreGatewayClient gateway, ExternalContributionPolicyOptions? policy = null)
        => new(gateway, new MagicLinkDurableSubmissionService(
            gateway, Options.Create(new MagicLinkSubmissionPollingOptions())),
            policy ?? ExternalContributionPolicyOptions.Default);

    private static TimesheetsRequestContext Context()
        => new(new TenantReference("tenant-1"), new PartyReference("contributor-1"), "correlation-1");

    private static SubmitExternalTimeEntry Command()
        => new(new TimeEntryId("entry-1"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            new PartyReference("contributor-1"), new ActivityTypeId("activity-1"),
            new DateOnly(2026, 10, 10), 60, BillableState.Billable,
            new ExternalContributionSource("supplier", "request-1"));

    private static TimeEntryRecorded Recorded(SubmitExternalTimeEntry command)
        => new(command.TimeEntryId, command.Target, command.Contributor, command.ActivityTypeId,
            ActivityTypeScope.Tenant, command.ServiceDate, command.DurationMinutes,
            command.BillableState, TimeEntryApprovalState.Draft, ContributorCategory.ExternalContributor, null)
        {
            ExternalSource = command.Source
        };

    private static CommandStatusQueryResponse Status(string messageId)
        => new(messageId, nameof(CommandStatus.Completed), (int)CommandStatus.Completed, null, messageId)
        {
            EventCount = 1,
            CommittedEventSequence = 1,
            TenantId = "tenant-1",
            Domain = "timesheets",
            AggregateId = "entry-1"
        };

    private static TimeEntrySubmitted Submitted(SubmitExternalTimeEntry command, DateTimeOffset submittedAtUtc)
        => new(command.TimeEntryId, Context().Actor!, Context().Tenant!, submittedAtUtc,
            new TimeEntrySubmissionId(command.Source.ExternalRequestId),
            TimeEntrySubmissionScope.SelectedEntries, TimeEntryApprovalState.Submitted);

    private static StreamReadPage Page(
        TimeEntryRecorded recorded, string messageId, TimeEntrySubmitted? submitted = null,
        string? recordedCorrelation = null, string? submittedCorrelation = null,
        string? recordedMessageId = null, string? submittedMessageId = null)
    {
        StoredTimeEntryRecorded stored = new(recorded);
        List<StreamReadEvent> events =
        [
            new(1, typeof(StoredTimeEntryRecorded).FullName!,
                JsonSerializer.SerializeToUtf8Bytes(stored, _jsonOptions), "json", 1,
                recordedMessageId ?? "event-message-1", recordedCorrelation ?? messageId, null, DateTimeOffset.UtcNow, "actor-1")
        ];
        if (submitted is not null)
        {
            events.Add(new StreamReadEvent(2, typeof(StoredTimeEntrySubmitted).FullName!,
                JsonSerializer.SerializeToUtf8Bytes(new StoredTimeEntrySubmitted(submitted), _jsonOptions), "json", 1,
                submittedMessageId ?? "event-message-2", submittedCorrelation ?? messageId, null, DateTimeOffset.UtcNow, "actor-1"));
        }
        return new StreamReadPage("tenant-1", "timesheets", "entry-1",
            events, new StreamReadMetadata(0, null, events.Count, events.Count, events.Count, false, null));
    }
}
