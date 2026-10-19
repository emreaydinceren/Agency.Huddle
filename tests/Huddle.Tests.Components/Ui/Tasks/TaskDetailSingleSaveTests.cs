using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;
using static Agency.Huddle.Tests.Ui.Tasks.TaskDetailFieldsTestSupport;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>
/// Pins the Task detail's actions and dialog (U15 T15.2 and T15.3): one Filled Save with Cancel before it
/// and never a second Save inside the "Unsaved edits" alert (UX-049, UX-020, UX-021), a Close in place of
/// the dead Save on a deleted Task (UX-V27), <c>Disabled</c> instead of <c>ReadOnly</c> on the conflict
/// fields (UX-V04), one <c>Actions for {id}</c> menu (UX-050a), and the dialog titled with what the caller
/// passed - <c>Id · Title</c> for an expanded Task, <c>Copy of …</c> for a copy - with the dialog's own
/// close button and no second id row (UX-V33).
/// </summary>
public sealed class TaskDetailSingleSaveTests
{
    /// <summary>With or without edits, the Save is exactly one Filled button in the actions row, in both layouts.</summary>
    /// <param name="mode">Which layout renders.</param>
    /// <param name="edited">Whether a Priority edit is pending.</param>
    [Theory]
    [InlineData(TaskDetailMode.Panel, false)]
    [InlineData(TaskDetailMode.Panel, true)]
    [InlineData(TaskDetailMode.Expanded, false)]
    [InlineData(TaskDetailMode.Expanded, true)]
    public async Task Save_WithOrWithoutEdits_IsOneFilledButtonInTheActionsRow(TaskDetailMode mode, bool edited)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Medium);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderDetail(ctx, task.Id, mode);
        if (edited)
        {
            await EditPriorityAsync(cut, TaskPriority.Urgent);
        }

        IElement save = Assert.Single(cut.FindAll("button"), button => string.Equals(button.TextContent.Trim(), "Save", StringComparison.Ordinal));

        Assert.NotNull(save.Closest(".task-detail-actions"));
        Assert.Contains("mud-button-filled", save.ClassList);
    }

    /// <summary>Cancel comes before Save in the actions row, with or without edits.</summary>
    /// <param name="edited">Whether a Priority edit is pending.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actions_CancelComesBeforeSave(bool edited)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Medium);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);
        if (edited)
        {
            await EditPriorityAsync(cut, TaskPriority.Urgent);
        }

        IReadOnlyList<string> labels = [.. cut.FindAll(".task-detail-actions button").Select(button => button.TextContent.Trim()).Where(text => text.Length > 0)];

        Assert.Equal(["Cancel", "Save"], labels);
    }

    /// <summary>The "Unsaved edits" alert offers Revert only: Save lives in the actions row.</summary>
    [Fact]
    public async Task UnsavedAlert_HoldsRevertAndNoSave()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, priority: TaskPriority.Medium);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);
        await EditPriorityAsync(cut, TaskPriority.Urgent);

        IReadOnlyList<string> labels = [.. cut.FindAll(".task-detail-unsaved button").Select(button => button.TextContent.Trim())];

        Assert.Equal(["Revert"], labels);
    }

    /// <summary>A deleted Task shows its notice with an enabled Close, no Save, and Close raises <c>OnClose</c>.</summary>
    [Fact]
    public async Task DeletedTask_OffersAnEnabledClose_AndNoSave()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        bool closed = false;
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderDetailWithClose(ctx, task.Id, TaskDetailMode.Panel, EventCallback.Factory.Create(this, () => closed = true));

        File.Delete(task.Path);
        harness.Store.RebuildFromWatcher();

        cut.WaitForAssertion(() => Assert.Equal("This Task was deleted", TextOf(cut, ".task-detail-deleted")));
        Assert.DoesNotContain(cut.FindAll("button"), button => string.Equals(button.TextContent.Trim(), "Save", StringComparison.Ordinal));
        IElement close = FindButton(cut, "Close");
        Assert.False(close.HasAttribute("disabled"));
        await cut.InvokeAsync(() => close.ClickAsync());
        Assert.True(closed);
    }

    /// <summary>The conflict's Description boxes are inert through <c>Disabled</c>, never <c>ReadOnly</c>, in both layouts.</summary>
    /// <param name="mode">Which layout renders.</param>
    [Theory]
    [InlineData(TaskDetailMode.Panel)]
    [InlineData(TaskDetailMode.Expanded)]
    public async Task ConflictDescription_FieldsAreDisabled_NotReadOnly(TaskDetailMode mode)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderDetail(ctx, task.Id, mode);
        IRenderedComponent<MudTextField<string>> description = cut.FindComponents<MudTextField<string>>().Single(m => m.Instance.Class?.Contains("task-detail-description-field", StringComparison.Ordinal) is true);
        await cut.InvokeAsync(() => description.Instance.ValueChanged.InvokeAsync("Mine: some plan."));
        _ = harness.Service.Update(task.Id, new TaskPatch { Description = "Theirs: a different plan." }, baseVersion: null, new TaskActor(TaskActorKind.Agent, "Nova", null));

        await cut.InvokeAsync(() => FindButton(cut, "Save").ClickAsync());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".task-detail-conflict-row")));
        string[] sides = ["task-detail-conflict-theirs", "task-detail-conflict-yours"];
        foreach (string side in sides)
        {
            IRenderedComponent<MudTextField<string>> box = cut.FindComponents<MudTextField<string>>().Single(m => string.Equals(m.Instance.Class, side, StringComparison.Ordinal));
            Assert.True(box.Instance.Disabled);
            Assert.False(box.Instance.ReadOnly);
        }
    }

    /// <summary>The detail's menu is the shared <c>Actions for {id}</c> one, holding Copy id, Copy link, Make a copy and Close task.</summary>
    [Fact]
    public async Task ActionsMenu_IsActionsForTheId_WithTheSharedItemsFirst()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);

        Assert.Empty(cut.FindAll("button[aria-label=\"More actions\"]"));
        await cut.InvokeAsync(() => cut.Find($"button[aria-label=\"Actions for {task.Id}\"]").ClickAsync());

        IReadOnlyList<string> items = [.. cut.FindAll(".mud-menu-item").Select(item => item.TextContent.Trim())];
        Assert.Equal(["Copy id", "Copy link", "Make a copy…", "Close task…"], items);
    }

    /// <summary><see cref="TaskActionsMenu"/> puts its extra items (the detail's Make a copy and Close task) after Copy link.</summary>
    [Fact]
    public async Task TaskActionsMenu_ExtraItems_FollowCopyLink()
    {
        await using MudBunitContext ctx = new();
        TaskId id = new("PLAT", 1);
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskActionsMenu>(0);
            builder.AddAttribute(1, nameof(TaskActionsMenu.Id), id);
            builder.AddAttribute(2, nameof(TaskActionsMenu.ExtraItems), (RenderFragment)(items =>
            {
                items.OpenComponent<MudMenuItem>(0);
                items.AddAttribute(1, nameof(MudMenuItem.ChildContent), (RenderFragment)(content => content.AddContent(0, "Make a copy")));
                items.CloseComponent();
            }));
            builder.CloseComponent();
        });

        await cut.InvokeAsync(() => cut.Find($"button[aria-label=\"Actions for {id}\"]").ClickAsync());

        IReadOnlyList<string> labels = [.. cut.FindAll(".mud-menu-item").Select(item => item.TextContent.Trim())];
        Assert.Equal(["Copy id", "Copy link", "Make a copy"], labels);
    }

    /// <summary>Expand titles the dialog <c>Id · Title</c>, shows the dialog's own close button, and leaves the id row out of the body.</summary>
    [Fact]
    public async Task Expand_TitlesTheDialogIdDotTitle_WithCloseButtonAndNoSecondId()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, title: "Ship the thing");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);

        await cut.InvokeAsync(() => cut.Find(".task-detail-expand").ClickAsync());

        IRenderedComponent<TaskDetailDialog> dialog = cut.FindComponent<TaskDetailDialog>();
        Assert.Equal($"{task.Id} · Ship the thing", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.True(TaskDetailDialog.Options.CloseButton);
        Assert.Empty(dialog.FindAll(".task-detail-title-display .task-id"));
        Assert.Equal("Ship the thing", dialog.Find(".task-detail-title-display").TextContent.Trim());
    }

    /// <summary>Make a copy opens the dialog titled <c>Copy of …</c>, not "New task".</summary>
    [Fact]
    public async Task MakeACopy_TitlesTheDialogWithThePassedTitle()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, title: "Ship the thing");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);

        await cut.InvokeAsync(() => cut.Find($"button[aria-label=\"Actions for {task.Id}\"]").ClickAsync());
        IElement item = cut.FindAll(".mud-menu-item").Single(element => string.Equals(element.TextContent.Trim(), "Make a copy…", StringComparison.Ordinal));
        await cut.InvokeAsync(() => item.ClickAsync());

        Assert.Equal("Copy of Ship the thing", cut.Find(".mud-dialog-title").TextContent.Trim());
    }

    /// <summary>The Assignee picker's Persona option is a <see cref="TaskAssignee"/>: unknown presence draws no dot and never the word "Offline".</summary>
    [Fact]
    public async Task AssigneePickerOption_IsATaskAssignee_WithNoOfflineGuess()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);
        IRenderedComponent<MudAutocomplete<AssigneeOption>> assignee = cut.FindComponents<MudAutocomplete<AssigneeOption>>().Single(m => string.Equals(m.Instance.Class, "task-detail-assignee", StringComparison.Ordinal));
        RenderFragment<AssigneeOption> itemTemplate = assignee.Instance.ItemTemplate ?? throw new InvalidOperationException("The Assignee picker has no ItemTemplate.");
        AssigneeOption nova = new("Nova", "Engineer", null);

        var item = ctx.Render(itemTemplate(nova));

        Assert.Equal("Nova", item.FindComponent<TaskAssignee>().Instance.Name);
        Assert.Null(item.FindComponent<TaskAssignee>().Instance.Presence);
        Assert.Empty(item.FindComponents<MudBadge>());
        Assert.DoesNotContain("Offline", item.Markup, StringComparison.Ordinal);
    }

    /// <summary>Renders <c>TaskDetail</c> for <paramref name="id"/> with an <c>OnClose</c> callback.</summary>
    /// <param name="ctx">The bUnit context.</param>
    /// <param name="id">The Task.</param>
    /// <param name="mode">Which layout renders.</param>
    /// <param name="onClose">Raised by Cancel and Close.</param>
    private static IRenderedComponent<ContainerFragment> RenderDetailWithClose(MudBunitContext ctx, TaskId id, TaskDetailMode mode, EventCallback onClose)
    {
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskDetail>(0);
            builder.AddAttribute(1, nameof(TaskDetail.Id), (TaskId?)id);
            builder.AddAttribute(2, nameof(TaskDetail.Mode), mode);
            builder.AddAttribute(3, nameof(TaskDetail.OnClose), onClose);
            builder.CloseComponent();
        });
    }

    /// <summary>Raises the Priority <c>MudSelect</c>'s <c>ValueChanged</c> for <paramref name="priority"/>, leaving one pending edit.</summary>
    /// <param name="cut">The rendered detail.</param>
    /// <param name="priority">The new Priority.</param>
    private static async Task EditPriorityAsync(IRenderedComponent<ContainerFragment> cut, TaskPriority priority)
    {
        IRenderedComponent<MudSelect<TaskPriority>> select = cut.FindComponents<MudSelect<TaskPriority>>().Single(m => string.Equals(m.Instance.Class, "task-detail-priority", StringComparison.Ordinal));
        await cut.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(priority));
    }
}
