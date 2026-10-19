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
/// <c>TaskHumanGate</c>. It never saves; it raises <c>HumanGateChanged</c> and <c>ActionChanged</c>
/// and the Task panel buffers them.
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

        IRenderedComponent<MudSwitch<bool>> toggle = cut.FindComponent<MudSwitch<bool>>();
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

        IRenderedComponent<MudSwitch<bool>> toggle = cut.FindComponent<MudSwitch<bool>>();
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

        IRenderedComponent<MudSwitch<bool>> toggle = cut.FindComponent<MudSwitch<bool>>();
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

        IRenderedComponent<MudSwitch<bool>> toggle = cut.FindComponent<MudSwitch<bool>>();
        await cut.InvokeAsync(() => toggle.Instance.ValueChanged.InvokeAsync(false));

        Assert.Equal([false], raised);
    }

    /// <summary>The What will happen field carries its label and the "Nothing leaves the team" placeholder, and shows the Action.</summary>
    [Fact]
    public async Task ActionField_HasLabelPlaceholderAndValue()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, true, SomeAction, "Ruth", "Emre");

        IRenderedComponent<MudTextField<string>> field = cut.FindComponent<MudTextField<string>>();
        Assert.Equal(HumanGateCopy.ActionLabel, field.Instance.Label);
        Assert.Equal("What will happen", HumanGateCopy.ActionLabel);
        Assert.Equal(HumanGateCopy.ActionPlaceholder, field.Instance.Placeholder);
        Assert.Equal("Nothing leaves the team", HumanGateCopy.ActionPlaceholder);
        Assert.Equal(1, field.Instance.Lines);
        Assert.Equal(SomeAction, cut.Find("input.mud-input-slot").GetAttribute("value"));
    }

    /// <summary>Typing in the What will happen field raises ActionChanged with the new text.</summary>
    [Fact]
    public async Task ActionField_Typed_RaisesActionChanged()
    {
        await using MudBunitContext ctx = new();
        List<string?> raised = [];
        IRenderedComponent<ContainerFragment> cut = Render(ctx, false, null, "Ruth", "Emre", onAction: raised.Add);

        IRenderedComponent<MudTextField<string>> field = cut.FindComponent<MudTextField<string>>();
        await cut.InvokeAsync(() => field.Instance.ValueChanged.InvokeAsync(SomeAction));

        Assert.Equal([SomeAction], raised);
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
        Assert.Contains(HumanGateCopy.Helper("Ruth"), text, StringComparison.Ordinal); // contains-ok: whole sentence inside the section's text; the exact string is pinned by the Assert.Equal beside it.
        Assert.Equal("Ruth can move it to Needs you once every line has Proof. Teammates can't mark it Done.", HumanGateCopy.Helper("Ruth"));
        Assert.Contains(HumanGateCopy.DoneRuleLabel, text, StringComparison.Ordinal); // contains-ok: whole sentence inside the section's text; the exact string is pinned by the Assert.Equal beside it.
        Assert.Equal("Can move to Done", HumanGateCopy.DoneRuleLabel);
        Assert.Contains(
            cut.FindComponents<MudChip<string>>(),
            c => string.Equals(c.Find(".mud-chip").TextContent.Trim(), HumanGateCopy.DoneRuleChip("Emre"), StringComparison.Ordinal));
        Assert.Equal("Emre only", HumanGateCopy.DoneRuleChip("Emre"));
    }

    /// <summary>Without an Action the explanation says nothing is sent or published (Spec §6.8, verbatim).</summary>
    [Fact]
    public async Task Explanation_NoAction_SaysNothingIsSent()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, true, null, "Ruth", "Emre");

        string text = cut.Find(".task-human-gate").TextContent;
        const string expected = "Teammates draft, check and track: you decide. Accepting marks it Done; nothing is sent or published. Done also needs the team's Proof to be complete.";
        Assert.Equal(expected, HumanGateCopy.ExplanationNoAction);
        Assert.Contains(HumanGateCopy.ExplanationNoAction, text, StringComparison.Ordinal); // contains-ok: whole sentence inside the section's text; the exact string is pinned by the Assert.Equal beside it.
    }

    /// <summary>With an Action the second sentence becomes "Accepting lets &lt;assignee&gt; &lt;action&gt;."</summary>
    [Fact]
    public async Task Explanation_WithAction_NamesTheAssigneeAndTheAction()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, true, SomeAction, "Ruth", "Emre");

        string text = cut.Find(".task-human-gate").TextContent;
        const string expected = "Teammates draft, check and track: you decide. Accepting lets Ruth send the press release to the list. Done also needs the team's Proof to be complete.";
        Assert.Equal(expected, HumanGateCopy.Explanation("Ruth", SomeAction));
        Assert.Contains(HumanGateCopy.Explanation("Ruth", SomeAction), text, StringComparison.Ordinal); // contains-ok: whole sentence inside the section's text; the exact string is pinned by the Assert.Equal beside it.
        Assert.DoesNotContain("nothing is sent or published", text, StringComparison.Ordinal);
    }

    /// <summary>An unassigned Task reads "the assignee" in the helper line and in the Action explanation.</summary>
    [Fact]
    public async Task Unassigned_ReadsTheAssignee()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, true, SomeAction, null, "Emre");

        string text = cut.Find(".task-human-gate").TextContent;
        Assert.Equal("the assignee", HumanGateCopy.UnassignedAssignee);
        Assert.Contains("the assignee can move it to Needs you once every line has Proof. Teammates can't mark it Done.", text, StringComparison.Ordinal); // contains-ok: whole sentence inside the section's text; the exact string is pinned by the literal itself.
        Assert.Contains("Accepting lets the assignee send the press release to the list.", text, StringComparison.Ordinal); // contains-ok: whole sentence inside the section's text; the exact string is pinned by the literal itself.
    }

    /// <summary>Renders <see cref="TaskHumanGateComponent"/> with the given parameters and optional callbacks.</summary>
    /// <param name="ctx">The test context.</param>
    /// <param name="gated">The bound gate flag.</param>
    /// <param name="action">The bound Action.</param>
    /// <param name="assignee">The assignee's name, or null when unassigned.</param>
    /// <param name="human">The Human's display name.</param>
    /// <param name="onGate">Receives each HumanGateChanged value.</param>
    /// <param name="onAction">Receives each ActionChanged value.</param>
    private static IRenderedComponent<ContainerFragment> Render(
        MudBunitContext ctx,
        bool gated,
        string? action,
        string? assignee,
        string human,
        Action<bool>? onGate = null,
        Action<string?>? onAction = null)
    {
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskHumanGateComponent>(0);
            builder.AddAttribute(1, "HumanGate", gated);
            builder.AddAttribute(2, "Action", action);
            builder.AddAttribute(3, "Assignee", assignee);
            builder.AddAttribute(4, "HumanName", human);
            builder.AddAttribute(5, "HumanGateChanged", EventCallback.Factory.Create<bool>(ctx, v => onGate?.Invoke(v)));
            builder.AddAttribute(6, "ActionChanged", EventCallback.Factory.Create<string?>(ctx, v => onAction?.Invoke(v)));
            builder.CloseComponent();
        });
    }
}
