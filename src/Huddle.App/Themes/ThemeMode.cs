namespace Agency.Huddle.App.Themes;

/// <summary>
/// The one palette a theme carries. A theme in this catalog is a single palette, not a light/dark
/// pair: the half named here is the half that was authored, and it is the only half
/// <see cref="Components.Layout.MainLayout"/> ever renders — it derives
/// <c>MudThemeProvider.IsDarkMode</c> from this value, so picking a theme picks the mode with it and
/// the two can no longer disagree.
/// </summary>
/// <remarks>
/// Until 2026-09-21 every theme carried both halves, the un-authored one borrowed wholesale from
/// <see cref="HuddleTheme"/>, and a separate light/dark control could select it — so choosing
/// "Solarized Dark" and then "Light" rendered Huddle's light palette under Solarized Dark's name.
/// The borrowed halves and that second control are both gone; see
/// <c>docs/adr/0017-a-theme-is-a-palette-not-a-pair.md</c>.
/// </remarks>
internal enum ThemeMode
{
    /// <summary>The theme is a light palette, carried in <see cref="MudBlazor.MudTheme.PaletteLight"/>.</summary>
    Light,

    /// <summary>The theme is a dark palette, carried in <see cref="MudBlazor.MudTheme.PaletteDark"/>.</summary>
    Dark,
}
