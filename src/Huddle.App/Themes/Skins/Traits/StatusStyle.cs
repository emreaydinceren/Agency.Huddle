namespace Agency.Huddle.App.Themes.Skins;

/// <summary>How status and priority marks are drawn (skin trait <c>status</c>).</summary>
internal enum StatusStyle
{
    /// <summary>Today's Huddle: a rounded chip.</summary>
    Chip,

    /// <summary>A full pill with a shape-coded dot for a Task's state and a clay priority.</summary>
    Pill,

    /// <summary>No chip: a leading ring glyph whose fill says the Task's state, then a plain label; priority is three rising bars.</summary>
    Glyph,
}
