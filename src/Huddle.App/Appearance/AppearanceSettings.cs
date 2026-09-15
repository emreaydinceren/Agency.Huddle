namespace Agency.Huddle.App.Appearance;

/// <summary>
/// The resolved appearance state <see cref="AppearanceStore"/> publishes on every render: which
/// <see cref="Themes.ThemeCatalog"/> entry is selected, and the Human's light/dark preference. A
/// page render reads this record's two values and nothing else — every validation decision that
/// could reject a theme id has already run inside the store, not here.
/// </summary>
/// <param name="ThemeId">
/// The selected theme's id, or <see langword="null"/> when no theme is selected in
/// <c>appearance.json</c>, or when the stored id names no <see cref="Themes.ThemeCatalog"/> entry
/// (see <see cref="AppearanceStore"/>'s remarks — that case is a warning, never a failure). Either
/// way the catalog's single built-in theme applies.
/// </param>
/// <param name="Dark">The Human's chosen light/dark mode. Defaults to <see cref="DarkModePreference.System"/>.</param>
internal sealed record AppearanceSettings(string? ThemeId, DarkModePreference Dark)
{
    /// <summary>The state when no <c>appearance.json</c> exists, or nothing in it is valid: no theme selected, System dark mode.</summary>
    public static AppearanceSettings Empty { get; } = new(null, DarkModePreference.System);
}
