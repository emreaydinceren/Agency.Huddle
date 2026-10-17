using MudBlazor;

namespace Agency.Huddle.App.Themes;

/// <summary>
/// Fluent: a Microsoft Teams / Fluent 2 enterprise pair of Themes - neutral greys, brand blue and a purple Review -
/// mapped from the <c>07-enterprise-fluent</c> mockup set's <c>app.css</c>, light (<c>:root</c>) and dark
/// (<c>[data-theme="dark"]</c>).
/// </summary>
/// <remarks>
/// <para>
/// Each value's comment names the mockup token it came from. <b>ADJUSTED</b> marks a value moved off the mockup to
/// clear a Huddle rule. The mockup's card borders lean on a shadow and its chrome lines on three different grounds,
/// so both are too faint on Huddle's one flat surface and step up to the set's own stronger strokes.
/// </para>
/// <para>
/// Three signals move to keep every signal 30 degrees from the others and from the accent (audit A2). The set
/// paints In Progress in brand blue, which would make Working read as the accent, so <c>Info</c> is the set's teal
/// <c>--k-request</c> turned a few degrees toward green. Its orange <c>--attn</c> sits 26 degrees from
/// <c>--danger</c>, so <c>Warning</c> is the set's amber <c>--warn</c> instead. <c>Secondary</c> is a second brand
/// blue, because <c>--review</c>'s purple is <c>Tertiary</c>, the Review signal.
/// </para>
/// </remarks>
internal static class FluentTheme
{
    /// <summary>Builds a fresh instance of Fluent Light's palette on every call, so no <see cref="Palette"/> is shared across Themes.</summary>
    /// <returns>A new <see cref="PaletteLight"/>.</returns>
    public static PaletteLight Light() => new()
    {
        Background = "#f3f4f6", // --bg-canvas
        Surface = "#ffffff", // --surface
        DrawerBackground = "#fafafa", // --bg-pane
        BackgroundGray = "#e6e6e6", // --surface-pressed
        TableHover = "#f0f0f0", // --surface-hover
        Divider = "#d1d1d1", // ADJUSTED from --stroke #e0e0e0 (1.32:1) to --stroke-2, 1.53:1
        DividerLight = "#e0e0e0", // --stroke
        LinesInputs = "#d1d1d1", // --stroke-2
        LinesDefault = "#b3b3b3", // ADJUSTED from --stroke to Fluent grey 70, 2.10:1
        TextPrimary = "#242424", // --fg1
        TextSecondary = "#5c5c5c", // --fg3
        ActionDefault = "#5c5c5c",
        DrawerText = "#242424",
        DrawerIcon = "#5c5c5c",
        TextDisabled = "#8a8a8a", // --stroke-strong
        Primary = "#0f6cbd", // --brand = --brand-text
        PrimaryDarken = "#115ea3", // --brand-hover
        PrimaryContrastText = "#ffffff", // --fg-on-brand
        Secondary = "#115ea3", // --brand-hover
        Tertiary = "#6b47a8", // Review: --review
        Success = "#0e700e", // --ok
        Warning = "#835b00", // ADJUSTED from --attn #b84000 (26 degrees from Error) to --warn
        Error = "#b10e1c", // --danger
        Info = "#037a72", // ADJUSTED from --k-request #03787c (26 degrees from the accent) to hue 176
        OverlayDark = "rgba(0,0,0,0.4)",
    };

    /// <summary>Builds a fresh instance of Fluent Dark's palette on every call, so no <see cref="Palette"/> is shared across Themes.</summary>
    /// <returns>A new <see cref="PaletteDark"/>.</returns>
    /// <remarks>
    /// <c>PrimaryDarken</c> is left for MudBlazor to derive: the set's dark hover goes lighter, to <c>#0f6cbd</c>,
    /// which means nothing once <c>Primary</c> is the light <c>--brand-text</c>.
    /// </remarks>
    public static PaletteDark Dark() => new()
    {
        Background = "#1f1f1f", // --bg-canvas
        Surface = "#292929", // --surface
        DrawerBackground = "#1a1a1a", // --bg-pane
        BackgroundGray = "#424242", // --surface-pressed
        TableHover = "#383838", // --surface-hover
        Divider = "#4a4a4a", // ADJUSTED from --stroke #3d3d3d (1.34:1) to --stroke-2, 1.64:1
        DividerLight = "#3d3d3d", // --stroke
        LinesInputs = "#4a4a4a", // --stroke-2
        LinesDefault = "#666666", // ADJUSTED from --stroke to Fluent grey 40, 2.53:1
        TextPrimary = "#ffffff", // --fg1
        TextSecondary = "#b3b3b3", // --fg3
        ActionDefault = "#b3b3b3",
        DrawerText = "#ffffff",
        DrawerIcon = "#b3b3b3",
        TextDisabled = "#8a8a8a", // --stroke-strong
        Primary = "#62abf5", // ADJUSTED role: --brand-text, since --brand #115ea3 is 2.18:1 on Surface
        PrimaryContrastText = "#000000", // --focus-inner
        Secondary = "#0f6cbd", // the set's dark brand hover
        Tertiary = "#b696e2", // Review: --review
        Success = "#6ccb5f", // --ok
        Warning = "#edae1d", // ADJUSTED from --attn #faa06b (27 degrees from Error) to --warn's hue, darkened so the presence badge's glyph reads 3:1
        Error = "#f1707b", // --danger
        Info = "#58ccc0", // ADJUSTED from --k-request #58c8cc (28 degrees from the accent) to hue 174
        OverlayDark = "rgba(0,0,0,0.6)", // .scrim
    };

    /// <summary>Fluent Light's descriptor.</summary>
    public static ThemeDescriptor LightDescriptor { get; } = new(
        Id: "fluent-light",
        Label: "Fluent Light",
        Theme: WithCorners(ThemeDefaults.CreateLight(Light())),
        Mode: ThemeMode.Light,
        Group: ThemeGroup.Light);

    /// <summary>Fluent Dark's descriptor.</summary>
    public static ThemeDescriptor DarkDescriptor { get; } = new(
        Id: "fluent-dark",
        Label: "Fluent Dark",
        Theme: WithCorners(ThemeDefaults.CreateDark(Dark())),
        Mode: ThemeMode.Dark,
        Group: ThemeGroup.Dark);

    /// <summary>Rounds the corners to Fluent 2's 6px medium radius.</summary>
    /// <param name="theme">A Fluent theme, freshly built.</param>
    /// <returns><paramref name="theme"/>, with its default border radius set.</returns>
    private static MudTheme WithCorners(MudTheme theme)
    {
        theme.LayoutProperties.DefaultBorderRadius = "6px";
        return theme;
    }
}
