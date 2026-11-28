using Hexalith.Timesheets.Server.TimeEntries;

using Microsoft.Extensions.Logging;

namespace Hexalith.Timesheets.Server.Tests;

internal sealed class RecordingOutcomeTestLogger : ILogger<TimeEntryRecordSubmissionService>
{
    public List<string> Messages { get; } = [];

    public List<(LogLevel Level, EventId EventId, KeyValuePair<string, object?>[] Fields, Exception? Exception)> Entries { get; } = [];

    public bool ThrowOnWrite { get; init; }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (ThrowOnWrite)
        {
            throw new InvalidOperationException("The diagnostic provider failed.");
        }

        Messages.Add(formatter(state, exception));
        KeyValuePair<string, object?>[] fields = state is IEnumerable<KeyValuePair<string, object?>> structured
            ? [.. structured]
            : [];
        Entries.Add((logLevel, eventId, fields, exception));
    }
}
