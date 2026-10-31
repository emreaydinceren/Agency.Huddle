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

    /// <summary>The Team's folders are its own, then each Project's, in the catalog's order.</summary>
    [Fact]
    public void TeamFolders_ListsTheTeamThenItsProjects()
    {
        using TeamMemoryHarness harness = new();

        IReadOnlyList<MemoryFolder> folders = harness.Paths.TeamFolders(harness.Catalog.Teams[0]);

        Assert.Equal(["Research", "Research/Atlas"], folders.Select(static folder => folder.Label));
    }
}
