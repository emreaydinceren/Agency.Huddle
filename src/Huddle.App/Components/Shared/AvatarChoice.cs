namespace Agency.Huddle.App.Components.Shared;

/// <summary>
/// <see cref="AvatarEditor"/>'s own editor-only choice between the four cards an Agent's
/// <see cref="Agency.Huddle.App.Avatars.Avatar"/> can be customised with - never persisted, and never read
/// back from disk. <see cref="Agency.Huddle.App.Avatars.Avatar"/> carries no discriminator field of its
/// own; rendering (and this editor) both derive the choice from which of <c>Image</c> and <c>Label</c>
/// is set, the same precedence <see cref="AgentAvatar"/> renders by, and a saved image does not record
/// whether it came from the library or an upload, so it always reopens on <see cref="Upload"/>. Deliberately
/// <c>internal</c> rather than <c>public</c>: it backs a private field of <see cref="AvatarEditor"/>, never a
/// Razor <c>[Parameter]</c> - a <c>[Parameter]</c> may not be of an <c>internal</c> type (<c>CS0053</c>,
/// see <c>docs/engineering/rules.md</c>), which is exactly the constraint this type is exempt from.
/// </summary>
internal enum AvatarChoice
{
    /// <summary>No <see cref="Agency.Huddle.App.Avatars.Avatar.Label"/> and no <see cref="Agency.Huddle.App.Avatars.Avatar.Image"/> chosen - rendering falls back to initials derived from the Name.</summary>
    Initials,

    /// <summary>A short text <see cref="Agency.Huddle.App.Avatars.Avatar.Label"/> stands in for an image.</summary>
    Label,

    /// <summary>A picture taken from the Avatar Library is shown. Not selectable until the card holds a picture.</summary>
    Library,

    /// <summary>An uploaded <see cref="Agency.Huddle.App.Avatars.Avatar.Image"/> is shown; selectable with no image, when the avatar falls back to initials.</summary>
    Upload,
}
