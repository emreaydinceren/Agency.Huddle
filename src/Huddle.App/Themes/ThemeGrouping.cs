namespace Agency.Huddle.App.Themes;

/// <summary>
/// One group in the Appearance tab's theme picker, and the themes listed in it. Built once by
/// <see cref="ThemeCatalog.Grouped"/>, so the picker does no grouping of its own and, more to the
/// point, never reorders <see cref="ThemeCatalog.BuiltIn"/> — whose first entry three call sites
/// read as "the default theme".
/// </summary>
/// <param name="Group">The group these themes belong to.</param>
/// <param name="Themes">The group's themes, in <see cref="ThemeCatalog.BuiltIn"/> order. Never empty.</param>
internal sealed record ThemeGrouping(ThemeGroup Group, IReadOnlyList<ThemeDescriptor> Themes);
