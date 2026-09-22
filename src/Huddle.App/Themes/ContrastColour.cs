using MudBlazor.Utilities;

namespace Agency.Huddle.App.Themes;

/// <summary>
/// WCAG 2.x contrast maths over <see cref="MudColor"/>. This is where "is this foreground legible on
/// that background" is answered once, rather than re-derived at each call site - the avatar monogram
/// (<c>Agency.Huddle.App.Avatars.Avatar</c>) needs exactly the same question <c>HuddleTheme</c> and
/// <c>ThemeCatalogTests</c> already answer for a palette.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ReadableForeground(MudColor)"/> never returns anything but pure black or pure white, and
/// that is a proven sufficient choice, not a shortcut. Contrast against black (relative luminance 0) is
/// <c>(L + 0.05) / 0.05</c>, which clears the WCAG 1.4.3 4.5:1 text minimum exactly when <c>L &gt;=
/// 0.175</c>. Contrast against white (relative luminance 1) is <c>1.05 / (L + 0.05)</c>, which clears
/// 4.5:1 exactly when <c>L &lt;= 0.18333...</c>. Those two intervals - <c>[0.175, 1]</c> for black and
/// <c>[0, 0.18333...]</c> for white - overlap on <c>[0.175, 0.18333...]</c>, so between them they cover
/// every possible relative luminance from 0 to 1. There is no sRGB colour for which neither pure black
/// nor pure white reaches 4.5:1 against it. The test suite mechanises this claim directly: it sweeps the
/// sRGB cube and asserts the floor holds for every colour it produces.
/// </para>
/// <para>
/// On an exact tie between the two ratios, black wins. That matches <c>HuddleTheme.Dark()</c>, which
/// already chose <c>PrimaryContrastText = "#000000"</c> for exactly this case - the avatar monogram
/// reads black-on-background wherever the choice is otherwise indifferent.
/// </para>
/// </remarks>
internal static class ContrastColour
{
    /// <summary>Pure black, one of the only two colours <see cref="ReadableForeground(MudColor)"/> ever returns.</summary>
    private static readonly MudColor PureBlack = new(r: 0, g: 0, b: 0, a: 255);

    /// <summary>Pure white, the other of the only two colours <see cref="ReadableForeground(MudColor)"/> ever returns.</summary>
    private static readonly MudColor PureWhite = new(r: 255, g: 255, b: 255, a: 255);

    /// <summary>
    /// Linearises one 0-255 sRGB channel per WCAG 2.1: <c>c / 12.92</c> when <c>c &lt;= 0.03928</c>,
    /// otherwise <c>((c + 0.055) / 1.055) ^ 2.4</c>, where <c>c</c> is <paramref name="channel"/>
    /// divided by 255.
    /// </summary>
    /// <param name="channel">The channel's 0-255 byte value.</param>
    /// <returns>The linearised channel value, in the range 0 to 1.</returns>
    internal static double LineariseChannel(byte channel)
    {
        var normalised = channel / 255.0;
        return normalised <= 0.03928 ? normalised / 12.92 : Math.Pow((normalised + 0.055) / 1.055, 2.4);
    }

    /// <summary>
    /// A colour's WCAG 2.1 relative luminance: each sRGB channel linearised via
    /// <see cref="LineariseChannel(byte)"/>, then combined as <c>0.2126R + 0.7152G + 0.0722B</c>.
    /// </summary>
    /// <param name="colour">The colour to measure.</param>
    /// <returns>The colour's relative luminance, in the range 0 to 1.</returns>
    internal static double RelativeLuminance(MudColor colour) =>
        (0.2126 * LineariseChannel(colour.R)) + (0.7152 * LineariseChannel(colour.G)) + (0.0722 * LineariseChannel(colour.B));

    /// <summary>
    /// The WCAG 2.1 contrast ratio between two colours: <c>(Lmax + 0.05) / (Lmin + 0.05)</c>, where
    /// <c>Lmax</c> is the lighter colour's relative luminance and <c>Lmin</c> the darker's. The ratio is
    /// symmetric, so it does not matter which of <paramref name="first"/> and <paramref name="second"/>
    /// is the foreground.
    /// </summary>
    /// <param name="first">One colour.</param>
    /// <param name="second">The other colour.</param>
    /// <returns>The contrast ratio, always 1 or greater.</returns>
    internal static double Ratio(MudColor first, MudColor second)
    {
        var firstLuminance = RelativeLuminance(first);
        var secondLuminance = RelativeLuminance(second);
        var lighter = Math.Max(firstLuminance, secondLuminance);
        var darker = Math.Min(firstLuminance, secondLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>
    /// Picks whichever of pure black and pure white gives the larger WCAG contrast ratio against
    /// <paramref name="background"/>, with black winning an exact tie. See the type's <c>remarks</c>
    /// for the proof that one of the two always clears the 4.5:1 WCAG 1.4.3 text minimum.
    /// </summary>
    /// <param name="background">The background colour the returned foreground must read against.</param>
    /// <returns><see cref="PureBlack"/> or <see cref="PureWhite"/>, whichever contrasts more against <paramref name="background"/>.</returns>
    internal static MudColor ReadableForeground(MudColor background)
    {
        var blackRatio = Ratio(PureBlack, background);
        var whiteRatio = Ratio(PureWhite, background);
        return whiteRatio > blackRatio ? PureWhite : PureBlack;
    }
}
