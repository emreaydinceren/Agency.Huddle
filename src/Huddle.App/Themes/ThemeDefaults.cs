using MudBlazor;

namespace Agency.Huddle.App.Themes;

/// <summary>
/// Builds the pieces every theme in this application shares. A VS Code theme — the source most of
/// the themes in this catalog are ported from — carries colours but no font information, so the UI
/// font stack is written once here instead of once per theme file.
/// </summary>
/// <remarks>
/// The two builders below each fill exactly one of <see cref="MudTheme"/>'s two palette slots,
/// because a theme in this catalog is one palette and not a pair
/// (<c>docs/adr/0017-a-theme-is-a-palette-not-a-pair.md</c>). The slot left alone keeps MudBlazor's
/// own default palette; nothing renders it, because
/// <see cref="Components.Layout.MainLayout"/> derives <c>MudThemeProvider.IsDarkMode</c> from
/// <see cref="ThemeDescriptor.Mode"/> and never from a separate preference. Having no second
/// parameter to pass is the point: there is no longer anywhere to put a borrowed palette.
/// </remarks>
internal static class ThemeDefaults
{
    // The application's UI font stack. Inter is loaded as a variable-weight web font via the
    // Google Fonts <link> in App.razor's <head>; "Helvetica Neue" and "sans-serif" are the system
    // fallback for the moment before that stylesheet loads (or if it fails to).
    private static readonly string[] UiFontFamily =
    [
        "Inter", "Helvetica Neue", "sans-serif",
    ];

    /// <summary>Builds a <see cref="MudTheme"/> for a light theme, applying the shared <see cref="UiFontFamily"/>.</summary>
    /// <param name="light">The theme's palette. Goes in <see cref="MudTheme.PaletteLight"/>; the dark slot is left at MudBlazor's default and never rendered.</param>
    /// <returns>The assembled <see cref="MudTheme"/>, ready for a <see cref="ThemeDescriptor"/> with <see cref="ThemeMode.Light"/>.</returns>
    public static MudTheme CreateLight(PaletteLight light) => WithSharedTypography(new MudTheme { PaletteLight = light });

    /// <summary>Builds a <see cref="MudTheme"/> for a dark theme, applying the shared <see cref="UiFontFamily"/>.</summary>
    /// <param name="dark">The theme's palette. Goes in <see cref="MudTheme.PaletteDark"/>; the light slot is left at MudBlazor's default and never rendered.</param>
    /// <returns>The assembled <see cref="MudTheme"/>, ready for a <see cref="ThemeDescriptor"/> with <see cref="ThemeMode.Dark"/>.</returns>
    public static MudTheme CreateDark(PaletteDark dark) => WithSharedTypography(new MudTheme { PaletteDark = dark });

    /// <summary>Applies the shared UI font stack to <paramref name="theme"/> and hands it back.</summary>
    /// <param name="theme">The freshly built theme, carrying one palette and nothing else of this application's own.</param>
    private static MudTheme WithSharedTypography(MudTheme theme)
    {
        theme.Typography.Default.FontFamily = UiFontFamily;
        return theme;
    }
}
