using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record TimeEntryHydratedDisplayLabel(
    DisplayHydrationState State,
    string? Label,
    DateTimeOffset? AsOfUtc,
    string? Detail)
{
    public static TimeEntryHydratedDisplayLabel Unknown { get; } = new(
        DisplayHydrationState.Unknown,
        null,
        null,
        null);

    public static TimeEntryHydratedDisplayLabel Fresh(string label, DateTimeOffset? asOfUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        return new(DisplayHydrationState.Fresh, label, asOfUtc, null);
    }

    public static TimeEntryHydratedDisplayLabel Stale(string label, DateTimeOffset? asOfUtc = null, string? detail = "Display label is stale.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        return new(DisplayHydrationState.Stale, label, asOfUtc, detail);
    }

    public static TimeEntryHydratedDisplayLabel Unavailable(string? detail = "Display label is unavailable.")
        => new(DisplayHydrationState.Unavailable, null, null, detail);

    public static TimeEntryHydratedDisplayLabel Denied(string? detail = "Display label is not available to this caller.")
        => new(DisplayHydrationState.Denied, null, null, detail);
}
