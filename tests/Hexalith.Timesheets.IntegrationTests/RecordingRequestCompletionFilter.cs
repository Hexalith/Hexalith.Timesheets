using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Hexalith.Timesheets.IntegrationTests;

internal sealed class RecordingRequestCompletionFilter(Action<Exception?> onCompleted) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                Exception? failure = null;
                try
                {
                    await nextMiddleware(context);
                }
                catch (Exception exception)
                {
                    failure = exception;
                    throw;
                }
                finally
                {
                    if (context.Request.Path == "/api/timesheets/external-contributions/")
                    {
                        onCompleted(failure);
                    }
                }
            });
            next(app);
        };
}
