using Agency.Huddle.App.Themes;
using MudBlazor.Utilities;

namespace Agency.Huddle.Tests.Themes;

/// <summary>
/// Pins <see cref="ContrastColour"/>'s WCAG 2.1 maths: <see cref="RelativeLuminance_PureChannels_MatchWcagPublishedValues(byte, byte, byte, double)"/>
/// checks the published luminance coefficients directly, known hex vectors pin
/// <see cref="ContrastColour.ReadableForeground(MudColor)"/> against colours this codebase already
/// reasons about in prose, and <see cref="ReadableForeground_AcrossTheSrgbCube_AlwaysClearsTheWcagFloor"/>
/// mechanises the sufficiency proof from <see cref="ContrastColour"/>'s own doc comment: black or white
/// always reaches 4.5:1 against any sRGB colour, so there is no background this suite lets through
/// unproven.
/// </summary>
public sealed class ContrastColourTests
{
    /// <summary>The WCAG 1.4.3 text minimum every <see cref="ContrastColour.ReadableForeground(MudColor)"/> result must clear.</summary>
    private const double WcagTextMinimum = 4.5;

    /// <summary>
    /// The sufficiency claim in <see cref="ContrastColour"/>'s doc comment, mechanised: sweeping the
    /// sRGB cube at step 17 (16 steps per channel, 4096 colours) plus all 256 greys, the foreground
    /// <see cref="ContrastColour.ReadableForeground(MudColor)"/> picks clears 4.5:1 against every single
    /// one. This one test is the whole claim - if it ever fails, black-or-white has stopped being a
    /// sufficient choice for some background colour.
    /// </summary>
    [Fact]
    public void ReadableForeground_AcrossTheSrgbCube_AlwaysClearsTheWcagFloor()
    {
        List<MudColor> backgrounds = [];
        for (var red = 0; red <= 255; red += 17)
        {
            for (var green = 0; green <= 255; green += 17)
            {
                for (var blue = 0; blue <= 255; blue += 17)
                {
                    backgrounds.Add(new MudColor(r: (byte)red, g: (byte)green, b: (byte)blue, a: 255));
                }
            }
        }

        for (var grey = 0; grey <= 255; grey++)
        {
            backgrounds.Add(new MudColor(r: (byte)grey, g: (byte)grey, b: (byte)grey, a: 255));
        }

        Assert.All(backgrounds, background =>
        {
            var foreground = ContrastColour.ReadableForeground(background);
            var ratio = ContrastColour.Ratio(foreground, background);
            Assert.True(ratio >= WcagTextMinimum, $"#{background.R:X2}{background.G:X2}{background.B:X2} only reached {ratio:F2}:1.");
        });
    }

    /// <summary>White-on-white has no contrast at all, so the readable foreground must be black.</summary>
    [Fact]
    public void ReadableForeground_White_ReturnsBlack()
    {
        MudColor background = "#ffffff";

        var foreground = ContrastColour.ReadableForeground(background);

        Assert.Equal("#000000FF", foreground.ToString(MudColorOutputFormats.HexA), ignoreCase: true);
    }

    /// <summary>Black-on-black has no contrast at all, so the readable foreground must be white.</summary>
    [Fact]
    public void ReadableForeground_Black_ReturnsWhite()
    {
        MudColor background = "#000000";

        var foreground = ContrastColour.ReadableForeground(background);

        Assert.Equal("#FFFFFFFF", foreground.ToString(MudColorOutputFormats.HexA), ignoreCase: true);
    }

    /// <summary>Huddle Light's <c>Primary</c> (<c>#4a154b</c>) is dark enough that white is the readable foreground - the choice this theme's own <c>PrimaryContrastText</c> already makes.</summary>
    [Fact]
    public void ReadableForeground_HuddleLightPrimary_ReturnsWhite()
    {
        MudColor background = "#4a154b";

        var foreground = ContrastColour.ReadableForeground(background);

        Assert.Equal("#FFFFFFFF", foreground.ToString(MudColorOutputFormats.HexA), ignoreCase: true);
    }

    /// <summary>
    /// Huddle Dark's <c>Primary</c> (<c>#c07bc3</c>) is the exact colour <c>HuddleTheme.cs</c> reasons
    /// about in prose: black reaches 6.86:1 against it, comfortably past the 4.5:1 floor, while white
    /// only reaches 3.06:1. This test is what stops that comment and <see cref="ContrastColour"/>
    /// drifting apart.
    /// </summary>
    [Fact]
    public void ReadableForeground_HuddleDarkPrimary_ReturnsBlackAt686ToOne()
    {
        MudColor background = "#c07bc3";

        var foreground = ContrastColour.ReadableForeground(background);
        var ratio = ContrastColour.Ratio(foreground, background);

        Assert.Equal("#000000FF", foreground.ToString(MudColorOutputFormats.HexA), ignoreCase: true);
        Assert.Equal(6.86, ratio, precision: 2);
    }

    /// <summary>
    /// <see cref="ContrastColour.RelativeLuminance(MudColor)"/> against WCAG's own published relative
    /// luminance coefficients for pure red, green and blue - <c>0.2126</c>, <c>0.7152</c> and
    /// <c>0.0722</c> respectively, since each pure channel's own luminance formula reduces to its
    /// coefficient.
    /// </summary>
    /// <param name="red">The red channel of the pure colour under test.</param>
    /// <param name="green">The green channel of the pure colour under test.</param>
    /// <param name="blue">The blue channel of the pure colour under test.</param>
    /// <param name="expectedLuminance">WCAG's published relative luminance for that pure colour.</param>
    [Theory]
    [InlineData((byte)255, (byte)0, (byte)0, 0.2126)]
    [InlineData((byte)0, (byte)255, (byte)0, 0.7152)]
    [InlineData((byte)0, (byte)0, (byte)255, 0.0722)]
    public void RelativeLuminance_PureChannels_MatchWcagPublishedValues(byte red, byte green, byte blue, double expectedLuminance)
    {
        MudColor colour = new(r: red, g: green, b: blue, a: (byte)255);

        var luminance = ContrastColour.RelativeLuminance(colour);

        Assert.Equal(expectedLuminance, luminance, precision: 4);
    }
}
