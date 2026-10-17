using MudBlazor;

namespace Agency.Huddle.App.Themes;

/// <summary>
/// Nordic: an Obsidian-like paper pair of Themes - birch and fjord-slate grounds, sage accent, heather Review -
/// mapped from the <c>09-nordic-paper</c> mockup set's <c>app.css</c>, light (<c>:root</c>) and dark
/// (<c>[data-theme="dark"]</c>).
/// </summary>
/// <remarks>
/// <para>
/// Each value's comment names the mockup token it came from. <b>ADJUSTED</b> marks a value moved off the mockup to
/// clear a Huddle rule. Nordic is the one Theme that carries a style pack (<c>wwwroot/packs/nordic.css</c>,
/// ADR-0057), which is what lets it express the set's four grounds. So the roles differ from every other Theme:
/// <c>Surface</c> is the white "raised paper" <c>--bg-elev</c> (cards, composer, inputs, dialogs, menus and the
/// current item), and <c>Background</c> is the reading ground <c>--bg</c>, which the pack paints on the shell.
/// <c>DrawerBackground</c> is the sunk step and <c>BackgroundGray</c> the light rail.
/// </para>
/// <para>
/// The set paints Done in <c>--sage</c>, the accent's own hue, so <c>Success</c> is its teal <c>--lichen</c> turned
/// a few degrees toward green, 30 degrees from the accent and from <c>Info</c> (audit A2). <c>Secondary</c> is
/// <c>--blue-ink</c>, because <c>--heather</c> is <c>Tertiary</c>, the Review signal.
/// </para>
/// </remarks>
internal static class NordicTheme
{
    /// <summary>The name of the style pack both Nordic Themes carry: <c>wwwroot/packs/nordic.css</c>.</summary>
    public const string Pack = "nordic";

    // The set's UI face. Literata (reading) and IBM Plex Mono (code) are the pack's --font-read and --font-mono.
    private static readonly string[] UiFontFamily =
    [
        "IBM Plex Sans", "system-ui", "-apple-system", "Segoe UI", "sans-serif",
    ];

    /// <summary>Builds a fresh instance of Nordic Light's palette on every call, so no <see cref="Palette"/> is shared across Themes.</summary>
    /// <returns>A new <see cref="PaletteLight"/>.</returns>
    public static PaletteLight Light() => new()
    {
        Background = "#f6f6f1", // --bg, birch: the page ground the pack paints on the shell
        Surface = "#ffffff", // --bg-elev, the raised paper
        DrawerBackground = "#edede6", // --bg-sunk, the sidebar ground and the wells
        BackgroundGray = "#e6e6de", // --bg-ribbon, the rail
        TableHover = "#e8e8e0", // --bg-hover
        Divider = "#cbcbbe", // ADJUSTED from --line #d9d9cf (1.42:1 on paper) to 1.64:1
        DividerLight = "#d9d9cf", // --line, the region hairline (decorative, below the Divider floor by ADR-0057)
        LinesInputs = "#bfc1b5", // --line-strong
        LinesDefault = "#abad9e", // ADJUSTED from --line along --line-strong's hue, 2.28:1 on paper
        TextPrimary = "#14181b", // --ink
        TextSecondary = "#4d555b", // --ink-3
        ActionDefault = "#4d555b",
        DrawerText = "#14181b",
        DrawerIcon = "#4d555b",
        TextDisabled = "#6b7278", // --ink-4
        Primary = "#386247", // --sage-strong
        PrimaryDarken = "#264631", // --sage-ink
        PrimaryContrastText = "#ffffff", // --on-accent
        Secondary = "#274f75", // --blue-ink
        Tertiary = "#745a88", // Review: --heather
        Success = "#35746c", // ADJUSTED from --sage #4a7a5b (the accent's hue) to --lichen at hue 172
        Warning = "#b07208", // --cloud, the "Needs you" signal
        Error = "#9c372f", // --danger
        Info = "#43709a", // --blue
        OverlayDark = "rgba(0,0,0,0.4)",
    };

    /// <summary>Builds a fresh instance of Nordic Dark's palette on every call, so no <see cref="Palette"/> is shared across Themes.</summary>
    /// <returns>A new <see cref="PaletteDark"/>.</returns>
    /// <remarks><c>PrimaryDarken</c> is left for MudBlazor to derive: the mockup hovers to the lighter <c>--sage-ink</c>.</remarks>
    public static PaletteDark Dark() => new()
    {
        Background = "#161a1d", // --bg, fjord slate: the page ground the pack paints on the shell
        Surface = "#1c2125", // --bg-elev, the raised paper
        DrawerBackground = "#121518", // --bg-sunk; the pack also paints the rail with it (--bg-ribbon #101316 has no slot)
        BackgroundGray = "#222830", // --bg-hover: the sunk step is only 1.05:1 on the ground
        TableHover = "#222830", // --bg-hover
        Divider = "#363f45", // ADJUSTED from --line #262d32 (1.16:1 on paper) to 1.51:1
        DividerLight = "#262d32", // --line, the region hairline (decorative, below the Divider floor by ADR-0057)
        LinesInputs = "#343c42", // --line-strong
        LinesDefault = "#4f5b64", // ADJUSTED from --line along --line-strong's hue, 2.33:1 on paper
        TextPrimary = "#e3e7e3", // --ink
        TextSecondary = "#939b98", // --ink-3
        ActionDefault = "#939b98",
        DrawerText = "#e3e7e3",
        DrawerIcon = "#939b98",
        TextDisabled = "#6d7673", // --ink-4
        Primary = "#a3c2ad", // --sage-strong
        PrimaryContrastText = "#10201a", // --on-accent
        Secondary = "#b3c8da", // --blue-ink
        Tertiary = "#b3a1bf", // Review: --heather
        Success = "#85b5b0", // ADJUSTED from --sage #93b39e (the accent's hue) to --lichen at hue 174
        Warning = "#e0b062", // --cloud
        Error = "#e08a82", // --danger
        Info = "#8ea9c1", // --blue
        OverlayDark = "rgba(0,0,0,0.6)",
    };

    /// <summary>Nordic Light's descriptor.</summary>
    public static ThemeDescriptor LightDescriptor { get; } = new(
        Id: "nordic-light",
        Label: "Nordic Light",
        Theme: WithNordicStyle(ThemeDefaults.CreateLight(Light()), dark: false),
        Mode: ThemeMode.Light,
        Group: ThemeGroup.Light,
        Pack: Pack);

    /// <summary>Nordic Dark's descriptor.</summary>
    public static ThemeDescriptor DarkDescriptor { get; } = new(
        Id: "nordic-dark",
        Label: "Nordic Dark",
        Theme: WithNordicStyle(ThemeDefaults.CreateDark(Dark()), dark: true),
        Mode: ThemeMode.Dark,
        Group: ThemeGroup.Dark,
        Pack: Pack);

    /// <summary>
    /// Applies what the set changes beyond the palette: its 8px corners, IBM Plex Sans as the UI face, and its three
    /// warm shadows. Elevation 1 becomes the paper at rest, 2 to 4 the hover and composer lift, and 5 upward (menus
    /// at 8, dialogs at 24) the floating layer. Sizes, weights and line heights stay the shared type scale.
    /// </summary>
    /// <param name="theme">A Nordic theme, freshly built.</param>
    /// <param name="dark">Whether <paramref name="theme"/> is Nordic Dark, whose shadows are deeper and neutral.</param>
    /// <returns><paramref name="theme"/>, restyled.</returns>
    private static MudTheme WithNordicStyle(MudTheme theme, bool dark)
    {
        theme.LayoutProperties.DefaultBorderRadius = "8px";
        theme.Typography.Default.FontFamily = UiFontFamily;

        string paper = dark
            ? "0 1px 0 rgba(0,0,0,.25)"
            : "0 1px 0 rgba(20,24,27,.06), 0 1px 3px rgba(20,24,27,.08)";
        string lift = dark
            ? "0 2px 6px rgba(0,0,0,.3), 0 10px 28px rgba(0,0,0,.3)"
            : "0 2px 4px rgba(20,24,27,.07), 0 8px 24px rgba(20,24,27,.11)";
        string floating = dark
            ? "0 16px 48px rgba(0,0,0,.55), 0 2px 8px rgba(0,0,0,.35)"
            : "0 12px 40px rgba(31,36,39,.16), 0 2px 6px rgba(31,36,39,.08)";
        for (int i = 1; i < theme.Shadows.Elevation.Length; i++)
        {
            theme.Shadows.Elevation[i] = i switch
            {
                1 => paper,
                2 or 3 or 4 => lift,
                _ => floating,
            };
        }

        return theme;
    }
}
