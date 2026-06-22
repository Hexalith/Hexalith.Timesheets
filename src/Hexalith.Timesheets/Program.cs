using Hexalith.Timesheets.Contracts;
using Hexalith.Timesheets.Endpoints;
using Hexalith.Timesheets.Endpoints.MagicLinks;
using Hexalith.Timesheets.Runtime;
using Hexalith.Timesheets.Server.Runtime;
using Hexalith.Timesheets.ServiceDefaults;

using Microsoft.Extensions.DependencyInjection.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Wire observability/telemetry defaults and the fail-closed Timesheets server kernel
// (authorization gate and reference validators) so future EventStore command handling
// has its registration seams present from the first executable slice.
builder.AddTimesheetsServiceDefaults();
builder.Services.AddTimesheetsServerKernel();
builder.Services.AddHttpContextAccessor();
builder.Services.Replace(ServiceDescriptor.Singleton<ITimesheetsTrustedContextAccessor, HttpContextTimesheetsTrustedContextAccessor>());
builder.Services.AddSingleton(TimeProvider.System);

WebApplication app = builder.Build();

app.MapTimesheetsDefaultEndpoints();
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
    }));

await app.RunAsync().ConfigureAwait(false);
