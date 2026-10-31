using Agency.Huddle.App.Library;
using Agency.Huddle.App.Sidebar;

namespace Agency.Huddle.Tests.Sidebar;

/// <summary>Pins the Subject keys of Spec D-2: what a page is about, without its live state.</summary>
public sealed class PageSubjectTests
{
    /// <summary>A room subject's key is the room's id behind a "room:" prefix.</summary>
    [Fact]
    public void RoomSubject_Key_IsRoomColonId()
    {
        RoomSubject subject = new("ab12");

        Assert.Equal("room:ab12", subject.Key);
    }

    /// <summary>A document subject's key is its Library root id and relative path, joined by a slash.</summary>
    [Fact]
    public void DocumentSubject_Key_IsDocRootSlashPath()
    {
        DocumentSubject subject = new(PlanPath(), LibraryFileKind.Markdown, "# Plan");

        Assert.Equal("doc:notes/Teams/Plan.md", subject.Key);
    }

    /// <summary>Two document subjects that differ only in their text share a key and are not equal.</summary>
    [Fact]
    public void DocumentSubject_Key_IgnoresText()
    {
        DocumentSubject before = new(PlanPath(), LibraryFileKind.Markdown, "# Plan");
        DocumentSubject after = new(PlanPath(), LibraryFileKind.Markdown, "# Plan, edited");

        Assert.Equal(before.Key, after.Key);
        Assert.NotEqual(before, after);
    }

    /// <summary>The Library file the document subject tests use, under a pinned root named notes.</summary>
    private static LibraryPath PlanPath()
    {
        LibraryRoot root = new("notes", "Notes", @"C:\n", LibraryRootKind.Pinned);
        return new LibraryPath(root, "Teams/Plan.md", @"C:\n\Teams\Plan.md", LibraryNodeRole.File);
    }
}
