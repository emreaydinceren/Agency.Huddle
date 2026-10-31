using Agency.Huddle.App.Components.Settings.Skills;
using Agency.Huddle.App.Skills.Ui;
using Agency.Huddle.SkillPacks.Resolving;
using AngleSharp.Dom;
using Bunit;

namespace Agency.Huddle.Tests.Ui.SkillPacks;

/// <summary>Pins <see cref="SkillStateChips"/> (Skill Packs spec §6.9.4): the look of every state, the In use chip, and that
/// each chip's sentence reaches keyboard and screen reader.</summary>
public sealed class SkillStateChipsTests
{
    /// <summary>Every state renders its text, its <c>status-chip-{colour}</c> class and an icon, so none is colour only.</summary>
    [Fact]
    public async Task EveryState_RendersTextColourAndIcon()
    {
        await using MudBunitContext ctx = new();
        foreach (ItemState state in Enum.GetValues<ItemState>())
        {
            IRenderedComponent<SkillStateChips> cut = ctx.Render<SkillStateChips>(parameters => parameters
                .Add(chips => chips.State, state)
                .Add(chips => chips.UsedBy, state == ItemState.InUse ? 2 : 0)
                .Add(chips => chips.HolderNames, "Jane, Omar"));

            StateLook look = SkillStateText.For(state);
            IElement chip = cut.Find($".skill-state-chip-{state.ToString().ToLowerInvariant()} .status-chip");
            Assert.Equal(SkillStateText.ChipText(state, 2), chip.TextContent.Trim());
            Assert.Contains($"status-chip-{look.Color.ToString().ToLowerInvariant()}", chip.ClassList);
            Assert.NotEmpty(chip.QuerySelectorAll("svg"));
        }
    }

    /// <summary>InUse shows "In use by 2" once; Update available with holders shows both chips.</summary>
    [Fact]
    public async Task InUse_AndUpdateWithHolders_ShowTheRightChips()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<SkillStateChips> inUse = ctx.Render<SkillStateChips>(parameters => parameters
            .Add(chips => chips.State, ItemState.InUse).Add(chips => chips.UsedBy, 2).Add(chips => chips.HolderNames, "Jane, Omar"));
        IRenderedComponent<SkillStateChips> update = ctx.Render<SkillStateChips>(parameters => parameters
            .Add(chips => chips.State, ItemState.UpdateAvailable).Add(chips => chips.UsedBy, 2).Add(chips => chips.HolderNames, "Jane, Omar"));

        Assert.Equal(["In use by 2"], inUse.FindAll(".status-chip").Select(static chip => chip.TextContent.Trim()));
        Assert.Equal(["Update available", "In use by 2"], update.FindAll(".status-chip").Select(static chip => chip.TextContent.Trim()));
    }

    /// <summary>A chip is keyboard focusable and carries its sentence as its accessible name.</summary>
    [Fact]
    public async Task Chip_IsFocusable_WithItsSentence()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<SkillStateChips> cut = ctx.Render<SkillStateChips>(parameters => parameters
            .Add(chips => chips.State, ItemState.Withdrawn).Add(chips => chips.Reason, "licence changed"));

        IElement wrapper = cut.Find(".skill-state-chip-withdrawn");
        Assert.Equal("0", wrapper.GetAttribute("tabindex"));
        Assert.Equal($"Withdrawn: {SkillStateText.Sentence(ItemState.Withdrawn, [], "licence changed")}", wrapper.GetAttribute("aria-label"));
    }

    /// <summary>The Hidden and modified locally flags draw their own chips; a built-in with no state draws none of the state chips.</summary>
    [Fact]
    public async Task Flags_DrawTheirChips_NoStateDrawsNone()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<SkillStateChips> cut = ctx.Render<SkillStateChips>(parameters => parameters
            .Add(chips => chips.State, (ItemState?)null).Add(chips => chips.Hidden, true).Add(chips => chips.ModifiedLocally, true));

        Assert.Equal(["Hidden", "modified locally"], cut.FindAll(".status-chip").Select(static chip => chip.TextContent.Trim()));
    }
}
