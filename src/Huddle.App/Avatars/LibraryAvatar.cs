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
    private const string GlassesTag = "glasses";

    /// <summary>
    /// The name a screen reader announces for the avatar: the skin tone and gender, then its descriptive tags,
    /// for example <c>fair man, short hair, black hair, black top, red glasses, beard</c>. Parts are trimmed,
    /// blank parts and repeated tags (ignoring case) are left out, a hyphen is read as a space
    /// (<c>no glasses</c>), and a bare <c>glasses</c> is dropped when a tag such as <c>red glasses</c> already
    /// says it. Falls back to the file name when nothing describes the picture.
    /// </summary>
    internal string AccessibleName
    {
        get
        {
            List<string> parts = [];
            string who = string.Join(' ', new[] { this.SkinTone.Trim(), this.Gender.Trim() }.Where(static part => part.Length > 0));
            if (who.Length > 0)
            {
                parts.Add(who);
            }

            List<string> tags = [.. this.Tags
                .Where(AvatarTags.IsDescriptive)
                .Select(static tag => tag.Trim().Replace('-', ' '))
                .Distinct(StringComparer.OrdinalIgnoreCase)];
            bool saysGlasses = tags.Exists(static tag => tag.Length > GlassesTag.Length
                && tag.EndsWith(" " + GlassesTag, StringComparison.OrdinalIgnoreCase)
                && !tag.StartsWith("no ", StringComparison.OrdinalIgnoreCase));
            parts.AddRange(tags.Where(tag => !saysGlasses || !string.Equals(tag, GlassesTag, StringComparison.OrdinalIgnoreCase)));

            return parts.Count > 0 ? string.Join(", ", parts) : Path.GetFileNameWithoutExtension(this.File);
        }
    }

    /// <summary>The skin group this avatar's tone belongs to, or null when the tone belongs to none.</summary>
    internal SkinGroup? Group => SkinGroups.From(this.SkinTone);
}
