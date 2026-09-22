namespace Agency.Huddle.App.Components.Shared;

/// <summary>
/// <see cref="TeammateCard"/>'s own editor-only choice between the three ways a Teammate's
/// <see cref="Agency.Huddle.App.Avatars.Avatar"/> can be customised - never persisted, and never read
/// back from disk. <see cref="Agency.Huddle.App.Avatars.Avatar"/> carries no discriminator field of its
/// own; rendering (and this editor) both derive the choice from which of <c>Image</c> and <c>Label</c>
/// is set, the same precedence <see cref="TeammateAvatar"/> renders by. Deliberately <c>internal</c>
/// rather than <c>public</c>: it backs a local field on <see cref="TeammateCard"/>, never a Razor
/// <c>[Parameter]</c> - a <c>[Parameter]</c> may not be of an <c>internal</c> type (<c>CS0053</c>,
/// see <c>docs/agencyteam/rules.md</c>), which is exactly the constraint this type is exempt from.
/// </summary>
internal enum AvatarChoice
{
    /// <summary>No <see cref="Agency.Huddle.App.Avatars.Avatar.Label"/> and no <see cref="Agency.Huddle.App.Avatars.Avatar.Image"/> chosen - rendering falls back to initials derived from the Name.</summary>
    Initials,

    /// <summary>A short text <see cref="Agency.Huddle.App.Avatars.Avatar.Label"/> stands in for an image.</summary>
    Label,

    /// <summary>An uploaded <see cref="Agency.Huddle.App.Avatars.Avatar.Image"/> is shown.</summary>
    Image,
}
