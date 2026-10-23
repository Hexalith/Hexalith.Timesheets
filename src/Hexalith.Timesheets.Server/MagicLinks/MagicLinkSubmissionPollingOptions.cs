namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>Bounds how long a magic-link submission waits for a terminal EventStore status.</summary>
public sealed class MagicLinkSubmissionPollingOptions
{
    /// <summary>Gets or sets the maximum status polling duration.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets the delay between status reads.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Gets or sets the maximum duration of each stored-event read, also bounded by the submission deadline.</summary>
    public TimeSpan ReadbackTimeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Checks that polling and stored-event reads remain finite without flooding the gateway.</summary>
    public bool IsValid()
        => Timeout > TimeSpan.Zero
            && Timeout <= TimeSpan.FromSeconds(30)
            && ReadbackTimeout > TimeSpan.Zero
            && ReadbackTimeout <= TimeSpan.FromSeconds(30)
            && PollInterval >= TimeSpan.FromMilliseconds(10)
            && PollInterval <= Timeout;
}
