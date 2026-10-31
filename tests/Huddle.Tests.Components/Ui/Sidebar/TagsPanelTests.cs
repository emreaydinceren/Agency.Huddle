using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Components.Sidebar;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Ui.Sidebar;

/// <summary>Pins <see cref="TagsPanel"/>: the open note's tags, the root's tag tree with counts, drill-in and Back, Refresh and the unavailable state (Spec §6.17).</summary>
public sealed class TagsPanelTests
{
    /// <summary>Renders the panel for <paramref name="note"/> over <paramref name="links"/>.</summary>
    private static IRenderedComponent<CascadingValue<SidebarPanelContext>> Render(
        MudBunitContext ctx,
        LibraryFileServiceFixture fixture,
        WikiLinkIndex links,
        SidebarPanelContext panelCtx,
        LibraryPath note)
    {
        ctx.Services.AddSingleton(fixture.Resolver);
        ctx.Services.AddSingleton(links);
        ctx.Services.AddSingleton(new LibraryTagIndex(links));
        DocumentSubject subject = new(note, LibraryFileKind.Markdown, null);
        return ctx.Render<CascadingValue<SidebarPanelContext>>(p => p
            .Add(c => c.Value, panelCtx)
            .Add(c => c.IsFixed, true)
            .AddChildContent<TagsPanel>(s => s
                .Add(c => c.Subject, subject)
                .Add(c => c.TabId, "t1")));
    }

    private static WikiLinkIndex Links(LibraryFileServiceFixture fixture, int maxIndexedFiles = 5000) =>
        new(fixture.RootStore, fixture.Resolver, Options.Create(new TeamOptions { Library = new LibraryOptions { MaxIndexedFiles = maxIndexedFiles } }));

    /// <summary>The open note's tags show as chips, the root's tags as a tree with counts, and the chip count reaches the header.</summary>
    [Fact]
    public async Task Tags_ShowsChipsAndTreeWithCounts()
    {
        await using MudBunitContext ctx = new();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "a.md"), "#work/active #inbox\n");
        File.WriteAllText(Path.Combine(root, "b.md"), "#inbox\n");
        fixture.Reload();
        using WikiLinkIndex links = Links(fixture);
        SidebarPanelContext panelCtx = new();

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = Render(ctx, fixture, links, panelCtx, fixture.Resolve(root, "a.md"));

        cut.WaitForAssertion(() => Assert.Equal(["work/active", "inbox"], cut.FindAll(".tags-chips .tag-chip").Select(static e => e.TextContent.Trim())));
        Assert.Equal(["inbox (2)", "work (1)"], cut.FindAll(".tags-tree-row").Select(static e => e.TextContent.Trim()));
        Assert.Equal(2, panelCtx.Count);
    }

    /// <summary>A note with no tags says so, and the root tree still shows.</summary>
    [Fact]
    public async Task Tags_NoteWithoutTags_SaysSoAndKeepsTree()
    {
        await using MudBunitContext ctx = new();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "a.md"), "plain\n");
        File.WriteAllText(Path.Combine(root, "b.md"), "#inbox\n");
        fixture.Reload();
        using WikiLinkIndex links = Links(fixture);

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = Render(ctx, fixture, links, new SidebarPanelContext(), fixture.Resolve(root, "a.md"));

        cut.WaitForAssertion(() => Assert.Equal(["This note has no tags."], cut.FindAll("[role=status]").Select(static e => e.TextContent.Trim())));
        Assert.Single(cut.FindAll(".tags-tree-row"));
    }

    /// <summary>A root with no tags at all says so.</summary>
    [Fact]
    public async Task Tags_EmptyRoot_SaysSo()
    {
        await using MudBunitContext ctx = new();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "a.md"), "plain\n");
        fixture.Reload();
        using WikiLinkIndex links = Links(fixture);

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = Render(ctx, fixture, links, new SidebarPanelContext(), fixture.Resolve(root, "a.md"));

        cut.WaitForAssertion(() => Assert.Equal(
            ["This note has no tags.", "No tags in this folder."],
            cut.FindAll("[role=status]").Select(static e => e.TextContent.Trim())));
    }

    /// <summary>A root over <c>MaxIndexedFiles</c> says the tags aren't available.</summary>
    [Fact]
    public async Task Tags_UnavailableRoot_SaysSo()
    {
        await using MudBunitContext ctx = new();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "a.md"), "#x\n");
        File.WriteAllText(Path.Combine(root, "b.md"), "#y\n");
        fixture.Reload();
        using WikiLinkIndex links = Links(fixture, maxIndexedFiles: 1);

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = Render(ctx, fixture, links, new SidebarPanelContext(), fixture.Resolve(root, "a.md"));

        cut.WaitForAssertion(() => Assert.Equal(
            ["Tags aren't available for this folder: it has too many files."],
            cut.FindAll("[role=status]").Select(static e => e.TextContent.Trim())));
        Assert.Empty(cut.FindAll(".tag-chip"));
    }

    /// <summary>A chip drills in to the notes that carry the tag, as links; Back returns to the tags.</summary>
    [Fact]
    public async Task Tags_ChipDrillsInAndBackReturns()
    {
        await using MudBunitContext ctx = new();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "a.md"), "#inbox\n");
        File.WriteAllText(Path.Combine(root, "b.md"), "#inbox\n");
        fixture.Reload();
        using WikiLinkIndex links = Links(fixture);
        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = Render(ctx, fixture, links, new SidebarPanelContext(), fixture.Resolve(root, "a.md"));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".tags-chips .tag-chip")));

        cut.Find(".tags-chips .tag-chip").Click();

        cut.WaitForAssertion(() => Assert.Equal(["a", "b"], cut.FindAll(".backlink-item-title").Select(static e => e.TextContent.Trim())));
        Assert.Equal(LibraryLinkQuery.BuildPageUri(fixture.Resolve(root, "b.md")), cut.FindAll(".backlink-item-title")[1].GetAttribute("href"));

        cut.Find("button[aria-label='Back to tags']").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".tags-chips .tag-chip")));
        Assert.Empty(cut.FindAll(".backlink-item-title"));
    }

    /// <summary>Refresh rebuilds the root, so a tag added on disk afterwards appears in the tree.</summary>
    [Fact]
    public async Task Tags_Refresh_RebuildsTheRoot()
    {
        await using MudBunitContext ctx = new();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "a.md"), "#inbox\n");
        fixture.Reload();
        using WikiLinkIndex links = Links(fixture);
        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = Render(ctx, fixture, links, new SidebarPanelContext(), fixture.Resolve(root, "a.md"));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".tags-tree-row")));
        File.WriteAllText(Path.Combine(root, "b.md"), "#later\n");

        cut.Find("button[aria-label='Refresh tags']").Click();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".tags-tree-row").Count));
    }

    /// <summary>The drill-in shows no "No notes carry this tag." message until its list has loaded, and shows it when the list is empty.</summary>
    [Fact]
    public async Task Tags_DrillIn_ShowsEmptyMessageOnlyAfterLoading()
    {
        await using MudBunitContext ctx = new();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "a.md"), "#inbox\n");
        fixture.Reload();
        using WikiLinkIndex links = Links(fixture);
        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = Render(ctx, fixture, links, new SidebarPanelContext(), fixture.Resolve(root, "a.md"));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".tags-chips .tag-chip")));

        cut.Find(".tags-chips .tag-chip").Click();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".backlink-item-title")));
        Assert.Empty(cut.FindAll("[role=status]"));
    }

    /// <summary>Expanding a parent row loads its children, each with its own count.</summary>
    [Fact]
    public async Task Tags_ExpandingParent_ShowsChildrenWithCounts()
    {
        await using MudBunitContext ctx = new();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "a.md"), "#work/active\n");
        File.WriteAllText(Path.Combine(root, "b.md"), "#work/later\n");
        fixture.Reload();
        using WikiLinkIndex links = Links(fixture);
        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = Render(ctx, fixture, links, new SidebarPanelContext(), fixture.Resolve(root, "a.md"));
        cut.WaitForAssertion(() => Assert.Equal(["work (2)"], cut.FindAll(".tags-tree-row").Select(static e => e.TextContent.Trim())));

        cut.Find(".mud-treeview-item-arrow button").Click();

        cut.WaitForAssertion(() => Assert.Equal(["work (2)", "active (1)", "later (1)"], cut.FindAll(".tags-tree-row").Select(static e => e.TextContent.Trim())));
    }

    /// <summary>A new open note reloads the panel's chips.</summary>
    [Fact]
    public async Task Tags_NewSubject_ReloadsChips()
    {
        await using MudBunitContext ctx = new();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "a.md"), "#one\n");
        File.WriteAllText(Path.Combine(root, "b.md"), "#two\n");
        fixture.Reload();
        using WikiLinkIndex links = Links(fixture);
        ctx.Services.AddSingleton(fixture.Resolver);
        ctx.Services.AddSingleton(links);
        ctx.Services.AddSingleton(new LibraryTagIndex(links));
        IRenderedComponent<TagsPanel> cut = ctx.Render<TagsPanel>(p => p
            .Add(c => c.Subject, new DocumentSubject(fixture.Resolve(root, "a.md"), LibraryFileKind.Markdown, null))
            .Add(c => c.TabId, "t1"));
        cut.WaitForAssertion(() => Assert.Equal(["one"], cut.FindAll(".tags-chips .tag-chip").Select(static e => e.TextContent.Trim())));

        cut.Render(p => p.Add(c => c.Subject, new DocumentSubject(fixture.Resolve(root, "b.md"), LibraryFileKind.Markdown, null)));

        cut.WaitForAssertion(() => Assert.Equal(["two"], cut.FindAll(".tags-chips .tag-chip").Select(static e => e.TextContent.Trim())));
    }
}
