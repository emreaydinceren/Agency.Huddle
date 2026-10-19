using Microsoft.Extensions.Logging;

namespace Agency.Huddle.Tests.Elicitation;

/// <summary>An <see cref="ILogger"/> that keeps every entry's level, formatted message, exception and category, for tests that assert what was logged.</summary>
internal sealed class RecordingLogger : ILogger
{
    private readonly LogEntryStore store;
    private readonly string? category;
    private readonly LogLevel minLevel;
    private readonly Exception? throwOnLog;

    /// <summary>Creates a logger with its own entry list and no category.</summary>
    /// <param name="minLevel">The lowest level recorded; lower levels are not enabled.</param>
    /// <param name="throwOnLog">An exception every enabled log call throws instead of recording, or <see langword="null"/>.</param>
    public RecordingLogger(LogLevel minLevel = LogLevel.Trace, Exception? throwOnLog = null)
        : this(new LogEntryStore(), null, minLevel, throwOnLog)
    {
    }

    /// <summary>Creates a logger that records into a shared list under a category.</summary>
    /// <param name="store">The list to record into.</param>
    /// <param name="category">The category stamped on every entry, or <see langword="null"/>.</param>
    /// <param name="minLevel">The lowest level recorded.</param>
    /// <param name="throwOnLog">An exception every enabled log call throws instead of recording, or <see langword="null"/>.</param>
    internal RecordingLogger(LogEntryStore store, string? category, LogLevel minLevel, Exception? throwOnLog)
    {
        this.store = store;
        this.category = category;
        this.minLevel = minLevel;
        this.throwOnLog = throwOnLog;
    }

    /// <summary>A copy of every entry logged so far, taken under the lock.</summary>
    public IReadOnlyList<LogEntry> Entries => this.store.Entries;

    /// <summary>The formatted message of every entry logged so far.</summary>
    public IReadOnlyList<string> Messages => this.store.Messages;

    /// <summary>The level of every entry logged so far.</summary>
    public IReadOnlyList<LogLevel> Levels => this.store.Levels;

    /// <summary>The entries logged at exactly <paramref name="level"/>; a higher level does not match.</summary>
    /// <param name="level">The level to match.</param>
    /// <returns>The matching entries, in the order they were logged.</returns>
    public IReadOnlyList<LogEntry> AtExactly(LogLevel level) => this.store.AtExactly(level);

    /// <summary>How many entries were logged at <paramref name="level"/> or above.</summary>
    /// <param name="level">The lowest level to count.</param>
    /// <returns>The number of entries at that level or higher.</returns>
    public int CountAtLeast(LogLevel level) => this.store.CountAtLeast(level);

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => (logLevel != LogLevel.None) && (logLevel >= this.minLevel);

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        if (!this.IsEnabled(logLevel))
        {
            return;
        }

        if (this.throwOnLog is not null)
        {
            throw this.throwOnLog;
        }

        this.store.Add(new LogEntry(logLevel, formatter(state, exception), exception, this.category));
    }
}

/// <summary>An <see cref="ILogger{TCategoryName}"/> that keeps every entry's level, formatted message, exception and category, for tests that assert what was logged.</summary>
/// <typeparam name="T">The category the logger is for.</typeparam>
/// <param name="minLevel">The lowest level recorded; lower levels are not enabled.</param>
/// <param name="throwOnLog">An exception every enabled log call throws instead of recording, or <see langword="null"/>.</param>
internal sealed class RecordingLogger<T>(LogLevel minLevel = LogLevel.Trace, Exception? throwOnLog = null) : ILogger<T>
{
    private readonly RecordingLogger inner = new(new LogEntryStore(), typeof(T).FullName, minLevel, throwOnLog);

    /// <summary>A copy of every entry logged so far, taken under the lock.</summary>
    public IReadOnlyList<LogEntry> Entries => this.inner.Entries;

    /// <summary>The formatted message of every entry logged so far.</summary>
    public IReadOnlyList<string> Messages => this.inner.Messages;

    /// <summary>The level of every entry logged so far.</summary>
    public IReadOnlyList<LogLevel> Levels => this.inner.Levels;

    /// <summary>The entries logged at exactly <paramref name="level"/>; a higher level does not match.</summary>
    /// <param name="level">The level to match.</param>
    /// <returns>The matching entries, in the order they were logged.</returns>
    public IReadOnlyList<LogEntry> AtExactly(LogLevel level) => this.inner.AtExactly(level);

    /// <summary>How many entries were logged at <paramref name="level"/> or above.</summary>
    /// <param name="level">The lowest level to count.</param>
    /// <returns>The number of entries at that level or higher.</returns>
    public int CountAtLeast(LogLevel level) => this.inner.CountAtLeast(level);

    /// <summary>A copy of every entry's level and message, kept for the callers that predate <see cref="Entries"/>.</summary>
    /// <returns>The entries, in the order they were logged.</returns>
    public IReadOnlyList<(LogLevel Level, string Message)> Snapshot() => [.. this.inner.Entries.Select(static entry => (entry.Level, entry.Message))];

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => this.inner.BeginScope(state);

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => this.inner.IsEnabled(logLevel);

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        this.inner.Log(logLevel, eventId, state, exception, formatter);
}
