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
/// clear a Huddle rule. Surface is the mockup's reading ground <c>--bg</c>; its white "raised paper" card layer
/// cannot be expressed on Huddle's one shell surface.
/// </para>
/// <para>
/// The set paints Done in <c>--sage</c>, the accent's own hue, so <c>Success</c> is its teal <c>--lichen</c> turned
/// a few degrees toward green, 30 degrees from the accent and from <c>Info</c> (audit A2). <c>Secondary</c> is
/// <c>--blue-ink</c>, because <c>--heather</c> is <c>Tertiary</c>, the Review signal.
/// </para>
/// </remarks>
internal static class NordicTheme
{
    /// <summary>Builds a fresh instance of Nordic Light's palette on every call, so no <see cref="Palette"/> is shared across Themes.</summary>
    /// <returns>A new <see cref="PaletteLight"/>.</returns>
    public static PaletteLight Light() => new()
    {
        Background = "#e6e6de", // --bg-ribbon
        Surface = "#f6f6f1", // --bg, birch
        DrawerBackground = "#edede6", // --bg-sunk, the sidebar ground
        BackgroundGray = "#e6e6de", // --bg-ribbon: --bg-sunk is only 1.07:1 on birch
        TableHover = "#e8e8e0", // --bg-hover
        Divider = "#cbcbbe", // ADJUSTED from --line #d9d9cf (1.31:1) to 1.51:1
        DividerLight = "#d9d9cf", // --line
        LinesInputs = "#bfc1b5", // --line-strong
        LinesDefault = "#abad9e", // ADJUSTED from --line along --line-strong's hue, 2.10:1
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
        Background = "#101316", // --bg-ribbon
        Surface = "#161a1d", // --bg, fjord slate
        DrawerBackground = "#121518", // --bg-sunk
        BackgroundGray = "#222830", // --bg-hover: the sunk step is only 1.05:1 on Surface
        TableHover = "#222830", // --bg-hover
        Divider = "#313a40", // ADJUSTED from --line #262d32 (1.25:1) to 1.51:1
        DividerLight = "#262d32", // --line
        LinesInputs = "#343c42", // --line-strong
        LinesDefault = "#4f5b64", // ADJUSTED from --line along --line-strong's hue, 2.51:1
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
        Theme: WithCorners(ThemeDefaults.CreateLight(Light())),
        Mode: ThemeMode.Light,
        Group: ThemeGroup.Light);

    /// <summary>Nordic Dark's descriptor.</summary>
    public static ThemeDescriptor DarkDescriptor { get; } = new(
        Id: "nordic-dark",
        Label: "Nordic Dark",
        Theme: WithCorners(ThemeDefaults.CreateDark(Dark())),
        Mode: ThemeMode.Dark,
        Group: ThemeGroup.Dark);

    /// <summary>Rounds the corners to the set's 8px.</summary>
    /// <param name="theme">A Nordic theme, freshly built.</param>
    /// <returns><paramref name="theme"/>, with its default border radius set.</returns>
    private static MudTheme WithCorners(MudTheme theme)
    {
        theme.LayoutProperties.DefaultBorderRadius = "8px";
        return theme;
    }
}
