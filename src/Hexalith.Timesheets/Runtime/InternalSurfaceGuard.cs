using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Hexalith.Timesheets.Runtime;

/// <summary>
/// Refuses the EventStore domain-service surface on any port other than the configured internal one.
/// </summary>
public static class InternalSurfaceGuard
{
    /// <summary>The domain-service path prefixes this host must not publish externally.</summary>
    public static readonly string[] ProtectedPrefixes =
    [
        "/process",
        "/replay-state",
        "/query",
        "/project",
        "/admin"
    ];

    /// <summary>
    /// Adds the guard ahead of routing, so a refused request never reaches an SDK endpoint.
    /// </summary>
    /// <param name="app">The application to guard.</param>
    /// <returns>The application for chaining.</returns>
    public static WebApplication UseTimesheetsInternalSurfaceGuard(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        InternalSurfaceOptions options = app.Services
            .GetRequiredService<IOptions<InternalSurfaceOptions>>()
            .Value;

        _ = app.Use(async (context, next) =>
        {
            if (IsProtectedPath(context.Request.Path) && !IsInternal(context, options))
            {
                // 404, not 403: a refusal that confirmed the route exists would hand an external
                // caller a map of the write surface, which is the same disclosure discipline the
                // magic-link routes follow.
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await next(context).ConfigureAwait(false);
        });

        return app;
    }

    /// <summary>Determines whether a path belongs to the domain-service surface.</summary>
    /// <param name="path">The request path.</param>
    /// <returns><see langword="true"/> when the path is part of the protected surface.</returns>
    public static bool IsProtectedPath(PathString path)
        => ProtectedPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));

    private static bool IsInternal(HttpContext context, InternalSurfaceOptions options)
        => options.AllowOnAnyPort
            || (options.Port is { } port && context.Connection.LocalPort == port);
}
