using AngleSharp.Dom;
using Bunit;
using Agency.Huddle.App.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Renders <see cref="ChipRadioGroup{T}"/> on its own: the radiogroup and radio roles, the roving tab stop, the count, a click, and
/// the one keyboard behaviour (plain arrows move and wrap, an arrow with Ctrl does nothing).
/// </summary>
public sealed class ChipRadioGroupTests
{
    private static readonly string[] Choices = ["All", "Needs you", "Mine"];

    /// <summary>Renders the group over <see cref="Choices"/> with "Needs you" chosen, recording each raised choice.</summary>
    private static IRenderedComponent<ChipRadioGroup<string>> RenderIt(MudBunitContext ctx, List<string> raised)
    {
        string chosen = "Needs you";
        IRenderedComponent<ChipRadioGroup<string>>? cut = null;
        cut = ctx.Render<ChipRadioGroup<string>>(parameters => parameters
            .Add(p => p.Items, Choices)
            .Add(p => p.Value, chosen)
            .Add(p => p.Label, "Category")
            .Add(p => p.ItemLabel, static item => item)
            .Add(p => p.ItemCount, static item => string.Equals(item, "Needs you", StringComparison.Ordinal) ? 3 : null)
            .Add(p => p.Class, "caller-hook")
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<string>(ctx, value =>
            {
                raised.Add(value);
                cut?.Render(again => again.Add(p => p.Value, value));
            })));
        return cut;
    }

    /// <summary>The checked chip's words, its count included, with the whitespace between them collapsed.</summary>
    private static string Checked(IRenderedComponent<ChipRadioGroup<string>> cut) =>
        string.Join(' ', cut.Find("[role='radio'][aria-checked='true']").TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>The group is a named radiogroup with the caller's class; each chip is a radio, and only the chosen one is checked and a tab stop.</summary>
    [Fact]
    public async Task Renders_RadioRoles_AndOneTabStopOnTheChosenChip()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<ChipRadioGroup<string>> cut = RenderIt(ctx, []);

        IElement group = cut.Find("[role='radiogroup']");
        Assert.Equal("Category", group.GetAttribute("aria-label"));
        Assert.True(group.ClassList.Contains("caller-hook"));
        IReadOnlyList<IElement> radios = cut.FindAll("[role='radio']");
        Assert.Equal(3, radios.Count);
        Assert.Equal(["false", "true", "false"], radios.Select(static radio => radio.GetAttribute("aria-checked")));
        Assert.Equal(["-1", "0", "-1"], radios.Select(static radio => radio.GetAttribute("tabindex")));
    }

    /// <summary>The count is drawn after the words of the item that has one, and on no other chip.</summary>
    [Fact]
    public async Task ItemCount_DrawsAfterTheWords_OnlyWhereGiven()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<ChipRadioGroup<string>> cut = RenderIt(ctx, []);

        IElement count = Assert.Single(cut.FindAll(".chip-radio-count"));
        Assert.Equal("3", count.TextContent.Trim());
        Assert.Equal("Needs you", count.PreviousElementSibling?.TextContent.Trim());
        Assert.Equal("Needs you 3", Checked(cut));
    }

    /// <summary>A plain Right arrow moves the choice forward and wraps from the last chip to the first; a plain Left wraps back.</summary>
    [Fact]
    public async Task PlainArrows_MoveTheChoice_AndWrap()
    {
        await using MudBunitContext ctx = new();
        ctx.Module(JsModule.Feed).SetupVoid("focusChecked", _ => true).SetVoidResult();
        List<string> raised = [];
        IRenderedComponent<ChipRadioGroup<string>> cut = RenderIt(ctx, raised);

        await cut.Find("[role='radiogroup']").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });
        cut.WaitForAssertion(() => Assert.Equal("Mine", Checked(cut)));
        await cut.Find("[role='radiogroup']").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        cut.WaitForAssertion(() => Assert.Equal("All", Checked(cut)));
        await cut.Find("[role='radiogroup']").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowLeft" });
        cut.WaitForAssertion(() => Assert.Equal("Mine", Checked(cut)));

        Assert.Equal(["Mine", "All", "Mine"], raised);
        Assert.NotEmpty(ctx.Module(JsModule.Feed).Invocations["focusChecked"]);
    }

    /// <summary>An arrow with Ctrl, and any key that is not an arrow, leave the choice alone, so a caller's own shortcut never moves it twice.</summary>
    [Fact]
    public async Task CtrlArrowAndOtherKeys_DoNotMoveTheChoice()
    {
        await using MudBunitContext ctx = new();
        List<string> raised = [];
        IRenderedComponent<ChipRadioGroup<string>> cut = RenderIt(ctx, raised);

        await cut.Find("[role='radiogroup']").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight", CtrlKey = true });
        await cut.Find("[role='radiogroup']").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.Empty(raised);
        Assert.Equal("Needs you 3", Checked(cut));
    }

    /// <summary>A click on another chip raises it; a click on the chosen chip raises nothing.</summary>
    [Fact]
    public async Task Click_RaisesANewChoiceOnly()
    {
        await using MudBunitContext ctx = new();
        List<string> raised = [];
        IRenderedComponent<ChipRadioGroup<string>> cut = RenderIt(ctx, raised);

        await cut.FindAll("[role='radio']")[1].ClickAsync(new MouseEventArgs());
        await cut.FindAll("[role='radio']")[0].ClickAsync(new MouseEventArgs());

        Assert.Equal(["All"], raised);
        Assert.Equal("All", Checked(cut));
    }
}
