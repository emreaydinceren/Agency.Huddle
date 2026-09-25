using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>Tests for Library value semantics records and enums (Spec §6.1, §6.16).</summary>
public sealed class LibraryRecordsTests
{
    /// <summary>LibraryRoot equality is by value, not by reference.</summary>
    [Fact]
    public void LibraryRoot_Equality_IsByValue()
    {
        LibraryRoot a = new("id", "Name", "path", LibraryRootKind.Teams);
        LibraryRoot b = new("id", "Name", "path", LibraryRootKind.Teams);

        Assert.Equal(a, b);
    }

    /// <summary>LibraryPath with() changes RelativePath.</summary>
    [Fact]
    public void LibraryPath_With_ChangesRelativePath()
    {
        LibraryRoot root = new("id", "Name", "path", LibraryRootKind.Teams);
        LibraryPath original = new(root, "a/b", "full/a/b", LibraryNodeRole.File);

        LibraryPath modified = original with { RelativePath = "x/y" };

        Assert.Equal("x/y", modified.RelativePath);
        Assert.Equal("a/b", original.RelativePath);
    }

    /// <summary>LibraryLocation equality is by value, not by reference.</summary>
    [Fact]
    public void LibraryLocation_Equality_IsByValue()
    {
        LibraryLocation a = new("rootId", "path");
        LibraryLocation b = new("rootId", "path");

        Assert.Equal(a, b);
    }

    /// <summary>LibraryRootKind has exactly Teams, Teammates, Pinned members.</summary>
    [Fact]
    public void LibraryRootKind_Members_AreTeamsTeammatesPinned()
    {
        string[] expected = ["Teams", "Teammates", "Pinned"];

        Assert.Equal(expected, Enum.GetNames<LibraryRootKind>());
    }

    /// <summary>LibraryNodeRole has members matching the Type map.</summary>
    [Fact]
    public void LibraryNodeRole_Members_MatchTypeMap()
    {
        string[] expected = ["Root", "TeamFolder", "ProjectFolder", "TeammateFolder", "TeammateDefinition", "WorkDir", "Folder", "File"];

        Assert.Equal(expected, Enum.GetNames<LibraryNodeRole>());
    }

    /// <summary>LibraryExplorerLayout has exactly Stacked and SideBySide members.</summary>
    [Fact]
    public void LibraryExplorerLayout_Members_AreStackedSideBySide()
    {
        string[] expected = ["Stacked", "SideBySide"];

        Assert.Equal(expected, Enum.GetNames<LibraryExplorerLayout>());
    }
}
