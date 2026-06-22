using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Works.Contracts.Models;
using Hexalith.Works.Contracts.ValueObjects;

using NSubstitute;

using Shouldly;

namespace Hexalith.Timesheets.Works.Tests;

/// <summary>
/// Edge-case and fail-closed unit tests for <see cref="WorksQueryWorkPlannedEffortProvider"/>: missing
/// tenant context, undefined/garbled payloads, cancellation propagation, envelope audit propagation,
/// case-insensitive tenant matching, and the fail-fast null-argument contract.
/// </summary>
public sealed class WorksQueryWorkPlannedEffortProviderEdgeCaseTests
{
    [Fact]
    public async Task ReturnsUnavailableAndDoesNotQueryWorksWhenTenantContextMissing()
    {
        IWorksQueryChannel channel = Substitute.For<IWorksQueryChannel>();
        WorksQueryWorkPlannedEffortProvider provider = new(channel);

        TimesheetsRequestContext noTenant = new(null, new PartyReference("actor-1"), "corr-1");

        WorkPlannedEffortReadModel result = await provider.GetPlannedEffortAsync(
            noTenant,
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        result.Availability.ShouldBe(WorkPlannedEffortAvailability.Unavailable);
        await channel.DidNotReceive().InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsUnavailableWhenSuccessfulResultHasUndefinedPayload()
    {
        WorksQueryWorkPlannedEffortProvider provider = ProviderReturning(new QueryResult(true));

        WorkPlannedEffortReadModel result = await provider.GetPlannedEffortAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        result.Availability.ShouldBe(WorkPlannedEffortAvailability.Unavailable);
    }

    [Fact]
    public async Task ReturnsUnavailableWhenPayloadCannotDeserializeToWorkItemView()
    {
        // A successful result whose payload is not a WorkItemView fails closed (the deserialize throws and
        // is caught), never a fabricated planned value.
        QueryResult garbled = QueryResult.FromPayload(
            JsonSerializer.SerializeToElement("not-a-work-item-view"));
        WorksQueryWorkPlannedEffortProvider provider = ProviderReturning(garbled);

        WorkPlannedEffortReadModel result = await provider.GetPlannedEffortAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        result.Availability.ShouldBe(WorkPlannedEffortAvailability.Unavailable);
    }

    [Fact]
    public async Task PropagatesOperationCanceledExceptionInsteadOfFailingClosed()
    {
        IWorksQueryChannel channel = Substitute.For<IWorksQueryChannel>();
        channel
            .InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns<Task<QueryResult>>(_ => throw new OperationCanceledException());
        WorksQueryWorkPlannedEffortProvider provider = new(channel);

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await provider.GetPlannedEffortAsync(
                WorksQueryTestData.Context(),
                WorksQueryTestData.Work(),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BuildsTenantScopedGetWorkItemEnvelope()
    {
        QueryEnvelope? captured = null;
        IWorksQueryChannel channel = Substitute.For<IWorksQueryChannel>();
        channel
            .InvokeAsync(Arg.Do<QueryEnvelope>(envelope => captured = envelope), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(WorksQueryTestData.SuccessResult(
                WorksQueryTestData.FoundView(WorkItemStatus.InProgress))));
        WorksQueryWorkPlannedEffortProvider provider = new(channel);

        await provider.GetPlannedEffortAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Domain.ShouldBe("work");
        captured.QueryType.ShouldBe("get-work-item");
        captured.AggregateId.ShouldBe(WorksQueryTestData.WorkIdValue);
        captured.TenantId.ShouldBe(WorksQueryTestData.TenantValue);
        captured.UserId.ShouldBe("actor-1");
    }

    [Fact]
    public async Task FallsBackToCorrelationIdForEnvelopeUserIdWhenActorMissing()
    {
        QueryEnvelope? captured = null;
        IWorksQueryChannel channel = Substitute.For<IWorksQueryChannel>();
        channel
            .InvokeAsync(Arg.Do<QueryEnvelope>(envelope => captured = envelope), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(WorksQueryTestData.SuccessResult(
                WorksQueryTestData.FoundView(WorkItemStatus.InProgress))));
        WorksQueryWorkPlannedEffortProvider provider = new(channel);

        TimesheetsRequestContext noActor = new(new TenantReference(WorksQueryTestData.TenantValue), null, "corr-9");

        await provider.GetPlannedEffortAsync(
            noActor,
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.UserId.ShouldBe("corr-9");
    }

    [Fact]
    public async Task MatchesRequestTenantCaseInsensitively()
    {
        WorksQueryWorkPlannedEffortProvider provider = ProviderReturning(
            WorksQueryTestData.SuccessResult(WorksQueryTestData.FoundView(
                WorkItemStatus.InProgress,
                tenant: "tenant-a",
                estimated: 5,
                done: 0,
                remaining: 5,
                unit: "days")));

        // Works lowercases the tenant on its view; the provider lowercases the request tenant before
        // comparing, so a differently-cased request tenant still matches and is authorized.
        TimesheetsRequestContext upperCaseTenant = new(
            new TenantReference("TENANT-A"),
            new PartyReference("actor-1"),
            "corr-1");

        WorkPlannedEffortReadModel result = await provider.GetPlannedEffortAsync(
            upperCaseTenant,
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        result.Availability.ShouldBe(WorkPlannedEffortAvailability.Supplied);
        result.Unit.ShouldBe("days");
    }

    [Fact]
    public void ConstructorRejectsNullChannel()
        => Should.Throw<ArgumentNullException>(() => new WorksQueryWorkPlannedEffortProvider(null!));

    [Fact]
    public async Task RejectsNullContext()
    {
        WorksQueryWorkPlannedEffortProvider provider = new(Substitute.For<IWorksQueryChannel>());

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await provider.GetPlannedEffortAsync(
                null!,
                WorksQueryTestData.Work(),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RejectsNullWork()
    {
        WorksQueryWorkPlannedEffortProvider provider = new(Substitute.For<IWorksQueryChannel>());

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await provider.GetPlannedEffortAsync(
                WorksQueryTestData.Context(),
                null!,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ForwardsCallerCancellationTokenToWorksChannel()
    {
        using CancellationTokenSource cts = new();
        IWorksQueryChannel channel = Substitute.For<IWorksQueryChannel>();
        channel
            .InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(WorksQueryTestData.SuccessResult(
                WorksQueryTestData.FoundView(WorkItemStatus.InProgress))));
        WorksQueryWorkPlannedEffortProvider provider = new(channel);

        await provider.GetPlannedEffortAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            cts.Token);

        // The caller's token must flow through to the Works query so cancellation is honored at the source.
        await channel.Received(1).InvokeAsync(Arg.Any<QueryEnvelope>(), cts.Token);
    }

    [Fact]
    public async Task UnavailableResultCarriesUnavailableReferenceStateMetadata()
    {
        // AC2 fail-closed: a transport failure must mark BOTH freshness and reference state Unavailable —
        // never leave reference state Current with no planned values.
        WorksQueryWorkPlannedEffortProvider provider = ProviderReturning(
            QueryResult.Failure("works-domain-unreachable"));

        WorkPlannedEffortReadModel result = await provider.GetPlannedEffortAsync(
            WorksQueryTestData.Context(),
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        result.Availability.ShouldBe(WorkPlannedEffortAvailability.Unavailable);
        result.SourceReferenceState.State.ShouldBe(ActualTimeReferenceState.Unavailable);
        result.SourceFreshness.State.ShouldBe(ProjectionFreshnessState.Unavailable);
        result.Estimated.ShouldBeNull();
    }

    private static WorksQueryWorkPlannedEffortProvider ProviderReturning(QueryResult result)
    {
        IWorksQueryChannel channel = Substitute.For<IWorksQueryChannel>();
        channel
            .InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));
        return new WorksQueryWorkPlannedEffortProvider(channel);
    }
}
