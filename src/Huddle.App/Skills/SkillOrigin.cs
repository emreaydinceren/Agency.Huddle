namespace Agency.Huddle.App.Skills;

/// <summary>Words that say who made a Skill and where it lives: its publisher and its path in the repository it comes from. Shown in the Skills dropdowns for every Skill, installed or not.</summary>
internal static class SkillOrigin
{
    private const string GitHubPrefix = "https://github.com/";

    /// <summary>Describes where an installed Skill lives: its publisher and path in the repository it was imported from, or, for one with no provenance record, its folder under the Skills folder. The provenance line already says "Built in" or "Yours", so a shipped Skill with no folder of its own adds nothing.</summary>
    /// <param name="skill">The installed Skill.</param>
    /// <returns>The words, or <see langword="null"/> when there is nothing to add.</returns>
    public static string? Of(Skill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);

        if (skill.Provenance is { } provenance)
        {
            return Describe(provenance.Publisher, provenance.Repo, provenance.Path);
        }

        return skill.FolderPath is null ? null : $"Skills/{Path.GetFileName(skill.FolderPath.TrimEnd('/', '\\'))}";
    }

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
