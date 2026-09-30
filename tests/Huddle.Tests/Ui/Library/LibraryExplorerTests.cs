using System.Globalization;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
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
using Agency.Huddle.Tests.Teams;

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
    private static readonly string[] BusinessOnly = ["Business"];

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

        await cut.InvokeAsync(() => cut.Find(".library-tree-actions-button").ClickAsync());
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

        await cut.InvokeAsync(() => cut.Find(".library-tree-actions-button").ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-menu-item")));
        int newNoteIndex = cut.FindAll(".library-tree-menu-label").Select(el => el.TextContent.Trim()).ToList().IndexOf("New note");
        _ = cut.InvokeAsync(() => cut.FindAll(".mud-menu-item")[newNoteIndex].Click());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-file-ops-name-field input")));
        await cut.InvokeAsync(() => cut.Find(".library-file-ops-name-field input").ChangeAsync("Idea"));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button")[0].ClickAsync());

        cut.WaitForAssertion(() => Assert.True(File.Exists(Path.Combine(root, "Sub", "Idea.md"))), TimeSpan.FromSeconds(5));
        Assert.False(File.Exists(Path.Combine(root, "Idea.md")));
    }

    /// <summary>[6.6] With nothing selected, <see cref="LibraryExplorer.NewNoteAsync"/> targets the first
    /// scope's folder: the note lands there and opens in the document area (Spec §6.8).</summary>
    [Fact]
    public async Task NewNoteAsync_NoSelection_TargetsTheScopeFolder_AndOpensTheNote()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        Directory.CreateDirectory(Path.Combine(root, "Sub"));
        LibraryPath scopePath = this.fixture.LibraryFixture.Resolve(root, "Sub");

        await using MudBunitContext ctx = this.fixture.NewContext();
        ArrangeStorage(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(ctx, scopes: [new LibraryLocation(scopePath.Root.Id, "Sub")]);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".library-tree > ul > li")));
        LibraryExplorer explorer = cut.FindComponent<LibraryExplorer>().Instance;

        Task pending = cut.InvokeAsync(() => explorer.NewNoteAsync());
        await ConfirmNewNoteAsync(cut, pending, "Idea", ct);

        Assert.True(File.Exists(Path.Combine(root, "Sub", "Idea.md")));
        Assert.False(File.Exists(Path.Combine(root, "Idea.md")));
        cut.WaitForAssertion(() => Assert.Equal("Sub/Idea.md", cut.FindComponent<LibraryDocument>().Instance.Path.RelativePath));
    }

    /// <summary>[6.6] With two scopes and nothing selected, the note goes into the FIRST scope's folder.</summary>
    [Fact]
    public async Task NewNoteAsync_NoSelection_TwoScopes_TargetsTheFirstScope()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        Directory.CreateDirectory(Path.Combine(root, "Sub"));
        Directory.CreateDirectory(Path.Combine(root, "Other"));
        LibraryPath scopePath = this.fixture.LibraryFixture.Resolve(root, "Sub");

        await using MudBunitContext ctx = this.fixture.NewContext();
        ArrangeStorage(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(
            ctx, scopes: [new LibraryLocation(scopePath.Root.Id, "Sub"), new LibraryLocation(scopePath.Root.Id, "Other")]);
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".library-tree > ul > li").Count));
        LibraryExplorer explorer = cut.FindComponent<LibraryExplorer>().Instance;

        Task pending = cut.InvokeAsync(() => explorer.NewNoteAsync());
        await ConfirmNewNoteAsync(cut, pending, "Idea", ct);

        Assert.True(File.Exists(Path.Combine(root, "Sub", "Idea.md")));
        Assert.False(File.Exists(Path.Combine(root, "Other", "Idea.md")));
    }

    /// <summary>[6.6] A folder selected inside the first scope is the target, not the scope's own folder.</summary>
    [Fact]
    public async Task NewNoteAsync_SelectedFolderInsideScope_TargetsIt_AndOpensTheNote()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        Directory.CreateDirectory(Path.Combine(root, "Sub", "Inner"));
        LibraryPath scopePath = this.fixture.LibraryFixture.Resolve(root, "Sub");

        await using MudBunitContext ctx = this.fixture.NewContext();
        ArrangeStorage(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(ctx, scopes: [new LibraryLocation(scopePath.Root.Id, "Sub")]);
        await ExpandFirstNodeAsync(cut);
        await ClickNodeAsync(cut, "Inner");
        List<string> expectedSelected = ["Inner"];
        cut.WaitForAssertion(() => Assert.Equal(expectedSelected, SelectedNames(cut)));
        LibraryExplorer explorer = cut.FindComponent<LibraryExplorer>().Instance;

        Task pending = cut.InvokeAsync(() => explorer.NewNoteAsync());
        await ConfirmNewNoteAsync(cut, pending, "Idea", ct);

        Assert.True(File.Exists(Path.Combine(root, "Sub", "Inner", "Idea.md")));
        Assert.False(File.Exists(Path.Combine(root, "Sub", "Idea.md")));
        cut.WaitForAssertion(() => Assert.Equal("Sub/Inner/Idea.md", cut.FindComponent<LibraryDocument>().Instance.Path.RelativePath));
    }

    /// <summary>[6.6] Selecting the scope's own top node targets the scope folder.</summary>
    [Fact]
    public async Task NewNoteAsync_SelectedScopeRoot_TargetsTheScopeFolder()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        Directory.CreateDirectory(Path.Combine(root, "Sub", "Inner"));
        LibraryPath scopePath = this.fixture.LibraryFixture.Resolve(root, "Sub");

        await using MudBunitContext ctx = this.fixture.NewContext();
        ArrangeStorage(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(ctx, scopes: [new LibraryLocation(scopePath.Root.Id, "Sub")]);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".library-tree > ul > li")));
        await ClickNodeAsync(cut, "Sub");
        List<string> expectedSelected = ["Sub"];
        cut.WaitForAssertion(() => Assert.Equal(expectedSelected, SelectedNames(cut)));
        LibraryExplorer explorer = cut.FindComponent<LibraryExplorer>().Instance;

        Task pending = cut.InvokeAsync(() => explorer.NewNoteAsync());
        await ConfirmNewNoteAsync(cut, pending, "Idea", ct);

        Assert.True(File.Exists(Path.Combine(root, "Sub", "Idea.md")));
        Assert.False(File.Exists(Path.Combine(root, "Sub", "Inner", "Idea.md")));
    }

    /// <summary>[6.6] A folder selected in a scope other than the first lies outside the first scope, so the
    /// note goes into the first scope's folder instead.</summary>
    [Fact]
    public async Task NewNoteAsync_SelectedFolderOutsideFirstScope_TargetsTheFirstScope()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        Directory.CreateDirectory(Path.Combine(root, "Sub"));
        Directory.CreateDirectory(Path.Combine(root, "Other"));
        LibraryPath scopePath = this.fixture.LibraryFixture.Resolve(root, "Sub");

        await using MudBunitContext ctx = this.fixture.NewContext();
        ArrangeStorage(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(
            ctx, scopes: [new LibraryLocation(scopePath.Root.Id, "Sub"), new LibraryLocation(scopePath.Root.Id, "Other")]);
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".library-tree > ul > li").Count));
        await ClickNodeAsync(cut, "Other");
        List<string> expectedSelected = ["Other"];
        cut.WaitForAssertion(() => Assert.Equal(expectedSelected, SelectedNames(cut)));
        LibraryExplorer explorer = cut.FindComponent<LibraryExplorer>().Instance;

        Task pending = cut.InvokeAsync(() => explorer.NewNoteAsync());
        await ConfirmNewNoteAsync(cut, pending, "Idea", ct);

        Assert.True(File.Exists(Path.Combine(root, "Sub", "Idea.md")));
        Assert.False(File.Exists(Path.Combine(root, "Other", "Idea.md")));
    }

    /// <summary>[6.6] A selected FILE targets its parent folder (Settled), and the new note replaces it in the document area.</summary>
    [Fact]
    public async Task NewNoteAsync_SelectedFile_TargetsItsParentFolder()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        Directory.CreateDirectory(Path.Combine(root, "Sub", "Inner"));
        File.WriteAllText(Path.Combine(root, "Sub", "Inner", "Existing.md"), "existing body");
        LibraryPath scopePath = this.fixture.LibraryFixture.Resolve(root, "Sub");

        await using MudBunitContext ctx = this.fixture.NewContext();
        ArrangeStorage(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(ctx, scopes: [new LibraryLocation(scopePath.Root.Id, "Sub")]);
        await ExpandFirstNodeAsync(cut);
        await ClickNodeAsync(cut, "Inner");
        await cut.InvokeAsync(() => cut.FindAll("div.mud-treeview-item-arrow button")[1].ClickAsync());
        await ClickNodeAsync(cut, "Existing.md");
        List<string> expectedSelected = ["Existing.md"];
        cut.WaitForAssertion(() => Assert.Equal(expectedSelected, SelectedNames(cut)));
        LibraryExplorer explorer = cut.FindComponent<LibraryExplorer>().Instance;

        Task pending = cut.InvokeAsync(() => explorer.NewNoteAsync());
        await ConfirmNewNoteAsync(cut, pending, "Idea", ct);

        Assert.True(File.Exists(Path.Combine(root, "Sub", "Inner", "Idea.md")));
        Assert.False(File.Exists(Path.Combine(root, "Sub", "Idea.md")));
        cut.WaitForAssertion(() => Assert.Equal("Sub/Inner/Idea.md", cut.FindComponent<LibraryDocument>().Instance.Path.RelativePath));
    }

    /// <summary>[6.6] Cancelling the New note dialog completes the call, creates nothing (the folder listing is
    /// unchanged) and opens nothing.</summary>
    [Fact]
    public async Task NewNoteAsync_WhenCancelled_CreatesNothing_AndOpensNothing()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        Directory.CreateDirectory(Path.Combine(root, "Sub"));
        File.WriteAllText(Path.Combine(root, "Sub", "Keep.md"), "keep");
        LibraryPath scopePath = this.fixture.LibraryFixture.Resolve(root, "Sub");
        List<string> before = [.. Directory.GetFileSystemEntries(Path.Combine(root, "Sub")).Order(StringComparer.Ordinal)];

        await using MudBunitContext ctx = this.fixture.NewContext();
        ArrangeStorage(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(ctx, scopes: [new LibraryLocation(scopePath.Root.Id, "Sub")]);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".library-tree > ul > li")));
        LibraryExplorer explorer = cut.FindComponent<LibraryExplorer>().Instance;

        Task pending = cut.InvokeAsync(() => explorer.NewNoteAsync());
        cut.WaitForAssertion(() => Assert.Equal("New note", cut.Find(".mud-dialog-title").TextContent.Trim()));
        await cut.InvokeAsync(() => cut.Find(".library-file-ops-name-field input").ChangeAsync("Idea"));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").First(b => b.TextContent.Trim() == "Cancel").ClickAsync());
        await pending.WaitAsync(TimeSpan.FromSeconds(5), ct);

        List<string> after = [.. Directory.GetFileSystemEntries(Path.Combine(root, "Sub")).Order(StringComparer.Ordinal)];
        Assert.Equal(before, after);
        Assert.Empty(cut.FindComponents<LibraryDocument>());
    }

    /// <summary>[6.6, corrections-D6 item 33] A scope whose folder does not exist yet gets no lazy create from
    /// the explorer: the dialog stays open with the service's refusal, nothing is created and nothing opens.</summary>
    [Fact]
    public async Task NewNoteAsync_MissingScopeFolder_ShowsCouldNotCreate_AndCreatesNothing()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        LibraryPath missing = this.fixture.LibraryFixture.ResolveTeams("NoSuchTeam");

        await using MudBunitContext ctx = this.fixture.NewContext();
        ArrangeStorage(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(ctx, scopes: [new LibraryLocation("teams", "NoSuchTeam")]);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".library-tree > ul > li")));
        LibraryExplorer explorer = cut.FindComponent<LibraryExplorer>().Instance;

        Task pending = cut.InvokeAsync(() => explorer.NewNoteAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-file-ops-name-field input")));
        await cut.InvokeAsync(() => cut.Find(".library-file-ops-name-field input").ChangeAsync("Idea"));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button")[0].ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal("Couldn't create Idea.md.", cut.Find(".library-file-ops-error").TextContent.Trim()));
        Assert.False(pending.IsCompleted);
        Assert.False(Directory.Exists(missing.FullPath));
        Assert.Empty(cut.FindComponents<LibraryDocument>());

        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").First(b => b.TextContent.Trim() == "Cancel").ClickAsync());
        await pending.WaitAsync(TimeSpan.FromSeconds(5), ct);
        Assert.False(Directory.Exists(missing.FullPath));
        Assert.Empty(cut.FindComponents<LibraryDocument>());
    }

    /// <summary>[6.7] A note created through <see cref="LibraryExplorer.NewNoteAsync"/> in the selected (and
    /// expanded) folder appears among that folder's tree nodes, not only on disk.</summary>
    [Fact]
    public async Task NewNoteAsync_CreatedNote_AppearsInTheTree()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        Directory.CreateDirectory(Path.Combine(root, "Sub", "Inner"));
        File.WriteAllText(Path.Combine(root, "Sub", "Inner", "Existing.md"), "existing body");
        LibraryPath scopePath = this.fixture.LibraryFixture.Resolve(root, "Sub");

        await using MudBunitContext ctx = this.fixture.NewContext();
        ArrangeStorage(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(ctx, scopes: [new LibraryLocation(scopePath.Root.Id, "Sub")]);
        await ExpandFirstNodeAsync(cut);
        await ClickNodeAsync(cut, "Inner");
        await cut.InvokeAsync(() => cut.FindAll("div.mud-treeview-item-arrow button")[1].ClickAsync());
        List<string> namesBefore = ["Existing.md", "Inner", "Sub"];
        cut.WaitForAssertion(() => Assert.Equal(namesBefore, SortedNodeNames(cut)));
        LibraryExplorer explorer = cut.FindComponent<LibraryExplorer>().Instance;

        Task pending = cut.InvokeAsync(() => explorer.NewNoteAsync());
        await ConfirmNewNoteAsync(cut, pending, "Idea", ct);

        Assert.True(File.Exists(Path.Combine(root, "Sub", "Inner", "Idea.md")));
        List<string> namesAfter = ["Existing.md", "Idea.md", "Inner", "Sub"];
        cut.WaitForAssertion(() => Assert.Equal(namesAfter, SortedNodeNames(cut)));
    }

    /// <summary>[6.7] With nothing selected, a note created through <see cref="LibraryExplorer.NewNoteAsync"/>
    /// appears at the top of the (expanded) scope root in the tree.</summary>
    [Fact]
    public async Task NewNoteAsync_NoSelection_CreatedNoteAppearsAtScopeTop()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        Directory.CreateDirectory(Path.Combine(root, "Sub"));
        File.WriteAllText(Path.Combine(root, "Sub", "Existing.md"), "existing body");
        LibraryPath scopePath = this.fixture.LibraryFixture.Resolve(root, "Sub");

        await using MudBunitContext ctx = this.fixture.NewContext();
        ArrangeStorage(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(ctx, scopes: [new LibraryLocation(scopePath.Root.Id, "Sub")]);
        await ExpandFirstNodeAsync(cut);
        List<string> namesBefore = ["Existing.md", "Sub"];
        cut.WaitForAssertion(() => Assert.Equal(namesBefore, SortedNodeNames(cut)));
        LibraryExplorer explorer = cut.FindComponent<LibraryExplorer>().Instance;

        Task pending = cut.InvokeAsync(() => explorer.NewNoteAsync());
        await ConfirmNewNoteAsync(cut, pending, "Idea", ct);

        Assert.True(File.Exists(Path.Combine(root, "Sub", "Idea.md")));
        List<string> namesAfter = ["Existing.md", "Idea.md", "Sub"];
        cut.WaitForAssertion(() => Assert.Equal(namesAfter, SortedNodeNames(cut)));
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

    /// <summary>
    /// <c>ShowScopeRoot="false"</c> (LibraryPane's own scoped rows): no folder-name title, but a
    /// header showing the root's full path, and the tree skips the scope's own wrapper row too - its
    /// own "..." menu button never exists at all - showing that folder's children directly as the
    /// tree's top-level nodes instead (LibraryTreeTests pins the tree's own end of this).
    /// </summary>
    [Fact]
    public async Task ShowScopeRootFalse_ShowsRootPathHeaderAndFlattensTheTree()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        Directory.CreateDirectory(Path.Combine(root, "Sub"));
        LibraryPath scopePath = this.fixture.LibraryFixture.Resolve(root, string.Empty);

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(
            ctx, scopes: [new LibraryLocation(scopePath.Root.Id, string.Empty)], showScopeRoot: false);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("button[aria-label='Actions for Sub']")));
        Assert.Empty(cut.FindAll(".library-explorer-title"));
        Assert.Equal(scopePath.FullPath, cut.Find(".library-explorer-root-path").TextContent.Trim());
        Assert.Empty(cut.FindAll("button[aria-label='Actions for Notes']"));
    }

    /// <summary>
    /// [12.7a] <c>Layout.Stacked</c> renders the <see cref="MudSplitPanel"/> with <c>Horizontal="true"</c>.
    /// Confirmed against 9.10.0's own split-panel.js (mudblazor.md "Facts already checked"):
    /// <c>Horizontal</c> names the DIVIDER's orientation, not the panels' - a horizontal dividing line
    /// separates panels stacked top/bottom (sized by height), so Stacked needs <c>Horizontal="true"</c>,
    /// the opposite of what the parameter's name suggests.
    /// </summary>
    [Fact]
    public async Task Layout_Stacked_IsVertical()
    {
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(ctx, scopes: null, layout: LibraryExplorerLayout.Stacked);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<MudSplitPanel>()));
        Assert.True(cut.FindComponent<MudSplitPanel>().Instance.Horizontal);
    }

    /// <summary>
    /// [12.7a] <c>Layout.SideBySide</c> renders the <see cref="MudSplitPanel"/> with <c>Horizontal="false"</c>:
    /// a vertical dividing line separates panels left/right (sized by width) - see
    /// <see cref="Layout_Stacked_IsVertical"/> for why this reads backwards from the parameter's name.
    /// </summary>
    [Fact]
    public async Task Layout_SideBySide_IsHorizontal()
    {
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(ctx, scopes: null, layout: LibraryExplorerLayout.SideBySide);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<MudSplitPanel>()));
        Assert.False(cut.FindComponent<MudSplitPanel>().Instance.Horizontal);
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
        await cut.InvokeAsync(() => cut.Find("div.mud-treeview-item-arrow button").ClickAsync());
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".library-tree-node-name").Count));
        await cut.InvokeAsync(() => cut.FindAll(".library-tree-node-name").Single(node => node.TextContent.Trim() == "One.md").ClickAsync());

        ctx.JSInterop.VerifyInvoke("huddleStorage.set");
        Assert.Contains(
            ctx.JSInterop.Invocations["huddleStorage.set"],
            invocation => Equals(invocation.Arguments[0], "library:pane-a:open"));
    }

    /// <summary>
    /// The remembered divider position is read under a key carrying the layout, never the old
    /// layout-less <c>library:{StateKey}:divider</c>: the number is a width side by side and a height
    /// stacked, and values stored while <c>Horizontal</c> was inverted were heights that came back as
    /// a tree column one row wide.
    /// </summary>
    [Theory]
    [InlineData(LibraryExplorerLayout.SideBySide, "library:k:side-by-side:divider")]
    [InlineData(LibraryExplorerLayout.Stacked, "library:k:stacked:divider")]
    public async Task RestoreState_ReadsTheLayoutSpecificDividerKey(LibraryExplorerLayout layout, string expectedKey)
    {
        await using MudBunitContext ctx = this.fixture.NewContext();
        ctx.JSInterop.Setup<string?>("huddleStorage.get", _ => true).SetResult(null);
        IRenderedComponent<ContainerFragment> cut = RenderExplorer(ctx, scopes: null, layout: layout, stateKey: "k");

        cut.WaitForAssertion(() => Assert.Contains(
            ctx.JSInterop.Invocations["huddleStorage.get"],
            invocation => Equals(invocation.Arguments[0], expectedKey)));
        Assert.DoesNotContain(
            ctx.JSInterop.Invocations["huddleStorage.get"],
            invocation => Equals(invocation.Arguments[0], "library:k:divider"));
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

        await cut.InvokeAsync(() => cut.Find("div.mud-treeview-item-arrow button").ClickAsync());
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".library-tree-node-name").Count));
        _ = cut.InvokeAsync(() => cut.FindAll(".library-tree-node-name").First(el => el.TextContent.Trim() == "Second.md").Click());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").First(b => b.TextContent.Trim() == "Cancel").ClickAsync());

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

        await cut.InvokeAsync(() => cut.Find("div.mud-treeview-item-arrow button").ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("button.library-tree-actions-button[aria-label='Actions for Old.md']")));
        await cut.InvokeAsync(() => cut.Find("button.library-tree-actions-button[aria-label='Actions for Old.md']").ClickAsync());
        int renameIndex = cut.FindAll(".library-tree-menu-label").Select(el => el.TextContent.Trim()).ToList().IndexOf("Rename");
        _ = cut.InvokeAsync(() => cut.FindAll(".mud-menu-item")[renameIndex].Click());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-file-ops-name-field input")));
        await cut.InvokeAsync(() => cut.Find(".library-file-ops-name-field input").ChangeAsync("New"));
        _ = cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button")[0].Click());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").First(b => b.TextContent.Trim() == "Cancel").ClickAsync());

        Assert.False(File.Exists(Path.Combine(root, "New.md")));
        Assert.True(File.Exists(Path.Combine(root, "Old.md")));
        Assert.Equal("Old.md", cut.FindComponent<LibraryDocument>().Instance.Path.RelativePath);
        Assert.Equal("Old.md ●", cut.Find(".library-document-path").TextContent.Trim());
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

        await cut.InvokeAsync(() => cut.Find("div.mud-treeview-item-arrow button").ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("button.library-tree-actions-button[aria-label='Actions for Sub']")));
        await cut.InvokeAsync(() => cut.Find("button.library-tree-actions-button[aria-label='Actions for Sub']").ClickAsync());
        int deleteIndex = cut.FindAll(".library-tree-menu-label").Select(el => el.TextContent.Trim()).ToList().IndexOf("Delete");
        _ = cut.InvokeAsync(() => cut.FindAll(".mud-menu-item")[deleteIndex].Click());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").First(b => b.TextContent.Trim() == "Delete").ClickAsync());

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

    /// <summary>[6.5] A null, empty or whitespace <c>Filter</c> shows the tree unchanged: no result list, and the
    /// tree's wrapper is not hidden (Spec §6.8).</summary>
    /// <param name="filter">The filter the host passes.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Filter_NullOrBlank_ShowsTheTree(string? filter)
    {
        SeedTeamFile("Business", "brief.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupStorage(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderHost(ctx, [new LibraryLocation("teams", "Business")], filter);

        cut.WaitForAssertion(() => Assert.Equal(BusinessOnly, NodeNames(cut)));
        Assert.Empty(cut.FindAll(".library-search-results"));
        Assert.False(cut.Find("div.library-tree").ClassList.Contains("hidden"));
    }

    /// <summary>[6.5] A filter lists every name match as a <c>.library-search-hit</c> carrying its root-relative
    /// <c>data-path</c>, its name and its folder path relative to the scope, in walk order, folders as well as
    /// files, with neither the truncated alert nor the empty text.</summary>
    [Fact]
    public async Task Filter_Brief_ShowsHitsWithFolderPaths()
    {
        string team = this.SeedTeam("Business");
        Directory.CreateDirectory(Path.Combine(team, "Briefing"));
        Directory.CreateDirectory(Path.Combine(team, "Plans", "Q4"));
        File.WriteAllText(Path.Combine(team, "brief.md"), "top");
        File.WriteAllText(Path.Combine(team, "other.md"), "no match");
        File.WriteAllText(Path.Combine(team, "Plans", "skip.txt"), "no match");
        File.WriteAllText(Path.Combine(team, "Plans", "Q4 brief.md"), "one level down");
        File.WriteAllText(Path.Combine(team, "Plans", "Q4", "deep brief.md"), "two levels down");

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupStorage(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderHost(ctx, [new LibraryLocation("teams", "Business")], "brief");

        List<(string Path, string Name, string Folder)> expected =
        [
            ("Business/Briefing", "Briefing", string.Empty),
            ("Business/brief.md", "brief.md", string.Empty),
            ("Business/Plans/Q4 brief.md", "Q4 brief.md", "Plans"),
            ("Business/Plans/Q4/deep brief.md", "deep brief.md", "Plans/Q4"),
        ];
        cut.WaitForAssertion(() => Assert.Equal(expected, Hits(cut)));
        Assert.Empty(cut.FindAll(".library-search-truncated"));
        Assert.Empty(cut.FindAll(".library-search-empty"));
    }

    /// <summary>[6.5, corrections item 26] While filtered the tree is hidden but never unmounted (nothing
    /// persists its expansion): the same <see cref="LibraryTree"/> instance stays, its wrapper carries
    /// <c>hidden</c>, and the result list is rendered beside it, in the same parent.</summary>
    [Fact]
    public async Task Filter_Brief_HidesTheTreeButKeepsItMounted()
    {
        SeedTeamFile("Business", "brief.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupStorage(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderHost(ctx, [new LibraryLocation("teams", "Business")], null);
        cut.WaitForAssertion(() => Assert.Equal(BusinessOnly, NodeNames(cut)));
        LibraryTree treeBefore = cut.FindComponent<LibraryTree>().Instance;

        FilterHost host = cut.FindComponent<FilterHost>().Instance;
        await cut.InvokeAsync(() => host.SetFilterAsync("brief"));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-search-results")));
        Assert.True(cut.Find("div.library-tree").ClassList.Contains("hidden"));
        Assert.Same(treeBefore, cut.FindComponent<LibraryTree>().Instance);
        AngleSharp.Dom.IElement? parent = cut.Find("div.library-tree").ParentElement;
        Assert.NotNull(parent);
        Assert.Single(parent.Children, child => child.ClassList.Contains("library-search-results"));
    }

    /// <summary>[6.5] Clicking a FILE hit opens it in the document area, exactly as a tree click does: the
    /// document component points at the hit's path and renders its body.</summary>
    [Fact]
    public async Task Filter_FileHit_Click_OpensTheDocument()
    {
        SeedTeamFile("Business", "brief.md", "the brief body");

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupStorage(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderHost(ctx, [new LibraryLocation("teams", "Business")], "brief");
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".library-search-hit")));

        await cut.InvokeAsync(() => cut.Find(".library-search-hit[data-path='Business/brief.md']").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal("Business/brief.md", cut.FindComponent<LibraryDocument>().Instance.Path.RelativePath));
        cut.WaitForAssertion(() => Assert.Equal("the brief body", cut.Find(".library-rendered").TextContent.Trim()));
    }

    /// <summary>[6.5, corrections items 22, 27, 28] Clicking a FOLDER hit through a parent that binds
    /// <c>FilterChanged</c> back: the result list goes, the parent's filter becomes null, <c>FilterChanged</c> is
    /// raised exactly once with null, and the folder is revealed in the tree (its ancestors expanded, the folder
    /// selected, siblings left collapsed).</summary>
    [Fact]
    public async Task Filter_FolderHit_Click_ClearsTheFilterAndRevealsTheFolder()
    {
        string team = this.SeedTeam("Business");
        Directory.CreateDirectory(Path.Combine(team, "Other"));
        File.WriteAllText(Path.Combine(team, "Other", "hidden-child.md"), "collapsed");
        Directory.CreateDirectory(Path.Combine(team, "Plans", "Q4"));

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupStorage(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderHost(ctx, [new LibraryLocation("teams", "Business")], "Q4");
        FilterHost host = cut.FindComponent<FilterHost>().Instance;
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".library-search-hit[data-path='Business/Plans/Q4']")));

        await cut.InvokeAsync(() => cut.Find(".library-search-hit[data-path='Business/Plans/Q4']").ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".library-search-results")));
        Assert.Single(host.Changes);
        Assert.Null(host.Changes[0]);
        Assert.Null(host.CurrentFilter);
        Assert.False(cut.Find("div.library-tree").ClassList.Contains("hidden"));
        List<string> expectedNames = ["Business", "Other", "Plans", "Q4"];
        cut.WaitForAssertion(() => Assert.Equal(expectedNames, NodeNames(cut)));
        List<string> expectedSelected = ["Q4"];
        cut.WaitForAssertion(() => Assert.Equal(expectedSelected, SelectedNames(cut)));
    }

    /// <summary>[6.5, corrections item 27] The same reveal in a flattened tree (<c>ShowScopeRoot</c> false), where
    /// the scope's own children are the top nodes: the nested folder is revealed and selected.</summary>
    [Fact]
    public async Task Filter_FolderHit_InFlattenedTree_RevealsTheFolder()
    {
        string team = this.SeedTeam("Business");
        Directory.CreateDirectory(Path.Combine(team, "Other"));
        Directory.CreateDirectory(Path.Combine(team, "Plans", "Q4"));

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupStorage(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderHost(
            ctx, [new LibraryLocation("teams", "Business")], "Q4", showScopeRoot: false);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".library-search-hit[data-path='Business/Plans/Q4']")));

        await cut.InvokeAsync(() => cut.Find(".library-search-hit[data-path='Business/Plans/Q4']").ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".library-search-results")));
        List<string> expectedNames = ["Other", "Plans", "Q4"];
        cut.WaitForAssertion(() => Assert.Equal(expectedNames, NodeNames(cut)));
        List<string> expectedSelected = ["Q4"];
        cut.WaitForAssertion(() => Assert.Equal(expectedSelected, SelectedNames(cut)));
    }

    /// <summary>[6.5, corrections item 28] A folder hit drops the results locally: a parent that ignores
    /// <c>FilterChanged</c> and keeps passing the same filter is re-rendered and the results stay gone (the
    /// explorer searches only when the normalised filter changes, and never writes its own <c>Filter</c>).</summary>
    [Fact]
    public async Task Filter_FolderHit_ParentIgnoresFilterChanged_ResultsStayGone()
    {
        string team = this.SeedTeam("Business");
        Directory.CreateDirectory(Path.Combine(team, "Plans", "Q4"));

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupStorage(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderHost(
            ctx, [new LibraryLocation("teams", "Business")], "Q4", bindBack: false);
        FilterHost host = cut.FindComponent<FilterHost>().Instance;
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".library-search-hit")));

        await cut.InvokeAsync(() => cut.Find(".library-search-hit[data-path='Business/Plans/Q4']").ClickAsync());
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".library-search-results")));
        await cut.InvokeAsync(host.RerenderAsync);

        Assert.Empty(cut.FindAll(".library-search-results"));
        Assert.Equal("Q4", host.CurrentFilter);
        Assert.Single(host.Changes);
    }

    /// <summary>[6.5, corrections item 23] 201 matching files: 200 hits render and the status alert says so, with
    /// the settled text (an em dash, U+2014) on the class-hooked element.</summary>
    [Fact]
    public async Task Filter_Truncated_ShowsTheMatchesAlert()
    {
        SeedNotes(this.SeedTeam("Business"), 201);

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupStorage(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderHost(ctx, [new LibraryLocation("teams", "Business")], "note");

        cut.WaitForAssertion(() => Assert.Equal(200, cut.FindAll(".library-search-hit").Count), TimeSpan.FromSeconds(10));
        AngleSharp.Dom.IElement alert = cut.Find(".library-search-truncated");
        Assert.Equal("Showing the first 200 matches — refine the search.", alert.TextContent.Trim());
        Assert.Equal("status", alert.GetAttribute("role"));
        Assert.Empty(cut.FindAll(".library-search-empty"));
    }

    /// <summary>[6.5] Exactly 200 matching files is not truncated: 200 hits and no alert.</summary>
    [Fact]
    public async Task Filter_ExactlyTwoHundredHits_ShowsNoAlert()
    {
        SeedNotes(this.SeedTeam("Business"), 200);

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupStorage(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderHost(ctx, [new LibraryLocation("teams", "Business")], "note");

        cut.WaitForAssertion(() => Assert.Equal(200, cut.FindAll(".library-search-hit").Count), TimeSpan.FromSeconds(10));
        Assert.Empty(cut.FindAll(".library-search-truncated"));
    }

    /// <summary>[6.5] A filter matching nothing shows <c>No files match "{term}".</c> and no hits.</summary>
    [Fact]
    public async Task Filter_NoHits_ShowsTheNoFilesText()
    {
        SeedTeamFile("Business", "other.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupStorage(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderHost(ctx, [new LibraryLocation("teams", "Business")], "zzz");

        cut.WaitForAssertion(() => Assert.Equal("No files match \"zzz\".", cut.Find(".library-search-empty").TextContent.Trim()));
        Assert.Empty(cut.FindAll(".library-search-hit"));
        Assert.Empty(cut.FindAll(".library-search-truncated"));
    }

    /// <summary>[6.5, corrections item 24] Two scopes: hits from both in scope order, capped at 200 in total.
    /// Scope 1 returning exactly 200 hits still probes scope 2 (with a max of 1) to decide the alert: one match
    /// there truncates, none does not; exactly 200 across both scopes is not truncated.</summary>
    /// <param name="businessFiles">Matching files in the first scope.</param>
    /// <param name="marketingFiles">Matching files in the second scope.</param>
    /// <param name="expectedBusiness">Hits expected from the first scope.</param>
    /// <param name="expectedMarketing">Hits expected from the second scope.</param>
    /// <param name="truncated">Whether the alert is expected.</param>
    [Theory]
    [InlineData(200, 1, 200, 0, true)]
    [InlineData(200, 0, 200, 0, false)]
    [InlineData(199, 1, 199, 1, false)]
    [InlineData(3, 2, 3, 2, false)]
    [InlineData(150, 100, 150, 50, true)]
    public async Task Filter_TwoScopes_ShowsBothInScopeOrderCappedAtTwoHundred(
        int businessFiles, int marketingFiles, int expectedBusiness, int expectedMarketing, bool truncated)
    {
        SeedNotes(this.SeedTeam("Business"), businessFiles);
        SeedNotes(this.SeedTeam("Marketing"), marketingFiles);

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupStorage(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderHost(
            ctx, [new LibraryLocation("teams", "Business"), new LibraryLocation("teams", "Marketing")], "note");

        List<string> expected = [.. Enumerable.Repeat("Business", expectedBusiness), .. Enumerable.Repeat("Marketing", expectedMarketing)];
        cut.WaitForAssertion(
            () => Assert.Equal(expected, [.. cut.FindAll(".library-search-hit").Select(hit => (hit.GetAttribute("data-path") ?? string.Empty).Split('/')[0])]),
            TimeSpan.FromSeconds(10));
        Assert.Equal(truncated ? 1 : 0, cut.FindAll(".library-search-truncated").Count);
    }

    /// <summary>[6.5, corrections item 29] Each hit's folder path is relative to ITS OWN scope, built from
    /// <c>RelativePath</c>: a hit under the second scope reads <c>Ideas</c>, not <c>Marketing/Ideas</c>.</summary>
    [Fact]
    public async Task Filter_TwoScopes_HitPathsAreRelativeToEachScope()
    {
        string business = this.SeedTeam("Business");
        Directory.CreateDirectory(Path.Combine(business, "Plans"));
        File.WriteAllText(Path.Combine(business, "Plans", "brief a.md"), "a");
        string marketing = this.SeedTeam("Marketing");
        Directory.CreateDirectory(Path.Combine(marketing, "Ideas"));
        File.WriteAllText(Path.Combine(marketing, "Ideas", "brief b.md"), "b");

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupStorage(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderHost(
            ctx, [new LibraryLocation("teams", "Business"), new LibraryLocation("teams", "Marketing")], "brief");

        List<(string Path, string Name, string Folder)> expected =
        [
            ("Business/Plans/brief a.md", "brief a.md", "Plans"),
            ("Marketing/Ideas/brief b.md", "brief b.md", "Ideas"),
        ];
        cut.WaitForAssertion(() => Assert.Equal(expected, Hits(cut)));
    }

    /// <summary>[6.5] <c>brief</c> then <c>notes</c> in succession: only the <c>notes</c> hits render. Green on
    /// arrival by design: the walk is synchronous, so no superseded result can land late.</summary>
    [Fact]
    public async Task Filter_ChangedQuickly_ShowsOnlyTheLatestResults()
    {
        string team = this.SeedTeam("Business");
        File.WriteAllText(Path.Combine(team, "brief.md"), "b");
        File.WriteAllText(Path.Combine(team, "notes.md"), "n");

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupStorage(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderHost(ctx, [new LibraryLocation("teams", "Business")], null);
        FilterHost host = cut.FindComponent<FilterHost>().Instance;

        await cut.InvokeAsync(() => host.SetFilterAsync("brief"));
        await cut.InvokeAsync(() => host.SetFilterAsync("notes"));

        List<(string Path, string Name, string Folder)> expected = [("Business/notes.md", "notes.md", string.Empty)];
        cut.WaitForAssertion(() => Assert.Equal(expected, Hits(cut)));
    }

    /// <summary>[6.5, corrections item 28] The explorer searches only when the normalised filter changes: a
    /// parent re-render, and the same term with padding, both keep the earlier results (a file added since is
    /// not listed); a genuinely different term searches again and sees it.</summary>
    [Fact]
    public async Task Filter_SameNormalisedTerm_DoesNotSearchAgain()
    {
        string team = this.SeedTeam("Business");
        File.WriteAllText(Path.Combine(team, "brief one.md"), "1");

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupStorage(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderHost(ctx, [new LibraryLocation("teams", "Business")], "brief");
        FilterHost host = cut.FindComponent<FilterHost>().Instance;
        List<(string Path, string Name, string Folder)> onlyOne = [("Business/brief one.md", "brief one.md", string.Empty)];
        cut.WaitForAssertion(() => Assert.Equal(onlyOne, Hits(cut)));

        File.WriteAllText(Path.Combine(team, "brief two.md"), "2");
        await cut.InvokeAsync(host.RerenderAsync);
        await cut.InvokeAsync(() => host.SetFilterAsync("  brief  "));

        Assert.Equal(onlyOne, Hits(cut));

        await cut.InvokeAsync(() => host.SetFilterAsync("two"));

        List<(string Path, string Name, string Folder)> onlyTwo = [("Business/brief two.md", "brief two.md", string.Empty)];
        cut.WaitForAssertion(() => Assert.Equal(onlyTwo, Hits(cut)));
    }

    /// <summary>[6.5, corrections item 26] The parent clearing the filter restores the tree with its expansion:
    /// a folder expanded before filtering is still expanded afterwards, on the same tree instance.</summary>
    [Fact]
    public async Task Filter_Cleared_RestoresTheTreeWithItsExpansion()
    {
        string team = this.SeedTeam("Business");
        Directory.CreateDirectory(Path.Combine(team, "Plans"));
        File.WriteAllText(Path.Combine(team, "Plans", "roadmap.md"), "r");

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupStorage(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderHost(ctx, [new LibraryLocation("teams", "Business")], null);
        FilterHost host = cut.FindComponent<FilterHost>().Instance;
        cut.WaitForAssertion(() => Assert.Equal(BusinessOnly, NodeNames(cut)));
        await cut.InvokeAsync(() => cut.Find("div.mud-treeview-item-arrow button").ClickAsync());
        List<string> expandedNames = ["Business", "Plans"];
        cut.WaitForAssertion(() => Assert.Equal(expandedNames, NodeNames(cut)));
        LibraryTree treeBefore = cut.FindComponent<LibraryTree>().Instance;

        await cut.InvokeAsync(() => host.SetFilterAsync("roadmap"));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".library-search-hit")));
        await cut.InvokeAsync(() => host.SetFilterAsync(null));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".library-search-results")));
        Assert.False(cut.Find("div.library-tree").ClassList.Contains("hidden"));
        Assert.Equal(expandedNames, NodeNames(cut));
        Assert.Same(treeBefore, cut.FindComponent<LibraryTree>().Instance);
    }

    /// <summary>Creates the Team folder <paramref name="team"/> under the Teams root and returns its path.</summary>
    private string SeedTeam(string team)
    {
        string path = Path.Combine(this.fixture.LibraryFixture.DataDir, "Teams", team);
        Directory.CreateDirectory(path);
        return path;
    }

    private void SeedTeamFile(string team, string fileName, string content = "body") =>
        File.WriteAllText(Path.Combine(this.SeedTeam(team), fileName), content);

    /// <summary>Writes <paramref name="count"/> files named <c>note-000.md</c>, <c>note-001.md</c>, ... into <paramref name="folder"/>.</summary>
    private static void SeedNotes(string folder, int count)
    {
        for (int index = 0; index < count; index++)
        {
            File.WriteAllText(Path.Combine(folder, string.Create(CultureInfo.InvariantCulture, $"note-{index:D3}.md")), "n");
        }
    }

    private static void SetupStorage(MudBunitContext ctx)
    {
        ctx.JSInterop.SetupVoid("huddleStorage.set", _ => true).SetVoidResult();
        ctx.JSInterop.Setup<string?>("huddleStorage.get", _ => true).SetResult(null);
    }

    private static List<string> NodeNames(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll("span.library-tree-node-name").Select(node => node.TextContent.Trim())];

    private static List<string> SelectedNames(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll("div.mud-treeview-item-selected span.library-tree-node-name").Select(node => node.TextContent.Trim())];

    private static List<(string Path, string Name, string Folder)> Hits(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll(".library-search-hit").Select(hit => (
            hit.GetAttribute("data-path") ?? string.Empty,
            hit.QuerySelector(".library-search-hit-name")?.TextContent.Trim() ?? string.Empty,
            hit.QuerySelector(".library-search-hit-path")?.TextContent.Trim() ?? string.Empty))];

    private static IRenderedComponent<ContainerFragment> RenderHost(
        MudBunitContext ctx,
        IReadOnlyList<LibraryLocation> scopes,
        string? filter,
        bool showScopeRoot = true,
        bool bindBack = true) => ctx.RenderWithPopovers(builder =>
    {
        builder.OpenComponent<FilterHost>(0);
        builder.AddAttribute(1, nameof(FilterHost.Scopes), scopes);
        builder.AddAttribute(2, nameof(FilterHost.InitialFilter), filter);
        builder.AddAttribute(3, nameof(FilterHost.ShowScopeRoot), showScopeRoot);
        builder.AddAttribute(4, nameof(FilterHost.BindBack), bindBack);
        builder.CloseComponent();
    });

    /// <summary>
    /// A parent that holds the filter and binds <see cref="LibraryExplorer.FilterChanged"/> back to it: the only
    /// real test of the two-way loop (the test project has no <c>.razor</c> files). <see cref="BindBack"/> false
    /// models a parent that ignores the callback and keeps passing its own value.
    /// </summary>
    private sealed class FilterHost : ComponentBase
    {
        private string? filter;

        /// <summary>The scopes handed to the explorer.</summary>
        [Parameter]
        public IReadOnlyList<LibraryLocation> Scopes { get; set; } = [];

        /// <summary>The filter the host starts with.</summary>
        [Parameter]
        public string? InitialFilter { get; set; }

        /// <summary>Forwarded to the explorer's <c>ShowScopeRoot</c>.</summary>
        [Parameter]
        public bool ShowScopeRoot { get; set; } = true;

        /// <summary>Whether <c>FilterChanged</c> updates the host's own filter.</summary>
        [Parameter]
        public bool BindBack { get; set; } = true;

        /// <summary>Every value the explorer raised through <c>FilterChanged</c>, in order.</summary>
        public List<string?> Changes { get; } = [];

        /// <summary>The filter the host currently passes down.</summary>
        public string? CurrentFilter => this.filter;

        /// <summary>Sets the filter and re-renders the host.</summary>
        /// <param name="value">The new filter.</param>
        public Task SetFilterAsync(string? value)
        {
            this.filter = value;
            return this.InvokeAsync(this.StateHasChanged);
        }

        /// <summary>Re-renders the host with the filter unchanged.</summary>
        public Task RerenderAsync() => this.InvokeAsync(this.StateHasChanged);

        /// <inheritdoc/>
        protected override void OnInitialized() => this.filter = this.InitialFilter;

        /// <inheritdoc/>
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<LibraryExplorer>(0);
            builder.AddAttribute(1, nameof(LibraryExplorer.Scopes), this.Scopes);
            builder.AddAttribute(2, nameof(LibraryExplorer.ShowScopeRoot), this.ShowScopeRoot);
            builder.AddAttribute(3, nameof(LibraryExplorer.StateKey), "test");
            builder.AddAttribute(4, nameof(LibraryExplorer.Filter), this.filter);
            builder.AddAttribute(5, nameof(LibraryExplorer.FilterChanged), EventCallback.Factory.Create<string?>(this, this.OnFilterChanged));
            builder.CloseComponent();
        }

        private void OnFilterChanged(string? value)
        {
            this.Changes.Add(value);
            if (this.BindBack)
            {
                this.filter = value;
            }
        }
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

    /// <summary>Sets up the remembered-state JS calls an explorer makes when a document opens.</summary>
    private static void ArrangeStorage(MudBunitContext ctx)
    {
        ctx.JSInterop.SetupVoid("huddleStorage.set", _ => true).SetVoidResult();
        ctx.JSInterop.Setup<string?>("huddleStorage.get", _ => true).SetResult(null);
    }

    /// <summary>Every tree node name currently rendered, ordinally sorted so the assertion does not depend on
    /// the tree's own ordering.</summary>
    private static List<string> SortedNodeNames(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll("span.library-tree-node-name").Select(node => node.TextContent.Trim()).Order(StringComparer.Ordinal)];

    /// <summary>Expands the first expandable tree node.</summary>
    private static async Task ExpandFirstNodeAsync(IRenderedComponent<ContainerFragment> cut)
    {
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("div.mud-treeview-item-arrow button")));
        await cut.InvokeAsync(() => cut.Find("div.mud-treeview-item-arrow button").ClickAsync());
    }

    /// <summary>Clicks the tree row named <paramref name="nodeName"/>, selecting it.</summary>
    private static async Task ClickNodeAsync(IRenderedComponent<ContainerFragment> cut, string nodeName)
    {
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".library-tree-node-name"), node => node.TextContent.Trim() == nodeName));
        await cut.InvokeAsync(() => cut.FindAll(".library-tree-node-name").Single(node => node.TextContent.Trim() == nodeName).ClickAsync());
    }

    /// <summary>Waits for the New note dialog, types <paramref name="name"/>, confirms, and waits for
    /// <paramref name="pending"/> (the <c>NewNoteAsync</c> call) to complete.</summary>
    private static async Task ConfirmNewNoteAsync(
        IRenderedComponent<ContainerFragment> cut, Task pending, string name, CancellationToken ct)
    {
        cut.WaitForAssertion(() => Assert.Equal("New note", cut.Find(".mud-dialog-title").TextContent.Trim()));
        await cut.InvokeAsync(() => cut.Find(".library-file-ops-name-field input").ChangeAsync(name));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button")[0].ClickAsync());
        await pending.WaitAsync(TimeSpan.FromSeconds(5), ct);
    }

    private static IRenderedComponent<ContainerFragment> RenderExplorer(
        MudBunitContext ctx,
        IReadOnlyList<LibraryLocation>? scopes,
        string? title = null,
        LibraryExplorerLayout layout = LibraryExplorerLayout.Stacked,
        string? initialFile = null,
        string stateKey = "test",
        EventCallback<LibraryPath> onOpenInLibrary = default,
        bool showScopeRoot = true) => ctx.RenderWithPopovers(builder =>
    {
        builder.OpenComponent<LibraryExplorer>(0);
        builder.AddAttribute(1, nameof(LibraryExplorer.Scopes), scopes);
        builder.AddAttribute(2, nameof(LibraryExplorer.Title), title);
        builder.AddAttribute(3, nameof(LibraryExplorer.Layout), layout);
        builder.AddAttribute(4, nameof(LibraryExplorer.InitialFile), initialFile);
        builder.AddAttribute(5, nameof(LibraryExplorer.StateKey), stateKey);
        builder.AddAttribute(6, nameof(LibraryExplorer.OnOpenInLibrary), onOpenInLibrary);
        builder.AddAttribute(7, nameof(LibraryExplorer.ShowScopeRoot), showScopeRoot);
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
                this.personas, this.LibraryFixture.RootStore, this.LibraryFixture.Resolver, NullLogger<TeamFolderProvisioner>.Instance, catalog: new FakeTeamCatalog()));
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
