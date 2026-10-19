using Agency.Huddle.App.Library;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.Tabs;

namespace Agency.Huddle.Tests.Sidebar;

/// <summary>Pins the per-Tab Subjects in <see cref="RightSidebarService"/> (Spec D-2) and the no-loop rule.</summary>
public sealed class RightSidebarServiceTests
{
    private static DocumentSubject Doc(string text)
    {
        LibraryRoot root = new("teams", "Teams", "/tmp/teams", LibraryRootKind.Teams);
        LibraryPath path = new(root, "Plan.md", "/tmp/teams/Plan.md", LibraryNodeRole.File);
        return new DocumentSubject(path, LibraryFileKind.Markdown, text);
    }

    /// <summary>A Subject set for a Tab is returned for that Tab.</summary>
    [Fact]
    public void SubjectFor_AfterSet_ReturnsIt()
    {
        RightSidebarService service = new();
        RoomSubject subject = new("r1");

        service.Set(service.NewId(), "t1", subject);

        Assert.Equal(subject, service.SubjectFor("t1"));
    }

    /// <summary>Another Tab has no Subject.</summary>
    [Fact]
    public void SubjectFor_OtherTab_IsNull()
    {
        RightSidebarService service = new();
        service.Set(service.NewId(), "t1", new RoomSubject("r1"));

        Assert.Null(service.SubjectFor("t2"));
    }

    /// <summary>Removing the only source clears the Tab's Subject.</summary>
    [Fact]
    public void Remove_ClearsTheSubject()
    {
        RightSidebarService service = new();
        long id = service.NewId();
        service.Set(id, "t1", new RoomSubject("r1"));

        service.Remove(id);

        Assert.Null(service.SubjectFor("t1"));
    }

    /// <summary>Setting an equal Subject again raises nothing, so page and sidebar cannot render each other in a loop.</summary>
    [Fact]
    public void Set_EqualSubject_DoesNotRaiseChanged()
    {
        RightSidebarService service = new();
        long id = service.NewId();
        service.Set(id, "t1", Doc("a"));
        int raised = 0;
        service.Changed += () => raised++;

        service.Set(id, "t1", Doc("a"));

        Assert.Equal(0, raised);
    }

    /// <summary>A Subject with new text is a change and raises <see cref="RightSidebarService.Changed"/> once.</summary>
    [Fact]
    public void Set_NewText_RaisesChanged()
    {
        RightSidebarService service = new();
        long id = service.NewId();
        service.Set(id, "t1", Doc("a"));
        int raised = 0;
        service.Changed += () => raised++;

        service.Set(id, "t1", Doc("b"));

        Assert.Equal(1, raised);
        Assert.Equal(Doc("b"), service.SubjectFor("t1"));
    }

    /// <summary>Two sources on one Tab: the one set last wins.</summary>
    [Fact]
    public void SubjectFor_TwoSourcesOneTab_LastSetWins()
    {
        RightSidebarService service = new();
        service.Set(service.NewId(), "t1", new RoomSubject("first"));
        service.Set(service.NewId(), "t1", new RoomSubject("second"));

        Assert.Equal(new RoomSubject("second"), service.SubjectFor("t1"));
    }

    /// <summary>Removing the winning source falls back to the other source.</summary>
    [Fact]
    public void Remove_LastSource_FallsBackToTheOther()
    {
        RightSidebarService service = new();
        service.Set(service.NewId(), "t1", new RoomSubject("first"));
        long second = service.NewId();
        service.Set(second, "t1", new RoomSubject("second"));

        service.Remove(second);

        Assert.Equal(new RoomSubject("first"), service.SubjectFor("t1"));
    }

    /// <summary>No Tab means no Subject.</summary>
    [Fact]
    public void SubjectFor_NullTab_IsNull()
    {
        RightSidebarService service = new();
        service.Set(service.NewId(), "t1", new RoomSubject("r1"));

        Assert.Null(service.SubjectFor(null));
    }
}
