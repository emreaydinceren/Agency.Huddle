using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Agency.Huddle.App.Themes;
using Agency.Huddle.App.Themes.Skins;
using Agency.Huddle.Tests.Ui;
using MudBlazor;

namespace Agency.Huddle.Tests.Themes.Skins;

/// <summary>
/// Pins <see cref="SkinLoader"/>: the ten embedded skin files load, merge their <c>include</c> chains as architecture
/// section 5 says, reject every invalid file with a message naming the file and key, and reproduce the C# Theme classes
/// property for property (<see cref="Skins_MatchLegacyTheme"/>) until P1-C deletes those classes.
/// </summary>
public sealed partial class SkinLoaderTests
{
    /// <summary>The families a skin may name that need no web font: the system and generic faces.</summary>
    private static readonly string[] SystemFonts = ["system-ui", "-apple-system", "Segoe UI", "Helvetica Neue", "sans-serif", "serif", "monospace"];

    /// <summary>Builds a minimal skin file; <paramref name="extra"/> is spliced in after the required keys.</summary>
    private static string File(string id, string mode, int order, string extra = "") =>
        $$"""{ "id": "{{id}}", "label": "{{id}}", "mode": "{{mode}}", "order": {{order}}{{(extra.Length == 0 ? string.Empty : ", " + extra)}} }""";

    /// <summary>Loads the given in-memory files.</summary>
    private static IReadOnlyList<Skin> Load(params (string Name, string Json)[] files) =>
        SkinLoader.LoadAll(files.ToDictionary(static file => file.Name, static file => file.Json, StringComparer.Ordinal));

    /// <summary>Every embedded skin file loads, and none of the files in the source folder is left out.</summary>
    [Fact]
    public void Skins_EveryFileLoads()
    {
        IReadOnlyList<Skin> skins = SkinLoader.LoadAll();

        string folder = CssSource.RepoPath("src", "Huddle.App", "Themes", "Skins");
        int files = Directory.GetFiles(folder, "*.json").Count(static path => !path.EndsWith("skin.schema.json", StringComparison.Ordinal));
        Assert.Equal(10, files);
        Assert.Equal(files, skins.Count);
        Assert.All(skins, static skin => Assert.NotNull(skin.Theme));
    }

    /// <summary>Ids are unique and the list comes back in strictly ascending order, with Huddle Light first.</summary>
    [Fact]
    public void Skins_IdsUniqueAndOrdered()
    {
        IReadOnlyList<Skin> skins = SkinLoader.LoadAll();

        Assert.Equal(skins.Count, skins.Select(static skin => skin.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(skins.OrderBy(static skin => skin.Order).Select(static skin => skin.Id), skins.Select(static skin => skin.Id));
        Assert.Equal(skins.Count, skins.Select(static skin => skin.Order).Distinct().Count());
        Assert.Equal("huddle", skins[0].Id);
    }

    /// <summary>Every light skin has a dark partner with the same stem and the next order, and the reverse.</summary>
    [Fact]
    public void Skins_LightAndDarkPairUp()
    {
        IReadOnlyList<Skin> skins = SkinLoader.LoadAll();

        foreach (Skin light in skins.Where(static skin => skin.Mode == ThemeMode.Light))
        {
            string stem = light.Id.EndsWith("-light", StringComparison.Ordinal) ? light.Id[..^"-light".Length] : light.Id;
            Skin dark = Assert.Single(skins, skin => skin.Mode == ThemeMode.Dark && string.Equals(skin.Id, stem + "-dark", StringComparison.Ordinal));
            Assert.Equal(light.Order + 10, dark.Order);
        }

        Assert.Equal(skins.Count(static skin => skin.Mode == ThemeMode.Light), skins.Count(static skin => skin.Mode == ThemeMode.Dark));
    }

    /// <summary>A child takes its include's scalars, faces, shadows (per index), traits and typography, and overrides key by key.</summary>
    [Fact]
    public void Skins_IncludesResolve()
    {
        IReadOnlyList<Skin> skins = Load(
            ("a.light.json", File("a", "light", 1, """
                "radius": "6px", "pack": "p", "faces": { "ui": ["Inter"], "mono": ["IBM Plex Mono"] },
                "typography": { "letterSpacing": "normal", "lineHeight": "1.5" },
                "shadows": { "1": "0 1px 0 red", "2-4": "0 2px 0 blue" }, "palette": { "primary": "#112233" }
                """)),
            ("a.dark.json", File("a-dark", "dark", 2, """
                "include": "a.light.json", "faces": { "ui": ["Literata"] }, "shadows": { "3": "0 3px 0 green" }, "palette": { "error": "#445566" }
                """)));

        Skin dark = Assert.Single(skins, static skin => skin.Id == "a-dark");
        Assert.Equal("6px", dark.Radius);
        Assert.Equal("6px", dark.Theme.LayoutProperties.DefaultBorderRadius);
        Assert.Equal("p", dark.Pack);
        Assert.Equal(["Literata"], dark.Faces.Ui);
        Assert.Equal(["IBM Plex Mono"], dark.Faces.Mono);
        Assert.Equal("1.5", dark.Typography.LineHeight);
        Assert.Equal("0 1px 0 red", dark.Shadows[1]);
        Assert.Equal("0 2px 0 blue", dark.Shadows[2]);
        Assert.Equal("0 3px 0 green", dark.Shadows[3]);
        Assert.Equal("0 2px 0 blue", dark.Shadows[4]);
        Assert.Equal("0 3px 0 green", dark.Theme.Shadows.Elevation[3]);
    }

    /// <summary>A file that includes itself through another file is refused, naming the file.</summary>
    [Fact]
    public void Skins_IncludeCycle_Fails()
    {
        SkinException exception = Assert.Throws<SkinException>(() => Load(
            ("a.json", File("a", "light", 1, "\"include\": \"b.json\"")),
            ("b.json", File("b", "light", 2, "\"include\": \"a.json\""))));

        Assert.Equal("a.json: include cycle a.json -> b.json -> a.json.", exception.Message);
    }

    /// <summary>An include naming no file is refused, naming both files.</summary>
    [Fact]
    public void Skins_IncludeMissing_Fails()
    {
        SkinException exception = Assert.Throws<SkinException>(() => Load(
            ("a.json", File("a", "light", 1, "\"include\": \"gone.json\""))));

        Assert.Equal("a.json: include 'gone.json' does not exist.", exception.Message);
    }

    /// <summary>A dark file that includes a light one gets none of its colours; two files of one mode do inherit.</summary>
    [Fact]
    public void Skins_PaletteNotInheritedAcrossModes()
    {
        IReadOnlyList<Skin> skins = Load(
            ("a.light.json", File("a", "light", 1, "\"palette\": { \"primary\": \"#112233\" }")),
            ("a.dark.json", File("a-dark", "dark", 2, "\"include\": \"a.light.json\"")),
            ("b.light.json", File("b", "light", 3, "\"include\": \"a.light.json\"")));

        Skin dark = Assert.Single(skins, static skin => skin.Id == "a-dark");
        Skin sameMode = Assert.Single(skins, static skin => skin.Id == "b");
        Assert.Empty(dark.Palette);
        Assert.Equal(new PaletteDark().Primary.ToString(), dark.Theme.PaletteDark.Primary.ToString());
        Assert.Equal("#112233", sameMode.Palette["primary"]);
    }

    /// <summary>A palette key that names no settable colour property of MudBlazor's Palette is refused, naming file and key.</summary>
    [Fact]
    public void Skins_UnknownPaletteKey_Fails()
    {
        SkinException exception = Assert.Throws<SkinException>(() => Load(
            ("a.json", File("a", "light", 1, "\"palette\": { \"hoverOpacity\": \"#112233\" }"))));

        Assert.Equal("a.json: palette.hoverOpacity: unknown palette key.", exception.Message);
    }

    /// <summary>A token key outside the registry is refused, naming file and key.</summary>
    [Fact]
    public void Skins_UnknownTokenKey_Fails()
    {
        SkinException exception = Assert.Throws<SkinException>(() => Load(
            ("a.json", File("a", "light", 1, "\"tokens\": { \"no.such.token\": \"palette.surface\" }"))));

        Assert.Equal("a.json: tokens.no.such.token: unknown token key.", exception.Message);
    }

    /// <summary>A trait value no enum has, and an unknown trait, are refused; the default value loads in any case.</summary>
    [Fact]
    public void Skins_UnknownTraitValue_Fails()
    {
        SkinException value = Assert.Throws<SkinException>(() => Load(
            ("a.json", File("a", "light", 1, "\"traits\": { \"message\": \"sideways\" }"))));
        SkinException trait = Assert.Throws<SkinException>(() => Load(
            ("a.json", File("a", "light", 1, "\"traits\": { \"typo\": \"bubble\" }"))));
        IReadOnlyList<Skin> fine = Load(("a.json", File("a", "light", 1, "\"traits\": { \"message\": \"Bubble\" }")));

        Assert.Equal("a.json: traits.message: 'sideways' is not a value of this trait; expected one of bubble, row.", value.Message);
        Assert.Equal("a.json: traits.typo: unknown trait.", trait.Message);
        Assert.Equal(MessageLayout.Bubble, fine[0].Traits.Message);
    }

    /// <summary>Single and range keys expand to indexes; overlaps, reversed and out-of-range keys and palette terms are refused.</summary>
    [Theory]
    [InlineData("\"shadows\": { \"1\": \"x\", \"25\": \"y\" }", 2)]
    [InlineData("\"shadows\": { \"1\": \"x\", \"2-4\": \"y\", \"5-25\": \"z\" }", 25)]
    [InlineData("\"shadows\": { \"1-25\": \"x\" }", 25)]
    public void Skins_ShadowRangesValid(string shadows, int expandedCount)
    {
        ArgumentNullException.ThrowIfNull(shadows);

        Skin skin = Load(("a.json", File("a", "light", 1, shadows)))[0];

        Assert.Equal(expandedCount, skin.Shadows.Count);
    }

    /// <summary>Overlapping, reversed and out-of-range shadow keys, and values with palette terms, are refused with the exact message.</summary>
    [Theory]
    [InlineData("\"shadows\": { \"1-3\": \"x\", \"3-5\": \"y\" }", "a.json: shadows.3-5: elevation 3 is already set by another key.")]
    [InlineData("\"shadows\": { \"1-3\": \"x\", \"2\": \"y\" }", "a.json: shadows.2: elevation 2 is already set by another key.")]
    [InlineData("\"shadows\": { \"0\": \"x\" }", "a.json: shadows.0: indexes must satisfy 1 <= N <= M <= 25.")]
    [InlineData("\"shadows\": { \"26\": \"x\" }", "a.json: shadows.26: indexes must satisfy 1 <= N <= M <= 25.")]
    [InlineData("\"shadows\": { \"4-2\": \"x\" }", "a.json: shadows.4-2: indexes must satisfy 1 <= N <= M <= 25.")]
    [InlineData("\"shadows\": { \"two\": \"x\" }", "a.json: shadows.two: the key must be \"N\" or \"N-M\".")]
    [InlineData("\"shadows\": { \"1\": \"0 1px palette.divider\" }", "a.json: shadows.1: the value must be a literal shadow with no palette terms.")]
    public void Skins_ShadowRangesInvalid_Fail(string shadows, string message)
    {
        ArgumentNullException.ThrowIfNull(shadows);

        SkinException exception = Assert.Throws<SkinException>(() => Load(("a.json", File("a", "light", 1, shadows))));

        Assert.Equal(message, exception.Message);
    }

    /// <summary>A family name outside <c>^[A-Za-z0-9 -]+$</c> is refused, naming file and face.</summary>
    /// <param name="family">The offending family name.</param>
    [Theory]
    [InlineData("Evil;Font")]
    [InlineData("a/b")]
    [InlineData("")]
    public void Skins_BadFaceName_Fails(string family)
    {
        SkinException exception = Assert.Throws<SkinException>(() => Load(
            ("a.json", File("a", "light", 1, $"\"faces\": {{ \"read\": [\"{family}\"] }}"))));

        Assert.Equal($"a.json: faces.read: '{family}' must match ^[A-Za-z0-9 -]+$.", exception.Message);
    }

    /// <summary>
    /// The JSON is a faithful transcription: every <see cref="Palette"/> property, the border radius, the UI font family and
    /// every shadow elevation equal the C# descriptor's theme, compared by reflection so no property is hand-picked.
    /// </summary>
    /// <param name="id">A built-in Theme id.</param>
    [Theory]
    [InlineData("huddle")]
    [InlineData("huddle-dark")]
    public void Skins_MatchLegacyTheme(string id)
    {
        bool dark = id == "huddle-dark";
        MudTheme expected = dark ? ThemeDefaults.CreateDark(HuddleTheme.Dark()) : ThemeDefaults.CreateLight(HuddleTheme.Light());
        expected.LayoutProperties.DefaultBorderRadius = "8px";
        Skin skin = Assert.Single(SkinLoader.LoadAll(), candidate => candidate.Id == id);
        MudTheme actual = skin.Theme;

        AssertPaletteEqual(expected.PaletteLight, actual.PaletteLight);
        AssertPaletteEqual(expected.PaletteDark, actual.PaletteDark);
        Assert.Equal(expected.LayoutProperties.DefaultBorderRadius, actual.LayoutProperties.DefaultBorderRadius);
        Assert.Equal(expected.Typography.Default.FontFamily, actual.Typography.Default.FontFamily);
        Assert.Equal(expected.Shadows.Elevation.Length, actual.Shadows.Elevation.Length);
        for (int i = 0; i < expected.Shadows.Elevation.Length; i++)
        {
            Assert.Equal(expected.Shadows.Elevation[i], actual.Shadows.Elevation[i]);
        }

        Assert.Null(skin.Pack);
        Assert.Equal(dark ? "Huddle Dark" : "Huddle Light", skin.Label);
    }

    /// <summary>Every shipped skin's token overrides are registry keys, and the registry defaults plus those overrides cover every token.</summary>
    [Fact]
    public void Skins_MergedSkinIsComplete()
    {
        foreach (Skin skin in SkinLoader.LoadAll())
        {
            Assert.All(skin.Tokens.Keys, key => Assert.True(SkinTokens.Contains(key), $"{skin.Id}: '{key}' is not a registry token."));
            Assert.Equal(SkinTokens.All.Count, MergedTokens(skin).Count);
        }
    }

    /// <summary>Every merged token value of every shipped skin parses, and every <c>ref()</c> resolves without a cycle.</summary>
    [Fact]
    public void Skins_TokenValuesParse()
    {
        foreach (Skin skin in SkinLoader.LoadAll())
        {
            IReadOnlyDictionary<string, string> merged = MergedTokens(skin);
            SkinExpression.Validate(merged, skin.Id);
            foreach (KeyValuePair<string, string> pair in merged)
            {
                Assert.NotEmpty(SkinExpression.ToCss(pair.Value, pair.Key, skin.Id));
            }
        }
    }

    /// <summary>A token value outside the grammar fails at load, and the message names the file and the key.</summary>
    [Fact]
    public void Skins_BadTokenExpression_FailsNamingFileAndKey()
    {
        SkinException exception = Assert.Throws<SkinException>(() => Load(
            ("a.json", File("a", "light", 1, "\"tokens\": { \"ground.rail\": \"url(x)\" }"))));

        Assert.Matches(@"^a\.json: .*ground\.rail", exception.Message);
    }

    /// <summary>The registry defaults for <paramref name="skin"/>'s mode with the skin's own overrides applied.</summary>
    private static Dictionary<string, string> MergedTokens(Skin skin)
    {
        Dictionary<string, string> merged = new(StringComparer.Ordinal);
        foreach (Token token in SkinTokens.All)
        {
            string fallback = skin.Mode == ThemeMode.Dark ? token.DarkDefault : token.LightDefault;
            merged[token.Key] = skin.Tokens.TryGetValue(token.Key, out string? value) ? value : fallback;
        }

        return merged;
    }

    /// <summary>Every family a skin names is on the Google Fonts link in <c>App.razor</c> or is a system face.</summary>
    [Fact]
    public void Skins_FacesAreLoaded()
    {
        string razor = System.IO.File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "App.razor"));
        HashSet<string> loaded = new(StringComparer.Ordinal);
        foreach (Match match in GoogleFamilyPattern().Matches(razor))
        {
            loaded.Add(match.Groups["name"].Value.Replace('+', ' '));
        }

        Assert.NotEmpty(loaded);
        List<string> named = [];
        foreach (Skin skin in SkinLoader.LoadAll())
        {
            named.AddRange(skin.Faces.Ui);
            named.AddRange(skin.Faces.Read);
            named.AddRange(skin.Faces.Mono);
        }

        Assert.NotEmpty(named);
        foreach (string family in named)
        {
            Assert.True(loaded.Contains(family) || SystemFonts.Contains(family, StringComparer.Ordinal), $"'{family}' is neither on the Google Fonts link nor a system face.");
        }
    }

    /// <summary>Compares every readable <see cref="Palette"/> property of two palettes by its invariant text.</summary>
    private static void AssertPaletteEqual(Palette expected, Palette actual)
    {
        PropertyInfo[] properties = typeof(Palette).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(static property => property.GetMethod is { IsPublic: true } && property.GetIndexParameters().Length == 0)
            .ToArray();
        Assert.True(properties.Length >= 60, "reflection found too few palette properties to prove anything");
        foreach (PropertyInfo property in properties)
        {
            string want = Convert.ToString(property.GetValue(expected), CultureInfo.InvariantCulture) ?? string.Empty;
            string got = Convert.ToString(property.GetValue(actual), CultureInfo.InvariantCulture) ?? string.Empty;
            Assert.True(string.Equals(want, got, StringComparison.Ordinal), $"Palette.{property.Name}: expected '{want}', got '{got}'.");
        }
    }

    [GeneratedRegex(@"family=(?<name>[A-Za-z0-9+]+)", RegexOptions.CultureInvariant)]
    private static partial Regex GoogleFamilyPattern();
}
