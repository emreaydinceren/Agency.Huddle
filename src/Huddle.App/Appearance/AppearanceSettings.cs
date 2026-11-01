using Agency.Huddle.App.Themes;
using Agency.Huddle.App.Themes.Skins;

namespace Agency.Huddle.App.Appearance;

/// <summary>
/// The resolved appearance state <see cref="AppearanceStore"/> publishes on every render: which
/// <see cref="ThemeCatalog"/> entry is selected, the Human's light and dark choices, and the Human's per-Theme
/// customizations. A page render reads this record's values and nothing else - every validation decision that could reject
/// a theme id, a colour or a token has already run inside the store, not here.
/// </summary>
/// <remarks>
/// This record carried a second value, a light/dark preference, until 2026-09-21, removed with the
/// second control that set it: a theme now carries exactly one palette and its own
/// <see cref="ThemeMode"/>, so the selected theme decides light or dark and there was no longer
/// a second value to disagree with it (<c>docs/adr/0017-a-theme-is-a-palette-not-a-pair.md</c>). A customization is not
/// that hazard reappearing: it belongs to one Theme id, says nothing about light or dark, and is read only when that
/// Theme is the one shown. It replaced the single accent colour the Custom Accent Themes used to read.
/// </remarks>
/// <param name="ThemeId">
/// The selected theme's id, or <see langword="null"/> when no theme is selected in
/// <c>appearance.json</c>, or when the stored id names no <see cref="ThemeCatalog"/> entry
/// (see <see cref="AppearanceStore"/>'s remarks - that case is a warning, never a failure). Either
/// way the catalog's default theme applies.
/// </param>
/// <param name="LightThemeId">
/// The Human's chosen light theme, which the top bar's mode toggle switches to; <see langword="null"/>
/// when none is chosen or the stored id is unknown or not a light theme (a warning, never a failure).
/// A file that predates the pair but selects a light <see cref="ThemeId"/> resolves that theme here.
/// </param>
/// <param name="DarkThemeId">The dark counterpart of <paramref name="LightThemeId"/>, resolved the same way.</param>
/// <param name="Customizations">
/// The valid customizations by Theme id; an invalid entry has already been dropped with a warning. Empty when the Human
/// customised nothing.
/// </param>
internal sealed record AppearanceSettings(
    string? ThemeId,
    string? LightThemeId,
    string? DarkThemeId,
    IReadOnlyDictionary<string, SkinCustomization> Customizations)
{
    /// <summary>The theme the mode toggle switches to from dark: the stored light choice, or <see cref="ThemeCatalog.DefaultLightId"/> when none is chosen.</summary>
    public string PreferredLightId => this.LightThemeId ?? ThemeCatalog.DefaultLightId;

    /// <summary>The theme the mode toggle switches to from light: the stored dark choice, or <see cref="ThemeCatalog.DefaultDarkId"/> when none is chosen.</summary>
    public string PreferredDarkId => this.DarkThemeId ?? ThemeCatalog.DefaultDarkId;

    /// <summary>The Human's customization of <paramref name="themeId"/>, or <see langword="null"/> when it has none.</summary>
    /// <param name="themeId">A Theme id.</param>
    /// <returns>The customization, or <see langword="null"/>.</returns>
    public SkinCustomization? CustomizationFor(string themeId) =>
        this.Customizations.TryGetValue(themeId, out SkinCustomization? customization) ? customization : null;
}
