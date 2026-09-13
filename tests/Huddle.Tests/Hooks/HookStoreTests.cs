namespace Agency.Huddle.Tests.Hooks;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Hooks;
using Agency.Huddle.Contracts;

/// <summary>
/// Tests for <see cref="HookStore"/>: that it joins <see cref="HookCatalog"/>'s defaults with an
/// override file, tolerates every shape of a missing or malformed file without throwing, never
/// drops a user's unrelated data, and — the most important invariant here — rebuilds its resolved
/// snapshot before raising <see cref="HookStore.HooksChanged"/> rather than after.
/// </summary>
public sealed class HookStoreTests
{
    /// <summary>Absent file: every one of the catalog's keys resolves to its own default text.</summary>
    [Fact]
    public void Raw_NoOverrideFile_EveryKeyResolvesToCatalogDefault()
    {
        using var dataDir = new TempDataDir();
        var store = new HookStore(dataDir.Options(), NullLogger<HookStore>.Instance);

        foreach (var hook in HookCatalog.All)
        {
            Assert.Equal(hook.Default, store.Raw(hook.Key));
        }
    }

    /// <summary>A missing override file is the normal first-run case, so the constructor never creates one.</summary>
    [Fact]
    public void Constructor_NoOverrideFile_DoesNotCreateOne()
    {
        using var dataDir = new TempDataDir();
        _ = new HookStore(dataDir.Options(), NullLogger<HookStore>.Instance);

        Assert.False(File.Exists(Path.Combine(dataDir.Path, "hooks.json")));
    }

    /// <summary>A file overriding one key leaves every other key resolving to its catalog default.</summary>
    [Fact]
    public void Raw_FileWithOneOverride_ThatKeyWinsAndOthersDefault()
    {
        using var dataDir = new TempDataDir();
        const string overriddenText = "[Room: {{roomName}} #{{roomId}}]";
        WriteHooksJson(dataDir.Path, new Dictionary<string, string> { ["turn.roomLabel"] = overriddenText });

        var store = new HookStore(dataDir.Options(), NullLogger<HookStore>.Instance);

        Assert.Equal(overriddenText, store.Raw("turn.roomLabel"));

        foreach (var hook in HookCatalog.All.Where(h => h.Key != "turn.roomLabel"))
        {
            Assert.Equal(hook.Default, store.Raw(hook.Key));
        }
    }

    /// <summary>Malformed JSON falls back to every default, does not throw, and logs a warning naming the file's path.</summary>
    [Fact]
    public void Constructor_MalformedJson_FallsBackToDefaultsAndLogsWarning()
    {
        using var dataDir = new TempDataDir();
        var path = Path.Combine(dataDir.Path, "hooks.json");
        File.WriteAllText(path, "{ this is not valid json");
        var logger = new RecordingLogger<HookStore>();

        var store = new HookStore(dataDir.Options(), logger);

        foreach (var hook in HookCatalog.All)
        {
            Assert.Equal(hook.Default, store.Raw(hook.Key));
        }

        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains(path, warning.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A key in the file that names no hook in the catalog is ignored for resolution, but is kept
    /// in the file — a save of some other key must not delete a user's unrelated data.
    /// </summary>
    [Fact]
    public void Save_FileHasUnknownKey_UnknownKeySurvivesAndIsIgnoredForResolution()
    {
        using var dataDir = new TempDataDir();
        WriteHooksJson(dataDir.Path, new Dictionary<string, string> { ["not.a.real.hook"] = "keep-me" });
        var store = new HookStore(dataDir.Options(), NullLogger<HookStore>.Instance);

        store.Save("getHelp.budget", "a brand new override");

        var onDisk = ReadHooksJson(dataDir.Path);
        Assert.Equal("keep-me", onDisk["not.a.real.hook"]);
        Assert.Equal("a brand new override", onDisk["getHelp.budget"]);
    }

    /// <summary>An unknown key in the file is logged (debug or information), never as a warning.</summary>
    [Fact]
    public void Constructor_FileHasUnknownKey_LogsAtInformationOrDebugNeverWarning()
    {
        using var dataDir = new TempDataDir();
        WriteHooksJson(dataDir.Path, new Dictionary<string, string> { ["not.a.real.hook"] = "keep-me" });
        var logger = new RecordingLogger<HookStore>();

        _ = new HookStore(dataDir.Options(), logger);

        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains(logger.Entries, e =>
            (e.Level == LogLevel.Information || e.Level == LogLevel.Debug) &&
            e.Message.Contains("not.a.real.hook", StringComparison.Ordinal));
    }

    /// <summary>A saved override round-trips through <see cref="HookStore.Raw"/>.</summary>
    [Fact]
    public void Save_ThenRaw_RoundTrips()
    {
        using var dataDir = new TempDataDir();
        var store = new HookStore(dataDir.Options(), NullLogger<HookStore>.Instance);

        store.Save("getHelp.intro", "A brand new introduction.");

        Assert.Equal("A brand new introduction.", store.Raw("getHelp.intro"));
    }

    /// <summary>Saving the catalog's own default text removes the override rather than storing a redundant copy.</summary>
    [Fact]
    public void Save_WithCatalogDefaultText_RemovesAnyExistingOverride()
    {
        using var dataDir = new TempDataDir();
        var store = new HookStore(dataDir.Options(), NullLogger<HookStore>.Instance);
        store.Save("getHelp.intro", "A brand new introduction.");

        store.Save("getHelp.intro", HookCatalog.Get("getHelp.intro").Default);

        var onDisk = ReadHooksJson(dataDir.Path);
        Assert.False(onDisk.ContainsKey("getHelp.intro"));
        Assert.Equal(HookCatalog.Get("getHelp.intro").Default, store.Raw("getHelp.intro"));
    }

    /// <summary><see cref="HookStore.Reset"/> reverts a saved override back to the catalog default.</summary>
    [Fact]
    public void Reset_AfterSave_RevertsToCatalogDefault()
    {
        using var dataDir = new TempDataDir();
        var store = new HookStore(dataDir.Options(), NullLogger<HookStore>.Instance);
        store.Save("getHelp.intro", "A brand new introduction.");

        store.Reset("getHelp.intro");

        Assert.Equal(HookCatalog.Get("getHelp.intro").Default, store.Raw("getHelp.intro"));
        var onDisk = ReadHooksJson(dataDir.Path);
        Assert.False(onDisk.ContainsKey("getHelp.intro"));
    }

    /// <summary><see cref="HookStore.Render"/> substitutes placeholders the same way <see cref="HookRenderer"/> does.</summary>
    [Fact]
    public void Render_SubstitutesThroughToHookRenderer()
    {
        using var dataDir = new TempDataDir();
        var store = new HookStore(dataDir.Options(), NullLogger<HookStore>.Instance);
        var values = new Dictionary<string, string>
        {
            ["{{roomName}}"] = "Ops",
            ["{{roomId}}"] = "42",
        };

        var rendered = store.Render("turn.roomLabel", values);

        var expected = HookRenderer.Render(HookCatalog.Get("turn.roomLabel").Default, values);
        Assert.Equal(expected, rendered);
    }

    /// <summary><see cref="HookStore.Raw"/> with a key naming no hook throws <see cref="KeyNotFoundException"/>.</summary>
    [Fact]
    public void Raw_UnknownKey_ThrowsKeyNotFoundException()
    {
        using var dataDir = new TempDataDir();
        var store = new HookStore(dataDir.Options(), NullLogger<HookStore>.Instance);

        Assert.Throws<KeyNotFoundException>(() => store.Raw("not.a.real.hook"));
    }

    /// <summary><see cref="HookStore.Render"/> with a key naming no hook throws <see cref="KeyNotFoundException"/>.</summary>
    [Fact]
    public void Render_UnknownKey_ThrowsKeyNotFoundException()
    {
        using var dataDir = new TempDataDir();
        var store = new HookStore(dataDir.Options(), NullLogger<HookStore>.Instance);

        Assert.Throws<KeyNotFoundException>(() => store.Render("not.a.real.hook", new Dictionary<string, string>()));
    }

    /// <summary>
    /// The rebuild-before-notify invariant: a handler subscribed to <see cref="HookStore.HooksChanged"/>
    /// that reads the store from inside its own callback must see the value the save just produced,
    /// never the value that was true a moment before. This mirrors the exact ordering
    /// <c>PersonaStore.OnDebounceElapsed</c> documents and depends on for the same reason.
    /// </summary>
    [Fact]
    public void HooksChanged_HandlerReadsStoreInsideCallback_SeesTheNewValue()
    {
        using var dataDir = new TempDataDir();
        var store = new HookStore(dataDir.Options(), NullLogger<HookStore>.Instance);
        string? seenInsideHandler = null;
        store.HooksChanged += () => seenInsideHandler = store.Raw("getHelp.intro");

        store.Save("getHelp.intro", "Freshly saved text.");

        Assert.Equal("Freshly saved text.", seenInsideHandler);
    }

    /// <summary>The same rebuild-before-notify invariant, exercised through <see cref="HookStore.Reset"/> instead of <see cref="HookStore.Save"/>.</summary>
    [Fact]
    public void HooksChanged_AfterReset_HandlerReadsStoreInsideCallback_SeesTheDefault()
    {
        using var dataDir = new TempDataDir();
        var store = new HookStore(dataDir.Options(), NullLogger<HookStore>.Instance);
        store.Save("getHelp.intro", "Freshly saved text.");
        string? seenInsideHandler = null;
        store.HooksChanged += () => seenInsideHandler = store.Raw("getHelp.intro");

        store.Reset("getHelp.intro");

        Assert.Equal(HookCatalog.Get("getHelp.intro").Default, seenInsideHandler);
    }

    /// <summary>Writes a flat override JSON object directly, as a human editing the file by hand would.</summary>
    /// <param name="dataDir">The temp data directory a <see cref="HookStore"/> will be pointed at.</param>
    /// <param name="overrides">The override keys and text to write.</param>
    private static void WriteHooksJson(string dataDir, Dictionary<string, string> overrides)
    {
        var path = Path.Combine(dataDir, "hooks.json");
        File.WriteAllText(path, JsonSerializer.Serialize(overrides, ProtocolJson.Options));
    }

    /// <summary>Reads the override file back as a flat dictionary, for asserting what a <see cref="HookStore"/> actually wrote.</summary>
    /// <param name="dataDir">The temp data directory a <see cref="HookStore"/> was pointed at.</param>
    private static Dictionary<string, string> ReadHooksJson(string dataDir)
    {
        var path = Path.Combine(dataDir, "hooks.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, ProtocolJson.Options)
            ?? throw new InvalidOperationException("hooks.json deserialised to null.");
    }

    /// <summary>
    /// A hand-written fake <see cref="ILogger{T}"/> that records every call, since this repo has no
    /// mocking framework. Modelled as a minimal store of each entry's level and formatted message —
    /// enough for <see cref="Constructor_MalformedJson_FallsBackToDefaultsAndLogsWarning"/> and
    /// <see cref="Constructor_FileHasUnknownKey_LogsAtInformationOrDebugNeverWarning"/> to assert on
    /// both what was logged and at what severity.
    /// </summary>
    /// <typeparam name="T">The category type the recorded logger stands in for.</typeparam>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        /// <summary>Every call made to this logger so far, in call order.</summary>
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        /// <summary>Not used by this fake: scoping is irrelevant to the tests that need it, so this returns a no-op.</summary>
        /// <typeparam name="TState">The scope state type.</typeparam>
        /// <param name="state">The scope state.</param>
        /// <returns>A no-op <see cref="IDisposable"/>.</returns>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <summary>Always enabled, so every call this fake receives is actually recorded.</summary>
        /// <param name="logLevel">The level being checked.</param>
        /// <returns><see langword="true"/>, always.</returns>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <summary>Records one log call's level and formatted message.</summary>
        /// <typeparam name="TState">The state type carrying this call's structured values.</typeparam>
        /// <param name="logLevel">The call's severity.</param>
        /// <param name="eventId">Unused by this fake.</param>
        /// <param name="state">The call's structured state, passed to <paramref name="formatter"/>.</param>
        /// <param name="exception">The call's exception, if any.</param>
        /// <param name="formatter">Formats <paramref name="state"/> and <paramref name="exception"/> into the message text.</param>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            this.Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
