namespace Agency.Huddle.App.Avatars;

/// <summary>
/// Which of an avatar's <c>master.json</c> tags describe what the picture looks like. The rest restate a
/// field the picker already shows another way: the skin tone (<c>skin-*</c>, the Skin buttons), the
/// background (<c>bg-*</c>, which the copy does not keep) and the gender words.
/// </summary>
internal static class AvatarTags
{
    /// <summary>True when <paramref name="tag"/> belongs in the tag cloud and the accessible name.</summary>
    /// <param name="tag">One tag as written in <c>master.json</c>.</param>
    /// <returns>False for <c>skin-*</c>, <c>bg-*</c>, <c>man</c> and <c>woman</c>, compared ignoring case.</returns>
    internal static bool IsDescriptive(string tag) =>
        !tag.StartsWith("skin-", StringComparison.OrdinalIgnoreCase)
        && !tag.StartsWith("bg-", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(tag, "man", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(tag, "woman", StringComparison.OrdinalIgnoreCase);
}
