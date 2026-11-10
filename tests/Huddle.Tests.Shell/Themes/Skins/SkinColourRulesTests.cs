namespace Agency.Huddle.Tests.Themes.Skins;

/// <summary>
/// The colour rules the Themes keep, asserted on the resolved colours of every shipped skin (<see cref="SkinColours"/>):
/// a set filter can be seen, the line rungs are ordered, the current row carries a cue hover lacks, and no two named
/// grounds or states sit closer than a person can tell apart.
/// </summary>
public sealed class SkinColourRulesTests
{
    /// <summary>The smallest CIEDE2000 difference a person can tell apart (plan D1).</summary>
    private const double VisibleDifference = 3.0;

    /// <summary>Every shipped skin, as a theory's data: the Theme in both modes (eight).</summary>
    public static TheoryData<string> SkinIds => [.. SkinColours.All.Select(static colours => colours.Id)];

    /// <summary>
    /// A filter or control that is set reads <c>selected.background</c>, so that fill must be at least ΔE 3 from the Surface
    /// it sits on in every Theme and mode (Lumen's old <c>chip.set.background</c> was ΔE 1.0 and could not be seen).
    /// </summary>
    /// <param name="skinId">The skin under test.</param>
    [Theory]
    [MemberData(nameof(SkinIds))]
    public void SetState_SelectedBackground_IsVisibleAgainstSurface(string skinId)
    {
        SkinColours colours = Skin(skinId);

        double difference = DeltaE.Ciede2000(colours.Token("selected.background") ?? throw new InvalidOperationException("selected.background is not a colour."), colours.Surface);

        Assert.True(difference >= VisibleDifference, $"{skinId}: selected.background is only ΔE {difference:F1} from Surface.");
    }

    private static SkinColours Skin(string skinId) => SkinColours.All.Single(colours => string.Equals(colours.Id, skinId, StringComparison.Ordinal));
}
