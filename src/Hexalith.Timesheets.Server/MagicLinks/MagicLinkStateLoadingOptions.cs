namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>Bounds the invocation deadline and total authoritative history read costs.</summary>
public sealed class MagicLinkStateLoadingOptions
{
    /// <summary>Gets or sets the total stream page allowance, including repeated stream reads.</summary>
    public int MaxPages { get; set; } = 64;

    /// <summary>Gets or sets the total raw event allowance, before normalization.</summary>
    public int MaxEvents { get; set; } = 10_000;

    /// <summary>Gets or sets the total raw payload byte allowance.</summary>
    public long MaxPayloadBytes { get; set; } = 16 * 1024 * 1024;

    /// <summary>Gets or sets the total elapsed allowance shared by candidate, history and catalog reads.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Checks that all allowances are positive and within supported hard limits.</summary>
    public bool IsValid()
        => MaxPages is > 0 and <= 256
            && MaxEvents is > 0 and <= 100_000
            && MaxPayloadBytes is > 0 and <= 128 * 1024 * 1024
            && Timeout > TimeSpan.Zero && Timeout <= TimeSpan.FromSeconds(30);
}
