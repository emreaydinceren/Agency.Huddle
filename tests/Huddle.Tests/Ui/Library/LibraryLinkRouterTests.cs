using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Components.Library;
using Agency.Huddle.App.Library;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>
/// Pins how a <c>?library=</c> link reaches the Library now that there is no docked pane
/// (docs/Huddle.Tabs-Specifications.md section 6): <see cref="LibraryLinkRouter"/> resolves the value
/// and replaces the address with the <c>/library</c> page on that file - the Library's one Tab - or
/// merely strips the query when it resolves to nothing. <see cref="LibraryNavLink"/>'s own root-list
/// and Add Folder behaviour is pinned in <c>LibraryNavLinkTests</c>. A final source-text test pins
/// that <c>MainLayout.razor</c> mounts the router and no longer mentions the pane host.
/// </summary>
public sealed class LibraryLinkRouterTests : IDisposable
{
    private readonly RouterFixture fixture = RouterFixture.Build();

    /// <summary>Disposes the underlying temp <c>DataDir</c> and stores.</summary>
    public void Dispose() => this.fixture.Dispose();

    /// <summary>The sidebar's Library group is titled "Library"; its root rows and Add Folder row are pinned in <c>LibraryNavLinkTests</c>.</summary>
    [Fact]
    public async Task SidebarLibraryGroup_IsTitledLibrary()
    {
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<LibraryNavLink> cut = ctx.Render<LibraryNavLink>();

        // MudNavGroup renders its own title as the first ".mud-nav-link" inside ".mud-nav-group";
        // the root and Add Folder rows nest further inside the same outer <nav> once expanded.
        Assert.Equal("Library", cut.Find(".mud-nav-group .mud-nav-link").TextContent.Trim());
    }

    /// <summary>A <c>?library=</c> navigation to a resolvable file replaces the address with the Library page on that file.</summary>
    [Fact]
    public async Task QueryLibrary_ResolvedFile_NavigatesToTheLibraryPageOnIt()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "a.md"), "# Hello");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        _ = ctx.Render<LibraryLinkRouter>();
        var nav = (BunitNavigationManager)ctx.Services.GetRequiredService<NavigationManager>();

        nav.NavigateTo("rooms/x?library=" + Uri.EscapeDataString(path.Root.Id) + "%2Fa.md");

        Assert.Equal(nav.ToAbsoluteUri(LibraryLinkQuery.BuildPageUri(path)).AbsoluteUri, nav.Uri);
        Assert.DoesNotContain("library=", nav.Uri, StringComparison.Ordinal);
    }

    /// <summary>A <c>?library=</c> value that resolves to nothing (an unknown root) is stripped and the Human stays where they were.</summary>
    [Fact]
    public async Task QueryLibrary_Refused_IsStrippedAndStaysOnThePage()
    {
        await using MudBunitContext ctx = this.fixture.NewContext();
        _ = ctx.Render<LibraryLinkRouter>();
        var nav = (BunitNavigationManager)ctx.Services.GetRequiredService<NavigationManager>();

        nav.NavigateTo("rooms/x?library=" + Uri.EscapeDataString("does-not-exist") + "%2Fa.md");

        Assert.DoesNotContain("library=", nav.Uri, StringComparison.Ordinal);
        Assert.EndsWith("/rooms/x", nav.Uri, StringComparison.Ordinal);
    }

    /// <summary>A forged, non-shape value (a traversal segment, no root) is refused by the resolver too: stripped, nowhere opened.</summary>
    [Fact]
    public async Task QueryLibrary_Forged_IsStrippedAndStaysOnThePage()
    {
        await using MudBunitContext ctx = this.fixture.NewContext();
        _ = ctx.Render<LibraryLinkRouter>();
        var nav = (BunitNavigationManager)ctx.Services.GetRequiredService<NavigationManager>();

        nav.NavigateTo("rooms/x?library=..%2Fx");

        Assert.DoesNotContain("library=", nav.Uri, StringComparison.Ordinal);
        Assert.DoesNotContain("/library", nav.Uri, StringComparison.Ordinal);
    }

    /// <summary>With the Library disabled the link row renders nothing and a <c>?library=</c> URL is left alone entirely.</summary>
    [Fact]
    public async Task LibraryDisabled_NoLinkAndQueryLeftAlone()
    {
        await using MudBunitContext ctx = this.fixture.NewContext(libraryEnabled: false);
        IRenderedComponent<LibraryNavLink> navCut = ctx.Render<LibraryNavLink>();
        _ = ctx.Render<LibraryLinkRouter>();
        var nav = (BunitNavigationManager)ctx.Services.GetRequiredService<NavigationManager>();

        nav.NavigateTo("rooms/x?library=notes%2Fa.md");

        Assert.Empty(navCut.FindAll(".mud-nav-link"));
        Assert.Equal(nav.ToAbsoluteUri("rooms/x?library=notes%2Fa.md").ToString(), nav.Uri);
    }

    /// <summary>Source-text pin (the repo's idiom, copied from <c>ProcessModeTests.FindRepoRoot</c>): <c>MainLayout.razor</c> mounts <c>&lt;LibraryLinkRouter&gt;</c> after the Library link, mounts the tab host (which draws the pages itself, so no <c>@Body</c> is rendered), and no longer names the retired pane host.</summary>
    [Fact]
    public void MainLayoutSource_MountsTheRouter_AndHasNoPaneHost()
    {
        string repoRoot = FindRepoRoot();
        string text = File.ReadAllText(Path.Combine(repoRoot, "src", "Huddle.App", "Components", "Layout", "MainLayout.razor"));

        int taskViewNavIndex = text.IndexOf("<TaskViewNav", StringComparison.Ordinal);
        int libraryNavLinkIndex = text.IndexOf("<LibraryNavLink", StringComparison.Ordinal);
        int tabHostIndex = text.IndexOf("<TabHost", StringComparison.Ordinal);

        Assert.True(taskViewNavIndex > 0);
        Assert.True(libraryNavLinkIndex > taskViewNavIndex);
        Assert.Contains("<LibraryLinkRouter", text, StringComparison.Ordinal);
        Assert.True(tabHostIndex > 0);
        Assert.DoesNotContain("@Body", text, StringComparison.Ordinal);
        Assert.DoesNotContain("LibraryPaneHost", text, StringComparison.Ordinal);
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

    private sealed class RouterFixture : IDisposable
    {
        private readonly TempDataDir dir;

        private RouterFixture(TempDataDir dir, LibraryFileServiceFixture libraryFixture)
        {
            this.dir = dir;
            this.LibraryFixture = libraryFixture;
        }

        /// <summary>The real Library stack (resolver, root store, file service) over this fixture's temp <c>DataDir</c>.</summary>
        public LibraryFileServiceFixture LibraryFixture { get; }

        /// <summary>Builds a fixture with the standard Teams/Teammates layout.</summary>
        public static RouterFixture Build()
        {
            TempDataDir dir = new();
            return new RouterFixture(dir, LibraryFileServiceFixture.Attach(dir.Path));
        }

        /// <summary>A <see cref="MudBunitContext"/> with the Library stack and the <c>Library.Enabled</c> gate registered.</summary>
        /// <param name="libraryEnabled">Whether <c>Library.Enabled</c> is set for this context.</param>
        public MudBunitContext NewContext(bool libraryEnabled = true)
        {
            MudBunitContext ctx = new();
            IOptions<TeamOptions> options = this.dir.Options();
            options.Value.Library.Enabled = libraryEnabled;
            ctx.Services.AddSingleton(options);
            ctx.Services.AddSingleton(this.LibraryFixture.Resolver);
            ctx.Services.AddSingleton(this.LibraryFixture.RootStore);
            ctx.Services.AddSingleton<Agency.Huddle.App.Tabs.TabService>();
            return ctx;
        }

        /// <summary>Disposes the underlying stores and temp <c>DataDir</c>.</summary>
        public void Dispose()
        {
            this.LibraryFixture.Dispose();
            this.dir.Dispose();
        }
    }
}
