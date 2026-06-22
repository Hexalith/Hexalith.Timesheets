using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.References;
using Hexalith.Works.Contracts.Models;
using Hexalith.Works.Contracts.ValueObjects;

using NSubstitute;

using Shouldly;

namespace Hexalith.Timesheets.Works.Tests;

public sealed class WorksQueryWorkReferenceValidatorTests
{
    [Theory]
    [InlineData(WorkItemStatus.Created, ReferenceValidationState.Valid)]
    [InlineData(WorkItemStatus.Assigned, ReferenceValidationState.Valid)]
    [InlineData(WorkItemStatus.Queued, ReferenceValidationState.Valid)]
    [InlineData(WorkItemStatus.InProgress, ReferenceValidationState.Valid)]
    [InlineData(WorkItemStatus.Completed, ReferenceValidationState.Valid)]
    [InlineData(WorkItemStatus.Suspended, ReferenceValidationState.DisabledOrArchived)]
    [InlineData(WorkItemStatus.Cancelled, ReferenceValidationState.DisabledOrArchived)]
    [InlineData(WorkItemStatus.Rejected, ReferenceValidationState.DisabledOrArchived)]
    [InlineData(WorkItemStatus.Expired, ReferenceValidationState.DisabledOrArchived)]
    [InlineData(WorkItemStatus.Unknown, ReferenceValidationState.Ambiguous)]
    public async Task MapsFoundWorkStatusToExpectedState(WorkItemStatus status, ReferenceValidationState expected)
    {
        IWorksQueryChannel channel = ChannelReturning(
            WorksQueryTestData.SuccessResult(WorksQueryTestData.FoundView(status)));
        WorksQueryWorkReferenceValidator validator = new(channel);

        ReferenceValidationResult result = await validator.ValidateAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        result.State.ShouldBe(expected);
        result.IsValid.ShouldBe(expected == ReferenceValidationState.Valid);
    }

    [Fact]
    public async Task DenialReasonDoesNotLeakWorksLifecycleState()
    {
        IWorksQueryChannel channel = ChannelReturning(
            WorksQueryTestData.SuccessResult(WorksQueryTestData.FoundView(WorkItemStatus.Cancelled)));
        WorksQueryWorkReferenceValidator validator = new(channel);

        ReferenceValidationResult result = await validator.ValidateAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Reason.ShouldNotContain("cancelled", Case.Insensitive);
        result.Reason.ShouldNotContain("status", Case.Insensitive);
    }

    [Fact]
    public async Task NotFoundWorkFailsClosedAsInvalidReference()
    {
        IWorksQueryChannel channel = ChannelReturning(
            WorksQueryTestData.SuccessResult(WorksQueryTestData.NotFoundView()));
        WorksQueryWorkReferenceValidator validator = new(channel);

        ReferenceValidationResult result = await validator.ValidateAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        result.State.ShouldBe(ReferenceValidationState.InvalidReference);
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task CrossTenantViewFailsClosedAsTenantMismatch()
    {
        IWorksQueryChannel channel = ChannelReturning(
            WorksQueryTestData.SuccessResult(
                WorksQueryTestData.FoundView(WorkItemStatus.InProgress, tenant: "tenant-b")));
        WorksQueryWorkReferenceValidator validator = new(channel);

        ReferenceValidationResult result = await validator.ValidateAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        result.State.ShouldBe(ReferenceValidationState.TenantMismatch);
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task UnsuccessfulQueryResultFailsClosedAsUnavailable()
    {
        IWorksQueryChannel channel = ChannelReturning(QueryResult.Failure("works-domain-unreachable"));
        WorksQueryWorkReferenceValidator validator = new(channel);

        ReferenceValidationResult result = await validator.ValidateAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        result.State.ShouldBe(ReferenceValidationState.Unavailable);
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task EmptyPayloadFailsClosedAsUnavailable()
    {
        IWorksQueryChannel channel = ChannelReturning(new QueryResult(true, PayloadBytes: null));
        WorksQueryWorkReferenceValidator validator = new(channel);

        ReferenceValidationResult result = await validator.ValidateAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        result.State.ShouldBe(ReferenceValidationState.Unavailable);
    }

    [Fact]
    public async Task TransportExceptionFailsClosedAsUnavailable()
    {
        IWorksQueryChannel channel = Substitute.For<IWorksQueryChannel>();
        channel
            .InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns<Task<QueryResult>>(_ => throw new InvalidOperationException("transport"));
        WorksQueryWorkReferenceValidator validator = new(channel);

        ReferenceValidationResult result = await validator.ValidateAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        result.State.ShouldBe(ReferenceValidationState.Unavailable);
    }

    [Fact]
    public async Task CancellationIsNotSwallowedIntoADenial()
    {
        IWorksQueryChannel channel = Substitute.For<IWorksQueryChannel>();
        channel
            .InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns<Task<QueryResult>>(_ => throw new OperationCanceledException());
        WorksQueryWorkReferenceValidator validator = new(channel);

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await validator.ValidateAsync(
                WorksQueryTestData.Context(),
                WorksQueryTestData.Work(),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MissingTenantContextFailsClosedWithoutCallingWorks()
    {
        IWorksQueryChannel channel = Substitute.For<IWorksQueryChannel>();
        WorksQueryWorkReferenceValidator validator = new(channel);
        TimesheetsRequestContext context = new(Tenant: null, new PartyReference("actor-1"), "corr-1");

        ReferenceValidationResult result = await validator.ValidateAsync(
            context,
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        result.State.ShouldBe(ReferenceValidationState.Unavailable);
        result.IsValid.ShouldBeFalse();
        await channel.DidNotReceive().InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokesWorksGetWorkItemQueryWithTenantScopedEnvelope()
    {
        IWorksQueryChannel channel = ChannelReturning(
            WorksQueryTestData.SuccessResult(WorksQueryTestData.FoundView(WorkItemStatus.InProgress)));
        WorksQueryWorkReferenceValidator validator = new(channel);

        _ = await validator.ValidateAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        await channel.Received(1).InvokeAsync(
            Arg.Is<QueryEnvelope>(envelope =>
                envelope != null
                && envelope.Domain == "work"
                && envelope.QueryType == "get-work-item"
                && envelope.AggregateId == WorksQueryTestData.WorkIdValue
                && envelope.TenantId == WorksQueryTestData.TenantValue),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DuplicateReplayedReadsAreDeterministic()
    {
        IWorksQueryChannel channel = ChannelReturning(
            WorksQueryTestData.SuccessResult(
                WorksQueryTestData.FoundView(WorkItemStatus.InProgress, sourceSequence: 42)));
        WorksQueryWorkReferenceValidator validator = new(channel);

        ReferenceValidationResult first = await validator.ValidateAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);
        ReferenceValidationResult second = await validator.ValidateAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        // Idempotent read: same work + same source sequence yields equivalent results, no side effects.
        second.ShouldBe(first);
        await channel.Received(2).InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>());
    }

    private static IWorksQueryChannel ChannelReturning(QueryResult result)
    {
        IWorksQueryChannel channel = Substitute.For<IWorksQueryChannel>();
        channel
            .InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));
        return channel;
    }
}
