namespace Agency.Huddle.App.Themes;

/// <summary>
/// One theme the Appearance tab can offer: the stylesheet id it loads, the label shown to the
/// Human, and whether it is a dark theme.
/// </summary>
/// <param name="Id">
/// The theme's id. This is what the stylesheet file is called (<c>wwwroot/themes/{Id}.css</c> for a
/// built-in, or <c>{DataDir}/themes/{Id}.css</c> for an imported one) and what <c>appearance.json</c>
/// stores — never <see cref="Label"/>, per <c>rules.md</c>'s "Store a Model's id, never its display
/// name."
/// </param>
/// <param name="Label">The display text the Appearance tab shows for this theme. Never persisted.</param>
/// <param name="IsDark">Whether this theme sets <c>color-scheme: dark</c> rather than <c>light</c>.</param>
internal sealed record ThemeDescriptor(string Id, string Label, bool IsDark);

/// <summary>
/// The fixed catalog of every theme this application ships with. This is the single source for the
/// Appearance tab's dropdown; roadmap item 7 extends it by enumerating <c>{DataDir}/themes/*.css</c>
/// and appending descriptors for whatever it finds there — adding a directory to read, never a second
/// list to maintain.
/// </summary>
internal static class ThemeCatalog
{
    /// <summary>Every theme built into this application, in the order the dropdown should offer them.</summary>
    public static IReadOnlyList<ThemeDescriptor> BuiltIn { get; } =
    [
        new ThemeDescriptor(Id: "huddle-light", Label: "Light", IsDark: false),
        new ThemeDescriptor(Id: "huddle-dark", Label: "Dark", IsDark: true),
    ];
}
