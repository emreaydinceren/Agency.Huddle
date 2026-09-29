using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for <see cref="LibraryPathResolver"/>: the Library's path boundary (Spec §6.1, steps 1-7)
/// as a data table, plus the link, pinned-root-overlap and role-resolution rows corrections-B3 4.2.t
/// adds (items 17-21, 23) and the <c>TryResolveChild</c> entry point (Addendum A2).
/// </summary>
public sealed class LibraryPathResolverTests
{
    /// <summary>The plan's boundary table (Spec §6.1): root lookup, segment refusals, canonicalisation, case-insensitive matching and role assignment.</summary>
    [Theory]
    [InlineData("teams", "", "", "Root", null)]
    [InlineData("teams", "Marketing", "Marketing", "TeamFolder", null)]
    [InlineData("teams", "Marketing/Launch Q4", "Marketing/Launch Q4", "ProjectFolder", null)]
    [InlineData("teams", "Marketing/memory", "Marketing/memory", "Folder", null)]
    [InlineData("teams", "Marketing/MEMORY", "Marketing/MEMORY", "Folder", null)]
    [InlineData("teams", "Marketing/Launch Q4/memory", "Marketing/Launch Q4/memory", "Folder", null)]
    [InlineData("teams", "memory", "memory", "TeamFolder", null)]
    [InlineData("teams", "memory/memory", "memory/memory", "Folder", null)]
    [InlineData("teams", "memory/Launch", "memory/Launch", "ProjectFolder", null)]
    [InlineData("teams", "Marketing/memory-notes", "Marketing/memory-notes", "ProjectFolder", null)]
    [InlineData("teams", "Marketing/Launch Q4/plan.md", "Marketing/Launch Q4/plan.md", "File", null)]
    [InlineData("teams", "Marketing\\Launch Q4\\plan.md", "Marketing/Launch Q4/plan.md", "File", null)]
    [InlineData("teams", "Marketing/_tasks/MKT-0001.md", null, null, "That folder is reserved.")]
    [InlineData("teams", "../Teammates/Nova/Nova.md", null, null, "That path isn't inside the Library.")]
    [InlineData("teams", "Marketing/../../x", null, null, "That path isn't inside the Library.")]
    [InlineData("teams", "./Marketing", null, null, "That path isn't inside the Library.")]
    [InlineData("teams", "Marketing//plan.md", null, null, "That path isn't inside the Library.")]
    [InlineData("teams", "Marketing./x", null, null, "That path isn't inside the Library.")]
    [InlineData("teammates", "Nova", "Nova", "TeammateFolder", null)]
    [InlineData("teammates", "Nova/Nova.md", "Nova/Nova.md", "TeammateDefinition", null)]
    [InlineData("teammates", "Nova/work", "Nova/work", "WorkDir", null)]
    [InlineData("teammates", "Nova/work/memory/x.md", "Nova/work/memory/x.md", "File", null)]
    [InlineData("vault", "../Vault-evil/secret.md", null, null, "That path isn't inside the Library.")]
    [InlineData("nope", "x", null, null, "Unknown Library root 'nope'.")]
    [InlineData("teams", "C:\\x", null, null, "That path isn't inside the Library.")]
    [InlineData("teams", "C:x", null, null, "That path isn't inside the Library.")]
    [InlineData("teams", "\\\\server\\share\\x", null, null, "That path isn't inside the Library.")]
    public void TryResolve_Table(string rootId, string input, string? expectedRelative, string? expectedRole, string? expectedError)
    {
        using Fixture fixture = BuildFixture();

        bool resolved = fixture.Resolver.TryResolve(rootId, input, out LibraryPath? path, out string? error);

        if (expectedError is not null)
        {
            Assert.False(resolved);
            Assert.Equal(expectedError, error);
        }
        else
        {
            Assert.True(resolved);
            Assert.NotNull(path);
            Assert.Equal(expectedRelative, path.RelativePath);
            Assert.NotNull(expectedRole);
            Assert.Equal(Enum.Parse<LibraryNodeRole>(expectedRole), path.Role);
        }
    }

    /// <summary>Case-insensitive matching resolves the input, keeping its own casing as the relative path (corrections-B3 item 21: assert relative path and role only).</summary>
    [Fact]
    public void TryResolve_CaseInsensitiveSegments_ResolvesWithGivenCasingAndFileRole()
    {
        if (!Agency.Huddle.App.FileChanges.FolderSnapshot.PathComparer.Equals("a", "A"))
        {
            Assert.Skip("On a case-sensitive file system, a differently cased path names a different (missing) file.");
            return;
        }

        using Fixture fixture = BuildFixture();

        bool resolved = fixture.Resolver.TryResolve("teams", "marketing/launch q4/PLAN.md", out LibraryPath? path, out string? error);

        Assert.True(resolved);
        Assert.NotNull(path);
        Assert.Equal("marketing/launch q4/PLAN.md", path.RelativePath);
        Assert.Equal(LibraryNodeRole.File, path.Role);
    }

    /// <summary>A pinned root at <c>DataDir</c>'s parent still refuses a reserved sub-folder reached through it.</summary>
    [Fact]
    public void TryResolve_PinnedRootContainingDataDir_RefusesReserved()
    {
        using Fixture fixture = BuildFixture(new PinnedRootOption { Name = "Parent", Path = "PLACEHOLDER" });
        fixture.SetParentRootPath();

        bool resolved = fixture.Resolver.TryResolve("parent", "App_Data/rooms/r.jsonl", out LibraryPath? path, out string? error);

        Assert.False(resolved);
        Assert.Null(path);
        Assert.Equal("That folder is reserved.", error);
    }

    /// <summary>Item 19: a Team folder's <c>_tasks</c> reached through a pinned root above <c>DataDir</c> is still reserved - the role follows the resolved location, not the root the caller named (Addendum A2's "resolved location" rule).</summary>
    [Fact]
    public void TryResolve_PinnedRootAboveDataDir_TeamsTasksFolder_Refused()
    {
        using Fixture fixture = BuildFixture(new PinnedRootOption { Name = "Parent", Path = "PLACEHOLDER" });
        fixture.SetParentRootPath();

        bool resolved = fixture.Resolver.TryResolve("parent", "App_Data/Teams/Marketing/_tasks/MKT-0001.md", out LibraryPath? path, out string? error);

        Assert.False(resolved);
        Assert.Null(path);
        Assert.Equal("That folder is reserved.", error);
    }

    /// <summary>Item 19: a Teammate definition reached through a pinned root above <c>DataDir</c> still gets the Teammates root's role.</summary>
    [Fact]
    public void TryResolve_PinnedRootAboveDataDir_TeammateDefinition_ResolvesRole()
    {
        using Fixture fixture = BuildFixture(new PinnedRootOption { Name = "Parent", Path = "PLACEHOLDER" });
        fixture.SetParentRootPath();

        bool resolved = fixture.Resolver.TryResolve("parent", "App_Data/Teammates/Nova/Nova.md", out LibraryPath? path, out string? error);

        Assert.True(resolved);
        Assert.NotNull(path);
        Assert.Null(error);
        Assert.Equal(LibraryNodeRole.TeammateDefinition, path.Role);
    }

    /// <summary>A pinned root at DataDir's parent resolves DataDir itself as a Folder without crashing.</summary>
    [Fact]
    public void TryResolve_PinnedParent_DataDirItself_ResolvesAsFolder()
    {
        using Fixture fixture = BuildFixture(new PinnedRootOption { Name = "Parent", Path = "PLACEHOLDER" });
        fixture.SetParentRootPath();

        bool resolved = fixture.Resolver.TryResolve("parent", "App_Data", out LibraryPath? path, out string? error);

        Assert.True(resolved);
        Assert.NotNull(path);
        Assert.Null(error);
        Assert.Equal(LibraryNodeRole.Folder, path.Role);
    }

    /// <summary>Item 19: a pinned root planted directly at a Team's folder still refuses its <c>_tasks</c> sub-folder.</summary>
    [Fact]
    public void TryResolve_PinnedRootAtTeamFolder_TasksSubfolder_Refused()
    {
        using Fixture fixture = BuildFixture(new PinnedRootOption { Name = "Marketing", Path = "PLACEHOLDER" });
        Directory.CreateDirectory(Path.Combine(fixture.DataDir, "Teams", "Marketing"));
        fixture.SetPinnedRootPath("Marketing", Path.Combine(fixture.DataDir, "Teams", "Marketing"));

        bool resolved = fixture.Resolver.TryResolve("marketing", "_tasks/x.md", out LibraryPath? path, out string? error);

        Assert.False(resolved);
        Assert.Null(path);
        Assert.Equal("That folder is reserved.", error);
    }

    /// <summary>Item 20: an existing file at depth 1 under Teams is a plain <c>File</c>, not a Team folder.</summary>
    [Fact]
    public void TryResolve_TeamsReadme_IsFile()
    {
        using Fixture fixture = BuildFixture();

        bool resolved = fixture.Resolver.TryResolve("teams", "readme.md", out LibraryPath? path, out string? error);

        Assert.True(resolved);
        Assert.NotNull(path);
        Assert.Null(error);
        Assert.Equal(LibraryNodeRole.File, path.Role);
    }

    /// <summary>Item 20: any <c>_</c>-prefixed segment under Teams is reserved, not only <c>_tasks</c>.</summary>
    [Fact]
    public void TryResolve_TeamsUnderscoreNotesFile_Reserved()
    {
        using Fixture fixture = BuildFixture();
        Directory.CreateDirectory(Path.Combine(fixture.DataDir, "Teams", "Marketing"));
        File.WriteAllText(Path.Combine(fixture.DataDir, "Teams", "Marketing", "_notes.md"), string.Empty);

        bool resolved = fixture.Resolver.TryResolve("teams", "Marketing/_notes.md", out LibraryPath? path, out string? error);

        Assert.False(resolved);
        Assert.Null(path);
        Assert.Equal("That folder is reserved.", error);
    }

    /// <summary>Item 20: an ordinary file under a Teammate folder (not the definition, not the Work Dir) is a plain <c>File</c>.</summary>
    [Fact]
    public void TryResolve_TeammatesOrdinaryFile_IsFile()
    {
        using Fixture fixture = BuildFixture();

        bool resolved = fixture.Resolver.TryResolve("teammates", "Nova/notes.md", out LibraryPath? path, out string? error);

        Assert.True(resolved);
        Assert.NotNull(path);
        Assert.Null(error);
        Assert.Equal(LibraryNodeRole.File, path.Role);
    }

    /// <summary>Item 20: an underscore-prefixed folder directly under Teammates is an ordinary <c>Folder</c> (the Teams underscore rule does not apply here).</summary>
    [Fact]
    public void TryResolve_TeammatesUnderscoreFolder_IsFolder()
    {
        using Fixture fixture = BuildFixture();

        bool resolved = fixture.Resolver.TryResolve("teammates", "_unsorted", out LibraryPath? path, out string? error);

        Assert.True(resolved);
        Assert.NotNull(path);
        Assert.Null(error);
        Assert.Equal(LibraryNodeRole.Folder, path.Role);
    }

    /// <summary>Item 20: the definition file's name matches its Teammate folder's name case-insensitively
    /// (<c>vega/VEGA.md</c>). Both exist on disk exactly as spelled, so this holds on case-sensitive file
    /// systems too.</summary>
    [Fact]
    public void TryResolve_TeammatesCaseInsensitiveDefinition_IsTeammateDefinition()
    {
        using Fixture fixture = BuildFixture();
        string vega = Path.Combine(fixture.DataDir, "Teammates", "vega");
        Directory.CreateDirectory(vega);
        File.WriteAllText(Path.Combine(vega, "VEGA.md"), "---\nname: vega\n---\n");

        bool resolved = fixture.Resolver.TryResolve("teammates", "vega/VEGA.md", out LibraryPath? path, out string? error);

        Assert.True(resolved);
        Assert.NotNull(path);
        Assert.Null(error);
        Assert.Equal(LibraryNodeRole.TeammateDefinition, path.Role);
    }

    /// <summary>Item 20: on Windows, an upper-case <c>WORK</c> still matches the lower-case <c>work</c> folder (<see cref="Agency.Huddle.App.FileChanges.FileState"/>'s <c>PathComparer</c>); off Windows the file system is case-sensitive, so this is Windows-only.</summary>
    [Fact]
    public void TryResolve_TeammatesUppercaseWorkFolder_IsWorkDirOnWindowsOnly()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("The Work Dir folder name match is case-sensitive off Windows (corrections-B3 item 20).");
            return;
        }

        using Fixture fixture = BuildFixture();

        bool resolved = fixture.Resolver.TryResolve("teammates", "Nova/WORK", out LibraryPath? path, out string? error);

        Assert.True(resolved);
        Assert.NotNull(path);
        Assert.Null(error);
        Assert.Equal(LibraryNodeRole.WorkDir, path.Role);
    }

    /// <summary>A junction to a folder inside the same root resolves normally (corrections-B3 item 18).</summary>
    [Fact]
    public void TryResolve_JunctionInsideSameRoot_Resolves()
    {
        using Fixture fixture = BuildFixture();
        string link = Path.Combine(fixture.DataDir, "Teams", "Marketing", "inRootLink");
        string target = Path.Combine(fixture.DataDir, "Teams", "Marketing", "Launch Q4");

        try
        {
            CreateLink(link, target);

            bool resolved = fixture.Resolver.TryResolve("teams", "Marketing/inRootLink/plan.md", out LibraryPath? path, out string? error);

            Assert.True(resolved);
            Assert.NotNull(path);
            Assert.Null(error);
        }
        finally
        {
            RemoveLink(link);
        }
    }

    /// <summary>A junction chain whose final target lies outside the root is refused, even on the resolved target, not only the typed path (corrections-B3 item 17-18, Spec §6.1 step 4).</summary>
    [Fact]
    public void TryResolve_JunctionOutsideRoot_Refused()
    {
        using Fixture fixture = BuildFixture();
        string link = Path.Combine(fixture.DataDir, "Teams", "Marketing", "outLink");

        try
        {
            CreateLink(link, fixture.Outside);

            bool resolved = fixture.Resolver.TryResolve("teams", "Marketing/outLink/x", out LibraryPath? path, out string? error);

            Assert.False(resolved);
            Assert.Null(path);
            Assert.Equal("That path isn't inside the Library.", error);
        }
        finally
        {
            RemoveLink(link);
        }
    }

    /// <summary>Step 5 (reserved folders) runs on a junction's resolved target too, not only the typed path (corrections-B3 item 18).</summary>
    [Fact]
    public void TryResolve_JunctionIntoReservedFolder_Refused()
    {
        using Fixture fixture = BuildFixture(new PinnedRootOption { Name = "Parent", Path = "PLACEHOLDER" });
        fixture.SetParentRootPath();
        string link = Path.Combine(fixture.Temp.Path, "link");
        string target = Path.Combine(fixture.DataDir, "rooms");

        try
        {
            CreateLink(link, target);

            bool resolved = fixture.Resolver.TryResolve("parent", "link/r.jsonl", out LibraryPath? path, out string? error);

            Assert.False(resolved);
            Assert.Null(path);
            Assert.Equal("That folder is reserved.", error);
        }
        finally
        {
            RemoveLink(link);
        }
    }

    /// <summary>Addendum A2: <c>TryResolveChild</c> resolves an ordinary file directly under a resolved Team folder.</summary>
    [Fact]
    public void TryResolveChild_File_UnderTeamFolder_IsFile()
    {
        using Fixture fixture = BuildFixture();
        Assert.True(fixture.Resolver.TryResolve("teams", "Marketing/Launch Q4", out LibraryPath? parent, out _));
        Assert.NotNull(parent);
        FileInfo child = new(Path.Combine(fixture.DataDir, "Teams", "Marketing", "Launch Q4", "plan.md"));

        bool resolved = fixture.Resolver.TryResolveChild(parent, child, out LibraryPath? path, out string? error);

        Assert.True(resolved);
        Assert.NotNull(path);
        Assert.Null(error);
        Assert.Equal("Marketing/Launch Q4/plan.md", path.RelativePath);
        Assert.Equal(LibraryNodeRole.File, path.Role);
    }

    /// <summary>Addendum A2: a junction child whose target lies outside the root is refused.</summary>
    [Fact]
    public void TryResolveChild_JunctionOutOfRoot_Refused()
    {
        using Fixture fixture = BuildFixture();
        Assert.True(fixture.Resolver.TryResolve("teams", "Marketing", out LibraryPath? parent, out _));
        Assert.NotNull(parent);
        string link = Path.Combine(fixture.DataDir, "Teams", "Marketing", "childLink");

        try
        {
            CreateLink(link, fixture.Outside);
            DirectoryInfo child = new(link);

            bool resolved = fixture.Resolver.TryResolveChild(parent, child, out LibraryPath? path, out string? error);

            Assert.False(resolved);
            Assert.Null(path);
            Assert.Equal("That path isn't inside the Library.", error);
        }
        finally
        {
            RemoveLink(link);
        }
    }

    /// <summary>Addendum A2: a reserved (underscore-prefixed) child name under Teams is refused.</summary>
    [Fact]
    public void TryResolveChild_ReservedName_Refused()
    {
        using Fixture fixture = BuildFixture();
        Assert.True(fixture.Resolver.TryResolve("teams", "Marketing", out LibraryPath? parent, out _));
        Assert.NotNull(parent);
        DirectoryInfo child = new(Path.Combine(fixture.DataDir, "Teams", "Marketing", "_tasks"));

        bool resolved = fixture.Resolver.TryResolveChild(parent, child, out LibraryPath? path, out string? error);

        Assert.False(resolved);
        Assert.Null(path);
        Assert.Equal("That folder is reserved.", error);
    }

    /// <summary>An unknown root id is refused by name.</summary>
    [Fact]
    public void TryResolve_UnknownRoot_NamesTheId()
    {
        using Fixture fixture = BuildFixture();

        bool resolved = fixture.Resolver.TryResolve("nope", "x", out LibraryPath? path, out string? error);

        Assert.False(resolved);
        Assert.Null(path);
        Assert.Equal("Unknown Library root 'nope'.", error);
    }

    /// <summary>Spec §6.6: a chat path inside a root resolves to its <see cref="LibraryRoot"/> and relative path.</summary>
    [Fact]
    public void TryResolveAbsolute_InsideRoot_ReturnsPath()
    {
        using Fixture fixture = BuildFixture();
        string fullPath = Path.Combine(fixture.DataDir, "Teams", "Marketing", "Launch Q4", "plan.md");

        bool resolved = fixture.Resolver.TryResolveAbsolute(fullPath, out LibraryPath? path);

        Assert.True(resolved);
        Assert.NotNull(path);
        Assert.Equal("teams", path.Root.Id);
        Assert.Equal("Marketing/Launch Q4/plan.md", path.RelativePath);
    }

    /// <summary>Spec §6.6: a <c>file:</c> URL (with its path percent-encoded, item 29) resolves the same as the plain path.</summary>
    [Fact]
    public void TryResolveAbsolute_FileUrl_Resolves()
    {
        using Fixture fixture = BuildFixture();
        string fullPath = Path.Combine(fixture.DataDir, "Teams", "Marketing", "Launch Q4", "plan.md");
        string fileUrl = new Uri(fullPath).AbsoluteUri;

        bool resolved = fixture.Resolver.TryResolveAbsolute(fileUrl, out LibraryPath? path);

        Assert.True(resolved);
        Assert.NotNull(path);
        Assert.Equal("teams", path.Root.Id);
        Assert.Equal("Marketing/Launch Q4/plan.md", path.RelativePath);
    }

    /// <summary>A path under no configured root is refused.</summary>
    [Fact]
    public void TryResolveAbsolute_Outside_ReturnsFalse()
    {
        using Fixture fixture = BuildFixture();
        string fullPath = Path.Combine(fixture.Outside, "x.md");

        bool resolved = fixture.Resolver.TryResolveAbsolute(fullPath, out LibraryPath? path);

        Assert.False(resolved);
        Assert.Null(path);
    }

    /// <summary>A reserved top-level folder under <c>DataDir</c>, reached through a pinned root above it, is refused (Spec §6.1 step 5).</summary>
    [Fact]
    public void TryResolveAbsolute_Reserved_ReturnsFalse()
    {
        using Fixture fixture = BuildFixture(new PinnedRootOption { Name = "Parent", Path = "PLACEHOLDER" });
        fixture.SetParentRootPath();
        string fullPath = Path.Combine(fixture.DataDir, "rooms", "r.jsonl");

        bool resolved = fixture.Resolver.TryResolveAbsolute(fullPath, out LibraryPath? path);

        Assert.False(resolved);
        Assert.Null(path);
    }

    /// <summary>A Team's <c>_tasks</c> folder, reached as an absolute path under the Teams root, is refused (Spec §6.1 step 6).</summary>
    [Fact]
    public void TryResolveAbsolute_TasksFolder_ReturnsFalse()
    {
        using Fixture fixture = BuildFixture();
        string fullPath = Path.Combine(fixture.DataDir, "Teams", "Marketing", "_tasks", "MKT-0001.md");

        bool resolved = fixture.Resolver.TryResolveAbsolute(fullPath, out LibraryPath? path);

        Assert.False(resolved);
        Assert.Null(path);
    }

    /// <summary>A pinned root nested inside the Teams root is the longer, and thus winning, prefix (item 32).</summary>
    [Fact]
    public void TryResolveAbsolute_OverlappingPinnedRoots_PrefersLongestRoot()
    {
        using Fixture fixture = BuildFixture(new PinnedRootOption { Name = "Marketing", Path = "PLACEHOLDER" });
        fixture.SetPinnedRootPath("Marketing", Path.Combine(fixture.DataDir, "Teams", "Marketing"));
        string fullPath = Path.Combine(fixture.DataDir, "Teams", "Marketing", "Launch Q4", "plan.md");

        bool resolved = fixture.Resolver.TryResolveAbsolute(fullPath, out LibraryPath? path);

        Assert.True(resolved);
        Assert.NotNull(path);
        Assert.Equal("marketing", path.Root.Id);
        Assert.Equal("Launch Q4/plan.md", path.RelativePath);
    }

    /// <summary>Item 22/30: a sibling folder whose name merely starts with a pinned root's name is not inside it (no separator-terminated prefix match).</summary>
    [Fact]
    public void TryResolveAbsolute_SiblingWithRootPrefix_ReturnsFalse()
    {
        using Fixture fixture = BuildFixture();
        string fullPath = Path.Combine(fixture.Temp.Path, "Vault-evil", "secret.md");

        bool resolved = fixture.Resolver.TryResolveAbsolute(fullPath, out LibraryPath? path);

        Assert.False(resolved);
        Assert.Null(path);
    }

    /// <summary>Item 30: relative text is never fully qualified, so it is refused before any root is even looked up.</summary>
    [Fact]
    public void TryResolveAbsolute_RelativeText_ReturnsFalse()
    {
        using Fixture fixture = BuildFixture();

        bool resolved = fixture.Resolver.TryResolveAbsolute("Marketing/Launch Q4/plan.md", out LibraryPath? path);

        Assert.False(resolved);
        Assert.Null(path);
    }

    /// <summary>Item 30: a Windows-shaped drive path is refused - on Linux it is not fully qualified, and on Windows it names an unrelated drive outside every root.</summary>
    [Fact]
    public void TryResolveAbsolute_WindowsPathOnLinux_ReturnsFalse()
    {
        using Fixture fixture = BuildFixture();

        bool resolved = fixture.Resolver.TryResolveAbsolute(@"C:\Users\someone\Documents\plan.md", out LibraryPath? path);

        Assert.False(resolved);
        Assert.Null(path);
    }

    /// <summary>Item 30, Windows only: a lowercase drive letter and forward slashes still resolve, and the relative part keeps the input's casing.</summary>
    [Fact]
    public void TryResolveAbsolute_LowercaseDriveAndForwardSlashes_Resolves()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using Fixture fixture = BuildFixture();
        string fullPath = Path.Combine(fixture.DataDir, "Teams", "Marketing", "Launch Q4", "plan.md");
        string lowered = char.ToLowerInvariant(fullPath[0]) + fullPath[1..].Replace('\\', '/');

        bool resolved = fixture.Resolver.TryResolveAbsolute(lowered, out LibraryPath? path);

        Assert.True(resolved);
        Assert.NotNull(path);
        Assert.Equal("teams", path.Root.Id);
        Assert.Equal("Marketing/Launch Q4/plan.md", path.RelativePath);
    }

    /// <summary>Item 30: two roots with an identical resolved path tie on length, and the built-in (earlier in <c>Roots</c> order) wins.</summary>
    [Fact]
    public void TryResolveAbsolute_TiedRoots_BuiltInWins()
    {
        using Fixture fixture = BuildFixture(new PinnedRootOption { Name = "TeamsAlias", Path = "PLACEHOLDER" });
        fixture.SetPinnedRootPath("TeamsAlias", Path.Combine(fixture.DataDir, "Teams"));
        string fullPath = Path.Combine(fixture.DataDir, "Teams", "Marketing", "Launch Q4", "plan.md");

        bool resolved = fixture.Resolver.TryResolveAbsolute(fullPath, out LibraryPath? path);

        Assert.True(resolved);
        Assert.NotNull(path);
        Assert.Equal("teams", path.Root.Id);
    }

    /// <summary>Item 30: an extended-length <c>\\?\</c> prefix never matches a configured root's own (unprefixed) full path.</summary>
    [Fact]
    public void TryResolveAbsolute_ExtendedPrefix_ReturnsFalse()
    {
        using Fixture fixture = BuildFixture();
        string fullPath = @"\\?\" + Path.Combine(fixture.DataDir, "Teams", "Marketing", "Launch Q4", "plan.md");

        bool resolved = fixture.Resolver.TryResolveAbsolute(fullPath, out LibraryPath? path);

        Assert.False(resolved);
        Assert.Null(path);
    }

    /// <summary>Spec §6.16: a Project folder scope resolves to its <see cref="LibraryPath"/>.</summary>
    [Fact]
    public void TryResolveScope_ProjectFolder_Resolves()
    {
        using Fixture fixture = BuildFixture();
        LibraryLocation scope = new("teams", "Marketing/Launch Q4");

        bool resolved = fixture.Resolver.TryResolveScope(scope, out LibraryPath? folder, out string? error);

        Assert.True(resolved);
        Assert.NotNull(folder);
        Assert.Null(error);
        Assert.Equal(LibraryNodeRole.ProjectFolder, folder.Role);
    }

    /// <summary>Item 33: every failure - here an unknown root - gives the same host-facing text.</summary>
    [Fact]
    public void TryResolveScope_UnknownRoot_ReturnsFalse()
    {
        using Fixture fixture = BuildFixture();
        LibraryLocation scope = new("nope", "x");

        bool resolved = fixture.Resolver.TryResolveScope(scope, out LibraryPath? folder, out string? error);

        Assert.False(resolved);
        Assert.Null(folder);
        Assert.Equal("This folder isn't available in the Library.", error);
    }

    /// <summary>Item 33: an existing file is not a folder, so it is refused even though it resolves.</summary>
    [Fact]
    public void TryResolveScope_NotAFolder_ReturnsFalse()
    {
        using Fixture fixture = BuildFixture();
        LibraryLocation scope = new("teams", "Marketing/Launch Q4/plan.md");

        bool resolved = fixture.Resolver.TryResolveScope(scope, out LibraryPath? folder, out string? error);

        Assert.False(resolved);
        Assert.Null(folder);
        Assert.Equal("This folder isn't available in the Library.", error);
    }

    /// <summary>Spec §6.16: a Team folder that doesn't exist yet still resolves, so the explorer shows an empty tree.</summary>
    [Fact]
    public void TryResolveScope_MissingTeamFolder_ResolvesAsEmpty()
    {
        using Fixture fixture = BuildFixture();
        LibraryLocation scope = new("teams", "Newteam");

        bool resolved = fixture.Resolver.TryResolveScope(scope, out LibraryPath? folder, out string? error);

        Assert.True(resolved);
        Assert.NotNull(folder);
        Assert.Null(error);
        Assert.Equal(LibraryNodeRole.TeamFolder, folder.Role);
    }

    /// <summary>Creates a link at <paramref name="link"/> pointing to <paramref name="target"/>: a junction on Windows (the only unelevated way, per Task 0.2), a symbolic link elsewhere.</summary>
    private static void CreateLink(string link, string target)
    {
        if (OperatingSystem.IsWindows())
        {
            ProcessStartInfo startInfo = new("cmd")
            {
                ArgumentList = { "/c", "mklink", "/J", link, target },
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("mklink did not start.");
            process.WaitForExit();
            Assert.Equal(0, process.ExitCode);
        }
        else
        {
            Directory.CreateSymbolicLink(link, target);
        }
    }

    /// <summary>Removes a link created by <see cref="CreateLink"/>, before the owning <see cref="TempDataDir"/> disposes (its cleanup cannot recurse through a reparse point).</summary>
    private static void RemoveLink(string link)
    {
        if (Directory.Exists(link))
        {
            Directory.Delete(link, recursive: false);
        }
    }

    /// <summary>An isolated fixture per Spec §6.1's cited layout (corrections-B3 4.2.t item 15): <c>{temp}/App_Data</c> as <c>DataDir</c>, with <c>Vault</c>, <c>Vault-evil</c> and <c>Outside</c> beside it.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly TeamOptions teamOptions;

        public Fixture(TempDataDir temp, string dataDir, string vault, string outside, TeamOptions teamOptions)
        {
            this.Temp = temp;
            this.DataDir = dataDir;
            this.Vault = vault;
            this.Outside = outside;
            this.teamOptions = teamOptions;
            TeammatePaths paths = new(Options.Create(teamOptions));
            this.RootStore = new LibraryRootStore(Options.Create(teamOptions), paths, NullLogger<LibraryRootStore>.Instance);
            this.Resolver = new LibraryPathResolver(this.RootStore, Options.Create(teamOptions), NullLogger<LibraryPathResolver>.Instance);
        }

        public TempDataDir Temp { get; }

        public string DataDir { get; }

        public string Vault { get; }

        public string Outside { get; }

        public LibraryRootStore RootStore { get; private set; }

        public LibraryPathResolver Resolver { get; private set; }

        /// <summary>Points the pinned root named "Parent" at this fixture's <c>DataDir</c>'s parent (its own temp root), then rebuilds the store and resolver.</summary>
        public void SetParentRootPath()
        {
            this.SetPinnedRootPath("Parent", this.Temp.Path);
        }

        /// <summary>Rewrites the named pinned root's configured path in place and rebuilds the store and resolver against it.</summary>
        public void SetPinnedRootPath(string name, string path)
        {
            List<PinnedRootOption> roots = [.. this.teamOptions.Library.Roots ?? []];
            int index = roots.FindIndex(r => string.Equals(r.Name, name, StringComparison.Ordinal));
            roots[index] = new PinnedRootOption { Name = name, Path = path };
            this.teamOptions.Library.Roots = roots;
            TeammatePaths paths = new(Options.Create(this.teamOptions));
            this.RootStore = new LibraryRootStore(Options.Create(this.teamOptions), paths, NullLogger<LibraryRootStore>.Instance);
            this.Resolver = new LibraryPathResolver(this.RootStore, Options.Create(this.teamOptions), NullLogger<LibraryPathResolver>.Instance);
        }

        public void Dispose() => this.Temp.Dispose();
    }

    /// <summary>Builds the shared fixture folder layout (corrections-B3 4.2.t item 15) plus any additional pinned roots the test needs.</summary>
    private static Fixture BuildFixture(params PinnedRootOption[] extraRoots)
    {
        TempDataDir temp = new();
        string dataDir = Path.Combine(temp.Path, "App_Data");
        Directory.CreateDirectory(dataDir);

        string vault = Path.Combine(temp.Path, "Vault");
        string vaultEvil = Path.Combine(temp.Path, "Vault-evil");
        string outside = Path.Combine(temp.Path, "Outside");
        Directory.CreateDirectory(vault);
        Directory.CreateDirectory(vaultEvil);
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(vaultEvil, "secret.md"), string.Empty);

        Directory.CreateDirectory(Path.Combine(dataDir, "Teams", "Marketing", "Launch Q4"));
        File.WriteAllText(Path.Combine(dataDir, "Teams", "Marketing", "Launch Q4", "plan.md"), string.Empty);
        Directory.CreateDirectory(Path.Combine(dataDir, "Teams", "Marketing", "_tasks"));
        File.WriteAllText(Path.Combine(dataDir, "Teams", "Marketing", "_tasks", "MKT-0001.md"), string.Empty);
        File.WriteAllText(Path.Combine(dataDir, "Teams", "readme.md"), string.Empty);

        Directory.CreateDirectory(Path.Combine(dataDir, "Teammates", "Nova", "work", "memory"));
        File.WriteAllText(Path.Combine(dataDir, "Teammates", "Nova", "Nova.md"), string.Empty);
        File.WriteAllText(Path.Combine(dataDir, "Teammates", "Nova", "work", "memory", "x.md"), string.Empty);
        File.WriteAllText(Path.Combine(dataDir, "Teammates", "Nova", "notes.md"), string.Empty);
        Directory.CreateDirectory(Path.Combine(dataDir, "Teammates", "_unsorted"));

        Directory.CreateDirectory(Path.Combine(dataDir, "rooms"));
        File.WriteAllText(Path.Combine(dataDir, "rooms", "r.jsonl"), string.Empty);

        List<PinnedRootOption> roots = [new PinnedRootOption { Name = "Vault", Path = vault }, .. extraRoots];
        TeamOptions teamOptions = new()
        {
            DataDir = dataDir,
            Library = new LibraryOptions { Roots = roots },
        };

        return new Fixture(temp, dataDir, vault, outside, teamOptions);
    }
}
