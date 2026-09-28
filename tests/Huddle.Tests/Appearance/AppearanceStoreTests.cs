using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Appearance;

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
        File.WriteAllText(path, "{\"theme\":\"solarized-dark\",\"dark\":\"light\"}");

        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        Assert.Equal("solarized-dark", store.Current.ThemeId);
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

    /// <summary>The default library pane side is Right, when no file exists or the key is absent.</summary>
    [Fact]
    public void LibraryPaneSide_Default_IsRight()
    {
        using var dataDir = new TempDataDir();
        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        Assert.Equal(LibraryPaneSide.Right, store.Current.LibraryPaneSide);
    }

    /// <summary>Saving the library pane side to Left persists it, and a new store reads it back as Left from appearance.json.</summary>
    [Fact]
    public void SaveLibraryPaneSide_Left_PersistsAndReloads()
    {
        using var dataDir = new TempDataDir();
        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        store.SaveLibraryPaneSide(LibraryPaneSide.Left);

        Assert.Equal(LibraryPaneSide.Left, store.Current.LibraryPaneSide);

        // Create a new store to verify it reads from disk
        using var store2 = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        Assert.Equal(LibraryPaneSide.Left, store2.Current.LibraryPaneSide);
    }

    /// <summary>
    /// Saving the library pane side preserves the theme and accent color - the side writer must not
    /// clobber unrelated keys, just like <see cref="AppearanceStore.Save"/> and <see cref="AppearanceStore.SaveAccentColor"/> do not.
    /// </summary>
    [Fact]
    public void SaveLibraryPaneSide_KeepsTheme()
    {
        using var dataDir = new TempDataDir();
        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);
        store.Save("huddle-dark");
        store.SaveAccentColor("#00b294");

        store.SaveLibraryPaneSide(LibraryPaneSide.Left);

        Assert.Equal("huddle-dark", store.Current.ThemeId);
        Assert.Equal("#00b294", store.Current.AccentColorHex);
        Assert.Equal(LibraryPaneSide.Left, store.Current.LibraryPaneSide);
    }

    /// <summary>Saving the library pane side raises <see cref="AppearanceStore.AppearanceChanged"/> after the write and the rebuild, mirroring the theme and accent color save behavior.</summary>
    [Fact]
    public void SaveLibraryPaneSide_RaisesAppearanceChanged()
    {
        using var dataDir = new TempDataDir();
        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);
        var raised = false;
        store.AppearanceChanged += () => raised = true;

        store.SaveLibraryPaneSide(LibraryPaneSide.Left);

        Assert.True(raised);
    }

    /// <summary>An appearance.json from before the library pane side feature existed has no libraryPaneSide key, and loads as the default Right without throwing.</summary>
    [Fact]
    public void Current_WithNoLibraryPaneSideKey_DefaultsToRight()
    {
        using var dataDir = new TempDataDir();
        var path = Path.Combine(dataDir.Path, "appearance.json");
        File.WriteAllText(path, "{\"theme\":\"huddle-dark\",\"accentColor\":\"#00b294\"}");

        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        Assert.Equal(LibraryPaneSide.Right, store.Current.LibraryPaneSide);
        Assert.Equal("huddle-dark", store.Current.ThemeId);
        Assert.Equal("#00b294", store.Current.AccentColorHex);
    }

    /// <summary>An unknown libraryPaneSide value (e.g. "up") loads as the default Right without throwing, and the file is left unchanged.</summary>
    [Fact]
    public void Current_WithAnUnknownLibraryPaneSideValue_DefaultsToRightAndLeavesTheFileUnchanged()
    {
        using var dataDir = new TempDataDir();
        var path = Path.Combine(dataDir.Path, "appearance.json");
        File.WriteAllText(path, "{\"libraryPaneSide\":\"up\"}");

        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        Assert.Equal(LibraryPaneSide.Right, store.Current.LibraryPaneSide);
        Assert.Equal("{\"libraryPaneSide\":\"up\"}", File.ReadAllText(path));
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
}
