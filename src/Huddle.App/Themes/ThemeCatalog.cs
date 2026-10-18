using System.Collections.Concurrent;
using Agency.Huddle.App.Themes.Skins;

namespace Agency.Huddle.App.Themes;

/// <summary>
/// The fixed catalog of every theme this application ships with: eight, four light and dark pairs (Lumen, Fluent, Nordic
/// and Cobalt Soft). This is the single source for the Appearance tab's picker. Each entry is a <see cref="ThemeDescriptor"/>
/// built from its skin file under <c>Themes/Skins/</c>. The seventeen Visual Studio Code ports that once followed them were removed
/// on 2026-10-17 because a palette alone cannot carry a skin (<c>docs/adr/0016-vs-codes-bundled-themes-are-converted-once-not-imported.md</c>),
/// and the Huddle and Custom Accent pairs went with the per-Theme customizations that replaced them (a Human's own accent is now
/// <see cref="Resolve"/>'s customization of whichever Theme is shown). MudBlazor's <see cref="MudBlazor.MudTheme"/> carries each
/// theme's palette, type scale and shape; a descriptor's skin tokens and traits (see <see cref="ThemeDescriptor.Skin"/>) carry
/// everything a palette cannot.
/// </summary>
internal static class ThemeCatalog
{
    /// <summary>The Light Theme the mode toggle and the Appearance tab fall back to when the Human has chosen none: Lumen Light, <see cref="BuiltIn"/>'s first entry.</summary>
    public const string DefaultLightId = "lumen-light";

    /// <summary>The Dark Theme the mode toggle and the Appearance tab fall back to when the Human has chosen none: Lumen Dark.</summary>
    public const string DefaultDarkId = "lumen-dark";

    /// <summary>The most customized descriptors kept at once; past this the cache starts over, so editing a colour live cannot grow it without bound.</summary>
    private const int ResolvedLimit = 64;

    /// <summary>The effective descriptor built for each (Theme id, customization), keyed by the id and the customization's canonical text.</summary>
    private static readonly ConcurrentDictionary<string, ThemeDescriptor> Resolved = new(StringComparer.Ordinal);

    /// <summary>
    /// Every theme built into this application. Each carries exactly one palette and the
    /// <see cref="ThemeMode"/> naming it, so selecting a theme selects light or dark with it
    /// (<c>docs/adr/0017-a-theme-is-a-palette-not-a-pair.md</c>).
    /// Lumen Light (<see cref="DefaultLightId"/>) is deliberately first: three call sites read
    /// <c>BuiltIn[0]</c> to mean "the default theme" - <c>MainLayout</c>'s field initialiser, its
    /// <c>FindDescriptor</c> fallback for an id that names nothing, and the Appearance tab's
    /// <c>SelectedThemeId</c> - so an insertion must never go before it.
    /// </summary>
    /// <remarks>
    /// This order is not the picker's order. The picker groups by <see cref="ThemeDescriptor.Group"/>
    /// and only orders within a group, precisely so that grouping can be changed without moving
    /// <c>BuiltIn[0]</c>. The order here is each skin file's <c>order</c>.
    /// </remarks>
    public static IReadOnlyList<ThemeDescriptor> BuiltIn { get; } = [.. SkinLoader.LoadAll().Select(static skin => ThemeDescriptor.From(skin))];

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

    /// <summary>The catalog entry named <paramref name="themeId"/>, or <see langword="null"/> when it is <see langword="null"/> or names none.</summary>
    /// <param name="themeId">The theme id to look up.</param>
    /// <returns>The entry, or <see langword="null"/>.</returns>
    public static ThemeDescriptor? Find(string? themeId)
    {
        foreach (ThemeDescriptor theme in BuiltIn)
        {
            if (string.Equals(theme.Id, themeId, StringComparison.Ordinal))
            {
                return theme;
            }
        }

        return null;
    }

    /// <summary>
    /// The descriptor to render for <paramref name="descriptor"/> once a Human's <paramref name="customization"/> is merged over
    /// its skin. Built once per (Theme id, customization) and cached, never per render: an unchanged customization returns the
    /// same instance.
    /// </summary>
    /// <param name="descriptor">A catalogue descriptor.</param>
    /// <param name="customization">The Human's overrides for that Theme, or <see langword="null"/> for none.</param>
    /// <returns><paramref name="descriptor"/> itself when nothing valid is customized; otherwise the customized descriptor.</returns>
    public static ThemeDescriptor Resolve(ThemeDescriptor descriptor, SkinCustomization? customization)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (customization is null || customization.IsEmpty)
        {
            return descriptor;
        }

        string key = descriptor.Id + "\n" + customization.CacheKey;
        if (Resolved.TryGetValue(key, out ThemeDescriptor? cached))
        {
            return cached;
        }

        Skin customized = SkinLoader.Customize(descriptor.Skin, customization);
        ThemeDescriptor built = ReferenceEquals(customized, descriptor.Skin) ? descriptor : ThemeDescriptor.From(customized);
        if (Resolved.Count >= ResolvedLimit)
        {
            Resolved.Clear();
        }

        return Resolved.GetOrAdd(key, built);
    }
}
