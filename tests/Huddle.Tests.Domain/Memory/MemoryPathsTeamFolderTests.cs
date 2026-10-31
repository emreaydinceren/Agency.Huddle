using Agency.Huddle.App.Memory;
using Agency.Huddle.Tests.Helpers;

namespace Agency.Huddle.Tests.Memory;

/// <summary>
/// Pins <see cref="MemoryPaths.TryResolveTeamFolder"/>, how Huddle resolves the folder a Team Memory Verdict names (Team Memory
/// Helper spec §6.6): only the proposing Team's own folder or one of its Projects', whatever the label says.
/// </summary>
public sealed class MemoryPathsTeamFolderTests
{
    /// <summary>The Team and its Project resolve, ignoring case, to the catalog's spelling.</summary>
    /// <param name="label">The label as a Verdict might write it.</param>
    /// <param name="expected">The label the folder carries.</param>
    /// <param name="project">The Project folder, or <see langword="null"/> for the Team-wide one.</param>
    [Theory]
    [InlineData("Research", "Research", null)]
    [InlineData("research", "Research", null)]
    [InlineData(" Research/atlas ", "Research/Atlas", "Atlas")]
    public void TryResolveTeamFolder_TheTeamOrItsProject_Resolves(string label, string expected, string? project)
    {
        using TeamMemoryHarness harness = new();

        Assert.True(harness.Paths.TryResolveTeamFolder("Research", label, out MemoryFolder? folder));

        Assert.Equal(expected, folder.Label);
        Assert.Equal(harness.Folder(project), folder.Path);
    }

    /// <summary>Anything outside the Team is refused: <c>me</c>, another Team, an unknown Project, a path, a parent step or nothing.</summary>
    /// <param name="label">The label.</param>
    [Theory]
    [InlineData("me")]
    [InlineData("Marketing")]
    [InlineData("Research/Nope")]
    [InlineData("Research/Atlas/deeper")]
    [InlineData("Research/..")]
    [InlineData("../Research")]
    [InlineData("")]
    [InlineData(null)]
    public void TryResolveTeamFolder_OutsideTheTeam_IsRefused(string? label)
    {
        using TeamMemoryHarness harness = new();

        Assert.False(harness.Paths.TryResolveTeamFolder("Research", label, out MemoryFolder? folder));
        Assert.Null(folder);
    }

    /// <summary>
    /// A memory folder that is a junction or link resolves to nothing, for Huddle's writes and for the tool, as the Team Memory index
    /// already skips it: the prompt would show it empty while a write went through to the target.
    /// </summary>
    [Fact]
    public void Resolve_JunctionedMemoryFolder_DoesNotResolve()
    {
        using TeamMemoryHarness harness = new(supportsReparsePoints: true);
        string target = Path.Combine(Path.GetDirectoryName(harness.Folder()) ?? string.Empty, "elsewhere");
        Directory.CreateDirectory(target);
        Directory.CreateDirectory(Path.GetDirectoryName(harness.Folder()) ?? string.Empty);
        CreateLink(harness.Folder(), target);
        try
        {
            Assert.False(harness.Paths.TryResolveTeamFolder("Research", "Research", out _));
            Assert.Null(harness.Paths.ResolveFolder("Leo", "Research"));
            Assert.Equal(["Research/Atlas"], harness.Paths.TeamFolders(harness.Catalog.Teams[0]).Select(static folder => folder.Label));
            Assert.True(harness.Paths.TryResolveTeamFolder("Research", "Research/Atlas", out _));
        }
        finally
        {
            Directory.Delete(harness.Folder(), recursive: false);
        }
    }

    /// <summary>Creates a directory junction on Windows, or a symbolic link elsewhere, from <paramref name="link"/> to <paramref name="target"/>.</summary>
    /// <param name="link">The link to create.</param>
    /// <param name="target">The existing folder it points at.</param>
    private static void CreateLink(string link, string target)
    {
        if (OperatingSystem.IsWindows())
        {
            System.Diagnostics.ProcessStartInfo startInfo = new("cmd")
            {
                ArgumentList = { "/c", "mklink", "/J", link, target },
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo) ?? throw new InvalidOperationException("mklink did not start.");
            process.WaitForExit();
            Assert.Equal(0, process.ExitCode);
        }
        else
        {
            _ = Directory.CreateSymbolicLink(link, target);
        }
    }

    /// <summary>The Team's folders are its own, then each Project's, in the catalog's order.</summary>
    [Fact]
    public void TeamFolders_ListsTheTeamThenItsProjects()
    {
        using TeamMemoryHarness harness = new();

        IReadOnlyList<MemoryFolder> folders = harness.Paths.TeamFolders(harness.Catalog.Teams[0]);

        Assert.Equal(["Research", "Research/Atlas"], folders.Select(static folder => folder.Label));
    }
}
