namespace Agency.Huddle.App.Avatars;

/// <summary>One library image read on demand: its bytes and the extension its bytes prove.</summary>
/// <param name="Bytes">The file's bytes, at most <see cref="AvatarImage.MaxBytes"/>.</param>
/// <param name="Extension">The extension including the dot, from <see cref="AvatarImage.SniffExtension"/>.</param>
internal sealed record AvatarLibraryImage(byte[] Bytes, string Extension);
