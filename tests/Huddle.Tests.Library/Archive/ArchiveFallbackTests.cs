using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library.Archive;

/// <summary>
/// Pins SB-R20 (Spec §8.5, corrections-12 items 4 and 10): a READ resolve that misses under the Teams root
/// falls back to the same relative path under the Archive root and marks it <c>Archived</c>; the strict
/// <c>TryResolve</c> every writer uses never falls back.
/// </summary>
public sealed class ArchiveFallbackTests : IDisposable
{
    private readonly LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();

    /// <inheritdoc />
    public void Dispose() => this.fixture.Dispose();

    private string ArchiveFile(string relative, string text = "# archived")
    {
        string full = Path.Combine(this.fixture.DataDir, "Archive", relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full) ?? throw new InvalidOperationException(full));
        File.WriteAllText(full, text);
        return full;
    }

    private string TeamsFile(string relative)
    {
        string full = Path.Combine(this.fixture.DataDir, "Teams", relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full) ?? throw new InvalidOperationException(full));
        File.WriteAllText(full, "# live");
        return full;
    }

    /// <summary>Home is archived (Archive/Home/_team.md exists, no Teams/Home): a read of teams:/Home/outputs/plan.md lands on archive:/Home/outputs/plan.md, Archived and read-only.</summary>
    [Fact]
    public void TryResolveForRead_TeamArchived_FallsBackToArchive()
    {
        string expected = this.ArchiveFile("Home/outputs/plan.md");
        this.ArchiveFile("Home/_team.md", "---\nname: Home\n---");

        bool ok = this.fixture.Resolver.TryResolveForRead("teams", "Home/outputs/plan.md", out LibraryPath? path, out _);

        Assert.True(ok);
        Assert.NotNull(path);
        Assert.Equal("archive", path.Root.Id);
        Assert.Equal("Home/outputs/plan.md", path.RelativePath);
        Assert.Equal(expected, path.FullPath);
        Assert.True(path.Archived);
        Assert.True(path.ReadOnly);
    }

    /// <summary>The strict resolve (writers) of the same archived path stays under Teams, not Archived, and does not exist: not-found.</summary>
    [Fact]
    public void TryResolve_TeamArchived_StaysUnderTeamsAndMissing()
    {
        this.ArchiveFile("Home/outputs/plan.md");

        bool ok = this.fixture.Resolver.TryResolve("teams", "Home/outputs/plan.md", out LibraryPath? path, out _);

        Assert.True(ok);
        Assert.NotNull(path);
        Assert.Equal("teams", path.Root.Id);
        Assert.False(path.Archived);
        Assert.False(File.Exists(path.FullPath));
    }

    /// <summary>A live path is unaffected by the read intent: same root, not Archived, even when an archive copy of the same name exists.</summary>
    [Fact]
    public void TryResolveForRead_LivePath_IsUnaffected()
    {
        string live = this.TeamsFile("Home/outputs/plan.md");
        this.ArchiveFile("Home/outputs/plan.md");

        bool ok = this.fixture.Resolver.TryResolveForRead("teams", "Home/outputs/plan.md", out LibraryPath? path, out _);

        Assert.True(ok);
        Assert.NotNull(path);
        Assert.Equal("teams", path.Root.Id);
        Assert.Equal(live, path.FullPath);
        Assert.False(path.Archived);
    }

    /// <summary>An archived Project of a live Team (Archive/Home/Old/x.md, Home live) falls back too.</summary>
    [Fact]
    public void TryResolveForRead_ProjectArchived_FallsBackToArchive()
    {
        this.TeamsFile("Home/_team.md");
        string expected = this.ArchiveFile("Home/Old/x.md");

        bool ok = this.fixture.Resolver.TryResolveForRead("teams", "Home/Old/x.md", out LibraryPath? path, out _);

        Assert.True(ok);
        Assert.NotNull(path);
        Assert.Equal("archive", path.Root.Id);
        Assert.Equal(expected, path.FullPath);
        Assert.True(path.Archived);
    }

    /// <summary>A missing path with no archive copy stays not-found: under Teams, not Archived, nonexistent.</summary>
    [Fact]
    public void TryResolveForRead_NoArchiveCopy_StaysNotFound()
    {
        this.ArchiveFile("Home/outputs/other.md");

        bool ok = this.fixture.Resolver.TryResolveForRead("teams", "Home/outputs/plan.md", out LibraryPath? path, out _);

        Assert.True(ok);
        Assert.NotNull(path);
        Assert.Equal("teams", path.Root.Id);
        Assert.False(path.Archived);
        Assert.False(File.Exists(path.FullPath));
    }

    /// <summary>A Team spelled in another case (teams:/home/...) still finds Archive/Home by listing, on Linux too; the result carries the on-disk spelling.</summary>
    [Fact]
    public void TryResolveForRead_DifferentCase_FindsArchiveByListing()
    {
        string expected = this.ArchiveFile("Home/outputs/plan.md");

        bool ok = this.fixture.Resolver.TryResolveForRead("teams", "home/Outputs/PLAN.md", out LibraryPath? path, out _);

        Assert.True(ok);
        Assert.NotNull(path);
        Assert.Equal("archive", path.Root.Id);
        Assert.Equal("Home/outputs/plan.md", path.RelativePath);
        Assert.Equal(expected, path.FullPath);
        Assert.True(path.Archived);
    }
}
