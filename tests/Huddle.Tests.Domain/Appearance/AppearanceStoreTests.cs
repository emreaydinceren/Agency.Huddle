using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Appearance;
using Agency.Huddle.Tests.Elicitation;
using Agency.Huddle.Tests.Waiting;

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
        Assert.Equal("lumen-light", store.Current.PreferredLightId);
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
        File.WriteAllText(path, "{\"theme\":\"lumen-dark\"}");

        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        Assert.Equal("lumen-dark", store.Current.ThemeId);
    }

#pragma warning disable S2699 // The test's only check is the Eventually.UntilAsync wait, which fails the test when the condition never holds; Sonar cannot see an assertion inside the shared helper.
    /// <summary>An edit made outside the process (a human hand-editing the file) is picked up by the filesystem watcher once its debounce settles.</summary>
    [Fact]
    public async Task Current_ExternalEdit_IsPickedUpByTheWatcher()
    {
        using var dataDir = new TempDataDir();
        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);
        var path = Path.Combine(dataDir.Path, "appearance.json");

        File.WriteAllText(path, "{\"theme\":\"lumen-light\"}");

        await Eventually.UntilAsync(() => string.Equals(store.Current.ThemeId, "lumen-light", StringComparison.Ordinal), TestContext.Current.CancellationToken, timeout: TestBudgets.Slow, interval: TimeSpan.FromMilliseconds(50));
    }
#pragma warning restore S2699

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

        store.Save("lumen-dark");

        Assert.Equal("lumen-dark", store.Current.ThemeId);
        var json = File.ReadAllText(path);
        Assert.Contains("lumen-dark", json, StringComparison.Ordinal);
        Assert.Contains("left alone", json, StringComparison.Ordinal);
        Assert.Contains("\"dark\"", json, StringComparison.Ordinal);
    }

    /// <summary>With nothing chosen the toggle moves between the two Lumen themes, and back.</summary>
    [Fact]
    public void ToggleMode_WithNothingChosen_SwitchesBetweenTheLumenThemes()
    {
        using var dataDir = new TempDataDir();
        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        store.ToggleMode();
        Assert.Equal("lumen-dark", store.Current.ThemeId);

        store.ToggleMode();
        Assert.Equal("lumen-light", store.Current.ThemeId);
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

        Assert.Equal("lumen-light", store.Current.PreferredLightId);
        Assert.Equal("lumen-dark", store.Current.PreferredDarkId);
    }

    /// <summary>Saving raises <see cref="AppearanceStore.AppearanceChanged"/> after the write and the rebuild.</summary>
    [Fact]
    public void Save_RaisesAppearanceChanged()
    {
        using var dataDir = new TempDataDir();
        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);
        var raised = false;
        store.AppearanceChanged += () => raised = true;

        store.Save("lumen-light");

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
        File.WriteAllText(path, "{\"theme\":\"lumen-dark\",\"libraryPaneSide\":\"left\"}");
        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        store.SaveAccentColor("lumen-dark", "#00b294");

        Assert.Equal("lumen-dark", store.Current.ThemeId);
        Assert.Equal("#00b294", store.Current.CustomizationFor("lumen-dark")?.Palette["primary"]);
        Assert.Contains("libraryPaneSide", File.ReadAllText(path), StringComparison.Ordinal);
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
        File.WriteAllText(path, """{ "theme": "lumen-light" }""");
        RecordingLogger<AppearanceStore> logger = new();
        using AppearanceStore store = new(dataDir.Options(), logger);

        using (FileStream held = LockedWatcherFileSupport.WriteAndHold(path, """{ "theme": "lumen-dark" }"""))
        {
            await LockedWatcherFileSupport.WaitForWarningAsync(logger, ct);

            Assert.Equal("lumen-light", store.Current.ThemeId);
        }
    }
}
