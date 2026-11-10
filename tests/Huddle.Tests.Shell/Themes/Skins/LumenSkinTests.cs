using System.Text.Json;
using Agency.Huddle.App.Themes.Skins;
using Agency.Huddle.Tests.Ui;

namespace Agency.Huddle.Tests.Themes.Skins;

/// <summary>Pins the Lumen skin files to the Lumen values sheet: its traits, its palette moves, and the tokens it sets.</summary>
public sealed class LumenSkinTests
{
    private static Skin Load(string id) =>
        Assert.Single(SkinLoader.LoadAll(), candidate => string.Equals(candidate.Id, id, StringComparison.Ordinal));

    private static Dictionary<string, string> OwnTokens(string file)
    {
        string path = CssSource.RepoPath("src", "Huddle.App", "Themes", "Skins", file);
        using JsonDocument document = JsonDocument.Parse(System.IO.File.ReadAllText(path));
        Dictionary<string, string> own = new(StringComparer.Ordinal);
        if (document.RootElement.TryGetProperty("tokens", out JsonElement tokens))
        {
            foreach (JsonProperty entry in tokens.EnumerateObject())
            {
                own[entry.Name] = entry.Value.GetString() ?? string.Empty;
            }
        }

        return own;
    }

    /// <summary>Both Lumen skins choose the nine traits the sheet lists, with the three phase 4 variants among them.</summary>
    [Fact]
    public void Lumen_TraitsMatchSheet()
    {
        SkinTraits expected = new(
            Message: MessageLayout.Row,
            Send: SendStyle.Square,
            Status: StatusStyle.Glyph,
            Labels: LabelCase.Sentence,
            Composer: ComposerStyle.Card,
            Nav: NavStyle.Tile,
            Surface: SurfaceStyle.Hairline,
            Voice: VoiceStyle.Plain,
            Ids: IdStyle.Mono);

        Assert.Equal(expected, Load("lumen-light").Traits);
        Assert.Equal(expected, Load("lumen-dark").Traits);
    }

    /// <summary>The sheet's palette move, face, line height and a few anchor tokens are present in the loaded skins.</summary>
    [Fact]
    public void Lumen_ValuesMatchSheet()
    {
        Skin light = Load("lumen-light");
        Skin dark = Load("lumen-dark");

        Assert.Equal("#111117", dark.Theme.PaletteDark.Surface.ToString(MudBlazor.Utilities.MudColorOutputFormats.Hex));
        Assert.Equal("#333341", dark.Theme.PaletteDark.Divider.ToString(MudBlazor.Utilities.MudColorOutputFormats.Hex));
        Assert.Equal("JetBrains Mono", light.Faces.Mono[0]);
        Assert.Equal("1.45", light.Typography.LineHeight);
        Assert.Equal("normal", light.Typography.LetterSpacing);
        Assert.Equal("palette.background", dark.Tokens["ground.page"]);
        Assert.Equal("palette.background", dark.Tokens["ground.rail"]);
        Assert.Equal("palette.backgroundGray", light.Tokens["ground.rail"]);
        Assert.Equal("palette.primary", light.Tokens["needs"]);
        Assert.Equal("palette.primaryContrastText", light.Tokens["send.fg"]);
        Assert.Equal("mix(palette.dividerLight 90%, palette.background)", dark.Tokens["current.background"]);
    }

    /// <summary>
    /// No token in the light skin repeats the registry default, and the dark file's own tokens each differ from the
    /// light value it would otherwise inherit.
    /// </summary>
    [Fact]
    public void Lumen_SetsOnlyTokensThatDiffer()
    {
        foreach (KeyValuePair<string, string> entry in Load("lumen-light").Tokens)
        {
            Token token = SkinTokens.All.Single(candidate => string.Equals(candidate.Key, entry.Key, StringComparison.Ordinal));
            Assert.False(string.Equals(entry.Value, token.LightDefault, StringComparison.Ordinal), $"lumen-light: token {entry.Key} repeats the registry default.");
        }

        Skin lightSkin = Load("lumen-light");
        foreach (KeyValuePair<string, string> entry in OwnTokens("lumen.dark.json"))
        {
            Assert.True(lightSkin.Tokens.TryGetValue(entry.Key, out string? inherited), $"lumen.dark.json sets {entry.Key}, which the light file does not set.");
            Assert.False(string.Equals(entry.Value, inherited, StringComparison.Ordinal), $"lumen.dark.json repeats the light value of {entry.Key}.");
        }
    }

    /// <summary>
    /// Lumen's Board lanes are tinted columns with the default lane border (it no longer sets <c>board.column.line</c>:
    /// every Theme draws one, plan D15), its note h1 is the mockup's 26px over the 16px body, and its
    /// dark prose sits a step below the heading white (side-by-side audit, 2026-10-30, LL-K02, LL-L01, LD-03).
    /// </summary>
    [Fact]
    public void Lumen_LanesAndNoteReadingMatchTheMockup()
    {
        Skin light = Load("lumen-light");
        Skin dark = Load("lumen-dark");

        Assert.False(light.Tokens.ContainsKey("board.column.line"));
        Assert.Equal("1.625em", light.Tokens["note.h1.size"]);
        Assert.Equal("-0.01em", light.Tokens["note.h1.tracking"]);
        Assert.Equal("#d4d4dc", dark.Tokens["note.prose.ink"]);
        Assert.Equal("#ececf1", dark.Palette["textPrimary"]);
    }
}
