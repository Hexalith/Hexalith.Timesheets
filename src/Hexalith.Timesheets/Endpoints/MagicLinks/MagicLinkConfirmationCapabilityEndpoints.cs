using System.Security.Claims;

using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.MagicLinks;

namespace Hexalith.Timesheets.Endpoints.MagicLinks;

public static class MagicLinkConfirmationCapabilityEndpoints
{
    public static IEndpointRouteBuilder MapTimesheetsMagicLinkConfirmationCapabilityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/timesheets/magic-links/confirmation-capabilities");

        group.MapPost(
            "/",
            static async Task<IResult> (
                IssueMagicLinkConfirmationCapability command,
                HttpContext httpContext,
                MagicLinkConfirmationCapabilityCommandService service,
                TimeProvider timeProvider,
                CancellationToken cancellationToken) =>
            {
                ClaimsPrincipal user = httpContext.User;
                MagicLinkCapabilityCommandResult result = await service.IssueAsync(
                    TimesheetsServerRequestContext.FromTrustedSources(
                        FirstClaimValue(user, "tenant_id", "tenant"),
                        FirstClaimValue(user, "party_id", ClaimTypes.NameIdentifier),
                        httpContext.TraceIdentifier),
                    command,
                    null,
                    UnavailableCatalog(),
                    timeProvider.GetUtcNow(),
                    cancellationToken).ConfigureAwait(false);

                return result.Authorization.IsAuthorized && result.DomainResult?.IsRejection != true
                    ? Results.Accepted(value: result.IssueResponse)
                    : Denied();
            });

        group.MapPost(
            "/{capabilityId}/revoke",
            static async Task<IResult> (
                string capabilityId,
                RevokeMagicLinkConfirmationCapability command,
                HttpContext httpContext,
                MagicLinkConfirmationCapabilityCommandService service,
                TimeProvider timeProvider,
                CancellationToken cancellationToken) =>
            {
                if (!StringComparer.Ordinal.Equals(capabilityId, command.CapabilityId.Value))
                {
                    return Denied();
                }

                ClaimsPrincipal user = httpContext.User;
                MagicLinkCapabilityCommandResult result = await service.RevokeAsync(
                    TimesheetsServerRequestContext.FromTrustedSources(
                        FirstClaimValue(user, "tenant_id", "tenant"),
                        FirstClaimValue(user, "party_id", ClaimTypes.NameIdentifier),
                        httpContext.TraceIdentifier),
                    command,
                    null,
                    timeProvider.GetUtcNow(),
                    cancellationToken).ConfigureAwait(false);

                return result.Authorization.IsAuthorized && result.DomainResult?.IsRejection != true
                    ? Results.Accepted()
                    : Denied();
            });

        group.MapPost(
            "/{capabilityId}/expire",
            static (
                string capabilityId,
                ExpireMagicLinkConfirmationCapability command,
                HttpContext httpContext,
                MagicLinkConfirmationCapabilityCommandService service,
                TimeProvider timeProvider) =>
            {
                if (!StringComparer.Ordinal.Equals(capabilityId, command.CapabilityId.Value))
                {
                    return Denied();
                }

                ClaimsPrincipal user = httpContext.User;
                return service.Expire(
                    command,
                    null,
                    TimesheetsServerRequestContext.FromTrustedSources(
                        FirstClaimValue(user, "tenant_id", "tenant"),
                        FirstClaimValue(user, "party_id", ClaimTypes.NameIdentifier),
                        httpContext.TraceIdentifier),
                    timeProvider.GetUtcNow()).IsRejection
                    ? Denied()
                    : Results.Accepted();
            });

        return endpoints;
    }

    private static IResult Denied()
        => Results.Problem(
            title: "Magic-link confirmation request was not accepted.",
            statusCode: StatusCodes.Status403Forbidden);

    private static ActivityTypeCatalogReadModel UnavailableCatalog()
        => new([], ProjectionFreshnessMetadata.Unavailable());

    private static string? FirstClaimValue(ClaimsPrincipal user, params string[] claimTypes)
    {
        foreach (string claimType in claimTypes)
        {
            string? value = user.FindFirstValue(claimType);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
