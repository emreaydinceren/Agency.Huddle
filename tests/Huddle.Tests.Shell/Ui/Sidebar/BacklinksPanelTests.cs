using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Sidebar;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Ui.Sidebar;

/// <summary>Pins <see cref="BacklinksPanel"/>: the notes that link to the open note, each with its linking line (Spec §6.5).</summary>
public sealed class BacklinksPanelTests
{
    /// <summary>Stands in for the Task store: the rows it reports, and a way to say the Task list changed.</summary>
    private sealed class FakeTaskBacklinks(params TaskBacklink[] rows) : ITaskBacklinkSource
    {
        public event Action? Changed;

        public IReadOnlyList<TaskBacklink> Rows { get; set; } = rows;

        public IReadOnlyList<TaskBacklink> For(LibraryPath document) => this.Rows;

        public void RaiseChanged() => this.Changed?.Invoke();
    }

    /// <summary>A row for the Task <c>PLAT-0001</c>, "Draft the plan".</summary>
    private static TaskBacklink TaskRow(string line)
    {
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        return new TaskBacklink(id, "Draft the plan", TaskState.InProgress, line);
    }

    /// <summary>Renders the panel for <paramref name="note"/> over <paramref name="fixture"/>'s resolver and index.</summary>
    private static IRenderedComponent<CascadingValue<SidebarPanelContext>> Render(
        MudBunitContext ctx,
        LibraryFileServiceFixture fixture,
        SidebarPanelContext panelCtx,
        LibraryPath note,
        ITaskBacklinkSource? tasks = null)
    {
        ctx.Services.AddSingleton(fixture.Resolver);
        ctx.Services.AddSingleton(fixture.Index);
        ctx.Services.AddSingleton<ITaskBacklinkSource>(tasks ?? new FakeTaskBacklinks());
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

    /// <summary>An open Task that links here is a row after the notes, with its title as a link to the Task, the line that links and its state dot; the count includes it.</summary>
    [Fact]
    public async Task Backlinks_ListsLinkingTasksAfterNotes()
    {
        await using MudBunitContext ctx = new();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "a.md"), "# a\n");
        File.WriteAllText(Path.Combine(root, "b.md"), "Drafting [[a]]\n");
        fixture.Reload();
        SidebarPanelContext panelCtx = new();

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = Render(ctx, fixture, panelCtx, fixture.Resolve(root, "a.md"), new FakeTaskBacklinks(TaskRow("See [[a]] for scope")));

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".backlink-item").Count));
        Assert.Single(cut.FindAll(".backlink-item:not(.backlink-task-item)"));
        Assert.Single(cut.FindAll(".backlink-task-item"));
        Assert.Equal("/tasks/item/PLAT-0001", cut.Find(".backlink-task-item .backlink-item-title").GetAttribute("href"));
        Assert.Equal("Draft the plan", cut.Find(".backlink-task-item .backlink-item-title").TextContent.Trim());
        Assert.Equal("[[a]]", cut.Find(".backlink-task-item .backlink-item-ref").TextContent);
        Assert.Single(cut.FindAll(".backlink-task-item .task-state-dot"));
        Assert.Equal(2, panelCtx.Count);
    }

    /// <summary>A document only a Task links to shows that Task, not the empty text.</summary>
    [Fact]
    public async Task Backlinks_OnlyATaskLinks_ShowsTheTask()
    {
        await using MudBunitContext ctx = new();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "a.md"), "# a\n");
        fixture.Reload();

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = Render(ctx, fixture, new SidebarPanelContext(), fixture.Resolve(root, "a.md"), new FakeTaskBacklinks(TaskRow("`E:\\x\\a.md`")));

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".backlink-task-item")));
        Assert.Empty(cut.FindAll("[role=status]"));
    }

    /// <summary>The Task list changing reloads the panel: a Task that starts to link appears without the document being reopened.</summary>
    [Fact]
    public async Task Backlinks_TasksChanged_Reloads()
    {
        await using MudBunitContext ctx = new();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "a.md"), "# a\n");
        fixture.Reload();
        FakeTaskBacklinks tasks = new();
        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = Render(ctx, fixture, new SidebarPanelContext(), fixture.Resolve(root, "a.md"), tasks);
        cut.WaitForAssertion(() => Assert.Equal("Nothing links here.", cut.Find("[role=status]").TextContent.Trim()));

        tasks.Rows = [TaskRow("[[a]]")];
        tasks.RaiseChanged();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".backlink-task-item")));
    }

    /// <summary>A folder too large to index still shows its Tasks, beside the notice that notes are not available.</summary>
    [Fact]
    public async Task Backlinks_IndexUnavailable_StillShowsTasks()
    {
        await using MudBunitContext ctx = new();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build(o => o.Library.MaxIndexedFiles = 1);
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "a.md"), "# a\n");
        File.WriteAllText(Path.Combine(root, "b.md"), "# b\n");
        fixture.Reload();
        SidebarPanelContext panelCtx = new();

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = Render(ctx, fixture, panelCtx, fixture.Resolve(root, "a.md"), new FakeTaskBacklinks(TaskRow("[[a]]")));

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".backlink-task-item")));
        Assert.Equal("Backlinks from notes aren't available for this folder: it has too many files.", cut.Find(".mud-typography-body2.mud-text-secondary").TextContent.Trim());
        Assert.Equal(1, panelCtx.Count);
    }

    /// <summary>A note nothing links to says so.</summary>
    [Fact]
    public async Task Backlinks_None_SaysNothingLinksHere()
    {
        await using MudBunitContext ctx = new();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "a.md"), "# a\n");
        fixture.Reload();

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = Render(ctx, fixture, new SidebarPanelContext(), fixture.Resolve(root, "a.md"));

        cut.WaitForAssertion(() => Assert.Equal("Nothing links here.", cut.Find("[role=status]").TextContent.Trim()));
        Assert.Empty(cut.FindAll(".backlink-item"));
    }
}
