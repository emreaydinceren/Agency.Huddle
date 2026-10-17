namespace Agency.Huddle.App.Components.Settings;

/// <summary>Turns a Skill's folder name (its slug) into the name a Human reads (design-system "Show a display name, never a raw id or slug").</summary>
internal static class SkillNames
{
    /// <summary>Sentence-cases a slug: <c>team-building</c> becomes <c>Team building</c>.</summary>
    /// <param name="slug">The Skill's name as its folder spells it.</param>
    /// <returns>The slug with hyphens and underscores as spaces and its first letter upper-cased, or the slug unchanged when it is blank.</returns>
    public static string Display(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return slug;
        }

        string spaced = slug.Trim().Replace('-', ' ').Replace('_', ' ');
        return string.Concat(char.ToUpperInvariant(spaced[0]), spaced[1..]);
    }
}
