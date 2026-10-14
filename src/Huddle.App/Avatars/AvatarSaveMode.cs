namespace Agency.Huddle.App.Avatars;

/// <summary>
/// Whether <c>AvatarEditor</c> (in <c>Components.Shared</c>, which a public non-Razor type would trip CA1716 in) writes what the Human chooses to the avatar store as they choose it, or only reports it
/// and leaves the writing to the surface that owns the Save button. Public because it is a Razor <c>[Parameter]</c> type.
/// </summary>
public enum AvatarSaveMode
{
    /// <summary>Every change is written to the avatar store at once, under the editor's <c>Name</c>; there is no Save to wait for.</summary>
    Immediate,

    /// <summary>Nothing is written: each change is reported through the editor's callbacks and a chosen image's bytes are held back as pending.</summary>
    Buffered,
}
