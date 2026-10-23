using System.Globalization;

namespace Hexalith.Timesheets.Server.Runtime;

/// <summary>Reloadable Timesheets gateway routing and sidecar credentials.</summary>
public sealed class TimesheetsEventStoreGatewayOptions
{
    /// <summary>Gets or sets the sidecar HTTP origin. Invalid origins use the port fallback.</summary>
    public string? Endpoint { get; set; }

    /// <summary>Gets or sets the fallback sidecar HTTP port.</summary>
    public string? HttpPort { get; set; }

    /// <summary>Gets or sets the target Dapr application id.</summary>
    public string AppId { get; set; } = "eventstore";

    /// <summary>Gets or sets the optional sidecar API credential.</summary>
    public string? ApiToken { get; set; }

    /// <summary>Gets the validated HTTP origin, falling back to localhost port 3500.</summary>
    public Uri BaseAddress
    {
        get
        {
            if (Uri.TryCreate(Endpoint, UriKind.Absolute, out Uri? configured)
                && (configured.Scheme == Uri.UriSchemeHttp || configured.Scheme == Uri.UriSchemeHttps)
                && string.IsNullOrEmpty(configured.UserInfo) && configured.AbsolutePath == "/"
                && string.IsNullOrEmpty(configured.Query) && string.IsNullOrEmpty(configured.Fragment))
                return new Uri(configured.GetLeftPart(UriPartial.Authority), UriKind.Absolute);
            return int.TryParse(HttpPort, NumberStyles.None, CultureInfo.InvariantCulture, out int port)
                && port is > 0 and <= 65535
                ? new UriBuilder(Uri.UriSchemeHttp, "localhost", port).Uri
                : new Uri("http://localhost:3500", UriKind.Absolute);
        }
    }
}
