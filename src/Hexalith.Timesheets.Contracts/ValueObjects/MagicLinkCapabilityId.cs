namespace Hexalith.Timesheets.Contracts.ValueObjects;

public sealed record MagicLinkCapabilityId
{
    public const int MaxLength = 120;

    public MagicLinkCapabilityId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.Length > MaxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Magic-link capability ID is too long.");
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
