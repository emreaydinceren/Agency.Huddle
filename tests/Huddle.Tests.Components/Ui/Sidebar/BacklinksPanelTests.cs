using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Sidebar;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Ui.Sidebar;

/// <summary>Pins <see cref="BacklinksPanel"/>: the notes that link to the open note, each with its linking line (Spec §6.5).</summary>
public sealed class BacklinksPanelTests
{
    /// <summary>Renders the panel for <paramref name="note"/> over <paramref name="fixture"/>'s resolver and index.</summary>
    private static IRenderedComponent<CascadingValue<SidebarPanelContext>> Render(
        MudBunitContext ctx,
        LibraryFileServiceFixture fixture,
        SidebarPanelContext panelCtx,
        LibraryPath note)
    {
        ctx.Services.AddSingleton(fixture.Resolver);
        ctx.Services.AddSingleton(fixture.Index);
        DocumentSubject subject = new(note, LibraryFileKind.Markdown, null);
        return ctx.Render<CascadingValue<SidebarPanelContext>>(p => p
            .Add(c => c.Value, panelCtx)
            .Add(c => c.IsFixed, true)
            .AddChildContent<BacklinksPanel>(s => s
                .Add(c => c.Subject, subject)
                .Add(c => c.TabId, "t1")));
    }

    /// <summary>A note linking here is a row with its name as a link, the line it sits on and the wikilink picked out; the count reaches the header.</summary>
    [Fact]
    public async Task Backlinks_ListsLinkingNotesWithTheLine()
    {
        await using MudBunitContext ctx = new();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "a.md"), "# a\n");
        File.WriteAllText(Path.Combine(root, "b.md"), "Drafting [[a]] from 3 tickets\n");
        fixture.Reload();
        LibraryPath note = fixture.Resolve(root, "a.md");
        LibraryPath linking = fixture.Resolve(root, "b.md");
        SidebarPanelContext panelCtx = new();

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = Render(ctx, fixture, panelCtx, note);

        cut.WaitForAssertion(() => Assert.Equal("b", cut.Find(".backlink-item-title").TextContent.Trim()));
        Assert.Equal(LibraryLinkQuery.BuildPageUri(linking), cut.Find(".backlink-item-title").GetAttribute("href"));
        Assert.Equal("[[a]]", cut.Find(".backlink-item-ref").TextContent);
        Assert.Equal("Drafting [[a]] from 3 tickets", cut.Find(".backlink-item-snippet").TextContent.Trim());
        Assert.Equal(1, panelCtx.Count);
    }

    /// <summary>A note nothing links to says so.</summary>
    [Fact]
    public async Task Backlinks_None_SaysNoNotesLinkHere()
    {
        await using MudBunitContext ctx = new();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "a.md"), "# a\n");
        fixture.Reload();

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = Render(ctx, fixture, new SidebarPanelContext(), fixture.Resolve(root, "a.md"));

        cut.WaitForAssertion(() => Assert.Equal("No notes link here.", cut.Find("[role=status]").TextContent.Trim()));
        Assert.Empty(cut.FindAll(".backlink-item"));
    }
}
