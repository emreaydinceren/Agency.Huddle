using MudBlazor;

namespace Agency.Huddle.App.Themes;

/// <summary>
/// Builds the pieces every theme in this application shares. A VS Code theme — the source most of
/// the twenty themes in this catalog are ported from — carries colours but no font information, so
/// the UI font stack is written once here instead of once per theme file.
/// </summary>
internal static class ThemeDefaults
{
    // The application's UI font stack, unchanged from the retired wwwroot/theme.css's --font-ui
    // token, so the base font does not change under this migration.
    private static readonly string[] UiFontFamily =
    [
        "-apple-system", "Segoe UI", "Roboto", "Helvetica", "Arial", "sans-serif",
    ];

    /// <summary>
    /// Builds a <see cref="MudTheme"/> from a theme's light and dark palettes, applying the shared
    /// <see cref="UiFontFamily"/> that every theme in this application uses, since a theme file
    /// carries only colour.
    /// </summary>
    /// <param name="light">The theme's light palette.</param>
    /// <param name="dark">The theme's dark palette.</param>
    /// <returns>The assembled <see cref="MudTheme"/>, ready for a <see cref="ThemeDescriptor"/>.</returns>
    public static MudTheme Create(PaletteLight light, PaletteDark dark)
    {
        MudTheme theme = new()
        {
            PaletteLight = light,
            PaletteDark = dark,
        };

        theme.Typography.Default.FontFamily = UiFontFamily;

        return theme;
    }
}
