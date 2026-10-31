using Agency.Huddle.App.Skills.Ui;
using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.Tests.Ui.SkillPacks;

/// <summary>Pins <see cref="SkillStateText"/> (Skill Packs spec §6.9.4): every state has chip text, an icon and one sentence.</summary>
public sealed class SkillStateTextTests
{
    /// <summary>Every <see cref="ItemState"/>, New and InUse included, has chip text, an icon, and one sentence ending in a full stop.</summary>
    [Fact]
    public void For_EveryState_HasTextIconAndOneSentence()
    {
        foreach (ItemState state in Enum.GetValues<ItemState>())
        {
            StateLook look = SkillStateText.For(state);

            Assert.False(string.IsNullOrWhiteSpace(look.Text), $"{state} has no chip text");
            Assert.False(string.IsNullOrWhiteSpace(look.Icon), $"{state} has no icon");
            Assert.EndsWith(".", look.Sentence, StringComparison.Ordinal); // contains-ok: the rule is one whole sentence for every state
        }
    }

    /// <summary>The In use sentence names the holders, and Withdrawn gives the Source's reason.</summary>
    [Fact]
    public void Sentence_FillsHoldersAndReason()
    {
        Assert.Equal("2 Agents carry it: Jane and Omar. They use a copy in Skills/, never your AI tool's clone.", SkillStateText.Sentence(ItemState.InUse, ["Jane", "Omar"], null));
        Assert.Equal("Its Source pulled it: licence changed. New hires cannot take it. Agents who have it keep working and are flagged.", SkillStateText.Sentence(ItemState.Withdrawn, [], "licence changed."));
        Assert.Equal("In use by 2", SkillStateText.ChipText(ItemState.InUse, 2));
        Assert.Equal("Jane, Omar and Ravi", SkillStateText.AndList(["Jane", "Omar", "Ravi"]));
        Assert.Equal("Jane", SkillStateText.AndList(["Jane"]));
    }

    /// <summary>Only Update available and modified locally are amber: Not downloaded is neutral because nothing is wrong.</summary>
    [Fact]
    public void Colours_OnlyUpdateAndModifiedAreAmber()
    {
        IReadOnlyList<ItemState> amber = [.. Enum.GetValues<ItemState>().Where(static state => SkillStateText.For(state).Color == MudBlazor.Color.Warning)];

        Assert.Equal([ItemState.UpdateAvailable], amber);
        Assert.Equal(MudBlazor.Color.Warning, SkillStateText.ModifiedLocally.Color);
        Assert.Equal(MudBlazor.Color.Default, SkillStateText.For(ItemState.NotDownloaded).Color);
    }
}
