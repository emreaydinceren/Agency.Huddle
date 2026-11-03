using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Task 128 (13.1.t, Spec §6.4): <c>WorkspaceProbe.StateOf</c> tells a repository (a <c>.git</c> folder or file at the root) from a plain folder, and a missing path is unreadable.</summary>
public sealed class WorkspaceStateTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "huddle-workspace-" + Guid.NewGuid().ToString("N"));

    /// <summary>Removes the temporary folder.</summary>
    public void Dispose()
    {
        if (Directory.Exists(this.root))
        {
            Directory.Delete(this.root, recursive: true);
        }
    }

    /// <summary>A folder with a <c>.git</c> folder at its root is a repository.</summary>
    [Fact]
    public void StateOf_GitFolder_IsRepository()
    {
        Directory.CreateDirectory(Path.Combine(this.root, ".git"));

        Assert.Equal(WorkspaceState.Repository, WorkspaceProbe.StateOf(this.root));
    }

    /// <summary>A <c>.git</c> file (a worktree or submodule pointer) at the root is a repository too.</summary>
    [Fact]
    public void StateOf_GitFile_IsRepository()
    {
        Directory.CreateDirectory(this.root);
        File.WriteAllText(Path.Combine(this.root, ".git"), "gitdir: ../elsewhere");

        Assert.Equal(WorkspaceState.Repository, WorkspaceProbe.StateOf(this.root));
    }

    /// <summary>A readable folder without <c>.git</c> is a plain folder, even with files and a nested <c>.git</c> one level down.</summary>
    [Fact]
    public void StateOf_NoGit_IsFolder()
    {
        Directory.CreateDirectory(Path.Combine(this.root, TeamNames.MemoryFolder, ".git")); // Only the root counts.
        File.WriteAllText(Path.Combine(this.root, "notes.md"), "x");

        Assert.Equal(WorkspaceState.Folder, WorkspaceProbe.StateOf(this.root));
    }

    /// <summary>A path that does not exist is unreadable.</summary>
    [Fact]
    public void StateOf_MissingPath_IsUnreadable()
    {
        Assert.Equal(WorkspaceState.Unreadable, WorkspaceProbe.StateOf(Path.Combine(this.root, "missing")));
    }

    /// <summary>A path that names a file, not a folder, cannot be listed and is unreadable.</summary>
    [Fact]
    public void StateOf_PathIsAFile_IsUnreadable()
    {
        Directory.CreateDirectory(this.root);
        string file = Path.Combine(this.root, "a.txt");
        File.WriteAllText(file, "x");

        Assert.Equal(WorkspaceState.Unreadable, WorkspaceProbe.StateOf(file));
    }
}
