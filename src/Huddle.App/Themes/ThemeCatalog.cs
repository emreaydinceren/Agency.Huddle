using Agency.Huddle.App.Themes.VsCode;

namespace Agency.Huddle.App.Themes;

/// <summary>
/// The fixed catalog of every theme this application ships with. This is the single source for the
/// Appearance tab's picker. Each entry is a <see cref="ThemeDescriptor"/> built in its own file:
/// <see cref="HuddleTheme"/> for this application's own colours, and one file per imported theme
/// under <c>Themes/VsCode/</c>. MudBlazor's <see cref="MudBlazor.MudTheme"/> is the single source of
/// theming — there is no companion stylesheet, no override file, and no per-token fallback layer;
/// every colour and font this application uses comes from the one <see cref="MudBlazor.MudTheme"/> a
/// descriptor here carries.
/// </summary>
internal static class ThemeCatalog
{
    /// <summary>
    /// Every theme built into this application. Each carries exactly one palette and the
    /// <see cref="ThemeMode"/> naming it, so selecting a theme selects light or dark with it
    /// (<c>docs/adr/0017-a-theme-is-a-palette-not-a-pair.md</c>).
    /// <see cref="HuddleTheme.LightDescriptor"/> is deliberately first: three call sites read
    /// <c>BuiltIn[0]</c> to mean "the default theme" — <c>MainLayout</c>'s field initialiser, its
    /// <c>FindDescriptor</c> fallback for an id that names nothing, and the Appearance tab's
    /// <c>SelectedThemeId</c> — so an insertion must never go before it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This order is not the picker's order. The picker groups by <see cref="ThemeDescriptor.Group"/>
    /// and only orders within a group, precisely so that grouping can be changed without moving
    /// <c>BuiltIn[0]</c>. The order here is the authoring order: this application's own two themes,
    /// then <see cref="AccentTheme"/>'s two - Primary is a Human-picked colour rather than
    /// hand-tuned, but otherwise Huddle's own neutrals - then the <c>theme-defaults</c> set bundled
    /// with Visual Studio Code, then the nine that began life as TextMate or Atom themes.
    /// </para>
    /// <para>
    /// VS Code's "Dark (Visual Studio)" and "Light (Visual Studio)" are deliberately absent. Their
    /// palettes resolve byte-identically to <c>Dark+</c> and <c>Light+</c> — upstream the pairs
    /// differ only in <c>tokenColors</c>, the TextMate syntax-highlighting array, and this
    /// application renders no syntax highlighting. Shipping all four would offer a distinction the
    /// picker cannot show.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<ThemeDescriptor> BuiltIn { get; } =
    [
        HuddleTheme.LightDescriptor,
        HuddleTheme.DarkDescriptor,
        AccentTheme.LightDescriptor,
        AccentTheme.DarkDescriptor,
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

    /// <summary>
    /// <see cref="BuiltIn"/> arranged for the picker: one entry per <see cref="ThemeGroup"/>, in
    /// <see cref="ThemeGroup"/>'s own declaration order, each holding that group's themes in
    /// <see cref="BuiltIn"/> order. A group with no themes is omitted rather than rendered empty.
    /// </summary>
    public static IReadOnlyList<ThemeGrouping> Grouped { get; } =
    [
        .. Enum.GetValues<ThemeGroup>()
            .Select(group => new ThemeGrouping(group, [.. BuiltIn.Where(descriptor => descriptor.Group == group)]))
            .Where(grouping => grouping.Themes.Count > 0),
    ];
}
