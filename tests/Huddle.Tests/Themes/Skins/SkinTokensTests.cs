using Agency.Huddle.App.Themes.Skins;

namespace Agency.Huddle.Tests.Themes.Skins;

/// <summary>The skin token registry: its size, naming and the CSS each default renders to.</summary>
public sealed class SkinTokensTests
{
    private const string Surface = "var(--mud-palette-surface)";
    private const string Ink = "var(--mud-palette-text-primary)";

    /// <summary>The registry holds every token of architecture section 1, the three phase 0 tokens among them.</summary>
    [Fact]
    public void Registry_HasAllTokensIncludingPhaseZero()
    {
        Assert.Equal(PinnedDefaults().Count, SkinTokens.All.Count);
        Assert.True(SkinTokens.Contains("send.fg"));
        Assert.True(SkinTokens.Contains("send.bg"));
        Assert.True(SkinTokens.Contains("ring"));
        Assert.False(SkinTokens.Contains("ground.pgae"));
    }

    /// <summary>A token's CSS name is <c>--hu-</c> and its key with every dot turned into a hyphen.</summary>
    [Fact]
    public void Registry_CssNameIsHuPlusHyphens()
    {
        Assert.Equal("--hu-send-fg", new Token("send.fg", "none", "none", "d").CssName);
        Assert.Equal("--hu-current-background-hover", new Token("current.background.hover", "none", "none", "d").CssName);
        Assert.All(SkinTokens.All, token => Assert.Equal("--hu-" + token.Key.Replace('.', '-'), token.CssName));
    }

    /// <summary>No two tokens share a key, or a CSS name.</summary>
    [Fact]
    public void Registry_KeysUnique()
    {
        Assert.Equal(SkinTokens.All.Count, SkinTokens.All.Select(token => token.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(SkinTokens.All.Count, SkinTokens.All.Select(token => token.CssName).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>Every token has a description, and every default is a valid expression.</summary>
    [Fact]
    public void Registry_EveryTokenHasDescriptionAndValidDefaults()
    {
        foreach (Token token in SkinTokens.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(token.Description), token.Key);
            Assert.NotEmpty(SkinExpression.ToCss(token.LightDefault, token.Key, "defaults"));
            Assert.NotEmpty(SkinExpression.ToCss(token.DarkDefault, token.Key, "defaults"));
        }
    }

    /// <summary>The defaults of each mode parse together with no reference loop.</summary>
    [Fact]
    public void Registry_DefaultsParseWithoutCycles()
    {
        SkinExpression.Validate(SkinTokens.All.ToDictionary(token => token.Key, token => token.LightDefault, StringComparer.Ordinal), "defaults");
        SkinExpression.Validate(SkinTokens.All.ToDictionary(token => token.Key, token => token.DarkDefault, StringComparer.Ordinal), "defaults");

        Assert.NotEmpty(SkinTokens.All);
    }

    /// <summary>Phases 1 and 2 default both modes alike: they are expressions over palette variables.</summary>
    [Fact]
    public void Registry_LightAndDarkDefaultsMatch()
    {
        Assert.All(SkinTokens.All, token => Assert.Equal(token.LightDefault, token.DarkDefault));
    }

    /// <summary>The defaults are registered in the order architecture section 1 lists them.</summary>
    [Fact]
    public void Registry_KeysAreInSectionOrder()
    {
        Assert.Equal(
            PinnedDefaults().Select(pair => pair.Key).ToList(),
            SkinTokens.All.Select(token => token.Key).ToList());
    }

    /// <summary>Each default renders to the CSS recipe the stylesheet writes today at the sites the token replaces.</summary>
    [Fact]
    public void Registry_DefaultsMatchTodaysRecipes()
    {
        foreach (KeyValuePair<string, string> expected in PinnedDefaults())
        {
            Token token = SkinTokens.All.Single(candidate => string.Equals(candidate.Key, expected.Key, StringComparison.Ordinal));

            Assert.Equal(expected.Value, SkinExpression.ToCss(token.LightDefault, token.Key, "defaults"));
        }
    }

    /// <summary>A <c>color-mix</c> of <paramref name="hue"/> at <paramref name="percent"/> percent over <paramref name="over"/>.</summary>
    private static string Mix(string hue, int percent, string over) =>
        $"color-mix(in srgb, var(--mud-palette-{hue}) {percent}%, {over})";

    /// <summary>The soft, ink recipe pair every status tone shares.</summary>
    private static (string Soft, string Ink) Tone(string hue) => (Mix(hue, 14, Surface), Mix(hue, 50, Ink));

    private static List<KeyValuePair<string, string>> PinnedDefaults()
    {
        List<KeyValuePair<string, string>> list = [];
        void Add(string key, string css) => list.Add(new KeyValuePair<string, string>(key, css));
        static string InkMix(int percent) => Mix("text-primary", percent, Surface);

        Add("ink.strip", InkMix(4));
        Add("ink.wash", InkMix(6));
        Add("ink.rule", InkMix(10));
        Add("ink.edge", InkMix(18));
        Add("ink.field", InkMix(30));
        Add("ground.page", Surface);
        Add("ground.sidebar", Surface);
        Add("ground.rail", Surface);
        Add("ground.raised", Surface);
        Add("ground.sunk", Surface);
        Add("line.region", "var(--mud-palette-lines-default)");
        Add("line.control", "var(--mud-palette-lines-default)");

        Add("accent.ink", Mix("primary", 70, Ink));
        Add("current.background", Mix("primary", 14, Surface));
        Add("current.background.hover", Mix("primary", 20, Surface));
        Add("current.foreground", "var(--hu-accent-ink)");
        Add("current.icon", "var(--hu-accent-ink)");
        Add("current.weight", "500");
        Add("current.shadow", "none");
        Add("selected.background", Mix("primary", 14, Surface));
        Add("selected.background.hover", Mix("primary", 20, Surface));
        Add("selected.foreground", "var(--hu-accent-ink)");
        Add("selected.task.background", "var(--hu-selected-background)");
        Add("selected.task.bar", "var(--mud-palette-primary)");
        Add("tree.current.bar", "var(--mud-palette-primary)");
        Add("hover", "var(--mud-palette-action-default-hover)");
        Add("focus", Ink);
        Add("needs", "var(--mud-palette-warning)");
        Add("text.subtle", Mix("text-secondary", 50, Ink));

        (string Soft, string Ink) neutral = Tone("text-secondary");
        Add("tone.neutral.soft", neutral.Soft);
        Add("tone.neutral.ink", neutral.Ink);
        Add("tone.primary.soft", Tone("primary").Soft);
        Add("tone.primary.ink", Tone("primary").Ink);
        Add("tone.primary.tint", Mix("primary", 8, Surface));
        Add("tone.info.soft", Tone("info").Soft);
        Add("tone.info.ink", Tone("info").Ink);
        Add("tone.warning.soft", Tone("warning").Soft);
        Add("tone.warning.ink", Tone("warning").Ink);
        Add("tone.warning.tint", Mix("warning", 8, Surface));
        Add("tone.warning.line", Mix("warning", 55, Surface));
        Add("tone.success.soft", Tone("success").Soft);
        Add("tone.success.ink", Tone("success").Ink);
        Add("tone.error.soft", Tone("error").Soft);
        Add("tone.error.ink", Tone("error").Ink);
        Add("tone.tertiary.soft", Tone("tertiary").Soft);
        Add("tone.tertiary.ink", Tone("tertiary").Ink);

        Add("shadow.card", "none");
        Add("shadow.card.hover", "none");
        Add("shadow.panel", "var(--mud-elevation-4)");
        Add("field.background", "transparent");
        Add("field.focus.line", "var(--mud-palette-primary)");
        Add("field.focus.shadow", "none");
        Add("scrim", "var(--mud-palette-overlay-dark)");
        Add("tabs.strip.background", InkMix(4));
        Add("tabs.item.hover", InkMix(7));
        Add("message.line-height", "var(--mud-typography-body1-lineheight)");
        Add("send.fg", "var(--mud-palette-primary)");
        Add("send.bg", "transparent");
        Add("ring", Surface);
        return list;
    }
}
