using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.References;
using Hexalith.Timesheets.Server.Runtime;

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
        provider.GetRequiredService<IProjectReferenceValidator>()
            .ShouldBeOfType<DenyAllProjectReferenceValidator>();
        provider.GetRequiredService<IWorkReferenceValidator>()
            .ShouldBeOfType<DenyAllWorkReferenceValidator>();
        provider.GetRequiredService<IContributorPartyValidator>()
            .ShouldBeOfType<DenyAllContributorPartyValidator>();
    }
}
