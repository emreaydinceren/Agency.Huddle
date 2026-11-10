using AngleSharp.Dom;
using Agency.Huddle.App.Components;
using Agency.Huddle.App.Components.Shared;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Tests for the one <c>Chip</c> (rule-reduction D12): each look wears the classes the one stylesheet block per look keys on, and a Filter chip carries its set state, menu and clear button as content of the same chip.</summary>
public sealed class ChipTests
{
    /// <summary>A Status chip is a MudChip wearing <c>status-chip</c> and its colour's class, and a Tag chip wears <c>tag-chip</c>; the caller's class comes last.</summary>
    /// <param name="look">The look to draw.</param>
    /// <param name="expected">The classes the look adds, before the caller's.</param>
    [Theory]
    [InlineData(ChipLook.Status, "status-chip status-chip-warning mine")]
    [InlineData(ChipLook.Tag, "tag-chip mine")]
    public async Task Render_StatusAndTag_WearTheirLookClasses(ChipLook look, string expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        await using MudBunitContext ctx = new();

        IRenderedComponent<Chip<string>> cut = ctx.Render<Chip<string>>(p => p
            .Add(c => c.Look, look).Add(c => c.Color, Color.Warning).Add(c => c.Class, "mine").Add(c => c.Text, "High"));

        IElement chip = cut.Find(".mud-chip");
        string[] ours = [.. chip.ClassList.Where(static c => !c.StartsWith("mud-", StringComparison.Ordinal))];
        Assert.Equal(expected.Split(' '), ours);
        Assert.Equal("High", chip.TextContent.Trim());
    }

    /// <summary>A Filter chip is the same MudChip with <c>filter-chip</c>, plus <c>is-set</c> once a choice is made.</summary>
    [Fact]
    public async Task Render_Filter_IsSetAddsTheSetClass()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<Chip<string>> unset = ctx.Render<Chip<string>>(p => p.Add(c => c.Look, ChipLook.Filter).Add(c => c.Text, "Me"));
        IRenderedComponent<Chip<string>> set = ctx.Render<Chip<string>>(p => p.Add(c => c.Look, ChipLook.Filter).Add(c => c.IsSet, true).Add(c => c.Text, "Me"));

        Assert.Contains("filter-chip", unset.Find(".mud-chip").ClassList);
        Assert.DoesNotContain("is-set", unset.Find(".mud-chip").ClassList);
        Assert.Contains("is-set", set.Find(".mud-chip").ClassList);
    }

    /// <summary>The clear button is drawn only once the Filter chip is set and has a handler, carries the caller's accessible name and raises <c>OnClear</c>.</summary>
    [Fact]
    public async Task Click_FilterClear_ShowsOnlyWhenSet_AndRaisesOnClear()
    {
        await using MudBunitContext ctx = new();
        int cleared = 0;
        Action<ComponentParameterCollectionBuilder<Chip<string>>> parameters(bool isSet) => p => p
            .Add(c => c.Look, ChipLook.Filter).Add(c => c.IsSet, isSet).Add(c => c.Text, "Me").Add(c => c.ClearLabel, "Clear Assignee filter")
            .Add(c => c.OnClear, () => cleared++);

        IRenderedComponent<Chip<string>> unset = ctx.Render(parameters(false));
        IRenderedComponent<Chip<string>> set = ctx.Render(parameters(true));
        await set.InvokeAsync(() => set.Find("[aria-label='Clear Assignee filter']").ClickAsync());

        Assert.Empty(unset.FindAll(".chip-clear"));
        Assert.Equal(1, cleared);
    }

    /// <summary>With <c>MenuContent</c> the words become the menu's trigger, a button with a caret and the caller's accessible name, inside the one chip.</summary>
    [Fact]
    public async Task Render_FilterWithMenu_DrawsTheTriggerInsideTheChip()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<Chip<string>>(0);
            builder.AddAttribute(1, nameof(Chip<string>.Look), ChipLook.Filter);
            builder.AddAttribute(2, nameof(Chip<string>.TriggerLabel), "Assignee filter: Any");
            builder.AddAttribute(3, nameof(Chip<string>.ChildContent), (RenderFragment)(content => content.AddContent(0, "Assignee: Any")));
            builder.AddAttribute(4, nameof(Chip<string>.MenuContent), (RenderFragment)(menu => menu.AddMarkupContent(0, "<span class=\"choice\">Me</span>")));
            builder.CloseComponent();
        });

        IElement trigger = cut.Find(".mud-chip.filter-chip .chip-menu .chip-trigger");
        Assert.Equal("Assignee filter: Any", trigger.GetAttribute("aria-label"));
        Assert.Equal("Assignee: Any", string.Join(' ', trigger.TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
        Assert.NotEmpty(trigger.QuerySelectorAll(".mud-icon-root"));
    }
}
