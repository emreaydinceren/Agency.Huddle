namespace Agency.Huddle.Tests.Ui.Tasks;

using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using MudBlazor;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Shared theory data, fixtures and helpers for <see cref="TaskDetailFieldsTests"/>, <see cref="TaskDetailAssigneeTests"/>
/// and <see cref="TaskDetailPlacementTests"/>.
/// </summary>
internal static class TaskDetailFieldsTestSupport
{
    /// <summary>The two Won't-do states that show a Reason field.</summary>
    public static IEnumerable<object[]> WontDoReasonStates()
    {
        yield return [TaskState.Cancelled];
        yield return [TaskState.Rejected];
    }

    /// <summary>One vs. two open blockers (singular vs. plural summary text).</summary>
    public static IEnumerable<object[]> BlockerCounts()
    {
        yield return [1];
        yield return [2];
    }

    /// <summary>A terminal vs. non-terminal blocker Status (J46: the check icon only for terminal).</summary>
    public static IEnumerable<object[]> BlockerTerminalStates()
    {
        yield return [TaskState.Done, true];
        yield return [TaskState.InProgress, false];
    }

    /// <summary>The Start and Due date fields.</summary>
    public static IEnumerable<object[]> DateFields()
    {
        yield return ["start"];
        yield return ["due"];
    }

    /// <summary>Creates a Task through <see cref="TaskService"/> with every field this file's tests need to control.</summary>
    internal static TaskItem CreateTask(
        TaskToolHarness harness,
        string title = "T",
        TaskState status = TaskState.ToDo,
        TaskPriority priority = TaskPriority.Medium,
        string? assignee = null,
        string? originRoomId = null,
        IReadOnlyList<TaskId>? blockedBy = null,
        DateOnly? dueDate = null)
    {
        TaskResult result = harness.Service.Create(
            new TaskDraft(title, "Platform", null, Status: status, Priority: priority, Assignee: assignee, OriginRoomId: originRoomId, BlockedBy: blockedBy, DueDate: dueDate),
            TaskActors.Human(harness.Options.Value));
        return Assert.IsType<TaskResult.Saved>(result).Task;
    }

    /// <summary>Renders <c>TaskDetail</c> for <paramref name="id"/>, inside <see cref="MudBunitContext.RenderWithPopovers"/> (the Won't-do menu and pickers are popovers).</summary>
    internal static IRenderedComponent<ContainerFragment> RenderDetail(MudBunitContext ctx, TaskId id)
    {
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskDetail>(0);
            builder.AddAttribute(1, nameof(TaskDetail.Id), (TaskId?)id);
            builder.CloseComponent();
        });
    }

    /// <summary>Finds the one <c>&lt;button&gt;</c> whose trimmed text is exactly <paramref name="text"/>.</summary>
    internal static IElement FindButton(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.FindAll("button").Single(b => string.Equals(b.TextContent.Trim(), text, StringComparison.Ordinal));

    /// <summary>The trimmed text content of the one element matching <paramref name="cssSelector"/>.</summary>
    internal static string TextOf(IRenderedComponent<ContainerFragment> cut, string cssSelector) =>
        cut.Find(cssSelector).TextContent.Trim();

    /// <summary>
    /// Raises the Status <c>MudSelect</c>'s <c>ValueChanged</c> for <paramref name="state"/> - the
    /// <c>TaskDetailConflictTests.EditTeamAsync</c> pattern (invoke the component's own callback
    /// directly rather than driving the popover's DOM), which now covers every <see cref="TaskState"/>
    /// alike since Status stopped being a toggle group plus a separate Won't-do menu.
    /// </summary>
    internal static async Task SetStatusAsync(IRenderedComponent<ContainerFragment> cut, TaskState state)
    {
        IRenderedComponent<MudSelect<TaskState>> select = cut.FindComponents<MudSelect<TaskState>>().Single(m => string.Equals(m.Instance.Class, "task-detail-status", StringComparison.Ordinal));
        await cut.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(state));
    }

    /// <summary>
    /// Closes the <c>MudChip{T}</c> whose <c>Value</c> equals <paramref name="value"/> by invoking that
    /// real rendered chip's own <c>OnClose</c> callback with itself - the same event <c>MudChipSet</c>'s
    /// close icon would raise. Never constructs a <c>MudChip{T}</c> by hand (MudBlazor's own analyzer,
    /// <c>BL0005</c>, forbids setting a component parameter outside its component) and does not depend on
    /// the close icon's exact DOM shape, which bUnit's static render does not reproduce identically to a
    /// live browser.
    /// </summary>
    /// <typeparam name="T">The chip's value type.</typeparam>
    /// <param name="cut">The rendered <c>TaskDetail</c>.</param>
    /// <param name="value">The <c>Value</c> of the chip to close.</param>
    internal static async Task CloseChipAsync<T>(IRenderedComponent<ContainerFragment> cut, T value)
    {
        IRenderedComponent<MudChip<T>> chip = cut.FindComponents<MudChip<T>>().Single(c => Equals(c.Instance.Value, value));
        await cut.InvokeAsync(() => chip.Instance.OnClose.InvokeAsync(chip.Instance));
    }

    /// <summary>Awaits <paramref name="autocomplete"/>'s own <c>SearchFunc</c> for <paramref name="query"/>, then invokes <c>ValueChanged</c> with the first result - never constructing a <typeparamref name="T"/> by hand.</summary>
    /// <typeparam name="T">The autocomplete's item type.</typeparam>
    internal static async Task<T> SearchAndPickFirst<T>(IRenderedComponent<ContainerFragment> cut, IRenderedComponent<MudAutocomplete<T>> autocomplete, string query)
    {
        Func<string?, CancellationToken, Task<IEnumerable<T>>?> search = autocomplete.Instance.SearchFunc
            ?? throw new InvalidOperationException("Autocomplete has no SearchFunc.");
        Task<IEnumerable<T>>? searchTask = search(query, Xunit.TestContext.Current.CancellationToken);
        if (searchTask is null)
        {
            throw new InvalidOperationException("SearchFunc returned null.");
        }

        T first = (await searchTask).First();
        await cut.InvokeAsync(() => autocomplete.Instance.ValueChanged.InvokeAsync(first));
        return first;
    }
}
