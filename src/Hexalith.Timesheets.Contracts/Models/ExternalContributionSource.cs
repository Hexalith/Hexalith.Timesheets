namespace Hexalith.Timesheets.Contracts.Models;

public sealed record ExternalContributionSource
{
    public const int MaxSourceSystemLength = 80;

    public const int MaxExternalRequestIdLength = 120;

    public ExternalContributionSource(string sourceSystem, string externalRequestId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalRequestId);

        if (sourceSystem.Length > MaxSourceSystemLength)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceSystem), sourceSystem, "External source system is too long.");
        }

        if (externalRequestId.Length > MaxExternalRequestIdLength)
        {
            throw new ArgumentOutOfRangeException(nameof(externalRequestId), externalRequestId, "External request ID is too long.");
        }

        SourceSystem = sourceSystem;
        ExternalRequestId = externalRequestId;
    }

    public string SourceSystem { get; }

    public string ExternalRequestId { get; }
}
