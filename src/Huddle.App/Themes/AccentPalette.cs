using MudBlazor;
using MudBlazor.Utilities;

namespace Agency.Huddle.App.Themes;

/// <summary>
/// Clamps an arbitrary accent colour so it is safe to use as <see cref="MudBlazor.Palette.Primary"/>.
/// A Human-picked accent colour carries no contrast guarantee the way every hand-tuned palette in
/// <see cref="ThemeCatalog"/> does - the former Huddle Dark theme's own remarks walked through exactly
/// this check for its own <c>Primary</c>, measured against WCAG 2.1's contrast maths in
/// <see cref="ContrastColour"/>. This does the same measurement at runtime instead of by hand.
/// </summary>
internal static class AccentPalette
{
    /// <summary>WCAG 1.4.3's text contrast minimum - the same floor <c>ThemeCatalogTests</c> holds every built-in theme's <c>Primary vs Surface</c> pair to.</summary>
    private const double MinimumContrastRatio = 4.5;

    /// <summary>How far <see cref="ClampForContrast"/> shifts lightness per step. Small enough to stay close to the original hue and saturation; the loop below takes as many steps as it needs.</summary>
    private const double LightnessStep = 0.02;

    /// <summary>
    /// One full lightness range (0 to 1) takes 50 steps of <see cref="LightnessStep"/>; twice that is
    /// more than enough headroom for <see cref="MudColor.ChangeLightness(double)"/>'s own clamping at
    /// the ends, so a candidate that still fails every step is a background <see cref="ContrastColour"/>
    /// cannot be cleared for at all - which <see cref="ContrastColour"/>'s own remarks prove never happens for pure black or white.
    /// </summary>
    private const int MaxSteps = 100;

    /// <summary>
    /// Adjusts <paramref name="accent"/>'s lightness, toward black or white per <paramref name="towardBlack"/>,
    /// until it clears <see cref="MinimumContrastRatio"/> against both <paramref name="background"/> and
    /// <paramref name="surface"/> - or falls back to pure black or white, whichever <paramref name="towardBlack"/>
    /// asked for, if lightness alone cannot get there. <see cref="ContrastColour"/>'s own remarks prove one of
    /// those two always clears the floor against any background, so the fallback itself can never fail.
    /// </summary>
    /// <param name="accent">The Human-picked accent colour, unclamped.</param>
    /// <param name="background">The palette's Background colour the result must read against.</param>
    /// <param name="surface">The palette's Surface colour the result must also read against.</param>
    /// <param name="towardBlack">Whether to darken (for a light palette) or lighten (for a dark palette) in search of contrast.</param>
    /// <returns>An accent-hued colour that clears <see cref="MinimumContrastRatio"/> against both <paramref name="background"/> and <paramref name="surface"/>.</returns>
    internal static MudColor ClampForContrast(MudColor accent, MudColor background, MudColor surface, bool towardBlack)
    {
        var candidate = accent;
        for (var step = 0; step < MaxSteps; step++)
        {
            if (ContrastColour.Ratio(candidate, background) >= MinimumContrastRatio &&
                ContrastColour.Ratio(candidate, surface) >= MinimumContrastRatio)
            {
                return candidate;
            }

            candidate = candidate.ChangeLightness(towardBlack ? -LightnessStep : LightnessStep);
        }

        return towardBlack ? new MudColor(r: 0, g: 0, b: 0, a: 255) : new MudColor(r: 255, g: 255, b: 255, a: 255);
    }

    /// <summary>The hue step <see cref="ChooseTertiary"/> scans the colour wheel in. Five degrees is fine enough that the winning gap is within a few degrees of the best possible one.</summary>
    private const int HueScanStep = 5;

    /// <summary>
    /// Picks the Tertiary (Review) colour for a Custom Accent palette: the hue on the wheel farthest from the accent
    /// and from the palette's Info, Success, Warning and Error hues, carrying <paramref name="preferred"/>'s saturation
    /// and lightness, then clamped for contrast like the accent itself. With five reference hues the widest gap on
    /// the wheel is at least 72 degrees, so the winner is at least 36 degrees from each of them: the 30 degree
    /// hue-distance rule (audit A2) holds for any accent the Human picks, at least for Tertiary.
    /// </summary>
    /// <param name="accent">The clamped accent colour.</param>
    /// <param name="palette">The palette being built; its Info, Success, Warning and Error hues are avoided.</param>
    /// <param name="preferred">Huddle's own Tertiary for this mode; its saturation and lightness are kept, and its hue wins a tie.</param>
    /// <param name="towardBlack">Whether to darken (light palette) or lighten (dark palette) in search of contrast.</param>
    /// <returns>A Tertiary colour readable against the palette's Background and Surface.</returns>
    internal static MudColor ChooseTertiary(MudColor accent, Palette palette, MudColor preferred, bool towardBlack)
    {
        double[] avoided = [accent.H, palette.Info.H, palette.Success.H, palette.Warning.H, palette.Error.H];

        double bestHue = preferred.H;
        double bestGap = MinimumHueGap(bestHue, avoided);
        for (int hue = 0; hue < 360; hue += HueScanStep)
        {
            double gap = MinimumHueGap(hue, avoided);
            if (gap > bestGap + 0.5)
            {
                bestGap = gap;
                bestHue = hue;
            }
        }

        MudColor candidate = new(bestHue, preferred.S, preferred.L, 1.0);
        return ClampForContrast(candidate, palette.Background, palette.Surface, towardBlack);
    }

    /// <summary>The smallest angular distance, in degrees, from <paramref name="hue"/> to any of <paramref name="others"/>.</summary>
    /// <param name="hue">A hue angle in degrees.</param>
    /// <param name="others">The hue angles to keep clear of.</param>
    /// <returns>The smallest distance around the wheel, 0 to 180.</returns>
    internal static double MinimumHueGap(double hue, IEnumerable<double> others) =>
        others.Min(other => HueDistance(hue, other));

    /// <summary>The distance between two hue angles around the wheel.</summary>
    /// <param name="first">A hue angle in degrees.</param>
    /// <param name="second">Another hue angle in degrees.</param>
    /// <returns>The shorter way around, 0 to 180.</returns>
    internal static double HueDistance(double first, double second)
    {
        double difference = Math.Abs(first - second) % 360.0;
        return difference > 180.0 ? 360.0 - difference : difference;
    }
}