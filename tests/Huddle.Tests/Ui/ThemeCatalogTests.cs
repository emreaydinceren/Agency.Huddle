using System.Reflection;
using MudBlazor;
using MudBlazor.Utilities;
using Agency.Huddle.App.Themes;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins <see cref="ThemeCatalog"/>'s shape now that MudBlazor's <see cref="MudTheme"/> is the single
/// source of theming: every descriptor has a unique, non-empty id, carries both a light and a dark
/// palette, the one shipped "Huddle" theme carries the colours this application shipped in
/// <c>wwwroot/theme.css</c> before this migration, every theme's own (native) palette clears its
/// WCAG contrast floor except for a short, doubly-pinned list of documented exceptions, and every
/// <c>--mud-palette-*</c> custom property the stylesheets actually reference has a value in every
/// theme.
/// </summary>
public sealed class ThemeCatalogTests
{
    /// <summary>Every descriptor's id is unique and non-empty - a blank or duplicate id would make the Appearance tab's dropdown either unusable or ambiguous.</summary>
    [Fact]
    public void BuiltIn_EveryIdIsUniqueAndNonEmpty()
    {
        var ids = ThemeCatalog.BuiltIn.Select(descriptor => descriptor.Id).ToList();

        Assert.All(ids, id => Assert.False(string.IsNullOrEmpty(id)));

        var distinct = ids.Distinct(StringComparer.Ordinal).ToList();
        Assert.Equal(ids.Count, distinct.Count);
    }

    /// <summary>
    /// Every descriptor's <see cref="MudTheme"/> has both <see cref="MudTheme.PaletteLight"/> and
    /// <see cref="MudTheme.PaletteDark"/> populated. MudBlazor defaults both to a non-null instance,
    /// so this is really asserting that nobody accidentally left a descriptor pointing at a bare
    /// <c>new MudTheme()</c> with none of this application's own colours set.
    /// </summary>
    [Fact]
    public void BuiltIn_EveryThemeHasBothPalettes()
    {
        foreach (var descriptor in ThemeCatalog.BuiltIn)
        {
            Assert.NotNull(descriptor.Theme.PaletteLight);
            Assert.NotNull(descriptor.Theme.PaletteDark);
        }
    }

    /// <summary>
    /// A spot-check that the shipped "Huddle" theme's <c>Primary</c> and <c>Background</c> carry the
    /// expected values in both palettes - the two colours most likely to visibly regress if a future
    /// edit swaps light and dark, or fat-fingers a hex value.
    /// </summary>
    [Fact]
    public void Huddle_PrimaryAndBackgroundCarryTheExpectedValues()
    {
        var huddle = ThemeCatalog.BuiltIn.Single(descriptor => string.Equals(descriptor.Id, "huddle", StringComparison.Ordinal));

        Assert.Equal("#4A154B", huddle.Theme.PaletteLight.Primary.ToString(MudColorOutputFormats.Hex), ignoreCase: true);
        Assert.Equal("#FFFFFF", huddle.Theme.PaletteLight.Background.ToString(MudColorOutputFormats.Hex), ignoreCase: true);

        Assert.Equal("#C07BC3", huddle.Theme.PaletteDark.Primary.ToString(MudColorOutputFormats.Hex), ignoreCase: true);
        Assert.Equal("#1B1B1F", huddle.Theme.PaletteDark.Background.ToString(MudColorOutputFormats.Hex), ignoreCase: true);
    }

    /// <summary>
    /// Every <c>(themeId, pair)</c> combination this suite holds to a WCAG contrast floor, minus the
    /// combinations <see cref="DocumentedShortfalls"/> pins as known, measured exceptions - a theme
    /// that cannot clear its floor is recorded there rather than skipped silently.
    /// </summary>
    public static TheoryData<string, string> ContrastCombinations()
    {
        TheoryData<string, string> data = new();
        foreach (var descriptor in ThemeCatalog.BuiltIn)
        {
            foreach (var pair in ContrastPairs)
            {
                if (!IsDocumentedShortfall(descriptor.Id, pair.Name))
                {
                    data.Add(descriptor.Id, pair.Name);
                }
            }
        }

        return data;
    }

    /// <summary>
    /// Each built-in theme's own (native) palette - the half named by its <see cref="ThemeDescriptor.Mode"/>,
    /// never the half it borrows from <see cref="HuddleTheme"/> - clears the WCAG floor for every pair in
    /// <see cref="ContrastPairs"/>. The borrowed half is Huddle's own and is not retested here: it is
    /// covered once, by Huddle's own native-palette rows.
    /// </summary>
    /// <param name="themeId"><see cref="ThemeDescriptor.Id"/> of the theme under test.</param>
    /// <param name="pairName">The <see cref="ContrastPairSpec.Name"/> of the foreground/background pair under test.</param>
    [Theory]
    [MemberData(nameof(ContrastCombinations))]
    public void BuiltIn_NativePaletteContrast_ClearsWcagFloor(string themeId, string pairName)
    {
        var descriptor = ThemeCatalog.BuiltIn.Single(candidate => string.Equals(candidate.Id, themeId, StringComparison.Ordinal));
        var pair = ContrastPairs.Single(candidate => string.Equals(candidate.Name, pairName, StringComparison.Ordinal));
        var palette = NativePalette(descriptor);

        var ratio = ContrastRatio(pair.Foreground(palette), pair.Background(palette));

        Assert.True(
            ratio >= pair.Floor,
            $"{themeId}: {pairName} contrast ratio {ratio:F2}:1 falls short of the {pair.Floor:F1}:1 WCAG AA {pair.FloorKind} minimum.");
    }

    /// <summary>Every <c>(themeId, pair)</c> combination <see cref="DocumentedShortfalls"/> pins.</summary>
    public static TheoryData<string, string> DocumentedShortfallCombinations()
    {
        TheoryData<string, string> data = new();
        foreach (var shortfall in DocumentedShortfalls)
        {
            data.Add(shortfall.ThemeId, shortfall.Pair);
        }

        return data;
    }

    /// <summary>
    /// The other half of <see cref="DocumentedShortfalls"/>: a listed combination must still measure
    /// below its floor, at the exact ratio recorded. If a palette value changes - fixed or otherwise -
    /// this fails the build, so a stale entry cannot quietly rot; it must be deleted instead.
    /// </summary>
    /// <param name="themeId"><see cref="ThemeDescriptor.Id"/> of the documented shortfall under test.</param>
    /// <param name="pairName">The <see cref="ContrastPairSpec.Name"/> of the documented shortfall under test.</param>
    [Theory]
    [MemberData(nameof(DocumentedShortfallCombinations))]
    public void DocumentedShortfalls_StillFallShort(string themeId, string pairName)
    {
        var shortfall = DocumentedShortfalls.Single(candidate =>
            string.Equals(candidate.ThemeId, themeId, StringComparison.Ordinal) &&
            string.Equals(candidate.Pair, pairName, StringComparison.Ordinal));
        var descriptor = ThemeCatalog.BuiltIn.Single(candidate => string.Equals(candidate.Id, themeId, StringComparison.Ordinal));
        var pair = ContrastPairs.Single(candidate => string.Equals(candidate.Name, pairName, StringComparison.Ordinal));
        var palette = NativePalette(descriptor);

        var ratio = ContrastRatio(pair.Foreground(palette), pair.Background(palette));

        Assert.True(ratio < shortfall.Floor, $"{themeId}: {pairName} no longer falls short of {shortfall.Floor:F1}:1 - delete this documented shortfall.");
        Assert.Equal(shortfall.Ratio, ratio, precision: 2);
    }

    /// <summary>
    /// Every <c>--mud-palette-*</c> custom property <c>app.css</c> or a <c>.razor.css</c> file actually
    /// references, mapped to its <see cref="Palette"/> property, has a value in every built-in theme's
    /// native palette. The theme-import item on the roadmap names its own trap - "the token list and
    /// the mapping must have one source of truth" - so a <c>var()</c> added to a stylesheet without a
    /// matching palette assignment would otherwise leave every theme silently carrying no value for it;
    /// this makes that a build failure instead of a silent gap.
    /// </summary>
    [Fact]
    public void BuiltIn_EveryThemeSetsEveryPalettePropertyAppCssReads()
    {
        List<string> referencedTokens = [.. CssSource.ReadReferencedTokens(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"))];
        var componentsDirectory = CssSource.RepoPath("src", "Huddle.App", "Components");
        foreach (var cssFile in Directory.EnumerateFiles(componentsDirectory, "*.razor.css", SearchOption.AllDirectories))
        {
            referencedTokens.AddRange(CssSource.ReadReferencedTokens(cssFile));
        }

        var paletteTokens = referencedTokens
            .Where(token => token.StartsWith("--mud-palette-", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        List<PropertyInfo> mappedProperties = [];
        foreach (var token in paletteTokens)
        {
            var property = typeof(Palette).GetProperty(MapTokenToPropertyName(token));
            if (property is not null)
            {
                mappedProperties.Add(property);
            }
        }

        Assert.NotEmpty(mappedProperties);

        foreach (var descriptor in ThemeCatalog.BuiltIn)
        {
            var palette = NativePalette(descriptor);
            foreach (var property in mappedProperties)
            {
                Assert.NotNull(property.GetValue(palette));
            }
        }
    }

    /// <summary>
    /// Maps a <c>--mud-palette-*</c> custom property name to its <see cref="Palette"/> property name.
    /// Kebab-case converts mechanically (<c>--mud-palette-text-secondary</c> to <c>TextSecondary</c>)
    /// except <c>--mud-palette-primary-text</c>, which MudBlazor names after <see cref="Palette.PrimaryContrastText"/>
    /// rather than a mechanical <c>PrimaryText</c>.
    /// </summary>
    /// <param name="token">The custom property name, including its <c>--mud-palette-</c> prefix.</param>
    private static string MapTokenToPropertyName(string token)
    {
        if (string.Equals(token, "--mud-palette-primary-text", StringComparison.Ordinal))
        {
            return nameof(Palette.PrimaryContrastText);
        }

        var name = token["--mud-palette-".Length..];
        var segments = name.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(segments.Select(segment => char.ToUpperInvariant(segment[0]) + segment[1..]));
    }

    /// <summary>Whether <paramref name="themeId"/>/<paramref name="pairName"/> is one of <see cref="DocumentedShortfalls"/>.</summary>
    /// <param name="themeId">The theme id to look up.</param>
    /// <param name="pairName">The pair name to look up.</param>
    private static bool IsDocumentedShortfall(string themeId, string pairName) =>
        DocumentedShortfalls.Any(shortfall =>
            string.Equals(shortfall.ThemeId, themeId, StringComparison.Ordinal) &&
            string.Equals(shortfall.Pair, pairName, StringComparison.Ordinal));

    /// <summary>
    /// The palette a theme was actually designed for: its <see cref="MudTheme.PaletteLight"/> when
    /// <see cref="ThemeDescriptor.Mode"/> is <see cref="ThemeMode.Light"/>, otherwise its
    /// <see cref="MudTheme.PaletteDark"/>. The other half is borrowed from <see cref="HuddleTheme"/>
    /// and carries no legibility guarantee of this theme's own choosing.
    /// </summary>
    /// <param name="descriptor">The theme descriptor to resolve.</param>
    private static Palette NativePalette(ThemeDescriptor descriptor)
    {
        Palette palette = descriptor.Mode == ThemeMode.Light ? descriptor.Theme.PaletteLight : descriptor.Theme.PaletteDark;
        return palette;
    }

    /// <summary>
    /// The WCAG 2.1 contrast ratio between two colours: <c>(L1 + 0.05) / (L2 + 0.05)</c>, where
    /// <c>L1</c> is the lighter colour's relative luminance and <c>L2</c> the darker's.
    /// </summary>
    /// <param name="first">One colour.</param>
    /// <param name="second">The other colour.</param>
    private static double ContrastRatio(MudColor first, MudColor second)
    {
        var firstLuminance = RelativeLuminance(first);
        var secondLuminance = RelativeLuminance(second);
        var lighter = Math.Max(firstLuminance, secondLuminance);
        var darker = Math.Min(firstLuminance, secondLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>
    /// A colour's WCAG 2.1 relative luminance: each sRGB channel linearised, then combined as
    /// <c>0.2126R + 0.7152G + 0.0722B</c>.
    /// </summary>
    /// <param name="color">The colour to measure.</param>
    private static double RelativeLuminance(MudColor color) =>
        (0.2126 * LineariseChannel(color.R)) + (0.7152 * LineariseChannel(color.G)) + (0.0722 * LineariseChannel(color.B));

    /// <summary>Linearises one 0-255 sRGB channel per WCAG 2.1's <c>c &lt;= 0.03928 ? c/12.92 : ((c+0.055)/1.055)^2.4</c>.</summary>
    /// <param name="channel">The channel's 0-255 byte value.</param>
    private static double LineariseChannel(byte channel)
    {
        var normalised = channel / 255.0;
        return normalised <= 0.03928 ? normalised / 12.92 : Math.Pow((normalised + 0.055) / 1.055, 2.4);
    }

    /// <summary>
    /// The six foreground/background pairs this suite holds to a WCAG floor, all at 4.5:1 - WCAG
    /// 1.4.3's text minimum. There is no separate focus-ring pair: <c>app.css</c> paints the focus
    /// ring with <c>outline: 2px solid var(--mud-palette-text-primary)</c> - deliberately, per that
    /// rule's own comment, which warns against "improving" it back to an accent colour - so the
    /// <c>TextPrimary</c> pairs above already cover its legibility, at 4.5:1, stricter than the
    /// 3.0:1 WCAG 1.4.11 would require for a non-text indicator. <c>LinesDefault</c> has exactly one
    /// consumer in this codebase, <c>.teammate-tile:hover</c>'s border, a decorative hover cue that
    /// is co-indicated by a background change in the same rule - not a WCAG non-text-contrast
    /// requirement - so it is not held to a floor here.
    /// </summary>
    private static readonly IReadOnlyList<ContrastPairSpec> ContrastPairs =
    [
        new("TextPrimary vs Background", palette => palette.TextPrimary, palette => palette.Background, 4.5, "text"),
        new("TextPrimary vs Surface", palette => palette.TextPrimary, palette => palette.Surface, 4.5, "text"),
        new("TextPrimary vs DrawerBackground", palette => palette.TextPrimary, palette => palette.DrawerBackground, 4.5, "text"),
        new("TextSecondary vs Surface", palette => palette.TextSecondary, palette => palette.Surface, 4.5, "text"),
        new("Primary vs Surface", palette => palette.Primary, palette => palette.Surface, 4.5, "text"),
        new("PrimaryContrastText vs Primary", palette => palette.PrimaryContrastText, palette => palette.Primary, 4.5, "text"),
    ];

    /// <summary>
    /// The themes that genuinely cannot clear their floor, pinned in both directions:
    /// <see cref="ContrastCombinations"/> skips each one, and <see cref="DocumentedShortfalls_StillFallShort"/>
    /// asserts it still measures at the recorded ratio, so a later fix - or any other change to the
    /// underlying palette value - fails the build and forces the stale entry to be deleted rather than
    /// left to rot. Every value here is imported unmodified from its VS Code source; "themes are
    /// imported faithfully" is the governing decision this list exists to keep.
    /// </summary>
    private static readonly IReadOnlyList<DocumentedShortfall> DocumentedShortfalls =
    [
        new(
            "light-plus",
            "TextSecondary vs Surface",
            4.40,
            4.5,
            "VS Code's registry default for descriptionForeground in light themes is #717171, which measures 4.48:1 against pure white; against this theme's own near-white Surface (#f3f3f3) it measures 4.40:1. Imported unmodified."),
        new(
            "quiet-light",
            "TextSecondary vs Surface",
            4.40,
            4.5,
            "VS Code's registry default for descriptionForeground in light themes is #717171, which measures 4.48:1 against pure white; against this theme's own near-white Surface (#f3f3f3) it measures 4.40:1. Imported unmodified."),
        new(
            "solarized-light",
            "TextSecondary vs Surface",
            3.98,
            4.5,
            "TextSecondary is the same #717171 registry default as the themes above, but Solarized Light's own Surface (#eee8d5) is not near-white, lowering the ratio to 3.98:1. Imported unmodified."),
    ];

    /// <summary>One foreground/background pair this suite holds to a WCAG contrast floor.</summary>
    /// <param name="Name">The pair's display name, used as the theory's data label and in failure messages.</param>
    /// <param name="Foreground">Selects the foreground colour from a theme's native palette.</param>
    /// <param name="Background">Selects the background colour from a theme's native palette.</param>
    /// <param name="Floor">The minimum WCAG 2.1 contrast ratio this pair must clear.</param>
    /// <param name="FloorKind">Names the floor in a failure message. Every pair here is a text pair against the WCAG 1.4.3 4.5:1 minimum, so this is always "text".</param>
    private sealed record ContrastPairSpec(string Name, Func<Palette, MudColor> Foreground, Func<Palette, MudColor> Background, double Floor, string FloorKind);

    /// <summary>One theme's documented, measured contrast shortfall.</summary>
    /// <param name="ThemeId"><see cref="ThemeDescriptor.Id"/> of the theme this shortfall belongs to.</param>
    /// <param name="Pair">The <see cref="ContrastPairSpec.Name"/> of the pair that falls short.</param>
    /// <param name="Ratio">The measured WCAG 2.1 contrast ratio, to two decimals.</param>
    /// <param name="Floor">The floor this ratio falls short of.</param>
    /// <param name="Justification">The factual, measured cause of the shortfall.</param>
    private sealed record DocumentedShortfall(string ThemeId, string Pair, double Ratio, double Floor, string Justification);
}
