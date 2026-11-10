using Agency.Huddle.App.Themes;
using MudBlazor.Utilities;

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

    /// <summary>The faintest contrast against Surface at which a Line still outlines a card (the darkest Theme's Line is 1.15:1).</summary>
    private const double MinimumLineContrast = 1.1;

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

    /// <summary>
    /// The three line rungs are ordered as they render: Line (a card's outline and an inner rule) is fainter than Field (an
    /// input's boundary), which is no stronger than Chrome (<c>palette.linesDefault</c>, the line that closes the shell's
    /// panes), measured as contrast against Surface. This replaces the retired check of <c>palette.Divider</c>, a colour no
    /// rule paints.
    /// </summary>
    /// <param name="skinId">The skin under test.</param>
    [Theory]
    [MemberData(nameof(SkinIds))]
    public void LineRungs_AreOrdered_LineBelowFieldAtMostChrome(string skinId)
    {
        SkinColours colours = Skin(skinId);
        double line = ContrastAgainstSurface(colours, colours.Token("ink.line"));
        double field = ContrastAgainstSurface(colours, colours.Token("ink.field"));
        double chrome = ContrastAgainstSurface(colours, colours.Palette("linesDefault"));

        Assert.True(line >= MinimumLineContrast, $"{skinId}: Line is {line:F2}:1 against Surface, too faint to outline a card.");
        Assert.True(line < field, $"{skinId}: Line {line:F2}:1 must be fainter than Field {field:F2}:1.");
        Assert.True(field <= chrome + 1e-6, $"{skinId}: Field {field:F2}:1 must not be stronger than Chrome {chrome:F2}:1.");
    }

    private static double ContrastAgainstSurface(SkinColours colours, SrgbColour? colour) =>
        ContrastColour.Ratio(
            new MudColor((colour ?? throw new InvalidOperationException("The rung is not a colour.")).Hex),
            new MudColor(colours.Surface.Hex));

    private static SkinColours Skin(string skinId) => SkinColours.All.Single(colours => string.Equals(colours.Id, skinId, StringComparison.Ordinal));
}
