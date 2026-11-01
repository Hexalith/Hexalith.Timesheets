using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Hexalith.Timesheets.Runtime;

/// <summary>Registers the capability credential without replacing the SDK's authentication defaults.</summary>
internal static class TimesheetsAuthenticationExtensions
{
    /// <summary>Wires existing human credentials and adds the capability policy for the four external link routes.</summary>
    /// <param name="services">The host services.</param>
    /// <returns>The host services.</returns>
    internal static IServiceCollection AddTimesheetsEndpointAuthentication(this IServiceCollection services)
    {
        // The domain-service SDK supplies its named human policy but leaves registration of
        // the human credential handler to the composing host. Reuse its shared JWT contract.
        services.AddOptions<JwtBearerAuthenticationOptions>()
            .BindConfiguration(EventStoreWorkloadAuthenticationDefaults.JwtContractSection)
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<JwtBearerAuthenticationOptions>,
            ValidateJwtBearerAuthenticationOptions>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<JwtBearerOptions>,
            ConfigureJwtBearerAuthenticationOptions>());
        services.AddAuthentication()
            .AddJwtBearer()
            .AddScheme<AuthenticationSchemeOptions, MagicLinkCapabilityAuthenticationHandler>(
                TimesheetsAuthenticationDefaults.CapabilityPolicy, static _ => { });
        services.AddAuthorizationBuilder()
            .AddPolicy(TimesheetsAuthenticationDefaults.CapabilityPolicy, static policy => policy
                .AddAuthenticationSchemes(TimesheetsAuthenticationDefaults.CapabilityPolicy)
                .RequireAuthenticatedUser());
        return services;
    }
}
