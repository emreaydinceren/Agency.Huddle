using MudBlazor;

namespace Agency.Huddle.App.Themes;

/// <summary>
/// Lumen: a calm, Linear-style minimal pair of Themes - cool 240-degree neutrals, hairlines and one soft-violet
/// accent - mapped from the <c>01-lumen-minimal</c> mockup set's <c>app.css</c>, light (<c>:root</c>) and dark
/// (<c>[data-theme="dark"]</c>).
/// </summary>
/// <remarks>
/// <para>
/// Each value's comment names the mockup token it came from. <b>ADJUSTED</b> marks a value moved off the mockup
/// to clear a Huddle floor: Lumen's 1.23:1 hairlines are below the 1.5:1 card-border floor, and its accent
/// <c>#8379d8</c> reads only 3.69:1 on white.
/// </para>
/// <para>
/// Lumen has no Review hue of its own (it paints Review green), so <c>Tertiary</c> is a magenta at 296 degrees,
/// 50 from the accent and 60 from Error, keeping every signal 30 degrees from the others (audit A2).
/// </para>
/// </remarks>
internal static class LumenTheme
{
    /// <summary>Builds a fresh instance of Lumen Light's palette on every call, so no <see cref="Palette"/> is shared across Themes.</summary>
    /// <returns>A new <see cref="PaletteLight"/>.</returns>
    public static PaletteLight Light() => new()
    {
        Background = "#ffffff", // --bg
        Surface = "#ffffff", // --bg = --surface
        DrawerBackground = "#fafafa", // --bg-subtle, the sidebar ground
        BackgroundGray = "#f4f4f5", // --bg-muted
        TableHover = "#f3f3f5", // --bg-hover
        Divider = "#d2d2da", // ADJUSTED from --border #e7e7eb (1.23:1) to 1.50:1
        DividerLight = "#e7e7eb", // --border
        LinesInputs = "#d4d4db", // --border-strong
        LinesDefault = "#b4b4bd", // ADJUSTED from --border to --grey-dot, 2.06:1
        TextPrimary = "#0f0f14", // --text
        TextSecondary = "#64646f", // ADJUSTED from --text-3 #6b6b76, to hold 4.5:1 on the selected+hover tint
        ActionDefault = "#64646f",
        DrawerText = "#0f0f14",
        DrawerIcon = "#64646f",
        TextDisabled = "#87878a", // --text at Lumen's 50% disabled opacity over --bg
        Primary = "#7368d3", // ADJUSTED from --accent #8379d8 (3.69:1), same hue: 4.52:1
        PrimaryDarken = "#5f55c0", // --accent-text
        PrimaryContrastText = "#ffffff", // --on-accent
        Secondary = "#0f766e", // --teal
        Tertiary = "#9a36a1", // Review: Lumen has no Review hue; hsl(296 50% 42%)
        Success = "#15803d", // --green
        Warning = "#a16207", // --amber
        Error = "#c42b2b", // --red
        Info = "#207cba", // Lumen's remote-cursor hue 204, darkened to 4.51:1
        OverlayDark = "rgba(0,0,0,0.4)",
    };

    /// <summary>Builds a fresh instance of Lumen Dark's palette on every call, so no <see cref="Palette"/> is shared across Themes.</summary>
    /// <returns>A new <see cref="PaletteDark"/>.</returns>
    public static PaletteDark Dark() => new()
    {
        Background = "#0b0b0f", // --bg
        Surface = "#0b0b0f", // --bg; the cards' #111117 lift is lost to the one shell surface
        DrawerBackground = "#0e0e13", // --bg-subtle
        BackgroundGray = "#15151c", // --bg-muted
        TableHover = "#16161d", // --bg-hover
        Divider = "#30303e", // ADJUSTED from --border #1f1f28 (1.20:1) to 1.51:1
        DividerLight = "#1f1f28", // --border
        LinesInputs = "#2d2d39", // --border-strong
        LinesDefault = "#4b4b57", // ADJUSTED from --border to --grey-dot, 2.29:1
        TextPrimary = "#ececf1", // --text
        TextSecondary = "#898994", // ADJUSTED from --text-3 #83838f, to hold 4.5:1 on the selected+hover tint
        ActionDefault = "#898994",
        DrawerText = "#ececf1",
        DrawerIcon = "#898994",
        TextDisabled = "#7c7c80", // --text at 50% over --bg
        Primary = "#8379d8", // --accent, verbatim: 5.32:1
        PrimaryDarken = "#948bdf", // --accent-hover (Lumen lightens on hover in dark)
        PrimaryContrastText = "#0f0f14", // ADJUSTED from --on-accent #ffffff (3.69:1): 5.18:1
        Secondary = "#14b8a6", // --teal-dot
        Tertiary = "#d788dd", // Review: hsl(296 55% 70%)
        Success = "#22c55e", // --green-dot
        Warning = "#f59e0b", // --amber-dot
        Error = "#f87171", // --red
        Info = "#2691d9", // hsl(204 70% 50%), Lumen's remote-cursor hue
        OverlayDark = "rgba(0,0,0,0.6)", // --scrim
    };

    /// <summary>Lumen Light's descriptor.</summary>
    public static ThemeDescriptor LightDescriptor { get; } = new(
        Id: "lumen-light",
        Label: "Lumen Light",
        Theme: WithCorners(ThemeDefaults.CreateLight(Light())),
        Mode: ThemeMode.Light,
        Group: ThemeGroup.Light);

    /// <summary>Lumen Dark's descriptor.</summary>
    public static ThemeDescriptor DarkDescriptor { get; } = new(
        Id: "lumen-dark",
        Label: "Lumen Dark",
        Theme: WithCorners(ThemeDefaults.CreateDark(Dark())),
        Mode: ThemeMode.Dark,
        Group: ThemeGroup.Dark);

    /// <summary>Rounds the corners to Lumen's 6px <c>--r</c>.</summary>
    /// <param name="theme">A Lumen theme, freshly built.</param>
    /// <returns><paramref name="theme"/>, with its default border radius set.</returns>
    private static MudTheme WithCorners(MudTheme theme)
    {
        theme.LayoutProperties.DefaultBorderRadius = "6px";
        return theme;
    }
}
