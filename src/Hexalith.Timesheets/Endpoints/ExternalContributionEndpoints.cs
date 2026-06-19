using System.Security.Claims;

using Hexalith.Timesheets.Contracts.Commands.ExternalContributions;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.TimeEntries;

namespace Hexalith.Timesheets.Endpoints;

public static class ExternalContributionEndpoints
{
    public static IEndpointRouteBuilder MapTimesheetsExternalContributionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/timesheets/external-contributions");

        group.MapPost(
            "/",
            static async Task<IResult> (
                SubmitExternalTimeEntry command,
                HttpContext httpContext,
                ExternalContributionCommandService service,
                CancellationToken cancellationToken) =>
            {
                ClaimsPrincipal user = httpContext.User;
                ExternalContributionCommandResult result = await service.SubmitAsync(
                    TimesheetsServerRequestContext.FromTrustedSources(
                        FirstClaimValue(user, "tenant_id", "tenant"),
                        FirstClaimValue(user, "party_id", ClaimTypes.NameIdentifier),
                        httpContext.TraceIdentifier),
                    command,
                    null,
                    UnavailableCatalog(),
                    DateTimeOffset.UtcNow,
                    cancellationToken).ConfigureAwait(false);

                return ToTransportResult(result.RecordResult.Authorization.IsAuthorized
                    && result.RecordResult.DomainResult?.IsRejection != true);
            });

        group.MapPost(
            "/{timeEntryId}/confirm",
            static async Task<IResult> (
                string timeEntryId,
                ConfirmExternalTimeEntry command,
                HttpContext httpContext,
                ExternalContributionCommandService service,
                CancellationToken cancellationToken) =>
            {
                if (!StringComparer.Ordinal.Equals(timeEntryId, command.TimeEntryId.Value))
                {
                    return Results.Problem(
                        title: "External contribution request was not accepted.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                ClaimsPrincipal user = httpContext.User;
                TimeEntryConfirmationCommandResult result = await service.ConfirmAsync(
                    TimesheetsServerRequestContext.FromTrustedSources(
                        FirstClaimValue(user, "tenant_id", "tenant"),
                        FirstClaimValue(user, "party_id", ClaimTypes.NameIdentifier),
                        httpContext.TraceIdentifier),
                    command,
                    null,
                    DateTimeOffset.UtcNow,
                    cancellationToken).ConfigureAwait(false);

                return ToTransportResult(result.Authorization.IsAuthorized
                    && result.DomainResult?.IsRejection != true);
            });

        return endpoints;
    }

    private static IResult ToTransportResult(bool accepted)
        => accepted
            ? Results.Accepted()
            : Results.Problem(
                title: "External contribution request was not accepted.",
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
