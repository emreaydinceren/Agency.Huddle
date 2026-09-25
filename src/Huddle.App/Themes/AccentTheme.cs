using MudBlazor;
using MudBlazor.Utilities;

namespace Agency.Huddle.App.Themes;

/// <summary>
/// "Custom Accent": <see cref="HuddleTheme"/>'s own neutrals and semantic colours, with
/// <c>Primary</c>/<c>PrimaryDarken</c>/<c>PrimaryContrastText</c> replaced by an accent colour the
/// Human picks in the Appearance tab, clamped for WCAG contrast through
/// <see cref="AccentPalette.ClampForContrast"/> - an arbitrary accent colour carries no contrast
/// guarantee the way every hand-tuned palette in <see cref="ThemeCatalog"/> does.
/// </summary>
/// <remarks>
/// <para>
/// Like <see cref="HuddleTheme"/>, this yields two catalog entries because it authors two palettes,
/// one per <see cref="ThemeMode"/> (<c>docs/adr/0017-a-theme-is-a-palette-not-a-pair.md</c>). Unlike
/// every other entry in <see cref="ThemeCatalog.BuiltIn"/>, a descriptor's own
/// <see cref="ThemeDescriptor.Theme"/> here is only ever a placeholder - plain <see cref="HuddleTheme"/>,
/// Primary and all - because the real accent colour is <see cref="Agency.Huddle.App.Appearance.AppearanceSettings.AccentColorHex"/>,
/// a per-installation choice with no value until the Human picks one. <see cref="Agency.Huddle.App.Components.Layout.MainLayout"/>
/// is what resolves the real, accent-derived theme via <see cref="BuildTheme(string, bool)"/> once a
/// colour is stored; the placeholder exists so <c>AppearanceStore.IsKnownTheme</c> accepts the id
/// before then, and so the Appearance tab's picker has a row and a colour to show before a colour is
/// chosen.
/// </para>
/// <para>
/// This was originally meant to auto-detect the browser's OS/user accent colour via the CSS
/// <c>AccentColor</c> system colour keyword. That keyword turned out to resolve to a fixed Chromium
/// blue rather than the real OS accent, confirmed empirically in two independent Chromium browsers on
/// the same machine against the true registry value - so the Human picks the colour directly instead.
/// </para>
/// </remarks>
internal static class AccentTheme
{
    /// <summary>
    /// Builds a fresh Custom Accent <see cref="MudTheme"/>: <see cref="HuddleTheme.Light"/> or
    /// <see cref="HuddleTheme.Dark"/>'s own palette, with <c>Primary</c>/<c>PrimaryDarken</c>/
    /// <c>PrimaryContrastText</c> replaced by <paramref name="accentHex"/> once clamped for contrast.
    /// </summary>
    /// <param name="accentHex">The Human-picked accent colour, as <c>#rrggbb</c>.</param>
    /// <param name="dark">Whether to build the dark palette (<see cref="HuddleTheme.Dark"/>'s neutrals) or the light one (<see cref="HuddleTheme.Light"/>'s).</param>
    /// <returns>A theme with Huddle's own neutrals and an accent-derived, contrast-safe Primary.</returns>
    public static MudTheme BuildTheme(string accentHex, bool dark)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accentHex);

        MudColor accent = new(accentHex);
        if (dark)
        {
            PaletteDark palette = HuddleTheme.Dark();
            ApplyAccent(palette, accent, towardBlack: false);
            return ThemeDefaults.CreateDark(palette);
        }

        PaletteLight lightPalette = HuddleTheme.Light();
        ApplyAccent(lightPalette, accent, towardBlack: true);
        return ThemeDefaults.CreateLight(lightPalette);
    }

    /// <summary>Replaces <paramref name="palette"/>'s Primary family in place with <paramref name="accent"/>, clamped against that same palette's Background and Surface.</summary>
    /// <param name="palette">The freshly built <see cref="HuddleTheme.Light"/> or <see cref="HuddleTheme.Dark"/> palette to mutate.</param>
    /// <param name="accent">The Human-picked accent colour, unclamped.</param>
    /// <param name="towardBlack">Passed straight through to <see cref="AccentPalette.ClampForContrast"/>: darken for a light palette, lighten for a dark one.</param>
    private static void ApplyAccent(Palette palette, MudColor accent, bool towardBlack)
    {
        var clamped = AccentPalette.ClampForContrast(accent, palette.Background, palette.Surface, towardBlack);
        palette.Primary = clamped;
        palette.PrimaryDarken = clamped.ColorRgbDarken().Value;
        palette.PrimaryContrastText = ContrastColour.ReadableForeground(clamped);
    }

    /// <summary>Custom Accent Light's descriptor. <see cref="ThemeDescriptor.Theme"/> is a placeholder - see the type's remarks.</summary>
    public static ThemeDescriptor LightDescriptor { get; } = new(
        Id: "custom-accent",
        Label: "Custom Accent Light",
        Theme: ThemeDefaults.CreateLight(HuddleTheme.Light()),
        Mode: ThemeMode.Light,
        Group: ThemeGroup.Light);

    /// <summary>Custom Accent Dark's descriptor. <see cref="ThemeDescriptor.Theme"/> is a placeholder - see the type's remarks.</summary>
    public static ThemeDescriptor DarkDescriptor { get; } = new(
        Id: "custom-accent-dark",
        Label: "Custom Accent Dark",
        Theme: ThemeDefaults.CreateDark(HuddleTheme.Dark()),
        Mode: ThemeMode.Dark,
        Group: ThemeGroup.Dark);
}
