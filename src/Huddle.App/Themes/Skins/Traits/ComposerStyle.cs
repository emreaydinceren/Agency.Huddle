namespace Agency.Huddle.App.Themes.Skins;

/// <summary>How the composer is framed (skin trait <c>composer</c>).</summary>
internal enum ComposerStyle
{
    /// <summary>Today's Huddle: a boxed input.</summary>
    Box,

    /// <summary>A paper card: the box is the surface, the input is borderless and a tool row sits along the bottom.</summary>
    Card,

    /// <summary>The box with a permanent accent edge along its bottom and a tool row below the field behind a hairline.</summary>
    Underlined,
}
