using Agency.Huddle.App;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Sidebar;
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

    /// <summary>A Markdown document gets the Outline, Linked documents and Recent edits panels.</summary>
    [Fact]
    public void MarkdownDocument_GetsOutlineLinkedAndRecentEdits()
    {
        using TempDataDir dataDir = new();
        using ServiceProvider provider = BuildProvider(dataDir, new Dictionary<string, string?>());
        SidebarPanelCatalog catalog = provider.GetRequiredService<SidebarPanelCatalog>();

        List<string> ids = catalog.For(MarkdownSubject()).Select(static panel => panel.Id).ToList();

        string[] expected = ["outline", "linked-documents", "recent-edits"];
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
