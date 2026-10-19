using Microsoft.Extensions.Logging;

namespace Agency.Huddle.Tests.Elicitation;

/// <summary>An <see cref="ILoggerProvider"/> whose loggers all record into one shared list, each entry carrying its logger's category name.</summary>
/// <param name="minLevel">The lowest level its loggers record.</param>
/// <param name="throwOnLog">An exception every enabled log call throws instead of recording, or <see langword="null"/>.</param>
internal sealed class RecordingLoggerProvider(LogLevel minLevel = LogLevel.Trace, Exception? throwOnLog = null) : ILoggerProvider
{
    private readonly LogEntryStore store = new();

    /// <summary>A copy of every entry recorded by any logger this provider created.</summary>
    public IReadOnlyList<LogEntry> Entries => this.store.Entries;

    /// <summary>The formatted message of every recorded entry.</summary>
    public IReadOnlyList<string> Messages => this.store.Messages;

    /// <summary>The level of every recorded entry.</summary>
    public IReadOnlyList<LogLevel> Levels => this.store.Levels;

    /// <summary>The recorded entries logged at exactly <paramref name="level"/>.</summary>
    /// <param name="level">The level to match.</param>
    /// <returns>The matching entries, in the order they were logged.</returns>
    public IReadOnlyList<LogEntry> AtExactly(LogLevel level) => this.store.AtExactly(level);

    /// <summary>How many recorded entries are at <paramref name="level"/> or above.</summary>
    /// <param name="level">The lowest level to count.</param>
    /// <returns>The number of entries at that level or higher.</returns>
    public int CountAtLeast(LogLevel level) => this.store.CountAtLeast(level);

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new RecordingLogger(this.store, categoryName, minLevel, throwOnLog);

    /// <inheritdoc />
    public void Dispose()
    {
        // Nothing to release: the entries stay readable after disposal so a test can assert on them.
    }
}
