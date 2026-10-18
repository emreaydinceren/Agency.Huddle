using System.Globalization;
using Agency.Huddle.App.Themes;
using Agency.Huddle.App.Themes.Skins;
using MudBlazor.Utilities;

namespace Agency.Huddle.Tests.Themes.Skins;

/// <summary>
/// Guards the legibility of every shipped skin: the text pairs that must reach 4.5:1 and the keyboard focus ring that must
/// reach 3:1 against the two grounds it sits on. The pairs are <see cref="SkinContrastPairs"/>, which the Theme lab shows
/// live; the token expressions are resolved to colours by <see cref="SkinColourResolver"/>.
/// </summary>
public sealed class SkinContrastTests
{
    /// <summary>
    /// TextPrimary and TextSecondary read on every ground token, <c>current.foreground</c> on <c>current.background</c>,
    /// <c>accent.ink</c> on <c>selected.background</c> and PrimaryContrastText on Primary, all at 4.5:1 or better.
    /// </summary>
    [Fact]
    public void Skins_TextContrast()
    {
        Assert.Empty(Failures(static pair => pair.Minimum >= SkinContrastPairs.TextMinimum));
    }

    /// <summary>The <c>focus</c> ring reads at 3:1 or better on <c>ground.page</c> and on <c>ground.raised</c>.</summary>
    [Fact]
    public void Skins_FocusVisible()
    {
        Assert.Empty(Failures(static pair => pair.Minimum < SkinContrastPairs.TextMinimum));
    }

    /// <summary>The shared pair list holds both kinds, so neither test above passes by checking nothing.</summary>
    [Fact]
    public void ContrastPairs_HoldTextAndFocusPairs()
    {
        Assert.Contains(SkinContrastPairs.All, static pair => pair.Minimum >= SkinContrastPairs.TextMinimum);
        Assert.Contains(SkinContrastPairs.All, static pair => pair.Minimum < SkinContrastPairs.TextMinimum);
        Assert.Equal("textPrimary on ground.page", SkinContrastPairs.All[0].Name);
    }

    /// <summary>The resolver itself: a nested mix, a ref chain and a palette override resolve to the expected colour.</summary>
    [Fact]
    public void Resolver_MixesInSrgbAndFollowsRefs()
    {
        SkinColourResolver resolver = new(ProbeSkin());

        MudColor mixed = resolver.Evaluate("mix(#000000 25%, #ffffff)");
        Assert.Equal(191, mixed.R);
        Assert.Equal(191, mixed.G);
        Assert.Equal(191, mixed.B);

        Assert.Equal(resolver.Token("accent.ink").ToString(MudColorOutputFormats.Hex), resolver.Token("current.foreground").ToString(MudColorOutputFormats.Hex));
        Assert.Equal("#ffffff", resolver.Palette("surface").ToString(MudColorOutputFormats.Hex));
    }

    /// <summary>Palette and token overrides given to the resolver win over the skin's own values.</summary>
    [Fact]
    public void Resolver_OverridesWinOverTheSkin()
    {
        Skin skin = ProbeSkin();
        Dictionary<string, string> palette = new(StringComparer.Ordinal) { ["surface"] = "#102030" };
        Dictionary<string, string> tokens = new(StringComparer.Ordinal) { ["ground.page"] = "#405060" };
        SkinColourResolver resolver = new(skin, palette, tokens);

        Assert.Equal("#102030", resolver.Palette("surface").ToString(MudColorOutputFormats.Hex));
        Assert.Equal("#405060", resolver.Token("ground.page").ToString(MudColorOutputFormats.Hex));
        Assert.Equal("#102030", resolver.Token("ground.sidebar").ToString(MudColorOutputFormats.Hex));
    }

    /// <summary>
    /// A light skin of the tests' own that sets only Surface, so the registry defaults (accent.ink and current.foreground
    /// one recipe, every ground Surface) are what resolve, whatever the shipped skins set.
    /// </summary>
    private static Skin ProbeSkin()
    {
        Dictionary<string, string> files = new(StringComparer.Ordinal)
        {
            ["probe.light.json"] = """{ "id": "probe", "label": "Probe", "mode": "light", "order": 1, "palette": { "surface": "#ffffff" } }""",
        };
        return Assert.Single(SkinLoader.LoadAll(files));
    }

    private static List<string> Failures(Func<SkinContrastPair, bool> include)
    {
        List<string> failures = [];
        foreach (Skin skin in SkinLoader.LoadAll())
        {
            SkinColourResolver resolver = new(skin);
            foreach (SkinContrastPair pair in SkinContrastPairs.All.Where(include))
            {
                double ratio = ContrastColour.Ratio(resolver.Evaluate(pair.Foreground), resolver.Evaluate(pair.Background));
                if (ratio < pair.Minimum)
                {
                    failures.Add(string.Create(CultureInfo.InvariantCulture, $"{skin.Id}: {pair.Name} is {ratio:0.00}:1, needs {pair.Minimum:0.0}:1"));
                }
            }
        }

        return failures;
    }
}
