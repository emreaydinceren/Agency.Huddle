namespace Agency.Huddle.App.Avatars;

/// <summary>
/// One entry of the Avatar Library's <c>master.json</c>: an image file and the attributes the picker filters on.
/// </summary>
/// <param name="File">The image file name, relative to the library folder.</param>
/// <param name="Profession">The single profession the avatar depicts.</param>
/// <param name="SkinTone">The raw <c>skin_tone</c> text, kept as written in <c>master.json</c>.</param>
/// <param name="Gender">The gender word the avatar is described with.</param>
/// <param name="Tags">The free-form tags the picker's tag cloud is built from.</param>
internal sealed record LibraryAvatar(string File, string Profession, string SkinTone, string Gender, IReadOnlyList<string> Tags)
{
    /// <summary>
    /// The name a screen reader announces for the avatar: the raw skin tone, gender and profession, for example <c>fair woman Podcaster</c>.
    /// </summary>
    internal string AccessibleName => $"{this.SkinTone} {this.Gender} {this.Profession}";

    /// <summary>The skin group this avatar's tone belongs to, or null when the tone belongs to none.</summary>
    internal SkinGroup? Group => SkinGroups.From(this.SkinTone);
}
