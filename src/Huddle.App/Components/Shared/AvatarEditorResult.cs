using Agency.Huddle.App.Avatars;

namespace Agency.Huddle.App.Components.Shared;

/// <summary>
/// What <see cref="AvatarEditorDialog"/> hands back when the Human commits: the avatar fields to show and, only when an
/// image was chosen and has not been written yet, that image's bytes. Internal because it travels as a dialog result's
/// <c>object</c>, never as a Razor <c>[Parameter]</c>.
/// </summary>
/// <param name="Avatar">The label, stored image name and background to show. <see cref="Avatars.Avatar.Image"/> is the stored file name, never the pending one.</param>
/// <param name="PendingImage">The bytes of a chosen image not yet written to the avatar store, or <see langword="null"/>.</param>
/// <param name="PendingImageExtension">The extension, with its dot, of <paramref name="PendingImage"/>; <see langword="null"/> exactly when it is.</param>
internal sealed record AvatarEditorResult(Avatar Avatar, byte[]? PendingImage, string? PendingImageExtension);
