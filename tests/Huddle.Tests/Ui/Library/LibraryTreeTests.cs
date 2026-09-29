using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Library;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>
/// Pins <c>LibraryTree</c> (Task 12.1): lazy folders, roles, protections, menus, orphans and the
/// empty state, over a REAL <see cref="LibraryFileService"/> on a temp tree (Spec §6.4, §6.2, §8,
/// §10; corrections-B6 items 18, 22, 24-27). The tree binds <c>MudTreeView&lt;LibraryPath&gt;</c>
/// with a comparer on (Root.Id, RelativePath) rather than <see cref="LibraryEntry"/> (item 22), so
/// selection and expansion survive a refresh that changes a file's length or last-write time.
/// </summary>
public sealed class LibraryTreeTests : IDisposable
{
    private readonly TreeFixture fixture = TreeFixture.Build();

    /// <summary>Disposes the underlying temp <c>DataDir</c> and stores.</summary>
    public void Dispose() => this.fixture.Dispose();

    /// <summary>One top-level tree node is rendered per entry in <c>Scopes</c>, in order, before any expansion.</summary>
    [Fact]
    public async Task Render_OneTopNodePerScope()
    {
        LibraryPath teamsScope = this.fixture.LibraryFixture.ResolveTeams(string.Empty);
        string notesPath = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        LibraryPath notesScope = this.fixture.LibraryFixture.Resolve(notesPath, string.Empty);

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTree(ctx, [teamsScope, notesScope]);

        Assert.Equal(
            [teamsScope.Root.DisplayName, notesScope.Root.DisplayName],
            cut.FindAll("span.library-tree-node-name").Select(e => e.TextContent.Trim()));
    }

    /// <summary>
    /// <c>ShowScopeRoot="false"</c> with exactly one Scope skips that scope's own wrapper row
    /// entirely: its children render as the top-level nodes directly, with no "Actions for {scope's
    /// name}" row anywhere in the tree.
    /// </summary>
    [Fact]
    public async Task ShowScopeRootFalse_FlattensToTheScopesOwnChildren()
    {
        string notesPath = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        Directory.CreateDirectory(Path.Combine(notesPath, "Sub"));
        File.WriteAllText(Path.Combine(notesPath, "a.md"), "hello");
        LibraryPath notesScope = this.fixture.LibraryFixture.Resolve(notesPath, string.Empty);

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTree(ctx, [notesScope], showScopeRoot: false);

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("span.library-tree-node-name").Count));
        Assert.Equal(
            ["Sub", "a.md"],
            cut.FindAll("span.library-tree-node-name").Select(e => e.TextContent.Trim()));
        Assert.Empty(cut.FindAll($"button[aria-label='Actions for {notesScope.Root.DisplayName}']"));
    }

    /// <summary>Flattened, a Refresh (RootsChanged) reloads the scope's own children as the new top-level nodes and keeps a still-expanded child's own children loaded.</summary>
    [Fact]
    public async Task ShowScopeRootFalse_Refresh_ReloadsChildrenAndKeepsExpansion()
    {
        string notesPath = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        Directory.CreateDirectory(Path.Combine(notesPath, "Sub"));
        File.WriteAllText(Path.Combine(notesPath, "Sub", "a.md"), "hello");
        LibraryPath notesScope = this.fixture.LibraryFixture.Resolve(notesPath, string.Empty);

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTree(ctx, [notesScope], showScopeRoot: false);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("span.library-tree-node-name")));
        await cut.InvokeAsync(() => cut.Find("div.mud-treeview-item-arrow button").ClickAsync());
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("span.library-tree-node-name").Count));

        File.WriteAllText(Path.Combine(notesPath, "b.md"), "hello");
        await cut.InvokeAsync(() => cut.FindComponent<Agency.Huddle.App.Components.Library.LibraryTree>().Instance.RefreshAsync());

        cut.WaitForAssertion(() => Assert.Equal(
            ["Sub", "a.md", "b.md"],
            cut.FindAll("span.library-tree-node-name").Select(e => e.TextContent.Trim())));
    }

    /// <summary>A folder's children are not in the DOM until its node is expanded, and appear after (Spec §6.4, ServerData).</summary>
    [Fact]
    public async Task Expand_LoadsChildrenLazily()
    {
        string notesPath = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(notesPath, "a.md"), "hello");
        LibraryPath notesScope = this.fixture.LibraryFixture.Resolve(notesPath, string.Empty);

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTree(ctx, [notesScope]);

        Assert.Single(cut.FindAll("span.library-tree-node-name"));

        await cut.InvokeAsync(() => cut.Find("div.mud-treeview-item-arrow button").ClickAsync());
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("span.library-tree-node-name").Count));

        Assert.Equal(
            [notesScope.Root.DisplayName, "a.md"],
            cut.FindAll("span.library-tree-node-name").Select(e => e.TextContent.Trim()));
    }

    /// <summary>A file's context menu lists every <see cref="LibraryTreeActionKind"/>, none of them disabled.</summary>
    [Fact]
    public async Task Menu_OnFile_HasEveryAction()
    {
        string notesPath = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(notesPath, "a.md"), "hello");
        LibraryPath filePath = this.fixture.LibraryFixture.Resolve(notesPath, "a.md");
        LibraryPath notesScope = this.fixture.LibraryFixture.Resolve(notesPath, string.Empty);

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTree(ctx, [notesScope]);
        await cut.InvokeAsync(() => cut.Find("div.mud-treeview-item-arrow button").ClickAsync());
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("span.library-tree-node-name").Count));

        await cut.InvokeAsync(() => cut.Find($"button.library-tree-actions-button[aria-label='Actions for {filePath.RelativePath}']").ClickAsync());

        Assert.Equal(
            ["New note", "New folder", "Rename", "Move", "Copy path", "Open in default app", "Delete"],
            cut.FindAll("div.mud-menu-item div.library-tree-menu-label").Select(e => e.TextContent.Trim()));
    }

    /// <summary>A Team folder's menu offers New Project, and Rename/Move/Delete are disabled with the settled protected text.</summary>
    [Fact]
    public async Task Menu_OnTeamFolder_HasNewProject_AndDisabledRenameDelete()
    {
        string teamsPath = this.fixture.LibraryFixture.DataDir;
        Directory.CreateDirectory(Path.Combine(teamsPath, "Teams", "Engineering"));
        this.fixture.LibraryFixture.Reload();
        this.fixture.RescanTasks();
        LibraryPath teamsScope = this.fixture.LibraryFixture.ResolveTeams(string.Empty);
        LibraryPath teamFolder = this.fixture.LibraryFixture.ResolveTeams("Engineering");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTree(ctx, [teamsScope]);
        await cut.InvokeAsync(() => cut.Find("div.mud-treeview-item-arrow button").ClickAsync());
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("span.library-tree-node-name").Count));

        await cut.InvokeAsync(() => cut.Find($"button.library-tree-actions-button[aria-label='Actions for {teamFolder.RelativePath}']").ClickAsync());

        Assert.Contains(
            cut.FindAll("div.mud-menu-item div.library-tree-menu-label"),
            e => string.Equals(e.TextContent.Trim(), "New Project", StringComparison.Ordinal));

        var renameItem = cut.FindAll("div.mud-menu-item").Single(e =>
            e.QuerySelector("div.library-tree-menu-label")?.TextContent.Trim() == "Rename");
        Assert.True(renameItem.ClassList.Contains("mud-disabled"));
        Assert.Equal(
            "Team and Project folders can't be renamed, moved or deleted here.",
            renameItem.QuerySelector("div.library-tree-menu-reason")?.TextContent.Trim());
    }

    /// <summary>A Teammate folder's menu is disabled with the settled teammate-page redirect text (item 26).</summary>
    [Fact]
    public async Task Menu_OnTeammateFolder_DisabledWithTeammateText()
    {
        this.fixture.LibraryFixture.CreateTeammate("ada", "Ada", "ada");
        LibraryPath teammatesScope = this.fixture.LibraryFixture.ResolveTeammatesFolder(string.Empty);
        LibraryPath teammateFolder = this.fixture.LibraryFixture.ResolveTeammatesFolder("ada");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTree(ctx, [teammatesScope]);
        await cut.InvokeAsync(() => cut.Find("div.mud-treeview-item-arrow button").ClickAsync());
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("span.library-tree-node-name").Count));

        await cut.InvokeAsync(() => cut.Find($"button.library-tree-actions-button[aria-label='Actions for {teammateFolder.RelativePath}']").ClickAsync());

        var renameItem = cut.FindAll("div.mud-menu-item").Single(e =>
            e.QuerySelector("div.library-tree-menu-label")?.TextContent.Trim() == "Rename");
        Assert.Equal(
            "Teammate folders can't be renamed, moved or deleted here.",
            renameItem.QuerySelector("div.library-tree-menu-reason")?.TextContent.Trim());
    }

    /// <summary>Right-clicking a node opens its own <c>MudMenu</c> (<c>ActivationEvent="RightClick"</c>, <c>PositionAtCursor</c>).</summary>
    [Fact]
    public async Task RightClick_OpensMenu()
    {
        string notesPath = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(notesPath, "a.md"), "hello");
        LibraryPath notesScope = this.fixture.LibraryFixture.Resolve(notesPath, string.Empty);

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTree(ctx, [notesScope]);

        await cut.InvokeAsync(() => cut.Find("div.library-tree-node").ContextMenuAsync());

        Assert.NotEmpty(cut.FindAll("div.mud-menu-item"));
    }

    /// <summary>An orphan Team folder (no Persona names it in <c>teams</c>) shows a warning icon whose <c>aria-label</c> names it (Spec §6.2).</summary>
    [Fact]
    public async Task OrphanTeam_ShowsWarningIcon()
    {
        Directory.CreateDirectory(Path.Combine(this.fixture.LibraryFixture.DataDir, "Teams", "Legacy"));
        this.fixture.LibraryFixture.Reload();
        this.fixture.RescanTasks();
        LibraryPath teamsScope = this.fixture.LibraryFixture.ResolveTeams(string.Empty);

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTree(ctx, [teamsScope]);
        await cut.InvokeAsync(() => cut.Find("div.mud-treeview-item-arrow button").ClickAsync());
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("span.library-tree-orphan")));

        Assert.Equal(
            "No teammate has the Team label \"Legacy\".",
            cut.Find("span.library-tree-orphan").GetAttribute("aria-label"));
    }

    /// <summary>An empty folder shows the settled message and a "New note" button rather than nothing.</summary>
    [Fact]
    public async Task EmptyFolder_ShowsMessageAndNewNote()
    {
        string notesPath = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        LibraryPath notesScope = this.fixture.LibraryFixture.Resolve(notesPath, string.Empty);

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTree(ctx, [notesScope]);
        await cut.InvokeAsync(() => cut.Find("div.mud-treeview-item-arrow button").ClickAsync());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("div.library-tree-empty")));
        Assert.Equal("No notes here yet.", cut.Find("div.library-tree-empty span.library-tree-empty-text").TextContent.Trim());
        Assert.Equal("New note", cut.Find("div.library-tree-empty button.library-tree-empty-new-note").TextContent.Trim());
    }

    /// <summary>A PINNED root whose directory is missing on disk shows "Folder not found" (item 24), checked with <see cref="Directory.Exists(string?)"/>.</summary>
    [Fact]
    public async Task MissingPinnedRoot_ShowsFolderNotFound()
    {
        string notesPath = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        LibraryPath notesScope = this.fixture.LibraryFixture.Resolve(notesPath, string.Empty);
        Directory.Delete(notesPath);

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTree(ctx, [notesScope]);

        Assert.Equal("Folder not found", cut.Find("div.library-tree-missing").TextContent.Trim());
    }

    /// <summary>A missing TEAM scope (a Team not yet provisioned on disk) shows an empty tree, never "Folder not found" (item 24).</summary>
    [Fact]
    public async Task MissingTeamScope_ShowsEmptyTree()
    {
        LibraryPath missingTeamScope = this.fixture.LibraryFixture.ResolveTeams("NeverCreated");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTree(ctx, [missingTeamScope]);

        Assert.Empty(cut.FindAll("div.library-tree-missing"));
        Assert.Single(cut.FindAll("span.library-tree-node-name"));

        // MudTreeViewItem always renders div.mud-treeview-item-arrow; with nothing to expand it's
        // empty, with no toggle button (MudBlazor 9.10 source; delivery-facts).
        Assert.Empty(cut.FindAll("div.mud-treeview-item-arrow button"));
    }

    /// <summary>Clicking a file node raises <c>OnOpenFile</c> with its resolved <see cref="LibraryPath"/>.</summary>
    [Fact]
    public async Task Click_File_RaisesOnOpenFile()
    {
        string notesPath = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(notesPath, "a.md"), "hello");
        LibraryPath notesScope = this.fixture.LibraryFixture.Resolve(notesPath, string.Empty);
        LibraryPath? opened = null;

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTree(
            ctx,
            [notesScope],
            onOpenFile: EventCallback.Factory.Create<LibraryPath>(this, p => opened = p));
        await cut.InvokeAsync(() => cut.Find("div.mud-treeview-item-arrow button").ClickAsync());
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("span.library-tree-node-name").Count));

        // CSS :last-of-type means "last sibling of its element type", not "last match in the
        // document", so pick the file node by its text.
        await cut.InvokeAsync(() => cut.FindAll("span.library-tree-node-name").Single(node => node.TextContent.Trim() == "a.md").ClickAsync());

        Assert.NotNull(opened);
        Assert.Equal("a.md", opened.RelativePath);
    }

    /// <summary>
    /// Clicking a Teammate's own <c>&lt;Name&gt;.md</c> raises <c>OnOpenFile</c> too. The resolver
    /// gives that file the <see cref="LibraryNodeRole.TeammateDefinition"/> role, not
    /// <see cref="LibraryNodeRole.File"/>, and a check on <c>File</c> alone left every definition
    /// unopenable, with no document panel and no Read/Edit/Split toggle.
    /// </summary>
    [Fact]
    public async Task Click_TeammateDefinition_RaisesOnOpenFile()
    {
        this.fixture.LibraryFixture.CreateTeammate("Jarvis", "Jarvis", "jar");
        LibraryPath teammateFolder = this.fixture.LibraryFixture.ResolveTeammatesFolder("Jarvis");
        LibraryPath? opened = null;

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTree(
            ctx,
            [teammateFolder],
            onOpenFile: EventCallback.Factory.Create<LibraryPath>(this, p => opened = p));
        await cut.InvokeAsync(() => cut.Find("div.mud-treeview-item-arrow button").ClickAsync());
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("span.library-tree-node-name").Count));

        await cut.InvokeAsync(() => cut.FindAll("span.library-tree-node-name").Single(node => node.TextContent.Trim() == "Jarvis.md").ClickAsync());

        Assert.NotNull(opened);
        Assert.Equal(LibraryNodeRole.TeammateDefinition, opened.Role);
        Assert.Equal("Jarvis/Jarvis.md", opened.RelativePath);
    }

    /// <summary>The "..." actions button is always in the DOM, not revealed only on hover, and carries the settled <c>aria-label</c> (item 27).</summary>
    [Fact]
    public async Task Render_ActionsButton_AlwaysVisible_WithAriaLabel()
    {
        string notesPath = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        LibraryPath notesScope = this.fixture.LibraryFixture.Resolve(notesPath, string.Empty);

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTree(ctx, [notesScope]);

        var button = cut.Find($"button.library-tree-actions-button[aria-label='Actions for {notesScope.Root.DisplayName}']");
        Assert.False(button.ClassList.Contains("library-tree-actions-hidden"));
    }

    /// <summary>A disabled menu item's reason is rendered as SECONDARY TEXT on the item, never tooltip-only (item 26).</summary>
    [Fact]
    public async Task Menu_OnTeamFolder_DisabledItems_ShowReasonAsSecondaryText()
    {
        Directory.CreateDirectory(Path.Combine(this.fixture.LibraryFixture.DataDir, "Teams", "Engineering"));
        this.fixture.LibraryFixture.Reload();
        this.fixture.RescanTasks();
        LibraryPath teamsScope = this.fixture.LibraryFixture.ResolveTeams(string.Empty);
        LibraryPath teamFolder = this.fixture.LibraryFixture.ResolveTeams("Engineering");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTree(ctx, [teamsScope]);
        await cut.InvokeAsync(() => cut.Find("div.mud-treeview-item-arrow button").ClickAsync());
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("span.library-tree-node-name").Count));
        await cut.InvokeAsync(() => cut.Find($"button.library-tree-actions-button[aria-label='Actions for {teamFolder.RelativePath}']").ClickAsync());

        foreach (string label in new[] { "Rename", "Move", "Delete" })
        {
            var item = cut.FindAll("div.mud-menu-item").Single(e =>
                e.QuerySelector("div.library-tree-menu-label")?.TextContent.Trim() == label);
            Assert.Equal(
                "Team and Project folders can't be renamed, moved or deleted here.",
                item.QuerySelector("div.library-tree-menu-reason")?.TextContent.Trim());
        }
    }

    /// <summary>Selecting a file, then changing its length on disk and refreshing, keeps it selected: the tree compares
    /// by (Root.Id, RelativePath), never <see cref="LibraryEntry"/> equality (item 22).</summary>
    [Fact]
    public async Task Selection_SurvivesRefresh()
    {
        string notesPath = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        string filePath = Path.Combine(notesPath, "a.md");
        File.WriteAllText(filePath, "hello");
        LibraryPath notesScope = this.fixture.LibraryFixture.Resolve(notesPath, string.Empty);
        LibraryPath fileNode = this.fixture.LibraryFixture.Resolve(notesPath, "a.md");
        LibraryPath? selected = fileNode;

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTree(
            ctx,
            [notesScope],
            selectedPath: fileNode,
            selectedPathChanged: EventCallback.Factory.Create<LibraryPath?>(this, p => selected = p));
        await cut.InvokeAsync(() => cut.Find("div.mud-treeview-item-arrow button").ClickAsync());
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("span.library-tree-node-name").Count));

        File.WriteAllText(filePath, "hello, much longer now");

        await cut.InvokeAsync(() => cut.FindComponent<LibraryTree>().Instance.RefreshAsync());

        Assert.NotNull(selected);
        Assert.Equal("a.md", selected.RelativePath);
        Assert.NotEmpty(cut.FindAll("div.mud-treeview-item-selected"));
    }

    /// <summary><c>_tasks</c> under a Team folder is hidden (Spec §6.16); a folder merely prefixed <c>x_</c> is an ordinary node (item matches Spec, not a correction number).</summary>
    [Fact]
    public async Task HiddenFolders_NotListed()
    {
        string teamPath = Path.Combine(this.fixture.LibraryFixture.DataDir, "Teams", "Engineering");
        Directory.CreateDirectory(Path.Combine(teamPath, "_tasks"));
        Directory.CreateDirectory(Path.Combine(teamPath, "x_tasks"));
        this.fixture.LibraryFixture.Reload();
        this.fixture.RescanTasks();
        LibraryPath teamsScope = this.fixture.LibraryFixture.ResolveTeams(string.Empty);
        LibraryPath teamFolder = this.fixture.LibraryFixture.ResolveTeams("Engineering");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTree(ctx, [teamsScope]);
        await cut.InvokeAsync(() => cut.Find("div.mud-treeview-item-arrow button").ClickAsync());
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("span.library-tree-node-name").Count));
        await cut.InvokeAsync(() => cut.FindAll("div.mud-treeview-item-arrow button")[1].ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("span.library-tree-node-name").Count));
        Assert.Equal(
            [teamsScope.Root.DisplayName, "Engineering", "x_tasks"],
            cut.FindAll("span.library-tree-node-name").Select(e => e.TextContent.Trim()));
    }

    /// <summary>A refresh reloads every currently expanded folder's children, not just the top level (item 22).</summary>
    [Fact]
    public async Task Refresh_ReloadsExpandedFolders()
    {
        string notesPath = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        LibraryPath notesScope = this.fixture.LibraryFixture.Resolve(notesPath, string.Empty);

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTree(ctx, [notesScope]);
        await cut.InvokeAsync(() => cut.Find("div.mud-treeview-item-arrow button").ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("div.library-tree-empty")));

        File.WriteAllText(Path.Combine(notesPath, "new.md"), "hello");
        await cut.InvokeAsync(() => cut.FindComponent<LibraryTree>().Instance.RefreshAsync());

        cut.WaitForAssertion(() => Assert.Contains(
            cut.FindAll("span.library-tree-node-name"),
            e => string.Equals(e.TextContent.Trim(), "new.md", StringComparison.Ordinal)));
    }

    /// <summary>A refresh keeps a NESTED expanded folder expanded: its children stay listed, not only the top item's.</summary>
    [Fact]
    public async Task RefreshAsync_KeepsNestedFoldersExpanded()
    {
        string notesPath = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        Directory.CreateDirectory(Path.Combine(notesPath, "Inner"));
        File.WriteAllText(Path.Combine(notesPath, "Inner", "a.md"), "hello");
        LibraryPath notesScope = this.fixture.LibraryFixture.Resolve(notesPath, string.Empty);

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTree(ctx, [notesScope]);
        await cut.InvokeAsync(() => cut.Find("div.mud-treeview-item-arrow button").ClickAsync());
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("span.library-tree-node-name").Count));
        await cut.InvokeAsync(() => cut.FindAll("div.mud-treeview-item-arrow button")[1].ClickAsync());
        List<string> namesBefore = [notesScope.Root.DisplayName, "Inner", "a.md"];
        cut.WaitForAssertion(() => Assert.Equal(
            namesBefore,
            cut.FindAll("span.library-tree-node-name").Select(e => e.TextContent.Trim()).ToList()));

        await cut.InvokeAsync(() => cut.FindComponent<LibraryTree>().Instance.RefreshAsync());

        cut.WaitForAssertion(() => Assert.Equal(
            namesBefore,
            cut.FindAll("span.library-tree-node-name").Select(e => e.TextContent.Trim()).ToList()));
    }

    /// <summary>A pinned root renamed in Settings (<see cref="LibraryRootStore.RootsChanged"/>, raised off the render thread) re-renders the tree's top-level label.</summary>
    [Fact]
    public async Task RootsChanged_Rerenders()
    {
        string notesPath = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        LibraryPath notesScope = this.fixture.LibraryFixture.Resolve(notesPath, string.Empty);

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTree(ctx, [notesScope]);

        PinnedRootEntry renamed = new("Renamed Notes", notesPath);
        _ = Task.Run(() => this.fixture.LibraryFixture.RootStore.Save([renamed]), Xunit.TestContext.Current.CancellationToken);

        cut.WaitForAssertion(() => Assert.Equal(
            "Renamed Notes",
            cut.Find("span.library-tree-node-name").TextContent.Trim()));
    }

    private static IRenderedComponent<ContainerFragment> RenderTree(
        MudBunitContext ctx,
        IReadOnlyList<LibraryPath> scopes,
        LibraryPath? selectedPath = null,
        EventCallback<LibraryPath?> selectedPathChanged = default,
        EventCallback<LibraryPath> onOpenFile = default,
        EventCallback<LibraryTreeAction> onAction = default,
        bool showScopeRoot = true) => ctx.RenderWithPopovers(builder =>
    {
        builder.OpenComponent<Agency.Huddle.App.Components.Library.LibraryTree>(0);
        builder.AddAttribute(1, nameof(Agency.Huddle.App.Components.Library.LibraryTree.Scopes), scopes);
        builder.AddAttribute(2, nameof(Agency.Huddle.App.Components.Library.LibraryTree.SelectedPath), selectedPath);
        builder.AddAttribute(3, nameof(Agency.Huddle.App.Components.Library.LibraryTree.SelectedPathChanged), selectedPathChanged);
        builder.AddAttribute(4, nameof(Agency.Huddle.App.Components.Library.LibraryTree.OnOpenFile), onOpenFile);
        builder.AddAttribute(5, nameof(Agency.Huddle.App.Components.Library.LibraryTree.OnAction), onAction);
        builder.AddAttribute(6, nameof(Agency.Huddle.App.Components.Library.LibraryTree.ShowScopeRoot), showScopeRoot);
        builder.CloseComponent();
    });

    /// <summary>An isolated fixture: a real <see cref="LibraryFileService"/> over a temp <c>DataDir</c>, plus a real
    /// <see cref="TaskStore"/>/<see cref="PersonaStore"/> pair so <see cref="TeamFolderCatalog.List"/> can decide
    /// orphan status the same way the board does (item 25).</summary>
    private sealed class TreeFixture : IDisposable
    {
        private readonly TempDataDir dir;
        private readonly PersonaStore personas;

        private TreeFixture(TempDataDir dir, LibraryFileServiceFixture libraryFixture, PersonaStore personas)
        {
            this.dir = dir;
            this.LibraryFixture = libraryFixture;
            this.personas = personas;
            this.Tasks = TestTaskStore.CreateTaskStore(dir, personas);
        }

        /// <summary>The real Library stack (resolver, root store, file service) over this fixture's temp <c>DataDir</c>.</summary>
        public LibraryFileServiceFixture LibraryFixture { get; }

        /// <summary>The board's Team folders, as <see cref="TeamFolderCatalog.List"/> reads them. Rebuilt by <see cref="RescanTasks"/>.</summary>
        public TaskStore Tasks { get; private set; }

        /// <summary>Builds a fixture with the standard Teams/Teammates layout, plus an empty <see cref="TaskStore"/>.</summary>
        public static TreeFixture Build()
        {
            TempDataDir dir = new();
            LibraryFileServiceFixture libraryFixture = LibraryFileServiceFixture.Attach(dir.Path);
            PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
            return new TreeFixture(dir, libraryFixture, personas);
        }

        /// <summary>Rebuilds <see cref="Tasks"/> so it reflects Team folders arranged on disk since this fixture was built.</summary>
        public void RescanTasks() => this.Tasks = TestTaskStore.CreateTaskStore(this.dir, this.personas);

        /// <summary>A <see cref="MudBunitContext"/> with this fixture's real <see cref="LibraryFileService"/>,
        /// <see cref="LibraryPathResolver"/>, <see cref="LibraryRootStore"/> and <see cref="TaskStore"/> registered.</summary>
        public MudBunitContext NewContext()
        {
            MudBunitContext ctx = new();
            ctx.Services.AddSingleton(this.LibraryFixture.Resolver);
            ctx.Services.AddSingleton(this.LibraryFixture.RootStore);
            ctx.Services.AddSingleton(this.LibraryFixture.CreateService());
            ctx.Services.AddSingleton(this.Tasks);
            return ctx;
        }

        /// <summary>Disposes the underlying stores and temp <c>DataDir</c>.</summary>
        public void Dispose()
        {
            this.Tasks.Dispose();
            this.personas.Dispose();
            this.LibraryFixture.Dispose();
            this.dir.Dispose();
        }
    }
}
