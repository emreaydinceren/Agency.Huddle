using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Tests for TaskLayout path mapping and file path generation.</summary>
public sealed class TaskLayoutTests
{
    /// <summary>A path directly under a Team folder maps to a location with no Project and not Closed.</summary>
    [Fact]
    public void TryMap_TeamLevel_NoProject()
    {
        string root = Path.Combine("root");
        string fullPath = Path.Combine(root, "T", "x.md");

        bool result = TaskLayout.TryMap(root, fullPath, out TaskLocation? location, out string? error);

        Assert.True(result);
        Assert.NotNull(location);
        Assert.Equal("T", location.Team);
        Assert.Null(location.Project);
        Assert.False(location.Closed);
        Assert.Null(error);
    }

    /// <summary>A path under Team/_closed folder maps to a location with no Project and Closed.</summary>
    [Fact]
    public void TryMap_TeamClosedFolder_NoProject()
    {
        string root = Path.Combine("root");
        string fullPath = Path.Combine(root, "T", "_closed", "x.md");

        bool result = TaskLayout.TryMap(root, fullPath, out TaskLocation? location, out string? error);

        Assert.True(result);
        Assert.NotNull(location);
        Assert.Equal("T", location.Team);
        Assert.Null(location.Project);
        Assert.True(location.Closed);
        Assert.Null(error);
    }

    /// <summary>A path under Team/Project folder maps to a location with a Project and not Closed.</summary>
    [Fact]
    public void TryMap_ProjectLevel_NoClosedFolder()
    {
        string root = Path.Combine("root");
        string fullPath = Path.Combine(root, "T", "P", "x.md");

        bool result = TaskLayout.TryMap(root, fullPath, out TaskLocation? location, out string? error);

        Assert.True(result);
        Assert.NotNull(location);
        Assert.Equal("T", location.Team);
        Assert.Equal("P", location.Project);
        Assert.False(location.Closed);
        Assert.Null(error);
    }

    /// <summary>A path under Team/Project/_closed folder maps to a location with a Project and Closed.</summary>
    [Fact]
    public void TryMap_ProjectClosedFolder_WithProject()
    {
        string root = Path.Combine("root");
        string fullPath = Path.Combine(root, "T", "P", "_closed", "x.md");

        bool result = TaskLayout.TryMap(root, fullPath, out TaskLocation? location, out string? error);

        Assert.True(result);
        Assert.NotNull(location);
        Assert.Equal("T", location.Team);
        Assert.Equal("P", location.Project);
        Assert.True(location.Closed);
        Assert.Null(error);
    }

    /// <summary>A path at the root directory level is not inside a Team folder and is rejected.</summary>
    [Fact]
    public void TryMap_AtRoot_ErrorNotInsideTeamFolder()
    {
        string root = Path.Combine("root");
        string fullPath = Path.Combine(root, "x.md");

        bool result = TaskLayout.TryMap(root, fullPath, out TaskLocation? location, out string? error);

        Assert.False(result);
        Assert.Null(location);
        Assert.Equal("is not inside a Team folder", error);
    }

    /// <summary>A path nested too deeply (more than Team/Project/_closed) is rejected.</summary>
    [Fact]
    public void TryMap_NestedTooDeep_Error()
    {
        string root = Path.Combine("root");
        string fullPath = Path.Combine(root, "T", "P", "Q", "x.md");

        bool result = TaskLayout.TryMap(root, fullPath, out TaskLocation? location, out string? error);

        Assert.False(result);
        Assert.Null(location);
        Assert.Equal("is nested too deeply; Tasks live at Team/[Project/][_closed/]", error);
    }

    /// <summary>A path with _closed/_closed is nested too deeply and is rejected.</summary>
    [Fact]
    public void TryMap_ClosedClosedFolder_Error()
    {
        string root = Path.Combine("root");
        string fullPath = Path.Combine(root, "T", "_closed", "_closed", "x.md");

        bool result = TaskLayout.TryMap(root, fullPath, out TaskLocation? location, out string? error);

        Assert.False(result);
        Assert.Null(location);
        Assert.Equal("is nested too deeply; Tasks live at Team/[Project/][_closed/]", error);
    }

    /// <summary>A path under a reserved underscore folder like _drafts is ignored and returns false with no error.</summary>
    [Fact]
    public void TryMap_ReservedDraftsFolder_IgnoredNoError()
    {
        string root = Path.Combine("root");
        string fullPath = Path.Combine(root, "T", "_drafts", "x.md");

        bool result = TaskLayout.TryMap(root, fullPath, out TaskLocation? location, out string? error);

        Assert.False(result);
        Assert.Null(location);
        Assert.Null(error);
    }

    /// <summary>A path under a reserved folder at project level is ignored, not rejected as nested too deeply.</summary>
    [Fact]
    public void TryMap_ReservedProjectLevel_IgnoredNoError()
    {
        string root = Path.Combine("root");
        string fullPath = Path.Combine(root, "Platform", "Auth", "_drafts", "x.md");

        bool result = TaskLayout.TryMap(root, fullPath, out TaskLocation? location, out string? error);

        Assert.False(result);
        Assert.Null(location);
        Assert.Null(error);
    }

    /// <summary>A path with reserved folder multiple levels deep is ignored, not rejected as nested too deeply.</summary>
    [Fact]
    public void TryMap_ReservedDeeplyNested_IgnoredNoError()
    {
        string root = Path.Combine("root");
        string fullPath = Path.Combine(root, "Platform", "Auth", "_drafts", "deep", "x.md");

        bool result = TaskLayout.TryMap(root, fullPath, out TaskLocation? location, out string? error);

        Assert.False(result);
        Assert.Null(location);
        Assert.Null(error);
    }

    /// <summary>A reserved folder at the team level is ignored.</summary>
    [Fact]
    public void TryMap_ReservedTeamFolder_IgnoredNoError()
    {
        string root = Path.Combine("root");
        string fullPath = Path.Combine(root, "_x", "P", "y.md");

        bool result = TaskLayout.TryMap(root, fullPath, out TaskLocation? location, out string? error);

        Assert.False(result);
        Assert.Null(location);
        Assert.Null(error);
    }

    /// <summary>A path with _closed/_closed is nested too deeply (not a reserved folder issue).</summary>
    [Fact]
    public void TryMap_ClosedClosedStillNested_Error()
    {
        string root = Path.Combine("root");
        string fullPath = Path.Combine(root, "T", "_closed", "_closed", "x.md");

        bool result = TaskLayout.TryMap(root, fullPath, out TaskLocation? location, out string? error);

        Assert.False(result);
        Assert.Null(location);
        Assert.Equal("is nested too deeply; Tasks live at Team/[Project/][_closed/]", error);
    }

    /// <summary>PathFor generates the correct file path for a task location with no Project and not Closed.</summary>
    [Fact]
    public void PathFor_TeamLevel_NoProject()
    {
        string root = Path.Combine("root");
        TaskLocation location = new TaskLocation("T", null, false);
        TaskId id = new TaskId("PLAT", 1);

        string path = TaskLayout.PathFor(root, location, id);

        string expected = Path.Combine(root, "T", "PLAT-0001.md");
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

        string expected = Path.Combine(root, "T", "_closed", "PLAT-0001.md");
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

        string expected = Path.Combine(root, "T", "P", "PLAT-0001.md");
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

        string expected = Path.Combine(root, "T", "P", "_closed", "PLAT-0001.md");
        Assert.Equal(expected, path);
    }

    /// <summary>PathFor and TryMap are inverse operations for a valid Team/Project/_closed location.</summary>
    [Fact]
    public void PathFor_RoundTrip_WithProjectAndClosed()
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
}
