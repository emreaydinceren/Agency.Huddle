namespace Agency.Huddle.App.Themes;

/// <summary>
/// The group a theme belongs to in the Appearance tab's picker. Deliberately a separate field from
/// <see cref="ThemeMode"/>, which answers a different question: <see cref="ThemeMode"/> names the
/// one palette a theme carries and so decides whether <c>MudThemeProvider.IsDarkMode</c> is set for
/// it, while this decides only which of the picker's two lists shows it. Today the two always agree;
/// they stay two fields so a future group (a high-contrast set, say) can separate them without
/// touching the palette slot a theme fills.
/// </summary>
/// <remarks>
/// A third value, <c>HighContrast</c>, existed while the Visual Studio Code ports shipped and was
/// removed with them on 2026-10-17 (<c>docs/adr/0016-vs-codes-bundled-themes-are-converted-once-not-imported.md</c>).
/// </remarks>
internal enum ThemeGroup
{
    /// <summary>Listed in the picker's light list.</summary>
    Light,

    /// <summary>Listed in the picker's dark list.</summary>
    Dark,
}
