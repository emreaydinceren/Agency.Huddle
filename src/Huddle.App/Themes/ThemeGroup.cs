namespace Agency.Huddle.App.Themes;

/// <summary>
/// The heading a theme sits under in the Appearance tab's picker. Deliberately separate from
/// <see cref="ThemeMode"/>, which answers a different question: <see cref="ThemeMode"/> names the
/// one palette a theme carries and so decides whether <c>MudThemeProvider.IsDarkMode</c> is set for
/// it, while this decides only where the picker lists it. The two high-contrast themes are exactly
/// why both exist — "Dark High Contrast" has <see cref="ThemeMode.Dark"/> but belongs under
/// <see cref="HighContrast"/>, not under <see cref="Dark"/>.
/// </summary>
/// <remarks>
/// The three values, and the order of the headings, are Visual Studio Code's own: its theme picker
/// groups the same catalog as "light themes", "dark themes" and "high contrast themes", in that
/// order.
/// </remarks>
internal enum ThemeGroup
{
    /// <summary>Listed under the picker's "Light" heading.</summary>
    Light,

    /// <summary>Listed under the picker's "Dark" heading.</summary>
    Dark,

    /// <summary>Listed under the picker's "High contrast" heading.</summary>
    HighContrast,
}
