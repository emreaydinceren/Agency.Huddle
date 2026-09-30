using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Components.Library;
using Agency.Huddle.App.Library;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>
/// Pins the Copy button (Spec §6.14) and freshness on refresh (Spec §6.8; ADR-0029;
/// corrections-B6 items 11, 23 and 28; judgements 44 and 46), over a REAL
/// <see cref="LibraryFileService"/> on a temp tree (corrections-B6 item 28: no counting fake, the
/// service is sealed and internal).
/// </summary>
public sealed class LibraryCopyAndFreshnessTests : IDisposable
{
    private readonly CopyFixture fixture = CopyFixture.Build();

    /// <summary>Disposes the underlying temp <c>DataDir</c> and stores.</summary>
    public void Dispose() => this.fixture.Dispose();

    /// <summary>(Spec §6.14) The document header's Copy button invokes <c>huddleClipboard.copy</c> with the
    /// file's absolute path and shows <c>Path copied</c> on success.</summary>
    [Fact]
    public async Task Copy_WritesAbsolutePath_ShowsSnackbar()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "a.md"), "# Hello");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        ctx.JSInterop.Setup<bool>("huddleClipboard.copy", path.FullPath).SetResult(true);
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-document-copy")));

        await cut.InvokeAsync(() => cut.Find(".library-document-copy").ClickAsync());

        JSRuntimeInvocation invocation = ctx.JSInterop.VerifyInvoke("huddleClipboard.copy");
        Assert.Equal(path.FullPath, Assert.Single(invocation.Arguments));
        cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));
        Assert.Equal("Path copied", snackbar.ShownSnackbars.Single().Message);
    }

    /// <summary>(Spec §6.14) A failed clipboard write (the JS call returns <see langword="false"/>) shows the
    /// exact failure wording instead.</summary>
    [Fact]
    public async Task Copy_Fails_ShowsFailure()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "a.md"), "# Hello");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        ctx.JSInterop.Setup<bool>("huddleClipboard.copy", path.FullPath).SetResult(false);
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-document-copy")));

        await cut.InvokeAsync(() => cut.Find(".library-document-copy").ClickAsync());

        cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));
        Assert.Equal("Couldn't copy the path.", snackbar.ShownSnackbars.Single().Message);
    }

    /// <summary>(corrections-B6 item 28) The tree's <c>CopyPath</c> action, driven through
    /// <see cref="LibraryFileOps.HandleAsync"/>, calls the same helper with the same texts.</summary>
    [Fact]
    public async Task Copy_TreeCopyPathAction_CallsHuddleClipboardCopy_ShowsSnackbar()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "a.md"), "# Hello");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        ctx.JSInterop.Setup<bool>("huddleClipboard.copy", path.FullPath).SetResult(true);
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        IRenderedComponent<ContainerFragment> cut = RenderOps(ctx);
        LibraryFileOps ops = cut.FindComponent<LibraryFileOps>().Instance;

        await cut.InvokeAsync(() => ops.HandleAsync(new LibraryTreeAction(LibraryTreeActionKind.CopyPath, path, null)));

        JSRuntimeInvocation invocation = ctx.JSInterop.VerifyInvoke("huddleClipboard.copy");
        Assert.Equal(path.FullPath, Assert.Single(invocation.Arguments));
        cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));
        Assert.Equal("Path copied", snackbar.ShownSnackbars.Single().Message);
    }

    /// <summary>(corrections-B6 item 28) The tree's <c>CopyPath</c> action shows the same failure wording
    /// when the clipboard write fails.</summary>
    [Fact]
    public async Task Copy_TreeCopyPathAction_Fails_ShowsFailure()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "a.md"), "# Hello");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        ctx.JSInterop.Setup<bool>("huddleClipboard.copy", path.FullPath).SetResult(false);
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        IRenderedComponent<ContainerFragment> cut = RenderOps(ctx);
        LibraryFileOps ops = cut.FindComponent<LibraryFileOps>().Instance;

        await cut.InvokeAsync(() => ops.HandleAsync(new LibraryTreeAction(LibraryTreeActionKind.CopyPath, path, null)));

        cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));
        Assert.Equal("Couldn't copy the path.", snackbar.ShownSnackbars.Single().Message);
    }

    /// <summary>(Spec §6.8; ADR-0029) A clean document whose file changed on disk since it was loaded is
    /// re-read on <c>RefreshAsync()</c>.</summary>
    [Fact]
    public async Task Refresh_CleanDocumentChangedOnDisk_Reloads()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        string filePath = Path.Combine(root, "a.md");
        File.WriteAllText(filePath, "# Hello");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-rendered")));

        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddMinutes(5));
        File.WriteAllText(filePath, "# Updated on disk");

        LibraryDocument document = cut.FindComponent<LibraryDocument>().Instance;
        await cut.InvokeAsync(() => document.RefreshAsync());

        cut.WaitForAssertion(() => Assert.Equal("Updated on disk", cut.Find(".library-rendered h1").TextContent.Trim()));
    }

    /// <summary>(Spec §6.8; ADR-0029) A document with unsaved edits is never reloaded, even when the file
    /// changed on disk: the editor keeps showing the human's own text.</summary>
    [Fact]
    public async Task Refresh_DirtyDocument_NeverReloads()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        string filePath = Path.Combine(root, "a.md");
        File.WriteAllText(filePath, "hi");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupEditorModule(ctx, "hi");
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);
        await SwitchModeAsync(cut, LibraryMode.Edit);

        LibraryEditor editor = cut.FindComponent<LibraryEditor>().Instance;
        await cut.InvokeAsync(() => editor.OnDirtyChanged(true));

        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddMinutes(5));
        File.WriteAllText(filePath, "someone else's content");

        LibraryDocument document = cut.FindComponent<LibraryDocument>().Instance;
        await cut.InvokeAsync(() => document.RefreshAsync());

        editor = cut.FindComponent<LibraryEditor>().Instance;
        Assert.Equal("hi", editor.Text);
    }

    /// <summary>(corrections-B6 item 28) An unchanged file - rewritten at the SAME length with its
    /// <c>LastWriteUtc</c> reset back to the value at load - is not re-read: the document keeps showing the
    /// OLD text.</summary>
    [Fact]
    public async Task Refresh_UnchangedFile_DoesNotReread()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        string filePath = Path.Combine(root, "a.md");
        File.WriteAllText(filePath, "hi");
        DateTime loadedWriteUtc = File.GetLastWriteTimeUtc(filePath);
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-rendered")));

        // Same length ("hi" -> "yo"), then reset LastWriteUtc back to the value at load.
        File.WriteAllText(filePath, "yo");
        File.SetLastWriteTimeUtc(filePath, loadedWriteUtc);

        LibraryDocument document = cut.FindComponent<LibraryDocument>().Instance;
        await cut.InvokeAsync(() => document.RefreshAsync());

        Assert.Equal("hi", cut.Find(".library-rendered").TextContent.Trim());
    }

    /// <summary>(corrections-B6 item 11, judgement 44) A dirty document whose file changed on disk shows the
    /// <c>MudAlert</c> notice with the exact settled text and never reloads on its own.</summary>
    [Fact]
    public async Task Refresh_DirtyChangedOnDisk_ShowsNotice()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        string filePath = Path.Combine(root, "a.md");
        File.WriteAllText(filePath, "hi");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupEditorModule(ctx, "hi");
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);
        await SwitchModeAsync(cut, LibraryMode.Edit);

        LibraryEditor editor = cut.FindComponent<LibraryEditor>().Instance;
        await cut.InvokeAsync(() => editor.OnDirtyChanged(true));

        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddMinutes(5));
        File.WriteAllText(filePath, "someone else's content");

        LibraryDocument document = cut.FindComponent<LibraryDocument>().Instance;
        await cut.InvokeAsync(() => document.RefreshAsync());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-document-freshness-notice")));
        Assert.Equal(
            "This file changed on disk since you opened it.",
            cut.Find(".library-document-freshness-notice").TextContent.Trim());
        Assert.Equal(
            ["Reload", "Keep mine"],
            cut.FindAll(".library-document-freshness-notice button").Select(b => b.TextContent.Trim()).ToList());
    }

    /// <summary>(corrections-B6 item 11) Reload, from the freshness notice, re-reads the file and clears the
    /// dirty marker.</summary>
    [Fact]
    public async Task Refresh_DirtyChangedOnDisk_Reload_RereadsAndClearsDirty()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        string filePath = Path.Combine(root, "a.md");
        File.WriteAllText(filePath, "hi");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupEditorModule(ctx, "hi");
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);
        await SwitchModeAsync(cut, LibraryMode.Edit);

        LibraryEditor editor = cut.FindComponent<LibraryEditor>().Instance;
        await cut.InvokeAsync(() => editor.OnDirtyChanged(true));

        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddMinutes(5));
        File.WriteAllText(filePath, "someone else's content");

        LibraryDocument document = cut.FindComponent<LibraryDocument>().Instance;
        await cut.InvokeAsync(() => document.RefreshAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-document-freshness-notice")));

        await cut.InvokeAsync(() => cut.FindAll(".library-document-freshness-notice button")
            .Single(b => string.Equals(b.TextContent.Trim(), "Reload", StringComparison.Ordinal)).ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".library-document-freshness-notice")));
        Assert.Equal("someone else's content", cut.FindComponent<LibraryEditor>().Instance.Text);
        Assert.False(cut.Find(".library-document-path").TextContent.Trim().EndsWith('●'));
    }

    /// <summary>(corrections-B6 item 11) Keep mine, from the freshness notice, hides the notice and keeps the
    /// human's unsaved edits untouched.</summary>
    [Fact]
    public async Task Refresh_DirtyChangedOnDisk_KeepMine_HidesNoticeAndKeepsEdits()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        string filePath = Path.Combine(root, "a.md");
        File.WriteAllText(filePath, "hi");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupEditorModule(ctx, "hi");
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);
        await SwitchModeAsync(cut, LibraryMode.Edit);

        LibraryEditor editor = cut.FindComponent<LibraryEditor>().Instance;
        await cut.InvokeAsync(() => editor.OnDirtyChanged(true));

        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddMinutes(5));
        File.WriteAllText(filePath, "someone else's content");

        LibraryDocument document = cut.FindComponent<LibraryDocument>().Instance;
        await cut.InvokeAsync(() => document.RefreshAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-document-freshness-notice")));

        await cut.InvokeAsync(() => cut.FindAll(".library-document-freshness-notice button")
            .Single(b => string.Equals(b.TextContent.Trim(), "Keep mine", StringComparison.Ordinal)).ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".library-document-freshness-notice")));
        Assert.Equal("hi", cut.FindComponent<LibraryEditor>().Instance.Text);
        Assert.Equal("a.md ●", cut.Find(".library-document-path").TextContent.Trim());
    }

    /// <summary>(corrections-B6 item 23, judgement 46) A refresh that finds the open file changed on disk
    /// invalidates that root's <see cref="WikiLinkIndex"/>: a backlinks query made after the refresh reflects
    /// content written directly to disk (never through Huddle) since the index was first built.</summary>
    [Fact]
    public async Task Refresh_ChangedFile_InvalidatesIndex()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        string aPath = Path.Combine(root, "a.md");
        File.WriteAllText(aPath, "# A");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-rendered")));

        // Written directly to disk (never through Huddle), so the already-built index does not know it yet.
        File.WriteAllText(Path.Combine(root, "b.md"), "See [[a]]");
        File.SetLastWriteTimeUtc(aPath, DateTime.UtcNow.AddMinutes(5));
        File.WriteAllText(aPath, "# A changed");

        LibraryDocument document = cut.FindComponent<LibraryDocument>().Instance;
        await cut.InvokeAsync(() => document.RefreshAsync());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".backlinks-panel-row")));
    }

    /// <summary>(corrections-B6 item 21) A refresh on a file that no longer exists shows the "moved or
    /// deleted" state.</summary>
    [Fact]
    public async Task Refresh_FileNowMissing_ShowsMovedOrDeleted()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        string filePath = Path.Combine(root, "a.md");
        File.WriteAllText(filePath, "hi");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-rendered")));

        File.Delete(filePath);

        LibraryDocument document = cut.FindComponent<LibraryDocument>().Instance;
        await cut.InvokeAsync(() => document.RefreshAsync());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-document-banner")));
        Assert.Equal("This file was moved or deleted.", cut.Find(".library-document-banner").TextContent.Trim());
    }

    /// <summary>(corrections-B6 item 28) The <c>[JSInvokable] OnVisible</c> callback triggers the same
    /// refresh as <see cref="LibraryDocument.RefreshAsync"/>.</summary>
    [Fact]
    public async Task OnVisible_TriggersRefresh()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        string filePath = Path.Combine(root, "a.md");
        File.WriteAllText(filePath, "# Hello");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-rendered")));

        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddMinutes(5));
        File.WriteAllText(filePath, "# Updated on disk");

        LibraryDocument document = cut.FindComponent<LibraryDocument>().Instance;
        await cut.InvokeAsync(() => document.OnVisible());

        cut.WaitForAssertion(() => Assert.Equal("Updated on disk", cut.Find(".library-rendered h1").TextContent.Trim()));
    }

    /// <summary>Switches the document's Read/Edit/Split mode through the toggle group's <c>ValueChanged</c>,
    /// as <see cref="LibraryDocumentEditTests"/> does for its own toggle groups.</summary>
    private static async Task SwitchModeAsync(IRenderedComponent<ContainerFragment> cut, LibraryMode mode)
    {
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<MudToggleGroup<LibraryMode>>()));
        MudToggleGroup<LibraryMode> toggle = cut.FindComponent<MudToggleGroup<LibraryMode>>().Instance;
        await cut.InvokeAsync(() => toggle.ValueChanged.InvokeAsync(mode));
    }

    /// <summary>Stubs the <c>library-editor.js</c> module and its <c>create</c> handle, with <c>getText</c>
    /// defaulting to <paramref name="text"/> (as <see cref="LibraryDocumentEditTests"/> does).</summary>
    private static (BunitJSModuleInterop Module, BunitJSModuleInterop Handle) SetupEditorModule(MudBunitContext ctx, string text)
    {
        BunitJSModuleInterop module = ctx.JSInterop.SetupModule("./library-editor.js");
        BunitJSModuleInterop handle = module.SetupModule("create", _ => true);
        handle.Setup<string>("getText", _ => true).SetResult(text);
        handle.SetupVoid("setText", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        return (module, handle);
    }

    private static IRenderedComponent<ContainerFragment> RenderDoc(
        MudBunitContext ctx,
        LibraryPath path,
        LibraryPath? scope = null) => ctx.RenderWithPopovers(builder =>
    {
        builder.OpenComponent<LibraryDocument>(0);
        builder.AddAttribute(1, nameof(LibraryDocument.Path), path);
        builder.AddAttribute(2, nameof(LibraryDocument.Scope), scope);
        builder.CloseComponent();
    });

    private static IRenderedComponent<ContainerFragment> RenderOps(MudBunitContext ctx) => ctx.RenderWithPopovers(builder =>
    {
        builder.OpenComponent<LibraryFileOps>(0);
        builder.CloseComponent();
    });

    /// <summary>An isolated fixture pairing a real <see cref="LibraryFileService"/> stack with a
    /// <see cref="FakeLibraryNoteResolver"/> and <see cref="FakeTaskReferenceResolver"/> registered for DI, as
    /// <see cref="LibraryDocumentEditTests"/>'s own fixture does.</summary>
    private sealed class CopyFixture : IDisposable
    {
        private CopyFixture(LibraryFileServiceFixture libraryFixture) => this.LibraryFixture = libraryFixture;

        /// <summary>The real Library stack (resolver, root store, file service) over this fixture's temp
        /// <c>DataDir</c>.</summary>
        public LibraryFileServiceFixture LibraryFixture { get; }

        /// <summary>Builds a fixture with the standard Teams/Teammates layout.</summary>
        public static CopyFixture Build() => new(LibraryFileServiceFixture.Build());

        /// <summary>A <see cref="MudBunitContext"/> with this fixture's real <see cref="LibraryFileService"/>,
        /// resolver, root store, and a permissive <see cref="FakeLibraryNoteResolver"/>/
        /// <see cref="FakeTaskReferenceResolver"/> pair.</summary>
        public MudBunitContext NewContext()
        {
            MudBunitContext ctx = new();
            ctx.Services.AddSingleton(this.LibraryFixture.Resolver);
            ctx.Services.AddSingleton(this.LibraryFixture.RootStore);
            ctx.Services.AddSingleton(this.LibraryFixture.CreateService());
            ctx.Services.AddSingleton(this.LibraryFixture.Index);
            ctx.Services.AddSingleton<ILibraryNoteResolver>(new FakeLibraryNoteResolver());
            ctx.Services.AddSingleton<Agency.Huddle.App.Tasks.ITaskReferenceResolver>(new FakeTaskReferenceResolver());
            return ctx;
        }

        /// <summary>Disposes the underlying stores and temp <c>DataDir</c>.</summary>
        public void Dispose() => this.LibraryFixture.Dispose();
    }
}
