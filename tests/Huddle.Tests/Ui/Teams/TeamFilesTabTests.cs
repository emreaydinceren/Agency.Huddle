using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Library;
using Agency.Huddle.App.Components.Teams;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Tasks;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins Spec §6.8's Files tab: <c>TeamFilesTab</c> hosts a <see cref="LibraryExplorer"/> scoped to the Team
/// (or Project) folder under the <c>teams</c> root, side by side, remembering its state under
/// <c>team:&lt;Team&gt;[/&lt;Project&gt;]</c>, above a <c>TeamTabToolbar</c> ("New note", "Search files"). The
/// tab's <c>Search</c> is the explorer's <c>Filter</c>, and both a folder hit's clearing and the toolbar's
/// clearing arrive as <c>SearchChanged(null)</c>. Runs over the REAL Library stack on a temp <c>DataDir</c>.
/// </summary>
public sealed class TeamFilesTabTests : IDisposable
{
    /// <summary>The toolbar's action button ("New note").</summary>
    private const string ActionButton = "button.team-tab-toolbar-action";

    /// <summary>The toolbar's search input.</summary>
    private const string SearchInput = ".team-tab-toolbar-search input";

    /// <summary>One name-match row of the explorer's result list.</summary>
    private const string Hit = ".library-search-hit";

    /// <summary>The name of every rendered tree node.</summary>
    private const string NodeName = "span.library-tree-node-name";

    private readonly TabFixture fixture = TabFixture.Build();

    /// <summary>Disposes the temp <c>DataDir</c> and the stores over it.</summary>
    public void Dispose() => this.fixture.Dispose();

    /// <summary>A Team alone scopes the explorer to that Team's folder; with a Project, to the Project's folder inside it.</summary>
    /// <param name="project">The Project, or <see langword="null"/> for the Team's own page.</param>
    /// <param name="expectedFolder">The folder under the <c>teams</c> root the explorer must show.</param>
    [Theory]
    [InlineData(null, "Business")]
    [InlineData("Marketing Project", "Business/Marketing Project")]
    public async Task Scopes_TeamOrProject_IsThatFolderUnderTheTeamsRoot(string? project, string expectedFolder)
    {
        this.SeedTeam(expectedFolder);

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx, "Business", project);

        IReadOnlyList<LibraryLocation> expected = [new LibraryLocation("teams", expectedFolder)];
        Assert.Equal(expected, cut.FindComponent<LibraryExplorer>().Instance.Scopes);
    }

    /// <summary>The explorer remembers its state under <c>team:Business</c>, or <c>team:Business/Marketing Project</c> for a Project.</summary>
    /// <param name="project">The Project, or <see langword="null"/> for the Team's own page.</param>
    /// <param name="expectedKey">The exact state key.</param>
    [Theory]
    [InlineData(null, "team:Business")]
    [InlineData("Marketing Project", "team:Business/Marketing Project")]
    public async Task StateKey_TeamOrProject_NamesTheTeamAndProject(string? project, string expectedKey)
    {
        this.SeedTeam(project is null ? "Business" : "Business/Marketing Project");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx, "Business", project);

        Assert.Equal(expectedKey, cut.FindComponent<LibraryExplorer>().Instance.StateKey);
    }

    /// <summary>The explorer lays the tree and the document side by side.</summary>
    [Fact]
    public async Task Layout_IsSideBySide()
    {
        this.SeedTeam("Business");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx, "Business");

        Assert.Equal(LibraryExplorerLayout.SideBySide, cut.FindComponent<LibraryExplorer>().Instance.Layout);
    }

    /// <summary>The toolbar's action button reads "New note".</summary>
    [Fact]
    public async Task Toolbar_ActionButton_ReadsNewNote()
    {
        this.SeedTeam("Business");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx, "Business");

        Assert.Equal("New note", cut.Find(ActionButton).TextContent.Trim());
    }

    /// <summary>The toolbar's search field carries the placeholder "Search files".</summary>
    [Fact]
    public async Task Toolbar_SearchField_HasThePlaceholderSearchFiles()
    {
        this.SeedTeam("Business");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx, "Business");

        Assert.Equal("Search files", cut.Find(SearchInput).GetAttribute("placeholder"));
    }

    /// <summary>The tab's <c>Search</c> is handed to the explorer as its <c>Filter</c> and shown in the toolbar's field.</summary>
    [Fact]
    public async Task Search_IsPassedToTheExplorerAsFilter_AndShownInTheField()
    {
        this.SeedTeam("Business");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx, "Business", search: "brief");

        Assert.Equal("brief", cut.FindComponent<LibraryExplorer>().Instance.Filter);
        Assert.Equal("brief", cut.Find(SearchInput).GetAttribute("value"));
    }

    /// <summary>Without a <c>Search</c> the explorer gets no filter and shows its tree, not a result list.</summary>
    [Fact]
    public async Task NoSearch_ExplorerHasNoFilter_AndShowsTheTree()
    {
        this.SeedTeam("Business");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx, "Business");

        Assert.Null(cut.FindComponent<LibraryExplorer>().Instance.Filter);
        cut.WaitForAssertion(() => Assert.Equal(["Business"], NodeNames(cut)));
        Assert.Empty(cut.FindAll(".library-search-results"));
    }

    /// <summary>A tab mounted with a <c>Search</c> already set (the remount after a tab switch) searches at once and lists the matches.</summary>
    [Fact]
    public async Task Search_OnFirstRender_ShowsTheResultList()
    {
        this.SeedTeam("Business");
        File.WriteAllText(Path.Combine(this.fixture.TeamsPath, "Business", "brief.md"), "the brief");
        File.WriteAllText(Path.Combine(this.fixture.TeamsPath, "Business", "other.md"), "other");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx, "Business", search: "brief");

        List<string> expected = ["Business/brief.md"];
        cut.WaitForAssertion(() => Assert.Equal(expected, HitPaths(cut)));
    }

    /// <summary>Clicking a FOLDER hit clears the explorer's filter, which the tab raises as <c>SearchChanged(null)</c>, exactly once.</summary>
    [Fact]
    public async Task FolderHit_Click_RaisesSearchChangedWithNull()
    {
        this.SeedTeam("Business/Plans/Q4");
        List<string?> changes = [];

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx, "Business", search: "Q4", changes: changes);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(Hit + "[data-path='Business/Plans/Q4']")));

        await cut.InvokeAsync(() => cut.Find(Hit + "[data-path='Business/Plans/Q4']").ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".library-search-results")));
        Assert.Equal<string?>([null], changes);
    }

    /// <summary>Clearing the toolbar's field raises the tab's <c>SearchChanged</c> with <see langword="null"/>, exactly once.</summary>
    [Fact]
    public async Task ToolbarClear_RaisesSearchChangedWithNull()
    {
        this.SeedTeam("Business");
        List<string?> changes = [];

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx, "Business", search: "brief", changes: changes);
        Assert.Single(cut.FindAll(".mud-input-clear-button"));

        await cut.InvokeAsync(() => cut.Find(".mud-input-clear-button").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal<string?>([null], changes));
    }

    /// <summary>The New note button opens the New note dialog; cancelling it creates and opens nothing.</summary>
    [Fact]
    public async Task NewNote_Button_OpensTheDialog_AndCancelCreatesNothing()
    {
        this.SeedTeam("Business");
        File.WriteAllText(Path.Combine(this.fixture.TeamsPath, "Business", "Existing.md"), "existing");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx, "Business");
        cut.WaitForAssertion(() => Assert.Equal(["Business"], NodeNames(cut)));

        _ = cut.InvokeAsync(() => cut.Find(ActionButton).Click());

        cut.WaitForAssertion(() => Assert.Equal("New note", cut.Find(".mud-dialog-title").TextContent.Trim()));
        await cut.InvokeAsync(() => cut.Find(".library-file-ops-name-field input").ChangeAsync("Idea"));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").First(button => button.TextContent.Trim() == "Cancel").ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-dialog-title")));
        string[] files = [.. Directory.GetFileSystemEntries(Path.Combine(this.fixture.TeamsPath, "Business")).Select(Path.GetFileName).OfType<string>()];
        Assert.Equal(["Existing.md"], files);
        Assert.Empty(cut.FindComponents<LibraryDocument>());
    }

    /// <summary>Confirming the New note dialog writes the note into the Team folder, shows it in the tree AND opens it in the document area.</summary>
    [Fact]
    public async Task NewNote_Confirm_ShowsTheNoteInTheTree_AndOpensIt()
    {
        this.SeedTeam("Business");
        File.WriteAllText(Path.Combine(this.fixture.TeamsPath, "Business", "Existing.md"), "existing");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx, "Business");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("div.mud-treeview-item-arrow button")));
        await cut.InvokeAsync(() => cut.Find("div.mud-treeview-item-arrow button").ClickAsync());
        cut.WaitForAssertion(() => Assert.Equal(["Business", "Existing.md"], SortedNodeNames(cut)));

        _ = cut.InvokeAsync(() => cut.Find(ActionButton).Click());
        cut.WaitForAssertion(() => Assert.Equal("New note", cut.Find(".mud-dialog-title").TextContent.Trim()));
        await cut.InvokeAsync(() => cut.Find(".library-file-ops-name-field input").ChangeAsync("Idea"));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button")[0].ClickAsync());

        cut.WaitForAssertion(() => Assert.True(File.Exists(Path.Combine(this.fixture.TeamsPath, "Business", "Idea.md"))), TimeSpan.FromSeconds(5));
        cut.WaitForAssertion(() => Assert.Equal(["Business", "Existing.md", "Idea.md"], SortedNodeNames(cut)));
        cut.WaitForAssertion(() => Assert.Equal("Business/Idea.md", cut.FindComponent<LibraryDocument>().Instance.Path.RelativePath));
    }

    /// <summary>
    /// A Team page that gains a Project (same tab instance, new parameters) gets a NEW explorer: the explorer
    /// resolves its scopes only in <c>OnInitialized</c>, so the tab keys it by its scope and the tree's top node
    /// becomes the Project.
    /// </summary>
    [Fact]
    public async Task ProjectChange_ReplacesTheExplorer_AndTheTreeShowsTheProject()
    {
        this.SeedTeam("Business/Marketing Project");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx, "Business");
        cut.WaitForAssertion(() => Assert.Equal(["Business"], NodeNames(cut)));
        LibraryExplorer before = cut.FindComponent<LibraryExplorer>().Instance;

        cut.FindComponent<TeamFilesTab>().Render(parameters => parameters.Add(tab => tab.Project, "Marketing Project"));

        cut.WaitForAssertion(() => Assert.Equal(["Marketing Project"], NodeNames(cut)));
        LibraryExplorer after = cut.FindComponent<LibraryExplorer>().Instance;
        Assert.NotSame(before, after);
        IReadOnlyList<LibraryLocation> expected = [new LibraryLocation("teams", "Business/Marketing Project")];
        Assert.Equal(expected, after.Scopes);
    }

    /// <summary>A Team with no folder on disk renders an empty tree (just its own node, nothing to expand), not an alert and not an exception.</summary>
    [Fact]
    public async Task TeamWithoutAFolder_RendersAnEmptyTree()
    {
        Assert.False(Directory.Exists(Path.Combine(this.fixture.TeamsPath, "Ghost")));

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx, "Ghost");

        cut.WaitForAssertion(() => Assert.Equal(["Ghost"], NodeNames(cut)));
        Assert.Empty(cut.FindAll(".library-explorer-unavailable"));
        Assert.Empty(cut.FindAll("div.library-tree-missing"));
        Assert.Empty(cut.FindAll("div.mud-treeview-item-arrow button"));
    }

    /// <summary>Creates the folder <paramref name="relative"/> under the Teams root.</summary>
    private void SeedTeam(string relative) => Directory.CreateDirectory(Path.Combine(this.fixture.TeamsPath, relative));

    private static List<string> NodeNames(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll(NodeName).Select(node => node.TextContent.Trim())];

    private static List<string> SortedNodeNames(IRenderedComponent<ContainerFragment> cut) =>
        [.. NodeNames(cut).Order(StringComparer.Ordinal)];

    private static List<string> HitPaths(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll(Hit).Select(hit => hit.GetAttribute("data-path") ?? string.Empty)];

    private static IRenderedComponent<ContainerFragment> RenderTab(
        MudBunitContext ctx,
        string team,
        string? project = null,
        string? search = null,
        List<string?>? changes = null)
    {
        ctx.JSInterop.SetupVoid("huddleStorage.set", _ => true).SetVoidResult();
        ctx.JSInterop.Setup<string?>("huddleStorage.get", _ => true).SetResult(null);
        List<string?> sink = changes ?? [];
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TeamFilesTab>(0);
            builder.AddAttribute(1, nameof(TeamFilesTab.Team), team);
            builder.AddAttribute(2, nameof(TeamFilesTab.Project), project);
            builder.AddAttribute(3, nameof(TeamFilesTab.Search), search);
            builder.AddAttribute(4, nameof(TeamFilesTab.SearchChanged), EventCallback.Factory.Create<string?>(sink, (Action<string?>)sink.Add));
            builder.CloseComponent();
        });
    }

    /// <summary>The real Library stack over a temp <c>DataDir</c> (a private copy of the Library explorer tests' fixture).</summary>
    private sealed class TabFixture : IDisposable
    {
        private readonly TempDataDir dir;
        private readonly LibraryFileServiceFixture libraryFixture;
        private readonly PersonaStore personas;
        private readonly TaskStore tasks;

        private TabFixture(TempDataDir dir, LibraryFileServiceFixture libraryFixture, PersonaStore personas, TaskStore tasks)
        {
            this.dir = dir;
            this.libraryFixture = libraryFixture;
            this.personas = personas;
            this.tasks = tasks;
        }

        /// <summary>The Teams root on disk, the folder the <c>teams</c> Library Root points at.</summary>
        public string TeamsPath => Path.Combine(this.libraryFixture.DataDir, "Teams");

        /// <summary>Builds a fixture with the standard Teams/Teammates layout.</summary>
        public static TabFixture Build()
        {
            TempDataDir dir = new();
            LibraryFileServiceFixture libraryFixture = LibraryFileServiceFixture.Attach(dir.Path);
            PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
            TaskStore tasks = TestTaskStore.CreateTaskStore(dir, personas);
            return new TabFixture(dir, libraryFixture, personas, tasks);
        }

        /// <summary>A context with the whole Library stack registered, ready to host a <see cref="LibraryExplorer"/> and its parts.</summary>
        public MudBunitContext NewContext()
        {
            MudBunitContext ctx = new();
            ctx.Services.AddSingleton(this.libraryFixture.Resolver);
            ctx.Services.AddSingleton(this.libraryFixture.RootStore);
            ctx.Services.AddSingleton(this.libraryFixture.CreateService());
            ctx.Services.AddSingleton(this.libraryFixture.Index);
            ctx.Services.AddSingleton<ILibraryNoteResolver>(new FakeLibraryNoteResolver());
            ctx.Services.AddSingleton<ITaskReferenceResolver>(new FakeTaskReferenceResolver());
            ctx.Services.AddSingleton(this.tasks);
            ctx.Services.AddSingleton(new TeamFolderProvisioner(
                this.personas, this.libraryFixture.RootStore, this.libraryFixture.Resolver, NullLogger<TeamFolderProvisioner>.Instance, catalog: new FakeTeamCatalog()));
            return ctx;
        }

        /// <summary>Disposes the stores and the temp <c>DataDir</c>.</summary>
        public void Dispose()
        {
            this.tasks.Dispose();
            this.personas.Dispose();
            this.libraryFixture.Dispose();
            this.dir.Dispose();
        }
    }
}
