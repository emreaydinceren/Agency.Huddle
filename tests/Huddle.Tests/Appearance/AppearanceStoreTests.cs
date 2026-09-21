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

    /// <summary>
    /// Polls <paramref name="condition"/> until it is true or a generous timeout elapses, for
    /// asserting on a <see cref="FileSystemWatcher"/>-driven, timing-dependent side effect without a
    /// bare <see cref="Task.Delay(TimeSpan, CancellationToken)"/> whose length is only a guess.
    /// Copied from <c>HookStoreTests.WaitForAsync</c>.
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
