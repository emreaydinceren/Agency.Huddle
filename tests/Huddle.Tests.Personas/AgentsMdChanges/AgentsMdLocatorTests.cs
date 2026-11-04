using Agency.Huddle.App.Acp;
using Agency.Huddle.App.AgentsMdChanges;

namespace Agency.Huddle.Tests.AgentsMdChanges;

/// <summary>
/// Pins <see cref="AgentsMdLocator"/> (Spec §6.1, §8.3): every level's path in composition order, whether or not
/// the file exists, with a relative path that uses <c>/</c> on every OS.
/// </summary>
public sealed class AgentsMdLocatorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "huddle-locator-" + Guid.NewGuid().ToString("N"));

    private string TeamsRoot => Path.Combine(this.root, "Teams");

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(this.root))
        {
            Directory.Delete(this.root, recursive: true);
        }
    }

    /// <summary>Creates a folder under the Teams root.</summary>
    /// <param name="segments">The folder segments below the Teams root.</param>
    private void Folder(params string[] segments) =>
        Directory.CreateDirectory(Path.Combine([this.TeamsRoot, .. segments]));

    /// <summary>Huddle-wide comes first, then each Team in the order given, each followed by its Projects sorted ordinally.</summary>
    [Fact]
    public void Enumerate_HuddleWideThenTeamThenProjectsSortedOrdinally()
    {
        this.Folder("B");
        this.Folder("A", "Zeta");
        this.Folder("A", "Alpha");
        this.Folder("A", "beta");

        AgentsMdFile[] files = AgentsMdLocator.Enumerate(this.TeamsRoot, ["A", "B"]).ToArray();

        Assert.Equal(
            [AgentsMdLevel.HuddleWide, AgentsMdLevel.Team, AgentsMdLevel.Project, AgentsMdLevel.Project, AgentsMdLevel.Project, AgentsMdLevel.Team],
            files.Select(static file => file.Level).ToArray());
        Assert.Equal(
            ["AGENTS.md", "A/AGENTS.md", "A/Alpha/AGENTS.md", "A/Zeta/AGENTS.md", "A/beta/AGENTS.md", "B/AGENTS.md"],
            files.Select(static file => file.RelativePath).ToArray());
        Assert.Equal([null, "A", "A", "A", "A", "B"], files.Select(static file => file.Team).ToArray());
        Assert.Equal([null, null, "Alpha", "Zeta", "beta", null], files.Select(static file => file.Project).ToArray());
    }

    /// <summary>A level whose file does not exist is still yielded, with the full path it would have.</summary>
    [Fact]
    public void Enumerate_YieldsALevelWithNoFile()
    {
        this.Folder("A");

        AgentsMdFile[] files = AgentsMdLocator.Enumerate(this.TeamsRoot, ["A"]).ToArray();

        Assert.Equal(2, files.Length);
        Assert.Equal(Path.Combine(this.TeamsRoot, "AGENTS.md"), files[0].FullPath);
        Assert.Equal(Path.Combine(this.TeamsRoot, "A", "AGENTS.md"), files[1].FullPath);
        Assert.False(File.Exists(files[1].FullPath));
    }

    /// <summary>Reserved and underscore-prefixed folders are not Projects.</summary>
    [Fact]
    public void Enumerate_SkipsReservedFolders()
    {
        this.Folder("A", "Real");
        this.Folder("A", "raw");
        this.Folder("A", "memory");
        this.Folder("A", "outputs");
        this.Folder("A", "_x");

        AgentsMdFile[] files = AgentsMdLocator.Enumerate(this.TeamsRoot, ["A"]).ToArray();

        Assert.Equal(
            ["AGENTS.md", "A/AGENTS.md", "A/Real/AGENTS.md"],
            files.Select(static file => file.RelativePath).ToArray());
    }

    /// <summary>A Team named twice, differing only in case, is yielded once, as first spelled.</summary>
    [Fact]
    public void Enumerate_DuplicateTeamIgnoringCase_Once()
    {
        AgentsMdFile[] files = AgentsMdLocator.Enumerate(this.TeamsRoot, ["A", "a"]).ToArray();

        Assert.Equal(
            ["AGENTS.md", "A/AGENTS.md"],
            files.Select(static file => file.RelativePath).ToArray());
    }

    /// <summary>The relative path never holds a backslash, whatever the OS separator is.</summary>
    [Fact]
    public void RelativePath_UsesForwardSlashes()
    {
        this.Folder("A", "P");

        AgentsMdFile[] files = AgentsMdLocator.Enumerate(this.TeamsRoot, ["A"]).ToArray();

        AgentsMdFile project = files[^1];
        Assert.Equal("A/P/AGENTS.md", project.RelativePath);
        Assert.DoesNotContain('\\', project.RelativePath);
    }
}
