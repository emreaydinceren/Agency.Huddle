namespace Agency.Huddle.App.Skills;

/// <summary>Words that say who made a Skill and where it lives, for a Skill that is not installed yet and so has no provenance line.</summary>
internal static class SkillOrigin
{
    private const string GitHubPrefix = "https://github.com/";

    /// <summary>Describes a Skill by its publisher and its place in the repository it comes from.</summary>
    /// <param name="publisher">Who published it, or <see langword="null"/> or blank when unknown.</param>
    /// <param name="source">The repository: <c>owner/repo</c> or a GitHub address, or <c>builtin</c>.</param>
    /// <param name="path">The Skill's folder inside the repository, or <see langword="null"/> for the default location.</param>
    /// <returns>For example <c>addyosmani · addyosmani/agent-skills/skills/incremental-implementation</c>; <c>Built in</c> for a built-in Skill.</returns>
    public static string Describe(string? publisher, string source, string? path)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (string.Equals(source, "builtin", StringComparison.Ordinal))
        {
            return "Built in";
        }

        string repo = source.StartsWith(GitHubPrefix, StringComparison.OrdinalIgnoreCase) ? source[GitHubPrefix.Length..] : source;
        repo = repo.TrimEnd('/');
        string where = string.IsNullOrWhiteSpace(path) ? repo : $"{repo}/{path.Trim('/')}";
        return string.IsNullOrWhiteSpace(publisher) ? where : $"{publisher} · {where}";
    }
}
