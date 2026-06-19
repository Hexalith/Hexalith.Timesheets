namespace Hexalith.Timesheets.Contracts.ValueObjects;

public sealed record MagicLinkTokenHash
{
    public const int MaxLength = 128;

    public MagicLinkTokenHash(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.Length > MaxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Magic-link token hash is too long.");
        }

        Value = value;
    }

    public string Value { get; }
}
