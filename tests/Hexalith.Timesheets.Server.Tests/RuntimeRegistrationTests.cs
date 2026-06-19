using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.Policies;
using Hexalith.Timesheets.Server.References;
using Hexalith.Timesheets.Server.Runtime;
using Hexalith.Timesheets.Server.TimeEntries;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class RuntimeRegistrationTests
{
    [Fact]
    public void Server_kernel_registers_fail_closed_defaults_until_trust_adapters_exist()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddTimesheetsServerKernel();

        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<ITimesheetsAuthorizationGate>()
            .ShouldBeOfType<DenyAllTimesheetsAuthorizationGate>();
        provider.GetRequiredService<ITimesheetsAccessGuard>()
            .ShouldBeOfType<TimesheetsAccessGuard>();
        provider.GetRequiredService<TenantActivityTypeCommandService>()
            .ShouldNotBeNull();
        provider.GetRequiredService<ProjectActivityTypeCommandService>()
            .ShouldNotBeNull();
        provider.GetRequiredService<TimeEntryCommandService>()
            .ShouldNotBeNull();
        provider.GetRequiredService<TimeEntryEvidenceQueryService>()
            .ShouldNotBeNull();
        provider.GetRequiredService<ITimesheetsTenantAccessValidator>()
            .ShouldBeOfType<DenyAllTimesheetsTenantAccessValidator>();
        provider.GetRequiredService<ITimesheetsPolicyEvaluator>()
            .ShouldBeOfType<TimesheetsEvidencePolicyEvaluator>();
        provider.GetRequiredService<TimesheetsEvidencePolicyOptions>()
            .ShouldBe(TimesheetsEvidencePolicyOptions.FailClosedDefault);
        provider.GetRequiredService<IProjectReferenceValidator>()
            .ShouldBeOfType<DenyAllProjectReferenceValidator>();
        provider.GetRequiredService<IWorkReferenceValidator>()
            .ShouldBeOfType<DenyAllWorkReferenceValidator>();
        provider.GetRequiredService<IContributorPartyValidator>()
            .ShouldBeOfType<DenyAllContributorPartyValidator>();
        provider.GetRequiredService<ITimeEntryEvidenceProjectionReader>()
            .ShouldBeOfType<UnavailableTimeEntryEvidenceProjectionReader>();
        provider.GetRequiredService<ITimeEntryDisplayHydrator>()
            .ShouldBeOfType<UnavailableTimeEntryDisplayHydrator>();
        provider.GetRequiredService<IPartyDisplayHydrationProvider>()
            .ShouldBeOfType<UnavailableDisplayHydrationProvider>();
        provider.GetRequiredService<IProjectDisplayHydrationProvider>()
            .ShouldBeOfType<UnavailableDisplayHydrationProvider>();
        provider.GetRequiredService<IWorkDisplayHydrationProvider>()
            .ShouldBeOfType<UnavailableDisplayHydrationProvider>();
        provider.GetRequiredService<IActivityTypeDisplayHydrationProvider>()
            .ShouldBeOfType<UnavailableDisplayHydrationProvider>();
    }

    [Fact]
    public async Task Composed_access_guard_fails_closed_with_unconfigured_defaults()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddTimesheetsServerKernel();

        using ServiceProvider provider = services.BuildServiceProvider();

        ITimesheetsAccessGuard guard = provider.GetRequiredService<ITimesheetsAccessGuard>();

        TimesheetsAuthorizationRequest request = new(
            new TimesheetsRequestContext(
                new TenantReference("tenant_01"),
                new PartyReference("party_01"),
                "correlation_01"),
            TimesheetsOperation.Command)
        {
            Project = new ProjectReference("project_01")
        };

        TimesheetsAuthorizationDecision decision = await guard.AuthorizeAsync(
            request,
            TestContext.Current.CancellationToken);

        decision.IsAuthorized.ShouldBeFalse();
        decision.DenialCategory.ShouldBe(TimesheetsDenialCategory.UnconfiguredPolicy);
        decision.Reason.ShouldBe("Authority cannot be resolved.");
    }
}
