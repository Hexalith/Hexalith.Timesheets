namespace Hexalith.Timesheets.Contracts.Models.MagicLinks;

public sealed record MagicLinkAuditMetadata
{
    public const int MaxSourceSystemLength = 80;

    public const int MaxSourceReferenceLength = 120;

    public MagicLinkAuditMetadata(string sourceSystem, string sourceReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceReference);

        if (sourceSystem.Length > MaxSourceSystemLength)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceSystem), sourceSystem, "Magic-link audit source system is too long.");
        }

        if (sourceReference.Length > MaxSourceReferenceLength)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceReference), sourceReference, "Magic-link audit source reference is too long.");
        }

        SourceSystem = sourceSystem;
        SourceReference = sourceReference;
    }

    public string SourceSystem { get; }

    public string SourceReference { get; }
}
