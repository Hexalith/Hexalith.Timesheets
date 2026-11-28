using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Hexalith.Timesheets.IntegrationTests;

internal sealed class RecordingRequestCompletionFilter(Action onCompleted) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                try
                {
                    await nextMiddleware(context);
                }
                finally
                {
                    if (context.Request.Path == "/api/timesheets/external-contributions/")
                    {
                        onCompleted();
                    }
                }
            });
            next(app);
        };
}
