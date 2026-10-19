using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;
using static Agency.Huddle.Tests.Ui.Tasks.TaskDetailFieldsTestSupport;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>
/// Pins <see cref="TaskPicker"/>, the one "pick another Task" field (UX-R28): Outlined and Dense, strict,
/// one debounce, one "{Id} · {Title}" text, the Task being edited left out, and the choice raised as an id.
/// Both the Duplicate-of field in the detail and the Board's duplicate dialog must use it.
/// </summary>
public sealed class TaskPickerTests
{
    /// <summary>The field's contract: label, outlined and dense, strict, the shared search debounce and the "{Id} · {Title}" text.</summary>
    [Fact]
    public async Task Renders_AStrictOutlinedDenseAutocomplete_WithTheSharedTextAndDebounce()
    {
        using TaskToolHarness harness = new();
        TaskItem alpha = CreateTask(harness, "Alpha plan");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = RenderPicker(ctx, excluding: null, onChosen: static _ => { });

        MudAutocomplete<TaskItem> field = cut.FindComponent<MudAutocomplete<TaskItem>>().Instance;
        Assert.Equal("Duplicate of", field.Label);
        Assert.Equal(Variant.Outlined, field.Variant);
        Assert.Equal(Margin.Dense, field.Margin);
        Assert.True(field.Strict);
        Assert.Equal(SearchField.DebounceMs, field.DebounceInterval);
        Func<TaskItem?, string?> text = field.ToStringFunc ?? throw new InvalidOperationException("No ToStringFunc.");
        Assert.Equal($"{alpha.Id} · Alpha plan", text(alpha));
    }

    /// <summary>The search lists matching Tasks but never the excluded one, and choosing one raises its id.</summary>
    [Fact]
    public async Task Search_LeavesOutTheExcludedTask_AndChoosingRaisesTheId()
    {
        using TaskToolHarness harness = new();
        TaskItem alpha = CreateTask(harness, "Alpha plan");
        TaskItem beta = CreateTask(harness, "Beta plan");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        TaskId? chosen = null;
        IRenderedComponent<ContainerFragment> cut = RenderPicker(ctx, excluding: alpha.Id, onChosen: id => chosen = id);
        IRenderedComponent<MudAutocomplete<TaskItem>> field = cut.FindComponent<MudAutocomplete<TaskItem>>();

        TaskItem first = await SearchAndPickFirst(cut, field, "plan");

        Assert.Equal(beta.Id, first.Id);
        Assert.Equal(beta.Id, chosen);
    }

    /// <summary>The Duplicate-of field in the detail is the shared picker.</summary>
    [Fact]
    public async Task Detail_DuplicateOf_IsATaskPicker()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);

        await SetStatusAsync(cut, TaskState.Duplicate);

        IRenderedComponent<TaskPicker> picker = cut.FindComponent<TaskPicker>();
        Assert.Equal("Duplicate of", picker.Instance.Label);
        Assert.Equal(task.Id, picker.Instance.Excluding);
    }

    /// <summary>Parent and Add blocker in the detail are the shared picker too, each leaving out the Task itself.</summary>
    [Fact]
    public async Task Detail_ParentAndAddBlocker_AreTaskPickers()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);

        IReadOnlyList<IRenderedComponent<TaskPicker>> pickers = cut.FindComponents<TaskPicker>();

        Assert.Equal(["Parent", "Add blocker"], pickers.Select(picker => picker.Instance.Label));
        Assert.All(pickers, picker => Assert.Equal(task.Id, picker.Instance.Excluding));
    }

    /// <summary>Clearing the Duplicate-of picker clears the pending value, so Save is disabled again.</summary>
    [Fact]
    public async Task Detail_ClearingDuplicateOf_DisablesSaveAgain()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        TaskItem other = CreateTask(harness, "Other");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);
        await SetStatusAsync(cut, TaskState.Duplicate);
        IRenderedComponent<TaskPicker> picker = cut.FindComponent<TaskPicker>();

        await cut.InvokeAsync(() => picker.Instance.ValueChanged.InvokeAsync(other.Id));
        Assert.False(FindButton(cut, "Save").HasAttribute("disabled"));
        await cut.InvokeAsync(() => picker.Instance.ValueChanged.InvokeAsync(null));

        Assert.True(FindButton(cut, "Save").HasAttribute("disabled"));
    }

    /// <summary>A conflict row's Keep mine / Take theirs choice is named after its field.</summary>
    [Fact]
    public async Task ConflictRow_ChoiceGroup_IsNamedAfterItsField()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<ConflictRow> row = ctx.Render<ConflictRow>(p =>
        {
            p.Add(r => r.Field, TaskField.Priority);
            p.Add(r => r.Stacked, true);
        });

        Assert.Equal("priority: keep mine or take theirs", row.Find("[role='radiogroup'], .mud-radio-group").GetAttribute("aria-label"));
    }

    /// <summary>The Board's duplicate dialog is the shared picker, leaving out the moved Task.</summary>
    [Fact]
    public async Task DuplicatePickerDialog_IsATaskPicker()
    {
        using TaskToolHarness harness = new();
        TaskItem moved = CreateTask(harness, "Moved");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        IDialogService dialogs = ctx.Services.GetRequiredService<IDialogService>();
        DialogParameters<DuplicatePickerDialog> parameters = new() { { dialog => dialog.Excluding, moved.Id } };

        _ = await cut.InvokeAsync(() => dialogs.ShowAsync<DuplicatePickerDialog>("Mark as a duplicate", parameters));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<TaskPicker>()));
        Assert.Equal(moved.Id, cut.FindComponent<TaskPicker>().Instance.Excluding);
    }

    /// <summary>Renders <see cref="TaskPicker"/> labelled "Duplicate of".</summary>
    /// <param name="ctx">The bUnit context.</param>
    /// <param name="excluding">The Task left out of the search, if any.</param>
    /// <param name="onChosen">Receives the chosen id.</param>
    private static IRenderedComponent<ContainerFragment> RenderPicker(MudBunitContext ctx, TaskId? excluding, Action<TaskId?> onChosen)
    {
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskPicker>(0);
            builder.AddAttribute(1, nameof(TaskPicker.Label), "Duplicate of");
            builder.AddAttribute(2, nameof(TaskPicker.Excluding), excluding);
            builder.AddAttribute(3, nameof(TaskPicker.ValueChanged), EventCallback.Factory.Create<TaskId?>(ctx, onChosen));
            builder.CloseComponent();
        });
    }
}
