using MudBlazor;

namespace Agency.Huddle.App.Themes;

/// <summary>
/// This application's own colours: the palettes it shipped in <c>wwwroot/theme.css</c> before the
/// migration to MudBlazor theming, resolved into a MudBlazor <see cref="Palette"/> each rather than
/// CSS <c>light-dark()</c> pairs.
/// </summary>
/// <remarks>
/// Both palettes here are authored, which makes this the one file in <c>Themes/</c> that yields two
/// catalog entries — <see cref="LightDescriptor"/> and <see cref="DarkDescriptor"/> — rather than
/// one. A theme in this catalog is a single palette
/// (<c>docs/adr/0017-a-theme-is-a-palette-not-a-pair.md</c>), so two palettes are two themes; the
/// alternative would have been to leave the dark palette unreachable, and it is the more carefully
/// measured of the two.
/// </remarks>
internal static class HuddleTheme
{
    /// <summary>
    /// Builds a fresh instance of Huddle Light's palette on every call, so no <see cref="Palette"/>
    /// instance is shared - and mutated - across the themes in this catalog.
    /// </summary>
    /// <returns>A new <see cref="PaletteLight"/> carrying today's light palette values.</returns>
    /// <remarks>
    /// <para>
    /// The neutrals are warm, low-contrast greys sampled from the Claude desktop app's light
    /// surface: an off-white ground with white inputs raised on it, and hairline borders a shade
    /// darker than the ground rather than mid-grey rules.
    /// </para>
    /// <para>
    /// <c>Primary</c> is <c>#5b64c8</c>, a deeper indigo than the dark palette's: 5.12:1 against Surface
    /// and 4.50:1 against Background, so accent-coloured text clears the 4.5:1 floor.
    /// <c>PrimaryContrastText</c> is white, which reads 5.12:1 on it. Each Huddle Theme authors its own
    /// accent (<c>docs/adr/0017-a-theme-is-a-palette-not-a-pair.md</c>) so neither carries a documented
    /// shortfall. <c>PrimaryDarken</c> keeps the same hue, a step darker.
    /// </para>
    /// <para>
    /// <c>Divider</c> is <c>#d0cfcc</c>, 1.56:1 against Surface, so message cards and tiles read as
    /// bordered (at least 1.5:1).
    /// </para>
    /// </remarks>
    public static PaletteLight Light() => new()
    {
        Background = "#f2f0ed",
        Surface = "#ffffff",
        DrawerBackground = "#e6e2dc",
        BackgroundGray = "#e5e2e0",
        TableHover = "#efefed",
        Divider = "#d0cfcc",
        DividerLight = "#ededec",
        LinesInputs = "#e0e0df",
        // The pane and chrome hairlines on the one shell surface: 2.1:1 against Surface, so a divider reads
        // as a line without turning into a frame. #d4d3d1 (1.5:1) disappeared once the panes stopped being
        // cards on a darker ground.
        LinesDefault = "#b4b2ad",
        TextPrimary = "#141413",
        TextSecondary = "#45443f",
        ActionDefault = "#45443f",
        DrawerText = "#141413",
        DrawerIcon = "#45443f",
        TextDisabled = "#8a8882",
        Primary = "#5b64c8",
        PrimaryDarken = "#3e47a8",
        PrimaryContrastText = "#ffffff",
        Secondary = "#3a6fd8",
        Success = "#2e9e44",
        Error = "#b32121",
        Warning = "#c77d00",
        WarningLighten = "#e0a83e",
        ErrorLighten = "#e3b6b6",
        Info = "#1d4f8f",
        OverlayDark = "rgba(0,0,0,0.4)",
    };

    /// <summary>
    /// Builds a fresh instance of Huddle Dark's palette on every call, so no <see cref="Palette"/>
    /// instance is shared - and mutated - across the themes in this catalog.
    /// </summary>
    /// <returns>A new <see cref="PaletteDark"/> carrying today's dark palette values.</returns>
    /// <remarks>
    /// The neutrals are near-neutral charcoals sampled from the Claude desktop app's dark surface,
    /// the counterpart of <see cref="Light"/>: inputs and the Human's bubble lifted a step off the
    /// ground, with hairline borders a step above that. <c>Divider</c> is <c>#444442</c>, 1.55:1 against
    /// Surface, so card and tile borders stay visible.
    /// </remarks>
    public static PaletteDark Dark() => new()
    {
        Background = "#1c1c1b",
        Surface = "#262625",
        DrawerBackground = "#1a1a19",
        BackgroundGray = "#2a2a29",
        TableHover = "#2a2a29",
        Divider = "#444442",
        DividerLight = "#2b2b2a",
        LinesInputs = "#454543",
        // The pane and chrome hairlines: 2.5:1 against Surface (#262625), the same "clearly a line" weight
        // as Huddle Light's.
        LinesDefault = "#646461",
        TextPrimary = "#ececea",
        TextSecondary = "#a3a29d",
        ActionDefault = "#a3a29d",
        DrawerText = "#ececea",
        DrawerIcon = "#a3a29d",

        // Also the composer's focus border, so it must clear WCAG 1.4.11's 3:1 against Surface:
        // 4.32:1.
        TextDisabled = "#8a8984",

        // A lighter periwinkle than Huddle Light's accent: 5.58:1 against Surface (#262625) and 6.28:1
        // against Background (#1c1c1b). Near-black #141413 on it reads 7.5:1, so PrimaryContrastText is that, not pure black.
        Primary = "#8f97e6",
        PrimaryDarken = "#616bc1",
        PrimaryContrastText = "#141413",
        Secondary = "#7aa2f7",
        Success = "#3fbf5a",
        Error = "#e05a5a",
        Warning = "#e0a33e",
        WarningLighten = "#e8c46a",
        ErrorLighten = "#6b3535",
        Info = "#8ab4f8",
        OverlayDark = "rgba(0,0,0,0.6)",
    };

    /// <summary>
    /// Huddle Light's descriptor, and the catalog's default theme. Its id stays the bare
    /// <c>"huddle"</c> the catalog has always used, so an <c>appearance.json</c> written before this
    /// theme was split in two still resolves to exactly the palette it resolved to then.
    /// </summary>
    public static ThemeDescriptor LightDescriptor { get; } = new(
        Id: "huddle",
        Label: "Huddle Light",
        Theme: WithSoftCorners(ThemeDefaults.CreateLight(Light())),
        Mode: ThemeMode.Light,
        Group: ThemeGroup.Light);

    /// <summary>
    /// Rounds both Huddle themes' corners to 8px, up from MudBlazor's 4px default, to match the
    /// soft chips and inputs their palettes were sampled from. <c>app.css</c> reads the same value
    /// through <c>--mud-default-borderradius</c>, so every other theme keeps its 4px corners.
    /// </summary>
    /// <param name="theme">A Huddle theme, freshly built.</param>
    /// <returns><paramref name="theme"/>, with its default border radius set.</returns>
    private static MudTheme WithSoftCorners(MudTheme theme)
    {
        theme.LayoutProperties.DefaultBorderRadius = "8px";
        return theme;
    }

    /// <summary>Huddle Dark's descriptor - the same application colours, authored for a dark ground.</summary>
    public static ThemeDescriptor DarkDescriptor { get; } = new(
        Id: "huddle-dark",
        Label: "Huddle Dark",
        Theme: WithSoftCorners(ThemeDefaults.CreateDark(Dark())),
        Mode: ThemeMode.Dark,
        Group: ThemeGroup.Dark);
}
