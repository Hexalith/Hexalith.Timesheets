using System.Security.Claims;

using Hexalith.EventStore.DomainService;

using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks.Commands;

using Microsoft.Extensions.Logging;

using ServerMagicLinkCapabilityState = Hexalith.Timesheets.Server.MagicLinks.MagicLinkCapabilityState;

namespace Hexalith.Timesheets.Endpoints.MagicLinks;

public static partial class MagicLinkConfirmationCapabilityEndpoints
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
                MagicLinkDurableSubmissionService submission,
                IMagicLinkTokenGenerator tokenGenerator,
                IMagicLinkConfirmationCapabilityStateLoader stateLoader,
                TimeProvider timeProvider,
                CancellationToken cancellationToken) =>
            {
                if (command.CapabilityId is null)
                {
                    return Denied();
                }

                ClaimsPrincipal user = httpContext.User;
                ActivityTypeCatalogReadModel catalog = await stateLoader
                    .LoadActivityTypeCatalogAsync(cancellationToken)
                    .ConfigureAwait(false);
                ServerMagicLinkCapabilityState? state = await stateLoader
                    .LoadCapabilityAsync(command.CapabilityId, cancellationToken)
                    .ConfigureAwait(false);
                var trustedContext = TimesheetsServerRequestContext.FromTrustedSources(
                        FirstClaimValue(user, "tenant_id", "tenant"),
                        FirstClaimValue(user, "party_id", ClaimTypes.NameIdentifier),
                        httpContext.TraceIdentifier);
                DateTimeOffset issuedAtUtc = timeProvider.GetUtcNow();
                MagicLinkCapabilityCommandResult result = await service.IssueAsync(
                    trustedContext,
                    command,
                    state,
                    catalog,
                    issuedAtUtc,
                    cancellationToken).ConfigureAwait(false);

                if (result.Authorization.IsAuthorized
                    && result.DomainResult?.IsSuccess == true
                    && result.IssueResponse is { } response
                    && trustedContext.Tenant is { } tenant
                    && trustedContext.Actor is { } issuer
                    && await submission.SubmitIssueAsync(
                        new CommitMagicLinkIssue(
                            command, tenant, issuer,
                            tokenGenerator.DeriveHash(response.OneTimeToken), issuedAtUtc),
                        httpContext.TraceIdentifier,
                        cancellationToken).ConfigureAwait(false))
                {
                    return Results.Accepted(value: response);
                }

                return Denied();
            });

        group.MapPost(
            "/{capabilityId}/revoke",
            static async Task<IResult> (
                string capabilityId,
                RevokeMagicLinkConfirmationCapability command,
                HttpContext httpContext,
                MagicLinkConfirmationCapabilityCommandService service,
                MagicLinkDurableSubmissionService submission,
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
                var trustedContext = TimesheetsServerRequestContext.FromTrustedSources(
                        FirstClaimValue(user, "tenant_id", "tenant"),
                        FirstClaimValue(user, "party_id", ClaimTypes.NameIdentifier),
                        httpContext.TraceIdentifier);
                DateTimeOffset revokedAtUtc = timeProvider.GetUtcNow();
                MagicLinkCapabilityCommandResult result = await service.RevokeAsync(
                    trustedContext,
                    command,
                    state,
                    revokedAtUtc,
                    cancellationToken).ConfigureAwait(false);

                return result.Authorization.IsAuthorized
                    && result.DomainResult?.IsSuccess == true
                    && state?.TimeEntryId is { } timeEntryId
                    && trustedContext.Tenant is { } tenant
                    && trustedContext.Actor is { } actor
                    && await submission.SubmitTransitionAsync(
                        new CommitMagicLinkTransition(
                            command.CapabilityId, tenant, timeEntryId, actor,
                            MagicLinkTransitionAction.Revoke, command.Source, revokedAtUtc),
                        httpContext.TraceIdentifier,
                        cancellationToken).ConfigureAwait(false)
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
                MagicLinkDurableSubmissionService submission,
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
                    .LoadCapabilityAsync(command.CapabilityId, httpContext.RequestAborted)
                    .ConfigureAwait(false);
                var trustedContext = TimesheetsServerRequestContext.FromTrustedSources(
                    FirstClaimValue(user, "tenant_id", "tenant"),
                    FirstClaimValue(user, "party_id", ClaimTypes.NameIdentifier),
                    httpContext.TraceIdentifier);
                DateTimeOffset expiredAtUtc = timeProvider.GetUtcNow();
                return !service.Expire(
                    command,
                    state,
                    trustedContext,
                    expiredAtUtc).IsRejection
                    && state?.TimeEntryId is { } timeEntryId
                    && trustedContext.Tenant is { } tenant
                    && await submission.SubmitTransitionAsync(
                        new CommitMagicLinkTransition(
                            command.CapabilityId, tenant, timeEntryId, trustedContext.Actor,
                            MagicLinkTransitionAction.Expire, command.Source, expiredAtUtc),
                        httpContext.TraceIdentifier,
                        cancellationToken).ConfigureAwait(false)
                        ? Results.Accepted()
                        : Denied();
            });

        endpoints.MapGet(
            "/api/timesheets/magic-links/confirm",
            static async Task<IResult> (
                string? t,
                HttpContext httpContext,
                ILoggerFactory loggerFactory,
                MagicLinkConfirmationCapabilityCommandService service,
                IMagicLinkConfirmationCapabilityStateLoader stateLoader,
                TimeProvider timeProvider,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(t))
                {
                    return DeniedWithDiagnostics(loggerFactory, httpContext, timeProvider.GetUtcNow(), MagicLinkInvalidLinkOutcomeCategory.Malformed);
                }

                MagicLinkEndpointTokenState state = await stateLoader
                    .LoadTokenStateAsync(t, cancellationToken)
                    .ConfigureAwait(false);
                MagicLinkConfirmationDisplayResponse? response = await service.DescribeAsync(
                    MagicLinkExternalRequestContext.FromResolvedCapability(
                        state.CapabilityState,
                        httpContext.TraceIdentifier),
                    t,
                    state.CapabilityState,
                    state.TimeEntryState,
                    state.ActivityTypeCatalog,
                    timeProvider.GetUtcNow(),
                    cancellationToken).ConfigureAwait(false);

                return response is null
                    ? DeniedWithDiagnostics(loggerFactory, httpContext, timeProvider.GetUtcNow(), MagicLinkInvalidLinkOutcomeCategory.Unknown)
                    : Results.Ok(response);
            }).AllowEventStorePublicEndpoint("/api/timesheets/magic-links/confirm");

        endpoints.MapPost(
            "/api/timesheets/magic-links/confirm/submit",
            static async Task<IResult> (
                string? t,
                ConfirmTimeThroughMagicLink command,
                HttpContext httpContext,
                ILoggerFactory loggerFactory,
                MagicLinkConfirmationCapabilityCommandService service,
                MagicLinkDurableSubmissionService submission,
                IMagicLinkConfirmationCapabilityStateLoader stateLoader,
                TimeProvider timeProvider,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(t))
                {
                    return DeniedWithDiagnostics(loggerFactory, httpContext, timeProvider.GetUtcNow(), MagicLinkInvalidLinkOutcomeCategory.Malformed);
                }

                MagicLinkEndpointTokenState state = await stateLoader
                    .LoadTokenStateAsync(t, cancellationToken)
                    .ConfigureAwait(false);
                if (state.ActivityTypeCatalog.ProjectionFreshness.State != ProjectionFreshnessState.Fresh)
                {
                    // ConfirmAsync takes no catalog, so this gate is load-bearing rather than
                    // defence in depth: the other three routes gate inside the command service.
                    //
                    // Catalog status is retained internally but must not change the public denial
                    // or diagnostics category.
                    return DeniedWithDiagnostics(
                        loggerFactory,
                        httpContext,
                        timeProvider.GetUtcNow(),
                        MagicLinkInvalidLinkOutcomeCategory.Unknown);
                }

                DateTimeOffset usedAtUtc = timeProvider.GetUtcNow();
                MagicLinkConfirmationUseResult result = await service.ConfirmAsync(
                    MagicLinkExternalRequestContext.FromResolvedCapability(
                        state.CapabilityState,
                        httpContext.TraceIdentifier),
                    t,
                    command,
                    state.CapabilityState,
                    state.TimeEntryState,
                    usedAtUtc,
                    cancellationToken).ConfigureAwait(false);

                if (result.WasDispatched
                    && state.CapabilityState is { } resolvedCapability
                    && await submission.SubmitResolvedUseAsync(
                        resolvedCapability, MagicLinkUseAction.Confirm, null, usedAtUtc,
                        httpContext.TraceIdentifier,
                        cancellationToken).ConfigureAwait(false))
                {
                    return Results.Accepted();
                }

                return DeniedWithDiagnostics(loggerFactory, httpContext, timeProvider.GetUtcNow(), MagicLinkInvalidLinkOutcomeCategory.Unknown);
            }).AllowEventStorePublicEndpoint("/api/timesheets/magic-links/confirm/submit");

        endpoints.MapGet(
            "/api/timesheets/magic-links/adjust",
            static async Task<IResult> (
                string? t,
                HttpContext httpContext,
                ILoggerFactory loggerFactory,
                MagicLinkConfirmationCapabilityCommandService service,
                IMagicLinkConfirmationCapabilityStateLoader stateLoader,
                TimeProvider timeProvider,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(t))
                {
                    return DeniedWithDiagnostics(loggerFactory, httpContext, timeProvider.GetUtcNow(), MagicLinkInvalidLinkOutcomeCategory.Malformed);
                }

                MagicLinkEndpointTokenState state = await stateLoader
                    .LoadTokenStateAsync(t, cancellationToken)
                    .ConfigureAwait(false);
                MagicLinkAdjustmentDisplayResponse? response = await service.DescribeAdjustmentAsync(
                    MagicLinkExternalRequestContext.FromResolvedCapability(
                        state.CapabilityState,
                        httpContext.TraceIdentifier),
                    t,
                    state.CapabilityState,
                    state.TimeEntryState,
                    state.ActivityTypeCatalog,
                    timeProvider.GetUtcNow(),
                    cancellationToken).ConfigureAwait(false);

                return response is null
                    ? DeniedWithDiagnostics(loggerFactory, httpContext, timeProvider.GetUtcNow(), MagicLinkInvalidLinkOutcomeCategory.Unknown)
                    : Results.Ok(response);
            }).AllowEventStorePublicEndpoint("/api/timesheets/magic-links/adjust");

        endpoints.MapPost(
            "/api/timesheets/magic-links/adjust/submit",
            static async Task<IResult> (
                string? t,
                AdjustTimeThroughMagicLink command,
                HttpContext httpContext,
                ILoggerFactory loggerFactory,
                MagicLinkConfirmationCapabilityCommandService service,
                MagicLinkDurableSubmissionService submission,
                IMagicLinkConfirmationCapabilityStateLoader stateLoader,
                TimeProvider timeProvider,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(t))
                {
                    return DeniedWithDiagnostics(loggerFactory, httpContext, timeProvider.GetUtcNow(), MagicLinkInvalidLinkOutcomeCategory.Malformed);
                }

                MagicLinkEndpointTokenState state = await stateLoader
                    .LoadTokenStateAsync(t, cancellationToken)
                    .ConfigureAwait(false);
                DateTimeOffset usedAtUtc = timeProvider.GetUtcNow();
                MagicLinkConfirmationUseResult result = await service.AdjustAsync(
                    MagicLinkExternalRequestContext.FromResolvedCapability(
                        state.CapabilityState,
                        httpContext.TraceIdentifier),
                    t,
                    command,
                    state.CapabilityState,
                    state.TimeEntryState,
                    state.ActivityTypeCatalog,
                    usedAtUtc,
                    cancellationToken).ConfigureAwait(false);

                if (result.WasDispatched
                    && state.CapabilityState is { } resolvedCapability
                    && await submission.SubmitResolvedUseAsync(
                        resolvedCapability, MagicLinkUseAction.Adjust, command, usedAtUtc,
                        httpContext.TraceIdentifier,
                        cancellationToken).ConfigureAwait(false))
                {
                    return Results.Accepted();
                }

                return DeniedWithDiagnostics(loggerFactory, httpContext, timeProvider.GetUtcNow(), MagicLinkInvalidLinkOutcomeCategory.Unknown);
            }).AllowEventStorePublicEndpoint("/api/timesheets/magic-links/adjust/submit");

        return endpoints;
    }

    private static IResult Denied()
        => Results.Problem(
            title: MagicLinkInvalidLinkDenial.Default.Title,
            detail: MagicLinkInvalidLinkDenial.Default.Detail,
            statusCode: StatusCodes.Status403Forbidden);

    private static IResult DeniedWithDiagnostics(
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        DateTimeOffset timestampUtc,
        MagicLinkInvalidLinkOutcomeCategory category)
    {
        ILogger logger = loggerFactory.CreateLogger("Hexalith.Timesheets.MagicLinkBoundary");
        LogExternalLinkDenial(logger, httpContext.TraceIdentifier, timestampUtc, category);
        return Denied();
    }

    [LoggerMessage(
        EventId = 37001,
        Level = LogLevel.Information,
        Message = "External link denial emitted with category {Category} at {TimestampUtc} for correlation {CorrelationId}.")]
    private static partial void LogExternalLinkDenial(
        ILogger logger,
        string correlationId,
        DateTimeOffset timestampUtc,
        MagicLinkInvalidLinkOutcomeCategory category);

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
