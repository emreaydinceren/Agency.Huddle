using Microsoft.Extensions.Logging;
using Agency.Huddle.Tests.Elicitation;

namespace Agency.Huddle.Tests.Logging;

/// <summary>Tests for the shared test fakes <see cref="RecordingLogger"/>, <see cref="RecordingLogger{T}"/> and <see cref="RecordingLoggerProvider"/>.</summary>
public sealed class RecordingLoggerTests
{
    /// <summary>Every call records its level, formatted message and exception.</summary>
    [Fact]
    public void Log_RecordsLevelMessageAndException()
    {
        RecordingLogger logger = new();
        InvalidOperationException failure = new("boom");

        Write(logger, LogLevel.Error, "failed x", failure);

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("failed x", entry.Message);
        Assert.Same(failure, entry.Exception);
        Assert.Equal(["failed x"], logger.Messages);
        Assert.Equal([LogLevel.Error], logger.Levels);
    }

    /// <summary>The generic logger records the same way and tags the entry with its category type.</summary>
    [Fact]
    public void GenericLog_RecordsEntryWithCategory()
    {
        RecordingLogger<RecordingLoggerTests> logger = new();

        Write(logger, LogLevel.Information, "hello");

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal("hello", entry.Message);
        Assert.Equal(typeof(RecordingLoggerTests).FullName, entry.Category);
    }

    /// <summary>Entries below <c>minLevel</c> are not enabled and not recorded.</summary>
    [Fact]
    public void MinLevel_FiltersLowerLevels()
    {
        RecordingLogger logger = new(LogLevel.Warning);

        Write(logger, LogLevel.Information, "quiet");
        Write(logger, LogLevel.Warning, "loud");

        Assert.False(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Warning));
        Assert.Equal(["loud"], logger.Messages);
    }

    /// <summary><c>AtExactly(Warning)</c> returns only Warning entries and excludes Error.</summary>
    [Fact]
    public void AtExactly_Warning_ExcludesError()
    {
        RecordingLogger logger = new();
        Write(logger, LogLevel.Warning, "w");
        Write(logger, LogLevel.Error, "e");

        LogEntry entry = Assert.Single(logger.AtExactly(LogLevel.Warning));

        Assert.Equal("w", entry.Message);
    }

    /// <summary><c>CountAtLeast(Warning)</c> counts Warning and Error but not Information.</summary>
    [Fact]
    public void CountAtLeast_Warning_IncludesError()
    {
        RecordingLogger logger = new();
        Write(logger, LogLevel.Information, "i");
        Write(logger, LogLevel.Warning, "w");
        Write(logger, LogLevel.Error, "e");

        Assert.Equal(2, logger.CountAtLeast(LogLevel.Warning));
    }

    /// <summary>With <c>throwOnLog</c> every call throws the given exception and nothing is recorded.</summary>
    [Fact]
    public void ThrowOnLog_Throws_AndRecordsNothing()
    {
        ObjectDisposedException failure = new("logger");
        RecordingLogger logger = new(throwOnLog: failure);

        ObjectDisposedException thrown = Assert.Throws<ObjectDisposedException>(() => Write(logger, LogLevel.Warning, "x"));

        Assert.Same(failure, thrown);
        Assert.Empty(logger.Entries);
    }

    /// <summary>A snapshot taken earlier is a copy: later entries do not appear in it, and it does not alias the logger's list.</summary>
    [Fact]
    public void Entries_IsASnapshotCopy()
    {
        RecordingLogger logger = new();
        Write(logger, LogLevel.Information, "one");
        IReadOnlyList<LogEntry> snapshot = logger.Entries;

        Write(logger, LogLevel.Information, "two");

        Assert.Single(snapshot);
        Assert.Equal(2, logger.Entries.Count);
        if (snapshot is IList<LogEntry> { IsReadOnly: false } mutable)
        {
            mutable.Clear();
        }

        Assert.Equal(2, logger.Entries.Count);
    }

    /// <summary>Eight writers logging a thousand entries each leave all eight thousand recorded.</summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task Log_ParallelWriters_RecordsEveryEntry()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingLogger logger = new();

        Task[] writers = Enumerable.Range(0, 8)
            .Select(writer => Task.Run(
                () =>
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        Write(logger, LogLevel.Information, $"w{writer}-{i}");
                    }
                },
                ct))
            .ToArray();
        await Task.WhenAll(writers);

        Assert.Equal(8000, logger.Entries.Count);
        Assert.Equal(8000, logger.Messages.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>Loggers made by the provider share one list and carry their category name.</summary>
    [Fact]
    public void Provider_SetsCategory_AndSharesEntries()
    {
        using RecordingLoggerProvider provider = new();

        ILogger first = provider.CreateLogger("Cat.One");
        ILogger second = provider.CreateLogger("Cat.Two");
        Write(first, LogLevel.Information, "a");
        Write(second, LogLevel.Warning, "b");

        Assert.Equal(["Cat.One", "Cat.Two"], provider.Entries.Select(static entry => entry.Category));
        Assert.Equal(["a", "b"], provider.Messages);
    }

    /// <summary>Writes one entry through the raw <see cref="ILogger.Log{TState}"/> contract, so the tests do not depend on logger extension methods.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="level">The level of the entry.</param>
    /// <param name="message">The message of the entry.</param>
    /// <param name="exception">An optional exception to attach.</param>
    private static void Write(ILogger logger, LogLevel level, string message, Exception? exception = null)
    {
        logger.Log(level, default(EventId), message, exception, static (state, _) => state);
    }
}
