using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Tests for <see cref="TaskLayout"/>: it maps only the `_tasks/` layout (ADR-0030, Spec §6.3).</summary>
public sealed class TaskLayoutTests
{
    /// <summary>A path under Team/_tasks maps to a location with no Project and not Closed.</summary>
    [Fact]
    public void TryMap_TeamTasks_NoProject()
    {
        string root = Path.Combine("root");
        string fullPath = Path.Combine(root, "T", "_tasks", "X.md");

        bool result = TaskLayout.TryMap(root, fullPath, out TaskLocation? location, out string? error);

        Assert.True(result);
        Assert.NotNull(location);
        Assert.Equal("T", location.Team);
        Assert.Null(location.Project);
        Assert.False(location.Closed);
        Assert.Null(error);
    }

    /// <summary>A path under Team/_tasks/_closed maps to a location with no Project and Closed.</summary>
    [Fact]
    public void TryMap_TeamTasksClosed()
    {
        string root = Path.Combine("root");
        string fullPath = Path.Combine(root, "T", "_tasks", "_closed", "X.md");

        bool result = TaskLayout.TryMap(root, fullPath, out TaskLocation? location, out string? error);

        Assert.True(result);
        Assert.NotNull(location);
        Assert.Equal("T", location.Team);
        Assert.Null(location.Project);
        Assert.True(location.Closed);
        Assert.Null(error);
    }

    /// <summary>A path under Team/Project/_tasks maps to a location with a Project and not Closed.</summary>
    [Fact]
    public void TryMap_ProjectTasks()
    {
        string root = Path.Combine("root");
        string fullPath = Path.Combine(root, "T", "P", "_tasks", "X.md");

        bool result = TaskLayout.TryMap(root, fullPath, out TaskLocation? location, out string? error);

        Assert.True(result);
        Assert.NotNull(location);
        Assert.Equal("T", location.Team);
        Assert.Equal("P", location.Project);
        Assert.False(location.Closed);
        Assert.Null(error);
    }

    /// <summary>A path under Team/Project/_tasks/_closed maps to a location with a Project and Closed.</summary>
    [Fact]
    public void TryMap_ProjectTasksClosed()
    {
        string root = Path.Combine("root");
        string fullPath = Path.Combine(root, "T", "P", "_tasks", "_closed", "X.md");

        bool result = TaskLayout.TryMap(root, fullPath, out TaskLocation? location, out string? error);

        Assert.True(result);
        Assert.NotNull(location);
        Assert.Equal("T", location.Team);
        Assert.Equal("P", location.Project);
        Assert.True(location.Closed);
        Assert.Null(error);
    }

    /// <summary>Every path outside the `_tasks/` layout is ignored: TryMap returns false with a null location and null error.</summary>
    [Theory]
    [InlineData("x.md")]
    [InlineData("T/note.md")]
    [InlineData("T/X.md")]
    [InlineData("T/_closed/X.md")]
    [InlineData("T/P/plan.md")]
    [InlineData("T/P/research/d.md")]
    [InlineData("T/_tasks/sub/X.md")]
    [InlineData("T/_tasks/_closed/_closed/X.md")]
    [InlineData("T/P/Q/_tasks/X.md")]
    [InlineData("_tasks/X.md")]
    [InlineData("_x/_tasks/X.md")]
    [InlineData(".obsidian/_tasks/X.md")]
    [InlineData("T/_drafts/_tasks/X.md")]
    [InlineData("Business/memory/_tasks/X.md")]
    [InlineData("Business/Memory/_tasks/X.md")]
    [InlineData("Business/MEMORY/_tasks/X.md")]
    [InlineData("Business/memory/_tasks/_closed/X.md")]
    [InlineData("Business/MEMORY/_tasks/_closed/X.md")]
    public void TryMap_NotATaskPath_IsIgnored(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        string root = Path.Combine("root");
        string fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));

        bool result = TaskLayout.TryMap(root, fullPath, out TaskLocation? location, out string? error);

        Assert.False(result);
        Assert.Null(location);
        Assert.Null(error);
    }

    /// <summary>A Project named <c>memory</c> in any case is never a Project (ADR-0032, Spec §6.3), so
    /// its <c>_tasks</c> is not mapped on any OS; the neighbours <c>memory-notes</c> and
    /// <c>memories</c> stay valid Projects, and a Team folder named <c>memory</c> is still a Team.</summary>
    [Theory]
    [InlineData("Business/memory-notes/_tasks/X.md", "Business", "memory-notes", false)]
    [InlineData("Business/memory-notes/_tasks/_closed/X.md", "Business", "memory-notes", true)]
    [InlineData("Business/memories/_tasks/X.md", "Business", "memories", false)]
    [InlineData("Business/Marketing/_tasks/X.md", "Business", "Marketing", false)]
    [InlineData("Business/_tasks/X.md", "Business", null, false)]
    [InlineData("memory/_tasks/X.md", "memory", null, false)]
    [InlineData("memory/_tasks/_closed/X.md", "memory", null, true)]
    [InlineData("memory/Marketing/_tasks/X.md", "memory", "Marketing", false)]
    public void TryMap_MemoryNeighboursAndTeamLevel_StillMap(string relativePath, string team, string? project, bool closed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        string root = Path.Combine("root");
        string fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));

        bool result = TaskLayout.TryMap(root, fullPath, out TaskLocation? location, out string? error);

        Assert.True(result);
        Assert.NotNull(location);
        Assert.Equal(team, location.Team);
        Assert.Equal(project, location.Project);
        Assert.Equal(closed, location.Closed);
        Assert.Null(error);
    }

    /// <summary>The `_tasks` folder name follows <c>FolderSnapshot.PathComparer</c>: it maps case-insensitively on Windows/macOS and is ignored elsewhere.</summary>
    [Fact]
    public void TryMap_TasksFolderCase_FollowsPathComparer()
    {
        string root = Path.Combine("root");
        string fullPath = Path.Combine(root, "T", "_Tasks", "X.md");

        bool result = TaskLayout.TryMap(root, fullPath, out TaskLocation? location, out string? error);

        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            Assert.True(result);
            Assert.NotNull(location);
            Assert.Equal("T", location.Team);
            Assert.Null(location.Project);
            Assert.False(location.Closed);
        }
        else
        {
            Assert.False(result);
            Assert.Null(location);
        }

        Assert.Null(error);
    }

    /// <summary>PathFor generates the correct file path for a task location with no Project and not Closed.</summary>
    [Fact]
    public void PathFor_TeamLevel_NoProject()
    {
        string root = Path.Combine("root");
        TaskLocation location = new TaskLocation("T", null, false);
        TaskId id = new TaskId("PLAT", 1);

        string path = TaskLayout.PathFor(root, location, id);

        string expected = Path.Combine(root, "T", "_tasks", "PLAT-0001.md");
        Assert.Equal(expected, path);
    }

    /// <summary>PathFor generates the correct file path for a task location with no Project and Closed.</summary>
    [Fact]
    public void PathFor_TeamClosedFolder_NoProject()
    {
        string root = Path.Combine("root");
        TaskLocation location = new TaskLocation("T", null, true);
        TaskId id = new TaskId("PLAT", 1);

        string path = TaskLayout.PathFor(root, location, id);

        string expected = Path.Combine(root, "T", "_tasks", "_closed", "PLAT-0001.md");
        Assert.Equal(expected, path);
    }

    /// <summary>PathFor generates the correct file path for a task location with a Project and not Closed.</summary>
    [Fact]
    public void PathFor_ProjectLevel_NoClosedFolder()
    {
        string root = Path.Combine("root");
        TaskLocation location = new TaskLocation("T", "P", false);
        TaskId id = new TaskId("PLAT", 1);

        string path = TaskLayout.PathFor(root, location, id);

        string expected = Path.Combine(root, "T", "P", "_tasks", "PLAT-0001.md");
        Assert.Equal(expected, path);
    }

    /// <summary>PathFor generates the correct file path for a task location with a Project and Closed.</summary>
    [Fact]
    public void PathFor_ProjectClosedFolder_WithProject()
    {
        string root = Path.Combine("root");
        TaskLocation location = new TaskLocation("T", "P", true);
        TaskId id = new TaskId("PLAT", 1);

        string path = TaskLayout.PathFor(root, location, id);

        string expected = Path.Combine(root, "T", "P", "_tasks", "_closed", "PLAT-0001.md");
        Assert.Equal(expected, path);
    }

    /// <summary>PathFor and TryMap are inverse operations for a valid Team/Project/_tasks/_closed location.</summary>
    [Fact]
    public void PathFor_RoundTrip()
    {
        string root = Path.Combine("root");
        TaskLocation location = new TaskLocation("T", "P", true);
        TaskId id = new TaskId("PLAT", 1);

        string path = TaskLayout.PathFor(root, location, id);

        bool result = TaskLayout.TryMap(root, path, out TaskLocation? mappedLocation, out string? error);

        Assert.True(result);
        Assert.NotNull(mappedLocation);
        Assert.Equal(location, mappedLocation);
        Assert.Null(error);
    }

    /// <summary>AffectsTasks tells a Task file, a Team folder, a Project folder and a `_tasks`/`_closed` folder from an unrelated note, its research subtree, hidden folders and a temp file.</summary>
    [Theory]
    [InlineData("T/_tasks/PLAT-0001.md", true)]
    [InlineData("T", true)]
    [InlineData("T/P", true)]
    [InlineData("T/_tasks", true)]
    [InlineData("T/P/_tasks/_closed", true)]
    [InlineData("T/v1.2", true)]
    [InlineData("T/note.md", false)]
    [InlineData("T/P/research/x.md", false)]
    [InlineData("T/P/research", false)]
    [InlineData(".obsidian/w.json", false)]
    [InlineData("T/_tasks/X.md.tmp", false)]
    public void AffectsTasks(string relativePath, bool expected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        string root = Path.Combine("root");
        string fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));

        bool result = TaskLayout.AffectsTasks(root, fullPath);

        Assert.Equal(expected, result);
    }

    /// <summary>IsReservedFolderName treats both `_`- and `.`-prefixed names as reserved; an ordinary name, even with a space, is not.</summary>
    [Theory]
    [InlineData("_x", true)]
    [InlineData(".git", true)]
    [InlineData("Launch Q4", false)]
    public void IsReservedFolderName(string name, bool expected)
    {
        bool result = TaskLayout.IsReservedFolderName(name);

        Assert.Equal(expected, result);
    }
}
