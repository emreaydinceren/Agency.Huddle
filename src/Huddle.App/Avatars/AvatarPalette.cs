using MudBlazor.Utilities;

namespace Agency.Huddle.App.Avatars;

/// <summary>
/// The one palette of avatar background colours: the swatches the avatar editor offers, and the set a new
/// Teammate's random background is drawn from, so a random colour is always one the editor can show as selected.
/// </summary>
internal static class AvatarPalette
{
    /// <summary>
    /// Every colour of the palette, in the order the editor lays them out: a crimson tint and shade run, a dodger-blue
    /// run, a reversed purple run, turquoise and its analogous colours, a blue-to-green gradient and an
    /// orange-green-violet gradient.
    /// </summary>
    internal static IReadOnlyList<MudColor> Colours { get; } = [.. new List<IEnumerable<MudColor>>
    {
        MudColor.GenerateTintShadePalette("#DC143C", shadeStep: 0),
        MudColor.GenerateTintShadePalette("#1E90FF", tintStep: 0),
        MudColor.GenerateTintShadePalette("#8E24AA").Reverse(),
        MudColor.GenerateAnalogousPalette("#40E0D0"),
        MudColor.GenerateGradientPalette("#1E90FF", "#32CD32"),
        MudColor.GenerateMultiGradientPalette(["#FF4500", "#32CD32", "#8A2BE2"]),
    }.SelectMany(static palette => palette)];

    /// <summary>
    /// Picks one colour of <see cref="Colours"/> at random and writes it the way the avatar store holds a background:
    /// a six-digit hex such as <c>#dc143c</c>. The result depends on nothing but <paramref name="random"/>, so a caller
    /// that is tested hands in a seeded one.
    /// </summary>
    /// <param name="random">The source of the choice.</param>
    internal static string Pick(Random random) =>
        Colours[random.Next(Colours.Count)].ToString(MudColorOutputFormats.Hex);
}
