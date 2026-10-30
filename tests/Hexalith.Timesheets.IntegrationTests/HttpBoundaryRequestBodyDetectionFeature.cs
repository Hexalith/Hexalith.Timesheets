using Microsoft.AspNetCore.Http.Features;

namespace Hexalith.Timesheets.IntegrationTests;

/// <summary>Marks the explicit JSON body supplied through TestServer's direct context API.</summary>
internal sealed class HttpBoundaryRequestBodyDetectionFeature : IHttpRequestBodyDetectionFeature
{
    /// <inheritdoc/>
    public bool CanHaveBody => true;
}
