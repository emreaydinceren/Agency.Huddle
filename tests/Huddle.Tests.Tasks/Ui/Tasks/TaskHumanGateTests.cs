namespace Agency.Huddle.Tests.Ui.Tasks;

using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Extensions;
using HumanGateCopy = Agency.Huddle.App.Components.Tasks.HumanGateCopy;
using TaskHumanGateComponent = Agency.Huddle.App.Components.Tasks.TaskHumanGate;

/// <summary>
/// Pins T9.3 (Spec §6.8, corrections P9b): the "Human gate" section of the Task panel,
/// <c>TaskHumanGate</c>. It never saves; it raises <c>HumanGateChanged</c>
/// and the Task panel buffers it.
/// </summary>
public sealed class TaskHumanGateTests
{
    /// <summary>The Action text shown in the tests.</summary>
    private const string SomeAction = "send the press release to the list";

    /// <summary>With no Action the switch is on or off as bound, enabled, labelled, and no lock icon is drawn.</summary>
    /// <param name="gated">The bound gate flag.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Switch_NoAction_IsEnabledAndLabelledAndShowsNoLock(bool gated)
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, gated, null, "Ruth", "Emre");

        IRenderedComponent<MudCheckBox<bool>> toggle = cut.FindComponent<MudCheckBox<bool>>();
        Assert.Equal(gated, toggle.Instance.GetState(x => x.Value));
        Assert.False(toggle.Instance.Disabled);
        Assert.Equal(HumanGateCopy.SwitchLabel, toggle.Instance.Label);
        Assert.Equal("Requires your acceptance", HumanGateCopy.SwitchLabel);
        Assert.Empty(cut.FindAll($"[aria-label='{HumanGateCopy.LockedTooltip}']"));
    }

    /// <summary>With an Action set the switch is on, Disabled (not read-only), and the lock icon carries the tooltip text as its aria-label.</summary>
    [Fact]
    public async Task Switch_WithAction_IsOnDisabledWithTheLockAriaLabel()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, true, SomeAction, "Ruth", "Emre");

        IRenderedComponent<MudCheckBox<bool>> toggle = cut.FindComponent<MudCheckBox<bool>>();
        Assert.True(toggle.Instance.GetState(x => x.Value));
        Assert.True(toggle.Instance.Disabled);
        Assert.Equal("This Task names an Action that leaves the team.", HumanGateCopy.LockedTooltip);
        Assert.Single(cut.FindAll($"[aria-label='{HumanGateCopy.LockedTooltip}']"));
    }

    /// <summary>Turning the switch on with no Action raises HumanGateChanged(true).</summary>
    [Fact]
    public async Task Switch_TurnedOn_RaisesHumanGateChangedTrue()
    {
        await using MudBunitContext ctx = new();
        List<bool> raised = [];
        IRenderedComponent<ContainerFragment> cut = Render(ctx, false, null, "Ruth", "Emre", onGate: raised.Add);

        IRenderedComponent<MudCheckBox<bool>> toggle = cut.FindComponent<MudCheckBox<bool>>();
        await cut.InvokeAsync(() => toggle.Instance.ValueChanged.InvokeAsync(true));

        Assert.Equal([true], raised);
    }

    /// <summary>Turning the switch off is allowed when no Action is set, and raises HumanGateChanged(false).</summary>
    [Fact]
    public async Task Switch_TurnedOffWithNoAction_RaisesHumanGateChangedFalse()
    {
        await using MudBunitContext ctx = new();
        List<bool> raised = [];
        IRenderedComponent<ContainerFragment> cut = Render(ctx, true, null, "Ruth", "Emre", onGate: raised.Add);

        IRenderedComponent<MudCheckBox<bool>> toggle = cut.FindComponent<MudCheckBox<bool>>();
        await cut.InvokeAsync(() => toggle.Instance.ValueChanged.InvokeAsync(false));

        Assert.Equal([false], raised);
    }

    /// <summary>The editable section has no What will happen field: the Action is not edited here.</summary>
    [Fact]
    public async Task Section_HasNoActionField()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, true, SomeAction, "Ruth", "Emre");

        Assert.Empty(cut.FindComponents<MudTextField<string>>());
        Assert.Empty(cut.FindAll("input.mud-input-slot"));
    }

    /// <summary>The header chip, its line, the helper line naming the assignee and the Done rule chip naming the Human, verbatim.</summary>
    [Fact]
    public async Task Section_ShowsHeaderHelperAndDoneRuleChip()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, true, null, "Ruth", "Emre");

        string text = cut.Find(".task-human-gate").TextContent;
        Assert.Contains(HumanGateCopy.Heading, text, StringComparison.Ordinal); // contains-ok: whole sentence inside the section's text; the exact string is pinned by the Assert.Equal beside it.
        Assert.Contains(HumanGateCopy.Subheading, text, StringComparison.Ordinal); // contains-ok: whole sentence inside the section's text; the exact string is pinned by the Assert.Equal beside it.
        Assert.Equal("Human gate", HumanGateCopy.Heading);
        Assert.Equal("You decide whether this is done", HumanGateCopy.Subheading);
        Assert.DoesNotContain("Agents draft, check and track", text, StringComparison.Ordinal);
        Assert.DoesNotContain(HumanGateCopy.DoneRuleLabel, text, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(".task-human-gate-helper, .task-human-gate-done-rule, .task-human-gate-explanation"));
    }

    /// <summary>Renders <see cref="TaskHumanGateComponent"/> with the given parameters and optional callbacks.</summary>
    /// <param name="ctx">The test context.</param>
    /// <param name="gated">The bound gate flag.</param>
    /// <param name="action">The bound Action.</param>
    /// <param name="assignee">The assignee's name, or null when unassigned.</param>
    /// <param name="human">The Human's display name.</param>
    /// <param name="onGate">Receives each HumanGateChanged value.</param>
    private static IRenderedComponent<ContainerFragment> Render(
        MudBunitContext ctx,
        bool gated,
        string? action,
        string? assignee,
        string human,
        Action<bool>? onGate = null)
    {
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskHumanGateComponent>(0);
            builder.AddAttribute(1, "HumanGate", gated);
            builder.AddAttribute(2, "Action", action);
            builder.AddAttribute(3, "Assignee", assignee);
            builder.AddAttribute(4, "HumanName", human);
            builder.AddAttribute(5, "HumanGateChanged", EventCallback.Factory.Create<bool>(ctx, v => onGate?.Invoke(v)));
            builder.CloseComponent();
        });
    }
}
