using Agency.Huddle.App.Themes.VsCode;

namespace Agency.Huddle.App.Themes;

/// <summary>
/// The fixed catalog of every theme this application ships with. This is the single source for the
/// Appearance tab's dropdown. Each entry is a <see cref="ThemeDescriptor"/> built in its own file:
/// <see cref="HuddleTheme"/> for this application's own theme, and one file per imported theme under
/// <c>Themes/VsCode/</c>. MudBlazor's <see cref="MudBlazor.MudTheme"/>
/// is the single source of theming — there is no companion stylesheet, no override file, and no
/// per-token fallback layer; every colour and font this application uses comes from the one
/// <see cref="MudBlazor.MudTheme"/> a descriptor here carries.
/// </summary>
internal static class ThemeCatalog
{
    /// <summary>
    /// Every theme built into this application, in the order the dropdown should offer them.
    /// <see cref="HuddleTheme"/> is deliberately first: three call sites read <c>BuiltIn[0]</c> to
    /// mean "the default theme" — <c>MainLayout</c>'s field initialiser, its <c>FindDescriptor</c>
    /// fallback for an id that names nothing, and the Appearance tab's <c>SelectedThemeId</c> — so
    /// an insertion must never go before it. The rest are the colour themes bundled with Visual
    /// Studio Code, ported once at authoring time: the <c>theme-defaults</c> set first, then the
    /// nine that began life as TextMate or Atom themes.
    /// </summary>
    /// <remarks>
    /// VS Code's "Dark (Visual Studio)" and "Light (Visual Studio)" are deliberately absent. Their
    /// palettes resolve byte-identically to <c>Dark+</c> and <c>Light+</c> — upstream the pairs
    /// differ only in <c>tokenColors</c>, the TextMate syntax-highlighting array, and this
    /// application renders no syntax highlighting. Shipping all four would offer a distinction the
    /// picker cannot show.
    /// </remarks>
    public static IReadOnlyList<ThemeDescriptor> BuiltIn { get; } =
    [
        HuddleTheme.Descriptor,
        Dark2026Theme.Descriptor,
        Light2026Theme.Descriptor,
        DarkModernTheme.Descriptor,
        LightModernTheme.Descriptor,
        DarkPlusTheme.Descriptor,
        LightPlusTheme.Descriptor,
        DarkHighContrastTheme.Descriptor,
        LightHighContrastTheme.Descriptor,
        AbyssTheme.Descriptor,
        KimbieDarkTheme.Descriptor,
        MonokaiTheme.Descriptor,
        MonokaiDimmedTheme.Descriptor,
        QuietLightTheme.Descriptor,
        RedTheme.Descriptor,
        SolarizedDarkTheme.Descriptor,
        SolarizedLightTheme.Descriptor,
        TomorrowNightBlueTheme.Descriptor,
    ];
}
