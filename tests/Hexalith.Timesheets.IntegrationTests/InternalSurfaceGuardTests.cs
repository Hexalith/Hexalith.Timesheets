using System.Net;

using Shouldly;

using Hexalith.Timesheets.Runtime;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hexalith.Timesheets.IntegrationTests;

/// <summary>
/// Proves the domain-service write surface is not published on the public ingress.
/// </summary>
public sealed class InternalSurfaceGuardTests
{
    public static TheoryData<string> ProtectedRoutes()
    {
        TheoryData<string> data = [];
        foreach (string route in new[]
        {
            "/process",
            "/replay-state",
            "/query",
            "/project",
            "/project/v2",
            "/project/v2/reconcile",
            "/project/rebuild/v1",
            "/project/rebuild/shared/v1",
            "/admin/operational-index-metadata",
            "/PROJECT/v2",
            "/Process",
            "/Admin/operational-index-metadata"
        })
        {
            data.Add(route);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task Domain_service_routes_are_refused_when_no_internal_port_is_configured(string route)
    {
        // Fail-closed is the whole point: a deployment that never configures the split must lose
        // projection delivery, not publish an unauthenticated write surface on its public ingress.
        using GuardedFactory factory = new(allowOnAnyPort: false);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsync(
            route,
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        // 404 rather than 403: a refusal that confirmed the route exists would hand an external
        // caller a map of the write surface.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>Lists the four public capability routes without supplying a user principal or workload credentials.</summary>
    /// <returns>The route and whether it requires POST.</returns>
    public static TheoryData<string, bool> PublicCapabilityRoutes() => new()
    {
        { "/api/timesheets/magic-links/confirm?t=opaque", false },
        { "/api/timesheets/magic-links/confirm/submit?t=opaque", true },
        { "/api/timesheets/magic-links/adjust?t=opaque", false },
        { "/api/timesheets/magic-links/adjust/submit?t=opaque", true }
    };

    /// <summary>Public capability routes reach their opaque denial without an authenticated principal.</summary>
    /// <param name="route">The public capability route.</param>
    /// <param name="post">Whether to send a POST body.</param>
    [Theory]
    [MemberData(nameof(PublicCapabilityRoutes))]
    public async Task The_public_magic_link_surface_stays_reachable_when_the_guard_is_closed(string route, bool post)
    {
        // The guard must not collapse the routes this host exists to publish.
        using GuardedFactory factory = new(allowOnAnyPort: false);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = post
            ? await client.PostAsync(
                route,
                new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
                TestContext.Current.CancellationToken)
            : await client.GetAsync(route, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Metadata_route_is_not_swept_up_by_the_guard()
    {
        using GuardedFactory factory = new(allowOnAnyPort: false);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            "/metadata/timesheets",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Management operations remain under the workload fallback when public capability routes are open.</summary>
    /// <param name="route">The protected management route.</param>
    [Theory]
    [InlineData("/api/timesheets/magic-links/confirmation-capabilities")]
    [InlineData("/api/timesheets/magic-links/confirmation-capabilities/capability-1/revoke")]
    [InlineData("/api/timesheets/magic-links/confirmation-capabilities/capability-1/expire")]
    public async Task Magic_link_management_routes_require_workload_credentials(string route)
    {
        using GuardedFactory factory = new(allowOnAnyPort: false);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsync(
            route,
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private sealed class GuardedFactory(bool allowOnAnyPort) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureLogging(static logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
                services.Configure<InternalSurfaceOptions>(
                    options => options.AllowOnAnyPort = allowOnAnyPort));
        }
    }
}
