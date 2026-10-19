using System.Text.RegularExpressions;
using Agency.Huddle.App.Themes;
using Agency.Huddle.App.Themes.Skins;

namespace Agency.Huddle.Tests.Themes.Skins;

/// <summary>The CSS the skin layer writes: the <c>:root</c> block, its safety, and the <c>app-vars.css</c> copy of the defaults.</summary>
public sealed partial class SkinCssTests
{
    private static readonly string AppVarsPath = RepoPaths.App("wwwroot", "app-vars.css");

    /// <summary>Every registered token is declared exactly once in the rendered block.</summary>
    [Fact]
    public void Render_DeclaresEveryToken()
    {
        string css = SkinCss.RenderDefaults(ThemeMode.Light);

        Assert.Equal(":root{", css[..6]);
        Assert.Equal('}', css[^1]);
        foreach (Token token in SkinTokens.All)
        {
            Assert.Equal(1, Regex.Count(css, "(?<![-\\w])" + Regex.Escape(token.CssName) + ":", RegexOptions.CultureInvariant));
        }
    }

    /// <summary>The block holds none of <c>&lt; &gt; &amp; @</c>, so it is safe as raw markup inside a style element.</summary>
    [Fact]
    public void Render_HasNoMarkupCharacters()
    {
        string css = SkinCss.Render(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["ground.page"] = "#112233" },
            ThemeMode.Dark,
            "test-skin",
            new SkinFacesCss("\"Literata\", serif", "\"Cascadia Mono\", monospace"));

        Assert.DoesNotContain('<', css);
        Assert.DoesNotContain('>', css);
        Assert.DoesNotContain('&', css);
        Assert.DoesNotContain('@', css);
    }

    /// <summary>A skin's value replaces the default for that token and no other.</summary>
    [Fact]
    public void Render_OverrideReplacesDefault()
    {
        string css = SkinCss.Render(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["ground.page"] = "#112233", ["hover"] = "ref(ground.page)" },
            ThemeMode.Light,
            "test-skin",
            null);

        Dictionary<string, string> declared = Declarations(css);
        Assert.Equal("#112233", declared["--hu-ground-page"]);
        Assert.Equal("var(--hu-ground-page)", declared["--hu-hover"]);
        Assert.Equal("var(--mud-palette-surface)", declared["--hu-ground-sidebar"]);
    }

    /// <summary>Faces, when given, are written as the two font variables; without faces neither appears.</summary>
    [Fact]
    public void Render_Faces_WriteFontVariables()
    {
        Dictionary<string, string> none = new(StringComparer.Ordinal);

        string withFaces = SkinCss.Render(none, ThemeMode.Light, "s", new SkinFacesCss("\"Literata\", serif", null));
        string without = SkinCss.Render(none, ThemeMode.Light, "s", null);

        Dictionary<string, string> declared = Declarations(withFaces);
        Assert.Equal("\"Literata\", serif", declared["--font-read"]);
        Assert.False(declared.ContainsKey("--font-mono"));
        Assert.False(Declarations(without).ContainsKey("--font-read"));
    }

    /// <summary>A face that could break out of the declaration is refused.</summary>
    [Theory]
    [InlineData("a; color: red")]
    [InlineData("a}")]
    [InlineData("</style>")]
    public void Render_UnsafeFace_Fails(string face)
    {
        Assert.Throws<SkinException>(() =>
            SkinCss.Render(new Dictionary<string, string>(StringComparer.Ordinal), ThemeMode.Light, "s", new SkinFacesCss(face, null)));
    }

    /// <summary>An override naming a token that is not registered is refused.</summary>
    [Fact]
    public void Render_UnknownOverrideKey_Fails()
    {
        SkinException ex = Assert.Throws<SkinException>(() =>
            SkinCss.Render(new Dictionary<string, string>(StringComparer.Ordinal) { ["ground.pgae"] = "#fff" }, ThemeMode.Light, "s", null));

        Assert.Equal("unknown token 'ground.pgae' in skin 's'", ex.Message);
    }

    /// <summary>Both modes render the same block in phase 1, because the defaults are mode-agnostic.</summary>
    [Fact]
    public void RenderDefaults_LightAndDarkMatch()
    {
        Assert.Equal(SkinCss.RenderDefaults(ThemeMode.Light), SkinCss.RenderDefaults(ThemeMode.Dark));
    }

    /// <summary><c>app-vars.css</c> declares every token's rendered default, value for value, and no other <c>--hu-*</c> token.</summary>
    [Fact]
    public void AppVars_DeclaresRenderDefaults()
    {
        Dictionary<string, string> expected = Declarations(SkinCss.RenderDefaults(ThemeMode.Light));
        Dictionary<string, string> actual = Declarations(File.ReadAllText(AppVarsPath));
        Dictionary<string, string> huActual = actual
            .Where(pair => pair.Key.StartsWith("--hu-", StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        Assert.Equal(expected.Count, huActual.Count);
        foreach (KeyValuePair<string, string> pair in expected)
        {
            Assert.True(huActual.TryGetValue(pair.Key, out string? value), "app-vars.css does not declare " + pair.Key);
            Assert.Equal(pair.Value, value);
        }
    }

    /// <summary>The ink ladder keeps its old names as aliases of the registry tokens, so app.css reads are unchanged.</summary>
    [Theory]
    [InlineData("strip")]
    [InlineData("wash")]
    [InlineData("rule")]
    [InlineData("edge")]
    [InlineData("field")]
    public void AppVars_AliasesInkLadder(string rung)
    {
        Dictionary<string, string> actual = Declarations(File.ReadAllText(AppVarsPath));

        Assert.Equal($"var(--hu-ink-{rung})", actual[$"--ink-{rung}"]);
    }

    /// <summary><c>app-vars.css</c> still declares the two font variables.</summary>
    [Fact]
    public void AppVars_KeepsFontVariables()
    {
        Dictionary<string, string> actual = Declarations(File.ReadAllText(AppVarsPath));

        Assert.True(actual.ContainsKey("--font-mono"));
        Assert.True(actual.ContainsKey("--font-read"));
    }

    /// <summary>Every <c>--name: value;</c> declaration in <paramref name="css"/>, comments removed, whitespace collapsed.</summary>
    /// <param name="css">A stylesheet or a rendered block.</param>
    private static Dictionary<string, string> Declarations(string css)
    {
        string stripped = CommentPattern().Replace(css, string.Empty);
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        foreach (Match match in DeclarationPattern().Matches(stripped))
        {
            result[match.Groups["name"].Value] = WhitespacePattern().Replace(match.Groups["value"].Value.Trim(), " ");
        }

        return result;
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex CommentPattern();

    [GeneratedRegex(@"(?<name>--[A-Za-z0-9-]+)\s*:\s*(?<value>[^;{}]+?)\s*(?:;|(?=\}))", RegexOptions.CultureInvariant)]
    private static partial Regex DeclarationPattern();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();
}
