using MudBlazor.Utilities;

namespace Agency.Huddle.App.Themes;

/// <summary>
/// The monogram colour recipe, in C#: the same <c>color-mix(in srgb, ...)</c> that <c>app.css</c> applies to
/// <c>.avatar-monogram</c>, so a test can run the recipe through <see cref="ContrastColour.Ratio(MudColor, MudColor)"/>
/// for any Agent colour in any Theme. A Light Theme: the fill is the Agent's colour at <see cref="LightFillPercent"/>%
/// over Surface and the letters are that colour mixed <see cref="LettersTowardPercent"/>% toward <c>TextPrimary</c>. A Dark
/// Theme: the colour at <see cref="DarkFillPercent"/>% over its Surface (a deep fill) and the letters mixed
/// <see cref="LettersTowardPercent"/>% toward white (light letters).
/// </summary>
internal static class AvatarColours
{
    /// <summary>How much of the Agent's colour is in a Light Theme's monogram fill; the rest is Surface.</summary>
    internal const int LightFillPercent = 40;

    /// <summary>How much of the Agent's colour is in a Dark Theme's monogram fill; the rest is Surface.</summary>
    internal const int DarkFillPercent = 40;

    /// <summary>How far the letters are pulled from the Agent's colour toward <c>TextPrimary</c> (Light) or white (Dark).</summary>
    internal const int LettersTowardPercent = 75;

    /// <summary>
    /// The inline style that gives a monogram <paramref name="agentColour"/>: it replaces the avatar's own copy of the
    /// primary colour, which <c>app.css</c> turns into the fill and the letters for the Theme in force (Light or Dark), so
    /// the recipe lives in one place and each Theme resolves it for itself.
    /// </summary>
    /// <param name="agentColour">The Agent's colour, as the stored <c>#rrggbb</c> text.</param>
    /// <returns>The style declaration.</returns>
    internal static string MonogramStyle(string agentColour) => $"--mud-palette-primary: {agentColour};";

    /// <summary>The monogram fill: <paramref name="agent"/> over <paramref name="surface"/>, at 40% in either Theme.</summary>
    /// <param name="agent">The Agent's colour.</param>
    /// <param name="surface">The Theme's Surface.</param>
    /// <param name="dark">Whether the Theme is a Dark one.</param>
    /// <returns>The opaque fill colour.</returns>
    internal static MudColor Fill(MudColor agent, MudColor surface, bool dark) =>
        Mix(surface, agent, dark ? DarkFillPercent : LightFillPercent);

    /// <summary>The monogram letters: <paramref name="agent"/> mixed 75% toward <paramref name="textPrimary"/> (Light) or white (Dark).</summary>
    /// <param name="agent">The Agent's colour.</param>
    /// <param name="textPrimary">The Theme's TextPrimary, used by a Light Theme.</param>
    /// <param name="dark">Whether the Theme is a Dark one.</param>
    /// <returns>The opaque letter colour.</returns>
    internal static MudColor Letters(MudColor agent, MudColor textPrimary, bool dark) =>
        Mix(agent, dark ? new MudColor("#ffffff") : textPrimary, LettersTowardPercent);

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
