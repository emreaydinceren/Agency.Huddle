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
    /// <c>Primary</c> was rebranded from the original burgundy (<c>#4a154b</c>), first to a seafoam
    /// green and then, on request, to a cyan. The requested shade, <c>#00B7C3</c>, measures only
    /// 2.4:1 against this palette's own Background and Surface - under the WCAG 1.4.3 4.5:1 floor
    /// <c>ThemeCatalogTests</c> holds every built-in theme's Primary to, and would read as
    /// low-contrast text or a hard-to-read active nav link. <c>#007c85</c> keeps that same hue (184
    /// degrees) and saturation (full), darkened until it clears the floor - 4.80:1 against
    /// Background, 4.97:1 against Surface - the identical move <see cref="Dark"/>'s own <c>Primary</c>
    /// already made for the original burgundy brand hue, and the one <c>AccentPalette.ClampForContrast</c>
    /// automates for a Human's own accent colour choice.
    /// </para>
    /// </remarks>
    public static PaletteLight Light() => new()
    {
        Background = "#f2f0ed",
        Surface = "#fbfaf8",
        DrawerBackground = "#f5f4f2",
        BackgroundGray = "#e5e2e0",
        TableHover = "#efefed",
        Divider = "#e5e5e4",
        DividerLight = "#ededec",
        LinesInputs = "#e0e0df",
        LinesDefault = "#d4d3d1",
        TextPrimary = "#141413",
        TextSecondary = "#52514e",
        TextDisabled = "#8a8882",
        Primary = "#007c85",
        PrimaryDarken = "#00565c",
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
    /// ground, with hairline borders a step above that.
    /// </remarks>
    public static PaletteDark Dark() => new()
    {
        Background = "#1c1c1b",
        Surface = "#262625",
        DrawerBackground = "#1a1a19",
        BackgroundGray = "#2a2a29",
        TableHover = "#2a2a29",
        Divider = "#333332",
        DividerLight = "#2b2b2a",
        LinesInputs = "#454543",
        LinesDefault = "#5a5a57",
        TextPrimary = "#ececea",
        TextSecondary = "#a3a29d",

        // Also the composer's focus border, so it must clear WCAG 1.4.11's 3:1 against Surface:
        // 4.32:1.
        TextDisabled = "#8a8984",

        // Lightened from the original #5e2b60, which measured only 1.63:1 against this
        // palette's then Background (#1b1b1f), well under the 4.5:1 MudBlazor needs for
        // --mud-palette-primary to read on the active MudNavLink. #c07bc3 keeps the same brand
        // hue (~297.5 degrees, against the light palette's ~298.9) and a close saturation, only
        // lighter, and was verified with the WCAG 2.1 relative-luminance formula against the
        // current neutrals:
        //   - vs Surface (#262625):    4.950:1
        //   - vs Background (#1c1c1b): 5.573:1
        // Both clear the 4.5:1 text minimum.
        Primary = "#c07bc3",
        PrimaryDarken = "#7a3a7e",
        // White PrimaryContrastText only reaches 3.06:1 against the new, lighter Primary -
        // enough for large text but short of the 4.5:1 minimum the avatar monogram needs.
        // Black reaches 6.86:1, comfortably clearing it. The general reasoning behind
        // picking black or white now lives in ContrastColour.ReadableForeground.
        PrimaryContrastText = "#000000",
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
