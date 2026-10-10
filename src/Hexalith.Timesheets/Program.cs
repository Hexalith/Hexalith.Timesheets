using Hexalith.Timesheets.Contracts;
using Hexalith.Timesheets.Endpoints;
using Hexalith.Timesheets.Endpoints.MagicLinks;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Runtime;
using Hexalith.Timesheets.Server.Runtime;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Client.Gateway;

using Microsoft.Extensions.DependencyInjection.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Request-start and request-finish messages include sensitive query values.
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
// Provider-specific configuration takes precedence over category-only rules. Apply the minimum
// after configuration while retaining each rule's behavior for every other category.
builder.Services.PostConfigure<LoggerFilterOptions>(static options =>
{
    for (int index = 0; index < options.Rules.Count; index++)
    {
        LoggerFilterRule rule = options.Rules[index];
        options.Rules[index] = new LoggerFilterRule(
            rule.ProviderName,
            rule.CategoryName,
            rule.LogLevel,
            (provider, category, level) =>
                (level >= LogLevel.Warning
                    || category?.StartsWith("Microsoft.AspNetCore.Hosting.Diagnostics", StringComparison.OrdinalIgnoreCase) != true)
                && (rule.Filter?.Invoke(provider, category, level) ?? true));
    }
});

// The canonical EventStore domain-service registration owns shared observability,
// health, service-discovery, and HTTP-resilience defaults for this host.
builder.AddEventStoreDomainService(
    TimesheetsEventStoreIntegration.RegistrationAssemblyMarker.Assembly,
    typeof(TimesheetsProjectionsMarker).Assembly);
// Keep the fail-closed Timesheets authorization and reference-validation seams.
builder.Services.AddTimesheetsServerKernel();
builder.Services.AddSingleton<IEventStoreGatewayWorkloadAssertionSource, EventStoreGatewayWorkloadAssertionSource>();
builder.Services.AddTimesheetsMagicLinkEventStoreProcessing();
builder.Services.AddHttpContextAccessor();
builder.Services.Replace(ServiceDescriptor.Singleton<ITimesheetsTrustedContextAccessor, HttpContextTimesheetsTrustedContextAccessor>());
builder.Services.AddSingleton(TimeProvider.System);

// Keep the SDK's protected routes on the internal listener. Fail-closed: unconfigured means
// unreachable, never public.
builder.Services.Configure<InternalSurfaceOptions>(
    builder.Configuration.GetSection(InternalSurfaceOptions.SectionName));

WebApplication app = builder.Build();

app.UseRouting();
app.UseTimesheetsInternalSurfaceGuard();
app.UseAuthentication();
app.UseAuthorization();
app.UseEventStoreDomainService();
app.MapTimesheetsExternalContributionEndpoints();
app.MapTimesheetsMagicLinkConfirmationCapabilityEndpoints();

app.MapGet(
    "/metadata/timesheets",
    static () => Results.Ok(new
    {
        Module = "Hexalith.Timesheets",
        ContractVersion = "1.0",
        Capabilities = TimesheetsMetadataCatalog.Descriptors
            .Select(static descriptor => descriptor.Capability)
            .Distinct(StringComparer.Ordinal)
            .ToArray(),
        MetadataDescriptors = TimesheetsMetadataCatalog.Descriptors
            .Select(static descriptor => descriptor.Name)
            .ToArray()
    })).AllowEventStorePublicEndpoint("/metadata/timesheets");

await app.RunAsync().ConfigureAwait(false);

/// <summary>
/// Entry point class, made partial so in-process HTTP-boundary tests can reference it via
/// <c>WebApplicationFactory&lt;Program&gt;</c>. This adds no runtime behavior.
/// </summary>
public partial class Program
{
}
