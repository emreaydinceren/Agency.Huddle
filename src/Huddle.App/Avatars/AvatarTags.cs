namespace Agency.Huddle.App.Avatars;

/// <summary>
/// Which of an avatar's <c>master.json</c> tags describe what the picture looks like. The rest restate a
/// field the picker already shows another way: the skin tone (<c>skin-*</c>, the Skin buttons), the
/// background (<c>bg-*</c>, which the copy does not keep) and the gender words. A blank tag describes
/// nothing, so it never becomes an empty chip or an empty part of a name.
/// </summary>
internal static class AvatarTags
{
    /// <summary>True when <paramref name="tag"/> belongs in the tag cloud and the accessible name.</summary>
    /// <param name="tag">One tag as written in <c>master.json</c>.</param>
    /// <returns>
    /// False for a blank tag and for <c>skin-*</c>, <c>bg-*</c>, <c>man</c> and <c>woman</c>, compared ignoring
    /// case and surrounding white space.
    /// </returns>
    internal static bool IsDescriptive(string tag)
    {
        string trimmed = tag.Trim();
        return trimmed.Length > 0
            && !trimmed.StartsWith("skin-", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("bg-", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(trimmed, "man", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(trimmed, "woman", StringComparison.OrdinalIgnoreCase);
    }
}
