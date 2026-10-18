using System.Text.Json;
using Agency.Huddle.App.Themes.Skins;
using Agency.Huddle.Tests.Ui;

namespace Agency.Huddle.Tests.Themes.Skins;

/// <summary>
/// Pins the Nordic skin files (theming phase 3, package P3-C): Nordic Light and Nordic Dark carry, in JSON, everything
/// the retired <c>packs/nordic.css</c> carried that is not a trait variant in <c>app.css</c>, and the pack is gone.
/// </summary>
public sealed class NordicSkinTests
{
    private static readonly string[] NordicIds = ["nordic-light", "nordic-dark"];

    /// <summary>
    /// A Nordic file sets only tokens whose value differs from the registry default for its mode; a dark key that is
    /// equal to the default is allowed only when it overrides a different value the light file it includes sets.
    /// </summary>
    [Fact]
    public void Nordic_SetsOnlyTokensThatDiffer()
    {
        Dictionary<string, string> light = TokensOf("nordic.light.json");
        Dictionary<string, string> dark = TokensOf("nordic.dark.json");

        Assert.NotEmpty(light);
        Assert.NotEmpty(dark);
        foreach (KeyValuePair<string, string> pair in light)
        {
            Token token = Registered(pair.Key);
            Assert.NotEqual(token.LightDefault, pair.Value);
        }

        foreach (KeyValuePair<string, string> pair in dark)
        {
            Token token = Registered(pair.Key);
            bool overridesInherited = light.TryGetValue(pair.Key, out string? inherited) && !string.Equals(inherited, pair.Value, StringComparison.Ordinal);
            Assert.True(
                overridesInherited || !string.Equals(token.DarkDefault, pair.Value, StringComparison.Ordinal),
                $"nordic.dark.json sets {pair.Key} to its default, {pair.Value}.");
        }
    }

    /// <summary>The dark file re-sets the tokens whose value depends on the mode: the rail, the soft tones over the dark ground and the scrim.</summary>
    [Fact]
    public void Nordic_DarkOverridesTheModeSpecificTokens()
    {
        Dictionary<string, string> light = TokensOf("nordic.light.json");
        Dictionary<string, string> dark = TokensOf("nordic.dark.json");

        Assert.Equal("palette.backgroundGray", light["ground.rail"]);
        Assert.Equal("palette.drawerBackground", dark["ground.rail"]);
        Assert.Equal("mix(palette.primary 14%, palette.background)", dark["tone.primary.soft"]);
        Assert.Equal("palette.overlayDark", dark["scrim"]);
        Assert.StartsWith("alpha(", light["scrim"], StringComparison.Ordinal); // contains-ok: pins the scrim's shape, not a value
    }

    /// <summary>Both Nordic skins name all nine traits, and every one differs from the default.</summary>
    [Fact]
    public void Nordic_TraitsAllNonDefault()
    {
        IReadOnlyDictionary<string, object> defaults = new SkinTraits().ToMarkerAttributes();
        Dictionary<string, string> expected = new(StringComparer.Ordinal)
        {
            ["data-hu-message"] = "row",
            ["data-hu-send"] = "text",
            ["data-hu-status"] = "pill",
            ["data-hu-labels"] = "upper",
            ["data-hu-composer"] = "card",
            ["data-hu-nav"] = "tile",
            ["data-hu-surface"] = "paper",
            ["data-hu-marks"] = "chip",
            ["data-hu-reading"] = "book",
        };

        foreach (string id in NordicIds)
        {
            IReadOnlyDictionary<string, object> actual = SkinOf(id).Traits.ToMarkerAttributes();
            foreach (KeyValuePair<string, string> pair in expected)
            {
                Assert.Equal(pair.Value, actual[pair.Key]);
                Assert.NotEqual(defaults[pair.Key], actual[pair.Key]);
            }
        }
    }

    /// <summary>Nordic's three faces and its tracking and body line height are skin data, in both modes.</summary>
    [Fact]
    public void Nordic_FacesAndTypography()
    {
        foreach (string id in NordicIds)
        {
            Skin skin = SkinOf(id);

            Assert.Equal(["IBM Plex Sans", "system-ui", "-apple-system", "Segoe UI", "sans-serif"], skin.Faces.Ui);
            Assert.Equal(["Literata", "Source Serif 4", "Georgia", "serif"], skin.Faces.Read);
            Assert.Equal(["IBM Plex Mono", "ui-monospace", "Cascadia Mono", "Consolas", "monospace"], skin.Faces.Mono);
            Assert.Equal("normal", skin.Typography.LetterSpacing);
            Assert.Equal("1.5", skin.Typography.LineHeight);
        }
    }

    /// <summary>The style pack is gone: no <c>wwwroot/packs</c> folder, no pack link in <c>App.razor</c>, no pack marker in the layout, no pack field on a skin.</summary>
    [Fact]
    public void NoPackFolder()
    {
        Assert.False(Directory.Exists(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "packs")));
        string app = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "App.razor"));
        Assert.DoesNotContain("packs/", app, StringComparison.Ordinal);
        Assert.DoesNotContain("nordic.css", app, StringComparison.Ordinal);
        string layout = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Layout", "MainLayout.razor"));
        Assert.DoesNotContain("data-theme-pack", layout, StringComparison.Ordinal);
        foreach (string name in new[] { "nordic.light.json", "nordic.dark.json", "skin.schema.json" })
        {
            string json = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Themes", "Skins", name));
            Assert.DoesNotContain("\"pack\"", json, StringComparison.Ordinal);
        }
    }

    private static Skin SkinOf(string id) =>
        Assert.Single(SkinLoader.LoadAll(), skin => string.Equals(skin.Id, id, StringComparison.Ordinal));

    private static Token Registered(string key) =>
        Assert.Single(SkinTokens.All, token => string.Equals(token.Key, key, StringComparison.Ordinal));

    private static Dictionary<string, string> TokensOf(string file)
    {
        string path = CssSource.RepoPath("src", "Huddle.App", "Themes", "Skins", file);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        Dictionary<string, string> tokens = new(StringComparer.Ordinal);
        if (document.RootElement.TryGetProperty("tokens", out JsonElement element))
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                tokens[property.Name] = property.Value.GetString() ?? string.Empty;
            }
        }

        return tokens;
    }
}
