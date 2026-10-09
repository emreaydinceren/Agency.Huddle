namespace Agency.Huddle.App.Avatars;

/// <summary>
/// Maps a library avatar's <c>skin_tone</c> text to its <see cref="SkinGroup"/>. The mapping lives in code,
/// not in <c>master.json</c>, so a new tone needs a code change and a test.
/// </summary>
internal static class SkinGroups
{
    /// <summary>
    /// Returns the group for a <c>skin_tone</c>, matched ordinally and ignoring case. A null, empty or
    /// unrecognised tone returns null, which places the avatar under All only.
    /// </summary>
    /// <param name="skinTone">The raw <c>skin_tone</c> value from <c>master.json</c>, or null when it is missing.</param>
    /// <returns>The group the tone belongs to, or null when it belongs to none.</returns>
    internal static SkinGroup? From(string? skinTone)
    {
        if (string.IsNullOrEmpty(skinTone))
        {
            return null;
        }

        if (string.Equals(skinTone, "fair", StringComparison.OrdinalIgnoreCase)
            || string.Equals(skinTone, "light", StringComparison.OrdinalIgnoreCase))
        {
            return SkinGroup.Light;
        }

        if (string.Equals(skinTone, "tan", StringComparison.OrdinalIgnoreCase)
            || string.Equals(skinTone, "brown", StringComparison.OrdinalIgnoreCase))
        {
            return SkinGroup.Tan;
        }

        if (string.Equals(skinTone, "dark", StringComparison.OrdinalIgnoreCase))
        {
            return SkinGroup.Dark;
        }

        return null;
    }
}
