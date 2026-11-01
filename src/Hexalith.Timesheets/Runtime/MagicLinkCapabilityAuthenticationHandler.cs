using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

using Hexalith.Timesheets.Endpoints.MagicLinks;
using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace Hexalith.Timesheets.Runtime;

/// <summary>Authenticates the opaque credential through the canonical bounded state loader.</summary>
/// <remarks>Authentication supplies no actor authority. The endpoint retains all action, state,
/// disclosure and durable retry checks, and EventStore processing revalidates before committing.</remarks>
/// <param name="options">The scheme options.</param>
/// <param name="logger">The authentication logger factory.</param>
/// <param name="encoder">The framework URL encoder.</param>
/// <param name="stateLoader">The canonical capability and scoped-state loader.</param>
internal sealed partial class MagicLinkCapabilityAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IMagicLinkConfirmationCapabilityStateLoader stateLoader)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private bool _faulted;
    private bool _missing;
    private int? _bindingStatus;

    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        MagicLinkCapabilityEndpointMetadata? endpoint = Context.GetEndpoint()?.Metadata
            .GetMetadata<MagicLinkCapabilityEndpointMetadata>();
        if (endpoint is null)
        {
            _faulted = true;
            return AuthenticateResult.Fail("Link request definition unavailable.");
        }
        // Bind once before credential resolution to preserve the existing malformed-body
        // boundary. The endpoint reuses the command and never parses or buffers the body again.
        if (HttpMethods.IsPost(Request.Method))
        {
            // Match the framework's required-body ordering: an absent body is 400,
            // even when Content-Type is absent or unsupported.
            if (Context.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody != true)
            {
                _bindingStatus = StatusCodes.Status400BadRequest;
                return AuthenticateResult.Fail("Link request body unavailable.");
            }
            if (!Request.HasJsonContentType())
            {
                _bindingStatus = StatusCodes.Status415UnsupportedMediaType;
                return AuthenticateResult.Fail("Link request body unavailable.");
            }
            try
            {
                if (endpoint.RequestType == typeof(ConfirmTimeThroughMagicLink))
                {
                    Context.Features.Set(await Request.ReadFromJsonAsync<ConfirmTimeThroughMagicLink>(
                        Context.RequestAborted).ConfigureAwait(false) ?? throw new JsonException());
                }
                else if (endpoint.RequestType == typeof(AdjustTimeThroughMagicLink))
                {
                    Context.Features.Set(await Request.ReadFromJsonAsync<AdjustTimeThroughMagicLink>(
                        Context.RequestAborted).ConfigureAwait(false) ?? throw new JsonException());
                }
                else
                {
                    _faulted = true;
                    return AuthenticateResult.Fail("Link request definition unavailable.");
                }
            }
            catch (Exception exception) when (!Context.RequestAborted.IsCancellationRequested
                && exception is (JsonException or IOException or BadHttpRequestException))
            {
                _bindingStatus = exception is BadHttpRequestException badRequest
                    ? badRequest.StatusCode : StatusCodes.Status400BadRequest;
                try
                {
                    LogBindingRefusal(Logger, new BadHttpRequestException("Invalid link request body.", _bindingStatus.Value));
                }
                catch (Exception)
                {
                    // Diagnostics must not change the selected request-binding response.
                }
                return AuthenticateResult.Fail("Link request body unavailable.");
            }
        }
        string? token = Request.Query["t"];
        _missing = string.IsNullOrWhiteSpace(token);
        if (_missing)
        {
            return AuthenticateResult.NoResult();
        }

        try
        {
            // The canonical loader verifies the token hash against the resolved capability,
            // tenant and owner. Share this request's result, never another request's authority.
            MagicLinkEndpointTokenState state = await stateLoader.LoadTokenStateAsync(
                token!, Context.RequestAborted).ConfigureAwait(false);
            if (state.CapabilityState is not { Exists: true, TokenHash: not null })
            {
                return AuthenticateResult.Fail("Capability credential unavailable.");
            }

            Context.Features.Set(state);
            var principal = new ClaimsPrincipal(new ClaimsIdentity(Scheme.Name));
            return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
        }
        catch (Exception exception) when (exception is not OperationCanceledException
            || !Context.RequestAborted.IsCancellationRequested)
        {
            // Backend faults retain their fixed 500 response; they are never credential denials.
            _faulted = true;
            return AuthenticateResult.Fail("Capability resolution unavailable.");
        }
    }

    /// <inheritdoc />
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (_bindingStatus is { } status)
        {
            Response.StatusCode = status;
            return Task.CompletedTask;
        }
        return MagicLinkConfirmationCapabilityEndpoints.RejectCapabilityCredentialAsync(Context, _faulted, _missing);
    }

    /// <inheritdoc />
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        => HandleChallengeAsync(properties);

    [LoggerMessage(EventId = 37003, Level = LogLevel.Warning, Message = "External link request body is invalid.")]
    private static partial void LogBindingRefusal(ILogger logger, Exception exception);
}
