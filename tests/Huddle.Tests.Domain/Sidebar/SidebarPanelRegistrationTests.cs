using Agency.Huddle.App;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Sidebar;

/// <summary>Pins the five Right sidebar panel registrations (Spec D-3) against the real container.</summary>
public sealed class SidebarPanelRegistrationTests
{
    /// <summary>A room with every feature on gets Members, Tasks and Documents, in Order.</summary>
    [Fact]
    public void Room_GetsMembersTasksDocuments_InOrder()
    {
        using TempDataDir dataDir = new();
        using ServiceProvider provider = BuildProvider(dataDir, new Dictionary<string, string?>());
        SidebarPanelCatalog catalog = provider.GetRequiredService<SidebarPanelCatalog>();

        List<string> ids = catalog.For(new RoomSubject("ab12")).Select(static panel => panel.Id).ToList();

        string[] expected = ["members", "room-tasks", "room-documents"];
        Assert.Equal(expected, ids);
    }

    /// <summary>A Markdown document gets the Outline, Linked documents, Backlinks and History panels.</summary>
    [Fact]
    public void MarkdownDocument_GetsOutlineLinkedBacklinksAndRecentEdits()
    {
        using TempDataDir dataDir = new();
        using ServiceProvider provider = BuildProvider(dataDir, new Dictionary<string, string?>());
        SidebarPanelCatalog catalog = provider.GetRequiredService<SidebarPanelCatalog>();

        List<string> ids = catalog.For(MarkdownSubject()).Select(static panel => panel.Id).ToList();

        string[] expected = ["outline", "linked-documents", "backlinks", "recent-edits"];
        Assert.Equal(expected, ids);
    }

    /// <summary>An image document gets no sidebar panels at all.</summary>
    [Fact]
    public void ImageDocument_GetsNothing()
    {
        using TempDataDir dataDir = new();
        using ServiceProvider provider = BuildProvider(dataDir, new Dictionary<string, string?>());
        SidebarPanelCatalog catalog = provider.GetRequiredService<SidebarPanelCatalog>();
        DocumentSubject image = new(SamplePath(), LibraryFileKind.Image, null);

        List<string> ids = catalog.For(image).Select(static panel => panel.Id).ToList();

        Assert.Empty(ids);
    }

    /// <summary>With the Library feature off, a room keeps Members and Tasks and loses Documents.</summary>
    [Fact]
    public void LibraryDisabled_RoomGetsMembersAndTasks()
    {
        using TempDataDir dataDir = new();
        using ServiceProvider provider = BuildProvider(dataDir, new Dictionary<string, string?> { ["Team:Library:Enabled"] = "false" });
        SidebarPanelCatalog catalog = provider.GetRequiredService<SidebarPanelCatalog>();

        List<string> ids = catalog.For(new RoomSubject("ab12")).Select(static panel => panel.Id).ToList();

        string[] expected = ["members", "room-tasks"];
        Assert.Equal(expected, ids);
    }

    /// <summary>With the Tasks feature off, a room keeps Members and Documents and loses Tasks.</summary>
    [Fact]
    public void TasksDisabled_RoomGetsMembersAndDocuments()
    {
        using TempDataDir dataDir = new();
        using ServiceProvider provider = BuildProvider(dataDir, new Dictionary<string, string?> { ["Team:Tasks:Enabled"] = "false" });
        SidebarPanelCatalog catalog = provider.GetRequiredService<SidebarPanelCatalog>();

        List<string> ids = catalog.For(new RoomSubject("ab12")).Select(static panel => panel.Id).ToList();

        string[] expected = ["members", "room-documents"];
        Assert.Equal(expected, ids);
    }

    /// <summary>A Task open for editing gets Activity and Linked documents; Blocked by and Blocks join it only when the Subject says the Task has rows for them (read view spec D-8).</summary>
    [Fact]
    public void Task_GetsActivityAndDocuments_AndTheBlockerPanelsOnlyWhenTheyHaveRows()
    {
        using TempDataDir dataDir = new();
        using ServiceProvider provider = BuildProvider(dataDir, new Dictionary<string, string?>());
        SidebarPanelCatalog catalog = provider.GetRequiredService<SidebarPanelCatalog>();
        _ = TaskId.TryParse("PLAT-0013", out TaskId id);

        List<string> bare = catalog.For(new TaskSubject(id)).Select(static panel => panel.Id).ToList();
        List<string> both = catalog.For(new TaskSubject(id, HasBlockers: true, IsBlocking: true)).Select(static panel => panel.Id).ToList();

        Assert.Equal(["task-activity", "task-documents"], bare);
        Assert.Equal(["task-activity", "task-blockers", "task-blocks", "task-documents"], both);
    }

    /// <summary>None of the four Task panels applies to a Room or a document.</summary>
    [Fact]
    public void TaskPanels_DoNotApplyToARoomOrADocument()
    {
        using TempDataDir dataDir = new();
        using ServiceProvider provider = BuildProvider(dataDir, new Dictionary<string, string?>());
        SidebarPanelCatalog catalog = provider.GetRequiredService<SidebarPanelCatalog>();

        List<string> room = catalog.For(new RoomSubject("ab12")).Select(static panel => panel.Id).ToList();
        List<string> document = catalog.For(MarkdownSubject()).Select(static panel => panel.Id).ToList();

        Assert.DoesNotContain(room.Concat(document), static id => id.StartsWith("task-", StringComparison.Ordinal));
    }

    /// <summary>With the Library off, the Task's Linked documents panel is not registered; the others stay.</summary>
    [Fact]
    public void TaskDocuments_LibraryDisabled_IsNotOffered()
    {
        using TempDataDir dataDir = new();
        using ServiceProvider provider = BuildProvider(dataDir, new Dictionary<string, string?> { ["Team:Library:Enabled"] = "false" });
        SidebarPanelCatalog catalog = provider.GetRequiredService<SidebarPanelCatalog>();
        _ = TaskId.TryParse("PLAT-0013", out TaskId id);

        List<string> ids = catalog.For(new TaskSubject(id, HasBlockers: true, IsBlocking: true)).Select(static panel => panel.Id).ToList();

        Assert.Equal(["task-activity", "task-blockers", "task-blocks"], ids);
    }

    /// <summary>The Task Subject's key names the Task alone, so its panels stay mounted while its flags change.</summary>
    [Fact]
    public void TaskSubject_KeyIgnoresTheFlags()
    {
        _ = TaskId.TryParse("PLAT-0013", out TaskId id);

        Assert.Equal("task:PLAT-0013", new TaskSubject(id, HasBlockers: true, IsBlocking: true).Key);
        Assert.Equal(new TaskSubject(id).Key, new TaskSubject(id, HasBlockers: true).Key);
    }

    /// <summary>A Task selected on the Tasks page gets the Task panel alone, and a Task open for editing does not get it, so the edit page never shows its Task twice.</summary>
    [Fact]
    public void TaskDetail_IsOfferedForTheTasksPagesSubjectAlone()
    {
        using TempDataDir dataDir = new();
        using ServiceProvider provider = BuildProvider(dataDir, new Dictionary<string, string?>());
        SidebarPanelCatalog catalog = provider.GetRequiredService<SidebarPanelCatalog>();
        _ = TaskId.TryParse("PLAT-0013", out TaskId id);

        List<string> selected = catalog.For(new TaskDetailSubject(id)).Select(static panel => panel.Id).ToList();
        List<string> editing = catalog.For(new TaskSubject(id)).Select(static panel => panel.Id).ToList();

        Assert.Equal(["task-detail"], selected);
        Assert.DoesNotContain("task-detail", editing);
    }

    /// <summary>With Tasks turned off the Task panel is not offered.</summary>
    [Fact]
    public void TaskDetail_TasksDisabled_IsNotOffered()
    {
        using TempDataDir dataDir = new();
        using ServiceProvider provider = BuildProvider(dataDir, new Dictionary<string, string?> { ["Team:Tasks:Enabled"] = "false" });
        SidebarPanelCatalog catalog = provider.GetRequiredService<SidebarPanelCatalog>();
        _ = TaskId.TryParse("PLAT-0013", out TaskId id);

        Assert.Empty(catalog.For(new TaskDetailSubject(id)));
    }

    /// <summary>Builds the real container with the Team data directory and the given feature switches.</summary>
    /// <param name="dataDir">The temporary data directory the Team services write to.</param>
    /// <param name="switches">Configuration values layered over the data directory.</param>
    /// <returns>The built service provider.</returns>
    private static ServiceProvider BuildProvider(TempDataDir dataDir, Dictionary<string, string?> switches)
    {
        Dictionary<string, string?> values = new(switches) { ["Team:DataDir"] = dataDir.Path };
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddTeamServices(configuration);
        return services.BuildServiceProvider();
    }

    /// <summary>A Markdown document subject for the path helper.</summary>
    /// <returns>The document subject.</returns>
    private static DocumentSubject MarkdownSubject()
    {
        return new DocumentSubject(SamplePath(), LibraryFileKind.Markdown, "# Hello");
    }

    /// <summary>A library path to a file at the root of a Teams library.</summary>
    /// <returns>The library path.</returns>
    private static LibraryPath SamplePath()
    {
        LibraryRoot root = new("teams", "Teams", "/tmp/teams", LibraryRootKind.Teams);
        return new LibraryPath(root, "Plan.md", "/tmp/teams/Plan.md", LibraryNodeRole.File);
    }
}
