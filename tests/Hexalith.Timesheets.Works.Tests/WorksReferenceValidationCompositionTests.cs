using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.References;
using Hexalith.Timesheets.Server.Runtime;
using Hexalith.Works.Contracts.Models;
using Hexalith.Works.Contracts.ValueObjects;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NSubstitute;

using Shouldly;

namespace Hexalith.Timesheets.Works.Tests;

public sealed class WorksReferenceValidationCompositionTests
{
    [Fact]
    public async Task ComposedGuardUsesAdapterAndDeniesWhenWorksReportsNotFound()
    {
        IWorksQueryChannel channel = ChannelReturning(
            WorksQueryTestData.SuccessResult(WorksQueryTestData.NotFoundView()));
        using ServiceProvider provider = BuildProvider(channel, registerAdapter: true);

        ITimesheetsAccessGuard guard = provider.GetRequiredService<ITimesheetsAccessGuard>();
        TimesheetsAuthorizationDecision decision = await guard.AuthorizeAsync(
            WorkCommandRequest(),
            TestContext.Current.CancellationToken);

        decision.IsAuthorized.ShouldBeFalse();
        decision.DenialCategory.ShouldBe(TimesheetsDenialCategory.InvalidReference);
        provider.GetRequiredService<IWorkReferenceValidator>()
            .ShouldBeOfType<WorksQueryWorkReferenceValidator>();
        await channel.Received(1).InvokeAsync(
            Arg.Is<QueryEnvelope>(envelope =>
                envelope != null
                && envelope.Domain == "work"
                && envelope.QueryType == "get-work-item"
                && envelope.AggregateId == WorksQueryTestData.WorkIdValue),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ComposedGuardAllowsActiveWorkThroughTheAdapter()
    {
        IWorksQueryChannel channel = ChannelReturning(
            WorksQueryTestData.SuccessResult(WorksQueryTestData.FoundView(WorkItemStatus.InProgress)));
        using ServiceProvider provider = BuildProvider(channel, registerAdapter: true);

        ITimesheetsAccessGuard guard = provider.GetRequiredService<ITimesheetsAccessGuard>();
        TimesheetsAuthorizationDecision decision = await guard.AuthorizeAsync(
            WorkCommandRequest(),
            TestContext.Current.CancellationToken);

        decision.IsAuthorized.ShouldBeTrue();
        await channel.Received(1).InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>());
    }

    // End-to-end proof that each Works status the adapter can return is mapped by the composed
    // TimesheetsAccessGuard to the correct allow/deny outcome AND TimesheetsDenialCategory.
    // The unit suite proves status -> ReferenceValidationState; this proves that state survives the
    // guard's MapReferenceState through the real DI-resolved adapter (Epic 1 retro: composed proof).
    [Theory]
    [InlineData(WorkItemStatus.Completed, true, TimesheetsDenialCategory.None)]
    [InlineData(WorkItemStatus.Suspended, false, TimesheetsDenialCategory.UnavailableSiblingAuthority)]
    [InlineData(WorkItemStatus.Cancelled, false, TimesheetsDenialCategory.UnavailableSiblingAuthority)]
    [InlineData(WorkItemStatus.Rejected, false, TimesheetsDenialCategory.UnavailableSiblingAuthority)]
    [InlineData(WorkItemStatus.Expired, false, TimesheetsDenialCategory.UnavailableSiblingAuthority)]
    [InlineData(WorkItemStatus.Unknown, false, TimesheetsDenialCategory.AmbiguousAuthority)]
    public async Task ComposedGuardMapsFoundWorkStatusToExpectedOutcome(
        WorkItemStatus status,
        bool expectedAuthorized,
        TimesheetsDenialCategory expectedCategory)
    {
        IWorksQueryChannel channel = ChannelReturning(
            WorksQueryTestData.SuccessResult(WorksQueryTestData.FoundView(status)));
        using ServiceProvider provider = BuildProvider(channel, registerAdapter: true);

        ITimesheetsAccessGuard guard = provider.GetRequiredService<ITimesheetsAccessGuard>();
        TimesheetsAuthorizationDecision decision = await guard.AuthorizeAsync(
            WorkCommandRequest(),
            TestContext.Current.CancellationToken);

        decision.IsAuthorized.ShouldBe(expectedAuthorized);
        decision.DenialCategory.ShouldBe(expectedCategory);
        await channel.Received(1).InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ComposedGuardMapsCrossTenantWorkToCrossTenantTarget()
    {
        IWorksQueryChannel channel = ChannelReturning(
            WorksQueryTestData.SuccessResult(
                WorksQueryTestData.FoundView(WorkItemStatus.InProgress, tenant: "tenant-b")));
        using ServiceProvider provider = BuildProvider(channel, registerAdapter: true);

        ITimesheetsAccessGuard guard = provider.GetRequiredService<ITimesheetsAccessGuard>();
        TimesheetsAuthorizationDecision decision = await guard.AuthorizeAsync(
            WorkCommandRequest(),
            TestContext.Current.CancellationToken);

        decision.IsAuthorized.ShouldBeFalse();
        decision.DenialCategory.ShouldBe(TimesheetsDenialCategory.CrossTenantTarget);
    }

    [Fact]
    public async Task ComposedGuardMapsTransportFailureToUnavailableSiblingAuthority()
    {
        IWorksQueryChannel channel = ChannelReturning(QueryResult.Failure("works-domain-unreachable"));
        using ServiceProvider provider = BuildProvider(channel, registerAdapter: true);

        ITimesheetsAccessGuard guard = provider.GetRequiredService<ITimesheetsAccessGuard>();
        TimesheetsAuthorizationDecision decision = await guard.AuthorizeAsync(
            WorkCommandRequest(),
            TestContext.Current.CancellationToken);

        decision.IsAuthorized.ShouldBeFalse();
        decision.DenialCategory.ShouldBe(TimesheetsDenialCategory.UnavailableSiblingAuthority);
    }

    [Fact]
    public async Task ComposedGuardStaysFailClosedWhenAdapterIsNotRegistered()
    {
        using ServiceProvider provider = BuildProvider(channel: null, registerAdapter: false);

        ITimesheetsAccessGuard guard = provider.GetRequiredService<ITimesheetsAccessGuard>();
        TimesheetsAuthorizationDecision decision = await guard.AuthorizeAsync(
            WorkCommandRequest(),
            TestContext.Current.CancellationToken);

        decision.IsAuthorized.ShouldBeFalse();
        decision.DenialCategory.ShouldBe(TimesheetsDenialCategory.InvalidReference);
        provider.GetRequiredService<IWorkReferenceValidator>()
            .ShouldBeOfType<DenyAllWorkReferenceValidator>();
    }

    [Fact]
    public void AdapterRegistrationWinsWhenCalledAfterKernel()
    {
        ServiceCollection services = new();
        services.AddTimesheetsServerKernel();
        services.AddTimesheetsWorksReferenceValidation();
        services.AddSingleton(Substitute.For<IWorksQueryChannel>());

        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<IWorkReferenceValidator>()
            .ShouldBeOfType<WorksQueryWorkReferenceValidator>();
    }

    [Fact]
    public void AdapterRegistrationWinsWhenCalledBeforeKernel()
    {
        ServiceCollection services = new();
        services.AddTimesheetsWorksReferenceValidation();
        services.AddTimesheetsServerKernel();
        services.AddSingleton(Substitute.For<IWorksQueryChannel>());

        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<IWorkReferenceValidator>()
            .ShouldBeOfType<WorksQueryWorkReferenceValidator>();
    }

    private static TimesheetsAuthorizationRequest WorkCommandRequest()
        => new(WorksQueryTestData.Context(), TimesheetsOperation.Command)
        {
            Work = WorksQueryTestData.Work(),
        };

    private static IWorksQueryChannel ChannelReturning(QueryResult result)
    {
        IWorksQueryChannel channel = Substitute.For<IWorksQueryChannel>();
        channel
            .InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));
        return channel;
    }

    private static ServiceProvider BuildProvider(IWorksQueryChannel? channel, bool registerAdapter)
    {
        ServiceCollection services = new();
        services.AddTimesheetsServerKernel();

        // Let requests reach the Work validation step: authorize tenant access and allow policy.
        ITimesheetsTenantAccessValidator tenant = Substitute.For<ITimesheetsTenantAccessValidator>();
        tenant
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimesheetsOperation>(), Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Authorized());
        services.Replace(ServiceDescriptor.Singleton(tenant));

        ITimesheetsPolicyEvaluator policy = Substitute.For<ITimesheetsPolicyEvaluator>();
        policy
            .EvaluateAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(TimesheetsPolicyEvaluationResult.Allowed());
        services.Replace(ServiceDescriptor.Singleton(policy));

        if (channel is not null)
        {
            services.AddSingleton(channel);
        }

        if (registerAdapter)
        {
            services.AddTimesheetsWorksReferenceValidation();
        }

        return services.BuildServiceProvider();
    }
}
