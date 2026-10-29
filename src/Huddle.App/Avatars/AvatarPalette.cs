using MudBlazor.Utilities;

namespace Agency.Huddle.App.Avatars;

/// <summary>
/// The one palette of avatar background colours: the swatches the avatar editor offers, and the set a new
/// Agent's random background is drawn from, so a random colour is always one the editor can show as selected.
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

    /// <summary>Everyday colour names with their approximate colours; larger than the palette so every swatch finds an unclaimed name.</summary>
    private static readonly (string Name, int Rgb)[] NamedColours =
    [
        ("Red", 0xFF0000), ("Crimson", 0xDC143C), ("Scarlet", 0xFF2400), ("Rose", 0xE8506E), ("Coral", 0xFF7F50),
        ("Salmon", 0xFA8072), ("Pink", 0xFFC0CB), ("Blush", 0xF4B6C2), ("Maroon", 0x800000), ("Burgundy", 0x800020),
        ("Brick", 0xB22222), ("Rust", 0xB7410E), ("Orange", 0xFFA500), ("Vermilion", 0xFF4500), ("Tangerine", 0xF28500),
        ("Peach", 0xFFCBA4), ("Apricot", 0xFBCEB1), ("Amber", 0xFFBF00), ("Gold", 0xFFD700), ("Yellow", 0xFFFF00),
        ("Lemon", 0xFFF44F), ("Olive", 0x808000), ("Lime", 0x32CD32), ("Chartreuse", 0x7FFF00), ("Green", 0x008000),
        ("Emerald", 0x50C878), ("Forest", 0x228B22), ("Mint", 0x98FF98), ("Sage", 0x9CAF88), ("Jade", 0x00A86B),
        ("Teal", 0x008080), ("Turquoise", 0x40E0D0), ("Aqua", 0x00FFFF), ("Cyan", 0x00B8D4), ("Aquamarine", 0x7FFFD4),
        ("Seafoam", 0x71EEB8), ("Sky", 0x87CEEB), ("Azure", 0x007FFF), ("Dodger blue", 0x1E90FF), ("Cornflower", 0x6495ED),
        ("Blue", 0x0000FF), ("Royal blue", 0x4169E1), ("Navy", 0x000080), ("Cobalt", 0x0047AB), ("Steel", 0x4682B4),
        ("Powder blue", 0xB0E0E6), ("Periwinkle", 0xCCCCFF), ("Indigo", 0x4B0082), ("Violet", 0x8A2BE2), ("Purple", 0x800080),
        ("Orchid", 0xDA70D6), ("Lavender", 0xE6E6FA), ("Lilac", 0xC8A2C8), ("Plum", 0x8E4585), ("Magenta", 0xFF00FF),
        ("Mauve", 0xE0B0FF), ("Amethyst", 0x9966CC), ("Grape", 0x8E24AA), ("Slate", 0x708090), ("Charcoal", 0x36454F),
        ("Silver", 0xC0C0C0), ("Grey", 0x808080), ("Lagoon", 0x20B2AA), ("Spring", 0x00FF7F), ("Fern", 0x4F7942),
        ("Moss", 0x8A9A5B), ("Honey", 0xE6A817), ("Sand", 0xE6D3A3), ("Ocean", 0x1F6FB2), ("Denim", 0x1560BD),
    ];

    /// <summary>
    /// Every colour of <see cref="Colours"/> with the name a screen reader says for it: the nearest unclaimed entry of a
    /// table of everyday colour names ("Rose", "Coral", "Salmon"). The editor draws the swatches as a radio group, so each
    /// needs a name a person can tell from its neighbours; a bare hex code is not one, and neither is "Red 2".
    /// </summary>
    internal static IReadOnlyList<AvatarSwatch> Swatches { get; } = BuildSwatches();

    private static int Distance(int first, int second)
    {
        int red = ((first >> 16) & 0xFF) - ((second >> 16) & 0xFF);
        int green = ((first >> 8) & 0xFF) - ((second >> 8) & 0xFF);
        int blue = (first & 0xFF) - (second & 0xFF);
        return (red * red) + (green * green) + (blue * blue);
    }

    private static List<AvatarSwatch> BuildSwatches()
    {
        HashSet<string> claimed = new(StringComparer.Ordinal);
        List<AvatarSwatch> swatches = [];
        foreach (MudColor colour in Colours)
        {
            int rgb = (colour.R << 16) | (colour.G << 8) | colour.B;
            string name = NamedColours
                .Where(candidate => !claimed.Contains(candidate.Name))
                .OrderBy(candidate => Distance(rgb, candidate.Rgb))
                .First().Name;
            _ = claimed.Add(name);
            swatches.Add(new AvatarSwatch(colour.ToString(MudColorOutputFormats.Hex), name));
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