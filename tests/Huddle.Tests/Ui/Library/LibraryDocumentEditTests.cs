using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Components.Library;
using Agency.Huddle.App.Library;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>
/// Pins <c>LibraryDocument</c>'s Edit and Split modes, the dirty marker, Ctrl+S save, the two
/// unsaved guards (<c>MudExitPrompt</c> and the switch-while-dirty confirm) and the save-time
/// freshness checks (Spec §6.7, §6.12; ADR-0029; corrections-B6 items 11, 15, 21; judgements 31,
/// 44), over a REAL <see cref="LibraryFileService"/> on a temp tree. The JS interop is mocked as
/// in <see cref="LibraryEditorTests"/>: <c>create</c>/<c>getText</c>/<c>setText</c>/<c>dispose</c>
/// are stubbed through <see cref="BunitJSModuleInterop"/>, and the Split preview arrives via the
/// <c>[JSInvokable] OnPreviewText</c> callback invoked directly - there is no .NET timer.
/// </summary>
public sealed class LibraryDocumentEditTests : IDisposable
{
    private const string ModulePath = "./library-editor.js";

    private readonly DocFixture fixture = DocFixture.Build();

    /// <summary>Disposes the underlying temp <c>DataDir</c> and stores.</summary>
    public void Dispose() => this.fixture.Dispose();

    /// <summary>Selecting Edit shows an editable <see cref="LibraryEditor"/> for the loaded text (Spec §6.7).</summary>
    [Fact]
    public async Task EditMode_CreatesEditableEditor()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "a.md"), "# Hello");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupEditorModule(ctx, "# Hello");
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);

        await SwitchModeAsync(cut, LibraryMode.Edit);

        LibraryEditor editor = cut.FindComponent<LibraryEditor>().Instance;
        Assert.False(editor.ReadOnly);
        Assert.Equal("# Hello", editor.Text);
    }

    /// <summary>Selecting Split shows both the editor and the rendered preview (Spec §6.7).</summary>
    [Fact]
    public async Task SplitMode_ShowsEditorAndPreview()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "a.md"), "# Hello");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupEditorModule(ctx, "# Hello");
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);

        await SwitchModeAsync(cut, LibraryMode.Split);

        Assert.NotEmpty(cut.FindComponents<LibraryEditor>());
        Assert.NotEmpty(cut.FindAll(".library-rendered"));
    }

    /// <summary>The document title shows a trailing dot while there are unsaved edits (Spec §6.7's <c>●</c>).</summary>
    [Fact]
    public async Task Dirty_ShowsDotInTitle()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "plan.md"), "hi");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "plan.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupEditorModule(ctx, "hi");
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);
        await SwitchModeAsync(cut, LibraryMode.Edit);

        Assert.Equal("plan.md", cut.Find(".library-document-path").TextContent.Trim());

        LibraryEditor editor = cut.FindComponent<LibraryEditor>().Instance;
        await cut.InvokeAsync(() => editor.OnDirtyChanged(true));

        Assert.Equal("plan.md ●", cut.Find(".library-document-path").TextContent.Trim());
    }

    /// <summary>Ctrl+S (the editor's <c>SaveRequested</c> callback) writes the edited text through <see cref="LibraryFileService.WriteTextAsync"/> and clears the dirty marker; the bytes on disk equal the expected re-encoded text.</summary>
    [Fact]
    public async Task SaveRequested_WritesThroughService_ClearsDirty()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        string filePath = Path.Combine(root, "a.md");
        File.WriteAllText(filePath, "hi");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        (_, BunitJSModuleInterop handle) = SetupEditorModule(ctx, "hi");
        handle.Setup<string>("getText", _ => true).SetResult("edited text");
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);
        await SwitchModeAsync(cut, LibraryMode.Edit);

        LibraryEditor editor = cut.FindComponent<LibraryEditor>().Instance;
        await cut.InvokeAsync(() => editor.OnDirtyChanged(true));
        Assert.Equal("a.md ●", cut.Find(".library-document-path").TextContent.Trim());

        await cut.InvokeAsync(() => editor.OnSaveRequested());

        // "hi" was written with no BOM, no line endings and no mixed endings, so the re-encoded
        // bytes for plain ASCII text are exactly its UTF-8 bytes (Spec §6.4 WriteTextAsync).
        byte[] expected = System.Text.Encoding.UTF8.GetBytes("edited text");
        cut.WaitForAssertion(() => Assert.Equal(expected, File.ReadAllBytes(filePath)));
        cut.WaitForAssertion(() => Assert.Equal("a.md", cut.Find(".library-document-path").TextContent.Trim()));
    }

    /// <summary>Saving a Teammate definition asks a confirm first (Spec §6.12); Cancel writes nothing.</summary>
    [Fact]
    public async Task SaveDefinition_AsksConfirm_Cancel_DoesNotWrite()
    {
        this.fixture.LibraryFixture.CreateTeammate("ada", "Ada", "ada");
        LibraryPath path = this.fixture.LibraryFixture.ResolveTeammates("ada");
        string filePath = this.fixture.LibraryFixture.ResolveTeammates("ada").FullPath;
        byte[] before = File.ReadAllBytes(filePath);

        await using MudBunitContext ctx = this.fixture.NewContext();
        (_, BunitJSModuleInterop handle) = SetupEditorModule(ctx, File.ReadAllText(filePath));
        handle.Setup<string>("getText", _ => true).SetResult(File.ReadAllText(filePath) + "\nmore");
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);
        await SwitchModeAsync(cut, LibraryMode.Edit);

        LibraryEditor editor = cut.FindComponent<LibraryEditor>().Instance;
        await cut.InvokeAsync(() => editor.OnDirtyChanged(true));
        _ = cut.InvokeAsync(() => editor.OnSaveRequested());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));
        Assert.Equal(["Cancel", "Save"], cut.FindAll(".mud-dialog-actions button").Select(b => b.TextContent.Trim()).ToList());

        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button")
            .Single(b => string.Equals(b.TextContent.Trim(), "Cancel", StringComparison.Ordinal)).ClickAsync());

        Assert.Equal(before, File.ReadAllBytes(filePath));
    }

    /// <summary>Saving a Teammate definition asks a confirm first (Spec §6.12); Save writes through.</summary>
    [Fact]
    public async Task SaveDefinition_AsksConfirm_Confirm_Writes()
    {
        this.fixture.LibraryFixture.CreateTeammate("ada", "Ada", "ada");
        LibraryPath path = this.fixture.LibraryFixture.ResolveTeammates("ada");
        string filePath = path.FullPath;
        string original = File.ReadAllText(filePath);
        string edited = original + "\nmore";

        await using MudBunitContext ctx = this.fixture.NewContext();
        (_, BunitJSModuleInterop handle) = SetupEditorModule(ctx, original);
        handle.Setup<string>("getText", _ => true).SetResult(edited);
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);
        await SwitchModeAsync(cut, LibraryMode.Edit);

        LibraryEditor editor = cut.FindComponent<LibraryEditor>().Instance;
        await cut.InvokeAsync(() => editor.OnDirtyChanged(true));
        _ = cut.InvokeAsync(() => editor.OnSaveRequested());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));

        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button")
            .Single(b => string.Equals(b.TextContent.Trim(), "Save", StringComparison.Ordinal)).ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(edited, File.ReadAllText(filePath)));
    }

    /// <summary>Switching to a different document while dirty offers Save, Discard or Cancel (Spec §6.7); Cancel keeps the current document and writes nothing.</summary>
    [Fact]
    public async Task SwitchWhileDirty_AsksSaveDiscardCancel_Cancel_KeepsDocument()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        string filePath = Path.Combine(root, "a.md");
        File.WriteAllText(filePath, "hi");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");
        byte[] before = File.ReadAllBytes(filePath);

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupEditorModule(ctx, "hi");
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);
        await SwitchModeAsync(cut, LibraryMode.Edit);

        LibraryEditor editor = cut.FindComponent<LibraryEditor>().Instance;
        await cut.InvokeAsync(() => editor.OnDirtyChanged(true));

        LibraryDocument document = cut.FindComponent<LibraryDocument>().Instance;
        Task<bool> leaving = cut.InvokeAsync(() => document.TryLeaveAsync());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));
        Assert.Equal(
            ["Cancel", "Discard", "Save"],
            cut.FindAll(".mud-dialog-actions button").Select(b => b.TextContent.Trim()).ToList());

        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button")
            .Single(b => string.Equals(b.TextContent.Trim(), "Cancel", StringComparison.Ordinal)).ClickAsync());

        bool canLeave = await leaving;

        Assert.False(canLeave);
        Assert.Equal(before, File.ReadAllBytes(filePath));
    }

    /// <summary>Discard switches without writing.</summary>
    [Fact]
    public async Task SwitchWhileDirty_Discard_SwitchesWithoutWriting()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        string filePath = Path.Combine(root, "a.md");
        File.WriteAllText(filePath, "hi");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");
        byte[] before = File.ReadAllBytes(filePath);

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupEditorModule(ctx, "hi");
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);
        await SwitchModeAsync(cut, LibraryMode.Edit);

        LibraryEditor editor = cut.FindComponent<LibraryEditor>().Instance;
        await cut.InvokeAsync(() => editor.OnDirtyChanged(true));

        LibraryDocument document = cut.FindComponent<LibraryDocument>().Instance;
        Task<bool> leaving = cut.InvokeAsync(() => document.TryLeaveAsync());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));

        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button")
            .Single(b => string.Equals(b.TextContent.Trim(), "Discard", StringComparison.Ordinal)).ClickAsync());

        bool canLeave = await leaving;

        Assert.True(canLeave);
        Assert.Equal(before, File.ReadAllBytes(filePath));
    }

    /// <summary>Save writes, then allows the switch (an allowed neighbour: unsaved edits saved through, no prompt after).</summary>
    [Fact]
    public async Task SwitchWhileDirty_Save_WritesThenSwitches()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        string filePath = Path.Combine(root, "a.md");
        File.WriteAllText(filePath, "hi");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        (_, BunitJSModuleInterop handle) = SetupEditorModule(ctx, "hi");
        handle.Setup<string>("getText", _ => true).SetResult("edited");
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);
        await SwitchModeAsync(cut, LibraryMode.Edit);

        LibraryEditor editor = cut.FindComponent<LibraryEditor>().Instance;
        await cut.InvokeAsync(() => editor.OnDirtyChanged(true));

        LibraryDocument document = cut.FindComponent<LibraryDocument>().Instance;
        Task<bool> leaving = cut.InvokeAsync(() => document.TryLeaveAsync());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));

        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button")
            .Single(b => string.Equals(b.TextContent.Trim(), "Save", StringComparison.Ordinal)).ClickAsync());

        bool canLeave = await leaving;

        Assert.True(canLeave);
        Assert.Equal("edited", File.ReadAllText(filePath));
    }

    /// <summary><see cref="MudExitPrompt"/> is disabled while the document is clean and enabled once it is dirty (Spec §6.7).</summary>
    [Fact]
    public async Task ExitPrompt_DisabledWhenClean_EnabledWhenDirty()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "a.md"), "hi");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupEditorModule(ctx, "hi");
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);
        await SwitchModeAsync(cut, LibraryMode.Edit);

        Assert.True(cut.FindComponent<MudExitPrompt>().Instance.Disabled);

        LibraryEditor editor = cut.FindComponent<LibraryEditor>().Instance;
        await cut.InvokeAsync(() => editor.OnDirtyChanged(true));

        Assert.False(cut.FindComponent<MudExitPrompt>().Instance.Disabled);
    }

    /// <summary>A view-only document (Spec §6.11) shows no Edit toggle: nothing to switch to.</summary>
    [Fact]
    public async Task ViewOnly_NoEditToggle()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "a.json"), "{}");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.json");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<LibraryEditor>()));
        Assert.Empty(cut.FindComponents<MudToggleGroup<LibraryMode>>());
    }

    /// <summary>(corrections-B6 item 11, judgement 44) Saving after the file changed on disk since it was opened asks <c>Overwrite changes on disk?</c>; Cancel leaves the OTHER writer's content; Overwrite replaces it with the editor's.</summary>
    [Fact]
    public async Task Save_AfterFileChangedOnDisk_AsksOverwrite()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        string filePath = Path.Combine(root, "a.md");
        File.WriteAllText(filePath, "hi");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        (_, BunitJSModuleInterop handle) = SetupEditorModule(ctx, "hi");
        handle.Setup<string>("getText", _ => true).SetResult("mine");
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);
        await SwitchModeAsync(cut, LibraryMode.Edit);

        LibraryEditor editor = cut.FindComponent<LibraryEditor>().Instance;
        await cut.InvokeAsync(() => editor.OnDirtyChanged(true));

        // The other writer: bump both Length and LastWriteUtc so the freshness check trips.
        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddMinutes(5));
        File.WriteAllText(filePath, "someone else's content");

        _ = cut.InvokeAsync(() => editor.OnSaveRequested());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));
        Assert.Equal("Overwrite changes on disk?", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal(
            "This file changed since you opened it. Saving replaces those changes.",
            cut.Find(".mud-dialog-content").TextContent.Trim());
        Assert.Equal(["Cancel", "Overwrite"], cut.FindAll(".mud-dialog-actions button").Select(b => b.TextContent.Trim()).ToList());

        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button")
            .Single(b => string.Equals(b.TextContent.Trim(), "Cancel", StringComparison.Ordinal)).ClickAsync());

        Assert.Equal("someone else's content", File.ReadAllText(filePath));

        _ = cut.InvokeAsync(() => editor.OnSaveRequested());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));

        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button")
            .Single(b => string.Equals(b.TextContent.Trim(), "Overwrite", StringComparison.Ordinal)).ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal("mine", File.ReadAllText(filePath)));
    }

    /// <summary>(corrections-B6 item 21) Saving after the loaded file was moved or deleted refuses without recreating it: no confirm, nothing written, the settled reason shown.</summary>
    [Fact]
    public async Task Save_AfterFileDeleted_RefusesAndDoesNotRecreate()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        string filePath = Path.Combine(root, "a.md");
        File.WriteAllText(filePath, "hi");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        (_, BunitJSModuleInterop handle) = SetupEditorModule(ctx, "hi");
        handle.Setup<string>("getText", _ => true).SetResult("mine");
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);
        await SwitchModeAsync(cut, LibraryMode.Edit);

        LibraryEditor editor = cut.FindComponent<LibraryEditor>().Instance;
        await cut.InvokeAsync(() => editor.OnDirtyChanged(true));

        File.Delete(filePath);

        await cut.InvokeAsync(() => editor.OnSaveRequested());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-document-save-error")));
        Assert.Equal("This file was moved or deleted.", cut.Find(".library-document-save-error").TextContent.Trim());
        Assert.False(File.Exists(filePath));
        Assert.Empty(cut.FindAll(".mud-dialog-actions button"));
    }

    /// <summary>The allowed neighbour of the two freshness guards: an unchanged file saves straight through, with no confirm dialog.</summary>
    [Fact]
    public async Task Save_Unchanged_NoConfirm_Writes()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        string filePath = Path.Combine(root, "a.md");
        File.WriteAllText(filePath, "hi");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        (_, BunitJSModuleInterop handle) = SetupEditorModule(ctx, "hi");
        handle.Setup<string>("getText", _ => true).SetResult("mine");
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);
        await SwitchModeAsync(cut, LibraryMode.Edit);

        LibraryEditor editor = cut.FindComponent<LibraryEditor>().Instance;
        await cut.InvokeAsync(() => editor.OnDirtyChanged(true));

        await cut.InvokeAsync(() => editor.OnSaveRequested());

        cut.WaitForAssertion(() => Assert.Equal("mine", File.ReadAllText(filePath)));
        Assert.Empty(cut.FindAll(".mud-dialog-actions button"));
    }

    /// <summary>Invoking the Split editor's <c>[JSInvokable] OnPreviewText</c> directly (the JS side debounces at 300 ms; there is no .NET timer) updates <c>.library-rendered</c>.</summary>
    [Fact]
    public async Task SplitMode_OnPreviewText_UpdatesPreview()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "a.md"), "# Hello");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        SetupEditorModule(ctx, "# Hello");
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);
        await SwitchModeAsync(cut, LibraryMode.Split);

        LibraryEditor editor = cut.FindComponent<LibraryEditor>().Instance;
        await cut.InvokeAsync(() => editor.OnPreviewText("# Updated"));

        cut.WaitForAssertion(() => Assert.Equal("Updated", cut.Find(".library-rendered h1").TextContent.Trim()));
    }

    /// <summary>(Spec §6.12) Saving a Teammate definition whose frontmatter Name changed is refused by <see cref="LibraryFileService.WriteTextAsync"/>'s settled reason; the disk is unchanged.</summary>
    [Fact]
    public async Task SaveDefinition_NameChanged_ShowsRefusal()
    {
        this.fixture.LibraryFixture.CreateTeammate("ada", "Ada", "ada");
        LibraryPath path = this.fixture.LibraryFixture.ResolveTeammates("ada");
        string filePath = path.FullPath;
        string original = File.ReadAllText(filePath);
        string renamed = original.Replace("Name: Ada", "Name: Ada2", StringComparison.Ordinal);

        await using MudBunitContext ctx = this.fixture.NewContext();
        (_, BunitJSModuleInterop handle) = SetupEditorModule(ctx, original);
        handle.Setup<string>("getText", _ => true).SetResult(renamed);
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);
        await SwitchModeAsync(cut, LibraryMode.Edit);

        LibraryEditor editor = cut.FindComponent<LibraryEditor>().Instance;
        await cut.InvokeAsync(() => editor.OnDirtyChanged(true));
        _ = cut.InvokeAsync(() => editor.OnSaveRequested());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button")
            .Single(b => string.Equals(b.TextContent.Trim(), "Save", StringComparison.Ordinal)).ClickAsync());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-document-save-error")));
        Assert.Equal("Rename teammates on the Teammates page.", cut.Find(".library-document-save-error").TextContent.Trim());
        Assert.Equal(original, File.ReadAllText(filePath));
    }

    /// <summary>A clean document's <see cref="LibraryDocument.TryLeaveAsync"/> returns <see langword="true"/> without showing a dialog.</summary>
    [Fact]
    public async Task TryLeaveAsync_Clean_ReturnsTrueWithoutDialog()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "a.md"), "hi");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-rendered")));

        LibraryDocument document = cut.FindComponent<LibraryDocument>().Instance;
        bool canLeave = await cut.InvokeAsync(() => document.TryLeaveAsync());

        Assert.True(canLeave);
        Assert.Empty(cut.FindAll(".mud-dialog-actions button"));
    }

    /// <summary>Switches the document's Read/Edit/Split mode through the toggle group's <c>ValueChanged</c>, as <c>ViewEditorDrawerTests</c> does for its own toggle groups.</summary>
    private static async Task SwitchModeAsync(IRenderedComponent<ContainerFragment> cut, LibraryMode mode)
    {
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<MudToggleGroup<LibraryMode>>()));
        MudToggleGroup<LibraryMode> toggle = cut.FindComponent<MudToggleGroup<LibraryMode>>().Instance;
        await cut.InvokeAsync(() => toggle.ValueChanged.InvokeAsync(mode));
    }

    /// <summary>Stubs the <c>library-editor.js</c> module and its <c>create</c> handle (as <see cref="LibraryEditorTests"/> does), with <c>getText</c> defaulting to <paramref name="text"/>.</summary>
    private static (BunitJSModuleInterop Module, BunitJSModuleInterop Handle) SetupEditorModule(MudBunitContext ctx, string text)
    {
        BunitJSModuleInterop module = ctx.JSInterop.SetupModule(ModulePath);
        BunitJSModuleInterop handle = module.SetupModule("create", _ => true);
        handle.Setup<string>("getText", _ => true).SetResult(text);
        handle.SetupVoid("setText", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        return (module, handle);
    }

    private static IRenderedComponent<ContainerFragment> RenderDoc(
        MudBunitContext ctx,
        LibraryPath path,
        LibraryPath? scope = null,
        EventCallback<LibraryPath> onNavigate = default,
        EventCallback<LibraryPath> onOpenInLibrary = default,
        EventCallback<LibraryTreeAction> onAction = default) => ctx.RenderWithPopovers(builder =>
    {
        builder.OpenComponent<LibraryDocument>(0);
        builder.AddAttribute(1, nameof(LibraryDocument.Path), path);
        builder.AddAttribute(2, nameof(LibraryDocument.Scope), scope);
        builder.AddAttribute(3, nameof(LibraryDocument.OnNavigate), onNavigate);
        builder.AddAttribute(4, nameof(LibraryDocument.OnOpenInLibrary), onOpenInLibrary);
        builder.AddAttribute(5, nameof(LibraryDocument.OnAction), onAction);
        builder.CloseComponent();
    });

    /// <summary>An isolated fixture pairing a real <see cref="LibraryFileService"/> stack with a <see cref="FakeLibraryNoteResolver"/> and <see cref="FakeTaskReferenceResolver"/> registered for DI.</summary>
    private sealed class DocFixture : IDisposable
    {
        private DocFixture(LibraryFileServiceFixture libraryFixture) => this.LibraryFixture = libraryFixture;

        /// <summary>The real Library stack (resolver, root store, file service) over this fixture's temp <c>DataDir</c>.</summary>
        public LibraryFileServiceFixture LibraryFixture { get; }

        /// <summary>Builds a fixture with the standard Teams/Teammates layout.</summary>
        public static DocFixture Build() => new(LibraryFileServiceFixture.Build());

        /// <summary>A <see cref="MudBunitContext"/> with this fixture's real <see cref="LibraryFileService"/>, resolver, root store, and a permissive <see cref="FakeLibraryNoteResolver"/>/<see cref="FakeTaskReferenceResolver"/> pair.</summary>
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
