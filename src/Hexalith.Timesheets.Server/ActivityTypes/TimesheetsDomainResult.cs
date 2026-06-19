using Hexalith.Timesheets.Contracts.Events.Rejections;

namespace Hexalith.Timesheets.Server.ActivityTypes;

public sealed record TimesheetsDomainResult
{
    private TimesheetsDomainResult(IReadOnlyList<object> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        Events = events;
    }

    public IReadOnlyList<object> Events { get; }

    public bool IsSuccess => Events.Count > 0 && Events[0] is not TimesheetsRejection;

    public bool IsRejection => Events.Count > 0 && Events[0] is TimesheetsRejection;

    public bool IsNoOp => Events.Count == 0;

    public static TimesheetsDomainResult Success(IReadOnlyList<object> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (events.Count == 0)
        {
            throw new ArgumentException("Success result requires at least one event.", nameof(events));
        }

        return new(events);
    }

    public static TimesheetsDomainResult Rejection(IReadOnlyList<TimesheetsRejection> rejections)
    {
        ArgumentNullException.ThrowIfNull(rejections);

        if (rejections.Count == 0)
        {
            throw new ArgumentException("Rejection result requires at least one rejection.", nameof(rejections));
        }

        return new([.. rejections]);
    }

    public static TimesheetsDomainResult NoOp() => new([]);
}
