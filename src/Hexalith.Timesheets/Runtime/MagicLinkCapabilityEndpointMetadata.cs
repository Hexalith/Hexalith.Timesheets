namespace Hexalith.Timesheets.Runtime;

/// <summary>Identifies the canonical link operation and its optional submission body.</summary>
/// <param name="Operation">The fixed diagnostic operation label.</param>
/// <param name="RequestType">The command read once by capability authentication, or no body for displays.</param>
internal sealed record MagicLinkCapabilityEndpointMetadata(string Operation, Type? RequestType = null);
