using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Teams;
using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Tests for <see cref="TierOfModel"/> and the order of a draft card's kept Skill rows: what a card says must follow what the Human chose.</summary>
public sealed class TierOfModelTests
{
    private static readonly Dictionary<string, IReadOnlyDictionary<ModelTier, string>> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["claude"] = new Dictionary<ModelTier, string> { [ModelTier.Strongest] = "opus", [ModelTier.Balanced] = "sonnet", [ModelTier.Fast] = "haiku" },
    };

    /// <summary>The chip follows the Model: the role's tier with none chosen, the tier naming a chosen one, and no chip for a Model no tier names or an Agent with no role.</summary>
    /// <param name="roleTier">The role's tier.</param>
    /// <param name="model">The chosen Model.</param>
    /// <param name="expected">The tier the chip says.</param>
    [Theory]
    [InlineData(ModelTier.Strongest, null, ModelTier.Strongest)]
    [InlineData(ModelTier.Strongest, "opus", ModelTier.Strongest)]
    [InlineData(ModelTier.Strongest, "sonnet", ModelTier.Balanced)]
    [InlineData(ModelTier.Balanced, "HAIKU", ModelTier.Fast)]
    [InlineData(ModelTier.Fast, "someone-elses-model", null)]
    [InlineData(null, "opus", null)]
    public void Of_FollowsTheChosenModel(ModelTier? roleTier, string? model, ModelTier? expected)
    {
        Assert.Equal(expected, TierOfModel.Of(roleTier, model, Map));
    }

    /// <summary>Unticking a kept Skill leaves every row where it was, so the next click lands on the row the pointer is over.</summary>
    [Fact]
    public void KeptSkillOrder_UntickingARow_MovesNothing()
    {
        IReadOnlyList<string> draft = ["alpha", "beta", "gamma"];

        IEnumerable<string> order = AgentProfileCard.KeptSkillOrder(["beta", "gamma"], draft);

        Assert.Equal(draft, order);
    }

    /// <summary>A Skill ticked beyond the draft's own is listed after them, once.</summary>
    [Fact]
    public void KeptSkillOrder_AnAddedSkill_ComesAfterTheDraftsOwn()
    {
        IEnumerable<string> order = AgentProfileCard.KeptSkillOrder(["beta", "extra"], ["alpha", "beta"]);

        Assert.Equal(["alpha", "beta", "extra"], order);
    }
}
