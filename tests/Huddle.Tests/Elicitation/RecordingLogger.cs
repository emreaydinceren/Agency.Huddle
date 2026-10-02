using Microsoft.Extensions.Logging;

namespace Agency.Huddle.Tests.Elicitation;

/// <summary>An <see cref="ILogger{TCategoryName}"/> that keeps every entry's level and formatted message, for tests that assert what was logged.</summary>
/// <typeparam name="T">The category the logger is for.</typeparam>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly Lock gate = new();
    private readonly List<(LogLevel Level, string Message)> entries = [];

    /// <summary>A copy of every entry logged so far.</summary>
    /// <returns>The entries, in the order they were logged.</returns>
    public IReadOnlyList<(LogLevel Level, string Message)> Snapshot()
    {
        lock (this.gate)
        {
            return [.. this.entries];
        }
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        lock (this.gate)
        {
            this.entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
