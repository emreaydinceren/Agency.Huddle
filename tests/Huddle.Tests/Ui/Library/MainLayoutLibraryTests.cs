using Bunit;
using Bunit.Rendering;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MudBlazor;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Appearance;
using Agency.Huddle.App.Components.Library;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>
/// Pins the Library pane's host (Task 13.2, Spec §6.9, §6.6): <see cref="LibraryPaneHost"/> wraps the
/// page body in a <see cref="MudSplitPanel"/> only while the pane is open, panel order follows
/// <see cref="AppearanceStore.LibraryPaneSide"/>, and a <c>?library=</c> navigation the pane's own
/// inner document doesn't intercept opens the pane and strips the query. <see cref="LibraryNavLink"/>'s
/// own root-list and Add Folder behaviour is pinned separately in <c>LibraryNavLinkTests</c>; this
/// file exercises it only through <see cref="LibraryPaneState"/> directly. The precedence rule (judgement 45,
/// corrections-B6 item 19): the pane's inner <see cref="LibraryExplorer"/> has no <c>Scopes</c>, so
/// its <see cref="LibraryDocument"/>'s own <c>NavigationManager.RegisterLocationChangingHandler</c>
/// intercepts and prevents every resolvable <c>?library=</c> navigation while a document is open in
/// the pane; <see cref="LibraryPaneHost"/> only ever sees <c>LocationChanged</c> for a navigation
/// nobody cancelled (pane closed, or nothing currently open in it).
/// A final source-text test (copying <c>FindRepoRoot</c> from
/// <c>tests/Huddle.Tests/Conformance/ProcessModeTests.cs</c>) pins that <c>MainLayout.razor</c> itself
/// changes by exactly the two new tags this task adds.
/// </summary>
public sealed class MainLayoutLibraryTests : IDisposable
{
    private readonly HostFixture fixture = HostFixture.Build();

    /// <summary>Disposes the underlying temp <c>DataDir</c> and stores.</summary>
    public void Dispose() => this.fixture.Dispose();

    /// <summary>Closed, <see cref="LibraryPaneHost"/> renders no <see cref="MudSplitPanel"/> at all: only <c>ChildContent</c>, full width.</summary>
    [Fact]
    public async Task Closed_NoSplitPanel_BodyFullWidth()
    {
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderHost(ctx);

        Assert.Empty(cut.FindComponents<MudSplitPanel>());
        Assert.NotEmpty(cut.FindAll(".pane-host-body-placeholder"));
    }

    /// <summary>The sidebar's Library group is titled "Library"; <see cref="LibraryNavLink"/>'s own root rows and Add Folder row are pinned in <c>LibraryNavLinkTests</c>.</summary>
    [Fact]
    public async Task SidebarLibraryGroup_IsTitledLibrary()
    {
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<LibraryNavLink> cut = ctx.Render<LibraryNavLink>();

        // MudNavGroup renders its own title as the first ".mud-nav-link" inside ".mud-nav-group";
        // the root and Add Folder rows nest further inside the same outer <nav> once expanded.
        Assert.Equal("Library", cut.Find(".mud-nav-group .mud-nav-link").TextContent.Trim());
    }

    /// <summary>
    /// Clicking a second root row while the pane is already open actually re-renders the visible
    /// pane - not just <see cref="LibraryPaneState.Scope"/> underneath it. Neither
    /// <c>&lt;FirstPanel&gt;&lt;LibraryPane /&gt;&lt;/FirstPanel&gt;</c> nor its <c>SecondPanel</c>
    /// counterpart in <see cref="LibraryPaneHost"/> reference any instance state, so without
    /// <c>@key="State.Scope"</c> on <see cref="LibraryPane"/> the Razor compiler's static-render-
    /// fragment caching lets the renderer skip that whole region on every subsequent
    /// <see cref="LibraryPaneHost"/> render: the pane opened correctly on the first root clicked and
    /// then never visibly changed again, however many different roots were clicked afterwards -
    /// confirmed live in the browser before this test was added, since every prior test in this
    /// file only ever opens the pane once per case.
    /// </summary>
    [Fact]
    public async Task OpeningASecondRootRow_WhilePaneAlreadyOpen_UpdatesTheVisiblePane()
    {
        Directory.CreateDirectory(Path.Combine(this.fixture.LibraryFixture.DataDir, "Teams", "Engineering"));
        this.fixture.LibraryFixture.CreateTeammate("jarvis", "Jarvis", "jar");

        await using MudBunitContext ctx = this.fixture.NewContext();
        LibraryPaneState state = ctx.Services.GetRequiredService<LibraryPaneState>();
        IRenderedComponent<ContainerFragment> cut = RenderHost(ctx);

        // Scoped from the sidebar, the tree flattens straight to the root's own children
        // (ShowScopeRoot=false, LibraryTreeTests.ShowScopeRootFalse_FlattensToTheScopesOwnChildren),
        // so this checks which children are visible rather than a wrapper row's own name.
        _ = cut.InvokeAsync(() => state.OpenRoot(new LibraryLocation("teams", string.Empty)));
        cut.WaitForAssertion(() => Assert.Equal(
            ["Engineering"],
            cut.FindAll(".library-tree-node-name").Select(e => e.TextContent.Trim())));

        _ = cut.InvokeAsync(() => state.OpenRoot(new LibraryLocation("teammates", string.Empty)));

        cut.WaitForAssertion(() => Assert.Equal(
            ["jarvis"],
            cut.FindAll(".library-tree-node-name").Select(e => e.TextContent.Trim())));
    }

    /// <summary>With the side set to Right (default), the split panel's first panel is the body and the second is the pane.</summary>
    [Fact]
    public async Task Open_Right_BodyThenPane()
    {
        await using MudBunitContext ctx = this.fixture.NewContext();
        LibraryPaneState state = ctx.Services.GetRequiredService<LibraryPaneState>();
        IRenderedComponent<ContainerFragment> cut = RenderHost(ctx);

        _ = cut.InvokeAsync(state.Toggle);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<MudSplitPanel>()));

        int bodyIndex = cut.Markup.IndexOf("pane-host-body-placeholder", StringComparison.Ordinal);
        int paneIndex = cut.Markup.IndexOf("library-explorer", StringComparison.Ordinal);
        Assert.True(bodyIndex >= 0 && paneIndex >= 0 && bodyIndex < paneIndex);
    }

    /// <summary>With the side set to Left, the split panel's first panel is the pane and the second is the body.</summary>
    [Fact]
    public async Task Open_Left_PaneThenBody()
    {
        await using MudBunitContext ctx = this.fixture.NewContext();
        this.fixture.Appearance.SaveLibraryPaneSide(LibraryPaneSide.Left);
        LibraryPaneState state = ctx.Services.GetRequiredService<LibraryPaneState>();
        IRenderedComponent<ContainerFragment> cut = RenderHost(ctx);

        _ = cut.InvokeAsync(state.Toggle);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<MudSplitPanel>()));

        int bodyIndex = cut.Markup.IndexOf("pane-host-body-placeholder", StringComparison.Ordinal);
        int paneIndex = cut.Markup.IndexOf("library-explorer", StringComparison.Ordinal);
        Assert.True(bodyIndex >= 0 && paneIndex >= 0 && paneIndex < bodyIndex);
    }

    /// <summary>Flipping the Library Pane side setting while the pane is open re-renders it in the new order (<see cref="AppearanceStore.AppearanceChanged"/>).</summary>
    [Fact]
    public async Task SideFlips_ReordersPanelsLive()
    {
        await using MudBunitContext ctx = this.fixture.NewContext();
        LibraryPaneState state = ctx.Services.GetRequiredService<LibraryPaneState>();
        IRenderedComponent<ContainerFragment> cut = RenderHost(ctx);
        _ = cut.InvokeAsync(state.Toggle);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<MudSplitPanel>()));
        Assert.True(cut.Markup.IndexOf("pane-host-body-placeholder", StringComparison.Ordinal)
            < cut.Markup.IndexOf("library-explorer", StringComparison.Ordinal));

        this.fixture.Appearance.SaveLibraryPaneSide(LibraryPaneSide.Left);

        cut.WaitForAssertion(() => Assert.True(
            cut.Markup.IndexOf("library-explorer", StringComparison.Ordinal)
            < cut.Markup.IndexOf("pane-host-body-placeholder", StringComparison.Ordinal)));
    }

    /// <summary>A <c>?library=</c> navigation to a resolvable file, with the pane closed, opens the pane on that file and strips the query with <c>replace: true</c> (no new history entry).</summary>
    [Fact]
    public async Task QueryLibrary_OpensPaneOnFile_AndStripsQuery()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "a.md"), "# Hello");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderHost(ctx);
        var nav = (BunitNavigationManager)ctx.Services.GetRequiredService<NavigationManager>();
        int historyBefore = nav.History.Count;

        nav.NavigateTo("rooms/x?library=" + Uri.EscapeDataString(path.Root.Id) + "%2Fa.md");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<MudSplitPanel>()));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-rendered h1")));
        Assert.Equal("Hello", cut.Find(".library-rendered h1").TextContent.Trim());
        Assert.DoesNotContain("library=", nav.Uri, StringComparison.Ordinal);
        Assert.Equal(historyBefore + 1, nav.History.Count);
    }

    /// <summary>While the pane is already open with a document, a <c>?library=</c> click on a new file is
    /// handled in place by the pane's own document (judgement 45): the pane stays open on the new file,
    /// and the query is stripped exactly once (<see cref="LibraryPaneHost"/>'s own handler never also fires).</summary>
    [Fact]
    public async Task QueryLibrary_WhilePaneOpen_OpensInPlace_NoDoubleHandling()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "a.md"), "# First");
        File.WriteAllText(Path.Combine(root, "b.md"), "# Second");
        LibraryPath a = this.fixture.LibraryFixture.Resolve(root, "a.md");
        LibraryPath b = this.fixture.LibraryFixture.Resolve(root, "b.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderHost(ctx);
        var nav = (BunitNavigationManager)ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("rooms/x?library=" + Uri.EscapeDataString(a.Root.Id) + "%2Fa.md");
        cut.WaitForAssertion(() => Assert.Equal("First", cut.Find(".library-rendered h1").TextContent.Trim()));
        int historyAfterFirst = nav.History.Count;

        nav.NavigateTo("rooms/x?library=" + Uri.EscapeDataString(b.Root.Id) + "%2Fb.md");

        cut.WaitForAssertion(() => Assert.Equal("Second", cut.Find(".library-rendered h1").TextContent.Trim()));
        Assert.NotEmpty(cut.FindComponents<MudSplitPanel>());
        Assert.DoesNotContain("library=", nav.Uri, StringComparison.Ordinal);
        Assert.Equal(historyAfterFirst + 1, nav.History.Count);
    }

    /// <summary>A <c>?library=</c> value that resolves to nothing (an unknown root) leaves the pane closed, and the query is still stripped.</summary>
    [Fact]
    public async Task QueryLibrary_Refused_IsIgnoredAndStripped()
    {
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderHost(ctx);
        var nav = (BunitNavigationManager)ctx.Services.GetRequiredService<NavigationManager>();

        nav.NavigateTo("rooms/x?library=" + Uri.EscapeDataString("does-not-exist") + "%2Fa.md");

        cut.WaitForAssertion(() => Assert.DoesNotContain("library=", nav.Uri, StringComparison.Ordinal));
        Assert.Empty(cut.FindComponents<MudSplitPanel>());
    }

    /// <summary>A forged, non-shape value (a traversal segment, no root) is refused by the resolver too: the pane stays closed.</summary>
    [Fact]
    public async Task QueryLibrary_Forged_IsIgnored()
    {
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderHost(ctx);
        var nav = (BunitNavigationManager)ctx.Services.GetRequiredService<NavigationManager>();

        nav.NavigateTo("rooms/x?library=..%2Fx");

        cut.WaitForAssertion(() => Assert.DoesNotContain("library=", nav.Uri, StringComparison.Ordinal));
        Assert.Empty(cut.FindComponents<MudSplitPanel>());
    }

    /// <summary>Closing the pane (toggling it off) saves the outer split's divider position under <c>library:pane:divider</c>.</summary>
    [Fact]
    public async Task PaneClose_StoresDivider()
    {
        await using MudBunitContext ctx = this.fixture.NewContext();
        ctx.JSInterop.SetupVoid("huddleStorage.set", _ => true).SetVoidResult();
        ctx.JSInterop.Setup<int>("huddleSplitPanel.getDividerPosition", _ => true).SetResult(420);
        LibraryPaneState state = ctx.Services.GetRequiredService<LibraryPaneState>();
        IRenderedComponent<ContainerFragment> cut = RenderHost(ctx);
        _ = cut.InvokeAsync(state.Toggle);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<MudSplitPanel>()));

        _ = cut.InvokeAsync(state.Toggle);

        cut.WaitForAssertion(() => ctx.JSInterop.VerifyInvoke("huddleStorage.set"));
        var invocations = ctx.JSInterop.Invocations["huddleStorage.set"];
        Assert.Equal("library:pane:divider", invocations[^1].Arguments[0]);
    }

    /// <summary>The Library disabled: <see cref="LibraryNavLink"/> renders nothing, and <see cref="LibraryPaneHost"/> never opens a split panel.</summary>
    [Fact]
    public async Task LibraryDisabled_NoLinkNoPane()
    {
        await using MudBunitContext ctx = this.fixture.NewContext(libraryEnabled: false);
        IRenderedComponent<LibraryNavLink> navCut = ctx.Render<LibraryNavLink>();
        IRenderedComponent<ContainerFragment> hostCut = RenderHost(ctx);

        Assert.Empty(navCut.FindAll(".mud-nav-link"));
        Assert.Empty(hostCut.FindComponents<MudSplitPanel>());
    }

    /// <summary>The Library disabled, a <c>?library=</c> URL is left alone entirely: no pane opens and the query is not stripped.</summary>
    [Fact]
    public async Task LibraryDisabled_QueryLibrary_LeftAlone()
    {
        await using MudBunitContext ctx = this.fixture.NewContext(libraryEnabled: false);
        IRenderedComponent<ContainerFragment> cut = RenderHost(ctx);
        var nav = (BunitNavigationManager)ctx.Services.GetRequiredService<NavigationManager>();

        nav.NavigateTo("rooms/x?library=notes%2Fa.md");

        Assert.Equal(nav.ToAbsoluteUri("rooms/x?library=notes%2Fa.md").ToString(), nav.Uri);
        Assert.Empty(cut.FindComponents<MudSplitPanel>());
    }

    /// <summary>Source-text pin (the repo's idiom, copied from <c>ProcessModeTests.FindRepoRoot</c>): <c>MainLayout.razor</c>
    /// changes by exactly the two new tags this task adds - <c>&lt;LibraryNavLink&gt;</c> right after
    /// <c>&lt;TaskViewNav /&gt;</c>, and <c>@Body</c> now sits inside <c>&lt;LibraryPaneHost&gt;</c>.</summary>
    [Fact]
    public void MainLayoutSource_HasLibraryNavLinkAfterTaskViewNav_AndBodyInsidePaneHost()
    {
        string repoRoot = FindRepoRoot();
        string text = File.ReadAllText(Path.Combine(repoRoot, "src", "Huddle.App", "Components", "Layout", "MainLayout.razor"));

        int taskViewNavIndex = text.IndexOf("<TaskViewNav", StringComparison.Ordinal);
        int libraryNavLinkIndex = text.IndexOf("<LibraryNavLink", StringComparison.Ordinal);
        int paneHostOpenIndex = text.IndexOf("<LibraryPaneHost", StringComparison.Ordinal);
        int bodyIndex = text.IndexOf("@Body", StringComparison.Ordinal);
        int paneHostCloseIndex = text.IndexOf("</LibraryPaneHost>", StringComparison.Ordinal);

        Assert.True(taskViewNavIndex >= 0);
        Assert.True(libraryNavLinkIndex > taskViewNavIndex);
        Assert.True(paneHostOpenIndex >= 0 && bodyIndex > paneHostOpenIndex && paneHostCloseIndex > bodyIndex);
    }

    private static IRenderedComponent<ContainerFragment> RenderHost(MudBunitContext ctx) => ctx.RenderWithPopovers(builder =>
    {
        builder.OpenComponent<LibraryPaneHost>(0);
        builder.AddAttribute(1, nameof(LibraryPaneHost.ChildContent), (RenderFragment)(bodyBuilder =>
        {
            bodyBuilder.OpenElement(0, "div");
            bodyBuilder.AddAttribute(1, "class", "pane-host-body-placeholder");
            bodyBuilder.AddContent(2, "BODY");
            bodyBuilder.CloseElement();
        }));
        builder.CloseComponent();
    });

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

    private sealed class HostFixture : IDisposable
    {
        private readonly TempDataDir dir;
        private readonly RecordingLogger<AppearanceStore> appearanceLogger = new();
        private readonly PersonaStore personas;
        private readonly TaskStore tasks;

        private HostFixture(TempDataDir dir, LibraryFileServiceFixture libraryFixture, AppearanceStore appearance, PersonaStore personas, TaskStore tasks)
        {
            this.dir = dir;
            this.LibraryFixture = libraryFixture;
            this.Appearance = appearance;
            this.personas = personas;
            this.tasks = tasks;
        }

        /// <summary>The real Library stack (resolver, root store, file service, wikilink index) over this fixture's temp <c>DataDir</c>.</summary>
        public LibraryFileServiceFixture LibraryFixture { get; }

        /// <summary>The real <see cref="AppearanceStore"/> over the same temp <c>DataDir</c>, so <see cref="AppearanceStore.SaveLibraryPaneSide"/> and <see cref="AppearanceStore.AppearanceChanged"/> behave exactly as in the app.</summary>
        public AppearanceStore Appearance { get; }

        /// <summary>Builds a fixture with the standard Teams/Teammates layout.</summary>
        public static HostFixture Build()
        {
            TempDataDir dir = new();
            LibraryFileServiceFixture libraryFixture = LibraryFileServiceFixture.Attach(dir.Path);
            AppearanceStore appearance = new(dir.Options(), new RecordingLogger<AppearanceStore>());
            PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
            TaskStore tasks = TestTaskStore.CreateTaskStore(dir, personas);
            return new HostFixture(dir, libraryFixture, appearance, personas, tasks);
        }

        /// <summary>A <see cref="MudBunitContext"/> with the Library stack, <see cref="AppearanceStore"/>, the
        /// <see cref="TaskStore"/> <see cref="LibraryTree"/> injects for orphan Team folders, a fresh
        /// <see cref="LibraryPaneState"/> and the <c>Library.Enabled</c> gate registered.</summary>
        /// <param name="libraryEnabled">Whether <c>Library.Enabled</c> is set for this context.</param>
        public MudBunitContext NewContext(bool libraryEnabled = true)
        {
            MudBunitContext ctx = new();
            IOptions<TeamOptions> options = this.dir.Options();
            options.Value.Library.Enabled = libraryEnabled;
            ctx.Services.AddSingleton(options);
            ctx.Services.AddSingleton(this.LibraryFixture.Resolver);
            ctx.Services.AddSingleton(this.LibraryFixture.RootStore);
            ctx.Services.AddSingleton(this.LibraryFixture.CreateService());
            ctx.Services.AddSingleton(this.LibraryFixture.Index);
            ctx.Services.AddSingleton<ILibraryNoteResolver>(new FakeLibraryNoteResolver());
            ctx.Services.AddSingleton<ITaskReferenceResolver>(new FakeTaskReferenceResolver());
            ctx.Services.AddSingleton(this.tasks);
            ctx.Services.AddSingleton(this.Appearance);
            ctx.Services.AddSingleton<LibraryPaneState>();
            return ctx;
        }

        /// <summary>Disposes the underlying stores and temp <c>DataDir</c>.</summary>
        public void Dispose()
        {
            this.tasks.Dispose();
            this.personas.Dispose();
            this.Appearance.Dispose();
            this.LibraryFixture.Dispose();
            this.dir.Dispose();
        }
    }

    /// <summary>A hand-written fake <see cref="ILogger{T}"/> that records every call, since this repo has no mocking framework.</summary>
    /// <typeparam name="T">The category type the recorded logger stands in for.</typeparam>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        /// <summary>Every call made to this logger so far, in call order.</summary>
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        /// <summary>Not used by this fake: scoping is irrelevant to the tests that need it, so this returns a no-op.</summary>
        /// <typeparam name="TState">The scope state type.</typeparam>
        /// <param name="state">The scope state.</param>
        /// <returns>A no-op <see cref="IDisposable"/>.</returns>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <summary>Always enabled, so every call this fake receives is actually recorded.</summary>
        /// <param name="logLevel">The level being checked.</param>
        /// <returns><see langword="true"/>, always.</returns>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <summary>Records one log call's level and formatted message.</summary>
        /// <typeparam name="TState">The state type carrying this call's structured values.</typeparam>
        /// <param name="logLevel">The call's severity.</param>
        /// <param name="eventId">Unused by this fake.</param>
        /// <param name="state">The call's structured state, passed to <paramref name="formatter"/>.</param>
        /// <param name="exception">The call's exception, if any.</param>
        /// <param name="formatter">Formats <paramref name="state"/> and <paramref name="exception"/> into the message text.</param>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            this.Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
