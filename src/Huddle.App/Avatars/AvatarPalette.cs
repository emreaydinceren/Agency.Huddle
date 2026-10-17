using System.Globalization;
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
    /// Every colour of <see cref="Colours"/> with the name a screen reader says for it: a lightness word and a hue family
    /// ("Deep red", "Light blue"), made unique within the palette by a trailing number. The editor draws them as a radio
    /// group, so each swatch needs a name a person can tell from its neighbours; a bare hex code is not one.
    /// </summary>
    internal static IReadOnlyList<AvatarSwatch> Swatches { get; } = BuildSwatches();

    /// <summary>The name of one palette colour before it is made unique: its lightness word and hue family, or a grey for a colour with almost no saturation.</summary>
    /// <param name="colour">The colour to name.</param>
    /// <returns>For example "Deep red" or "Light blue".</returns>
    internal static string NameOf(MudColor colour)
    {
        ArgumentNullException.ThrowIfNull(colour);

        double hue = colour.H;
        double saturation = colour.S;
        double lightness = colour.L;

        string family;
        if (saturation < 0.12)
        {
            family = lightness switch
            {
                < 0.2 => "black",
                > 0.9 => "white",
                _ => "grey",
            };
            return Capitalise(family);
        }

        family = hue switch
        {
            < 15 or >= 345 => "red",
            < 45 => "orange",
            < 70 => "yellow",
            < 150 => "green",
            < 190 => "teal",
            < 250 => "blue",
            < 290 => "purple",
            _ => "pink",
        };

        string prefix = lightness switch
        {
            < 0.25 => "deep ",
            < 0.42 => "dark ",
            < 0.65 => string.Empty,
            < 0.82 => "light ",
            _ => "pale ",
        };
        return Capitalise(prefix + family);
    }

    private static string Capitalise(string text) => string.Concat(text[..1].ToUpperInvariant(), text[1..]);

    private static List<AvatarSwatch> BuildSwatches()
    {
        Dictionary<string, int> seen = new(StringComparer.Ordinal);
        List<AvatarSwatch> swatches = [];
        foreach (MudColor colour in Colours)
        {
            string name = NameOf(colour);
            int count = seen.GetValueOrDefault(name) + 1;
            seen[name] = count;
            swatches.Add(new AvatarSwatch(colour.ToString(MudColorOutputFormats.Hex), count == 1 ? name : string.Create(CultureInfo.InvariantCulture, $"{name} {count}")));
        }

        return swatches;
    }

    /// <summary>
    /// Picks one colour of <see cref="Colours"/> at random and writes it the way the avatar store holds a background:
    /// a six-digit hex such as <c>#dc143c</c>. The result depends on nothing but <paramref name="random"/>, so a caller
    /// that is tested hands in a seeded one.
    /// </summary>
    /// <param name="random">The source of the choice.</param>
    internal static string Pick(Random random) =>
        Colours[random.Next(Colours.Count)].ToString(MudColorOutputFormats.Hex);
}