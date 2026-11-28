using System.Runtime.CompilerServices;
using System.Text.Json;

using Hexalith.Timesheets.Contracts.Commands.ExternalContributions;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Runtime;
using Hexalith.Timesheets.Server.TimeEntries;

using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace Hexalith.Timesheets.Endpoints;

public static class ExternalContributionEndpoints
{
    private static readonly ConditionalWeakTable<JsonSerializerOptions, JsonSerializerOptions> _strictBodyJsonOptions = new();
    public static IEndpointRouteBuilder MapTimesheetsExternalContributionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/timesheets/external-contributions")
            .RequireAuthorization(TimesheetsAuthenticationDefaults.ExternalContributionPolicy);

        group.MapPost(
            "/",
            static async Task<IResult> (
                SubmitExternalTimeEntry command,
                HttpContext httpContext,
                TimeEntryRecordSubmissionService service,
                CancellationToken cancellationToken) =>
            {
                if (ExternalContributionRequestContext.Resolve(httpContext) is not { } identity)
                {
                    return ToTransportResult(false);
                }

                var context = TimesheetsServerRequestContext.FromTrustedSources(
                    identity.TenantId, identity.ActorId, httpContext.TraceIdentifier);
                bool accepted = await service.SubmitAsync(
                    context,
                    command,
                    DateTimeOffset.UtcNow,
                    cancellationToken).ConfigureAwait(false);

                return ToTransportResult(accepted);
            });

        group.MapPost(
            "/{timeEntryId}/confirm",
            static async Task<IResult> (
                string timeEntryId,
                JsonElement? body,
                HttpContext httpContext,
                TimeEntryConfirmationSubmissionService service,
                TimeProvider clock,
                IOptions<JsonOptions> jsonOptions,
                CancellationToken cancellationToken) =>
            {
                ConfirmExternalTimeEntry? command;
                try
                {
                    command = body?.Deserialize<ConfirmExternalTimeEntry>(StrictBodyJsonOptions(jsonOptions.Value.SerializerOptions));
                }
                catch (Exception exception) when (exception is JsonException or ArgumentException)
                {
                    return InvalidConfirmationBody();
                }

                if (command?.TimeEntryId is null
                    || string.IsNullOrWhiteSpace(command.TimeEntryId.Value)
                    || !StringComparer.Ordinal.Equals(timeEntryId, command.TimeEntryId.Value))
                {
                    return InvalidConfirmationBody();
                }

                (string? TenantId, string? ActorId)? identity = ExternalContributionRequestContext.Resolve(httpContext);
                var context = TimesheetsServerRequestContext.FromTrustedSources(
                    identity?.TenantId, identity?.ActorId, httpContext.TraceIdentifier);
                bool accepted = await service.ConfirmAsync(
                    context,
                    command,
                    clock.GetUtcNow(),
                    cancellationToken).ConfigureAwait(false);

                return ToTransportResult(accepted);
            })
            .Accepts<ConfirmExternalTimeEntry>("application/json");

        return endpoints;
    }

    private static JsonSerializerOptions StrictBodyJsonOptions(JsonSerializerOptions source)
        => _strictBodyJsonOptions.GetValue(source, static options => new JsonSerializerOptions(options)
        {
            AllowDuplicateProperties = false
        });

    private static IResult InvalidConfirmationBody()
        => Results.Problem(
            title: "External contribution request was not accepted.",
            statusCode: StatusCodes.Status400BadRequest);

    private static IResult ToTransportResult(bool accepted)
        => accepted
            ? Results.Accepted()
            : Results.Problem(
                title: "External contribution request was not accepted.",
                statusCode: StatusCodes.Status403Forbidden);
}
