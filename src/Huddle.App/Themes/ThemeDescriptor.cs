using Agency.Huddle.App.Themes.Skins;
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
/// <param name="Skin">
/// The skin whose tokens, faces and traits the layout renders for this theme. The Custom Accent descriptors
/// carry Huddle's own skin.
/// </param>
internal sealed record ThemeDescriptor(
    string Id, string Label, MudTheme Theme, ThemeMode Mode, ThemeGroup Group, Skin Skin)
{
    /// <summary>The id of the element <see cref="StyleBlock"/> renders, so a test or a probe can find it.</summary>
    public const string StyleElementId = "hu-skin";

    /// <summary>
    /// The skin's <c>&lt;style id="hu-skin"&gt;</c> element as markup, rendered once when the descriptor is built.
    /// The layout writes it as a <see cref="Microsoft.AspNetCore.Components.MarkupString"/> so the quotes in a font stack
    /// are not HTML-encoded on prerender.
    /// </summary>
    public string StyleBlock { get; } = RenderStyleBlock(Skin);

    /// <summary>The skin's nine traits as the <c>data-hu-*</c> attributes of the layout's Theme marker, built once.</summary>
    public IReadOnlyDictionary<string, object> TraitAttributes { get; } = Skin.Traits.ToMarkerAttributes();

    /// <summary>Builds the descriptor a skin implies: its theme, mode, and the picker group that mode selects.</summary>
    /// <param name="skin">A loaded skin.</param>
    /// <returns>The descriptor for <paramref name="skin"/>.</returns>
    public static ThemeDescriptor From(Skin skin)
    {
        ArgumentNullException.ThrowIfNull(skin);
        return new ThemeDescriptor(
            skin.Id,
            skin.Label,
            skin.Theme,
            skin.Mode,
            skin.Mode == ThemeMode.Dark ? ThemeGroup.Dark : ThemeGroup.Light,
            skin);
    }

    private static string RenderStyleBlock(Skin skin)
    {
        SkinFacesCss faces = new(FontStack(skin.Faces.Read), FontStack(skin.Faces.Mono));
        string css = SkinCss.Render(skin.Tokens, skin.Mode, skin.Id, faces);
        return $"<style id=\"{StyleElementId}\">{css}</style>";
    }

    /// <summary>A CSS font stack for the families, quoting a name that has a space, or <see langword="null"/> for none.</summary>
    private static string? FontStack(IReadOnlyList<string> families) =>
        families.Count == 0
            ? null
            : string.Join(", ", families.Select(static family => family.Contains(' ', StringComparison.Ordinal) ? $"\"{family}\"" : family));
}
