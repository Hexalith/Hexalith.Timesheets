using System.Security.Claims;

using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.MagicLinks;

using ServerMagicLinkCapabilityState = Hexalith.Timesheets.Server.MagicLinks.MagicLinkCapabilityState;

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
                IMagicLinkConfirmationCapabilityStateLoader stateLoader,
                TimeProvider timeProvider,
                CancellationToken cancellationToken) =>
            {
                ClaimsPrincipal user = httpContext.User;
                ActivityTypeCatalogReadModel catalog = await stateLoader
                    .LoadActivityTypeCatalogAsync(cancellationToken)
                    .ConfigureAwait(false);
                ServerMagicLinkCapabilityState? state = await stateLoader
                    .LoadCapabilityAsync(command.CapabilityId, cancellationToken)
                    .ConfigureAwait(false);
                MagicLinkCapabilityCommandResult result = await service.IssueAsync(
                    TimesheetsServerRequestContext.FromTrustedSources(
                        FirstClaimValue(user, "tenant_id", "tenant"),
                        FirstClaimValue(user, "party_id", ClaimTypes.NameIdentifier),
                        httpContext.TraceIdentifier),
                    command,
                    state,
                    catalog,
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
                IMagicLinkConfirmationCapabilityStateLoader stateLoader,
                TimeProvider timeProvider,
                CancellationToken cancellationToken) =>
            {
                if (!StringComparer.Ordinal.Equals(capabilityId, command.CapabilityId.Value))
                {
                    return Denied();
                }

                ClaimsPrincipal user = httpContext.User;
                ServerMagicLinkCapabilityState? state = await stateLoader
                    .LoadCapabilityAsync(command.CapabilityId, cancellationToken)
                    .ConfigureAwait(false);
                MagicLinkCapabilityCommandResult result = await service.RevokeAsync(
                    TimesheetsServerRequestContext.FromTrustedSources(
                        FirstClaimValue(user, "tenant_id", "tenant"),
                        FirstClaimValue(user, "party_id", ClaimTypes.NameIdentifier),
                        httpContext.TraceIdentifier),
                    command,
                    state,
                    timeProvider.GetUtcNow(),
                    cancellationToken).ConfigureAwait(false);

                return result.Authorization.IsAuthorized && result.DomainResult?.IsRejection != true
                    ? Results.Accepted()
                    : Denied();
            });

        group.MapPost(
            "/{capabilityId}/expire",
            static async Task<IResult> (
                string capabilityId,
                ExpireMagicLinkConfirmationCapability command,
                HttpContext httpContext,
                MagicLinkConfirmationCapabilityCommandService service,
                IMagicLinkConfirmationCapabilityStateLoader stateLoader,
                TimeProvider timeProvider) =>
            {
                if (!StringComparer.Ordinal.Equals(capabilityId, command.CapabilityId.Value))
                {
                    return Denied();
                }

                ClaimsPrincipal user = httpContext.User;
                ServerMagicLinkCapabilityState? state = await stateLoader
                    .LoadCapabilityAsync(command.CapabilityId, httpContext.RequestAborted)
                    .ConfigureAwait(false);
                return service.Expire(
                    command,
                    state,
                    TimesheetsServerRequestContext.FromTrustedSources(
                        FirstClaimValue(user, "tenant_id", "tenant"),
                        FirstClaimValue(user, "party_id", ClaimTypes.NameIdentifier),
                        httpContext.TraceIdentifier),
                    timeProvider.GetUtcNow()).IsRejection
                    ? Denied()
                    : Results.Accepted();
            });

        endpoints.MapGet(
            "/api/timesheets/magic-links/confirm",
            static async Task<IResult> (
                string? t,
                HttpContext httpContext,
                MagicLinkConfirmationCapabilityCommandService service,
                IMagicLinkConfirmationCapabilityStateLoader stateLoader,
                TimeProvider timeProvider,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(t))
                {
                    return Denied();
                }

                ClaimsPrincipal user = httpContext.User;
                MagicLinkEndpointTokenState state = await stateLoader
                    .LoadTokenStateAsync(t, cancellationToken)
                    .ConfigureAwait(false);
                MagicLinkConfirmationDisplayResponse? response = await service.DescribeAsync(
                    TimesheetsServerRequestContext.FromTrustedSources(
                        FirstClaimValue(user, "tenant_id", "tenant"),
                        FirstClaimValue(user, "party_id", ClaimTypes.NameIdentifier),
                        httpContext.TraceIdentifier),
                    t,
                    state.CapabilityState,
                    state.TimeEntryState,
                    state.ActivityTypeCatalog,
                    timeProvider.GetUtcNow(),
                    cancellationToken).ConfigureAwait(false);

                return response is null ? Denied() : Results.Ok(response);
            });

        endpoints.MapPost(
            "/api/timesheets/magic-links/confirm/submit",
            static async Task<IResult> (
                string? t,
                ConfirmTimeThroughMagicLink command,
                HttpContext httpContext,
                MagicLinkConfirmationCapabilityCommandService service,
                IMagicLinkConfirmationCapabilityStateLoader stateLoader,
                TimeProvider timeProvider,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(t))
                {
                    return Denied();
                }

                ClaimsPrincipal user = httpContext.User;
                MagicLinkEndpointTokenState state = await stateLoader
                    .LoadTokenStateAsync(t, cancellationToken)
                    .ConfigureAwait(false);
                MagicLinkConfirmationUseResult result = await service.ConfirmAsync(
                    TimesheetsServerRequestContext.FromTrustedSources(
                        FirstClaimValue(user, "tenant_id", "tenant"),
                        FirstClaimValue(user, "party_id", ClaimTypes.NameIdentifier),
                        httpContext.TraceIdentifier),
                    t,
                    command,
                    state.CapabilityState,
                    state.TimeEntryState,
                    timeProvider.GetUtcNow(),
                    cancellationToken).ConfigureAwait(false);

                return result.WasDispatched ? Results.Accepted() : Denied();
            });

        endpoints.MapGet(
            "/api/timesheets/magic-links/adjust",
            static async Task<IResult> (
                string? t,
                HttpContext httpContext,
                MagicLinkConfirmationCapabilityCommandService service,
                IMagicLinkConfirmationCapabilityStateLoader stateLoader,
                TimeProvider timeProvider,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(t))
                {
                    return Denied();
                }

                ClaimsPrincipal user = httpContext.User;
                MagicLinkEndpointTokenState state = await stateLoader
                    .LoadTokenStateAsync(t, cancellationToken)
                    .ConfigureAwait(false);
                MagicLinkAdjustmentDisplayResponse? response = await service.DescribeAdjustmentAsync(
                    TimesheetsServerRequestContext.FromTrustedSources(
                        FirstClaimValue(user, "tenant_id", "tenant"),
                        FirstClaimValue(user, "party_id", ClaimTypes.NameIdentifier),
                        httpContext.TraceIdentifier),
                    t,
                    state.CapabilityState,
                    state.TimeEntryState,
                    state.ActivityTypeCatalog,
                    timeProvider.GetUtcNow(),
                    cancellationToken).ConfigureAwait(false);

                return response is null ? Denied() : Results.Ok(response);
            });

        endpoints.MapPost(
            "/api/timesheets/magic-links/adjust/submit",
            static async Task<IResult> (
                string? t,
                AdjustTimeThroughMagicLink command,
                HttpContext httpContext,
                MagicLinkConfirmationCapabilityCommandService service,
                IMagicLinkConfirmationCapabilityStateLoader stateLoader,
                TimeProvider timeProvider,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(t))
                {
                    return Denied();
                }

                ClaimsPrincipal user = httpContext.User;
                MagicLinkEndpointTokenState state = await stateLoader
                    .LoadTokenStateAsync(t, cancellationToken)
                    .ConfigureAwait(false);
                MagicLinkConfirmationUseResult result = await service.AdjustAsync(
                    TimesheetsServerRequestContext.FromTrustedSources(
                        FirstClaimValue(user, "tenant_id", "tenant"),
                        FirstClaimValue(user, "party_id", ClaimTypes.NameIdentifier),
                        httpContext.TraceIdentifier),
                    t,
                    command,
                    state.CapabilityState,
                    state.TimeEntryState,
                    state.ActivityTypeCatalog,
                    timeProvider.GetUtcNow(),
                    cancellationToken).ConfigureAwait(false);

                return result.WasDispatched ? Results.Accepted() : Denied();
            });

        return endpoints;
    }

    private static IResult Denied()
        => Results.Problem(
            title: MagicLinkInvalidLinkDenial.Default.Title,
            detail: MagicLinkInvalidLinkDenial.Default.Detail,
            statusCode: StatusCodes.Status403Forbidden);

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
