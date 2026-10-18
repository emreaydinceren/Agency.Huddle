namespace Agency.Huddle.App.Themes.Skins;

/// <summary>How cards and panels are surfaced (skin trait <c>surface</c>).</summary>
internal enum SurfaceStyle
{
    /// <summary>Today's Huddle: flat surfaces.</summary>
    Flat,

    /// <summary>Raised paper on a ground: hairline cards, flat buttons, paper dialogs and a shown room-kind chip.</summary>
    Paper,

    /// <summary>Flat resting cards on a region line, no shadow, a plain radius step up, and floating layers lifted by a ring shadow instead of a border.</summary>
    Hairline,
}
