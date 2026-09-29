using Bunit;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Library;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>
/// Pins the sidebar's Library group (<see cref="LibraryNavLink"/>, extended for the root list):
/// mirrors <c>TaskViewNav</c>'s own shape - one row per <see cref="LibraryRootStore.VisibleRoots"/>
/// (a hidden built-in is left out), each row's own Href routes to the full <c>/library</c> page
/// scoped to that root (Spec §6.9), landing the Human on Library as its own primary panel rather
/// than opening a docked pane next to whatever page they were on - and a trailing "Add Folder" row
/// opens <see cref="AddFolderDialog"/>, the same pin flow and refusal text <c>LibraryPanelTests</c>
/// pins for Settings > Library's own Add row. The group re-renders on
/// <see cref="LibraryRootStore.RootsChanged"/> from anywhere - this dialog, Settings, or a reset -
/// and renders nothing when <c>Library.Enabled</c> is <see langword="false"/>. The docked
/// <see cref="LibraryPaneState"/> pane this row used to open is pinned separately, directly against
/// that state, in <c>MainLayoutLibraryTests</c>.
/// </summary>
public sealed class LibraryNavLinkTests
{
    /// <summary>The rows list the built-ins in <see cref="LibraryRootStore.Roots"/>'s own order: Teams then Teammates.</summary>
    [Fact]
    public async Task ListsVisibleRoots_TeamsThenTeammates()
    {
        using NavFixture fixture = NavFixture.Build();
        await using MudBunitContext ctx = fixture.NewContext();

        var cut = ctx.RenderWithPopovers(RenderNav);

        Assert.Equal(["Teams", "Teammates"], cut.FindAll(".library-nav-root-link").Select(e => e.TextContent.Trim()));
    }

    /// <summary>A built-in hidden through <see cref="LibraryRootStore.SetHidden"/> is left out of the rows, matching <see cref="LibraryRootStore.VisibleRoots"/>.</summary>
    [Fact]
    public async Task HiddenBuiltIn_IsNotListed()
    {
        using NavFixture fixture = NavFixture.Build();
        fixture.Store.SetHidden("teams", hidden: true);
        await using MudBunitContext ctx = fixture.NewContext();

        var cut = ctx.RenderWithPopovers(RenderNav);

        Assert.Equal(["Teammates"], cut.FindAll(".library-nav-root-link").Select(e => e.TextContent.Trim()));
    }

    /// <summary>Each root row's own Href routes to the full <c>/library</c> page, scoped to that root's own top level (Spec §6.9) - the same query shape <c>LibraryPageTests</c> pins for the page's own <c>ScopeRoot</c>/<c>ScopePath</c> parameters.</summary>
    [Fact]
    public async Task RootRow_Href_PointsToTheScopedLibraryPage()
    {
        using NavFixture fixture = NavFixture.Build();
        await using MudBunitContext ctx = fixture.NewContext();

        var cut = ctx.RenderWithPopovers(RenderNav);

        Assert.Equal(
            "/library?scopeRoot=teams&scopePath=",
            cut.FindAll(".library-nav-root-link").Single(e => string.Equals(e.TextContent.Trim(), "Teams", StringComparison.Ordinal)).QuerySelector("a")?.GetAttribute("href"));
        Assert.Equal(
            "/library?scopeRoot=teammates&scopePath=",
            cut.FindAll(".library-nav-root-link").Single(e => string.Equals(e.TextContent.Trim(), "Teammates", StringComparison.Ordinal)).QuerySelector("a")?.GetAttribute("href"));
    }

    /// <summary>Adding an existing folder through the dialog pins it via <see cref="LibraryRootStore.Save"/>, closes the dialog, and the new row shows up live.</summary>
    [Fact]
    public async Task AddFolder_ValidFolder_SavesAndShowsUpLive()
    {
        using NavFixture fixture = NavFixture.Build();
        string newPath = Path.Combine(fixture.DataDir, "Notes");
        Directory.CreateDirectory(newPath);
        await using MudBunitContext ctx = fixture.NewContext();
        var cut = ctx.RenderWithPopovers(RenderNav);

        _ = cut.InvokeAsync(() => cut.Find(".nav-action-link .mud-nav-link").Click());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));
        Assert.Equal("Add folder", cut.Find(".mud-dialog-title").TextContent.Trim());

        await cut.InvokeAsync(() => cut.Find(".add-folder-dialog-name-field input").ChangeAsync("Notes"));
        await cut.InvokeAsync(() => cut.Find(".add-folder-dialog-path-field input").ChangeAsync(newPath));
        await cut.InvokeAsync(() => cut.Find(".add-folder-dialog-add-button").ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-dialog-title")));
        Assert.Equal(["Notes"], fixture.Store.Pinned.Select(p => p.Name));
        cut.WaitForAssertion(() => Assert.Equal(
            ["Teams", "Teammates", "Notes"],
            cut.FindAll(".library-nav-root-link").Select(e => e.TextContent.Trim())));
    }

    /// <summary>A path that does not exist on disk is refused inline with the Spec's settled text; the dialog stays open and nothing is saved.</summary>
    [Fact]
    public async Task AddFolder_MissingFolder_RefusedInline_DialogStaysOpen()
    {
        using NavFixture fixture = NavFixture.Build();
        string missingPath = Path.Combine(fixture.DataDir, "DoesNotExist");
        await using MudBunitContext ctx = fixture.NewContext();
        var cut = ctx.RenderWithPopovers(RenderNav);

        _ = cut.InvokeAsync(() => cut.Find(".nav-action-link .mud-nav-link").Click());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));

        await cut.InvokeAsync(() => cut.Find(".add-folder-dialog-name-field input").ChangeAsync("Ghost"));
        await cut.InvokeAsync(() => cut.Find(".add-folder-dialog-path-field input").ChangeAsync(missingPath));
        await cut.InvokeAsync(() => cut.Find(".add-folder-dialog-add-button").ClickAsync());

        Assert.Equal("That folder doesn't exist.", cut.Find(".add-folder-dialog-error").TextContent.Trim());
        Assert.NotEmpty(cut.FindAll(".mud-dialog-title"));
        Assert.Empty(fixture.Store.Pinned);
    }

    /// <summary>A pin saved from outside this component (Settings, another tab) still re-renders the row list.</summary>
    [Fact]
    public async Task RootsChanged_FromOutside_ReRendersList()
    {
        using NavFixture fixture = NavFixture.Build();
        await using MudBunitContext ctx = fixture.NewContext();
        var cut = ctx.RenderWithPopovers(RenderNav);
        Assert.Equal(["Teams", "Teammates"], cut.FindAll(".library-nav-root-link").Select(e => e.TextContent.Trim()));

        string outsidePath = Path.Combine(fixture.DataDir, "Outside");
        Directory.CreateDirectory(outsidePath);
        fixture.Store.Save([new PinnedRootEntry("Outside", outsidePath)]);

        cut.WaitForAssertion(() => Assert.Equal(
            ["Teams", "Teammates", "Outside"],
            cut.FindAll(".library-nav-root-link").Select(e => e.TextContent.Trim())));
    }

    /// <summary>With <c>Library.Enabled</c> off, the component renders nothing at all.</summary>
    [Fact]
    public async Task LibraryDisabled_RendersNothing()
    {
        using NavFixture fixture = NavFixture.Build();
        await using MudBunitContext ctx = fixture.NewContext(libraryEnabled: false);

        var cut = ctx.Render<LibraryNavLink>();

        Assert.Equal(string.Empty, cut.Markup.Trim());
    }

    private static void RenderNav(RenderTreeBuilder builder)
    {
        builder.OpenComponent<LibraryNavLink>(0);
        builder.CloseComponent();
    }

    /// <summary>A fixture over a real <see cref="LibraryRootStore"/> and temp <c>DataDir</c>, matching <c>LibraryPanelTests.PanelFixture</c>'s own shape.</summary>
    private sealed class NavFixture : IDisposable
    {
        private readonly TempDataDir dataDir;

        private NavFixture(TempDataDir dataDir, IOptions<TeamOptions> options, LibraryRootStore store)
        {
            this.dataDir = dataDir;
            this.Options = options;
            this.Store = store;
        }

        /// <summary>The bound <see cref="TeamOptions"/>, mutable through <see cref="IOptions{TOptions}.Value"/> for the <c>Library.Enabled</c> gate.</summary>
        public IOptions<TeamOptions> Options { get; }

        /// <summary>The real <see cref="LibraryRootStore"/> under test.</summary>
        public LibraryRootStore Store { get; }

        /// <summary>The temp <c>DataDir</c> this fixture's roots live under.</summary>
        public string DataDir => this.dataDir.Path;

        /// <summary>Builds a fixture with the standard, empty Teams/Teammates layout and no pinned roots.</summary>
        public static NavFixture Build()
        {
            TempDataDir dataDir = new();
            IOptions<TeamOptions> options = dataDir.Options();
            TeammatePaths paths = new(options);
            LibraryRootStore store = new(options, paths, NullLogger<LibraryRootStore>.Instance);
            return new NavFixture(dataDir, options, store);
        }

        /// <summary>A <see cref="MudBunitContext"/> with this fixture's real <see cref="LibraryRootStore"/> registered.</summary>
        /// <param name="libraryEnabled">Whether <c>Library.Enabled</c> is set for this context.</param>
        public MudBunitContext NewContext(bool libraryEnabled = true)
        {
            this.Options.Value.Library.Enabled = libraryEnabled;
            MudBunitContext ctx = new();
            ctx.Services.AddSingleton(this.Options);
            ctx.Services.AddSingleton(this.Store);
            return ctx;
        }

        /// <summary>Disposes the underlying temp <c>DataDir</c>.</summary>
        public void Dispose() => this.dataDir.Dispose();
    }
}
