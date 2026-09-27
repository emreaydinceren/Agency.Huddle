namespace Agency.Huddle.App.Appearance;

/// <summary>
/// The resolved appearance state <see cref="AppearanceStore"/> publishes on every render: which
/// <see cref="Themes.ThemeCatalog"/> entry is selected, and the Human's own accent colour choice for
/// <see cref="Themes.AccentTheme"/>. A page render reads this record's values and nothing else — every
/// validation decision that could reject a theme id or a malformed colour has already run inside the
/// store, not here.
/// </summary>
/// <remarks>
/// This record carried a second value, a light/dark preference, until 2026-09-21, removed with the
/// second control that set it: a theme now carries exactly one palette and its own
/// <see cref="Themes.ThemeMode"/>, so the selected theme decides light or dark and there was no longer
/// a second value to disagree with it (<c>docs/adr/0017-a-theme-is-a-palette-not-a-pair.md</c>).
/// <see cref="AccentColorHex"/> is not that same hazard reappearing: it makes no claim about light or
/// dark, or about any theme other than the two <see cref="Themes.AccentTheme"/> entries that read it -
/// every other theme ignores it entirely, the same way <c>avatars.json</c>'s per-Human customisation
/// coexists with the selected theme without contradicting it.
/// </remarks>
/// <param name="ThemeId">
/// The selected theme's id, or <see langword="null"/> when no theme is selected in
/// <c>appearance.json</c>, or when the stored id names no <see cref="Themes.ThemeCatalog"/> entry
/// (see <see cref="AppearanceStore"/>'s remarks — that case is a warning, never a failure). Either
/// way the catalog's default theme applies.
/// </param>
/// <param name="AccentColorHex">
/// The Human's own accent colour, as <c>#rrggbb</c>, or <see langword="null"/> when none is chosen
/// yet or the stored value is malformed (a warning, never a failure - see <see cref="AppearanceStore"/>'s
/// remarks). Only <see cref="Themes.AccentTheme.LightDescriptor"/> and
/// <see cref="Themes.AccentTheme.DarkDescriptor"/> ever read it; every other <see cref="ThemeId"/>
/// ignores it.
/// </param>
internal sealed record AppearanceSettings(string? ThemeId, string? AccentColorHex)
{
    /// <summary>The state when no <c>appearance.json</c> exists, or nothing in it is valid: no theme selected and no accent colour chosen, so the catalog's default applies.</summary>
    public static AppearanceSettings Empty { get; } = new((string?)null, (string?)null);
}
