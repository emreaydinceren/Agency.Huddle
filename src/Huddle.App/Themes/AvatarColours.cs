using MudBlazor.Utilities;

namespace Agency.Huddle.App.Themes;

/// <summary>
/// The monogram colour recipe, in C#: the same <c>color-mix(in srgb, ...)</c> that <c>app.css</c> applies to
/// <c>.avatar-monogram</c>, so a test can run the recipe through <see cref="ContrastColour.Ratio(MudColor, MudColor)"/>
/// for any Teammate colour in any Theme. The fill is the Teammate's colour at <see cref="FillPercent"/>% over Surface;
/// the letters are that colour mixed <see cref="LettersTowardTextPercent"/>% toward <c>TextPrimary</c>. A Dark Theme
/// goes through the same maths against its own Surface and TextPrimary, giving a deep fill with light letters.
/// </summary>
internal static class AvatarColours
{
    /// <summary>How much of the Teammate's colour is in the monogram fill; the rest is Surface.</summary>
    internal const int FillPercent = 20;

    /// <summary>How far the letters are pulled from the Teammate's colour toward <c>TextPrimary</c>.</summary>
    internal const int LettersTowardTextPercent = 75;

    /// <summary>
    /// The inline style that gives a monogram <paramref name="teammateColour"/>: the fill and the letters as
    /// <c>color-mix</c> against the Theme's own Surface and TextPrimary, so each Theme resolves it for itself.
    /// </summary>
    /// <param name="teammateColour">The Teammate's colour, as the stored <c>#rrggbb</c> text.</param>
    /// <returns>The <c>background</c> and <c>color</c> declarations.</returns>
    internal static string MonogramStyle(string teammateColour) =>
        $"background: color-mix(in srgb, {teammateColour} {FillPercent}%, var(--mud-palette-surface)); "
        + $"color: color-mix(in srgb, {teammateColour} {100 - LettersTowardTextPercent}%, var(--mud-palette-text-primary));";

    /// <summary>The monogram fill:<paramref name="teammate"/> at <see cref="FillPercent"/>% over <paramref name="surface"/>.</summary>
    /// <param name="teammate">The Teammate's colour.</param>
    /// <param name="surface">The Theme's Surface.</param>
    /// <returns>The opaque fill colour.</returns>
    internal static MudColor Fill(MudColor teammate, MudColor surface) => Mix(surface, teammate, FillPercent);

    /// <summary>The monogram letters: <paramref name="teammate"/> mixed <see cref="LettersTowardTextPercent"/>% toward <paramref name="textPrimary"/>.</summary>
    /// <param name="teammate">The Teammate's colour.</param>
    /// <param name="textPrimary">The Theme's TextPrimary.</param>
    /// <returns>The opaque letter colour.</returns>
    internal static MudColor Letters(MudColor teammate, MudColor textPrimary) => Mix(teammate, textPrimary, LettersTowardTextPercent);

    /// <summary>
    /// Mixes two opaque colours in sRGB space, as <c>color-mix(in srgb, from, to N%)</c> does.
    /// </summary>
    /// <param name="from">The colour at 0%.</param>
    /// <param name="to">The colour at 100%.</param>
    /// <param name="toPercent">How much of <paramref name="to"/>, 0 to 100.</param>
    /// <returns>The mixed colour.</returns>
    internal static MudColor Mix(MudColor from, MudColor to, int toPercent)
    {
        double fraction = toPercent / 100.0;
        return new MudColor(
            r: Channel(from.R, to.R, fraction),
            g: Channel(from.G, to.G, fraction),
            b: Channel(from.B, to.B, fraction),
            a: 255);
    }

    /// <summary>One channel of <see cref="Mix(MudColor, MudColor, int)"/>.</summary>
    private static byte Channel(byte from, byte to, double fraction) =>
        (byte)Math.Round(from + ((to - from) * fraction), MidpointRounding.AwayFromZero);
}
