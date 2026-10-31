using MudBlazor.Utilities;
using Agency.Huddle.App.Avatars;

namespace Agency.Huddle.Tests.Avatars;

/// <summary>
/// <see cref="AvatarPalette"/>: the one palette the avatar editor offers as swatches and the one a new Agent's
/// random background is drawn from, so a random colour is always a colour the editor can show as selected.
/// </summary>
public sealed class AvatarPaletteTests
{
    /// <summary>The hex of every colour in the palette, the way the avatar store holds a background.</summary>
    private static HashSet<string> PaletteHex() =>
        [.. AvatarPalette.Colours.Select(static colour => colour.ToString(MudColorOutputFormats.Hex))];

    /// <summary>The palette has swatches to offer.</summary>
    [Fact]
    public void Colours_AreNotEmpty()
    {
        Assert.NotEmpty(AvatarPalette.Colours);
    }

    /// <summary>Every pick is a colour from the palette, written as the six-digit hex the store holds.</summary>
    [Fact]
    public void Pick_ReturnsAPaletteColourAsHex()
    {
        HashSet<string> hex = PaletteHex();

        for (int seed = 0; seed < 200; seed++)
        {
            string picked = AvatarPalette.Pick(new Random(seed));

            Assert.Contains(picked, hex);
            Assert.Matches("^#[0-9a-fA-F]{6}$", picked);
        }
    }

    /// <summary>The same random source picks the same colour, so a pick depends on nothing but the random it is given.</summary>
    [Fact]
    public void Pick_IsDeterministicForASeed()
    {
        Assert.Equal(AvatarPalette.Pick(new Random(42)), AvatarPalette.Pick(new Random(42)));
    }

    /// <summary>Across many seeds the pick varies: it is not a constant.</summary>
    [Fact]
    public void Pick_VariesAcrossSeeds()
    {
        HashSet<string> picked = [.. Enumerable.Range(0, 200).Select(static seed => AvatarPalette.Pick(new Random(seed)))];

        Assert.True(picked.Count > 5, $"Expected a spread of colours, got {picked.Count}.");
    }
}
