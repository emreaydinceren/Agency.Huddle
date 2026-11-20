using Agency.Huddle.App.Themes.Skins;
using MudBlazor.Utilities;

namespace Agency.Huddle.Tests.Themes.Skins;

/// <summary>
/// <see cref="SkinColours"/>, the test helper that turns every skin token into the concrete sRGB colour a browser would
/// paint: coverage of all eight Themes, a few known values, the mix and transparency arithmetic on a skin of the tests'
/// own, and agreement with the app's byte-rounded <see cref="SkinColourResolver"/> where both can answer.
/// </summary>
public sealed class SkinColoursTests
{
    /// <summary>The four rungs of the ink ladder.</summary>
    private static readonly string[] Rungs = ["ink.strip", "ink.wash", "ink.line", "ink.field"];

    /// <summary>Every shipped Theme in both modes is covered, and each resolves its four ink rungs and its grounds.</summary>
    [Fact]
    public void All_CoversEightThemes_AndResolvesTheInkLadder()
    {
        Assert.Equal(8, SkinColours.All.Count);
        foreach (SkinColours colours in SkinColours.All)
        {
            IReadOnlyDictionary<string, SrgbColour> resolved = colours.AllColours();
            foreach (string rung in Rungs)
            {
                Assert.True(resolved.ContainsKey(rung), $"{colours.Id}: {rung} did not resolve");
            }

            Assert.Equal(Rungs, colours.InkRungs);
            Assert.True(resolved.ContainsKey("ground.page"), colours.Id);
            Assert.True(resolved.Count > 80, $"{colours.Id}: only {resolved.Count} colour tokens resolved");
        }
    }

    /// <summary>Lumen Light sets its Line rung, <c>ink.line</c>, to <c>palette.dividerLight</c> (#e7e7eb), and the region (Chrome) line is a rung stronger, so Line &lt; Chrome holds on the page (#742).</summary>
    [Fact]
    public void LumenLight_InkLineIsTheDividerLight()
    {
        SkinColours lumen = SkinColours.All.Single(static colours => colours.Id == "lumen-light");

        Assert.Equal("#e7e7eb", lumen.Token("ink.line")?.Hex);
        Assert.NotEqual(lumen.Token("line.region"), lumen.Token("ink.line"));
        Assert.Equal("#f0f0f2", lumen.Token("ink.wash")?.Hex);
        Assert.True(lumen.Difference("ink.line", "line.region") > 2);
        Assert.True(lumen.Difference("ink.line", "ink.field") > 2);
    }

    /// <summary>A dark skin resolves against the dark palette: its surface is #111117, not white, and the registry's raised ground is that surface.</summary>
    [Fact]
    public void Dark_UsesTheDarkPalette()
    {
        SkinColours light = SkinColours.All.Single(static colours => colours.Id == "lumen-light");
        SkinColours dark = SkinColours.All.Single(static colours => colours.Id == "lumen-dark");

        Assert.Equal("#ffffff", light.Surface.Hex);
        Assert.Equal("#111117", dark.Surface.Hex);
        Assert.Equal(dark.Surface, dark.Palette("surface"));
        Assert.Equal(dark.Surface, dark.Token("ground.raised"));
        Assert.NotEqual(light.Token("ink.field"), dark.Token("ink.field"));
    }

    /// <summary>
    /// A mix of two opaque colours is carried unrounded and rounded once at the end (229.5 prints as e6, where a
    /// byte-rounding resolver would already have chosen), an <c>rgba()</c> token is composited over the surface, and
    /// <c>alpha()</c> mixes with <c>transparent</c>.
    /// </summary>
    [Fact]
    public void Resolves_MixAlphaAndTransparency()
    {
        SkinColours colours = Probe(
            """
            "ink.line": "mix(palette.textPrimary 10%, palette.surface)",
            "line.region": "ref(ink.line)",
            "ink.field": "rgba(0, 0, 0, .5)",
            "ink.wash": "alpha(palette.textPrimary 25%)",
            "ink.strip": "transparent",
            "scrim": "mix(#ff0000 50%, transparent)"
            """);

        Assert.Equal("#e6e6e6", colours.Token("ink.line")?.Hex);
        Assert.Equal(colours.Token("ink.line"), colours.Token("line.region"));
        Assert.Equal("#808080", colours.Token("ink.field")?.Hex);
        Assert.Equal("#bfbfbf", colours.Token("ink.wash")?.Hex);
        Assert.Equal("#ffffff", colours.Token("ink.strip")?.Hex);
        Assert.Equal("#ff8080", colours.Token("scrim")?.Hex);
    }

    /// <summary>Tokens whose value is not a colour (a weight, a length, <c>none</c>, a <c>mud()</c> variable, a shadow list) resolve to nothing.</summary>
    [Fact]
    public void NonColourTokens_ResolveToNull()
    {
        SkinColours colours = SkinColours.All[0];

        Assert.Null(colours.Token("list.title.weight"));
        Assert.Null(colours.Token("shadow.card"));
        Assert.Null(colours.Token("radius.card"));
        Assert.Null(colours.Token("board.needs.halo"));
        Assert.DoesNotContain("shadow.card", colours.ColourKeys);
        Assert.Throws<InvalidOperationException>(() => colours.Token("no.such.token"));
    }

    /// <summary>
    /// Where the app's own resolver can answer (opaque tokens), both agree to within the byte rounding the app's resolver
    /// applies at every mix step, so the helper extends it rather than disagreeing with it.
    /// </summary>
    [Fact]
    public void AgreesWithTheAppResolver_WithinByteRounding()
    {
        string[] keys = ["ground.page", "ground.raised", "accent.ink", "current.background", "selected.background", "text.subtle", "tone.primary.soft", "tone.warning.line", "ink.field"];
        List<string> failures = [];
        foreach (SkinColours colours in SkinColours.All)
        {
            SkinColourResolver resolver = new(SkinLoader.LoadAll().Single(skin => skin.Id == colours.Id));
            foreach (string key in keys)
            {
                MudColor expected = resolver.Token(key);
                SrgbColour actual = colours.Token(key) ?? throw new InvalidOperationException(key);
                if (Math.Abs(expected.R - actual.R) > 2 || Math.Abs(expected.G - actual.G) > 2 || Math.Abs(expected.B - actual.B) > 2)
                {
                    failures.Add($"{colours.Id} {key}: app {expected.ToString(MudColorOutputFormats.Hex)}, helper {actual.Hex}");
                }
            }
        }

        Assert.Empty(failures);
    }

    /// <summary>A light skin of the tests' own with a pure black and white palette and the given tokens, so the arithmetic is checkable by hand.</summary>
    /// <param name="tokens">The body of the skin's <c>tokens</c> object.</param>
    private static SkinColours Probe(string tokens)
    {
        Dictionary<string, string> files = new(StringComparer.Ordinal)
        {
            ["probe.light.json"] = "{ \"id\": \"probe\", \"label\": \"Probe\", \"mode\": \"light\", \"order\": 1, "
                + "\"palette\": { \"surface\": \"#ffffff\", \"textPrimary\": \"#000000\" }, \"tokens\": { " + tokens + " } }",
        };
        return new SkinColours(Assert.Single(SkinLoader.LoadAll(files)));
    }
}
