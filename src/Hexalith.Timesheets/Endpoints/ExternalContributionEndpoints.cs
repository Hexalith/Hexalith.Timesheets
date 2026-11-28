using System.Runtime.CompilerServices;
using System.Text.Json;

using Hexalith.Timesheets.Contracts.Commands.ExternalContributions;
using Hexalith.Timesheets.Runtime;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.TimeEntries;

using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Http.Metadata;
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
                HttpContext httpContext,
                TimeEntryRecordSubmissionService service,
                TimeProvider clock,
                IOptions<JsonOptions> jsonOptions,
                CancellationToken cancellationToken) =>
            {
                (string? TenantId, string? ActorId)? identity = ExternalContributionRequestContext.Resolve(httpContext);
                var context = TimesheetsServerRequestContext.FromTrustedSources(
                    identity?.TenantId, identity?.ActorId, httpContext.TraceIdentifier);
                SubmitExternalTimeEntry? command = null;
                if (context.Tenant is not null && context.Actor is not null)
                {
                    if (httpContext.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody != true)
                    {
                        return InvalidExternalContributionBody();
                    }

                    if (!httpContext.Request.HasJsonContentType())
                    {
                        return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
                    }

                    try
                    {
                        JsonSerializerOptions options = StrictBodyJsonOptions(jsonOptions.Value.SerializerOptions);
                        // Validate JSON syntax before domain constructors can reject values.
                        JsonElement body = await httpContext.Request.ReadFromJsonAsync<JsonElement>(
                            options, cancellationToken).ConfigureAwait(false);
                        ExternalRecordingBody? request = body.Deserialize<ExternalRecordingBody>(options);
                        if (request is null)
                        {
                            return InvalidExternalContributionBody();
                        }

                        command = request.ToCommand();
                    }
                    catch (JsonException)
                    {
                        return InvalidExternalContributionBody();
                    }
                    catch (ArgumentException)
                    {
                        // Domain value constructors reject blank or missing required values.
                    }
                }

                bool accepted = await service.SubmitAsync(
                    context,
                    command,
                    clock.GetUtcNow(),
                    cancellationToken).ConfigureAwait(false);

                return ToTransportResult(accepted);
            })
            // Body type/requiredness support API discovery; leave media selection to the
            // authenticated handler so routing cannot replace this route with a synthetic 415.
            .WithMetadata(new AcceptsMetadata([], typeof(SubmitExternalTimeEntry), isOptional: false));

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
                    return InvalidExternalContributionBody();
                }

                if (command?.TimeEntryId is null
                    || string.IsNullOrWhiteSpace(command.TimeEntryId.Value)
                    || !StringComparer.Ordinal.Equals(timeEntryId, command.TimeEntryId.Value))
                {
                    return InvalidExternalContributionBody();
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

    private static IResult InvalidExternalContributionBody()
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
