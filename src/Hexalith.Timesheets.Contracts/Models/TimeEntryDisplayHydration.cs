namespace Hexalith.Timesheets.Contracts.Models;

public sealed record TimeEntryDisplayHydration(
    TimeEntryHydratedDisplayLabel Contributor,
    TimeEntryHydratedDisplayLabel Target,
    TimeEntryHydratedDisplayLabel ActivityType)
{
    public static TimeEntryDisplayHydration Unknown { get; } = new(
        TimeEntryHydratedDisplayLabel.Unknown,
        TimeEntryHydratedDisplayLabel.Unknown,
        TimeEntryHydratedDisplayLabel.Unknown);

    public static TimeEntryDisplayHydration Unavailable(string? detail = "Display hydration is unavailable.")
        => new(
            TimeEntryHydratedDisplayLabel.Unavailable(detail),
            TimeEntryHydratedDisplayLabel.Unavailable(detail),
            TimeEntryHydratedDisplayLabel.Unavailable(detail));
}
