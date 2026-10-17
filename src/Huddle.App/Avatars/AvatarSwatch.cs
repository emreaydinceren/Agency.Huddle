namespace Agency.Huddle.App.Avatars;

/// <summary>One swatch of the avatar editor's background palette.</summary>
/// <param name="Hex">The colour as the avatar store holds a background: six-digit hex such as <c>#dc143c</c>.</param>
/// <param name="Name">The name a screen reader says for it, unique within the palette.</param>
internal sealed record AvatarSwatch(string Hex, string Name);