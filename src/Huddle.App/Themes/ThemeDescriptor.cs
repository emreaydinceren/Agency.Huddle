using MudBlazor;

namespace Agency.Huddle.App.Themes;

/// <summary>
/// One theme the Appearance tab can offer: the id <c>appearance.json</c> stores, the label shown to
/// the Human, the <see cref="MudTheme"/> itself, which of the theme's two palette slots that theme
/// actually fills, and the picker list it appears in.
/// </summary>
/// <param name="Id">
/// The theme's id, and what <c>appearance.json</c> stores — never <see cref="Label"/>, per
/// <c>rules.md</c>'s "Store a Model's id, never its display name."
/// </param>
/// <param name="Label">The display text the Appearance tab shows for this theme. Never persisted.</param>
/// <param name="Theme">
/// The MudBlazor theme this descriptor resolves to. <see cref="MudTheme"/> has a slot for a light
/// and a dark palette, but a theme here fills only the one <see cref="Mode"/> names; the other slot
/// keeps MudBlazor's own defaults and is never rendered, because
/// <see cref="Components.Layout.MainLayout"/> sets <c>MudThemeProvider.IsDarkMode</c> from
/// <see cref="Mode"/> rather than from a separate preference. Build one with
/// <see cref="ThemeDefaults.CreateLight(PaletteLight)"/> or
/// <see cref="ThemeDefaults.CreateDark(PaletteDark)"/>, which is what makes the unfilled slot
/// unreachable by construction.
/// </param>
/// <param name="Mode">
/// Which of <see cref="Theme"/>'s two palette slots this theme fills, and so whether selecting it
/// puts the application in dark mode.
/// </param>
/// <param name="Group">
/// The picker list this theme appears in. Today it always matches <see cref="Mode"/>; see
/// <see cref="ThemeGroup"/> for why the two are separate fields.
/// </param>
/// <param name="Pack">
/// The style pack this theme carries, or <see langword="null"/> for none: the name of one stylesheet under
/// <c>wwwroot/packs/</c> whose every rule is scoped to <c>:root:has([data-theme-pack="name"])</c>.
/// <see cref="Components.Layout.MainLayout"/> renders the name on a hidden marker, so a pack applies only while
/// its theme is selected. See <c>docs/adr/0057-a-theme-may-carry-a-style-pack.md</c>.
/// </param>
internal sealed record ThemeDescriptor(
    string Id, string Label, MudTheme Theme, ThemeMode Mode, ThemeGroup Group, string? Pack = null);
