using Hexalith.Timesheets.Projections.ActivityTypes;

using Microsoft.Extensions.Logging;

namespace Hexalith.Timesheets.Projections.Tests;

/// <summary>Captures complete diagnostic records through both projection logger seams.</summary>
internal sealed class DeliveryOutcomeLogger : ILogger<TenantActivityTypeCatalogProjectionHandler>, ILoggerFactory
{
    /// <summary>Gets messages, structured state, and exception references exactly as emitted.</summary>
    internal List<(LogLevel Level, EventId Id, string Message, string State, Exception? Exception)> Records { get; } = [];

    /// <summary>Gets or sets a provider failure at diagnostic emission.</summary>
    internal Exception? Failure { get; set; }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => this;

    /// <inheritdoc />
    public void AddProvider(ILoggerProvider provider) => throw new NotSupportedException();

    /// <inheritdoc />
    public void Dispose() { }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => Failure is null ? true : throw Failure;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
        => Records.Add((logLevel, eventId, formatter(state, exception),
            JsonState(state), exception));

    private static string JsonState<TState>(TState state)
        => System.Text.Json.JsonSerializer.Serialize(
            state as IEnumerable<KeyValuePair<string, object?>> ?? []);
}
