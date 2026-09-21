namespace Agency.Huddle.App.Appearance;

/// <summary>
/// The resolved appearance state <see cref="AppearanceStore"/> publishes on every render: which
/// <see cref="Themes.ThemeCatalog"/> entry is selected. A page render reads this record's one value
/// and nothing else — every validation decision that could reject a theme id has already run inside
/// the store, not here.
/// </summary>
/// <remarks>
/// This record carried a second value, a light/dark preference, until 2026-09-21. It was removed
/// with the second control that set it: a theme now carries exactly one palette and its own
/// <see cref="Themes.ThemeMode"/>, so the selected theme decides light or dark and there is no
/// longer a second value to disagree with it
/// (<c>docs/adr/0017-a-theme-is-a-palette-not-a-pair.md</c>).
/// </remarks>
/// <param name="ThemeId">
/// The selected theme's id, or <see langword="null"/> when no theme is selected in
/// <c>appearance.json</c>, or when the stored id names no <see cref="Themes.ThemeCatalog"/> entry
/// (see <see cref="AppearanceStore"/>'s remarks — that case is a warning, never a failure). Either
/// way the catalog's default theme applies.
/// </param>
internal sealed record AppearanceSettings(string? ThemeId)
{
    /// <summary>The state when no <c>appearance.json</c> exists, or nothing in it is valid: no theme selected, so the catalog's default applies.</summary>
    public static AppearanceSettings Empty { get; } = new((string?)null);
}
