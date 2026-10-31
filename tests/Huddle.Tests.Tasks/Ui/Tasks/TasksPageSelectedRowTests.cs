using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui.Tasks;

using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Tabs;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Tools;
using TasksPage = Agency.Huddle.App.Components.Pages.Tasks;

/// <summary>
/// Pins that the Task open in the detail panel is marked in the List row (and the Board card) it came
/// from: the row carries <c>task-row-selected</c> and its title carries <c>aria-current="true"</c>
/// while the panel shows it, no other row does, and closing the panel clears the mark.
/// </summary>
public sealed class TasksPageSelectedRowTests
{
    /// <summary>Only the row of the Task open in the panel is marked selected, visually and for assistive technology.</summary>
    [Fact]
    public async Task ListRow_OpenTask_IsTheOnlySelectedRow()
    {
        using TaskToolHarness harness = new();
        _ = CreateTask(harness, "Alpha");
        _ = CreateTask(harness, "Bravo");

        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);

        Assert.Empty(cut.FindAll("tr.task-row-selected"));

        await ClickTaskRowAsync(cut, "Alpha");

        IElement alpha = FindTaskRow(cut, "Alpha");
        IElement bravo = FindTaskRow(cut, "Bravo");
        Assert.Equal(["mud-table-row", "task-row-selected"], alpha.ClassList);
        Assert.Equal("true", alpha.QuerySelector(".task-list-title")?.GetAttribute("aria-current"));
        Assert.Equal(["mud-table-row"], bravo.ClassList);
        Assert.Null(bravo.QuerySelector(".task-list-title")?.GetAttribute("aria-current"));
    }

    /// <summary>Opening another Task moves the mark to that row.</summary>
    [Fact]
    public async Task ListRow_OpeningAnotherTask_MovesTheMark()
    {
        using TaskToolHarness harness = new();
        _ = CreateTask(harness, "Alpha");
        _ = CreateTask(harness, "Bravo");

        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);

        await ClickTaskRowAsync(cut, "Alpha");
        await ClickTaskRowAsync(cut, "Bravo");

        Assert.Equal(["mud-table-row"], FindTaskRow(cut, "Alpha").ClassList);
        Assert.Equal(["mud-table-row", "task-row-selected"], FindTaskRow(cut, "Bravo").ClassList);
    }

    /// <summary>Closing the Right sidebar that shows the Task clears the mark from every row.</summary>
    [Fact]
    public async Task ListRow_ClosingTheSidebar_ClearsTheMark()
    {
        using TaskToolHarness harness = new();
        _ = CreateTask(harness, "Alpha");

        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);

        await ClickTaskRowAsync(cut, "Alpha");
        Assert.Single(cut.FindAll("tr.task-row-selected"));

        await cut.InvokeAsync(ctx.Services.GetRequiredService<RightSidebarService>().Toggle);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("tr.task-row-selected")));
        Assert.Empty(cut.FindAll(".task-list-title[aria-current]"));
    }

    /// <summary>On the Board, the card of the open Task is marked selected and the others are not; closing the Right sidebar clears it.</summary>
    [Fact]
    public async Task BoardCard_OpenTask_IsTheOnlySelectedCard()
    {
        using TaskToolHarness harness = new();
        _ = CreateTask(harness, "Alpha");
        _ = CreateTask(harness, "Bravo");
        TaskView board = new() { Id = "board-sel", Name = "Board", Kind = ViewKind.Kanban, Columns = BoardLayout.DefaultColumns };
        Assert.True(harness.Views.Save(board).Saved);

        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "board-sel");

        Assert.Empty(cut.FindAll(".task-card-selected"));

        await cut.InvokeAsync(() => cut.FindAll(".task-card-open").First(b => b.TextContent.Contains("Alpha", StringComparison.Ordinal)).ClickAsync());

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".task-card-selected")));
        IElement selected = Assert.Single(cut.FindAll(".task-card-selected"));
        Assert.Equal("Alpha", selected.QuerySelector(".task-card-title")?.TextContent.Trim());
        Assert.Equal(["task-card", "task-card-selected"], selected.ClassList);
        Assert.Equal("true", Assert.Single(cut.FindAll(".task-card-open[aria-current]")).GetAttribute("aria-current"));

        await cut.InvokeAsync(ctx.Services.GetRequiredService<RightSidebarService>().Toggle);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".task-card-selected")));
    }

    /// <summary>app.css declares the selected List row and Board card as a tint of the primary palette colour, with no colour literal.</summary>
    [Fact]
    public void AppCss_DeclaresSelectedRowAndCard_FromPaletteVariables()
    {
        string text = CssText.AppCss(CssNewlines.AsIs);

        // contains-ok: source-fact test, no CSS parser - app.css's own text is what's pinned.
        Assert.Contains("tr.task-row-selected", text, StringComparison.Ordinal);
        // contains-ok: source-fact test, no CSS parser - app.css's own text is what's pinned.
        Assert.Contains(".task-card.task-card-selected", text, StringComparison.Ordinal);
        int start = text.IndexOf("tr.task-row-selected", StringComparison.Ordinal);
        string rule = text[start..text.IndexOf('}', start)];
        // contains-ok: source-fact test, no CSS parser - app.css's own text is what's pinned.
        Assert.Contains("var(--hu-selected-task-background)", rule, StringComparison.Ordinal);
        Assert.DoesNotContain('#', rule);
    }

    /// <summary>Renders the Tasks page, optionally routed to <paramref name="viewId"/>.</summary>
    private static IRenderedComponent<ContainerFragment> RenderPage(MudBunitContext ctx, string? viewId = null) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TasksPage>(0);
            if (viewId is not null)
            {
                builder.AddAttribute(1, nameof(TasksPage.ViewId), viewId);
            }

            builder.CloseComponent();
        });

    /// <summary>Creates a Task through the real <see cref="TaskService"/>.</summary>
    private static TaskItem CreateTask(TaskToolHarness harness, string title)
    {
        TaskResult result = harness.Service.Create(new TaskDraft(title, "Platform", null), TaskActors.Human(harness.Options.Value));
        return Assert.IsType<TaskResult.Saved>(result).Task;
    }

    /// <summary>Finds the List row whose cells contain <paramref name="title"/>.</summary>
    private static IElement FindTaskRow(IRenderedComponent<ContainerFragment> cut, string title)
    {
        IElement cell = cut.FindAll("td").First(td => td.TextContent.Contains(title, StringComparison.Ordinal));
        return cell.Closest("tr") ?? throw new InvalidOperationException("The cell has no row.");
    }

    /// <summary>Clicks the List row whose cells contain <paramref name="title"/>.</summary>
    private static Task ClickTaskRowAsync(IRenderedComponent<ContainerFragment> cut, string title) =>
        cut.InvokeAsync(() => FindTaskRow(cut, title).ClickAsync());
}
