using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Appearance;

namespace Agency.Huddle.Tests.Appearance;

/// <summary>
/// Tests for <see cref="AppearanceStore"/>: that it joins the built-in theme pair with an override
/// file, tolerates every shape of a missing or malformed file without throwing, never drops a
/// user's unrelated data (an unknown theme id or override key stays in the file), and rebuilds its
/// resolved snapshot before raising <see cref="AppearanceStore.AppearanceChanged"/> rather than
/// after.
/// </summary>
public sealed class AppearanceStoreTests
{
    /// <summary>A missing override file is the normal first-run case: everything resolves empty and the constructor never creates one.</summary>
    [Fact]
    public void Current_WithNoFile_IsEmptyAndCreatesNothing()
    {
        using var dataDir = new TempDataDir();
        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        Assert.Null(store.Current.ThemeId);
        Assert.Equal(string.Empty, store.Current.OverrideCss);
        Assert.Empty(store.Current.Problems);
        Assert.False(File.Exists(Path.Combine(dataDir.Path, "appearance.json")));
    }

    /// <summary>Malformed JSON falls back to no theme and no overrides, and does not throw.</summary>
    [Fact]
    public void Current_WithMalformedFile_FallsBackToEmpty()
    {
        using var dataDir = new TempDataDir();
        File.WriteAllText(Path.Combine(dataDir.Path, "appearance.json"), "{ this is not valid json");

        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        Assert.Null(store.Current.ThemeId);
        Assert.Equal(string.Empty, store.Current.OverrideCss);
    }

    /// <summary>
    /// A theme id naming no catalogue entry is a warning, never a failure: the built-in theme
    /// applies and the file is left exactly as it was, so re-adding the theme restores the choice.
    /// </summary>
    [Fact]
    public void Current_WithAnUnknownThemeId_SelectsNoThemeAndLeavesTheFileUnchanged()
    {
        using var dataDir = new TempDataDir();
        var path = Path.Combine(dataDir.Path, "appearance.json");
        File.WriteAllText(path, "{\"theme\":\"nonsense\"}");

        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        Assert.Null(store.Current.ThemeId);
        Assert.NotEmpty(store.Current.Problems);
        Assert.Contains("nonsense", File.ReadAllText(path), StringComparison.Ordinal);
    }

    /// <summary>An override key naming no theme token is dropped from resolution but kept in the file, never silently deleted.</summary>
    [Fact]
    public void Current_WithAnUnknownOverrideKey_KeepsItInTheFile()
    {
        using var dataDir = new TempDataDir();
        var path = Path.Combine(dataDir.Path, "appearance.json");
        File.WriteAllText(path, "{\"overrides\":{\"--not-a-token\":\"red\"}}");

        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        Assert.Equal(string.Empty, store.Current.OverrideCss);
        Assert.NotEmpty(store.Current.Problems);
        Assert.Contains("--not-a-token", File.ReadAllText(path), StringComparison.Ordinal);
    }

    /// <summary>A file naming both a known theme and valid overrides resolves both at once.</summary>
    [Fact]
    public void Current_WithAThemeAndOverrides_ResolvesBoth()
    {
        using var dataDir = new TempDataDir();
        var path = Path.Combine(dataDir.Path, "appearance.json");
        File.WriteAllText(path, "{\"theme\":\"huddle-dark\",\"overrides\":{\"--font-chat\":\"Georgia, serif\"}}");

        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        Assert.Equal("huddle-dark", store.Current.ThemeId);
        Assert.Equal(":root {\n    --font-chat: Georgia, serif;\n}", store.Current.OverrideCss);
        Assert.Empty(store.Current.Problems);
    }

    /// <summary>An edit made outside the process (a human hand-editing the file) is picked up by the filesystem watcher once its debounce settles.</summary>
    [Fact]
    public async Task Current_ExternalEdit_IsPickedUpByTheWatcher()
    {
        using var dataDir = new TempDataDir();
        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);
        var path = Path.Combine(dataDir.Path, "appearance.json");

        File.WriteAllText(path, "{\"theme\":\"huddle-dark\"}");

        await WaitForAsync(
            () => string.Equals(store.Current.ThemeId, "huddle-dark", StringComparison.Ordinal),
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Saving a theme re-reads the file first, so a hand-edited <c>overrides</c> block already on
    /// disk survives the round-trip untouched - the tab must not be able to clobber it.
    /// </summary>
    [Fact]
    public void Save_WritesTheThemeAndLeavesOverridesUntouched()
    {
        using var dataDir = new TempDataDir();
        var path = Path.Combine(dataDir.Path, "appearance.json");
        File.WriteAllText(path, "{\"overrides\":{\"--font-chat\":\"Georgia, serif\"}}");

        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);

        store.Save("huddle-dark");

        Assert.Equal("huddle-dark", store.Current.ThemeId);
        var json = File.ReadAllText(path);
        Assert.Contains("huddle-dark", json, StringComparison.Ordinal);
        Assert.Contains("Georgia, serif", json, StringComparison.Ordinal);
    }

    /// <summary>Saving raises <see cref="AppearanceStore.AppearanceChanged"/> after the write and the rebuild.</summary>
    [Fact]
    public void Save_RaisesAppearanceChanged()
    {
        using var dataDir = new TempDataDir();
        using var store = new AppearanceStore(dataDir.Options(), NullLogger<AppearanceStore>.Instance);
        var raised = false;
        store.AppearanceChanged += () => raised = true;

        store.Save("huddle-light");

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
