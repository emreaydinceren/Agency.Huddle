using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Tests for the LR-R5 Warning that <see cref="AdapterCatalog"/> logs for an Adapter that runs Claude
/// with user-settings isolation off (Spec §3.2, correction C11). The Warning text is Settled text T17.
/// </summary>
public sealed class AdapterIsolationWarningTests
{
    /// <summary>The Settled text T17, with the Adapter id as the only placeholder, parsed once.</summary>
    private static readonly CompositeFormat WarningTemplate = CompositeFormat.Parse(
        "Adapter '{0}' prefixes tool names but does not isolate user settings, so the Human's Claude settings and memory may load into its sessions.");

    /// <summary>A configured entry that runs Claude, with isolation off, logs exactly one Warning naming it.</summary>
    [Fact]
    public void Constructor_RunsClaudeNotIsolated_LogsOneWarningNamingTheEntry()
    {
        RecordingLogger recorder = new();
        AdapterProfileOptions entry = new()
        {
            Id = "claude-live",
            Command = "node",
            AdapterPath = @"tools\claude-agent-acp\index.js",
            IsolateUserSettings = false,
        };

        _ = new AdapterCatalog(OptionsWith(entry), logger: recorder);

        string warning = Assert.Single(recorder.Warnings);
        Assert.Equal(string.Format(CultureInfo.InvariantCulture, WarningTemplate, "claude-live"), warning);
    }

    /// <summary>A configured entry that runs Claude with isolation on logs no Warning.</summary>
    [Fact]
    public void Constructor_RunsClaudeIsolated_LogsNoWarning()
    {
        RecordingLogger recorder = new();
        AdapterProfileOptions entry = new()
        {
            Id = "claude-live",
            Command = "node",
            AdapterPath = @"tools\claude-agent-acp\index.js",
            IsolateUserSettings = true,
        };

        _ = new AdapterCatalog(OptionsWith(entry), logger: recorder);

        Assert.Empty(recorder.Warnings);
    }

    /// <summary>A mock-acp entry keeps the default prefix but runs no Claude, so it logs no Warning.</summary>
    [Fact]
    public void Constructor_MockAcpEntryNotIsolated_LogsNoWarning()
    {
        RecordingLogger recorder = new();
        AdapterProfileOptions entry = new()
        {
            Id = "mock",
            Command = "dotnet",
            AdapterPath = "mock-acp",
            IsolateUserSettings = false,
        };

        _ = new AdapterCatalog(OptionsWith(entry), logger: recorder);

        Assert.Empty(recorder.Warnings);
    }

    /// <summary>An agency-acp entry keeps the default prefix but runs no Claude, so it logs no Warning.</summary>
    [Fact]
    public void Constructor_AgencyAcpEntryNotIsolated_LogsNoWarning()
    {
        RecordingLogger recorder = new();
        AdapterProfileOptions entry = new()
        {
            Id = "agency",
            Command = "agency-acp",
            IsolateUserSettings = false,
        };

        _ = new AdapterCatalog(OptionsWith(entry), logger: recorder);

        Assert.Empty(recorder.Warnings);
    }

    /// <summary>Two offending Claude entries log two Warnings, one per id, in configuration order.</summary>
    [Fact]
    public void Constructor_TwoRunsClaudeNotIsolated_LogsOneWarningPerEntryInOrder()
    {
        RecordingLogger recorder = new();
        AdapterProfileOptions first = new()
        {
            Id = "claude-a",
            Command = "node",
            AdapterPath = @"tools\claude-agent-acp\a.js",
            IsolateUserSettings = false,
        };
        AdapterProfileOptions second = new()
        {
            Id = "claude-b",
            Command = "node",
            Args = ["claude-agent-acp"],
            IsolateUserSettings = false,
        };

        _ = new AdapterCatalog(OptionsWith(first, second), logger: recorder);

        string[] expected =
        [
            string.Format(CultureInfo.InvariantCulture, WarningTemplate, "claude-a"),
            string.Format(CultureInfo.InvariantCulture, WarningTemplate, "claude-b"),
        ];
        Assert.Equal(expected, recorder.Warnings);
    }

    /// <summary>The synthesised built-in Claude profile, with no configured Adapters, logs no Warning.</summary>
    [Fact]
    public void Constructor_NoConfiguredAdapters_LogsNoWarning()
    {
        RecordingLogger recorder = new();

        _ = new AdapterCatalog(TestOptions.Create(), logger: recorder);

        Assert.Empty(recorder.Warnings);
    }

    private static IOptions<TeamOptions> OptionsWith(params AdapterProfileOptions[] entries) =>
        TestOptions.Create(opts => opts.Acp = new AcpOptions { Adapters = [.. entries] });

    /// <summary>
    /// Records every entry the catalog logs. Each message is rendered with the formatter the logger
    /// receives, so the assertions see exactly what a log sink would print.
    /// </summary>
    private sealed class RecordingLogger : ILogger<AdapterCatalog>
    {
        private readonly List<RecordedEntry> entries = [];

        /// <summary>The rendered message of every Warning-level entry, in the order logged.</summary>
        public IReadOnlyList<string> Warnings =>
            [.. this.entries.Where(entry => entry.Level == LogLevel.Warning).Select(entry => entry.Message)];

        /// <summary>Records one entry, rendering its message with the supplied formatter.</summary>
        /// <typeparam name="TState">The log state type.</typeparam>
        /// <param name="logLevel">The level the entry was logged at.</param>
        /// <param name="eventId">The event id, unused by this recorder.</param>
        /// <param name="state">The state to render.</param>
        /// <param name="exception">The exception, unused by this recorder.</param>
        /// <param name="formatter">Renders the state into the message text.</param>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            this.entries.Add(new RecordedEntry(logLevel, formatter(state, exception)));

        /// <summary>Reports every level as enabled, so the catalog always logs.</summary>
        /// <param name="logLevel">The level being checked.</param>
        /// <returns>Always <see langword="true"/>.</returns>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <summary>Scopes are not used by the catalog, so none is opened.</summary>
        /// <typeparam name="TState">The scope state type.</typeparam>
        /// <param name="state">The scope state, unused.</param>
        /// <returns>Always <see langword="null"/>.</returns>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <summary>One logged entry: its level and its rendered message.</summary>
        /// <param name="Level">The level the entry was logged at.</param>
        /// <param name="Message">The rendered message text.</param>
        private sealed record RecordedEntry(LogLevel Level, string Message);
    }
}
