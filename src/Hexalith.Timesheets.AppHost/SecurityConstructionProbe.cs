using System.Net;

using Aspire.Hosting.ApplicationModel;

using Hexalith.EventStore.Aspire;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Hexalith.Timesheets.AppHost;

/// <summary>Checks composed credential references without resolving secret values.</summary>
internal static class SecurityConstructionProbe
{
    private const string ReadinessKey = "eventstore_http_/ready_200_check";

    internal static ReadinessHandler ConfigureReadiness(IDistributedApplicationBuilder builder)
    {
        ReadinessHandler handler = new();
        builder.Services.AddHttpClient(ReadinessKey).ConfigurePrimaryHttpMessageHandler(() => handler);
        return handler;
    }

    internal static async Task VerifyReadinessAsync(
        IDistributedApplicationBuilder builder, IServiceProvider services, ReadinessHandler handler)
    {
        IResource gateway = builder.Resources.Single(resource => resource.Name == "eventstore");
        HealthCheckAnnotation[] annotations = gateway.Annotations.OfType<HealthCheckAnnotation>().ToArray();
        EndpointAnnotation endpoint = gateway.Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "http");
        bool fixture = builder.Configuration["TIMESHEETS_LIVE_DAPR_FIXTURE"] == "1";
        if (endpoint.Port != 8082 || endpoint.UriScheme != "http"
            || (fixture ? annotations.Length != 1 || annotations[0].Key != ReadinessKey : annotations.Length != 0))
        {
            throw new InvalidOperationException("Gateway readiness must use its named HTTP endpoint.");
        }

        IResource module = builder.Resources.Single(resource => resource.Name == "timesheets");
        WaitAnnotation[] waits = module.Annotations.OfType<WaitAnnotation>()
            .Where(wait => ReferenceEquals(wait.Resource, gateway)).ToArray();
        WaitType expected = fixture ? WaitType.WaitUntilStarted : WaitType.WaitUntilHealthy;
        bool referenced = module.Annotations.OfType<EndpointReferenceAnnotation>()
            .Any(reference => ReferenceEquals(reference.Resource, gateway) && reference.UseAllEndpoints);
        if (waits.Length != 1 || waits[0].WaitType != expected || !referenced)
        {
            throw new InvalidOperationException("Gateway startup ordering must retain its service reference.");
        }

        if (fixture)
        {
            // Allocate only the model endpoint. The handler performs no network I/O;
            // no application, listener or orchestration resource is started.
            endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "127.0.0.1", 8082);
            HealthCheckRegistration registration = services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value
                .Registrations.Single(registration => registration.Name == annotations[0].Key);
            IHealthCheck check = registration.Factory(services);
            HealthCheckContext context = new() { Registration = registration };
            handler.Status = HttpStatusCode.ServiceUnavailable;
            HealthCheckResult unavailable = await check.CheckHealthAsync(context);
            handler.Status = HttpStatusCode.OK;
            HealthCheckResult ready = await check.CheckHealthAsync(context);
            if (unavailable.Status != HealthStatus.Unhealthy || ready.Status != HealthStatus.Healthy
                || handler.Requests.Count != 2 || handler.Requests.Any(uri => uri != "http://127.0.0.1:8082/ready"))
            {
                throw new InvalidOperationException("Gateway readiness must reject unavailable HTTP responses.");
            }
        }

        Console.WriteLine("Timesheets gateway readiness model: " + System.Text.Json.JsonSerializer.Serialize(new
        {
            endpoint = "http", path = fixture ? "/ready" : null, port = 8082, healthCheckAttached = fixture,
            unavailableRejected = fixture, readyAccepted = fixture,
            moduleWait = expected.ToString(), gatewayReferenced = true,
        }));
    }

    internal sealed class ReadinessHandler : HttpMessageHandler
    {
        internal HttpStatusCode Status { get; set; }
        internal List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(Status));
        }
    }

    /// <summary>Verifies each actual application and security environment binding.</summary>
    /// <param name="builder">The fully constructed application builder.</param>
    /// <param name="security">The optional platform security resources.</param>
    internal static async Task VerifyAsync(IDistributedApplicationBuilder builder, HexalithEventStoreSecurityResources? security)
    {
        List<object> bindings = [];
        foreach (string name in new[] { "eventstore", "timesheets" })
        {
            IResource resource = builder.Resources.Single(resource => resource.Name == name);
            await VerifyBindingAsync(resource, "APP_API_TOKEN", name + "-app-api-token", generated: false);
        }

        if (security is not null)
        {
            await VerifyBindingAsync(security.Keycloak.Resource,
                HexalithEventStoreSecurityOptions.DefaultClientUsernameEnvironmentName,
                "security-eventstore-client-username", generated: true);
            await VerifyBindingAsync(security.Keycloak.Resource,
                HexalithEventStoreSecurityOptions.DefaultClientPasswordEnvironmentName,
                "security-eventstore-client-password", generated: true);
        }

        Console.WriteLine("Timesheets security references: " + System.Text.Json.JsonSerializer.Serialize(bindings));

        async Task VerifyBindingAsync(IResource resource, string variable, string parameterName, bool generated)
        {
            // Evaluate callbacks into separate dictionaries to reject duplicate declarations,
            // including declarations that overwrite the same parameter with itself.
            List<object> values = [];
            foreach (EnvironmentCallbackAnnotation annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            {
                EnvironmentCallbackContext context = new(builder.ExecutionContext, resource);
                await annotation.Callback(context);
                if (context.EnvironmentVariables.TryGetValue(variable, out object? value))
                {
                    values.Add(value);
                }
            }

            ParameterResource[] parameters = builder.Resources.OfType<ParameterResource>()
                .Where(parameter => parameter.Name == parameterName).ToArray();
            if (parameters.Length != 1 || !parameters[0].Secret || values.Count != 1
                || !ReferencesParameter(values[0], parameters[0])
                || (generated && parameters[0].Default is not GenerateParameterDefault))
            {
                throw new InvalidOperationException("Security construction requires unique generated secret references.");
            }

            bindings.Add(new { resource = resource.Name, environment = variable, parameter = parameterName,
                secret = true, generated });
        }
    }

    private static bool ReferencesParameter(object value, ParameterResource parameter)
        => ReferenceEquals(value, parameter)
            || value is ReferenceExpression expression && expression.ValueProviders.Count == 1
                && ReferenceEquals(expression.ValueProviders[0], parameter) && expression.Format == "{0}";
}
