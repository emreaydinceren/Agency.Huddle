namespace Agency.Huddle.App.Themes.Skins;

/// <summary>How status and priority marks are drawn (skin trait <c>status</c>).</summary>
internal enum StatusStyle
{
    /// <summary>Today's Huddle: no chip, a leading ring glyph whose fill says the Task's state, then a plain label.</summary>
    Glyph,

    /// <summary>A full pill with a shape-coded dot for a Task's state and a tone for a priority.</summary>
    Pill,
}
