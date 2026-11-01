using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;

namespace Hexalith.Timesheets.IntegrationTests;

/// <summary>Installs a controlled server-side body for real HttpClient cancellation tests.</summary>
/// <param name="body">The stream observed by framework request binding.</param>
internal sealed class HttpBoundaryBodyOverrideStartupFilter(Stream body) : IStartupFilter
{
    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        => app =>
        {
            app.Use(async (context, following) =>
            {
                context.Request.Body = body;
                context.Request.ContentType = "application/json";
                context.Request.ContentLength = 1;
                context.Features.Set<IHttpRequestBodyDetectionFeature>(new HttpBoundaryRequestBodyDetectionFeature());
                await following(context);
            });
            next(app);
        };
}
