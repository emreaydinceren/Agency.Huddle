using Microsoft.Extensions.Logging;

namespace Agency.Huddle.Tests.Elicitation;

/// <summary>The thread-safe entry list that recording loggers and their provider share, with the read accessors tests use.</summary>
internal sealed class LogEntryStore
{
    private readonly Lock gate = new();
    private readonly List<LogEntry> entries = [];

    /// <summary>A copy of every entry recorded so far, taken under the lock.</summary>
    /// <returns>The entries, in the order they were logged.</returns>
    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.entries];
            }
        }
    }

    /// <summary>The formatted message of every entry recorded so far.</summary>
    public IReadOnlyList<string> Messages => [.. this.Entries.Select(static entry => entry.Message)];

    /// <summary>The level of every entry recorded so far.</summary>
    public IReadOnlyList<LogLevel> Levels => [.. this.Entries.Select(static entry => entry.Level)];

    /// <summary>Records one entry.</summary>
    /// <param name="entry">The entry to record.</param>
    public void Add(LogEntry entry)
    {
        lock (this.gate)
        {
            this.entries.Add(entry);
        }
    }

    /// <summary>The entries logged at exactly <paramref name="level"/>; a higher level does not match.</summary>
    /// <param name="level">The level to match.</param>
    /// <returns>The matching entries, in the order they were logged.</returns>
    public IReadOnlyList<LogEntry> AtExactly(LogLevel level) => [.. this.Entries.Where(entry => entry.Level == level)];

    /// <summary>How many entries were logged at <paramref name="level"/> or above.</summary>
    /// <param name="level">The lowest level to count.</param>
    /// <returns>The number of entries at that level or higher.</returns>
    public int CountAtLeast(LogLevel level) => this.Entries.Count(entry => entry.Level >= level);
}
