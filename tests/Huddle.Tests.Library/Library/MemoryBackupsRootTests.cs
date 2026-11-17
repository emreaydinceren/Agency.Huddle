using Agency.Huddle.App;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Search;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Pins the Memory backups root (Memory Tidy spec §6.12 and §8.1, "The backup root"): it exists, is read only, follows
/// <c>Team:Library:MemoryBackups:Dir</c> and resolves a backup path, and neither Global Search nor the Agent's <c>search</c> tool
/// (both go through <see cref="LibrarySearchProvider"/>) returns anything from it.
/// </summary>
public sealed class MemoryBackupsRootTests
{
    /// <summary>The root is built in, read only, of its own kind, and sits under the data folder at the default name.</summary>
    [Fact]
    public void Roots_HoldTheReadOnlyMemoryBackupsRoot_AtTheDefaultFolder()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();

        LibraryRoot root = Assert.Single(fixture.RootStore.Roots, static root => root.Kind == LibraryRootKind.MemoryBackups);

        Assert.Equal("memory-backups", root.Id);
        Assert.Equal("Memory backups", root.DisplayName);
        Assert.True(root.ReadOnly);
        Assert.Equal(Path.GetFullPath(Path.Combine(fixture.DataDir, "MemoryBackups")), root.FullPath);
        Assert.Contains(fixture.RootStore.VisibleRoots, static visible => visible.Kind == LibraryRootKind.MemoryBackups);
    }

    /// <summary>The folder name comes from <c>Team:Library:MemoryBackups:Dir</c>.</summary>
    [Fact]
    public void Roots_FollowTheConfiguredFolderName()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build(static options => options.Library.MemoryBackups.Dir = "Copies");

        LibraryRoot root = Assert.Single(fixture.RootStore.Roots, static root => root.Kind == LibraryRootKind.MemoryBackups);

        Assert.Equal(Path.GetFullPath(Path.Combine(fixture.DataDir, "Copies")), root.FullPath);
    }

    /// <summary>A copy under a pass folder resolves through the root, and the root can be hidden like any built-in one.</summary>
    [Fact]
    public void Resolver_ResolvesABackupPath_AndTheRootCanBeHidden()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string copy = Path.Combine(fixture.DataDir, "MemoryBackups", "2026-11-22", "Agents", "Nova", "pricing.md");
        WriteFile(copy, "old pricing");

        bool resolved = fixture.Resolver.TryResolve("memory-backups", "2026-11-22/Agents/Nova/pricing.md", out LibraryPath? path, out _);
        fixture.RootStore.SetHidden("memory-backups", true);

        Assert.True(resolved);
        Assert.NotNull(path);
        Assert.Equal(Path.GetFullPath(copy), Path.GetFullPath(path.FullPath));
        Assert.DoesNotContain(fixture.RootStore.VisibleRoots, static visible => visible.Kind == LibraryRootKind.MemoryBackups);
    }

    /// <summary>
    /// A phrase held by a backup copy and by a Team file finds only the Team file: the provider leaves the backup root out. The same provider
    /// serves Global Search and the Agent's <c>search</c> tool.
    /// </summary>
    [Fact]
    public async Task Search_NeverReturnsAHitFromTheBackupRoot()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        WriteFile(Path.Combine(fixture.DataDir, "MemoryBackups", "2026-11-22", "Agents", "Nova", "zebra-pricing.md"), "The zebra price was 40.");
        WriteFile(Path.Combine(fixture.DataDir, "MemoryBackups", "2026-11-22", "changes.md"), "The zebra file was rewritten.");
        WriteFile(Path.Combine(fixture.DataDir, "Teams", "Alpha", "notes.md"), "The zebra crossed.");
        LibrarySearchProvider provider = new(fixture.RootStore, fixture.Resolver, fixture.CreateService(), Options.Create(new TeamOptions { DataDir = fixture.DataDir }));

        SearchResult named = await provider.SearchAsync(new SearchQuery("zebra", SearchFilter.Library, null, SearchScope.Active, 50), TestContext.Current.CancellationToken);

        string[] rootIds = [.. named.Hits.OfType<LibraryHit>().Select(static hit => hit.RootId)];
        Assert.Equal(["teams"], rootIds);
    }

    /// <summary>Writes <paramref name="text"/> to <paramref name="path"/>, creating its folder first.</summary>
    /// <param name="path">The file to write.</param>
    /// <param name="text">The file's text.</param>
    private static void WriteFile(string path, string text)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidOperationException("The path has no folder."));
        File.WriteAllText(path, text);
    }
}
