using MudBlazor.Utilities;

namespace Agency.Huddle.App.Themes;

/// <summary>
/// The tinted status chip's colours computed in C#, mirroring the <c>color-mix</c> rules of <c>.status-chip-*</c>
/// in app.css, so a test can hold the chip's text to the WCAG 1.4.3 floor in every Theme without a browser.
/// </summary>
internal static class StatusChipContrast
{
    /// <summary>How much of the status colour tints the chip's fill over Surface.</summary>
    internal const double FillShare = 0.14;

    /// <summary>How much of the status colour stays in the chip's text; the rest is <c>TextPrimary</c>.</summary>
    internal const double TextShare = 0.50;

    /// <summary>The chip's fill: <paramref name="status"/> at <see cref="FillShare"/> over <paramref name="surface"/>.</summary>
    /// <param name="status">The status colour.</param>
    /// <param name="surface">The Surface colour behind the chip.</param>
    internal static MudColor Fill(MudColor status, MudColor surface) => Mix(status, surface, FillShare);

    /// <summary>The chip's text and icon colour: <paramref name="status"/> mixed <see cref="TextShare"/> toward <paramref name="textPrimary"/>.</summary>
    /// <param name="status">The status colour.</param>
    /// <param name="textPrimary">The Theme's TextPrimary.</param>
    internal static MudColor Text(MudColor status, MudColor textPrimary) => Mix(status, textPrimary, TextShare);

    /// <summary>The contrast ratio of the chip's text on its fill.</summary>
    /// <param name="status">The status colour.</param>
    /// <param name="surface">The Surface colour behind the chip.</param>
    /// <param name="textPrimary">The Theme's TextPrimary.</param>
    internal static double Ratio(MudColor status, MudColor surface, MudColor textPrimary) =>
        ContrastColour.Ratio(Text(status, textPrimary), Fill(status, surface));

    /// <summary>An sRGB mix like CSS <c>color-mix(in srgb, first share%, second)</c>.</summary>
    private static MudColor Mix(MudColor first, MudColor second, double share) => new(
        r: Channel(first.R, second.R, share),
        g: Channel(first.G, second.G, share),
        b: Channel(first.B, second.B, share),
        a: 255);

    private static byte Channel(byte first, byte second, double share) =>
        (byte)Math.Round((first * share) + (second * (1 - share)), MidpointRounding.AwayFromZero);
}
