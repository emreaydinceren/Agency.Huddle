namespace Agency.Huddle.App.Appearance;

/// <summary>
/// The resolved appearance state <see cref="AppearanceStore"/> publishes on every render:
/// which built-in theme stylesheet (if any) <c>App.razor</c> should link, and the
/// already-validated CSS body it should inline afterwards. A page render reads this record's two
/// values and nothing else — every validation decision that could reject a theme id or an
/// override value has already run inside the store, not here.
/// </summary>
/// <param name="ThemeId">
/// The selected theme's id, or <see langword="null"/> when no theme is selected in
/// <c>appearance.json</c>, or when the stored id names no <see cref="Themes.ThemeCatalog"/> entry
/// (see <see cref="AppearanceStore"/>'s remarks — that case is a warning, never a failure). Either
/// way the built-in light/dark pair in <c>theme.css</c> applies via the operating system's
/// preference.
/// </param>
/// <param name="OverrideCss">
/// The finished, already-validated inline <c>:root { ... }</c> CSS body <see cref="ThemeOverrides.Build"/>
/// produced, or <see cref="string.Empty"/> when there is nothing to emit.
/// </param>
/// <param name="Problems">
/// One human-readable line per rejected theme id or override entry, for the Appearance tab (T4.1)
/// to display. Empty when nothing was rejected.
/// </param>
internal sealed record AppearanceSettings(string? ThemeId, string OverrideCss, IReadOnlyList<string> Problems)
{
    /// <summary>The state when no <c>appearance.json</c> exists, or nothing in it is valid: no theme, no overrides, no problems.</summary>
    public static AppearanceSettings Empty { get; } = new(null, string.Empty, []);
}
