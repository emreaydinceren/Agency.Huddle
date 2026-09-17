using MudBlazor;

namespace Agency.Huddle.App.Themes;

/// <summary>
/// One theme the Appearance tab can offer: the id <c>appearance.json</c> stores, the label shown to
/// the Human, and the <see cref="MudTheme"/> itself.
/// </summary>
/// <param name="Id">
/// The theme's id, and what <c>appearance.json</c> stores — never <see cref="Label"/>, per
/// <c>rules.md</c>'s "Store a Model's id, never its display name."
/// </param>
/// <param name="Label">The display text the Appearance tab shows for this theme. Never persisted.</param>
/// <param name="Theme">
/// The MudBlazor theme this descriptor resolves to. Carries both <see cref="MudTheme.PaletteLight"/>
/// and <see cref="MudTheme.PaletteDark"/>; <see cref="Components.Layout.MainLayout"/> picks between
/// them through <c>MudThemeProvider.IsDarkMode</c>, not through a second stylesheet.
/// </param>
/// <param name="Mode">
/// The palette this theme was designed for. The other palette is borrowed rather than authored,
/// and a future test uses this to know which half of the theme to hold to a contrast floor.
/// </param>
internal sealed record ThemeDescriptor(string Id, string Label, MudTheme Theme, ThemeMode Mode);
