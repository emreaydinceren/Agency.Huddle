using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using Microsoft.AspNetCore.Components.Web;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Library;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>
/// Pins <see cref="LibraryExplorer"/> (Task 12.7): scopes, title, layout and the outside-scope hint
/// (<c>[12.7a]</c>, Spec §6.16, corrections-B6 item 31's split), plus remembered state under
/// <c>StateKey</c>, <c>TryLeaveAsync</c> before a document switch or a file-op that touches the open
/// document, and refresh-on-focus wiring (<c>[12.7b]</c>, corrections-B6 items 21, 22, 28 and
/// judgement 45), over a REAL <see cref="LibraryFileService"/> stack on a temp tree.
/// </summary>
public sealed class LibraryExplorerTests : IDisposable
{
    private readonly ExplorerFixture fixture = ExplorerFixture.Build();

    /// <summary>Disposes the underlying temp <c>DataDir</c> and stores.</summary>
    public void Dispose() => this.fixture.Dispose();

    /// <summary>[12.7a] With no <c>Scopes</c>, every visible Library Root shows as a top node (Spec §6.16).</summary>
    [Fact]
    public async Task NullScopes_ShowsEveryVisibleRoot()
    {
        this.fixture.LibraryFixture.CreatePinnedRoot("Notes");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(ctx, scopes: null);

        IReadOnlyList<string> rootNames = [.. this.fixture.LibraryFixture.Resolver.AllRoots.Select(root => root.DisplayName)];
        cut.WaitForAssertion(() => Assert.Equal(
            rootNames,
            // Children load lazily and nothing is expanded, so every node name on first render is a top node.
            cut.FindAll("span.library-tree-node-name").Select(node => node.TextContent.Trim()).ToList()));
    }

    /// <summary>[12.7a] With one <see cref="LibraryLocation"/>, the tree shows only that folder, and its
    /// top node is protected exactly like a root (Spec §6.16 "Inside a scope").</summary>
    [Fact]
    public async Task OneScope_ShowsOnlyThatFolder_TopNodeProtected()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        Directory.CreateDirectory(Path.Combine(root, "Sub"));
        LibraryPath scopePath = this.fixture.LibraryFixture.Resolve(root, "Sub");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(ctx, scopes: [new LibraryLocation(scopePath.Root.Id, "Sub")]);

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".library-tree > ul > li")));
        Assert.Equal("Sub", cut.Find(".library-tree-node-name").TextContent.Trim());

        await cut.InvokeAsync(() => cut.Find(".library-tree-actions-button").Click());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-menu-item")));
        IReadOnlyList<string> renameLabels = [.. cut.FindAll(".library-tree-menu-label").Select(el => el.TextContent.Trim())];
        int renameIndex = renameLabels.ToList().IndexOf("Rename");
        Assert.True(cut.FindAll(".mud-menu-item")[renameIndex].ClassList.Contains("mud-disabled"));
    }

    /// <summary>[12.7a] A scope that doesn't resolve shows the settled <see cref="MudAlert"/> instead of the tree.</summary>
    [Fact]
    public async Task UnresolvableScope_ShowsAlert()
    {
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(ctx, scopes: [new LibraryLocation("does-not-exist", string.Empty)]);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-explorer-unavailable")));
        AngleSharp.Dom.IElement alert = cut.Find(".library-explorer-unavailable");
        Assert.Equal("This folder isn't available in the Library.", alert.TextContent.Trim());
        Assert.Equal("status", alert.GetAttribute("role"));
        Assert.Empty(cut.FindAll(".library-tree"));
    }

    /// <summary>[12.7a] A scope resolving to a Team folder that doesn't exist yet shows an empty tree, not
    /// the unavailable alert (Spec §6.16).</summary>
    [Fact]
    public async Task MissingTeamScope_ShowsEmptyTree()
    {
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(ctx, scopes: [new LibraryLocation("teams", "NoSuchTeam")]);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".library-explorer-unavailable")));
        Assert.Single(cut.FindAll(".library-tree > ul > li"));
    }

    /// <summary>[12.7a] The scoped explorer's own New note action creates inside the scope folder, not at the root.</summary>
    [Fact]
    public async Task NewNote_InScope_CreatesInsideScope()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        Directory.CreateDirectory(Path.Combine(root, "Sub"));
        LibraryPath scopePath = this.fixture.LibraryFixture.Resolve(root, "Sub");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(ctx, scopes: [new LibraryLocation(scopePath.Root.Id, "Sub")]);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".library-tree > ul > li")));

        await cut.InvokeAsync(() => cut.Find(".library-tree-actions-button").Click());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-menu-item")));
        int newNoteIndex = cut.FindAll(".library-tree-menu-label").Select(el => el.TextContent.Trim()).ToList().IndexOf("New note");
        await cut.InvokeAsync(() => cut.FindAll(".mud-menu-item")[newNoteIndex].Click());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-file-ops-name-field input")));
        await cut.InvokeAsync(() => cut.Find(".library-file-ops-name-field input").Change("Idea"));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button")[0].Click());

        cut.WaitForAssertion(() => Assert.True(File.Exists(Path.Combine(root, "Sub", "Idea.md"))), TimeSpan.FromSeconds(5));
        Assert.False(File.Exists(Path.Combine(root, "Idea.md")));
    }

    /// <summary>[12.7a] Opening a wikilink target outside the scope shows the document with its "Outside
    /// this view" hint, and the tree is left unchanged (Spec §6.16, no expansion beyond the scope).</summary>
    [Fact]
    public async Task WikiLinkOutsideScope_OpensWithHint_TreeUnchanged()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        Directory.CreateDirectory(Path.Combine(root, "Sub"));
        File.WriteAllText(Path.Combine(root, "Sub", "InScope.md"), "See [[../Outside]].");
        File.WriteAllText(Path.Combine(root, "Outside.md"), "outside body");
        LibraryPath scopePath = this.fixture.LibraryFixture.Resolve(root, "Sub");
        LibraryPath inScope = this.fixture.LibraryFixture.Resolve(root, "Sub/InScope.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(
            ctx, scopes: [new LibraryLocation(scopePath.Root.Id, "Sub")], initialFile: "InScope.md");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-rendered")));

        string href = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"?library={Uri.EscapeDataString(inScope.Root.Id)}%2FOutside.md");
        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        await cut.InvokeAsync(() => nav.NavigateTo(nav.BaseUri + href.TrimStart('?').Insert(0, "?")));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-document-outside-scope")));
        Assert.Empty(cut.FindAll(".library-tree .mud-treeview-item--expanded"));
    }

    /// <summary>[12.7a] With one scope and no <c>Title</c>, the header defaults to that scope's folder name.</summary>
    [Fact]
    public async Task Title_DefaultsToScopeFolderName()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        Directory.CreateDirectory(Path.Combine(root, "Sub"));
        LibraryPath scopePath = this.fixture.LibraryFixture.Resolve(root, "Sub");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(ctx, scopes: [new LibraryLocation(scopePath.Root.Id, "Sub")]);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-explorer-title")));
        Assert.Equal("Sub", cut.Find(".library-explorer-title").TextContent.Trim());
    }

    /// <summary>[12.7a] A <c>Title</c> parameter always wins over the default.</summary>
    [Fact]
    public async Task Title_Parameter_Wins()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        Directory.CreateDirectory(Path.Combine(root, "Sub"));
        LibraryPath scopePath = this.fixture.LibraryFixture.Resolve(root, "Sub");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(
            ctx, scopes: [new LibraryLocation(scopePath.Root.Id, "Sub")], title: "Launch Q4 files");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-explorer-title")));
        Assert.Equal("Launch Q4 files", cut.Find(".library-explorer-title").TextContent.Trim());
    }

    /// <summary>[12.7a] With no <c>Scopes</c> and no <c>Title</c>, the header reads "Library".</summary>
    [Fact]
    public async Task NullScopes_TitleIsLibrary()
    {
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(ctx, scopes: null);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-explorer-title")));
        Assert.Equal("Library", cut.Find(".library-explorer-title").TextContent.Trim());
    }

    /// <summary>[12.7a] <c>Layout.Stacked</c> renders the <see cref="MudSplitPanel"/> vertically
    /// (<c>Horizontal="false"</c>, mudblazor.md "Facts already checked").</summary>
    [Fact]
    public async Task Layout_Stacked_IsVertical()
    {
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(ctx, scopes: null, layout: LibraryExplorerLayout.Stacked);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<MudSplitPanel>()));
        Assert.False(cut.FindComponent<MudSplitPanel>().Instance.Horizontal);
    }

    /// <summary>[12.7a] <c>Layout.SideBySide</c> renders the <see cref="MudSplitPanel"/> horizontally.</summary>
    [Fact]
    public async Task Layout_SideBySide_IsHorizontal()
    {
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(ctx, scopes: null, layout: LibraryExplorerLayout.SideBySide);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<MudSplitPanel>()));
        Assert.True(cut.FindComponent<MudSplitPanel>().Instance.Horizontal);
    }

    /// <summary>[12.7b] <c>StateKey</c> keys the remembered expanded folders, open file and divider
    /// position separately per host, through <c>huddleStorage.set</c> (corrections-B6 item 28).</summary>
    [Fact]
    public async Task StateKey_SeparatesRememberedState()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "One.md"), "one");
        LibraryPath one = this.fixture.LibraryFixture.Resolve(root, "One.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        ctx.JSInterop.SetupVoid("huddleStorage.set", _ => true).SetVoidResult();
        ctx.JSInterop.Setup<string?>("huddleStorage.get", _ => true).SetResult(null);
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(
            ctx, scopes: [new LibraryLocation(one.Root.Id, string.Empty)], stateKey: "pane-a");

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".library-tree-node-name")));
        await cut.InvokeAsync(() => cut.Find("div.mud-treeview-item-arrow button").Click());
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".library-tree-node-name").Count));
        await cut.InvokeAsync(() => cut.FindAll(".library-tree-node-name").Single(node => node.TextContent.Trim() == "One.md").Click());

        ctx.JSInterop.VerifyInvoke("huddleStorage.set");
        Assert.Contains(
            ctx.JSInterop.Invocations["huddleStorage.set"],
            invocation => Equals(invocation.Arguments[0], "library:pane-a:open"));
    }

    /// <summary>[12.7b] Switching to a different document asks the currently open one to
    /// <c>TryLeaveAsync</c> first; answering Cancel on a dirty document keeps it open.</summary>
    [Fact]
    public async Task SwitchDocument_CallsTryLeaveAsync()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "First.md"), "first");
        File.WriteAllText(Path.Combine(root, "Second.md"), "second");
        LibraryPath first = this.fixture.LibraryFixture.Resolve(root, "First.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupEditorModule(ctx, "edited text");
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(
            ctx, scopes: [new LibraryLocation(first.Root.Id, string.Empty)], initialFile: "First.md");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<MudToggleGroup<LibraryMode>>()));

        MudToggleGroup<LibraryMode> toggle = cut.FindComponent<MudToggleGroup<LibraryMode>>().Instance;
        await cut.InvokeAsync(() => toggle.ValueChanged.InvokeAsync(LibraryMode.Edit));
        await cut.InvokeAsync(() => cut.FindComponent<LibraryEditor>().Instance.DirtyChanged.InvokeAsync(true));

        await cut.InvokeAsync(() => cut.Find("div.mud-treeview-item-arrow button").Click());
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".library-tree-node-name").Count));
        _ = cut.InvokeAsync(() => cut.FindAll(".library-tree-node-name").First(el => el.TextContent.Trim() == "Second.md").Click());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").First(b => b.TextContent.Trim() == "Cancel").Click());

        cut.WaitForAssertion(() => Assert.Equal("First.md", cut.FindComponent<LibraryDocument>().Instance.Path.RelativePath));
    }

    /// <summary>[12.7b] Renaming the open document asks it to leave first (corrections-B6 item 21):
    /// Cancel on a dirty document leaves nothing renamed, the document still shows the old path with
    /// its edits intact; clean, it renames and the document shows the new path.</summary>
    [Fact]
    public async Task RenameOpenDocument_AsksToLeaveFirst_ThenRepoints()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "Old.md"), "body");
        LibraryPath old = this.fixture.LibraryFixture.Resolve(root, "Old.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupEditorModule(ctx, "edited text");
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(
            ctx, scopes: [new LibraryLocation(old.Root.Id, string.Empty)], initialFile: "Old.md");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-rendered")));

        MudToggleGroup<LibraryMode> toggle = cut.FindComponent<MudToggleGroup<LibraryMode>>().Instance;
        await cut.InvokeAsync(() => toggle.ValueChanged.InvokeAsync(LibraryMode.Edit));
        await cut.InvokeAsync(() => cut.FindComponent<LibraryEditor>().Instance.DirtyChanged.InvokeAsync(true));

        await cut.InvokeAsync(() => cut.Find("div.mud-treeview-item-arrow button").Click());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("button.library-tree-actions-button[aria-label='Actions for Old.md']")));
        await cut.InvokeAsync(() => cut.Find("button.library-tree-actions-button[aria-label='Actions for Old.md']").Click());
        int renameIndex = cut.FindAll(".library-tree-menu-label").Select(el => el.TextContent.Trim()).ToList().IndexOf("Rename");
        await cut.InvokeAsync(() => cut.FindAll(".mud-menu-item")[renameIndex].Click());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-file-ops-name-field input")));
        await cut.InvokeAsync(() => cut.Find(".library-file-ops-name-field input").Change("New"));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button")[0].Click());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").First(b => b.TextContent.Trim() == "Cancel").Click());

        Assert.False(File.Exists(Path.Combine(root, "New.md")));
        Assert.True(File.Exists(Path.Combine(root, "Old.md")));
        Assert.Equal("Old.md", cut.FindComponent<LibraryDocument>().Instance.Path.RelativePath);
        Assert.Equal("Old.md ●", cut.Find(".library-document-title").TextContent.Trim());
    }

    /// <summary>[12.7b] Deleting a folder that contains the open document also asks it to leave first.</summary>
    [Fact]
    public async Task DeleteFolderContainingOpenDocument_AsksToLeaveFirst()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        Directory.CreateDirectory(Path.Combine(root, "Sub"));
        File.WriteAllText(Path.Combine(root, "Sub", "Inside.md"), "body");
        LibraryPath sub = this.fixture.LibraryFixture.Resolve(root, "Sub");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(
            ctx, scopes: [new LibraryLocation(sub.Root.Id, string.Empty)], initialFile: "Sub/Inside.md");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-rendered")));

        await cut.InvokeAsync(() => cut.Find("div.mud-treeview-item-arrow button").Click());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("button.library-tree-actions-button[aria-label='Actions for Sub']")));
        await cut.InvokeAsync(() => cut.Find("button.library-tree-actions-button[aria-label='Actions for Sub']").Click());
        int deleteIndex = cut.FindAll(".library-tree-menu-label").Select(el => el.TextContent.Trim()).ToList().IndexOf("Delete");
        await cut.InvokeAsync(() => cut.FindAll(".mud-menu-item")[deleteIndex].Click());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").First(b => b.TextContent.Trim() == "Delete").Click());

        cut.WaitForAssertion(
            () => Assert.Equal([sub.FullPath], this.fixture.LibraryFixture.RecycleBin.Sent), TimeSpan.FromSeconds(5));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindComponents<LibraryDocument>()));
    }

    /// <summary>[12.7b] A <c>focusin</c> on the explorer root refreshes both the tree and the open
    /// document (corrections-B6 item 28).</summary>
    [Fact]
    public async Task FocusIn_RefreshesTreeAndDocument()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "One.md"), "one");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(ctx, scopes: null, initialFile: "One.md");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-rendered")));

        File.WriteAllText(Path.Combine(root, "One.md"), "one changed, a fair bit longer than before");
        await cut.InvokeAsync(() => cut.Find(".library-explorer").FocusIn(new FocusEventArgs()));

        cut.WaitForAssertion(
            () => Assert.Equal(
                "one changed, a fair bit longer than before",
                cut.Find(".library-rendered").TextContent.Trim()),
            TimeSpan.FromSeconds(5));
    }

    /// <summary>[12.7b, H5 judgement 45] Inside a scoped explorer, a <c>?library=</c> target inside its
    /// own scope opens in place rather than being left for a page-level pane to handle.</summary>
    [Fact]
    public async Task ScopedExplorer_LibraryLinkInsideScope_OpensInPlace()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        Directory.CreateDirectory(Path.Combine(root, "Sub"));
        File.WriteAllText(Path.Combine(root, "Sub", "InScope.md"), "See [[Target]].");
        File.WriteAllText(Path.Combine(root, "Sub", "Target.md"), "target body");
        LibraryPath scopePath = this.fixture.LibraryFixture.Resolve(root, "Sub");
        LibraryPath target = this.fixture.LibraryFixture.Resolve(root, "Sub/Target.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(
            ctx, scopes: [new LibraryLocation(scopePath.Root.Id, "Sub")], initialFile: "InScope.md");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-rendered")));

        string href = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"?library={Uri.EscapeDataString(target.Root.Id)}%2FSub%2FTarget.md");
        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        await cut.InvokeAsync(() => nav.NavigateTo(nav.BaseUri + href.TrimStart('?').Insert(0, "?")));

        cut.WaitForAssertion(() => Assert.Equal(
            "Sub/Target.md",
            cut.FindComponent<LibraryDocument>().Instance.Path.RelativePath));
        Assert.Empty(cut.FindAll(".library-document-outside-scope"));
    }

    /// <summary>[12.7a] <c>InitialFile</c> opens on the explorer's first render, relative to the first scope.</summary>
    [Fact]
    public async Task InitialFile_OpensOnFirstRender()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "Welcome.md"), "welcome body");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(ctx, scopes: null, initialFile: "Welcome.md");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-rendered")));
        Assert.Equal("welcome body", cut.Find(".library-rendered").TextContent.Trim());
    }

    private static (BunitJSModuleInterop Module, BunitJSModuleInterop Handle) SetupEditorModule(MudBunitContext ctx, string text)
    {
        BunitJSModuleInterop module = ctx.JSInterop.SetupModule("./library-editor.js");
        BunitJSModuleInterop handle = module.SetupModule("create", _ => true);
        handle.Setup<string>("getText", _ => true).SetResult(text);
        handle.SetupVoid("setText", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        return (module, handle);
    }

    private static IRenderedComponent<ContainerFragment> RenderExplorer(
        MudBunitContext ctx,
        IReadOnlyList<LibraryLocation>? scopes,
        string? title = null,
        LibraryExplorerLayout layout = LibraryExplorerLayout.Stacked,
        string? initialFile = null,
        string stateKey = "test",
        EventCallback<LibraryPath> onOpenInLibrary = default) => ctx.RenderWithPopovers(builder =>
    {
        builder.OpenComponent<LibraryExplorer>(0);
        builder.AddAttribute(1, nameof(LibraryExplorer.Scopes), scopes);
        builder.AddAttribute(2, nameof(LibraryExplorer.Title), title);
        builder.AddAttribute(3, nameof(LibraryExplorer.Layout), layout);
        builder.AddAttribute(4, nameof(LibraryExplorer.InitialFile), initialFile);
        builder.AddAttribute(5, nameof(LibraryExplorer.StateKey), stateKey);
        builder.AddAttribute(6, nameof(LibraryExplorer.OnOpenInLibrary), onOpenInLibrary);
        builder.CloseComponent();
    });

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

        /// <summary>A <see cref="MudBunitContext"/> with the whole Library stack registered, ready to host
        /// a <see cref="LibraryExplorer"/> and its parts (<see cref="LibraryTree"/>,
        /// <see cref="LibraryDocument"/>, <see cref="LibraryFileOps"/>).</summary>
        public MudBunitContext NewContext()
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
                this.personas, this.LibraryFixture.RootStore, this.LibraryFixture.Resolver, NullLogger<TeamFolderProvisioner>.Instance));
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
