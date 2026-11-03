using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for the three Second Brain roots of <see cref="LibraryRootStore"/>: <c>raw</c>, <c>archive</c> (read-only)
/// and <c>prompts</c>, which replace the former <c>uploads</c> root.
/// </summary>
public sealed class SecondBrainRootsTests
{
    /// <summary>The Raw root is a writable built-in named "Raw" at <c>{DataDir}/Raw</c>.</summary>
    [Fact]
    public void Roots_Default_IncludeRaw()
    {
        using TempDataDir dataDir = new();

        LibraryRoot raw = Find(dataDir, "raw");

        Assert.Equal("Raw", raw.DisplayName);
        Assert.Equal(LibraryRootKind.Raw, raw.Kind);
        Assert.False(raw.ReadOnly);
        Assert.Equal(Path.GetFullPath(Path.Combine(dataDir.Path, "Raw")), Path.GetFullPath(raw.FullPath));
    }

    /// <summary>The Archive root is a read-only built-in named "Archive" at <c>{DataDir}/Archive</c>.</summary>
    [Fact]
    public void Roots_Default_IncludeArchiveReadOnly()
    {
        using TempDataDir dataDir = new();

        LibraryRoot archive = Find(dataDir, "archive");

        Assert.Equal("Archive", archive.DisplayName);
        Assert.Equal(LibraryRootKind.Archive, archive.Kind);
        Assert.True(archive.ReadOnly);
        Assert.Equal(Path.GetFullPath(Path.Combine(dataDir.Path, "Archive")), Path.GetFullPath(archive.FullPath));
    }

    /// <summary>The Prompts root is a writable built-in named "Prompts" at <c>{DataDir}/Prompts</c>.</summary>
    [Fact]
    public void Roots_Default_IncludePrompts()
    {
        using TempDataDir dataDir = new();

        LibraryRoot prompts = Find(dataDir, "prompts");

        Assert.Equal("Prompts", prompts.DisplayName);
        Assert.Equal(LibraryRootKind.Prompts, prompts.Kind);
        Assert.False(prompts.ReadOnly);
        Assert.Equal(Path.GetFullPath(Path.Combine(dataDir.Path, "Prompts")), Path.GetFullPath(prompts.FullPath));
    }

    /// <summary>No root keeps the retired <c>uploads</c> id.</summary>
    [Fact]
    public void Roots_Default_HaveNoUploadsRoot()
    {
        using TempDataDir dataDir = new();
        LibraryRootStore store = new(dataDir.Options(), new AgentPaths(dataDir.Options()), NullLogger<LibraryRootStore>.Instance);

        Assert.DoesNotContain(store.Roots, r => string.Equals(r.Id, "uploads", StringComparison.Ordinal));
    }

    /// <summary>A pinned root named like a Second Brain built-in cannot take the built-in id, so it gets a different slug.</summary>
    [Theory]
    [InlineData("Raw", "raw")]
    [InlineData("Archive", "archive")]
    [InlineData("Prompts", "prompts")]
    public void Slug_BuiltInName_IsReserved(string name, string reserved)
    {
        string slug = LibraryRootStore.Slug(name, []);

        Assert.NotEqual(reserved, slug);
    }

    /// <summary>A Team with a readable workspace adds a writable <c>Pinned</c> root <c>workspace-business</c> named "Business workspace" at that folder.</summary>
    [Fact]
    public void Roots_TeamWithReadableWorkspace_AddsWorkspaceRoot()
    {
        using TempDataDir dataDir = new();
        string workspace = MakeFolder(dataDir, "biz");
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", workspace)] };

        LibraryRootStore store = Store(dataDir, catalog);

        // The id is "workspace-" plus the slug of the Team name ("Business" -> "business"); the name is "<Team> workspace".
        LibraryRoot root = Assert.Single(store.Roots, r => string.Equals(r.Id, "workspace-business", StringComparison.Ordinal));
        Assert.Equal("Business workspace", root.DisplayName);
        Assert.Equal(LibraryRootKind.Workspace, root.Kind);
        Assert.False(root.ReadOnly);
        Assert.Equal(Path.GetFullPath(workspace), Path.GetFullPath(root.FullPath));
    }

    /// <summary>A Team whose workspace folder is missing, and a Team with no workspace, add no root.</summary>
    [Fact]
    public void Roots_UnreadableOrAbsentWorkspace_AddsNone()
    {
        using TempDataDir dataDir = new();
        string missing = Path.Combine(dataDir.Path, "gone");
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", missing), Team("Sales", null)] };

        LibraryRootStore store = Store(dataDir, catalog);

        Assert.DoesNotContain(store.Roots, r => r.Id.StartsWith("workspace-", StringComparison.Ordinal));
    }

    /// <summary>Two Teams with readable workspaces add two roots, each with its own id.</summary>
    [Fact]
    public void Roots_TwoTeams_AddTwoRoots()
    {
        using TempDataDir dataDir = new();
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", MakeFolder(dataDir, "a")), Team("Sales & Ops", MakeFolder(dataDir, "b"))] };

        LibraryRootStore store = Store(dataDir, catalog);

        // "Sales & Ops" slugs to "sales-ops".
        string[] ids = [.. store.Roots.Where(r => r.Id.StartsWith("workspace-", StringComparison.Ordinal)).Select(r => r.Id)];
        Assert.Equal(["workspace-business", "workspace-sales-ops"], ids);
    }

    /// <summary>When the Team list changes, the workspace roots are re-derived and <c>RootsChanged</c> fires, as it does for a Skill Sources change.</summary>
    [Fact]
    public void Roots_CatalogChanged_RefreshesWorkspaceRootsAndRaises()
    {
        using TempDataDir dataDir = new();
        FakeTeamCatalog catalog = new();
        LibraryRootStore store = Store(dataDir, catalog);
        int raised = 0;
        store.RootsChanged += () => raised++;
        Assert.DoesNotContain(store.Roots, r => r.Id.StartsWith("workspace-", StringComparison.Ordinal));

        catalog.Teams = [Team("Business", MakeFolder(dataDir, "biz"))];
        catalog.Raise();

        Assert.Contains(store.Roots, r => string.Equals(r.Id, "workspace-business", StringComparison.Ordinal));
        Assert.Equal(1, raised);
    }

    /// <summary>A workspace root is derived, not pinned: it is not in <c>Pinned</c>, so a Settings save cannot persist it.</summary>
    [Fact]
    public void Pinned_WithWorkspaceRoot_DoesNotListIt()
    {
        using TempDataDir dataDir = new();
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", MakeFolder(dataDir, "biz"))] };

        LibraryRootStore store = Store(dataDir, catalog);

        Assert.Empty(store.Pinned);
    }

    private static LibraryRootStore Store(TempDataDir dataDir, FakeTeamCatalog catalog) =>
        new(dataDir.Options(), new AgentPaths(dataDir.Options()), NullLogger<LibraryRootStore>.Instance, null, catalog);

    private static string MakeFolder(TempDataDir dataDir, string name)
    {
        string path = Path.Combine(dataDir.Path, "workspaces", name);
        _ = Directory.CreateDirectory(path);
        return path;
    }

    private static TeamSummary Team(string name, string? workspace) =>
        new(name, [], ["Nova"], true) { Workspace = workspace };

    private static LibraryRoot Find(TempDataDir dataDir, string id)
    {
        LibraryRootStore store = new(dataDir.Options(), new AgentPaths(dataDir.Options()), NullLogger<LibraryRootStore>.Instance);

        return Assert.Single(store.Roots, r => string.Equals(r.Id, id, StringComparison.Ordinal));
    }
}
