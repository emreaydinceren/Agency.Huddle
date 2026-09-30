using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Library;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Tasks;
using Agency.Huddle.Tests.Teams;
using LibraryPage = Agency.Huddle.App.Components.Pages.Library;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>
/// Pins the `/library` page (Task 13.3): query parameter binding, rendering the
/// <see cref="LibraryExplorer"/> at full width with SideBySide layout, scope and file opening,
/// and the Library.Enabled gate.
/// </summary>
public sealed class LibraryPageTests : IDisposable
{
    private readonly ExplorerFixture fixture = ExplorerFixture.Build();

    /// <summary>Disposes the underlying temp <c>DataDir</c> and stores.</summary>
    public void Dispose() => this.fixture.Dispose();

    /// <summary>
    /// The page renders the <c>LibraryExplorer</c> with <c>Layout</c> SideBySide and <c>StateKey</c> "page",
    /// which passes the underlying <see cref="MudSplitPanel"/> <c>Horizontal="false"</c> - a vertical
    /// dividing line, panels left/right (see <c>LibraryExplorerTests.Layout_SideBySide_IsHorizontal</c>
    /// for why that reads backwards from the parameter's name).
    /// </summary>
    [Fact]
    public async Task Page_RendersSideBySideExplorer()
    {
        this.fixture.LibraryFixture.CreatePinnedRoot("Notes");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, root: null, path: null, scopeRoot: null, scopePath: null);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<MudSplitPanel>()));
        Assert.False(cut.FindComponent<MudSplitPanel>().Instance.Horizontal);
    }

    /// <summary><c>?root=…&amp;path=…</c> query parameters open that file on the page.</summary>
    [Fact]
    public async Task Page_RootAndPathQuery_OpensFile()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "Test.md"), "test body");
        LibraryPath filePath = this.fixture.LibraryFixture.Resolve(root, "Test.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderPage(
            ctx, root: filePath.Root.Id, path: filePath.RelativePath, scopeRoot: null, scopePath: null);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-rendered")));
    }

    /// <summary>The Pop out URL built by <see cref="LibraryPane"/>, parsed back into query parameters and fed
    /// to the page, opens the same file (Spec §6.9 "Pop out").</summary>
    [Fact]
    public async Task Page_PopOutUrl_RoundTrips()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        Directory.CreateDirectory(Path.Combine(root, "My Folder"));
        File.WriteAllText(Path.Combine(root, "My Folder", "Test.md"), "test body");
        LibraryPath filePath = this.fixture.LibraryFixture.Resolve(root, "My Folder/Test.md");

        string popOutUri = LibraryPane.BuildPopOutUri(filePath);
        string rawQuery = popOutUri[popOutUri.IndexOf('?', StringComparison.Ordinal)..];
        Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query =
            Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(rawQuery);
        Assert.Equal(filePath.Root.Id, query["root"].ToString());
        Assert.Equal(filePath.RelativePath, query["path"].ToString());

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderPage(
            ctx, root: query["root"].ToString(), path: query["path"].ToString(), scopeRoot: null, scopePath: null);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-document-path")));
        AngleSharp.Dom.IElement title = cut.Find(".library-document-path");
        Assert.Equal("My Folder / Test.md", title.TextContent.Trim());
    }

    /// <summary>An unknown root in the query shows the explorer with no file open, and no exception.</summary>
    [Fact]
    public async Task Page_BadRootQuery_ShowsExplorerWithoutFile()
    {
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderPage(
            ctx, root: "does-not-exist", path: "some/path", scopeRoot: null, scopePath: null);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-explorer")));
        Assert.Empty(cut.FindAll(".library-rendered"));
    }

    /// <summary>
    /// <c>?scopeRoot=teams&amp;scopePath=Marketing</c> keeps the scope and, since the only current
    /// producer of that query (<c>LibraryNavLink</c>'s own sidebar rows) already names the folder
    /// there, the page passes <c>ShowScopeRoot=false</c>: no redundant title above the tree (just the
    /// root's full path as a header), and
    /// <see cref="LibraryTree"/> flattens straight to the scoped folder's own children instead of a
    /// wrapper row that exists only to be expanded.
    /// </summary>
    [Fact]
    public async Task Page_ScopeQuery_FlattensToTheScopesOwnChildren()
    {
        Directory.CreateDirectory(Path.Combine(this.fixture.LibraryFixture.DataDir, "Teams", "Marketing", "Campaigns"));

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderPage(
            ctx, root: null, path: null, scopeRoot: "teams", scopePath: "Marketing");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-explorer")));
        Assert.Empty(cut.FindAll(".library-explorer-title"));
        Assert.Equal(
            Path.Combine(this.fixture.LibraryFixture.DataDir, "Teams", "Marketing"),
            cut.Find(".library-explorer-root-path").TextContent.Trim());
        cut.WaitForAssertion(() => Assert.Equal(
            ["Campaigns"],
            cut.FindAll(".library-tree-node-name").Select(e => e.TextContent.Trim())));
    }

    /// <summary>An unresolvable scope shows the explorer's unavailable alert.</summary>
    [Fact]
    public async Task Page_ScopeQuery_Unresolvable_ShowsUnavailableAlert()
    {
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderPage(
            ctx, root: null, path: null, scopeRoot: "does-not-exist", scopePath: "NoSuchPath");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-explorer-unavailable")));
        AngleSharp.Dom.IElement alert = cut.Find(".library-explorer-unavailable");
        Assert.Equal("This folder isn't available in the Library.", alert.TextContent.Trim());
    }

    /// <summary>When <c>Library.Enabled</c> is <see langword="false"/>, the page shows the settled disabled text.</summary>
    [Fact]
    public async Task Page_LibraryDisabled_ShowsText()
    {
        await using MudBunitContext ctx = this.fixture.NewContext(libraryEnabled: false);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, root: null, path: null, scopeRoot: null, scopePath: null);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-disabled-alert")));
        AngleSharp.Dom.IElement alert = cut.Find(".library-disabled-alert");
        Assert.Equal("The Library is turned off.", alert.TextContent.Trim());
        Assert.Empty(cut.FindAll(".library-explorer"));
    }

    /// <summary>Renders the Library page with the given query parameters.</summary>
    private static IRenderedComponent<ContainerFragment> RenderPage(
        MudBunitContext ctx,
        string? root,
        string? path,
        string? scopeRoot,
        string? scopePath)
    {
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<LibraryPage>(0);
            builder.AddAttribute(1, nameof(LibraryPage.Root), root);
            builder.AddAttribute(2, nameof(LibraryPage.Path), path);
            builder.AddAttribute(3, nameof(LibraryPage.ScopeRoot), scopeRoot);
            builder.AddAttribute(4, nameof(LibraryPage.ScopePath), scopePath);
            builder.CloseComponent();
        });
    }

    /// <summary>Fixture providing a Library stack with temp DataDir and stores.</summary>
    private sealed class ExplorerFixture : IDisposable
    {
        private readonly TempDataDir dir;
        private readonly PersonaStore personas;
        private readonly TaskStore tasks;

        private ExplorerFixture(TempDataDir dir, LibraryFileServiceFixture libraryFixture, PersonaStore personas, TaskStore tasks)
        {
            this.dir = dir;
            this.LibraryFixture = libraryFixture;
            this.personas = personas;
            this.tasks = tasks;
        }

        /// <summary>The real Library stack (resolver, root store, file service, wikilink index) over this
        /// fixture's temp <c>DataDir</c>.</summary>
        public LibraryFileServiceFixture LibraryFixture { get; }

        /// <summary>Builds a fixture with the standard Teams/Teammates layout.</summary>
        public static ExplorerFixture Build()
        {
            TempDataDir dir = new();
            LibraryFileServiceFixture libraryFixture = LibraryFileServiceFixture.Attach(dir.Path);
            PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
            TaskStore tasks = TestTaskStore.CreateTaskStore(dir, personas);
            return new ExplorerFixture(dir, libraryFixture, personas, tasks);
        }

        /// <summary>A <see cref="MudBunitContext"/> with the whole Library stack registered and Library.Enabled
        /// set as specified, ready to host the Library page and its parts.</summary>
        public MudBunitContext NewContext(bool libraryEnabled = true)
        {
            MudBunitContext ctx = new();
            ctx.Services.AddSingleton(this.LibraryFixture.Resolver);
            ctx.Services.AddSingleton(this.LibraryFixture.RootStore);
            ctx.Services.AddSingleton(this.LibraryFixture.CreateService());
            ctx.Services.AddSingleton(this.LibraryFixture.Index);
            ctx.Services.AddSingleton<ILibraryNoteResolver>(new FakeLibraryNoteResolver());
            ctx.Services.AddSingleton<ITaskReferenceResolver>(new FakeTaskReferenceResolver());
            ctx.Services.AddSingleton(this.tasks);
            ctx.Services.AddSingleton(new TeamFolderProvisioner(
                this.personas, this.LibraryFixture.RootStore, this.LibraryFixture.Resolver, NullLogger<TeamFolderProvisioner>.Instance, catalog: new FakeTeamCatalog()));
            ctx.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new TeamOptions
            {
                Library = new LibraryOptions { Enabled = libraryEnabled },
            }));

            return ctx;
        }

        /// <summary>Disposes the underlying stores and temp <c>DataDir</c>.</summary>
        public void Dispose()
        {
            this.tasks.Dispose();
            this.personas.Dispose();
            this.LibraryFixture.Dispose();
            this.dir.Dispose();
        }
    }
}
