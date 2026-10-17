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
    /// hand-tuned, but otherwise Huddle's own neutrals - then the four authored pairs mapped from the
    /// approved mockup directions (<see cref="LumenTheme"/>, <see cref="FluentTheme"/>,
    /// <see cref="NordicTheme"/> and <see cref="CobaltSoftTheme"/>).
    /// </para>
    /// </remarks>
    public static IReadOnlyList<ThemeDescriptor> BuiltIn { get; } =
    [
        HuddleTheme.LightDescriptor,
        HuddleTheme.DarkDescriptor,
        AccentTheme.LightDescriptor,
        AccentTheme.DarkDescriptor,
        LumenTheme.LightDescriptor,
        LumenTheme.DarkDescriptor,
        FluentTheme.LightDescriptor,
        FluentTheme.DarkDescriptor,
        NordicTheme.LightDescriptor,
        NordicTheme.DarkDescriptor,
        CobaltSoftTheme.LightDescriptor,
        CobaltSoftTheme.DarkDescriptor,
    ];

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
