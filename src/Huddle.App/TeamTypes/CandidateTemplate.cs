using Agency.Huddle.App.Acp;

namespace Agency.Huddle.App.TeamTypes;

/// <summary>A candidate Teammate template read from a team type's candidate file (Spec §Candidate).</summary>
/// <param name="Title">The role title.</param>
/// <param name="Alias">The optional alias.</param>
/// <param name="Skills">The skill ids the candidate starts with.</param>
/// <param name="Adapter">The optional adapter id.</param>
/// <param name="JobDescriptionFields">Every other non-reserved field, in file order.</param>
/// <param name="Stance">The body text that becomes the Teammate's stance.</param>
internal sealed record CandidateTemplate(
    string Title,
    string? Alias,
    IReadOnlyList<string> Skills,
    string? Adapter,
    IReadOnlyList<PersonaFrontmatterField> JobDescriptionFields,
    string Stance);

/// <summary>The outcome of parsing a candidate file.</summary>
/// <param name="Template">The template, or <see langword="null"/> when there are problems.</param>
/// <param name="Problems">Why the file is not a valid candidate; empty when it is valid.</param>
internal sealed record CandidateParseResult(CandidateTemplate? Template, IReadOnlyList<string> Problems);

/// <summary>Parses candidate files into <see cref="CandidateTemplate"/> values.</summary>
internal static class CandidateTemplateParser
{
    private const string NameKey = "name";
    private const string TitleKey = "title";
    private const string AliasKey = "alias";
    private const string SkillsKey = "skills";
    private const string AdapterKey = "adapter";
    private const string WatchesKey = "watches";

    /// <summary>Parses a candidate file.</summary>
    /// <param name="text">The raw file text.</param>
    /// <returns>The template, or the problems that prevent one.</returns>
    public static CandidateParseResult Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        (IReadOnlyList<PersonaFrontmatterField> fields, string body) = PersonaFrontmatter.Parse(text);
        List<string> problems = [];

        if (Find(fields, NameKey) is not null)
        {
            problems.Add("A candidate has no name; remove the 'name' field. Hiring supplies the name.");
        }

        string? title = Find(fields, TitleKey);
        if (string.IsNullOrWhiteSpace(title))
        {
            problems.Add("The candidate has no 'title' field.");
        }

        string stance = body.Trim();
        if (stance.Length == 0)
        {
            problems.Add("The candidate has no stance: the text after the frontmatter is empty.");
        }

        if (problems.Count > 0 || title is null)
        {
            return new(null, problems);
        }

        string? skillsValue = Find(fields, SkillsKey);
        string[] skills = skillsValue is null
            ? []
            : skillsValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        List<PersonaFrontmatterField> jobDescription = [.. fields.Where(static f => !IsExcludedFromJobDescription(f.Key))];

        return new(new(title.Trim(), NullIfBlank(Find(fields, AliasKey)), skills, NullIfBlank(Find(fields, AdapterKey)), jobDescription, stance), []);
    }

    /// <summary>Returns the first field's value whose key matches, ignoring case, or <see langword="null"/>.</summary>
    private static string? Find(IReadOnlyList<PersonaFrontmatterField> fields, string key)
    {
        return fields.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    /// <summary>Trims a value and maps blank to <see langword="null"/>.</summary>
    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>Tells whether a key is reserved or carried by a dedicated member rather than the job description.</summary>
    private static bool IsExcludedFromJobDescription(string key)
    {
        return key.StartsWith('_')
            || string.Equals(key, NameKey, StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, TitleKey, StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, AliasKey, StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, SkillsKey, StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, AdapterKey, StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, WatchesKey, StringComparison.OrdinalIgnoreCase);
    }
}
