using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Components.Library;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.Tabs;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Ui.Sidebar;

/// <summary>Pins that the Library document publishes a <see cref="DocumentSubject"/> for its own Tab, and that Split edits update its Text (Spec D-2, D-5).</summary>
public sealed class LibraryDocumentSubjectTests : IDisposable
{
    private const string TabId = "t1";
    private const string ModulePath = "./library-editor.js";

    private readonly LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();

    /// <summary>Disposes the temp Library stack.</summary>
    public void Dispose() => this.library.Dispose();

    /// <summary>A Markdown file publishes its path, Markdown kind and file text.</summary>
    [Fact]
    public async Task Document_Markdown_PublishesKindAndText()
    {
        string root = this.library.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "a.md"), "# Hello");
        LibraryPath path = this.library.Resolve(root, "a.md");
        await using MudBunitContext ctx = this.NewContext();

        IRenderedComponent<ContainerFragment> cut = LibraryDocumentSubjectTests.RenderDoc(ctx, path);

        cut.WaitForAssertion(() => Assert.Equal(new DocumentSubject(path, LibraryFileKind.Markdown, "# Hello"), LibraryDocumentSubjectTests.Subject(ctx)));
    }

    /// <summary>A Split-mode preview update (the editor's JS callback) re-publishes the new text, with the same Key.</summary>
    [Fact]
    public async Task Document_SplitPreviewText_UpdatesTheSubjectText()
    {
        string root = this.library.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "a.md"), "# Hello");
        LibraryPath path = this.library.Resolve(root, "a.md");
        await using MudBunitContext ctx = this.NewContext();
        BunitJSModuleInterop module = ctx.JSInterop.SetupModule(LibraryDocumentSubjectTests.ModulePath);
        BunitJSModuleInterop handle = module.SetupModule("create", _ => true);
        handle.Setup<string>("getText", _ => true).SetResult("# Hello");
        handle.SetupVoid("setText", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        IRenderedComponent<ContainerFragment> cut = LibraryDocumentSubjectTests.RenderDoc(ctx, path);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<MudToggleGroup<LibraryMode>>()));
        MudToggleGroup<LibraryMode> toggle = cut.FindComponent<MudToggleGroup<LibraryMode>>().Instance;
        await cut.InvokeAsync(() => toggle.ValueChanged.InvokeAsync(LibraryMode.Split));
        string keyBefore = LibraryDocumentSubjectTests.Subject(ctx)?.Key ?? string.Empty;
        LibraryEditor editor = cut.FindComponent<LibraryEditor>().Instance;

        await cut.InvokeAsync(() => editor.OnPreviewText("# Updated"));

        DocumentSubject subject = Assert.IsType<DocumentSubject>(LibraryDocumentSubjectTests.Subject(ctx));
        Assert.Equal("# Updated", subject.Text);
        Assert.Equal(keyBefore, subject.Key);
    }

    /// <summary>A file that was moved or deleted publishes nothing.</summary>
    [Fact]
    public async Task Document_Missing_PublishesNothing()
    {
        string root = this.library.CreatePinnedRoot("Notes");
        string filePath = Path.Combine(root, "a.md");
        File.WriteAllText(filePath, "hi");
        LibraryPath path = this.library.Resolve(root, "a.md");
        File.Delete(filePath);
        await using MudBunitContext ctx = this.NewContext();

        IRenderedComponent<ContainerFragment> cut = LibraryDocumentSubjectTests.RenderDoc(ctx, path);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-document-banner")));
        Assert.Null(LibraryDocumentSubjectTests.Subject(ctx));
    }

    /// <summary>An image publishes kind Image and no text.</summary>
    [Fact]
    public async Task Document_Image_PublishesKindImage()
    {
        Directory.CreateDirectory(Path.Combine(this.library.DataDir, "Teams", "Marketing"));
        File.WriteAllBytes(Path.Combine(this.library.DataDir, "Teams", "Marketing", "x.png"), [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0]);
        this.library.Reload();
        LibraryPath path = this.library.ResolveTeams("Marketing/x.png");
        await using MudBunitContext ctx = this.NewContext();

        IRenderedComponent<ContainerFragment> cut = LibraryDocumentSubjectTests.RenderDoc(ctx, path);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("img.library-document-image")));
        DocumentSubject subject = Assert.IsType<DocumentSubject>(LibraryDocumentSubjectTests.Subject(ctx));
        Assert.Equal(LibraryFileKind.Image, subject.Kind);
        Assert.Null(subject.Text);
    }

    private static PageSubject? Subject(MudBunitContext ctx) =>
        ctx.Services.GetRequiredService<RightSidebarService>().SubjectFor(LibraryDocumentSubjectTests.TabId);

    private static IRenderedComponent<ContainerFragment> RenderDoc(MudBunitContext ctx, LibraryPath path) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<CascadingValue<TabPageContext>>(0);
            builder.AddAttribute(1, nameof(CascadingValue<TabPageContext>.Value), new TabPageContext("/library", LibraryDocumentSubjectTests.TabId));
            builder.AddAttribute(2, nameof(CascadingValue<TabPageContext>.ChildContent), (RenderFragment)(content =>
            {
                content.OpenComponent<LibraryDocument>(0);
                content.AddAttribute(1, nameof(LibraryDocument.Path), path);
                content.CloseComponent();
            }));
            builder.CloseComponent();
        });

    private MudBunitContext NewContext()
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(this.library.Resolver);
        ctx.Services.AddSingleton(this.library.RootStore);
        ctx.Services.AddSingleton(this.library.CreateService());
        ctx.Services.AddSingleton(this.library.Index);
        ctx.Services.AddSingleton<ILibraryNoteResolver>(new FakeLibraryNoteResolver());
        ctx.Services.AddSingleton<Agency.Huddle.App.Tasks.ITaskReferenceResolver>(new FakeTaskReferenceResolver());
        return ctx;
    }
}
