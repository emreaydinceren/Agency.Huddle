using MudBlazor;

namespace Agency.Huddle.App.Themes;

/// <summary>
/// Cobalt Soft: a pastel-periwinkle data-productivity pair of Themes - cool blue-grey neutrals, an indigo accent and
/// a violet Review - mapped from the <c>14b-cobalt-soft</c> mockup set's <c>app.css</c>, light (<c>:root</c>) and
/// dark (<c>[data-theme="dark"]</c>).
/// </summary>
/// <remarks>
/// <para>
/// Each value's comment names the mockup token it came from. <b>ADJUSTED</b> marks a value moved off the mockup to
/// clear a Huddle rule; <b>SUBSTITUTED</b> marks a role filled from a different token of the set. The mockup
/// separates panes with a ground change plus a faint line, so Huddle's one surface takes <c>--border-strong</c>
/// for its chrome lines.
/// </para>
/// <para>
/// <c>--violet</c> sits 26 degrees from the accent, so the Review signal, <c>Tertiary</c>, is that violet turned
/// to hue 262, 32 degrees away (audit A2), and <c>Secondary</c> keeps the violet itself.
/// </para>
/// </remarks>
internal static class CobaltSoftTheme
{
    /// <summary>Builds a fresh instance of Cobalt Soft Light's palette on every call, so no <see cref="Palette"/> is shared across Themes.</summary>
    /// <returns>A new <see cref="PaletteLight"/>.</returns>
    public static PaletteLight Light() => new()
    {
        Background = "#ffffff", // --bg
        Surface = "#ffffff", // --bg = --surface
        DrawerBackground = "#f4f6fa", // --bg-app, the sidebar ground
        BackgroundGray = "#e9edf4", // --surface-3
        TableHover = "#f5f7fb", // --surface-2
        Divider = "#cbd2de", // --border, 1.52:1
        DividerLight = "#e2e6ee", // --border-soft
        LinesInputs = "#cbd2de", // --border
        LinesDefault = "#aeb7c7", // SUBSTITUTED --border-strong, 2.02:1
        TextPrimary = "#0c1325", // --text
        TextSecondary = "#545e71", // --text-3
        ActionDefault = "#545e71",
        DrawerText = "#0c1325",
        DrawerIcon = "#545e71",
        TextDisabled = "#8a93a5", // --idle
        Primary = "#5a6ed6", // ADJUSTED from --primary #5b6fd6 (4.48:1) to 4.53:1
        PrimaryDarken = "#4d61c7", // --primary-hover
        PrimaryContrastText = "#ffffff", // --on-primary
        Secondary = "#8a70cf", // --violet
        Tertiary = "#9370cf", // Review: ADJUSTED from --violet to hue 262
        Success = "#13a35f", // --green-dot
        Warning = "#d38200", // ADJUSTED from --amber-dot #e08a00, for chip text at 4.5:1
        Error = "#b8232b", // --red
        Info = "#06707a", // --teal
        OverlayDark = "rgba(0,0,0,0.4)",
    };

    /// <summary>Builds a fresh instance of Cobalt Soft Dark's palette on every call, so no <see cref="Palette"/> is shared across Themes.</summary>
    /// <returns>A new <see cref="PaletteDark"/>.</returns>
    public static PaletteDark Dark() => new()
    {
        Background = "#0e1220", // --bg
        Surface = "#0e1220", // --bg; the raised --surface #131a2b is lost to the one shell surface
        DrawerBackground = "#0a0d18", // --bg-app
        BackgroundGray = "#1e2841", // --surface-3
        TableHover = "#172036", // --surface-2
        Divider = "#2b3653", // --border, 1.56:1
        DividerLight = "#222c46", // --border-soft
        LinesInputs = "#2b3653", // --border
        LinesDefault = "#42507a", // SUBSTITUTED --border-strong, 2.36:1
        TextPrimary = "#eef2fb", // --text
        TextSecondary = "#a0aac2", // --text-3
        ActionDefault = "#a0aac2",
        DrawerText = "#eef2fb",
        DrawerIcon = "#a0aac2",
        TextDisabled = "#6f7a94", // --idle
        Primary = "#6376d7", // ADJUSTED from --primary #5f72d6 (4.32:1) to 4.53:1
        PrimaryDarken = "#6f82e0", // --primary-hover (the dark hover is lighter)
        PrimaryContrastText = "#0e1220", // ADJUSTED from --on-primary #ffffff (4.12:1) to --text-inv
        Secondary = "#a28be0", // --violet
        Tertiary = "#aa8be0", // Review: ADJUSTED from --violet to hue 262
        Success = "#2fcf86", // --green-dot
        Warning = "#f5a524", // --amber-dot
        Error = "#ff8a8a", // --red
        Info = "#24cdd7", // ADJUSTED from --teal #5fdce4, for the presence badge's glyph at 3:1
        OverlayDark = "rgba(0,0,0,0.6)",
    };

    /// <summary>Cobalt Soft Light's descriptor.</summary>
    public static ThemeDescriptor LightDescriptor { get; } = new(
        Id: "cobalt-soft-light",
        Label: "Cobalt Soft Light",
        Theme: WithCorners(ThemeDefaults.CreateLight(Light())),
        Mode: ThemeMode.Light,
        Group: ThemeGroup.Light);

    /// <summary>Cobalt Soft Dark's descriptor.</summary>
    public static ThemeDescriptor DarkDescriptor { get; } = new(
        Id: "cobalt-soft-dark",
        Label: "Cobalt Soft Dark",
        Theme: WithCorners(ThemeDefaults.CreateDark(Dark())),
        Mode: ThemeMode.Dark,
        Group: ThemeGroup.Dark);

    /// <summary>Rounds the corners to the set's 8px.</summary>
    /// <param name="theme">A Cobalt Soft theme, freshly built.</param>
    /// <returns><paramref name="theme"/>, with its default border radius set.</returns>
    private static MudTheme WithCorners(MudTheme theme)
    {
        theme.LayoutProperties.DefaultBorderRadius = "8px";
        return theme;
    }
}
