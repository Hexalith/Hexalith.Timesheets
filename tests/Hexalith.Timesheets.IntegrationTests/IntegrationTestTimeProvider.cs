namespace Hexalith.Timesheets.IntegrationTests;

/// <summary>Supplies a controlled UTC clock and real timers for composed-host tests.</summary>
internal sealed class IntegrationTestTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
