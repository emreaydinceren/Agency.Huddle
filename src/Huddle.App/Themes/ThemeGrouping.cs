namespace Agency.Huddle.App.Themes;

/// <summary>
/// One heading in the Appearance tab's theme picker, and the themes listed beneath it. Built once by
/// <see cref="ThemeCatalog.Grouped"/>, so the picker renders headings without doing its own grouping
/// and, more to the point, without reordering <see cref="ThemeCatalog.BuiltIn"/> — whose first entry
/// three call sites read as "the default theme".
/// </summary>
/// <param name="Group">The group these themes belong to.</param>
/// <param name="Themes">The group's themes, in <see cref="ThemeCatalog.BuiltIn"/> order. Never empty.</param>
internal sealed record ThemeGrouping(ThemeGroup Group, IReadOnlyList<ThemeDescriptor> Themes)
{
    /// <summary>The heading text the picker shows above <see cref="Themes"/>.</summary>
    public string Heading => this.Group switch
    {
        ThemeGroup.Light => "Light",
        ThemeGroup.Dark => "Dark",
        _ => "High contrast",
    };
}
