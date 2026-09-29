namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins where the Teams group sits in the sidebar (Task 7.5, Spec §5.2, §6.5): between the Chats group and
/// the Teammates link, ahead of the Tasks and Library groups. One test reads the real host's prerendered
/// drawer, which also proves DI resolves the Team services in the real host; one pins the layout source.
/// </summary>
public sealed class MainLayoutTeamsTests
{
    /// <summary>The prerendered drawer lists Chats, Teams, Teammates, Tasks and Library in that order.</summary>
    [Fact]
    public async Task Drawer_ListsChats_Teams_Teammates_Tasks_Library_InThatOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        string html = await client.GetStringAsync("/", ct);

        int chats = html.IndexOf(">Chats<", StringComparison.Ordinal);
        int teams = html.IndexOf("teams-nav", StringComparison.Ordinal);
        int teammates = html.IndexOf("href=\"/teammates\"", StringComparison.Ordinal);
        int tasks = html.IndexOf("task-view-nav", StringComparison.Ordinal);
        int library = html.IndexOf("library-nav", StringComparison.Ordinal);

        Assert.True(chats >= 0, "the Chats group title is missing from the drawer");
        Assert.True(teams >= 0, "the 'teams-nav' marker is missing from the drawer");
        Assert.True(teammates >= 0, "the Teammates link is missing from the drawer");
        Assert.True(tasks >= 0, "the 'task-view-nav' marker is missing from the drawer");
        Assert.True(library >= 0, "the 'library-nav' marker is missing from the drawer");
        Assert.True(chats < teams, "Chats must come before Teams");
        Assert.True(teams < teammates, "Teams must come before Teammates");
        Assert.True(teammates < tasks, "Teammates must come before Tasks");
        Assert.True(tasks < library, "Tasks must come before Library");
    }

    /// <summary>Source-text pin: <c>MainLayout.razor</c> renders <c>&lt;TeamsNav</c> after <c>&lt;RoomList</c> and before the Teammates link.</summary>
    [Fact]
    public void MainLayoutSource_HasTeamsNavAfterRoomList_AndBeforeTheTeammatesLink()
    {
        string repoRoot = FindRepoRoot();
        string text = File.ReadAllText(Path.Combine(repoRoot, "src", "Huddle.App", "Components", "Layout", "MainLayout.razor"));

        int roomListIndex = text.IndexOf("<RoomList", StringComparison.Ordinal);
        int teamsNavIndex = text.IndexOf("<TeamsNav", StringComparison.Ordinal);
        int teammatesIndex = text.IndexOf("Href=\"/teammates\"", StringComparison.Ordinal);

        Assert.True(roomListIndex >= 0, "'<RoomList' is missing from MainLayout.razor");
        Assert.True(teamsNavIndex >= 0, "'<TeamsNav' is missing from MainLayout.razor");
        Assert.True(teammatesIndex >= 0, "the Teammates link is missing from MainLayout.razor");
        Assert.True(teamsNavIndex > roomListIndex, "'<TeamsNav' must come after '<RoomList'");
        Assert.True(teammatesIndex > teamsNavIndex, "the Teammates link must come after '<TeamsNav'");
    }

    /// <summary>Walks up from <see cref="AppContext.BaseDirectory"/> until it finds the directory containing <c>Huddle.slnx</c>.</summary>
    private static string FindRepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "Huddle.slnx");
            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Could not find 'Huddle.slnx' above '{AppContext.BaseDirectory}'.");
    }
}
