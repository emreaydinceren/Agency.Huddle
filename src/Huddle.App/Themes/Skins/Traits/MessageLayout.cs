namespace Agency.Huddle.App.Themes.Skins;

/// <summary>How a message is laid out in the conversation (skin trait <c>message</c>).</summary>
internal enum MessageLayout
{
    /// <summary>Today's Huddle: a bubble per message.</summary>
    Bubble,

    /// <summary>A flat row per message: avatar and name on the left, no bubble, for the Human's messages too.</summary>
    Row,
}
