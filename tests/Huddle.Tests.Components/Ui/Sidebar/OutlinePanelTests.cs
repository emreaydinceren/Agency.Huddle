using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Sidebar;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Sidebar;

namespace Agency.Huddle.Tests.Ui.Sidebar;

/// <summary>Pins <see cref="OutlinePanel"/> (Spec Outline, D-8): the headings of the document, indented by level, counted, and clickable.</summary>
public sealed class OutlinePanelTests
{
    private const string TabId = "t1";

    private static readonly LibraryPath NotePath = new(
        new LibraryRoot("teams", "Teams", "C:\\teams", LibraryRootKind.Teams),
        "note.md",
        "C:\\teams\\note.md",
        LibraryNodeRole.File);

    private static DocumentSubject Doc(string? text) => new(NotePath, LibraryFileKind.Markdown, text);

    private static IRenderedComponent<CascadingValue<SidebarPanelContext>> Render(MudBunitContext ctx, SidebarPanelContext panelCtx, DocumentSubject subject)
    {
        return ctx.Render<CascadingValue<SidebarPanelContext>>(p => p
            .Add(c => c.Value, panelCtx)
            .Add(c => c.IsFixed, true)
            .AddChildContent<OutlinePanel>(s => s
                .Add(c => c.Subject, subject)
                .Add(c => c.TabId, TabId)));
    }

    /// <summary>Each heading is an outline item whose data-level is relative to the shallowest heading, 0-based.</summary>
    [Fact]
    public async Task Outline_ListsHeadingsIndentedByLevel()
    {
        await using MudBunitContext ctx = UiTestHost.Create();
        SidebarPanelContext panelCtx = new();

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = Render(ctx, panelCtx, Doc("## Alpha\n\n### Beta\n\n#### Gamma\n\n## Delta\n"));

        string[] levels = [.. cut.FindAll(".outline-item").Select(e => e.GetAttribute("data-level") ?? "")];
        string[] texts = [.. cut.FindAll(".outline-item").Select(e => e.TextContent.Trim())];
        Assert.Equal(["0", "1", "2", "0"], levels);
        Assert.Equal(["Alpha", "Beta", "Gamma", "Delta"], texts);
    }

    /// <summary>The panel reports the number of headings as its count.</summary>
    [Fact]
    public async Task Outline_SetsTheCount()
    {
        await using MudBunitContext ctx = UiTestHost.Create();
        SidebarPanelContext panelCtx = new();

        _ = Render(ctx, panelCtx, Doc("# One\n\n## Two\n\n## Three\n"));

        Assert.Equal(3, panelCtx.Count);
    }

    /// <summary>A document with no headings shows the empty line and a count of 0.</summary>
    [Fact]
    public async Task Outline_NoHeadings_ShowsEmptyLine()
    {
        await using MudBunitContext ctx = UiTestHost.Create();
        SidebarPanelContext panelCtx = new();

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = Render(ctx, panelCtx, Doc("just text\n"));

        Assert.Equal("No headings in this document.", cut.Find("[role=status]").TextContent.Trim());
        Assert.Empty(cut.FindAll(".outline-item"));
        Assert.Equal(0, panelCtx.Count);
    }

    /// <summary>New text for the same Subject Key re-parses the headings on the same panel instance.</summary>
    [Fact]
    public async Task Outline_NewText_SameInstance_Updates()
    {
        await using MudBunitContext ctx = UiTestHost.Create();
        SidebarPanelContext panelCtx = new();
        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = Render(ctx, panelCtx, Doc("# First\n"));
        IRenderedComponent<OutlinePanel> panel = cut.FindComponent<OutlinePanel>();

        panel.Render(p => p
            .Add(c => c.Subject, Doc("# First\n\n## Second\n"))
            .Add(c => c.TabId, TabId));

        Assert.Equal(["First", "Second"], [.. panel.FindAll(".outline-item").Select(e => e.TextContent.Trim())]);
        Assert.Equal(2, panelCtx.Count);
    }

    /// <summary>Clicking a heading sends ScrollToHeading with its id and line to the panel's Tab.</summary>
    [Fact]
    public async Task Outline_Click_SendsScrollToHeading()
    {
        await using MudBunitContext ctx = UiTestHost.Create();
        List<PageCommand> received = [];
        using IDisposable subscription = ctx.Services.GetRequiredService<PageCommandBus>().Subscribe(TabId, command =>
        {
            received.Add(command);
            return Task.CompletedTask;
        });
        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = Render(ctx, new SidebarPanelContext(), Doc("intro\n\n# Plan\n\n## Risks\n"));

        await cut.FindAll(".outline-item")[1].ClickAsync(new());

        ScrollToHeading command = Assert.IsType<ScrollToHeading>(Assert.Single(received));
        Assert.Equal("doc-h-risks", command.HeadingId);
        Assert.Equal(5, command.Line);
    }
}
