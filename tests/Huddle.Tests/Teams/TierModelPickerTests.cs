using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Tests for <see cref="TierModelPicker"/>: tier to adapter and model (HR-6, MS-3, NF-9, D10).</summary>
public sealed class TierModelPickerTests
{
    private static readonly string[] NoAvoid = [];

    private static Dictionary<string, IReadOnlyDictionary<ModelTier, string>> Map(string adapter, ModelTier tier, string model)
    {
        return new(StringComparer.Ordinal)
        {
            [adapter] = new Dictionary<ModelTier, string> { [tier] = model },
        };
    }

    /// <summary>A mapped model that the adapter lists is the pick.</summary>
    [Fact]
    public void Pick_MappedModelListed_ReturnsThatModel()
    {
        List<AdapterAvailability> answered = [new("claude", ["opus", "sonnet"])];

        ModelPick? pick = TierModelPicker.Pick(ModelTier.Strongest, answered, TierModelPickerTests.Map("claude", ModelTier.Strongest, "opus"), TierModelPickerTests.NoAvoid);

        Assert.Equal(new ModelPick("claude", "opus"), pick);
    }

    /// <summary>An adapter with no entry for the tier yields the adapter's default model (null).</summary>
    [Fact]
    public void Pick_TierUnmapped_ReturnsNullModel()
    {
        List<AdapterAvailability> answered = [new("claude", ["opus", "sonnet"])];

        ModelPick? pick = TierModelPicker.Pick(ModelTier.Fast, answered, TierModelPickerTests.Map("claude", ModelTier.Strongest, "opus"), TierModelPickerTests.NoAvoid);

        Assert.Equal(new ModelPick("claude", null), pick);
    }

    /// <summary>An adapter outside the avoid list is preferred over one inside it.</summary>
    [Fact]
    public void Pick_AvoidListed_PrefersOtherAdapter()
    {
        List<AdapterAvailability> answered = [new("claude", ["opus"]), new("gemini", ["pro"])];

        ModelPick? pick = TierModelPicker.Pick(ModelTier.Balanced, answered, new Dictionary<string, IReadOnlyDictionary<ModelTier, string>>(StringComparer.Ordinal), ["claude"]);

        Assert.Equal(new ModelPick("gemini", null), pick);
    }

    /// <summary>When the avoided adapter is the only one that answered, it is still used.</summary>
    [Fact]
    public void Pick_OnlyAdapterIsAvoided_FallsBackToIt()
    {
        List<AdapterAvailability> answered = [new("claude", ["opus"])];

        ModelPick? pick = TierModelPicker.Pick(ModelTier.Balanced, answered, new Dictionary<string, IReadOnlyDictionary<ModelTier, string>>(StringComparer.Ordinal), ["claude"]);

        Assert.Equal(new ModelPick("claude", null), pick);
    }

    /// <summary>With no adapter answered there is no pick.</summary>
    [Fact]
    public void Pick_NoneAnswered_ReturnsNull()
    {
        ModelPick? pick = TierModelPicker.Pick(ModelTier.Balanced, [], TierModelPickerTests.Map("claude", ModelTier.Balanced, "sonnet"), TierModelPickerTests.NoAvoid);

        Assert.Null(pick);
    }

    /// <summary>A mapped model the adapter does not list is ignored and the default (null) is used.</summary>
    [Fact]
    public void Pick_MappedModelNotListed_IgnoresMapping()
    {
        List<AdapterAvailability> answered = [new("claude", ["sonnet"])];

        ModelPick? pick = TierModelPicker.Pick(ModelTier.Strongest, answered, TierModelPickerTests.Map("claude", ModelTier.Strongest, "opus"), TierModelPickerTests.NoAvoid);

        Assert.Equal(new ModelPick("claude", null), pick);
    }
}
