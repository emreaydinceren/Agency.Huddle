using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Library;
using Agency.Huddle.App.Library;
using static Agency.Huddle.Tests.Ui.Library.LibraryExplorerTests;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>
/// Pins the Library explorer's two halves mounted apart (<see cref="LibraryExplorerPart"/>): the tree is the Sidebar's
/// Library view (<see cref="LibrarySidebarTree"/>) and the document is the Library page's, meeting through
/// <see cref="LibraryBrowserBridge"/>.
/// </summary>
public sealed class LibraryExplorerPartTests : IDisposable
{
    private readonly ExplorerFixture fixture = ExplorerFixture.Build();

    /// <summary>Disposes the underlying temp <c>DataDir</c> and stores.</summary>
    public void Dispose() => this.fixture.Dispose();

    /// <summary>The Sidebar's half shows the search field, New note and the tree, and none of the document: no divider, no "No note open".</summary>
    [Fact]
    public async Task TreeOnly_ShowsTheTreeAndNoDocument()
    {
        _ = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");

        await using MudBunitContext ctx = this.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderPart(ctx, LibraryExplorerPart.TreeOnly, scopes: null);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("span.library-tree-node-name")));
        Assert.Single(cut.FindAll(".library-explorer-tree-header"));
        Assert.Empty(cut.FindAll(".library-explorer-split"));
        Assert.DoesNotContain("No note open", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>Opening a file in the Sidebar's tree opens the Library page on it, so the document appears in the main area.</summary>
    [Fact]
    public async Task TreeOnly_FileClick_OpensTheLibraryPageOnTheFile()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "a.md"), "# Hello");
        LibraryPath file = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderPart(ctx, LibraryExplorerPart.TreeOnly, [new LibraryLocation(file.Root.Id, string.Empty)], showScopeRoot: false);
        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll("span.library-tree-node-name"), node => node.TextContent.Trim() == "a.md"));

        await cut.InvokeAsync(() => cut.FindAll("span.library-tree-node-name").First(node => node.TextContent.Trim() == "a.md").Click());

        var nav = (Bunit.TestDoubles.BunitNavigationManager)ctx.Services.GetRequiredService<NavigationManager>();
        cut.WaitForAssertion(() => Assert.Equal(nav.ToAbsoluteUri(LibraryLinkQuery.BuildPageUri(file)).AbsoluteUri, nav.Uri));
    }

    /// <summary>The Library page's half shows the file it was given and no tree, and tells the bridge which file is open.</summary>
    [Fact]
    public async Task DocumentOnly_ShowsNoTree_AndPublishesTheOpenFile()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "a.md"), "# Hello");
        LibraryPath file = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderPart(
            ctx, LibraryExplorerPart.DocumentOnly, [new LibraryLocation(file.Root.Id, string.Empty)], initialFile: "a.md", initialRoot: file.Root.Id, showScopeRoot: false);

        LibraryBrowserBridge bridge = ctx.Services.GetRequiredService<LibraryBrowserBridge>();
        cut.WaitForAssertion(() => Assert.Equal(file.RelativePath, bridge.OpenPath?.RelativePath));
        Assert.Empty(cut.FindAll(".library-tree"));
        Assert.Empty(cut.FindAll(".library-explorer-tree-header"));
    }

    /// <summary>A file chosen in the Sidebar's tree replaces the open one: the page's document opens the new file when its address changes, and the tree is told.</summary>
    [Fact]
    public async Task DocumentOnly_ChangedInitialFile_OpensTheNewFile()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "a.md"), "# A");
        File.WriteAllText(Path.Combine(root, "b.md"), "# B");
        LibraryPath a = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.NewContext();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<DocumentHost>(0);
            builder.AddAttribute(1, nameof(DocumentHost.RootId), a.Root.Id);
            builder.CloseComponent();
        });
        LibraryBrowserBridge bridge = ctx.Services.GetRequiredService<LibraryBrowserBridge>();
        cut.WaitForAssertion(() => Assert.Equal("a.md", bridge.OpenPath?.RelativePath));

        DocumentHost host = cut.FindComponent<DocumentHost>().Instance;
        await cut.InvokeAsync(() => host.ShowAsync("b.md"));

        cut.WaitForAssertion(() => Assert.Equal("b.md", bridge.OpenPath?.RelativePath));
    }

    /// <summary>A rename made in the Sidebar's tree reaches the page: the open file follows it to its new name.</summary>
    [Fact]
    public async Task DocumentOnly_RenameFromTheTree_FollowsTheFile()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "a.md"), "# A");
        LibraryPath a = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderPart(
            ctx, LibraryExplorerPart.DocumentOnly, [new LibraryLocation(a.Root.Id, string.Empty)], initialFile: "a.md", initialRoot: a.Root.Id, showScopeRoot: false);
        LibraryBrowserBridge bridge = ctx.Services.GetRequiredService<LibraryBrowserBridge>();
        cut.WaitForAssertion(() => Assert.Equal("a.md", bridge.OpenPath?.RelativePath));

        File.Move(a.FullPath, Path.Combine(root, "renamed.md"));
        LibraryPath renamed = this.fixture.LibraryFixture.Resolve(root, "renamed.md");
        await cut.InvokeAsync(() => bridge.PublishChangeAsync(new LibraryChange(a, renamed, [])));

        cut.WaitForAssertion(() => Assert.Equal("renamed.md", bridge.OpenPath?.RelativePath));
    }

    /// <summary>When the page goes away the bridge forgets its file and its guards, so the Sidebar's tree stops highlighting a file nothing shows.</summary>
    [Fact]
    public async Task DocumentOnly_Disposed_ForgetsTheOpenFileAndTheGuards()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "a.md"), "# A");
        LibraryPath a = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderPart(
            ctx, LibraryExplorerPart.DocumentOnly, [new LibraryLocation(a.Root.Id, string.Empty)], initialFile: "a.md", initialRoot: a.Root.Id, showScopeRoot: false);
        LibraryBrowserBridge bridge = ctx.Services.GetRequiredService<LibraryBrowserBridge>();
        cut.WaitForAssertion(() => Assert.NotNull(bridge.OpenPath));

        await ctx.DisposeComponentsAsync();

        Assert.Null(bridge.OpenPath);
        Assert.Null(bridge.TryLeave);
        Assert.Null(bridge.BeforeChange);
    }

    /// <summary>The leave guard the tree asks before it opens another file is the document's, and a refusal is passed on.</summary>
    [Fact]
    public async Task Bridge_TryLeave_PassesOnTheDocumentsAnswer()
    {
        LibraryBrowserBridge bridge = new();
        Assert.True(await bridge.TryLeaveAsync());

        bridge.TryLeave = () => Task.FromResult(false);

        Assert.False(await bridge.TryLeaveAsync());
    }

    /// <summary>A rename the tree half makes reaches the document half, and the guard asks the document whether the file may change.</summary>
    [Fact]
    public async Task Bridge_ForwardsChangesAndAsksTheGuard()
    {
        LibraryBrowserBridge bridge = new();
        LibraryChange? seen = null;
        bridge.ChangeObserver = change =>
        {
            seen = change;
            return Task.CompletedTask;
        };
        bridge.BeforeChange = _ => Task.FromResult(false);

        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        LibraryPath item = this.fixture.LibraryFixture.Resolve(root, "a.md");
        LibraryChange sent = new(item, null, []);
        await bridge.PublishChangeAsync(sent);

        Assert.Same(sent, seen);
        Assert.False(await bridge.CanChangeAsync(item));
    }

    private MudBunitContext NewContext()
    {
        MudBunitContext ctx = this.fixture.NewContext();
        ctx.Services.AddScoped<LibraryBrowserBridge>();
        return ctx;
    }

    private static IRenderedComponent<ContainerFragment> RenderPart(
        MudBunitContext ctx,
        LibraryExplorerPart part,
        IReadOnlyList<LibraryLocation>? scopes,
        string? initialFile = null,
        string? initialRoot = null,
        bool showScopeRoot = true) => ctx.RenderWithPopovers(builder =>
    {
        builder.OpenComponent<LibraryExplorer>(0);
        builder.AddAttribute(1, nameof(LibraryExplorer.Part), part);
        builder.AddAttribute(2, nameof(LibraryExplorer.Scopes), scopes);
        builder.AddAttribute(3, nameof(LibraryExplorer.InitialFile), initialFile);
        builder.AddAttribute(4, nameof(LibraryExplorer.InitialRoot), initialRoot);
        builder.AddAttribute(5, nameof(LibraryExplorer.ShowScopeRoot), showScopeRoot);
        builder.AddAttribute(6, nameof(LibraryExplorer.StateKey), "test");
        builder.CloseComponent();
    });

    /// <summary>Stands in for the Library page: holds the file the Tab's address names, so a test can change it after the explorer mounted.</summary>
    private sealed class DocumentHost : ComponentBase
    {
        private string file = "a.md";

        /// <summary>The Root the file lies in.</summary>
        [Parameter]
        public string? RootId { get; set; }

        /// <summary>Changes the file the address names and re-renders.</summary>
        /// <param name="relative">The file's path under the Root.</param>
        public Task ShowAsync(string relative)
        {
            this.file = relative;
            return this.InvokeAsync(this.StateHasChanged);
        }

        /// <inheritdoc/>
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenComponent<LibraryExplorer>(0);
            builder.AddAttribute(1, nameof(LibraryExplorer.Part), LibraryExplorerPart.DocumentOnly);
            builder.AddAttribute(2, nameof(LibraryExplorer.Scopes), new[] { new LibraryLocation(this.RootId ?? string.Empty, string.Empty) });
            builder.AddAttribute(3, nameof(LibraryExplorer.InitialFile), this.file);
            builder.AddAttribute(4, nameof(LibraryExplorer.InitialRoot), this.RootId);
            builder.AddAttribute(5, nameof(LibraryExplorer.ShowScopeRoot), false);
            builder.AddAttribute(6, nameof(LibraryExplorer.StateKey), "test");
            builder.CloseComponent();
        }
    }
}
