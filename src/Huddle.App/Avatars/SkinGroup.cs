namespace Agency.Huddle.App.Avatars;

/// <summary>
/// The three skin groups the Avatar Library's filter offers. An avatar's <c>skin_tone</c> maps to at most one of them.
/// </summary>
internal enum SkinGroup
{
    /// <summary>The <c>fair</c> and <c>light</c> tones.</summary>
    Light,

    /// <summary>The <c>tan</c> and <c>brown</c> tones.</summary>
    Tan,

    /// <summary>The <c>dark</c> tone.</summary>
    Dark,
}
