using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Appearance;
using Agency.Huddle.Tests.Elicitation;

namespace Agency.Huddle.Tests.Appearance;

/// <summary>
/// Tests for <see cref="AppearanceStore"/>: that it joins <see cref="Agency.Huddle.App.Themes.ThemeCatalog"/>
/// with a selection file, tolerates every shape of a missing or malformed file without throwing,
/// never drops a user's unrelated data (an unknown top-level key stays in the file), and rebuilds
/// its resolved snapshot before raising <see cref="AppearanceStore.AppearanceChanged"/> rather than
/// after.
/// </summary>
public sealed class AppearanceStoreTests
{
    /// <summary>A missing selection file is the normal first-run case: everything resolves to the default and the constructor never creates one.</summary>
    [Fact]
    public void Current_WithNoFile_IsEmptyAndCreatesNothing()
    {
        using var dataDir = new TempDataDir();
        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        Assert.Null(store.Current.ThemeId);
        Assert.False(File.Exists(Path.Combine(dataDir.Path, "appearance.json")));
    }

    /// <summary>Malformed JSON falls back to the default appearance, and does not throw.</summary>
    [Fact]
    public void Current_WithMalformedFile_FallsBackToEmpty()
    {
        using var dataDir = new TempDataDir();
        File.WriteAllText(Path.Combine(dataDir.Path, "appearance.json"), "{ this is not valid json");

        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        Assert.Null(store.Current.ThemeId);
    }

    /// <summary>
    /// A theme id naming no catalogue entry is a warning, never a failure: no theme is selected
    /// (so the caller falls back to the catalog's first entry) and the file is left exactly as it
    /// was, so fixing the id restores the choice with no further edit.
    /// </summary>
    [Fact]
    public void Current_WithAnUnknownThemeId_SelectsNoThemeAndLeavesTheFileUnchanged()
    {
        using var dataDir = new TempDataDir();
        var path = Path.Combine(dataDir.Path, "appearance.json");
        File.WriteAllText(path, "{\"theme\":\"nonsense\"}");

        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        Assert.Null(store.Current.ThemeId);
        Assert.Contains("nonsense", File.ReadAllText(path), StringComparison.Ordinal);
    }

    /// <summary>
    /// An <c>appearance.json</c> written while the VS Code ports shipped still names one (here
    /// <c>dark-modern</c>). That id is gone from the catalogue, so it takes the same path as any unknown id:
    /// the existing warning naming it, no theme selected (the default applies), and the file left as it was.
    /// </summary>
    [Fact]
    public void Current_WithADeletedVsCodeThemeId_WarnsAndFallsBackToTheDefault()
    {
        using var dataDir = new TempDataDir();
        var path = Path.Combine(dataDir.Path, "appearance.json");
        File.WriteAllText(path, "{\"theme\":\"dark-modern\"}");
        WarningRecorder logger = new();

        using var store = new AppearanceStore(dataDir.Options(), logger);

        Assert.Null(store.Current.ThemeId);
        Assert.Equal("huddle", store.Current.PreferredLightId);
        Assert.Equal("{\"theme\":\"dark-modern\"}", File.ReadAllText(path));
        Assert.Equal(
            $"Appearance file '{path}' selects theme 'dark-modern', which is not a known theme; the default theme is used instead and the file is left unchanged.",
            Assert.Single(logger.Warnings));
    }

    /// <summary>Collects the formatted text of every warning an <see cref="AppearanceStore"/> logs.</summary>
    private sealed class WarningRecorder : ILogger<AppearanceStore>
    {
        /// <summary>The formatted warning messages, in order.</summary>
        public List<string> Warnings { get; } = [];

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                this.Warnings.Add(formatter(state, exception));
            }
        }
    }

    /// <summary>
    /// The retired <c>dark</c> key, still present in any <c>appearance.json</c> written before the
    /// light/dark control was folded into the theme, is now just an unknown key: the theme beside it
    /// still resolves, nothing warns, and the key itself is left on disk rather than deleted. This is
    /// the whole of the migration story for an existing file - there is no migration code.
    /// </summary>
    [Fact]
    public void Current_WithARetiredDarkKey_ResolvesTheThemeAndKeepsTheKey()
    {
        using var dataDir = new TempDataDir();
        var path = Path.Combine(dataDir.Path, "appearance.json");
        File.WriteAllText(path, "{\"theme\":\"nordic-dark\",\"dark\":\"light\"}");

        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        Assert.Equal("nordic-dark", store.Current.ThemeId);
        Assert.Contains("\"dark\"", File.ReadAllText(path), StringComparison.Ordinal);
    }

    /// <summary>A file naming a known theme resolves it - the one value this store now carries.</summary>
    [Fact]
    public void Current_WithAKnownTheme_ResolvesIt()
    {
        using var dataDir = new TempDataDir();
        var path = Path.Combine(dataDir.Path, "appearance.json");
        File.WriteAllText(path, "{\"theme\":\"huddle-dark\"}");

        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        Assert.Equal("huddle-dark", store.Current.ThemeId);
    }

    /// <summary>An edit made outside the process (a human hand-editing the file) is picked up by the filesystem watcher once its debounce settles.</summary>
    [Fact]
    public async Task Current_ExternalEdit_IsPickedUpByTheWatcher()
    {
        using var dataDir = new TempDataDir();
        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);
        var path = Path.Combine(dataDir.Path, "appearance.json");

        File.WriteAllText(path, "{\"theme\":\"huddle\"}");

        await WaitForAsync(
            () => string.Equals(store.Current.ThemeId, "huddle", StringComparison.Ordinal),
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Saving re-reads the file first, so an unknown top-level key already on disk survives the
    /// round-trip untouched - the tab must not be able to clobber data it does not understand.
    /// </summary>
    [Fact]
    public void Save_WritesTheThemeAndLeavesUnknownKeysUntouched()
    {
        using var dataDir = new TempDataDir();
        var path = Path.Combine(dataDir.Path, "appearance.json");
        File.WriteAllText(path, "{\"custom-note\":\"left alone\",\"dark\":\"light\"}");

        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        store.Save("huddle-dark");

        Assert.Equal("huddle-dark", store.Current.ThemeId);
        var json = File.ReadAllText(path);
        Assert.Contains("huddle-dark", json, StringComparison.Ordinal);
        Assert.Contains("left alone", json, StringComparison.Ordinal);
        Assert.Contains("\"dark\"", json, StringComparison.Ordinal);
    }

    /// <summary>With nothing chosen the toggle moves between the two Huddle themes, and back.</summary>
    [Fact]
    public void ToggleMode_WithNothingChosen_SwitchesBetweenTheHuddleThemes()
    {
        using var dataDir = new TempDataDir();
        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        store.ToggleMode();
        Assert.Equal("huddle-dark", store.Current.ThemeId);

        store.ToggleMode();
        Assert.Equal("huddle", store.Current.ThemeId);
    }

    /// <summary>Choosing a dark theme while a light one is shown records it for the toggle without changing what is shown.</summary>
    [Fact]
    public void SavePreferred_OtherModesTheme_IsRememberedAndNotShownUntilToggled()
    {
        using var dataDir = new TempDataDir();
        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        store.SavePreferred("nordic-dark");

        Assert.Null(store.Current.ThemeId);
        Assert.Equal("nordic-dark", store.Current.DarkThemeId);

        store.ToggleMode();

        Assert.Equal("nordic-dark", store.Current.ThemeId);
    }

    /// <summary>Choosing a theme of the mode already shown repaints at once.</summary>
    [Fact]
    public void SavePreferred_SameModeTheme_IsShownImmediately()
    {
        using var dataDir = new TempDataDir();
        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        store.SavePreferred("nordic-light");

        Assert.Equal("nordic-light", store.Current.ThemeId);
        Assert.Equal("nordic-light", store.Current.LightThemeId);
    }

    /// <summary>A file written before the pair existed still toggles back to the theme it was using.</summary>
    [Fact]
    public void ToggleMode_FromALegacyFile_ReturnsToTheThemeThatWasInUse()
    {
        using var dataDir = new TempDataDir();
        File.WriteAllText(Path.Combine(dataDir.Path, "appearance.json"), "{\"theme\":\"nordic-dark\"}");
        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        store.ToggleMode();
        store.ToggleMode();

        Assert.Equal("nordic-dark", store.Current.ThemeId);
    }

    /// <summary>A stored light choice that names a dark theme is ignored rather than shown.</summary>
    [Fact]
    public void Current_WithALightThemeKeyNamingADarkTheme_IgnoresIt()
    {
        using var dataDir = new TempDataDir();
        File.WriteAllText(Path.Combine(dataDir.Path, "appearance.json"), "{\"lightTheme\":\"nordic-dark\"}");
        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        Assert.Equal("huddle", store.Current.PreferredLightId);
    }

    /// <summary>Saving raises <see cref="AppearanceStore.AppearanceChanged"/> after the write and the rebuild.</summary>
    [Fact]
    public void Save_RaisesAppearanceChanged()
    {
        using var dataDir = new TempDataDir();
        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);
        var raised = false;
        store.AppearanceChanged += () => raised = true;

        store.Save("huddle");

        Assert.True(raised);
    }

    /// <summary>A well-formed <c>#rrggbb</c> accent colour resolves - the one extra value this store carries alongside the theme id.</summary>
    [Fact]
    public void Current_WithAValidAccentColor_ResolvesIt()
    {
        using var dataDir = new TempDataDir();
        var path = Path.Combine(dataDir.Path, "appearance.json");
        File.WriteAllText(path, "{\"theme\":\"custom-accent\",\"accentColor\":\"#00b294\"}");

        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        Assert.Equal("custom-accent", store.Current.ThemeId);
        Assert.Equal("#00b294", store.Current.AccentColorHex);
    }

    /// <summary>A malformed accent colour is a warning, never a failure: no colour is used (so the placeholder theme applies) and the file is left exactly as it was, the same tolerance an unknown theme id gets.</summary>
    [Fact]
    public void Current_WithAMalformedAccentColor_ResolvesNullAndLeavesTheFileUnchanged()
    {
        using var dataDir = new TempDataDir();
        var path = Path.Combine(dataDir.Path, "appearance.json");
        File.WriteAllText(path, "{\"accentColor\":\"not-a-colour\"}");

        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        Assert.Null(store.Current.AccentColorHex);
        Assert.Contains("not-a-colour", File.ReadAllText(path), StringComparison.Ordinal);
    }

    /// <summary><see cref="AppearanceStore.SaveAccentColor"/> writes its own key and leaves the theme id - saved separately by <see cref="AppearanceStore.Save"/> - untouched, proving the two writers cannot clobber each other.</summary>
    [Fact]
    public void SaveAccentColor_WritesTheAccentColorAndLeavesTheThemeUntouched()
    {
        using var dataDir = new TempDataDir();
        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);
        store.Save("custom-accent");

        store.SaveAccentColor("#00b294");

        Assert.Equal("custom-accent", store.Current.ThemeId);
        Assert.Equal("#00b294", store.Current.AccentColorHex);
    }

    /// <summary><see cref="AppearanceStore.SaveAccentColor"/> with <see langword="null"/> clears a previously chosen colour, removing the key entirely rather than leaving an empty string behind.</summary>
    [Fact]
    public void SaveAccentColor_WithNull_ClearsAPreviouslyChosenColour()
    {
        using var dataDir = new TempDataDir();
        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);
        store.SaveAccentColor("#00b294");

        store.SaveAccentColor(null);

        Assert.Null(store.Current.AccentColorHex);
        Assert.DoesNotContain("accentColor", File.ReadAllText(Path.Combine(dataDir.Path, "appearance.json")), StringComparison.Ordinal);
    }

    /// <summary>Saving an accent colour raises <see cref="AppearanceStore.AppearanceChanged"/> after the write and the rebuild, mirroring <see cref="Save_RaisesAppearanceChanged"/>.</summary>
    [Fact]
    public void SaveAccentColor_RaisesAppearanceChanged()
    {
        using var dataDir = new TempDataDir();
        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);
        var raised = false;
        store.AppearanceChanged += () => raised = true;

        store.SaveAccentColor("#00b294");

        Assert.True(raised);
    }

    /// <summary>
    /// An appearance.json written while the docked Library pane existed still carries a
    /// <c>libraryPaneSide</c> key. It is ignored without a warning, and a later save leaves it as found
    /// rather than rewriting a file the Human may have edited.
    /// </summary>
    [Fact]
    public void Current_WithALeftoverLibraryPaneSideKey_IgnoresItAndKeepsItOnSave()
    {
        using var dataDir = new TempDataDir();
        var path = Path.Combine(dataDir.Path, "appearance.json");
        File.WriteAllText(path, "{\"theme\":\"huddle-dark\",\"libraryPaneSide\":\"left\"}");
        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        store.SaveAccentColor("#00b294");

        Assert.Equal("huddle-dark", store.Current.ThemeId);
        Assert.Equal("#00b294", store.Current.AccentColorHex);
        Assert.Contains("libraryPaneSide", File.ReadAllText(path), StringComparison.Ordinal);
    }

    /// <summary>
    /// Polls <paramref name="condition"/> until it is true or a generous timeout elapses, for
    /// asserting on a <see cref="FileSystemWatcher"/>-driven, timing-dependent side effect without a
    /// bare <see cref="Task.Delay(TimeSpan, CancellationToken)"/> whose length is only a guess.
    /// Copied from <c>PromptStoreTests.WaitForAsync</c>.
    /// </summary>
    /// <param name="condition">Checked repeatedly until it returns <see langword="true"/>.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    private static async Task WaitForAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Timed out waiting for the filesystem watcher to pick up the change.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
    }

    /// <summary>
    /// A watcher rebuild that cannot read the file on any attempt (an editor holds it exclusively) logs a
    /// warning and keeps the previously resolved appearance instead of letting the <see cref="IOException"/> escape the timer thread.
    /// </summary>
    [Fact]
    public async Task Current_FileLockedForEveryRetry_KeepsThePreviousAppearance()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        string path = Path.Combine(dataDir.Path, "appearance.json");
        File.WriteAllText(path, """{ "theme": "huddle" }""");
        RecordingLogger<AppearanceStore> logger = new();
        using AppearanceStore store = new(dataDir.Options(), logger);

        using (FileStream held = LockedWatcherFileSupport.WriteAndHold(path, """{ "theme": "huddle-dark" }"""))
        {
            await LockedWatcherFileSupport.WaitForWarningAsync(logger, ct);

            Assert.Equal("huddle", store.Current.ThemeId);
        }
    }
}
