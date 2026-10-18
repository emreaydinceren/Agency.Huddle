namespace Agency.Huddle.App.Themes.Skins;

/// <summary>How the current navigation item is marked (skin trait <c>nav</c>).</summary>
internal enum NavStyle
{
    /// <summary>Today's Huddle: a tinted row.</summary>
    Tint,

    /// <summary>A lifted tile, with a bar on the activity bar's current item.</summary>
    Tile,

    /// <summary>The current item is marked by a 3px accent bar, on the activity bar and on sidebar rows, instead of a tint fill.</summary>
    Bar,
}
