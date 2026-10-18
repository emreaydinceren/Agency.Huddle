using Agency.Huddle.App.Themes.Skins;

namespace Agency.Huddle.App.Themes;

/// <summary>
/// The fixed catalog of every theme this application ships with: twelve, six light and dark pairs
/// (Huddle, Custom Accent, Lumen, Fluent, Nordic and Cobalt Soft). This is the single source for the
/// Appearance tab's picker. Each entry is a <see cref="ThemeDescriptor"/> built in its own file. The
/// seventeen Visual Studio Code ports that once followed them were removed on 2026-10-17 because a
/// palette alone cannot carry a skin (<c>docs/adr/0016-vs-codes-bundled-themes-are-converted-once-not-imported.md</c>).
/// MudBlazor's <see cref="MudBlazor.MudTheme"/> carries each theme's palette, type scale and shape; a
/// descriptor's optional style pack (see <see cref="ThemeDescriptor.Pack"/>) layers a stylesheet on top
/// of it for the one theme that needs more than a palette.
/// </summary>
internal static class ThemeCatalog
{
    /// <summary>
    /// Every theme built into this application. Each carries exactly one palette and the
    /// <see cref="ThemeMode"/> naming it, so selecting a theme selects light or dark with it
    /// (<c>docs/adr/0017-a-theme-is-a-palette-not-a-pair.md</c>).
    /// Huddle Light (<see cref="HuddleTheme.LightId"/>) is deliberately first: three call sites read
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
    /// hand-tuned, but otherwise Huddle's own neutrals - then the four authored pairs mapped from the
    /// approved mockup directions (Lumen, Fluent, Nordic and Cobalt Soft). Every entry but the Custom Accent pair is
    /// built from a skin file under <c>Themes/Skins/</c>, in that file's <c>order</c>.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<ThemeDescriptor> BuiltIn { get; } = BuildBuiltIn();

    /// <summary>
    /// Builds <see cref="BuiltIn"/>: one descriptor per skin file in the skins' own <c>order</c>, with the two Custom
    /// Accent descriptors - which have no file of their own - slotted in at orders 30 and 40, between Huddle (10, 20) and
    /// Lumen (50, 60).
    /// </summary>
    private static List<ThemeDescriptor> BuildBuiltIn()
    {
        List<(int Order, ThemeDescriptor Descriptor)> entries =
        [
            .. SkinLoader.LoadAll().Select(static skin => (skin.Order, ThemeDescriptor.From(skin))),
            (AccentLightOrder, AccentTheme.LightDescriptor),
            (AccentDarkOrder, AccentTheme.DarkDescriptor),
        ];
        return [.. entries.OrderBy(static entry => entry.Order).Select(static entry => entry.Descriptor)];
    }

    /// <summary>Where Custom Accent Light sorts among the skins' <c>order</c> values.</summary>
    private const int AccentLightOrder = 30;

    /// <summary>Where Custom Accent Dark sorts among the skins' <c>order</c> values.</summary>
    private const int AccentDarkOrder = 40;

    /// <summary>
    /// <see cref="BuiltIn"/> arranged for the picker: one entry per <see cref="ThemeGroup"/>, in
    /// <see cref="ThemeGroup"/>'s own declaration order, each holding that group's themes in
    /// <see cref="BuiltIn"/> order. A group with no themes is omitted rather than listed empty.
    /// </summary>
    public static IReadOnlyList<ThemeGrouping> Grouped { get; } =
    [
        .. Enum.GetValues<ThemeGroup>()
            .Select(group => new ThemeGrouping(group, [.. BuiltIn.Where(descriptor => descriptor.Group == group)]))
            .Where(grouping => grouping.Themes.Count > 0),
    ];
}
