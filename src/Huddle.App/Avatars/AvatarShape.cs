namespace Agency.Huddle.App.Avatars;

/// <summary>
/// The outline an avatar is drawn in. An Agent or the Human is a <see cref="Circle"/>; a Team is a <see cref="RoundedSquare"/>, so a
/// Team never reads as a person. Public because a Razor <c>[Parameter]</c> may not be of an <c>internal</c> type (<c>CS0053</c>).
/// </summary>
public enum AvatarShape
{
    /// <summary>A circle: the face of an Agent or the Human.</summary>
    Circle,

    /// <summary>A square with rounded corners: the face of a Team.</summary>
    RoundedSquare,
}
