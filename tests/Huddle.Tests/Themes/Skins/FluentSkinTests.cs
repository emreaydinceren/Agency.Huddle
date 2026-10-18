using Agency.Huddle.App.Themes;
using Agency.Huddle.App.Themes.Skins;

namespace Agency.Huddle.Tests.Themes.Skins;

/// <summary>
/// Pins the Fluent skin pair to its values sheet (<c>values-fluent.md</c>): the nine traits, the radius, the type, the
/// elevation ramp and the tokens that differ from the registry default.
/// </summary>
public sealed class FluentSkinTests
{
    private static Skin Load(string id) =>
        SkinLoader.LoadAll().First(skin => string.Equals(skin.Id, id, StringComparison.Ordinal));

    /// <summary>Both Fluent skins set the nine traits the sheet names, including the three phase 4 variants.</summary>
    /// <param name="id">The skin id.</param>
    [Theory]
    [InlineData("fluent-light")]
    [InlineData("fluent-dark")]
    public void Fluent_TraitsMatchSheet(string id)
    {
        SkinTraits traits = Load(id).Traits;

        Assert.Equal(MessageLayout.Bubble, traits.Message);
        Assert.Equal(SendStyle.Icon, traits.Send);
        Assert.Equal(StatusStyle.Glyph, traits.Status);
        Assert.Equal(LabelCase.Sentence, traits.Labels);
        Assert.Equal(ComposerStyle.Underlined, traits.Composer);
        Assert.Equal(NavStyle.Bar, traits.Nav);
        Assert.Equal(SurfaceStyle.Flat, traits.Surface);
        Assert.Equal(MarkStyle.Chip, traits.Marks);
        Assert.Equal(ReadingStyle.Plain, traits.Reading);
    }

    /// <summary>Every token a Fluent skin sets differs from the registry default for that mode, so no line is dead weight.</summary>
    /// <param name="id">The skin id.</param>
    [Theory]
    [InlineData("fluent-light")]
    [InlineData("fluent-dark")]
    public void Fluent_SetsOnlyTokensThatDiffer(string id)
    {
        Skin skin = Load(id);
        bool dark = skin.Mode == ThemeMode.Dark;

        Assert.NotEmpty(skin.Tokens);
        foreach (KeyValuePair<string, string> own in skin.Tokens)
        {
            Token token = SkinTokens.All.First(candidate => string.Equals(candidate.Key, own.Key, StringComparison.Ordinal));
            string fallback = dark ? token.DarkDefault : token.LightDefault;
            Assert.False(string.Equals(own.Value, fallback, StringComparison.Ordinal), $"{id}: {own.Key} repeats the registry default '{fallback}'.");
        }
    }

    /// <summary>The Fluent look: one 6px radius, the Segoe face with the default Inter fallback, the Fluent elevation ramp.</summary>
    [Fact]
    public void Fluent_SetsRadiusFacesAndShadows()
    {
        Skin light = Load("fluent-light");
        Skin dark = Load("fluent-dark");

        Assert.Equal("6px", light.Radius);
        Assert.Contains("Segoe UI", light.Faces.Ui);
        Assert.Contains("Inter", light.Faces.Ui);
        Assert.Equal("1.43", light.Typography.LineHeight);
        Assert.Equal("0 0 2px rgba(0,0,0,.12), 0 1px 2px rgba(0,0,0,.14)", light.Shadows[1]);
        Assert.Equal("0 0 2px rgba(0,0,0,.24), 0 1px 2px rgba(0,0,0,.28)", dark.Shadows[1]);
        Assert.Equal(25, light.Shadows.Count);
        Assert.Equal(25, dark.Shadows.Count);
    }

    /// <summary>The sheet's decisions: the chrome ground, the info tone as primary, the dark filled button and the bottom-edge focus.</summary>
    [Fact]
    public void Fluent_TokensCarrySheetDecisions()
    {
        Skin light = Load("fluent-light");
        Skin dark = Load("fluent-dark");

        Assert.Equal("#e8ecf1", light.Tokens["ground.rail"]);
        Assert.Equal("#0f0f0f", dark.Tokens["ground.rail"]);
        Assert.Equal("ref(accent.ink)", dark.Tokens["tone.info.ink"]);
        Assert.Equal("#115ea3", dark.Tokens["send.bg"]);
        Assert.Equal("palette.primary", light.Tokens["send.bg"]);
        Assert.Equal("inset 0 -1px 0 0 palette.primary", dark.Tokens["field.focus.shadow"]);
        Assert.Equal(light.Tokens["shadow.card"], light.Tokens["shadow.card.hover"]);
    }
}
