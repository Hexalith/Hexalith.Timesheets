using Hexalith.Timesheets.Projections.MagicLinks;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.Timesheets.Endpoints.MagicLinks;

/// <summary>Internal, authenticated operator route for the one-time selected-index migration.</summary>
public static class MagicLinkIndexMigrationEndpoints
{
    /// <summary>Registers the default-deny server-side authority and migration service.</summary>
    public static IServiceCollection AddTimesheetsIndexMigration(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IMagicLinkIndexMigrationAuthority,
            DenyAllMagicLinkIndexMigrationAuthority>();
        services.AddSingleton<MagicLinkTokenHashCapabilityIndexV6Migration>();
        return services;
    }

    /// <summary>Maps the internal operator route behind the host's port and auth guards.</summary>
    public static IEndpointRouteBuilder MapTimesheetsIndexMigrationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapPost("/admin/timesheets/magic-link-index/migrate-v6",
            async (MagicLinkV6MigrationRequest request,
                MagicLinkTokenHashCapabilityIndexV6Migration migration,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    await migration.MigrateAsync(request.OperationId,
                        request.CertifiedInventoryFingerprint, cancellationToken)
                        .ConfigureAwait(false);
                    return Results.NoContent();
                }
                catch (UnauthorizedAccessException)
                {
                    return Results.NotFound();
                }
            }).RequireAuthorization();
        return endpoints;
    }
}

/// <summary>Operator request; authority and inventory verification remain server-side.</summary>
public sealed record MagicLinkV6MigrationRequest(string OperationId,
    string CertifiedInventoryFingerprint);
