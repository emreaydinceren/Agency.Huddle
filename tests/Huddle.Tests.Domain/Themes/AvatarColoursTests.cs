using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Themes;
using MudBlazor.Utilities;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Themes;

/// <summary>
/// Pins the monogram recipe (<see cref="AvatarColours"/>): in a Light Theme the Teammate's colour at 20% over Surface for the fill and
/// 75% toward TextPrimary for the letters; in a Dark Theme the colour at 35% over Surface and 75% toward white. The recipe replaces the
/// old black-or-white choice, so the proof that matters is that the letters clear the WCAG 4.5:1 text floor on the fill for any colour,
/// in the Huddle Light and Huddle Dark palettes.
/// </summary>
public sealed class AvatarColoursTests
{
    /// <summary>The WCAG 1.4.3 text minimum.</summary>
    private const double WcagTextMinimum = 4.5;

    /// <summary>The Lumen Light palette, the default Light Theme.</summary>
    private static MudBlazor.Palette LumenLight() => ThemeCatalog.BuiltIn.Single(static descriptor => descriptor.Id == "lumen-light").Theme.PaletteLight;

    /// <summary>The Lumen Dark palette, the default Dark Theme.</summary>
    private static MudBlazor.Palette LumenDark() => ThemeCatalog.BuiltIn.Single(static descriptor => descriptor.Id == "lumen-dark").Theme.PaletteDark;

    /// <summary>The Lumen Light and Lumen Dark Surface and TextPrimary pairs, with whether the Theme is a Dark one, by Theme name.</summary>
    public static TheoryData<string, string, string, bool> Palettes() => new()
    {
        { "Lumen Light", LumenLight().Surface.ToString(MudColorOutputFormats.Hex), LumenLight().TextPrimary.ToString(MudColorOutputFormats.Hex), false },
        { "Lumen Dark", LumenDark().Surface.ToString(MudColorOutputFormats.Hex), LumenDark().TextPrimary.ToString(MudColorOutputFormats.Hex), true },
    };

    /// <summary>The mix is plain sRGB interpolation, so its end points and midpoint are exact.</summary>
    [Fact]
    public void Mix_InterpolatesInSrgb()
    {
        MudColor black = new("#000000");
        MudColor white = new("#ffffff");

        Assert.Equal("#000000", AvatarColours.Mix(black, white, 0).ToString(MudColorOutputFormats.Hex));
        Assert.Equal("#ffffff", AvatarColours.Mix(black, white, 100).ToString(MudColorOutputFormats.Hex));
        Assert.Equal("#808080", AvatarColours.Mix(black, white, 50).ToString(MudColorOutputFormats.Hex));
    }

    /// <summary>The inline style hands only the Teammate's colour to <c>app.css</c> as the avatar's own primary, so each Theme resolves the recipe itself.</summary>
    [Fact]
    public void MonogramStyle_HandsTheColourToTheStylesheetRecipe()
    {
        string style = AvatarColours.MonogramStyle("#123456");

        Assert.Equal("--mud-palette-primary: #123456;", style);
    }

    /// <summary>The stylesheet states the Light recipe (20% fill, letters 75% toward TextPrimary) and the Dark one (35% fill, letters 75% toward white) that this class mirrors.</summary>
    [Fact]
    public void Stylesheet_StatesBothRecipes()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);

        Assert.Contains("background-color: color-mix(in srgb, var(--mud-palette-primary) 20%, var(--mud-palette-surface));", css, StringComparison.Ordinal); // contains-ok: haystack is the stylesheet source
        Assert.Contains("color: color-mix(in srgb, var(--mud-palette-primary) 25%, var(--mud-palette-text-primary));", css, StringComparison.Ordinal); // contains-ok: haystack is the stylesheet source
        Assert.Contains(":root:has(.theme-mode-dark) .mud-avatar.mud-avatar-filled.avatar-monogram", css, StringComparison.Ordinal); // contains-ok: haystack is the stylesheet source
        Assert.Contains("background-color: color-mix(in srgb, var(--mud-palette-primary) 35%, var(--mud-palette-surface));", css, StringComparison.Ordinal); // contains-ok: haystack is the stylesheet source
        Assert.Contains("color: color-mix(in srgb, var(--mud-palette-primary) 25%, var(--mud-palette-white));", css, StringComparison.Ordinal); // contains-ok: haystack is the stylesheet source
        Assert.Equal(20, AvatarColours.LightFillPercent);
        Assert.Equal(35, AvatarColours.DarkFillPercent);
        Assert.Equal(75, AvatarColours.LettersTowardPercent);
    }

    /// <summary>
    /// Every colour of the avatar palette, the Huddle accent of each Theme and the extremes (pure black, white, primaries and
    /// secondaries) give letters that clear 4.5:1 on the fill, in Huddle Light and in Huddle Dark.
    /// </summary>
    [Theory]
    [MemberData(nameof(Palettes))]
    public void Letters_OnTheFill_ClearTheTextFloor_ForASampleOfColours(string theme, string surfaceHex, string textHex, bool dark)
    {
        MudColor surface = new(surfaceHex);
        MudColor text = new(textHex);
        List<MudColor> sample = [.. AvatarPalette.Colours];
        foreach (string hex in new[] { "#5b64c8", "#8f97e6", "#000000", "#ffffff", "#ff0000", "#00ff00", "#0000ff", "#ffff00", "#00ffff", "#ff00ff", "#808080" })
        {
            sample.Add(new MudColor(hex));
        }

        foreach (MudColor colour in sample)
        {
            double ratio = ContrastColour.Ratio(AvatarColours.Letters(colour, text, dark), AvatarColours.Fill(colour, surface, dark));

            Assert.True(ratio >= WcagTextMinimum, $"{theme}: {colour.ToString(MudColorOutputFormats.Hex)} gives {ratio:0.00}:1.");
        }
    }

    /// <summary>The claim is not about a sample: sweeping the sRGB cube at step 17 (4096 colours) the letters clear 4.5:1 in both Themes.</summary>
    [Theory]
    [MemberData(nameof(Palettes))]
    public void Letters_OnTheFill_ClearTheTextFloor_AcrossTheSrgbCube(string theme, string surfaceHex, string textHex, bool dark)
    {
        MudColor surface = new(surfaceHex);
        MudColor text = new(textHex);

        for (int red = 0; red <= 255; red += 17)
        {
            for (int green = 0; green <= 255; green += 17)
            {
                for (int blue = 0; blue <= 255; blue += 17)
                {
                    MudColor colour = new(r: (byte)red, g: (byte)green, b: (byte)blue, a: 255);
                    double ratio = ContrastColour.Ratio(AvatarColours.Letters(colour, text, dark), AvatarColours.Fill(colour, surface, dark));

                    Assert.True(ratio >= WcagTextMinimum, $"{theme}: {colour.ToString(MudColorOutputFormats.Hex)} gives {ratio:0.00}:1.");
                }
            }
        }
    }

    /// <summary>A Dark Theme gets a deep fill with light letters, a Light Theme a pale fill with dark letters, from the one recipe.</summary>
    [Fact]
    public void DarkTheme_GivesADeepFillWithLightLetters()
    {
        MudColor accent = new("#5b64c8");

        MudColor lightFill = AvatarColours.Fill(accent, LumenLight().Surface, dark: false);
        MudColor lightLetters = AvatarColours.Letters(accent, LumenLight().TextPrimary, dark: false);
        MudColor darkFill = AvatarColours.Fill(accent, LumenDark().Surface, dark: true);
        MudColor darkLetters = AvatarColours.Letters(accent, LumenDark().TextPrimary, dark: true);

        Assert.True(ContrastColour.RelativeLuminance(lightFill) > ContrastColour.RelativeLuminance(lightLetters));
        Assert.True(ContrastColour.RelativeLuminance(darkFill) < ContrastColour.RelativeLuminance(darkLetters));
    }
}
