using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.References;
using Hexalith.Works.Contracts.Models;
using Hexalith.Works.Contracts.ValueObjects;

using NSubstitute;

using Shouldly;

namespace Hexalith.Timesheets.Works.Tests;

/// <summary>
/// Edge-case coverage for the Works reference-validation adapter: query-envelope audit propagation,
/// case-insensitive tenant matching, and the fail-fast argument contract. Complements the mapping and
/// composition suites.
/// </summary>
public sealed class WorksQueryWorkReferenceValidatorEdgeCaseTests
{
    [Fact]
    public async Task EnvelopeUsesActorPartyIdAsUserIdAndPropagatesCorrelationId()
    {
        QueryEnvelope captured = await CaptureEnvelopeAsync(
            new TimesheetsRequestContext(
                new TenantReference(WorksQueryTestData.TenantValue),
                new PartyReference("actor-99"),
                "corr-xyz"));

        captured.UserId.ShouldBe("actor-99");
        captured.CorrelationId.ShouldBe("corr-xyz");
    }

    [Fact]
    public async Task EnvelopeFallsBackToCorrelationIdAsUserIdWhenActorIsMissing()
    {
        // A null actor must not throw out of the validator; the correlation id stands in for audit.
        QueryEnvelope captured = await CaptureEnvelopeAsync(
            new TimesheetsRequestContext(
                new TenantReference(WorksQueryTestData.TenantValue),
                Actor: null,
                "corr-fallback"));

        captured.UserId.ShouldBe("corr-fallback");
        captured.CorrelationId.ShouldBe("corr-fallback");
    }

    [Fact]
    public async Task TenantMatchIsCaseInsensitiveSoMixedCaseRequestTenantStillValidates()
    {
        // Works normalizes its tenant id to lowercase; the request tenant authority may arrive in any
        // case. The adapter must treat an only-case-different tenant as a match, never TenantMismatch.
        IWorksQueryChannel channel = ChannelReturning(
            WorksQueryTestData.SuccessResult(
                WorksQueryTestData.FoundView(WorkItemStatus.InProgress, tenant: "tenant-a")));
        WorksQueryWorkReferenceValidator validator = new(channel);
        TimesheetsRequestContext context = new(
            new TenantReference("TENANT-A"),
            new PartyReference("actor-1"),
            "corr-1");

        ReferenceValidationResult result = await validator.ValidateAsync(
            context,
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        result.State.ShouldBe(ReferenceValidationState.Valid);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ConstructorRejectsNullChannel()
        => Should.Throw<ArgumentNullException>(() => new WorksQueryWorkReferenceValidator(null!));

    [Fact]
    public async Task ValidateAsyncRejectsNullContext()
    {
        WorksQueryWorkReferenceValidator validator = new(Substitute.For<IWorksQueryChannel>());

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await validator.ValidateAsync(null!, WorksQueryTestData.Work(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ValidateAsyncRejectsNullWork()
    {
        WorksQueryWorkReferenceValidator validator = new(Substitute.For<IWorksQueryChannel>());

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await validator.ValidateAsync(WorksQueryTestData.Context(), null!, TestContext.Current.CancellationToken));
    }

    private static async Task<QueryEnvelope> CaptureEnvelopeAsync(TimesheetsRequestContext context)
    {
        QueryEnvelope? captured = null;
        IWorksQueryChannel channel = Substitute.For<IWorksQueryChannel>();
        channel
            .InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<QueryEnvelope>();
                return Task.FromResult(
                    WorksQueryTestData.SuccessResult(WorksQueryTestData.FoundView(WorkItemStatus.InProgress)));
            });
        WorksQueryWorkReferenceValidator validator = new(channel);

        ReferenceValidationResult result = await validator.ValidateAsync(
            context,
            WorksQueryTestData.Work(),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
        captured.ShouldNotBeNull();
        return captured;
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
