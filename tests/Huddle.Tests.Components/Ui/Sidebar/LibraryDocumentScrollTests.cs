using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using Agency.Huddle.App;
using Agency.Huddle.App.Components.Library;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.Tabs;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Ui.Sidebar;

/// <summary>Pins how the Library document answers the <see cref="ScrollToHeading"/> Page command (Spec D-6): Read scrolls the rendered heading, Edit reveals the line.</summary>
public sealed class LibraryDocumentScrollTests : IDisposable
{
    private const string TabId = "t1";
    private const string ModulePath = "./library-editor.js";
    private const string ScrollCall = "huddleLibrary.scrollToHeading";

    private readonly LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();

    /// <summary>Disposes the temp Library stack.</summary>
    public void Dispose() => this.library.Dispose();

    /// <summary>In Read mode the command scrolls the rendered heading: the root element and the id go to the JS helper.</summary>
    [Fact]
    public async Task Read_ScrollToHeading_CallsScrollToHeading()
    {
        LibraryPath path = this.NewDoc();
        await using MudBunitContext ctx = this.NewContext();
        ctx.JSInterop.Setup<bool>(LibraryDocumentScrollTests.ScrollCall, _ => true).SetResult(true);
        IRenderedComponent<ContainerFragment> cut = LibraryDocumentScrollTests.RenderDoc(ctx, path);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-document-toolbar")));

        await cut.InvokeAsync(() => ctx.Services.GetRequiredService<PageCommandBus>().SendAsync(LibraryDocumentScrollTests.TabId, new ScrollToHeading("doc-h-a", 3)));

        ctx.JSInterop.VerifyInvoke(LibraryDocumentScrollTests.ScrollCall);
        JSRuntimeInvocation call = Assert.Single(ctx.JSInterop.Invocations[LibraryDocumentScrollTests.ScrollCall]);
        Assert.Equal(2, call.Arguments.Count);
        Assert.Equal("doc-h-a", call.Arguments[1]);
    }

    /// <summary>In Edit mode the command reveals the heading's line in the editor, and does not scroll the preview.</summary>
    [Fact]
    public async Task Edit_ScrollToHeading_RevealsTheLine()
    {
        LibraryPath path = this.NewDoc();
        await using MudBunitContext ctx = this.NewContext();
        BunitJSModuleInterop module = ctx.JSInterop.SetupModule(LibraryDocumentScrollTests.ModulePath);
        BunitJSModuleInterop handle = module.SetupModule("create", _ => true);
        handle.Setup<string>("getText", _ => true).SetResult("# A");
        handle.SetupVoid("setText", _ => true).SetVoidResult();
        handle.SetupVoid("revealLine", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        ctx.JSInterop.Setup<bool>(LibraryDocumentScrollTests.ScrollCall, _ => true).SetResult(true);
        IRenderedComponent<ContainerFragment> cut = LibraryDocumentScrollTests.RenderDoc(ctx, path);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<MudToggleGroup<LibraryMode>>()));
        MudToggleGroup<LibraryMode> toggle = cut.FindComponent<MudToggleGroup<LibraryMode>>().Instance;
        await cut.InvokeAsync(() => toggle.ValueChanged.InvokeAsync(LibraryMode.Edit));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<LibraryEditor>()));
        cut.WaitForAssertion(() => Assert.NotEmpty(module.Invocations["create"]));

        await cut.InvokeAsync(() => ctx.Services.GetRequiredService<PageCommandBus>().SendAsync(LibraryDocumentScrollTests.TabId, new ScrollToHeading("doc-h-a", 3)));

        JSRuntimeInvocation call = Assert.Single(handle.Invocations["revealLine"]);
        Assert.Equal(3, call.Arguments[0]);
        Assert.DoesNotContain(ctx.JSInterop.Invocations, i => string.Equals(i.Identifier, LibraryDocumentScrollTests.ScrollCall, StringComparison.Ordinal));
    }

    /// <summary>A command sent to another Tab is ignored: nothing scrolls.</summary>
    [Fact]
    public async Task OtherTab_Command_IsIgnored()
    {
        LibraryPath path = this.NewDoc();
        await using MudBunitContext ctx = this.NewContext();
        ctx.JSInterop.Setup<bool>(LibraryDocumentScrollTests.ScrollCall, _ => true).SetResult(true);
        IRenderedComponent<ContainerFragment> cut = LibraryDocumentScrollTests.RenderDoc(ctx, path);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-document-toolbar")));

        await cut.InvokeAsync(() => ctx.Services.GetRequiredService<PageCommandBus>().SendAsync("t2", new ScrollToHeading("doc-h-a", 3)));

        Assert.DoesNotContain(ctx.JSInterop.Invocations, i => string.Equals(i.Identifier, LibraryDocumentScrollTests.ScrollCall, StringComparison.Ordinal));
    }

    /// <summary>The ScrollToHistory command scrolls the History into view in Read mode: the element id goes to the JS helper.</summary>
    [Fact]
    public async Task Read_ScrollToHistory_ScrollsTheHistory()
    {
        LibraryPath path = this.NewDoc();
        await using MudBunitContext ctx = this.NewContext();
        ctx.JSInterop.Setup<bool>(LibraryDocumentScrollTests.ScrollCall, _ => true).SetResult(true);
        IRenderedComponent<ContainerFragment> cut = LibraryDocumentScrollTests.RenderDoc(ctx, path);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-document-toolbar")));

        await cut.InvokeAsync(() => ctx.Services.GetRequiredService<PageCommandBus>().SendAsync(LibraryDocumentScrollTests.TabId, new ScrollToHistory()));

        JSRuntimeInvocation call = Assert.Single(ctx.JSInterop.Invocations[LibraryDocumentScrollTests.ScrollCall]);
        Assert.Equal("doc-history", call.Arguments[1]);
    }

    /// <summary>In Edit mode the History is not shown, so the ScrollToHistory command is ignored.</summary>
    [Fact]
    public async Task Edit_ScrollToHistory_IsIgnored()
    {
        LibraryPath path = this.NewDoc();
        await using MudBunitContext ctx = this.NewContext();
        BunitJSModuleInterop module = ctx.JSInterop.SetupModule(LibraryDocumentScrollTests.ModulePath);
        BunitJSModuleInterop handle = module.SetupModule("create", _ => true);
        handle.Setup<string>("getText", _ => true).SetResult("# A");
        handle.SetupVoid("setText", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        ctx.JSInterop.Setup<bool>(LibraryDocumentScrollTests.ScrollCall, _ => true).SetResult(true);
        IRenderedComponent<ContainerFragment> cut = LibraryDocumentScrollTests.RenderDoc(ctx, path);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<MudToggleGroup<LibraryMode>>()));
        MudToggleGroup<LibraryMode> toggle = cut.FindComponent<MudToggleGroup<LibraryMode>>().Instance;
        await cut.InvokeAsync(() => toggle.ValueChanged.InvokeAsync(LibraryMode.Edit));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<LibraryEditor>()));

        await cut.InvokeAsync(() => ctx.Services.GetRequiredService<PageCommandBus>().SendAsync(LibraryDocumentScrollTests.TabId, new ScrollToHistory()));

        Assert.DoesNotContain(ctx.JSInterop.Invocations, i => string.Equals(i.Identifier, LibraryDocumentScrollTests.ScrollCall, StringComparison.Ordinal));
        Assert.Empty(cut.FindAll(".doc-history"));
    }

    /// <summary>Clicking the byline's time scrolls the History into view.</summary>
    [Fact]
    public async Task Byline_Click_ScrollsTheHistory()
    {
        LibraryPath path = this.NewDoc();
        await using MudBunitContext ctx = this.NewContext();
        DocHistoryStore store = new(this.library.BoundOptions, NullLogger<DocHistoryStore>.Instance);
        store.Append(path, new DocHistoryEntry(DateTimeOffset.UtcNow, "Emre", DocActorKind.Human, DocChangeKind.Edited, string.Empty));
        ctx.Services.AddSingleton(store);
        ctx.Services.AddSingleton(new Agency.Huddle.App.Avatars.AvatarStore(this.library.BoundOptions, NullLogger<Agency.Huddle.App.Avatars.AvatarStore>.Instance));
        ctx.JSInterop.Setup<bool>(LibraryDocumentScrollTests.ScrollCall, _ => true).SetResult(true);
        IRenderedComponent<ContainerFragment> cut = LibraryDocumentScrollTests.RenderDoc(ctx, path);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".doc-byline-time")));

        await cut.Find(".doc-byline-time").ClickAsync(new());

        JSRuntimeInvocation call = Assert.Single(ctx.JSInterop.Invocations[LibraryDocumentScrollTests.ScrollCall]);
        Assert.Equal("doc-history", call.Arguments[1]);
    }

    /// <summary>With History switched off the document shows neither the byline nor the History, and ScrollToHistory does nothing.</summary>
    [Fact]
    public async Task HistoryDisabled_ShowsNeitherAndIgnoresScroll()
    {
        LibraryPath path = this.NewDoc();
        await using MudBunitContext ctx = this.NewContext();
        TeamOptions off = new() { DataDir = this.library.DataDir, DocHistory = { Enabled = false } };
        DocHistoryStore store = new(Microsoft.Extensions.Options.Options.Create(off), NullLogger<DocHistoryStore>.Instance);
        store.Append(path, new DocHistoryEntry(DateTimeOffset.UtcNow, "Mira", DocActorKind.Agent, DocChangeKind.Created, "Drafted"));
        ctx.Services.AddSingleton(store);
        ctx.JSInterop.Setup<bool>(LibraryDocumentScrollTests.ScrollCall, _ => true).SetResult(true);
        IRenderedComponent<ContainerFragment> cut = LibraryDocumentScrollTests.RenderDoc(ctx, path);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-document-toolbar")));

        await cut.InvokeAsync(() => ctx.Services.GetRequiredService<PageCommandBus>().SendAsync(LibraryDocumentScrollTests.TabId, new ScrollToHistory()));

        Assert.Empty(cut.FindAll(".doc-byline"));
        Assert.Empty(cut.FindAll(".doc-history"));
        Assert.DoesNotContain(ctx.JSInterop.Invocations, i => string.Equals(i.Identifier, LibraryDocumentScrollTests.ScrollCall, StringComparison.Ordinal));
    }

    private static IRenderedComponent<ContainerFragment> RenderDoc(MudBunitContext ctx, LibraryPath path) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<CascadingValue<TabPageContext>>(0);
            builder.AddAttribute(1, nameof(CascadingValue<TabPageContext>.Value), new TabPageContext("/library", LibraryDocumentScrollTests.TabId));
            builder.AddAttribute(2, nameof(CascadingValue<TabPageContext>.ChildContent), (RenderFragment)(content =>
            {
                content.OpenComponent<LibraryDocument>(0);
                content.AddAttribute(1, nameof(LibraryDocument.Path), path);
                content.CloseComponent();
            }));
            builder.CloseComponent();
        });

    private LibraryPath NewDoc()
    {
        string root = this.library.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "a.md"), "# A");
        return this.library.Resolve(root, "a.md");
    }

    private MudBunitContext NewContext()
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(this.library.Resolver);
        ctx.Services.AddSingleton(this.library.RootStore);
        ctx.Services.AddSingleton(this.library.CreateService());
        ctx.Services.AddSingleton(this.library.Index);
        ctx.Services.AddSingleton<ILibraryNoteResolver>(new FakeLibraryNoteResolver());
        ctx.Services.AddDocHistoryServices();
        ctx.Services.AddSingleton<Agency.Huddle.App.Tasks.ITaskReferenceResolver>(new FakeTaskReferenceResolver());
        return ctx;
    }
}
