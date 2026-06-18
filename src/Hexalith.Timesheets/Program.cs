using Hexalith.Timesheets.Server.Runtime;
using Hexalith.Timesheets.ServiceDefaults;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Wire observability/telemetry defaults and the fail-closed Timesheets server kernel
// (authorization gate and reference validators) so future EventStore command handling
// has its registration seams present from the first executable slice.
builder.AddTimesheetsServiceDefaults();
builder.Services.AddTimesheetsServerKernel();

WebApplication app = builder.Build();

app.MapTimesheetsDefaultEndpoints();

app.MapGet(
    "/metadata/timesheets",
    static () => Results.Ok(new
    {
        Module = "Hexalith.Timesheets",
        EventStoreDomain = "timesheets",
        RegistrationAssembly = "Hexalith.Timesheets.Server"
    }));

await app.RunAsync().ConfigureAwait(false);
