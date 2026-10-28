namespace Hexalith.Timesheets.IntegrationTests;

/// <summary>Attributes one fixture store read without dropping background observations.</summary>
/// <param name="StoreName">The complete read-model store address.</param>
/// <param name="Key">The synthetic read-model key requested.</param>
/// <param name="RequestId">The host HTTP trace identifier, or null outside an active request.</param>
internal sealed record HttpBoundaryReadObservation(string StoreName, string Key, string? RequestId);
