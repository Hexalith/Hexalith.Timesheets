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
        services.TryAddSingleton<IMagicLinkV1VerifiedInventoryProvider,
            UnavailableMagicLinkV1VerifiedInventoryProvider>();
        services.AddSingleton<MagicLinkTokenHashCapabilityIndexV1Migration>();
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
                catch (Exception error) when (error is UnauthorizedAccessException or ArgumentException)
                {
                    return Results.NotFound();
                }
            }).RequireAuthorization();
        endpoints.MapPost("/admin/timesheets/magic-link-index/migrate-v1",
            async (MagicLinkV1MigrationRequest request,
                MagicLinkTokenHashCapabilityIndexV1Migration migration,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    await migration.MigrateAsync(request.OperationId, request.ReceiptId,
                        cancellationToken).ConfigureAwait(false);
                    return Results.NoContent();
                }
                catch (Exception error) when (error is UnauthorizedAccessException or ArgumentException or MagicLinkV1MigrationValidationException)
                {
                    return Results.NotFound();
                }
                catch (Exception error) when (error is InvalidOperationException or IOException or HttpRequestException
                    or TimeoutException or OperationCanceledException
                    or Hexalith.EventStore.Client.Projections.SharedProjectionReadUnavailableException
                    or Hexalith.EventStore.Client.Projections.SharedProjectionWriteUnavailableException)
                {
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
                }
            }).RequireAuthorization();
        return endpoints;
    }
}

/// <summary>Operator request; authority and inventory verification remain server-side.</summary>
public sealed record MagicLinkV6MigrationRequest(string OperationId,
    string CertifiedInventoryFingerprint);
