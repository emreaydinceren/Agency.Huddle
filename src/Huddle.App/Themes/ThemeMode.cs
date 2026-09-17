namespace Agency.Huddle.App.Themes;

/// <summary>
/// A theme's native mode: every <see cref="ThemeDescriptor"/> carries both a
/// <c>PaletteLight</c> and a <c>PaletteDark</c>, but was authored for only one of the two —
/// the other is borrowed. A future contrast test uses this to know which half of a theme to
/// hold to a WCAG floor.
/// </summary>
internal enum ThemeMode
{
    /// <summary>The theme was designed for its light palette; its dark palette is borrowed.</summary>
    Light,

    /// <summary>The theme was designed for its dark palette; its light palette is borrowed.</summary>
    Dark,
}
