using System.Reflection;
using MudBlazor;
using MudBlazor.Utilities;
using Agency.Huddle.App.Themes;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins <see cref="ThemeCatalog"/>'s shape now that MudBlazor's <see cref="MudTheme"/> is the single
/// source of theming: every descriptor has a unique, non-empty id and fills the one palette slot its
/// <see cref="ThemeDescriptor.Mode"/> names, the two "Huddle" themes carry the colours this
/// application shipped in <c>wwwroot/theme.css</c> before this migration, the grouping the picker
/// renders covers the catalog without reordering it, every theme's palette clears its WCAG contrast
/// floor except for a short, doubly-pinned list of documented exceptions, and every
/// <c>--mud-palette-*</c> custom property the stylesheets actually reference has a value in every
/// theme.
/// </summary>
public sealed class ThemeCatalogTests
{
    /// <summary>Every descriptor's id is unique and non-empty - a blank or duplicate id would make the Appearance tab's picker either unusable or ambiguous.</summary>
    [Fact]
    public void BuiltIn_EveryIdIsUniqueAndNonEmpty()
    {
        var ids = ThemeCatalog.BuiltIn.Select(descriptor => descriptor.Id).ToList();

        Assert.All(ids, id => Assert.False(string.IsNullOrEmpty(id)));

        var distinct = ids.Distinct(StringComparer.Ordinal).ToList();
        Assert.Equal(ids.Count, distinct.Count);
    }

    /// <summary>
    /// Every descriptor fills the palette slot its <see cref="ThemeDescriptor.Mode"/> names and
    /// leaves the other slot at MudBlazor's own defaults. A theme here is one palette, not a pair
    /// (<c>docs/adr/0017-a-theme-is-a-palette-not-a-pair.md</c>), so this pins the arrangement that
    /// makes the unfilled slot unreachable: its <c>Background</c> still matches a bare
    /// <c>new MudTheme()</c>'s, while the filled slot's does not. A descriptor that went back to
    /// carrying two authored palettes - the shape that let a theme and a separate light/dark control
    /// contradict each other - fails here.
    /// </summary>
    [Fact]
    public void BuiltIn_EveryThemeFillsItsNativeSlotAndLeavesTheOther()
    {
        MudTheme untouched = new();

        foreach (var descriptor in ThemeCatalog.BuiltIn)
        {
            var filled = Hex(NativePalette(descriptor).Background);
            var empty = Hex(descriptor.Mode == ThemeMode.Light ? descriptor.Theme.PaletteDark.Background : descriptor.Theme.PaletteLight.Background);
            var untouchedEmpty = Hex(descriptor.Mode == ThemeMode.Light ? untouched.PaletteDark.Background : untouched.PaletteLight.Background);

            Assert.NotEqual(untouchedEmpty, filled, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(untouchedEmpty, empty, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// A spot-check that the two shipped "Huddle" themes carry the expected <c>Primary</c> and
    /// <c>Background</c> - the colours most likely to visibly regress if a future edit swaps the two
    /// themes' palettes, or fat-fingers a hex value. Huddle is the one file in <c>Themes/</c> with
    /// two authored palettes, which is why it is the one that became two catalog entries.
    /// </summary>
    [Fact]
    public void Huddle_PrimaryAndBackgroundCarryTheExpectedValues()
    {
        var light = ThemeCatalog.BuiltIn.Single(descriptor => string.Equals(descriptor.Id, "huddle", StringComparison.Ordinal));
        var dark = ThemeCatalog.BuiltIn.Single(descriptor => string.Equals(descriptor.Id, "huddle-dark", StringComparison.Ordinal));

        Assert.Equal(ThemeMode.Light, light.Mode);
        Assert.Equal("#5B64C8", Hex(light.Theme.PaletteLight.Primary), ignoreCase: true);
        Assert.Equal("#FFFFFF", Hex(light.Theme.PaletteLight.PrimaryContrastText), ignoreCase: true);
        Assert.Equal("#F2F0ED", Hex(light.Theme.PaletteLight.Background), ignoreCase: true);
        Assert.Equal("#FFFFFF", Hex(light.Theme.PaletteLight.Surface), ignoreCase: true);
        Assert.Equal("#e6e2dc", Hex(light.Theme.PaletteLight.DrawerBackground), ignoreCase: true);

        Assert.Equal(ThemeMode.Dark, dark.Mode);
        Assert.Equal("#8F97E6", Hex(dark.Theme.PaletteDark.Primary), ignoreCase: true);
        Assert.Equal("#141413", Hex(dark.Theme.PaletteDark.PrimaryContrastText), ignoreCase: true);
        Assert.Equal("#1C1C1B", Hex(dark.Theme.PaletteDark.Background), ignoreCase: true);

        // F4: default icon buttons draw in the Theme's secondary text colour, not MudBlazor's translucent grey.
        Assert.Equal(Hex(light.Theme.PaletteLight.TextSecondary), Hex(light.Theme.PaletteLight.ActionDefault), ignoreCase: true);
        Assert.Equal(Hex(dark.Theme.PaletteDark.TextSecondary), Hex(dark.Theme.PaletteDark.ActionDefault), ignoreCase: true);
    }

    /// <summary>
    /// <see cref="ThemeCatalog.BuiltIn"/>'s first entry is the "Huddle Light" theme, whose id is the
    /// bare <c>"huddle"</c>. Three call sites read <c>BuiltIn[0]</c> as "the default theme", and that
    /// id is what an <c>appearance.json</c> written before the split already stores, so both are
    /// pinned here rather than left to a comment.
    /// </summary>
    [Fact]
    public void BuiltIn_FirstEntryIsTheHuddleLightDefault()
    {
        Assert.Equal("huddle", ThemeCatalog.BuiltIn[0].Id);
        Assert.Equal(ThemeMode.Light, ThemeCatalog.BuiltIn[0].Mode);
    }

    /// <summary>
    /// <see cref="ThemeCatalog.Grouped"/> - what the Appearance tab's picker renders - covers every
    /// theme in <see cref="ThemeCatalog.BuiltIn"/> exactly once, lists no empty heading, and keeps
    /// <see cref="ThemeCatalog.BuiltIn"/>'s relative order within each group. That last part is the
    /// point of grouping in a projection instead of by sorting the catalog: the picker gets its
    /// headings without <c>BuiltIn[0]</c> ever moving.
    /// </summary>
    [Fact]
    public void Grouped_CoversEveryThemeOnceAndKeepsCatalogOrderWithinAGroup()
    {
        var flattened = ThemeCatalog.Grouped.SelectMany(grouping => grouping.Themes).ToList();

        Assert.Equal(ThemeCatalog.BuiltIn.Count, flattened.Count);
        Assert.Equal(
            ThemeCatalog.BuiltIn.Select(descriptor => descriptor.Id).Order(StringComparer.Ordinal),
            flattened.Select(descriptor => descriptor.Id).Order(StringComparer.Ordinal));
        Assert.All(ThemeCatalog.Grouped, grouping => Assert.NotEmpty(grouping.Themes));

        foreach (var grouping in ThemeCatalog.Grouped)
        {
            Assert.All(grouping.Themes, theme => Assert.Equal(grouping.Group, theme.Group));

            var catalogOrder = ThemeCatalog.BuiltIn.Where(descriptor => descriptor.Group == grouping.Group).Select(descriptor => descriptor.Id);
            Assert.Equal(catalogOrder, grouping.Themes.Select(descriptor => descriptor.Id));
        }
    }

    /// <summary>
    /// A high-contrast theme is grouped under <see cref="ThemeGroup.HighContrast"/> even though its
    /// <see cref="ThemeDescriptor.Mode"/> is an ordinary light or dark - the exact case that keeps
    /// <see cref="ThemeGroup"/> and <see cref="ThemeMode"/> two fields rather than one.
    /// </summary>
    [Fact]
    public void BuiltIn_HighContrastThemesAreGroupedApartFromTheirMode()
    {
        var darkHighContrast = ThemeCatalog.BuiltIn.Single(descriptor => string.Equals(descriptor.Id, "dark-high-contrast", StringComparison.Ordinal));
        var lightHighContrast = ThemeCatalog.BuiltIn.Single(descriptor => string.Equals(descriptor.Id, "light-high-contrast", StringComparison.Ordinal));

        Assert.Equal(ThemeMode.Dark, darkHighContrast.Mode);
        Assert.Equal(ThemeGroup.HighContrast, darkHighContrast.Group);

        Assert.Equal(ThemeMode.Light, lightHighContrast.Mode);
        Assert.Equal(ThemeGroup.HighContrast, lightHighContrast.Group);
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
    /// Each built-in theme's palette - the one slot named by its <see cref="ThemeDescriptor.Mode"/>,
    /// which since the split is also the only slot it fills - clears the WCAG floor for every pair in
    /// <see cref="ContrastPairs"/>.
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

        var ratio = ContrastColour.Ratio(pair.Foreground(palette), pair.Background(palette));

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

        var ratio = ContrastColour.Ratio(pair.Foreground(palette), pair.Background(palette));

        Assert.True(ratio < shortfall.Floor, $"{themeId}: {pairName} no longer falls short of {shortfall.Floor:F1}:1 - delete this documented shortfall.");
        Assert.Equal(shortfall.Ratio, ratio, precision: 2);
    }

    /// <summary>
    /// Every <c>--mud-palette-*</c> custom property <c>app.css</c> or a <c>.razor.css</c> file actually
    /// references, mapped to its <see cref="Palette"/> property, has a value in every built-in theme's
    /// palette. The theme-import item on the roadmap names its own trap - "the token list and
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
    /// Every built-in theme sets the sidebar's text to its <c>TextPrimary</c> and the sidebar's icons to its
    /// <c>TextSecondary</c>. A theme that leaves <c>DrawerText</c> or <c>DrawerIcon</c> unset shows MudBlazor's
    /// own greys there (a cool <c>#424242</c> on light, 50% white on dark), the one list in the app the
    /// theme's own greys would not reach.
    /// </summary>
    [Fact]
    public void BuiltIn_EveryThemeSetsDrawerTextAndIconFromItsOwnTextColours()
    {
        foreach (var descriptor in ThemeCatalog.BuiltIn)
        {
            var palette = NativePalette(descriptor);

            Assert.True(
                string.Equals(Hex(palette.DrawerText), Hex(palette.TextPrimary), StringComparison.OrdinalIgnoreCase),
                $"{descriptor.Id}: DrawerText {Hex(palette.DrawerText)} is not TextPrimary {Hex(palette.TextPrimary)}.");
            Assert.True(
                string.Equals(Hex(palette.DrawerIcon), Hex(palette.TextSecondary), StringComparison.OrdinalIgnoreCase),
                $"{descriptor.Id}: DrawerIcon {Hex(palette.DrawerIcon)} is not TextSecondary {Hex(palette.TextSecondary)}.");
        }
    }

    /// <summary>
    /// The two Huddle themes' <c>Divider</c> - the border of message cards, tiles and roster cards - reads at
    /// least 1.5:1 against <c>Surface</c>. The other themes are imported faithfully and held to no floor here;
    /// <see cref="BuiltIn_DividerVersusSurface_IsRecorded"/> measures them.
    /// </summary>
    /// <param name="themeId"><see cref="ThemeDescriptor.Id"/> of the Huddle theme under test.</param>
    [Theory]
    [InlineData("huddle")]
    [InlineData("huddle-dark")]
    public void Huddle_DividerAgainstSurface_ReachesCardBorderVisibility(string themeId)
    {
        var descriptor = ThemeCatalog.BuiltIn.Single(candidate => string.Equals(candidate.Id, themeId, StringComparison.Ordinal));
        var palette = NativePalette(descriptor);

        var ratio = ContrastColour.Ratio(palette.Divider, palette.Surface);

        Assert.True(ratio >= 1.5, $"{themeId}: Divider vs Surface is {ratio:F2}:1, under the 1.5:1 card-border floor.");
    }

    /// <summary>
    /// Records <c>Divider</c> against <c>Surface</c> for every built-in theme, so a new or imported palette's
    /// card-border visibility is a measured number rather than a guess. It asserts only that each ratio is
    /// finite and at least 1:1; the 1.5:1 floor is enforced for the Huddle themes alone.
    /// </summary>
    [Fact]
    public void BuiltIn_DividerVersusSurface_IsRecorded()
    {
        Dictionary<string, double> measured = new(StringComparer.Ordinal);
        foreach (var descriptor in ThemeCatalog.BuiltIn)
        {
            var palette = NativePalette(descriptor);
            measured[descriptor.Id] = ContrastColour.Ratio(palette.Divider, palette.Surface);
        }

        Assert.Equal(ThemeCatalog.BuiltIn.Count, measured.Count);
        Assert.All(measured.Values, ratio => Assert.True(double.IsFinite(ratio) && ratio >= 1.0));
    }

    /// <summary>Every built-in theme sets <c>Tertiary</c>, the Review signal, to its own value rather than MudBlazor's default, and it is not the <c>Secondary</c> fill.</summary>
    [Fact]
    public void BuiltIn_EveryThemeSetsATertiaryOfItsOwn()
    {
        MudTheme untouched = new();

        foreach (var descriptor in ThemeCatalog.BuiltIn)
        {
            var palette = NativePalette(descriptor);
            var bare = descriptor.Mode == ThemeMode.Light ? untouched.PaletteLight.Tertiary : untouched.PaletteDark.Tertiary;

            Assert.NotEqual(Hex(bare), Hex(palette.Tertiary), StringComparer.OrdinalIgnoreCase);
            Assert.NotEqual(Hex(palette.Secondary), Hex(palette.Tertiary), StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// In an authored Theme every pair of signals, and each signal against the Accent, sit at least 30 degrees apart in
    /// hue (audit A2), so a meaning never rests on a hue that collides with another. The signals are Working
    /// (<c>Info</c>), Review (<c>Tertiary</c>), <c>Success</c>, <c>Warning</c> and <c>Error</c>; the Accent is <c>Primary</c>.
    /// Ported Themes take their signals from VS Code and may fall short; their second cues carry the meaning.
    /// </summary>
    /// <param name="themeId"><see cref="ThemeDescriptor.Id"/> of the authored theme under test.</param>
    [Theory]
    [InlineData("huddle")]
    [InlineData("huddle-dark")]
    public void Authored_SignalsAreAtLeastThirtyDegreesApartFromEachOtherAndTheAccent(string themeId)
    {
        var descriptor = ThemeCatalog.BuiltIn.Single(candidate => string.Equals(candidate.Id, themeId, StringComparison.Ordinal));

        AssertSignalHueDistances(themeId, NativePalette(descriptor));
    }

    /// <summary>
    /// Custom Accent derives its Review colour from whatever accent the Human picks, so for accents all round the
    /// wheel <c>Tertiary</c> stays at least 30 degrees from the accent and from every other signal. What the code cannot
    /// guarantee is the fixed signals against a picked accent: an accent chosen within 30 degrees of <c>Info</c>,
    /// <c>Success</c>, <c>Warning</c> or <c>Error</c> collides with that signal, and its second cue (the state dot's
    /// shape, the priority glyph) carries the meaning. That is the one hue-distance shortfall of Custom Accent.
    /// </summary>
    /// <param name="hue">The hue, in degrees, of the accent the Human picks.</param>
    /// <param name="dark">Whether the dark palette is built.</param>
    [Theory]
    [MemberData(nameof(AccentHues))]
    public void CustomAccent_TertiaryIsAtLeastThirtyDegreesFromTheAccentAndEverySignal(int hue, bool dark)
    {
        MudColor picked = new((double)hue, 0.7, 0.5, 1.0);
        var theme = AccentTheme.BuildTheme(picked.ToString(MudColorOutputFormats.Hex), dark);
        Palette palette = dark ? theme.PaletteDark : theme.PaletteLight;

        foreach (var (name, other) in new[] { ("Primary", palette.Primary), ("Info", palette.Info), ("Success", palette.Success), ("Warning", palette.Warning), ("Error", palette.Error) })
        {
            var distance = AccentPalette.HueDistance(palette.Tertiary.H, other.H);
            Assert.True(distance >= 30.0, $"Accent hue {hue} ({(dark ? "dark" : "light")}): Tertiary is {distance:F1} degrees from {name}.");
        }
    }

    /// <summary>Accent hues round the wheel, in both modes.</summary>
    public static TheoryData<int, bool> AccentHues()
    {
        TheoryData<int, bool> data = new();
        for (var hue = 0; hue < 360; hue += 20)
        {
            data.Add(hue, false);
            data.Add(hue, true);
        }

        return data;
    }

    /// <summary>
    /// The text ladder (audit A9): <c>TextPrimary</c> (Ink) reads at least 7:1 on <c>Surface</c> in every theme not
    /// recorded in <see cref="InkShortfalls"/>, and <c>TextSecondary</c> (Quiet ink) sits between Ink at 50% and Ink at
    /// 80% mixed into the Surface, so it is visibly quieter than Ink and never fainter than half of it. The 4.5:1
    /// floor for Quiet ink is held by <see cref="BuiltIn_NativePaletteContrast_ClearsWcagFloor"/>.
    /// </summary>
    /// <param name="themeId"><see cref="ThemeDescriptor.Id"/> of the theme under test.</param>
    [Theory]
    [MemberData(nameof(ThemeIds))]
    public void BuiltIn_TextLadder_InkIsSevenToOneAndQuietInkSitsBetweenFiftyAndEightyPercent(string themeId)
    {
        var descriptor = ThemeCatalog.BuiltIn.Single(candidate => string.Equals(candidate.Id, themeId, StringComparison.Ordinal));
        var palette = NativePalette(descriptor);
        var inkRatio = ContrastColour.Ratio(palette.TextPrimary, palette.Surface);
        var quietRatio = ContrastColour.Ratio(palette.TextSecondary, palette.Surface);
        var fifty = ContrastColour.Ratio(InkMix(palette, 0.5), palette.Surface);
        var eighty = ContrastColour.Ratio(InkMix(palette, 0.8), palette.Surface);

        if (!InkShortfalls.Contains(themeId))
        {
            Assert.True(inkRatio >= 7.0, $"{themeId}: Ink on Surface is {inkRatio:F2}:1, under 7:1.");
            Assert.True(quietRatio >= fifty && quietRatio <= eighty, $"{themeId}: Quiet ink is {quietRatio:F2}:1 on Surface, outside Ink 50% ({fifty:F2}:1) to Ink 80% ({eighty:F2}:1).");
        }
        else
        {
            Assert.True(inkRatio < 7.0 || quietRatio < fifty || quietRatio > eighty, $"{themeId}: no longer misses the text ladder - delete it from {nameof(InkShortfalls)}.");
        }
    }

    /// <summary>Every built-in theme id.</summary>
    public static TheoryData<string> ThemeIds()
    {
        TheoryData<string> data = new();
        foreach (var descriptor in ThemeCatalog.BuiltIn)
        {
            data.Add(descriptor.Id);
        }

        return data;
    }

    /// <summary>Every signal pair and each signal against the Accent is at least 30 degrees apart in hue.</summary>
    /// <param name="themeId">The theme under test, for the failure message.</param>
    /// <param name="palette">The theme's native palette.</param>
    private static void AssertSignalHueDistances(string themeId, Palette palette)
    {
        (string Name, double Hue)[] signals =
        [
            ("Info", palette.Info.H),
            ("Tertiary", palette.Tertiary.H),
            ("Success", palette.Success.H),
            ("Warning", palette.Warning.H),
            ("Error", palette.Error.H),
        ];

        for (var first = 0; first < signals.Length; first++)
        {
            var accentDistance = AccentPalette.HueDistance(signals[first].Hue, palette.Primary.H);
            Assert.True(accentDistance >= 30.0, $"{themeId}: {signals[first].Name} is {accentDistance:F1} degrees from the Accent.");

            for (var second = first + 1; second < signals.Length; second++)
            {
                var distance = AccentPalette.HueDistance(signals[first].Hue, signals[second].Hue);
                Assert.True(distance >= 30.0, $"{themeId}: {signals[first].Name} and {signals[second].Name} are {distance:F1} degrees apart.");
            }
        }
    }

    /// <summary>Ink (<c>TextPrimary</c>) mixed into <c>Surface</c> at <paramref name="amount"/>, as CSS <c>color-mix(in srgb, ...)</c> does it.</summary>
    /// <param name="palette">The palette whose Ink and Surface are mixed.</param>
    /// <param name="amount">The share of Ink, 0 to 1.</param>
    private static MudColor InkMix(Palette palette, double amount)
    {
        static int Channel(int ink, int surface, double share) => (int)Math.Round((ink * share) + (surface * (1.0 - share)));

        return new MudColor(
            Channel(palette.TextPrimary.R, palette.Surface.R, amount),
            Channel(palette.TextPrimary.G, palette.Surface.G, amount),
            Channel(palette.TextPrimary.B, palette.Surface.B, amount),
            255);
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
    /// The palette a theme actually carries: its <see cref="MudTheme.PaletteLight"/> when
    /// <see cref="ThemeDescriptor.Mode"/> is <see cref="ThemeMode.Light"/>, otherwise its
    /// <see cref="MudTheme.PaletteDark"/>. The other slot holds MudBlazor's own defaults and is never
    /// rendered, so it carries no legibility guarantee and nothing here measures it.
    /// </summary>
    /// <param name="descriptor">The theme descriptor to resolve.</param>
    private static Palette NativePalette(ThemeDescriptor descriptor)
    {
        Palette palette = descriptor.Mode == ThemeMode.Light ? descriptor.Theme.PaletteLight : descriptor.Theme.PaletteDark;
        return palette;
    }

    /// <summary>Renders <paramref name="color"/> as the <c>#rrggbb</c> text this suite compares colours as.</summary>
    /// <param name="color">The colour to render.</param>
    private static string Hex(MudColor color) => color.ToString(MudColorOutputFormats.Hex);

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

    /// <summary>
    /// The ported themes that miss the text ladder (Ink under 7:1 on Surface, or Quiet ink outside Ink 50% to 80%),
    /// pinned both ways like <see cref="DocumentedShortfalls"/>: a listed theme must still miss, so a stale entry fails
    /// the build. Every value is imported unmodified from its VS Code source, which is why these are recorded and not fixed.
    /// </summary>
    private static readonly HashSet<string> InkShortfalls = new(StringComparer.Ordinal)
    {
        // Ink 5.58:1 on Surface: VS Code's registry-default foreground #616161 on the #f3f3f3 Surface.
        "light-plus",
        // Ink 5.58:1 on Surface, the same pair.
        "quiet-light",
        // Ink 5.05:1 on Surface: the same #616161 foreground on Solarized Light's #eee8d5 Surface.
        "solarized-light",
        // Quiet ink 10.55:1 on Surface, above Ink 80% (5.83:1): this theme's descriptionForeground is the same #3b3b3b as its foreground, so Quiet ink equals Ink.
        "light-modern",
    };
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
