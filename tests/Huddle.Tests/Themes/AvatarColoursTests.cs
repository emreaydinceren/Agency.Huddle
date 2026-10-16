using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Themes;
using MudBlazor.Utilities;

namespace Agency.Huddle.Tests.Themes;

/// <summary>
/// Pins the monogram recipe (<see cref="AvatarColours"/>): the Teammate's colour at 20% over Surface for the fill and 75% toward
/// TextPrimary for the letters. The recipe replaces the old black-or-white choice, so the proof that matters is that the letters
/// clear the WCAG 4.5:1 text floor on the fill for any colour, in the Huddle Light and Huddle Dark palettes.
/// </summary>
public sealed class AvatarColoursTests
{
    /// <summary>The WCAG 1.4.3 text minimum.</summary>
    private const double WcagTextMinimum = 4.5;

    /// <summary>The Huddle Light and Huddle Dark Surface and TextPrimary pairs, by Theme name.</summary>
    public static TheoryData<string, string, string> Palettes() => new()
    {
        { "Huddle Light", HuddleTheme.Light().Surface.ToString(MudColorOutputFormats.Hex), HuddleTheme.Light().TextPrimary.ToString(MudColorOutputFormats.Hex) },
        { "Huddle Dark", HuddleTheme.Dark().Surface.ToString(MudColorOutputFormats.Hex), HuddleTheme.Dark().TextPrimary.ToString(MudColorOutputFormats.Hex) },
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

    /// <summary>The inline style states the recipe as <c>color-mix</c> against the Theme's tokens, so each Theme resolves it itself.</summary>
    [Fact]
    public void MonogramStyle_StatesTheRecipeAgainstTheThemeTokens()
    {
        string style = AvatarColours.MonogramStyle("#123456");

        Assert.Equal(
            "background: color-mix(in srgb, #123456 20%, var(--mud-palette-surface)); color: color-mix(in srgb, #123456 25%, var(--mud-palette-text-primary));",
            style);
    }

    /// <summary>
    /// Every colour of the avatar palette, the Huddle accent of each Theme and the extremes (pure black, white, primaries and
    /// secondaries) give letters that clear 4.5:1 on the fill, in Huddle Light and in Huddle Dark.
    /// </summary>
    [Theory]
    [MemberData(nameof(Palettes))]
    public void Letters_OnTheFill_ClearTheTextFloor_ForASampleOfColours(string theme, string surfaceHex, string textHex)
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
            double ratio = ContrastColour.Ratio(AvatarColours.Letters(colour, text), AvatarColours.Fill(colour, surface));

            Assert.True(ratio >= WcagTextMinimum, $"{theme}: {colour.ToString(MudColorOutputFormats.Hex)} gives {ratio:0.00}:1.");
        }
    }

    /// <summary>The claim is not about a sample: sweeping the sRGB cube at step 17 (4096 colours) the letters clear 4.5:1 in both Themes.</summary>
    [Theory]
    [MemberData(nameof(Palettes))]
    public void Letters_OnTheFill_ClearTheTextFloor_AcrossTheSrgbCube(string theme, string surfaceHex, string textHex)
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
                    double ratio = ContrastColour.Ratio(AvatarColours.Letters(colour, text), AvatarColours.Fill(colour, surface));

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

        MudColor lightFill = AvatarColours.Fill(accent, HuddleTheme.Light().Surface);
        MudColor lightLetters = AvatarColours.Letters(accent, HuddleTheme.Light().TextPrimary);
        MudColor darkFill = AvatarColours.Fill(accent, HuddleTheme.Dark().Surface);
        MudColor darkLetters = AvatarColours.Letters(accent, HuddleTheme.Dark().TextPrimary);

        Assert.True(ContrastColour.RelativeLuminance(lightFill) > ContrastColour.RelativeLuminance(lightLetters));
        Assert.True(ContrastColour.RelativeLuminance(darkFill) < ContrastColour.RelativeLuminance(darkLetters));
    }
}
