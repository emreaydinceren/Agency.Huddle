using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Extensions;
using Agency.Huddle.App.Components.Library;
using Agency.Huddle.App.Library;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>
/// Pins <c>LibraryDocument</c>'s Read mode and view states (Task 12.2.t) over a REAL
/// <see cref="LibraryFileService"/> on a temp tree (Spec §6.7 Read row, §6.11, §6.12, §10 E-3/E-5/E-6,
/// §8 Tool Bar/Breadcrumbs/Toggle Group). Tests tagged <c>[12.2a]</c> cover the header, file kinds and
/// states (corrections-B6 item 31 split a); tests tagged <c>[12.2b]</c> cover link navigation, wired
/// through <see cref="NavigationManager.RegisterLocationChangingHandler"/> per corrections-B6 item 19,
/// never a container <c>@onclick:preventDefault</c> or a JS <c>linkFromEvent</c> helper.
/// </summary>
public sealed class LibraryDocumentReadTests : IDisposable
{
    private readonly DocFixture fixture = DocFixture.Build();

    /// <summary>Disposes the underlying temp <c>DataDir</c> and stores.</summary>
    public void Dispose() => this.fixture.Dispose();

    /// <summary>[12.2a] A Markdown file in the default Read mode renders through <see cref="Agency.Huddle.App.Services.MarkdownRenderer"/> inside <c>.library-rendered</c>.</summary>
    [Fact]
    public async Task Markdown_ReadMode_RendersHtml()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "a.md"), "# Hello");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);

        cut.WaitForAssertion(() => Assert.Equal("Hello", cut.Find(".library-rendered h1").TextContent.Trim()));
    }

    /// <summary>[12.2a] The breadcrumbs show the root's display name, then each folder on the way to the file, in order (Spec §8).</summary>
    [Fact]
    public async Task Header_BreadcrumbsRootThenFolders()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        Directory.CreateDirectory(Path.Combine(root, "sub", "deeper"));
        File.WriteAllText(Path.Combine(root, "sub", "deeper", "a.md"), "hi");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "sub/deeper/a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<MudBlazor.MudBreadcrumbs>()));
        MudBlazor.MudBreadcrumbs breadcrumbs = cut.FindComponent<MudBlazor.MudBreadcrumbs>().Instance;
        Assert.Equal(["Notes", "sub", "deeper"], (breadcrumbs.Items ?? []).Select(item => item.Text ?? string.Empty));
    }

    /// <summary>[12.2a] The header's Read/Edit/Split toggle group starts with Read selected for an editable Markdown document (Spec §8).</summary>
    [Fact]
    public async Task Header_ToggleGroupReadEditSplit()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "a.md"), "hi");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<MudBlazor.MudToggleGroup<LibraryMode>>()));
        MudBlazor.MudToggleGroup<LibraryMode> toggle = cut.FindComponent<MudBlazor.MudToggleGroup<LibraryMode>>().Instance;
        Assert.Equal(LibraryMode.Read, toggle.GetState(x => x.Value));
    }

    /// <summary>[12.2a] A JSON file opens read-only in <see cref="LibraryEditor"/> with the json language mode - no Read/Edit/Split toggle, since it is never editable (Spec §6.11).</summary>
    [Fact]
    public async Task Json_ReadOnlyEditor_NoToggle()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "a.json"), "{}");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.json");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<LibraryEditor>()));
        LibraryEditor editor = cut.FindComponent<LibraryEditor>().Instance;
        Assert.True(editor.ReadOnly);
        Assert.Equal("json", editor.LanguageId);
        Assert.Empty(cut.FindComponents<MudBlazor.MudToggleGroup<LibraryMode>>());
    }

    /// <summary>[12.2a] An image file is shown as an <c>&lt;img&gt;</c> served from <c>/library-files/{rootId}/…</c> (Spec §6.11).</summary>
    [Fact]
    public async Task Image_ShowsImgFromLibraryFiles()
    {
        Directory.CreateDirectory(Path.Combine(this.fixture.LibraryFixture.DataDir, "Teams", "Marketing"));
        string pngPath = Path.Combine(this.fixture.LibraryFixture.DataDir, "Teams", "Marketing", "x.png");
        File.WriteAllBytes(pngPath, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0]);
        this.fixture.LibraryFixture.Reload();
        LibraryPath path = this.fixture.LibraryFixture.ResolveTeams("Marketing/x.png");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("img.library-document-image")));
        Assert.Equal("/library-files/teams/Marketing/x.png", cut.Find("img.library-document-image").GetAttribute("src"));
    }

    /// <summary>[12.2a] An SVG is never served as an image; it is offered as read-only text (Spec §6.11, rules.md).</summary>
    [Fact]
    public async Task Svg_ShowsAsText()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        const string svg = "<svg><script>alert(1)</script></svg>";
        File.WriteAllText(Path.Combine(root, "a.svg"), svg);
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.svg");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<LibraryEditor>()));
        LibraryEditor editor = cut.FindComponent<LibraryEditor>().Instance;
        Assert.True(editor.ReadOnly);
        Assert.Equal(svg, editor.Text);
        Assert.Empty(cut.FindAll("img.library-document-image"));
    }

    /// <summary>[12.2a] A file of an unrecognised kind shows a card with its name, size and an "Open in default app" action (Spec §6.11).</summary>
    [Fact]
    public async Task Other_ShowsCardWithOpenDefault()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllBytes(Path.Combine(root, "a.bin"), new byte[1024]);
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.bin");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-document-other")));
        Assert.Equal("a.bin", cut.Find(".library-document-other-name").TextContent.Trim());
        Assert.Equal("1 KB", cut.Find(".library-document-other-size").TextContent.Trim());
        Assert.Equal("Open in default app", cut.Find(".library-document-open-default").TextContent.Trim());
    }

    /// <summary>[12.2a] A Markdown file over <c>MaxEditableBytes</c> shows the settled too-large reason and no mode toggle (Spec §6.4/§8).</summary>
    [Fact]
    public async Task TooLarge_ShowsReason()
    {
        using DocFixture smallLimitFixture = DocFixture.Build(options => options.Library.MaxEditableBytes = 16);
        string root = smallLimitFixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "a.md"), new string('x', 1024));
        LibraryPath path = smallLimitFixture.LibraryFixture.Resolve(root, "a.md");

        await using MudBunitContext ctx = smallLimitFixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);

        cut.WaitForAssertion(() => Assert.Equal(
            "This file is too large to edit here.",
            cut.Find(".library-document-banner").TextContent.Trim()));
        Assert.Empty(cut.FindComponents<MudBlazor.MudToggleGroup<LibraryMode>>());
    }

    /// <summary>[12.2a] A non-UTF-8 legacy file opens read-only with the settled unsupported-encoding reason (Spec §10 E-5).</summary>
    [Fact]
    public async Task Unsupported_ShowsReason()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        byte[] windows1252 = [0x80, 0x81, 0x8D, 0x8F, 0x90, 0x9D];
        File.WriteAllBytes(Path.Combine(root, "a.txt"), windows1252);
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.txt");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);

        cut.WaitForAssertion(() => Assert.Equal(
            "Unsupported text format",
            cut.Find(".library-document-banner").TextContent.Trim()));
    }

    /// <summary>[12.2a] A Teammate's definition file opens with the settled banner naming it (Spec §6.12).</summary>
    [Fact]
    public async Task Definition_ShowsBanner()
    {
        this.fixture.LibraryFixture.CreateTeammate("ada", "Ada", "ada");
        LibraryPath path = this.fixture.LibraryFixture.ResolveTeammates("ada");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);

        cut.WaitForAssertion(() => Assert.Equal(
            "This is Ada's definition.",
            cut.Find(".library-document-banner").TextContent.Trim()));
    }

    /// <summary>[12.2a] A file renamed or deleted outside Huddle shows the settled moved-or-deleted banner (Spec §10 E-3).</summary>
    [Fact]
    public async Task Missing_ShowsMovedOrDeleted()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        string filePath = Path.Combine(root, "a.md");
        File.WriteAllText(filePath, "hi");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "a.md");
        File.Delete(filePath);

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);

        cut.WaitForAssertion(() => Assert.Equal(
            "This file was moved or deleted.",
            cut.Find(".library-document-banner").TextContent.Trim()));
    }

    /// <summary>[12.2a] A file outside the pane's <c>Scope</c> shows the settled hint and an "Open in Library" action instead of its content (Spec §6.16, §8).</summary>
    [Fact]
    public async Task OutsideScope_ShowsHintAndOpenInLibrary()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        Directory.CreateDirectory(Path.Combine(root, "inscope"));
        Directory.CreateDirectory(Path.Combine(root, "outside"));
        File.WriteAllText(Path.Combine(root, "outside", "a.md"), "hi");
        LibraryPath scope = this.fixture.LibraryFixture.Resolve(root, "inscope");
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, "outside/a.md");

        int openInLibraryCount = 0;
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderDoc(
            ctx, path, scope: scope, onOpenInLibrary: EventCallback.Factory.Create<LibraryPath>(this, p => openInLibraryCount++));

        cut.WaitForAssertion(() => Assert.Equal("Outside this view", cut.Find(".library-document-outside-scope").TextContent.Trim()));
        await cut.InvokeAsync(() => cut.Find("button.library-document-open-in-library").Click());
        Assert.Equal(1, openInLibraryCount);
    }

    /// <summary>
    /// [12.2b] Navigating to a resolved wikilink's rendered <c>href</c> raises <c>OnNavigate</c> with the
    /// target's <see cref="LibraryPath"/>. bUnit doesn't simulate the router's anchor interception; in the
    /// browser a click on this same-origin href becomes <c>NavigateTo</c> (UAT covers the real click).
    /// </summary>
    [Fact]
    public async Task WikiLinkClick_RaisesOnNavigate()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "b.md"), "target");
        LibraryPath source = this.fixture.LibraryFixture.Resolve(root, "a.md");
        LibraryPath target = this.fixture.LibraryFixture.Resolve(root, "b.md");
        File.WriteAllText(Path.Combine(root, "a.md"), "See [[b]]");

        LibraryPath? navigated = null;
        FakeLibraryNoteResolver resolver = new(resolveWikiLink: (_, _) => new LibraryReference(target.Root.Id, target.RelativePath, Exists: true));
        await using MudBunitContext ctx = this.fixture.NewContext(resolver);
        IRenderedComponent<ContainerFragment> cut = RenderDoc(
            ctx, source, onNavigate: EventCallback.Factory.Create<LibraryPath>(this, p => navigated = p));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("a.library-ref")));
        string href = cut.Find("a.library-ref").GetAttribute("href") ?? string.Empty;
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        await cut.InvokeAsync(() => navigation.NavigateTo(href));

        Assert.NotNull(navigated);
        Assert.Equal(target.RelativePath, navigated.RelativePath);
        Assert.Equal(target.Root.Id, navigated.Root.Id);
    }

    /// <summary>
    /// [12.2b] Navigating to an unresolved wikilink's rendered <c>href</c> offers to create the note, raising
    /// <c>OnAction</c> with <see cref="LibraryTreeActionKind.NewNote"/> and the target's Name (corrections-B6
    /// item 20). bUnit doesn't simulate the router's anchor interception; in the browser a click on this
    /// same-origin href becomes <c>NavigateTo</c> (UAT covers the real click).
    /// </summary>
    [Fact]
    public async Task MissingWikiLinkClick_OffersCreate()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        LibraryPath source = this.fixture.LibraryFixture.Resolve(root, "a.md");
        File.WriteAllText(Path.Combine(root, "a.md"), "See [[NewIdea]]");
        LibraryRoot rootInfo = source.Root;

        LibraryTreeAction? raised = null;
        FakeLibraryNoteResolver resolver = new(resolveWikiLink: (_, _) => new LibraryReference(rootInfo.Id, "NewIdea.md", Exists: false));
        await using MudBunitContext ctx = this.fixture.NewContext(resolver);
        IRenderedComponent<ContainerFragment> cut = RenderDoc(
            ctx, source, onAction: EventCallback.Factory.Create<LibraryTreeAction>(this, a => raised = a));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("a.library-ref-missing")));
        string href = cut.Find("a.library-ref-missing").GetAttribute("href") ?? string.Empty;
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        await cut.InvokeAsync(() => navigation.NavigateTo(href));

        Assert.NotNull(raised);
        Assert.Equal(LibraryTreeActionKind.NewNote, raised.Kind);
        Assert.Equal("NewIdea", raised.Name);
    }

    /// <summary>[12.2b] Navigating to a <c>?library=</c> target (as a click on a rendered <c>a.library-ref</c> would) raises <c>OnNavigate</c> and cancels the browser navigation - no container <c>preventDefault</c> or JS helper (corrections-B6 item 19).</summary>
    [Fact]
    public async Task Navigating_ToLibraryTarget_RaisesOnNavigate_AndCancelsNavigation()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "b.md"), "target");
        LibraryPath source = this.fixture.LibraryFixture.Resolve(root, "a.md");
        LibraryPath target = this.fixture.LibraryFixture.Resolve(root, "b.md");
        File.WriteAllText(Path.Combine(root, "a.md"), "hi");

        LibraryPath? navigated = null;
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderDoc(
            ctx, source, onNavigate: EventCallback.Factory.Create<LibraryPath>(this, p => navigated = p));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-rendered")));

        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        string before = navigation.Uri;

        await cut.InvokeAsync(() => navigation.NavigateTo($"?library={target.Root.Id}/{Uri.EscapeDataString(target.RelativePath)}"));

        Assert.NotNull(navigated);
        Assert.Equal(target.RelativePath, navigated.RelativePath);
        Assert.Equal(before, navigation.Uri);
    }

    /// <summary>[12.2b] An external <c>https://</c> navigation is never intercepted: it proceeds and <c>OnNavigate</c> is not raised.</summary>
    [Fact]
    public async Task Navigating_ToExternalLink_IsNotIntercepted()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "a.md"), "hi");
        LibraryPath source = this.fixture.LibraryFixture.Resolve(root, "a.md");

        int navigateCount = 0;
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderDoc(
            ctx, source, onNavigate: EventCallback.Factory.Create<LibraryPath>(this, _ => navigateCount++));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-rendered")));

        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        await cut.InvokeAsync(() => navigation.NavigateTo("https://example.com/"));

        Assert.Equal(0, navigateCount);
        Assert.Equal("https://example.com/", navigation.Uri);
    }

    /// <summary>[12.2b] A <c>?library=</c> target the resolver refuses is cancelled (never left half-navigated) but does not raise <c>OnNavigate</c>.</summary>
    [Fact]
    public async Task Navigating_ToRefusedLibraryTarget_IsCancelled_ButNotRaised()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "a.md"), "hi");
        LibraryPath source = this.fixture.LibraryFixture.Resolve(root, "a.md");

        int navigateCount = 0;
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderDoc(
            ctx, source, onNavigate: EventCallback.Factory.Create<LibraryPath>(this, _ => navigateCount++));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-rendered")));

        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        string before = navigation.Uri;

        await cut.InvokeAsync(() => navigation.NavigateTo($"?library={source.Root.Id}/does-not-exist.md"));

        Assert.Equal(0, navigateCount);
        Assert.Equal(before, navigation.Uri);
    }

    /// <summary>[12.2b] (judgement 45) A scoped document pane does not intercept a <c>?library=</c> navigation to a target OUTSIDE its own <see cref="LibraryDocument.Scope"/>: navigation proceeds and <c>OnNavigate</c> is not raised, so a page-level pane (MainLayout, D13) can act on it instead.</summary>
    [Fact]
    public async Task Navigating_ToTargetOutsideScope_IsNotIntercepted()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        Directory.CreateDirectory(Path.Combine(root, "inscope"));
        File.WriteAllText(Path.Combine(root, "inscope", "a.md"), "hi");
        File.WriteAllText(Path.Combine(root, "outside.md"), "hi");
        LibraryPath scope = this.fixture.LibraryFixture.Resolve(root, "inscope");
        LibraryPath source = this.fixture.LibraryFixture.Resolve(root, "inscope/a.md");
        LibraryPath target = this.fixture.LibraryFixture.Resolve(root, "outside.md");

        int navigateCount = 0;
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderDoc(
            ctx, source, scope: scope, onNavigate: EventCallback.Factory.Create<LibraryPath>(this, _ => navigateCount++));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-rendered")));

        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        string libraryUri = $"?library={target.Root.Id}/{Uri.EscapeDataString(target.RelativePath)}";
        string expectedUri = navigation.ToAbsoluteUri(libraryUri).ToString();

        await cut.InvokeAsync(() => navigation.NavigateTo(libraryUri));

        Assert.Equal(0, navigateCount);
        Assert.Equal(expectedUri, navigation.Uri);
    }

    /// <summary>[12.2a] No mode toggle is shown for a view-only kind: JSON, an image or an unrecognised file (Spec §8's Toggle Group row).</summary>
    [Theory]
    [InlineData("a.json")]
    [InlineData("a.png")]
    [InlineData("a.bin")]
    public async Task Header_ToggleGroup_NotShownForViewOnly(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        byte[] bytes = string.Equals(fileName, "a.png", StringComparison.Ordinal)
            ? [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0]
            : "{}"u8.ToArray();
        File.WriteAllBytes(Path.Combine(root, fileName), bytes);
        LibraryPath path = this.fixture.LibraryFixture.Resolve(root, fileName);

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);

        cut.WaitForAssertion(() => Assert.True(
            cut.FindAll("img.library-document-image, .library-document-other, code, pre").Count > 0
            || cut.FindComponents<LibraryEditor>().Count > 0));
        Assert.Empty(cut.FindComponents<MudBlazor.MudToggleGroup<LibraryMode>>());
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

        /// <summary>Builds a fixture with the standard Teams/Teammates layout, optionally customising <see cref="Agency.Huddle.App.TeamOptions"/> first.</summary>
        /// <param name="configure">An optional callback to customise the bound <see cref="Agency.Huddle.App.TeamOptions"/> before use.</param>
        public static DocFixture Build(Action<Agency.Huddle.App.TeamOptions>? configure = null) => new(LibraryFileServiceFixture.Build(configure));

        /// <summary>A <see cref="MudBunitContext"/> with this fixture's real <see cref="LibraryFileService"/>, resolver, root store, and a permissive <see cref="FakeLibraryNoteResolver"/>/<see cref="FakeTaskReferenceResolver"/> pair.</summary>
        public MudBunitContext NewContext(FakeLibraryNoteResolver? noteResolver = null)
        {
            MudBunitContext ctx = new();
            ctx.Services.AddSingleton(this.LibraryFixture.Resolver);
            ctx.Services.AddSingleton(this.LibraryFixture.RootStore);
            ctx.Services.AddSingleton(this.LibraryFixture.CreateService());
            ctx.Services.AddSingleton(this.LibraryFixture.Index);
            ctx.Services.AddSingleton<ILibraryNoteResolver>(noteResolver ?? new FakeLibraryNoteResolver());
            ctx.Services.AddSingleton<Agency.Huddle.App.Tasks.ITaskReferenceResolver>(new FakeTaskReferenceResolver());
            return ctx;
        }

        /// <summary>Disposes the underlying stores and temp <c>DataDir</c>.</summary>
        public void Dispose() => this.LibraryFixture.Dispose();
    }
}
