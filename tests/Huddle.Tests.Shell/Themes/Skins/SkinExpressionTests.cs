using System.Reflection;
using Agency.Huddle.App.Themes.Skins;
using MudBlazor;
using MudBlazor.Utilities;

namespace Agency.Huddle.Tests.Themes.Skins;

/// <summary>The skin expression grammar: what each term emits and every way a bad value is refused.</summary>
public sealed class SkinExpressionTests
{
    private const string Key = "ground.page";
    private const string SkinId = "test-skin";

    /// <summary>Rewrites <paramref name="value"/> as the token <c>ground.page</c> of skin <c>test-skin</c>.</summary>
    private static string Css(string value) => SkinExpression.ToCss(value, Key, SkinId);

    /// <summary>The full message for a refusal of <paramref name="value"/>: the <paramref name="detail"/>, then the token, skin and value.</summary>
    private static string Expected(string detail, string value) =>
        $"{detail} in token 'ground.page' of skin 'test-skin' (value '{value}')";

    /// <summary>Asserts that <paramref name="value"/> is refused and returns the message.</summary>
    private static string Fail(string value) =>
        Assert.Throws<SkinException>(() => Css(value)).Message;

    /// <summary>Colour literals, lengths, numbers and allow-listed keywords pass through unchanged.</summary>
    [Theory]
    [InlineData("#fff")]
    [InlineData("#1a2b3c")]
    [InlineData("#1a2b3c80")]
    [InlineData("rgba(0, 0, 0, 0.5)")]
    [InlineData("rgb(0 0 0 / 50%)")]
    [InlineData("500")]
    [InlineData("12px")]
    [InlineData("0.5em")]
    [InlineData("none")]
    [InlineData("transparent")]
    [InlineData("currentColor")]
    [InlineData("0 1px 2px rgba(0, 0, 0, 0.1)")]
    public void Parse_Literals_PassThrough(string value)
    {
        Assert.Equal(value, Css(value));
    }

    /// <summary>A palette name becomes the matching MudBlazor variable.</summary>
    [Theory]
    [InlineData("palette.textPrimary", "var(--mud-palette-text-primary)")]
    [InlineData("palette.drawerBackground", "var(--mud-palette-drawer-background)")]
    [InlineData("palette.backgroundGray", "var(--mud-palette-background-gray)")]
    [InlineData("palette.linesDefault", "var(--mud-palette-lines-default)")]
    [InlineData("palette.dividerLight", "var(--mud-palette-divider-light)")]
    [InlineData("palette.overlayDark", "var(--mud-palette-overlay-dark)")]
    [InlineData("palette.actionDefaultHover", "var(--mud-palette-action-default-hover)")]
    [InlineData("palette.primaryRgb", "var(--mud-palette-primary-rgb)")]
    [InlineData("palette.primaryContrastText", "var(--mud-palette-primary-text)")]
    [InlineData("palette.primaryText", "var(--mud-palette-primary-text)")]
    public void Parse_PaletteTerm_EmitsMudVariable(string value, string expected)
    {
        Assert.Equal(expected, Css(value));
    }

    /// <summary>Every colour property a skin may set on <see cref="Palette"/> maps to a variable on the allow-list.</summary>
    [Theory]
    [MemberData(nameof(SettablePaletteColours))]
    public void Parse_PaletteName_MapsToMudVariable(string property)
    {
        ArgumentNullException.ThrowIfNull(property);
        string camel = char.ToLowerInvariant(property[0]) + property[1..];

        string css = Css("palette." + camel);

        Assert.Equal("var(", css[..4]);
        string name = css["var(".Length..^1];
        Assert.Contains(name, MudVariables.All);
    }

    /// <summary>The settable colour properties of <see cref="Palette"/>, the same set a skin's <c>palette</c> block uses.</summary>
    public static TheoryData<string> SettablePaletteColours()
    {
        TheoryData<string> data = [];
        foreach (PropertyInfo property in typeof(Palette).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.CanWrite && property.PropertyType == typeof(MudColor))
            {
                data.Add(property.Name);
            }
        }

        return data;
    }

    /// <summary><c>mud(name)</c> reads a MudBlazor variable on the allow-list.</summary>
    [Fact]
    public void Parse_MudTerm_EmitsVar()
    {
        Assert.Equal("var(--mud-elevation-4)", Css("mud(elevation-4)"));
    }

    /// <summary><c>ref(key)</c> reads another token's variable.</summary>
    [Fact]
    public void Parse_RefTerm_EmitsHuVar()
    {
        Assert.Equal("var(--hu-current-background-hover)", Css("ref(current.background.hover)"));
    }

    /// <summary><c>mix</c> and <c>alpha</c> become <c>color-mix</c>.</summary>
    [Fact]
    public void Parse_MixAndAlpha_EmitColorMix()
    {
        Assert.Equal(
            "color-mix(in srgb, var(--mud-palette-primary) 14%, var(--mud-palette-surface))",
            Css("mix(palette.primary 14%, palette.surface)"));
        Assert.Equal(
            "color-mix(in srgb, var(--mud-palette-primary) 40%, transparent)",
            Css("alpha(palette.primary 40%)"));
    }

    /// <summary>A terms sequence rewrites each term and keeps the rest, as in a shadow.</summary>
    [Fact]
    public void Parse_SequenceWithTerms_RewritesEachTerm()
    {
        Assert.Equal(
            "0 0 0 3px color-mix(in srgb, var(--hu-tone-info-soft) 50%, transparent)",
            Css("0 0 0 3px alpha(ref(tone.info.soft) 50%)"));
    }

    /// <summary>Nested mixes are valid.</summary>
    [Fact]
    public void Parse_NestedMix_IsValid()
    {
        Assert.Equal(
            "color-mix(in srgb, color-mix(in srgb, var(--mud-palette-info) 20%, var(--mud-palette-surface)) 50%, var(--hu-ground-page))",
            Css("mix(mix(palette.info 20%, palette.surface) 50%, ref(ground.page))"));
    }

    /// <summary><c>calc</c> keeps its operators and rewrites nested terms.</summary>
    [Fact]
    public void Parse_Calc_RewritesNestedTerms()
    {
        Assert.Equal(
            "calc(var(--mud-default-borderradius) * 0.75)",
            Css("calc(mud(default-borderradius) * 0.75)"));
    }

    /// <summary>A misspelt palette colour names the colour, the token and the skin.</summary>
    [Fact]
    public void Parse_UnknownPalette_NamesColourKeyAndSkin()
    {
        Assert.Equal(
            Expected("unknown palette colour 'textPrimry'", "palette.textPrimry"),
            Fail("palette.textPrimry"));
    }

    /// <summary>A <c>Palette</c> member that is not a colour maps to no variable and is refused the same way.</summary>
    [Fact]
    public void Parse_HoverOpacity_IsUnknownPaletteColour()
    {
        Assert.Equal(
            Expected("unknown palette colour 'hoverOpacity'", "palette.hoverOpacity"),
            Fail("palette.hoverOpacity"));
    }

    /// <summary>A <c>ref</c> to a key that is not registered is refused, naming the key.</summary>
    [Fact]
    public void Parse_RefToUnknownToken_Fails()
    {
        Assert.Equal(
            Expected("unknown token 'ground.pgae'", "ref(ground.pgae)"),
            Fail("ref(ground.pgae)"));
    }

    /// <summary>A mix without a percentage, or outside 0 to 100 exclusive, is refused.</summary>
    /// <param name="value">The refused value.</param>
    /// <param name="function">The function named in the message.</param>
    /// <param name="found">The text found where the percentage belongs.</param>
    [Theory]
    [InlineData("mix(palette.primary 140%, palette.surface)", "mix", "140%")]
    [InlineData("mix(palette.primary 0%, palette.surface)", "mix", "0%")]
    [InlineData("mix(palette.primary 100%, palette.surface)", "mix", "100%")]
    [InlineData("mix(palette.primary, palette.surface)", "mix", "")]
    [InlineData("alpha(palette.primary)", "alpha", "")]
    public void Parse_BadPercentage_Fails(string value, string function, string found)
    {
        string detail = $"{function}() needs a percentage greater than 0 and less than 100 after the colour, found '{found}'";

        Assert.Equal(Expected(detail, value), Fail(value));
    }

    /// <summary>Named colours, quotes, short hex, a hand-written <c>var(</c> and markup are forbidden terms.</summary>
    /// <param name="value">The refused value.</param>
    /// <param name="term">The term the message names.</param>
    [Theory]
    [InlineData("red", "red")]
    [InlineData("\"x\"", "\"x\"")]
    [InlineData("#12", "#12")]
    [InlineData("var(--x)", "var(")]
    [InlineData("</style>", "</style>")]
    [InlineData("url(x)", "url(")]
    [InlineData("1px; color: red", "1px;")]
    [InlineData("a{b}", "a{b}")]
    [InlineData("a&b", "a&b")]
    [InlineData("a@b", "a@b")]
    [InlineData("a\\b", "a\\b")]
    public void Parse_ForbiddenTerm_Fails(string value, string term)
    {
        Assert.Equal(Expected($"forbidden term '{term}'", value), Fail(value));
    }

    /// <summary>An empty value is refused.</summary>
    [Fact]
    public void Parse_EmptyValue_Fails()
    {
        Assert.Throws<SkinException>(() => Css("  "));
    }

    /// <summary>A <c>mud()</c> name outside the allow-list is refused.</summary>
    [Fact]
    public void Parse_MudVariableNotOnAllowList_Fails()
    {
        Assert.Equal(
            Expected("'elevation-26' is not a MudBlazor variable", "mud(elevation-26)"),
            Fail("mud(elevation-26)"));
    }

    /// <summary>A cycle across two tokens is reported as <c>a → b → a</c>, naming the skin.</summary>
    [Fact]
    public void Validate_TwoTokenCycle_NamesTheCycle()
    {
        Dictionary<string, string> tokens = new(StringComparer.Ordinal)
        {
            ["a"] = "ref(b)",
            ["b"] = "ref(a)",
        };

        SkinException ex = Assert.Throws<SkinException>(() => SkinExpression.Validate(tokens, SkinId));

        Assert.Equal("token reference cycle a → b → a in skin 'test-skin'", ex.Message);
    }

    /// <summary>A token that refers to itself is a cycle.</summary>
    [Fact]
    public void Validate_SelfReference_NamesTheCycle()
    {
        Dictionary<string, string> tokens = new(StringComparer.Ordinal) { ["a"] = "ref(a)" };

        SkinException ex = Assert.Throws<SkinException>(() => SkinExpression.Validate(tokens, SkinId));

        Assert.Equal("token reference cycle a → a in skin 'test-skin'", ex.Message);
    }

    /// <summary>A skin override that closes a loop through a registry default is a cycle.</summary>
    [Fact]
    public void Validate_CycleThroughRegistryDefault_Fails()
    {
        Dictionary<string, string> tokens = SkinTokens.All.ToDictionary(
            token => token.Key,
            token => token.LightDefault,
            StringComparer.Ordinal);
        tokens["accent.ink"] = "ref(current.foreground)";

        SkinException ex = Assert.Throws<SkinException>(() => SkinExpression.Validate(tokens, SkinId));

        Assert.Equal("token reference cycle accent.ink → current.foreground → accent.ink in skin 'test-skin'", ex.Message);
    }

    /// <summary>A set whose references all resolve and never loop is valid.</summary>
    [Fact]
    public void Validate_AcyclicSet_Passes()
    {
        Dictionary<string, string> tokens = new(StringComparer.Ordinal)
        {
            ["a"] = "ref(b)",
            ["b"] = "palette.primary",
        };

        SkinExpression.Validate(tokens, SkinId);

        Assert.Equal(2, tokens.Count);
    }
}
