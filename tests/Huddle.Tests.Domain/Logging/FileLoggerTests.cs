using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Agency.Huddle.App.Logging;

namespace Agency.Huddle.Tests.Logging;

/// <summary>Tests for the per-run file log: <see cref="FileLoggerProvider"/> and its registration.</summary>
public sealed class FileLoggerTests
{
    /// <summary>
    /// A controllable <see cref="TimeProvider"/> pinned to UTC, so the file name this clock produces
    /// is the same on every machine rather than shifting with the runner's time zone.
    /// </summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset now;

        /// <summary>Creates a clock fixed at <paramref name="start"/>.</summary>
        /// <param name="start">The instant this clock reports until <see cref="Advance"/> is called.</param>
        public ManualTimeProvider(DateTimeOffset start)
        {
            this.now = start;
        }

        /// <inheritdoc/>
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow()
        {
            return this.now;
        }

        /// <summary>Moves this clock forward by <paramref name="delta"/>.</summary>
        /// <param name="delta">How far forward to move.</param>
        public void Advance(TimeSpan delta)
        {
            this.now += delta;
        }
    }

    private static readonly DateTimeOffset Start = new(2026, 9, 21, 14, 33, 18, TimeSpan.Zero);

    /// <summary>The run's file is named for the start time and carries a header naming the environment.</summary>
    [Fact]
    public void Constructor_NamesTheFileForTheRunAndWritesAHeader()
    {
        using TempDataDir dataDir = new();
        var logDirectory = Path.Combine(dataDir.Path, "logs");

        FileLoggerProvider provider = new(logDirectory, 20, "Development", new ManualTimeProvider(Start));
        var path = provider.Writer.FilePath;
        provider.Dispose();

        Assert.StartsWith("huddle-20260921-143318-", Path.GetFileName(path), StringComparison.Ordinal);
        Assert.EndsWith(".log", path, StringComparison.Ordinal);

        var text = File.ReadAllText(path);
        Assert.Contains("# Agency.Huddle run log", text, StringComparison.Ordinal);
        Assert.Contains("# started     2026-09-21 14:33:18.000", text, StringComparison.Ordinal);
        Assert.Contains("# environment Development", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// An entry records its timestamp, level and category, and takes its message from the supplied
    /// formatter rather than from the state - which is what makes a structured template render.
    /// </summary>
    [Fact]
    public void Log_WritesTimestampLevelCategoryAndTheFormattedMessage()
    {
        using TempDataDir dataDir = new();

        FileLoggerProvider provider = new(dataDir.Path, 20, "Development", new ManualTimeProvider(Start));
        var path = provider.Writer.FilePath;
        ILogger logger = provider.CreateLogger("Agency.Huddle.App.Services.ChatService");

        logger.Log(
            LogLevel.Warning,
            0,
            "01a0c5de",
            null,
            static (roomId, _) => "Room " + roomId + " refused a message.");
        provider.Dispose();

        var text = File.ReadAllText(path);
        Assert.Contains(
            "2026-09-21 14:33:18.000 WRN Agency.Huddle.App.Services.ChatService Room 01a0c5de refused a message.",
            text,
            StringComparison.Ordinal);
    }

    /// <summary>An exception is recorded in full, on indented continuation lines.</summary>
    [Fact]
    public void Log_WithAnException_RecordsTheExceptionText()
    {
        using TempDataDir dataDir = new();

        FileLoggerProvider provider = new(dataDir.Path, 20, "Development", new ManualTimeProvider(Start));
        var path = provider.Writer.FilePath;
        ILogger logger = provider.CreateLogger("Agency.Huddle.App.Acp.PersonaRunner");

        logger.Log(
            LogLevel.Error,
            0,
            "Persona failed.",
            new InvalidOperationException("session/prompt failed: boom"),
            static (message, _) => message);
        provider.Dispose();

        var text = File.ReadAllText(path);
        Assert.Contains("ERR Agency.Huddle.App.Acp.PersonaRunner Persona failed.", text, StringComparison.Ordinal);
        Assert.Contains("    System.InvalidOperationException: session/prompt failed: boom", text, StringComparison.Ordinal);
    }

    /// <summary>An ambient scope is recorded alongside the entry it surrounds.</summary>
    [Fact]
    public void Log_WithinAScope_RecordsTheScope()
    {
        using TempDataDir dataDir = new();

        FileLoggerProvider provider = new(dataDir.Path, 20, "Development", new ManualTimeProvider(Start));
        var path = provider.Writer.FilePath;
        ILogger logger = provider.CreateLogger("Agency.Huddle.App.Services.ChatService");

        using (logger.BeginScope("RequestPath:/_blazor"))
        {
            logger.Log(LogLevel.Information, 0, "A circuit opened.", null, static (message, _) => message);
        }

        provider.Dispose();

        var text = File.ReadAllText(path);
        Assert.Contains("    scope: RequestPath:/_blazor", text, StringComparison.Ordinal);
    }

    /// <summary>A zero event id is left out, and a real one is kept.</summary>
    [Fact]
    public void Log_WritesTheEventIdOnlyWhenItIsNonZero()
    {
        using TempDataDir dataDir = new();

        FileLoggerProvider provider = new(dataDir.Path, 20, "Development", new ManualTimeProvider(Start));
        var path = provider.Writer.FilePath;
        ILogger logger = provider.CreateLogger("Cat");

        logger.Log(LogLevel.Information, new EventId(0), "plain", null, static (state, _) => state);
        logger.Log(LogLevel.Information, new EventId(4242), "identified", null, static (state, _) => state);
        provider.Dispose();

        var text = File.ReadAllText(path);
        Assert.Contains("INF Cat plain", text, StringComparison.Ordinal);
        Assert.Contains("INF Cat[4242] identified", text, StringComparison.Ordinal);
    }

    /// <summary>Starting a run deletes the oldest files beyond the retained count.</summary>
    [Fact]
    public void Constructor_PrunesTheOldestFilesBeyondTheRetainedCount()
    {
        using TempDataDir dataDir = new();
        ManualTimeProvider clock = new(Start);

        for (var i = 0; i < 5; i++)
        {
            FileLoggerProvider provider = new(dataDir.Path, 3, "Development", clock);
            provider.Dispose();
            clock.Advance(TimeSpan.FromSeconds(1));
        }

        string[] remaining = Directory.GetFiles(dataDir.Path, "huddle-*.log");
        Array.Sort(remaining, StringComparer.Ordinal);

        Assert.Equal(3, remaining.Length);

        // The three kept are the newest three: 14:33:20, :21 and :22, the first two having been pruned.
        Assert.StartsWith("huddle-20260921-143320-", Path.GetFileName(remaining[0]), StringComparison.Ordinal);
        Assert.StartsWith("huddle-20260921-143322-", Path.GetFileName(remaining[2]), StringComparison.Ordinal);
    }

    /// <summary>With the feature off, registration adds no provider and opens no file.</summary>
    [Fact]
    public void AddTeamFileLogging_WhenDisabled_CreatesNoLogDirectory()
    {
        using TempDataDir dataDir = new();
        IConfiguration configuration = BuildConfiguration(dataDir.Path, enabled: false);

        using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddTeamFileLogging(configuration));
        factory.CreateLogger("Cat").Log(
            LogLevel.Information,
            0,
            "Nothing should reach a file.",
            null,
            static (message, _) => message);

        Assert.False(Directory.Exists(Path.Combine(dataDir.Path, "logs")));
    }

    /// <summary>A relative directory resolves under <c>Team:DataDir</c>, which is what puts logs in App_Data.</summary>
    [Fact]
    public void AddTeamFileLogging_WhenEnabled_ResolvesARelativeDirectoryUnderTheDataDir()
    {
        using TempDataDir dataDir = new();
        IConfiguration configuration = BuildConfiguration(dataDir.Path, enabled: true);

        using (ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddTeamFileLogging(configuration)))
        {
            factory.CreateLogger("Cat").Log(
                LogLevel.Information,
                0,
                "A line that proves the file was opened.",
                null,
                static (message, _) => message);
        }

        string[] files = Directory.GetFiles(Path.Combine(dataDir.Path, "logs"), "huddle-*.log");
        var file = Assert.Single(files);
        Assert.Contains("A line that proves the file was opened.", File.ReadAllText(file), StringComparison.Ordinal);
    }

    /// <summary>Builds configuration equivalent to appsettings for one temp data directory.</summary>
    /// <param name="dataDirPath">Value for <c>Team:DataDir</c>.</param>
    /// <param name="enabled">Value for <c>Team:FileLog:Enabled</c>.</param>
    /// <returns>Configuration carrying just the keys the registration reads.</returns>
    private static IConfiguration BuildConfiguration(string dataDirPath, bool enabled)
    {
        KeyValuePair<string, string?>[] values =
        [
            new("Team:DataDir", dataDirPath),
            new("Team:FileLog:Enabled", enabled ? "true" : "false"),
            new("Team:FileLog:DirectoryPath", "logs"),
            new("Team:FileLog:RetainedFileCount", "20"),
        ];

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}