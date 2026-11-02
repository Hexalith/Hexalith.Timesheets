using Microsoft.Extensions.Logging;

namespace Hexalith.Timesheets.Server.Tests;

/// <summary>Copies pooled structured logging state while ILogger.Log still owns it.</summary>
internal sealed class EagerStructuredLogCapture(bool failOnIsEnabled = false) : ILogger
{
    /// <summary>Gets immutable snapshots of log calls made so far.</summary>
    public List<(LogLevel Level, EventId EventId, Exception? Exception,
        IReadOnlyDictionary<string, object?> State, string Text)> Entries { get; } = [];

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => failOnIsEnabled
        ? throw new IOException("Diagnostic provider unavailable.") : true;

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        if (state is not IEnumerable<KeyValuePair<string, object?>> structured)
        {
            throw new InvalidOperationException("Expected structured logging state.");
        }

        Dictionary<string, object?> fields = structured.ToDictionary(pair => pair.Key, pair => pair.Value,
            StringComparer.Ordinal);
        Entries.Add((logLevel, eventId, exception, fields, formatter(state, exception)));
    }
}
