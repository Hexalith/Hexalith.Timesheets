namespace Hexalith.Timesheets.Runtime;

/// <summary>
/// Controls where the EventStore domain-service surface may be reached.
/// </summary>
/// <remarks>
/// <para>
/// <c>UseEventStoreDomainService()</c> maps the canonical SDK routes — <c>/process</c>,
/// <c>/replay-state</c>, <c>/query</c>, <c>/project</c>, <c>/project/v2</c>, the
/// <c>/project/rebuild/*</c> family and <c>/admin/operational-index-metadata</c> — with no
/// authorization of their own. This host also publishes the deliberately anonymous magic-link
/// confirm and adjust routes, so without a split an anonymous caller reaching the public ingress
/// could write the cross-tenant token-hash index, the trust-bearing Activity Type catalog, and
/// dispatch domain commands.
/// </para>
/// <para>
/// The split is fail-closed: with nothing configured the SDK surface is unreachable rather than
/// public, so a deployment that forgets to set it loses projection delivery instead of exposing a
/// write surface.
/// </para>
/// </remarks>
public sealed class InternalSurfaceOptions
{
    /// <summary>The configuration section binding these options.</summary>
    public const string SectionName = "Timesheets:InternalSurface";

    /// <summary>
    /// The local port the domain-service surface is served on. Requests for those paths arriving on
    /// any other port are refused. <see langword="null"/> refuses them on every port.
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Serves the domain-service surface on every port. Intended for in-process boundary tests,
    /// where there is no real listener and <c>Connection.LocalPort</c> is always <c>0</c>. Setting
    /// this in a deployed host republishes the unauthenticated write surface on the public ingress.
    /// </summary>
    public bool AllowOnAnyPort { get; set; }
}
