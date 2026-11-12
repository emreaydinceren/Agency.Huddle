namespace Agency.Huddle.App.Avatars;

/// <summary>
/// One entry of the Avatar Library's <c>master.json</c>: an image file and the attributes the picker filters on.
/// </summary>
/// <param name="File">The image file name, relative to the library folder.</param>
/// <param name="SkinTone">The raw <c>skin_tone</c> text, kept as written in <c>master.json</c>.</param>
/// <param name="Gender">The gender word the avatar is described with.</param>
/// <param name="Tags">The free-form tags the picker's tag cloud and search are built from.</param>
internal sealed record LibraryAvatar(string File, string SkinTone, string Gender, IReadOnlyList<string> Tags)
{
    /// <summary>
    /// The name a screen reader announces for the avatar: the raw skin tone and gender, then its descriptive
    /// tags, for example <c>fair woman, long hair, red hair, blue top, glasses, blue glasses</c>. Falls back to
    /// the file name when the record says nothing about the picture.
    /// </summary>
    internal string AccessibleName
    {
        get
        {
            string who = string.Join(' ', new[] { this.SkinTone, this.Gender }.Where(static part => part.Length > 0));
            List<string> parts = [.. this.Tags.Where(AvatarTags.IsDescriptive)];
            if (who.Length > 0)
            {
                parts.Insert(0, who);
            }

            return parts.Count > 0 ? string.Join(", ", parts) : Path.GetFileNameWithoutExtension(this.File);
        }
    }

    /// <summary>The skin group this avatar's tone belongs to, or null when the tone belongs to none.</summary>
    internal SkinGroup? Group => SkinGroups.From(this.SkinTone);
}
