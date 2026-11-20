using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Agency.Huddle.App.Components.Sidebar;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Ui.Sidebar;

/// <summary>
/// Pins the Task order panel under "one Task In Progress per Project" (TaskOrder spec §10.3, ADR-0103): with the rule on exactly
/// one Task per group is Next and counted and the others say what they wait for; with it off the panel is as before.
/// </summary>
public sealed class TaskOrderPanelGroupRuleTests
{
    private const string Item = ".mud-timeline-item";

    private static void Seed(TaskToolHarness harness, params TaskItem[] tasks)
    {
        foreach (TaskItem task in tasks)
        {
            string fullPath = TaskLayout.PathFor(harness.TasksDirPath, task.Location, task.Id);
            TestTaskStore.WriteTask(harness.TasksDirPath, Path.GetRelativePath(harness.TasksDirPath, fullPath), task);
        }

        harness.Store.RebuildFromWatcher();
    }

    private static TaskItem Make(int number, string title, string? project, int rank) =>
        TestTasks.Make(id: $"PLAT-{number:D4}", title: title, location: new TaskLocation("Platform", project, false)) with { Rank = rank };

    private static void TeamRuleOn(TaskToolHarness harness)
    {
        Assert.True(TeamFile.TryParse("---\nteamType: 'web@1.2.0'\noneInProgressPerProject: 'on'\n---\n", out TeamFile? file));
        harness.Folders.ReadResult = file;
    }

    private static IRenderedComponent<ContainerFragment> Render(MudBunitContext ctx, SidebarPanelContext panel, TaskId open) =>
        ctx.Render(builder =>
        {
            builder.OpenComponent<CascadingValue<SidebarPanelContext>>(0);
            builder.AddComponentParameter(1, nameof(CascadingValue<SidebarPanelContext>.Value), panel);
            builder.AddComponentParameter(2, nameof(CascadingValue<SidebarPanelContext>.IsFixed), true);
            builder.AddComponentParameter(3, nameof(CascadingValue<SidebarPanelContext>.ChildContent), (RenderFragment)(inner =>
            {
                inner.OpenComponent<TaskOrderPanel>(0);
                inner.AddComponentParameter(1, "Subject", new TaskSubject(open));
                inner.AddComponentParameter(2, "TabId", "t1");
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        });

    private static string DotClass(IElement item) =>
        string.Join(' ', item.QuerySelectorAll(".mud-timeline-item-dot-inner").SelectMany(static dot => dot.ClassList).Where(static c => c.StartsWith("mud-timeline-dot-", StringComparison.Ordinal)));

    /// <summary>With the rule on, only the first Task of a Project is Next and counted; the second says it waits for it, in a Default dot.</summary>
    [Fact]
    public async Task RuleOn_OneTaskPerProjectIsNextAndCounted_TheOtherWaits()
    {
        using TaskToolHarness harness = new();
        TeamRuleOn(harness);
        Seed(harness, Make(1, "First", "API", 1), Make(2, "Second", "API", 2), Make(3, "Other project", "Web", 3));
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        SidebarPanelContext panel = new();

        IRenderedComponent<ContainerFragment> cut = Render(ctx, panel, new TaskId("PLAT", 1));

        Assert.Equal(2, panel.Count);
        Assert.Equal(
            ["PLAT-0001 · Next", "PLAT-0002 · waits for PLAT-0001 in API", "PLAT-0003"],
            cut.FindAll(Item + " .task-order-meta").Select(static e => e.TextContent.Trim()));
        Assert.Equal(
            ["mud-timeline-dot-primary", "mud-timeline-dot-default", "mud-timeline-dot-default"],
            cut.FindAll(Item).Select(static item => DotClass(item)));
    }

    /// <summary>With nothing setting the rule, the same Tasks give today's panel: both count and no Task waits.</summary>
    [Fact]
    public async Task RuleOff_IsTodaysPanel()
    {
        using TaskToolHarness harness = new();
        Seed(harness, Make(1, "First", "API", 1), Make(2, "Second", "API", 2));
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        SidebarPanelContext panel = new();

        IRenderedComponent<ContainerFragment> cut = Render(ctx, panel, new TaskId("PLAT", 1));

        Assert.Equal(2, panel.Count);
        Assert.Equal(["PLAT-0001 · Next", "PLAT-0002"], cut.FindAll(Item + " .task-order-meta").Select(static e => e.TextContent.Trim()));
    }

    /// <summary>A Project set to Parallel over a Team set to one at a time counts both of its Tasks.</summary>
    [Fact]
    public async Task ProjectOffOverTeamOn_CountsBothTasks()
    {
        using TaskToolHarness harness = new();
        TeamRuleOn(harness);
        harness.Folders.ProjectFiles["API"] = ProjectFile.Empty with { OneInProgressPerProject = RuleSetting.Off };
        Seed(harness, Make(1, "First", "API", 1), Make(2, "Second", "API", 2));
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        SidebarPanelContext panel = new();

        _ = Render(ctx, panel, new TaskId("PLAT", 1));

        Assert.Equal(2, panel.Count);
    }
}
