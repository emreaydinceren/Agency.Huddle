using Agency.Huddle.App.Themes;
using Agency.Huddle.App.Themes.Skins;
using MudBlazor;
using MudBlazor.Utilities;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins the per-Theme customization (P5-D1): <see cref="SkinLoader.Customize"/> merges a Human's palette and token
/// overrides over a skin, clamps a customized <c>palette.primary</c> for contrast, never mutates the shared skin, and
/// refuses (never throws on) an entry a skin file would refuse; <see cref="ThemeCatalog.Resolve"/> builds the effective
/// descriptor once per (Theme id, customization).
/// </summary>
public sealed class ThemeCustomizationTests
{
    /// <summary>A customization that sets only <c>palette.primary</c>.</summary>
    /// <param name="hex">The colour.</param>
    private static SkinCustomization Accent(string hex) =>
        new(new Dictionary<string, string>(StringComparer.Ordinal) { ["primary"] = hex }, new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>A customization that sets only tokens.</summary>
    /// <param name="key">The token key.</param>
    /// <param name="value">The expression.</param>
    private static SkinCustomization Token(string key, string value) =>
        new(new Dictionary<string, string>(StringComparer.Ordinal), new Dictionary<string, string>(StringComparer.Ordinal) { [key] = value });

    /// <summary>The loaded skin with the given id.</summary>
    /// <param name="id">A Theme id.</param>
    private static Skin SkinOf(string id) => SkinLoader.LoadAll().Single(skin => string.Equals(skin.Id, id, StringComparison.Ordinal));

    /// <summary>The palette a skin's Theme fills.</summary>
    /// <param name="skin">The skin.</param>
    private static Palette NativePalette(Skin skin) => skin.Mode == ThemeMode.Dark ? skin.Theme.PaletteDark : skin.Theme.PaletteLight;

    /// <summary>An accent too pale for a light ground still yields a Primary that clears 4.5:1 on Surface, with readable text on it.</summary>
    [Fact]
    public void Customize_LightPrimaryTooPale_ClearsThePrimaryContrastFloor()
    {
        Skin result = SkinLoader.Customize(SkinOf("lumen-light"), Accent("#ffeb3b"));

        Palette palette = result.Theme.PaletteLight;
        Assert.True(ContrastColour.Ratio(palette.Primary, palette.Surface) >= 4.5);
        Assert.Equal(ContrastColour.ReadableForeground(palette.Primary), palette.PrimaryContrastText);
    }

    /// <summary>An accent too dark for a dark ground still yields a Primary that clears 4.5:1 on Surface.</summary>
    [Fact]
    public void Customize_DarkPrimaryTooDark_ClearsThePrimaryContrastFloor()
    {
        Skin result = SkinLoader.Customize(SkinOf("lumen-dark"), Accent("#1a0033"));

        Palette palette = result.Theme.PaletteDark;
        Assert.True(ContrastColour.Ratio(palette.Primary, palette.Surface) >= 4.5);
        Assert.Equal(ContrastColour.ReadableForeground(palette.Primary), palette.PrimaryContrastText);
    }

    /// <summary>Whatever accent is chosen, the text on it is whichever of black and white reads better, in every Theme.</summary>
    /// <param name="themeId">The Theme id.</param>
    /// <param name="accent">The accent.</param>
    [Theory]
    [InlineData("lumen-light", "#6e78da")]
    [InlineData("lumen-dark", "#6e78da")]
    [InlineData("nordic-light", "#ffeb3b")]
    [InlineData("cobalt-soft-dark", "#1a0033")]
    [InlineData("fluent-dark", "#ffeb3b")]
    public void Customize_AnyAccent_HasReadableTextOnAccent(string themeId, string accent)
    {
        Skin result = SkinLoader.Customize(SkinOf(themeId), Accent(accent));
        Palette palette = NativePalette(result);

        Assert.True(ContrastColour.Ratio(palette.PrimaryContrastText, palette.Primary) >= 4.5);
    }

    /// <summary>Only the Primary family changes: the neutrals and signals are the skin's own.</summary>
    [Fact]
    public void Customize_Primary_LeavesTheOtherPaletteValuesAsTheSkinAuthoredThem()
    {
        Skin baseSkin = SkinOf("lumen-light");

        Palette palette = SkinLoader.Customize(baseSkin, Accent("#3a6fd8")).Theme.PaletteLight;

        Assert.Equal(baseSkin.Theme.PaletteLight.Background, palette.Background);
        Assert.Equal(baseSkin.Theme.PaletteLight.Surface, palette.Surface);
        Assert.Equal(baseSkin.Theme.PaletteLight.TextPrimary, palette.TextPrimary);
        Assert.Equal(baseSkin.Theme.PaletteLight.Success, palette.Success);
        Assert.Equal(baseSkin.Theme.PaletteLight.Error, palette.Error);
        Assert.Equal(baseSkin.Theme.LayoutProperties.DefaultBorderRadius, SkinLoader.Customize(baseSkin, Accent("#3a6fd8")).Theme.LayoutProperties.DefaultBorderRadius);
    }

    /// <summary>A customization never mutates the shared skin or its theme: a second circuit would otherwise inherit the first one's accent.</summary>
    [Fact]
    public void Customize_DoesNotMutateTheBaseSkin()
    {
        Skin baseSkin = SkinOf("lumen-light");
        MudColor before = baseSkin.Theme.PaletteLight.Primary;

        Skin result = SkinLoader.Customize(baseSkin, Accent("#00b294"));

        Assert.NotSame(baseSkin, result);
        Assert.NotSame(baseSkin.Theme, result.Theme);
        Assert.Equal(before, baseSkin.Theme.PaletteLight.Primary);
        Assert.NotEqual(before, result.Theme.PaletteLight.Primary);
        Assert.Equal("#7368d3", baseSkin.Palette["primary"]);
    }

    /// <summary>The customized skin keeps the skin's identity, mode, traits and radius.</summary>
    [Fact]
    public void Customize_KeepsIdentityModeAndTraits()
    {
        Skin baseSkin = SkinOf("nordic-dark");

        Skin result = SkinLoader.Customize(baseSkin, Accent("#00b294"));

        Assert.Equal(baseSkin.Id, result.Id);
        Assert.Equal(baseSkin.Label, result.Label);
        Assert.Equal(ThemeMode.Dark, result.Mode);
        Assert.Equal(baseSkin.Traits, result.Traits);
        Assert.Equal(baseSkin.Theme.LayoutProperties.DefaultBorderRadius, result.Theme.LayoutProperties.DefaultBorderRadius);
    }

    /// <summary>An empty customization returns the very same skin, so nothing is rebuilt for a Human who customised nothing.</summary>
    [Fact]
    public void Customize_Empty_ReturnsTheSameSkin()
    {
        Skin baseSkin = SkinOf("lumen-light");

        Assert.Same(baseSkin, SkinLoader.Customize(baseSkin, SkinCustomization.Empty));
    }

    /// <summary>An explicit <c>primaryContrastText</c> in the customization wins over the derived black-or-white.</summary>
    [Fact]
    public void Customize_ExplicitPrimaryContrastText_IsKept()
    {
        SkinCustomization customization = new(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["primary"] = "#00b294", ["primaryContrastText"] = "#123456" },
            new Dictionary<string, string>(StringComparer.Ordinal));

        Palette palette = SkinLoader.Customize(SkinOf("lumen-light"), customization).Theme.PaletteLight;

        Assert.Equal("#123456", palette.PrimaryContrastText.ToString(MudColorOutputFormats.Hex));
    }

    /// <summary>A palette key other than Primary is applied as written, without the accent clamp.</summary>
    [Fact]
    public void Customize_PaletteKey_IsAppliedAndMergedIntoThePaletteMap()
    {
        SkinCustomization customization = new(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["drawerBackground"] = "#102030" },
            new Dictionary<string, string>(StringComparer.Ordinal));

        Skin result = SkinLoader.Customize(SkinOf("lumen-light"), customization);

        Assert.Equal("#102030", result.Theme.PaletteLight.DrawerBackground.ToString(MudColorOutputFormats.Hex));
        Assert.Equal("#102030", result.Palette["drawerBackground"]);
    }

    /// <summary>A token override reaches the skin's token map and the rendered style block, and only there.</summary>
    [Fact]
    public void Customize_Token_ChangesTheStyleBlock()
    {
        Skin baseSkin = SkinOf("lumen-light");

        Skin result = SkinLoader.Customize(baseSkin, Token("ground.rail", "#112233"));

        Assert.Equal("#112233", result.Tokens["ground.rail"]);
        Assert.NotEqual(ThemeDescriptor.From(baseSkin).StyleBlock, ThemeDescriptor.From(result).StyleBlock);
        Assert.Contains("--hu-ground-rail:#112233", ThemeDescriptor.From(result).StyleBlock, StringComparison.Ordinal);
    }

    /// <summary>A customized Primary reaches the rendered Theme, so the style block's <c>palette.primary</c> terms follow it.</summary>
    [Fact]
    public void Customize_Primary_ReachesTheDescriptorTheme()
    {
        ThemeDescriptor descriptor = ThemeDescriptor.From(SkinLoader.Customize(SkinOf("lumen-light"), Accent("#00b294")));

        Assert.NotEqual("#7368d3", descriptor.Theme.PaletteLight.Primary.ToString(MudColorOutputFormats.Hex));
        Assert.Equal(ThemeMode.Light, descriptor.Mode);
    }

    /// <summary>Every kind of entry a skin file would refuse is dropped, with a message naming it, and the valid entry beside it survives.</summary>
    /// <param name="section">Which map the bad entry is in: palette or tokens.</param>
    /// <param name="key">The bad entry's key.</param>
    /// <param name="value">The bad entry's value.</param>
    /// <param name="expectedMessagePart">Text the rejection message must contain.</param>
    [Theory]
    [InlineData("palette", "nonsense", "#ffffff", "palette.nonsense")]
    [InlineData("palette", "primary", "not-a-colour", "palette.primary")]
    [InlineData("palette", "primary", "#12", "palette.primary")]
    [InlineData("palette", "primary", "red; background: blue", "palette.primary")]
    [InlineData("palette", "drawerBackground", "<b>", "palette.drawerBackground")]
    [InlineData("tokens", "no.such.token", "#ffffff", "tokens.no.such.token")]
    [InlineData("tokens", "ground.rail", "", "tokens.ground.rail")]
    [InlineData("tokens", "ground.rail", "url(javascript:alert(1))", "tokens.ground.rail")]
    [InlineData("tokens", "ground.rail", "ref(no.such.token)", "tokens.ground.rail")]
    [InlineData("tokens", "ground.rail", "#fff; } body { display: none", "tokens.ground.rail")]
    public void Sanitize_AnInvalidEntry_IsDroppedWithAMessage(string section, string key, string value, string expectedMessagePart)
    {
        Dictionary<string, string> palette = new(StringComparer.Ordinal) { ["background"] = "#fefefe" };
        Dictionary<string, string> tokens = new(StringComparer.Ordinal) { ["ground.sidebar"] = "#eeeeee" };
        (section == "palette" ? palette : tokens)[key] = value;
        List<string> rejected = [];

        SkinCustomization clean = SkinLoader.Sanitize(SkinOf("lumen-light"), new SkinCustomization(palette, tokens), rejected.Add);

        Assert.DoesNotContain(key, (section == "palette" ? clean.Palette : clean.Tokens).Keys, StringComparer.Ordinal);
        Assert.Equal("#fefefe", clean.Palette["background"]);
        Assert.Equal("#eeeeee", clean.Tokens["ground.sidebar"]);
        Assert.Contains(expectedMessagePart, Assert.Single(rejected), StringComparison.Ordinal);
    }

    /// <summary>A token whose references loop back on themselves is refused, because CSS would silently invalidate every token in the loop.</summary>
    [Fact]
    public void Sanitize_ATokenCycle_IsDropped()
    {
        List<string> rejected = [];

        SkinCustomization clean = SkinLoader.Sanitize(SkinOf("lumen-light"), Token("tone.primary.soft", "ref(tone.primary.tint)"), rejected.Add);

        Assert.Empty(clean.Tokens);
        Assert.Single(rejected);
    }

    /// <summary>Colours in every form a skin file accepts are kept: hex of three to eight digits and rgb()/rgba().</summary>
    /// <param name="value">The colour text.</param>
    [Theory]
    [InlineData("#abc")]
    [InlineData("#aabbcc")]
    [InlineData("#aabbccdd")]
    [InlineData("rgb(10, 20, 30)")]
    [InlineData("rgba(10, 20, 30, 0.5)")]
    public void Sanitize_AColourInAnyAcceptedForm_IsKept(string value)
    {
        SkinCustomization clean = SkinLoader.Sanitize(
            SkinOf("lumen-light"),
            new SkinCustomization(new Dictionary<string, string>(StringComparer.Ordinal) { ["drawerBackground"] = value }, new Dictionary<string, string>(StringComparer.Ordinal)),
            null);

        Assert.Equal(value, clean.Palette["drawerBackground"]);
    }

    /// <summary><see cref="SkinLoader.Customize"/> on an invalid entry drops it silently rather than throwing at render.</summary>
    [Fact]
    public void Customize_AnInvalidEntry_NeverThrows()
    {
        SkinCustomization customization = new(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["primary"] = "garbage", ["nonsense"] = "#fff" },
            new Dictionary<string, string>(StringComparer.Ordinal) { ["no.such.token"] = "#fff" });
        Skin baseSkin = SkinOf("lumen-light");

        Skin result = SkinLoader.Customize(baseSkin, customization);

        Assert.Same(baseSkin, result);
    }

    /// <summary>The cache key is the same for equal content in any insertion order and differs when any value differs.</summary>
    [Fact]
    public void CacheKey_IsCanonical()
    {
        SkinCustomization first = new(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["primary"] = "#111111", ["background"] = "#222222" },
            new Dictionary<string, string>(StringComparer.Ordinal));
        SkinCustomization reordered = new(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["background"] = "#222222", ["primary"] = "#111111" },
            new Dictionary<string, string>(StringComparer.Ordinal));

        Assert.Equal(first.CacheKey, reordered.CacheKey);
        Assert.NotEqual(first.CacheKey, Accent("#111111").CacheKey);
        Assert.NotEqual(Accent("#111111").CacheKey, Token("ground.rail", "#111111").CacheKey);
    }

    /// <summary>Resolving a Theme with nothing customised returns the catalogue's own descriptor.</summary>
    [Fact]
    public void Resolve_NullOrEmpty_ReturnsTheCatalogueDescriptor()
    {
        ThemeDescriptor descriptor = ThemeCatalog.BuiltIn[0];

        Assert.Same(descriptor, ThemeCatalog.Resolve(descriptor, null));
        Assert.Same(descriptor, ThemeCatalog.Resolve(descriptor, SkinCustomization.Empty));
    }

    /// <summary>The effective descriptor is built once per (Theme, customization): the same instance comes back until the customization changes.</summary>
    [Fact]
    public void Resolve_SameCustomization_IsBuiltOnce_AndAChangeRebuilds()
    {
        ThemeDescriptor descriptor = ThemeCatalog.BuiltIn[0];

        ThemeDescriptor first = ThemeCatalog.Resolve(descriptor, Accent("#00b294"));
        ThemeDescriptor again = ThemeCatalog.Resolve(descriptor, Accent("#00b294"));
        ThemeDescriptor changed = ThemeCatalog.Resolve(descriptor, Accent("#b20094"));

        Assert.Same(first, again);
        Assert.NotSame(first, changed);
        Assert.NotEqual(descriptor.Theme.PaletteLight.Primary, first.Theme.PaletteLight.Primary);
        Assert.NotEqual(first.Theme.PaletteLight.Primary, changed.Theme.PaletteLight.Primary);
        Assert.Same(first.StyleBlock, again.StyleBlock);
        Assert.Equal(descriptor.Id, first.Id);
    }

    /// <summary>A customization of one Theme never leaks into another Theme's descriptor.</summary>
    [Fact]
    public void Resolve_CustomizingOneTheme_LeavesTheOthersAlone()
    {
        ThemeDescriptor lumen = ThemeCatalog.BuiltIn.Single(static descriptor => descriptor.Id == "lumen-light");
        ThemeDescriptor nordic = ThemeCatalog.BuiltIn.Single(static descriptor => descriptor.Id == "nordic-light");

        _ = ThemeCatalog.Resolve(lumen, Accent("#00b294"));

        Assert.Same(nordic, ThemeCatalog.Resolve(nordic, null));
        Assert.Equal("#7368d3", lumen.Skin.Palette["primary"]);
    }
}
