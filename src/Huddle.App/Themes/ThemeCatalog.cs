using MudBlazor;

namespace Agency.Huddle.App.Themes;

/// <summary>
/// One theme the Appearance tab can offer: the id <c>appearance.json</c> stores, the label shown to
/// the Human, and the <see cref="MudTheme"/> itself.
/// </summary>
/// <param name="Id">
/// The theme's id, and what <c>appearance.json</c> stores — never <see cref="Label"/>, per
/// <c>rules.md</c>'s "Store a Model's id, never its display name."
/// </param>
/// <param name="Label">The display text the Appearance tab shows for this theme. Never persisted.</param>
/// <param name="Theme">
/// The MudBlazor theme this descriptor resolves to. Carries both <see cref="MudTheme.PaletteLight"/>
/// and <see cref="MudTheme.PaletteDark"/>; <see cref="Components.Layout.MainLayout"/> picks between
/// them through <c>MudThemeProvider.IsDarkMode</c>, not through a second stylesheet.
/// </param>
internal sealed record ThemeDescriptor(string Id, string Label, MudTheme Theme);

/// <summary>
/// The fixed catalog of every theme this application ships with. This is the single source for the
/// Appearance tab's dropdown. MudBlazor's <see cref="MudTheme"/> is now the single source of
/// theming — there is no companion stylesheet, no override file, and no per-token fallback layer;
/// every colour and font this application uses comes from the one <see cref="MudTheme"/> a
/// descriptor here carries.
/// </summary>
internal static class ThemeCatalog
{
    // The application's UI font stack, unchanged from the retired wwwroot/theme.css's --font-ui
    // token, so the base font does not change under this migration.
    private static readonly string[] UiFontFamily =
    [
        "-apple-system", "Segoe UI", "Roboto", "Helvetica", "Arial", "sans-serif",
    ];

    /// <summary>Every theme built into this application, in the order the dropdown should offer them.</summary>
    public static IReadOnlyList<ThemeDescriptor> BuiltIn { get; } =
    [
        new ThemeDescriptor(Id: "huddle", Label: "Huddle", Theme: BuildHuddleTheme()),
    ];

    /// <summary>
    /// Builds the single "Huddle" <see cref="MudTheme"/>, carrying the palette this application
    /// shipped in <c>wwwroot/theme.css</c> before this migration, resolved into MudBlazor's light
    /// and dark <see cref="Palette"/> pair rather than CSS <c>light-dark()</c> pairs.
    /// </summary>
    private static MudTheme BuildHuddleTheme()
    {
        MudTheme theme = new()
        {
            PaletteLight = new PaletteLight
            {
                Background = "#ffffff",
                Surface = "#ffffff",
                DrawerBackground = "#f5f5f5",
                BackgroundGray = "#f7f7f7",
                TableHover = "#eef3ff",
                Divider = "#d0d0d0",
                DividerLight = "#e3e3e3",
                LinesInputs = "#bbbbbb",
                LinesDefault = "#9fb8e8",
                TextPrimary = "#222222",
                TextSecondary = "#444444",
                TextDisabled = "#999999",
                Primary = "#4a154b",
                PrimaryDarken = "#611f69",
                PrimaryContrastText = "#ffffff",
                Secondary = "#3a6fd8",
                Success = "#2e9e44",
                Error = "#b32121",
                Warning = "#c77d00",
                WarningLighten = "#e0a83e",
                ErrorLighten = "#e3b6b6",
                Info = "#1d4f8f",
                OverlayDark = "rgba(0,0,0,0.4)",
            },
            PaletteDark = new PaletteDark
            {
                Background = "#1b1b1f",
                Surface = "#2a2a31",
                DrawerBackground = "#232328",
                BackgroundGray = "#17171b",
                TableHover = "#32323b",
                Divider = "#3a3a44",
                DividerLight = "#30303a",
                LinesInputs = "#4a4a56",
                LinesDefault = "#5a6f99",
                TextPrimary = "#e6e6ea",
                TextSecondary = "#c3c3cc",
                TextDisabled = "#6a6a74",
                // Lightened from the original #5e2b60, which measured only 1.63:1 against this
                // palette's Background (#1b1b1f) - see app.css's focus-ring comment for that
                // measurement - and 1.35:1 against Surface (#2a2a31), both well under the 4.5:1
                // MudBlazor needs for --mud-palette-primary to read on the active MudNavLink.
                // #c07bc3 keeps the same brand hue (~297.5 degrees, against the light palette's
                // ~298.9 and this palette's own prior ~297.7) and a close saturation, only lighter,
                // and was verified with the WCAG 2.1 relative-luminance formula:
                //   - vs Surface (#2a2a31):    4.656:1
                //   - vs Background (#1b1b1f): 5.610:1
                // Both clear the 4.5:1 text minimum.
                Primary = "#c07bc3",
                PrimaryDarken = "#7a3a7e",
                // White PrimaryContrastText only reaches 3.06:1 against the new, lighter Primary -
                // enough for large text but short of the 4.5:1 minimum the avatar monogram needs.
                // Black reaches 6.86:1, comfortably clearing it.
                PrimaryContrastText = "#000000",
                Secondary = "#7aa2f7",
                Success = "#3fbf5a",
                Error = "#e05a5a",
                Warning = "#e0a33e",
                WarningLighten = "#e8c46a",
                ErrorLighten = "#6b3535",
                Info = "#8ab4f8",
                OverlayDark = "rgba(0,0,0,0.6)",
            },
        };

        theme.Typography.Default.FontFamily = UiFontFamily;

        return theme;
    }
}
