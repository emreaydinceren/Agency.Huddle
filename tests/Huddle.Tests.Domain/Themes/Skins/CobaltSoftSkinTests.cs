using Agency.Huddle.App.Themes;
using Agency.Huddle.App.Themes.Skins;

namespace Agency.Huddle.Tests.Themes.Skins;

/// <summary>Pins the Cobalt Soft skin pair to its values sheet: the traits, the raised dark ground and only the tokens that differ.</summary>
public sealed class CobaltSoftSkinTests
{
    /// <summary>Returns the skin with the given id.</summary>
    private static Skin Load(string id) => Assert.Single(SkinLoader.LoadAll(), candidate => string.Equals(candidate.Id, id, StringComparison.Ordinal));

    /// <summary>Both modes carry the nine traits the sheet names: row messages, text send, glyph status, upper labels, box composer, tint nav, flat surface, chip marks and plain reading.</summary>
    [Theory]
    [InlineData("cobalt-soft-light")]
    [InlineData("cobalt-soft-dark")]
    public void CobaltSoft_TraitsMatchSheet(string id)
    {
        SkinTraits traits = Load(id).Traits;

        Assert.Equal(MessageLayout.Row, traits.Message);
        Assert.Equal(SendStyle.Text, traits.Send);
        Assert.Equal(StatusStyle.Glyph, traits.Status);
        Assert.Equal(LabelCase.Upper, traits.Labels);
        Assert.Equal(ComposerStyle.Box, traits.Composer);
        Assert.Equal(NavStyle.Tint, traits.Nav);
        Assert.Equal(SurfaceStyle.Flat, traits.Surface);
        Assert.Equal(MarkStyle.Chip, traits.Marks);
        Assert.Equal(ReadingStyle.Plain, traits.Reading);
    }

    /// <summary>The labelled Send is the mockup's filled primary button (<c>btn-primary</c>) in both modes, not a text-only one.</summary>
    [Theory]
    [InlineData("cobalt-soft-light")]
    [InlineData("cobalt-soft-dark")]
    public void CobaltSoft_SendIsAFilledPrimaryButton(string id)
    {
        Skin skin = Load(id);

        Assert.Equal(("palette.primary", "palette.primaryContrastText"), (skin.Tokens["send.bg"], skin.Tokens["send.fg"]));
    }

    /// <summary>
    /// Every token a Cobalt Soft file sets is a registry key whose value differs from the registry default for that mode.
    /// The one allowed repeat is a dark value that equals the dark default but must stay because the included light file
    /// sets a different value for the key (a dark file cannot unset an inherited token).
    /// </summary>
    [Theory]
    [InlineData("cobalt-soft-light")]
    [InlineData("cobalt-soft-dark")]
    public void CobaltSoft_SetsOnlyTokensThatDiffer(string id)
    {
        Skin skin = Load(id);

        Skin light = Load("cobalt-soft-light");
        Assert.NotEmpty(skin.Tokens);
        foreach (KeyValuePair<string, string> pair in skin.Tokens)
        {
            Token token = Assert.Single(SkinTokens.All, candidate => string.Equals(candidate.Key, pair.Key, StringComparison.Ordinal));
            string fallback = skin.Mode == ThemeMode.Dark ? token.DarkDefault : token.LightDefault;
            bool overridesLight = skin.Mode == ThemeMode.Dark && light.Tokens.ContainsKey(pair.Key);
            Assert.False(!overridesLight &&string.Equals(fallback, pair.Value, StringComparison.Ordinal), $"{id}: '{pair.Key}' repeats its default '{fallback}'.");
        }
    }

    /// <summary>The dark raised ground is the mockup's lighter step, not the page colour, while the page stays the deep navy.</summary>
    [Fact]
    public void CobaltSoft_DarkRestoresRaisedGround()
    {
        Skin dark = Load("cobalt-soft-dark");

        Assert.Equal("#131a2b", dark.Palette["surface"]);
        Assert.Equal("#0e1220", dark.Palette["background"]);
        Assert.Equal("palette.background", dark.Tokens["ground.page"]);
    }

    /// <summary>The shape fields: Inter for the interface, an 8px radius and the sheet's three-step shadow ladder.</summary>
    [Fact]
    public void CobaltSoft_SetsFaceRadiusAndShadows()
    {
        Skin light = Load("cobalt-soft-light");

        Assert.Equal("8px", light.Radius);
        Assert.Equal("Inter", light.Faces.Ui[0]);
        Assert.Equal("1.5", light.Typography.LineHeight);
        Assert.Equal("0 1px 2px rgba(12,19,37,.06)", light.Shadows[1]);
        Assert.Equal("0 1px 2px rgba(0,0,0,.4)", Load("cobalt-soft-dark").Shadows[1]);
    }

    /// <summary>Cobalt Soft's note headings are its tight, heavy setting: 650 weight, a 30px h1 at -0.025em (side-by-side audit, CL-04).</summary>
    [Fact]
    public void CobaltSoft_NoteHeadingsAreTightAndHeavy()
    {
        Skin light = Load("cobalt-soft-light");

        Assert.Equal("650", light.Tokens["note.heading.weight"]);
        Assert.Equal("1.875em", light.Tokens["note.h1.size"]);
        Assert.Equal("-0.025em", light.Tokens["note.h1.tracking"]);
    }
}
