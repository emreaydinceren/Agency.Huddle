using Bunit;
using Bunit.Rendering;
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
using Agency.Huddle.Tests.Ui.Library;

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
        IRenderedComponent<ContainerFragment> cut = LibraryDocumentTestSupport.RenderDoc(ctx, path, tabPage: new TabPageContext("/library", LibraryDocumentScrollTests.TabId));
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
        IRenderedComponent<ContainerFragment> cut = LibraryDocumentTestSupport.RenderDoc(ctx, path, tabPage: new TabPageContext("/library", LibraryDocumentScrollTests.TabId));
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
        IRenderedComponent<ContainerFragment> cut = LibraryDocumentTestSupport.RenderDoc(ctx, path, tabPage: new TabPageContext("/library", LibraryDocumentScrollTests.TabId));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-document-toolbar")));

        await cut.InvokeAsync(() => ctx.Services.GetRequiredService<PageCommandBus>().SendAsync("t2", new ScrollToHeading("doc-h-a", 3)));

        Assert.DoesNotContain(ctx.JSInterop.Invocations, i => string.Equals(i.Identifier, LibraryDocumentScrollTests.ScrollCall, StringComparison.Ordinal));
    }

    /// <summary>Clicking the byline's time opens the Right sidebar on its Subject panels, where the History is; the document itself carries no History under its text.</summary>
    [Fact]
    public async Task Byline_Click_OpensTheRightSidebar()
    {
        LibraryPath path = this.NewDoc();
        await using MudBunitContext ctx = this.NewContext();
        DocHistoryStore store = new(this.library.BoundOptions, NullLogger<DocHistoryStore>.Instance);
        store.Append(path, new DocHistoryEntry(DateTimeOffset.UtcNow, "Emre", DocActorKind.Human, DocChangeKind.Edited, string.Empty));
        ctx.Services.AddSingleton(store);
        ctx.Services.AddSingleton(new Agency.Huddle.App.Avatars.AvatarStore(this.library.BoundOptions, NullLogger<Agency.Huddle.App.Avatars.AvatarStore>.Instance));
        ctx.Services.AddPersonaStore();
        RightSidebarService sidebar = new();
        ctx.Services.AddSingleton(sidebar);
        ctx.JSInterop.Setup<bool>(LibraryDocumentScrollTests.ScrollCall, _ => true).SetResult(true);
        IRenderedComponent<ContainerFragment> cut = LibraryDocumentTestSupport.RenderDoc(ctx, path, tabPage: new TabPageContext("/library", LibraryDocumentScrollTests.TabId));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".doc-byline-time")));

        await cut.Find(".doc-byline-time").ClickAsync(new());

        Assert.True(sidebar.IsOpen);
        Assert.Equal(RightSidebarMode.Subject, sidebar.Mode);
        Assert.Empty(cut.FindAll(".doc-history"));
    }

    /// <summary>The note reads top to bottom as its tags, its first heading, then its byline; the byline's groups are separated by a "·" and no separator comes first.</summary>
    [Fact]
    public async Task Read_ShowsTagsThenHeadingThenByline_WithDotsBetweenGroups()
    {
        string root = this.library.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "n.md"), "---\ntags: [alpha]\n---\n# Title\n\nBody text #beta\n");
        LibraryPath path = this.library.Resolve(root, "n.md");
        await using MudBunitContext ctx = this.NewContext();
        this.SeedHistory(ctx, path);
        IRenderedComponent<ContainerFragment> cut = LibraryDocumentTestSupport.RenderDoc(ctx, path, tabPage: new TabPageContext("/library", LibraryDocumentScrollTests.TabId));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-rendered .doc-byline")));

        string[] order = [.. cut.FindAll(".library-rendered > .library-document-tags, .library-rendered > h1, .library-rendered > .doc-byline, .library-rendered > p").Select(static e => e.LocalName == "div" ? e.ClassList[0] : e.LocalName)];
        Assert.Equal(["library-document-tags", "h1", "doc-byline", "p"], order);
        Assert.Equal(["alpha", "beta"], cut.FindAll(".library-document-tag").Select(static e => e.TextContent.Trim()));
        // Written by · Edited by · Reviewed by · the time: three separators for four groups.
        Assert.Equal(["·", "·", "·"], cut.FindAll(".doc-byline > .doc-byline-sep").Select(static e => e.TextContent));
        Assert.Equal("doc-byline-part", cut.Find(".doc-byline").FirstElementChild?.ClassList[0]);
    }

    /// <summary>A note with no tags draws no tags row, and one that does not open with a heading gets the byline above its body.</summary>
    [Fact]
    public async Task Read_NoTagsNoHeading_BylineLeadsTheBody()
    {
        string root = this.library.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "plain.md"), "Just a paragraph.\n");
        LibraryPath path = this.library.Resolve(root, "plain.md");
        await using MudBunitContext ctx = this.NewContext();
        this.SeedHistory(ctx, path);
        IRenderedComponent<ContainerFragment> cut = LibraryDocumentTestSupport.RenderDoc(ctx, path, tabPage: new TabPageContext("/library", LibraryDocumentScrollTests.TabId));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-rendered .doc-byline")));

        Assert.Empty(cut.FindAll(".library-document-tags"));
        Assert.Equal(["doc-byline", "p"], cut.FindAll(".library-rendered > .doc-byline, .library-rendered > p").Select(static e => e.LocalName == "div" ? e.ClassList[0] : e.LocalName));
    }

    private void SeedHistory(MudBunitContext ctx, LibraryPath path)
    {
        DocHistoryStore store = new(this.library.BoundOptions, NullLogger<DocHistoryStore>.Instance);
        store.Append(path, new DocHistoryEntry(DateTimeOffset.UtcNow.AddHours(-3), "Mira", DocActorKind.Agent, DocChangeKind.Created, "Drafted"));
        store.Append(path, new DocHistoryEntry(DateTimeOffset.UtcNow.AddHours(-2), "Ruth", DocActorKind.Agent, DocChangeKind.Edited, "Tightened"));
        store.Append(path, new DocHistoryEntry(DateTimeOffset.UtcNow.AddHours(-1), "Lena", DocActorKind.Agent, DocChangeKind.Reviewed, "Checked"));
        ctx.Services.AddSingleton(store);
        ctx.Services.AddSingleton(new Agency.Huddle.App.Avatars.AvatarStore(this.library.BoundOptions, NullLogger<Agency.Huddle.App.Avatars.AvatarStore>.Instance));
        ctx.Services.AddPersonaStore();
        ctx.Services.AddSingleton(new RightSidebarService());
    }

    /// <summary>With History switched off the document shows neither the byline nor the History.</summary>
    [Fact]
    public async Task HistoryDisabled_ShowsNeitherByline()
    {
        LibraryPath path = this.NewDoc();
        await using MudBunitContext ctx = this.NewContext();
        TeamOptions off = new() { DataDir = this.library.DataDir, DocHistory = { Enabled = false } };
        DocHistoryStore store = new(Microsoft.Extensions.Options.Options.Create(off), NullLogger<DocHistoryStore>.Instance);
        store.Append(path, new DocHistoryEntry(DateTimeOffset.UtcNow, "Mira", DocActorKind.Agent, DocChangeKind.Created, "Drafted"));
        ctx.Services.AddSingleton(store);
        IRenderedComponent<ContainerFragment> cut = LibraryDocumentTestSupport.RenderDoc(ctx, path, tabPage: new TabPageContext("/library", LibraryDocumentScrollTests.TabId));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-document-toolbar")));

        Assert.Empty(cut.FindAll(".doc-byline"));
        Assert.Empty(cut.FindAll(".doc-history"));
    }

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
