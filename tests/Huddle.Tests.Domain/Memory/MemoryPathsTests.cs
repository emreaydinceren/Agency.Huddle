namespace Agency.Huddle.Tests.Memory;

using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Teams;

/// <summary>
/// Pins <see cref="MemoryPaths.TryResolve"/>, the one rule turning a <c>memory</c> call's <c>folder</c> and <c>file</c> into a
/// path, shared by the tool and by File Changes (Instructions spec §5.3, §6.1).
/// </summary>
public sealed class MemoryPathsTests
{
    /// <summary>Alpha: Ada manages, Leo is a member; one Project, Web.</summary>
    /// <param name="harness">The harness whose catalog is filled.</param>
    private static void Seed(MemoryToolHarness harness)
    {
        harness.Catalog.Teams = [new TeamSummary("Alpha", ["Web"], ["Leo"], true) { Manager = "Ada" }];
        Directory.CreateDirectory(Path.Combine(harness.TeamsRoot, "Alpha", "Web"));
    }

    /// <summary>A Team folder resolves under <c>Teams/Alpha/memory</c> and a Project folder under <c>Teams/Alpha/Web/memory</c>, each spelled as the catalog spells it.</summary>
    [Fact]
    public void TryResolve_TeamAndProjectFolders_ResolveUnderTheTeamsRoot()
    {
        using MemoryToolHarness harness = new();
        Seed(harness);
        MemoryPaths paths = harness.Paths();

        bool team = paths.TryResolve("Ada", "alpha", "notes.md", out string? teamPath);
        bool project = paths.TryResolve("Leo", "Alpha/web", "notes.md", out string? projectPath);

        Assert.True(team);
        Assert.True(project);
        Assert.Equal(Path.Combine(harness.TeamsRoot, "Alpha", "memory", "notes.md"), teamPath);
        Assert.Equal(Path.Combine(harness.TeamsRoot, "Alpha", "Web", "memory", "notes.md"), projectPath);
    }

    /// <summary>No folder, and <c>me</c>, both resolve to the caller's own memory folder.</summary>
    [Fact]
    public void TryResolve_OwnFolder_ResolvesUnderTheWorkDir()
    {
        using MemoryToolHarness harness = new();
        MemoryPaths paths = harness.Paths();

        bool absent = paths.TryResolve("Nova", null, "notes.md", out string? absentPath);
        bool me = paths.TryResolve("Nova", "me", "notes.md", out string? mePath);

        Assert.True(absent);
        Assert.True(me);
        Assert.Equal(Path.Combine(harness.MemoryDir("Nova"), "notes.md"), absentPath);
        Assert.Equal(absentPath, mePath);
    }

    /// <summary>A Team the caller is not in, an unknown Project, a file name that is not a memory file and a missing file all resolve to nothing.</summary>
    /// <param name="caller">The calling Persona.</param>
    /// <param name="folder">The <c>folder</c> argument.</param>
    /// <param name="file">The <c>file</c> argument.</param>
    [Theory]
    [InlineData("Zed", "Alpha", "notes.md")]
    [InlineData("Ada", "Alpha/Mobile", "notes.md")]
    [InlineData("Ada", "Alpha", "../notes.md")]
    [InlineData("Ada", "Alpha", "notes.txt")]
    [InlineData("Ada", "Alpha", null)]
    public void TryResolve_UnusableFolderOrFile_ResolvesToNothing(string caller, string folder, string? file)
    {
        using MemoryToolHarness harness = new();
        Seed(harness);

        bool resolved = harness.Paths().TryResolve(caller, folder, file, out string? path);

        Assert.False(resolved);
        Assert.Null(path);
    }
}
