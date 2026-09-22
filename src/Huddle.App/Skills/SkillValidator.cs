using System.Collections.Frozen;
using System.Text;
using System.Text.RegularExpressions;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.App.Skills;

/// <summary>
/// Validates one candidate Skill's files against Spec §8.1, reading <c>SKILL.md</c>'s frontmatter
/// with the existing <see cref="PersonaFrontmatter.Parse"/> rather than a second YAML parser.
/// </summary>
/// <remarks>
/// <see cref="Grantable"/> lives here only until Task 4.1.i moves it to <c>SkillGrants</c> (D4);
/// nothing else in this type should assume it stays.
/// </remarks>
internal static partial class SkillValidator
{
    /// <summary>The maximum size, in UTF-8 bytes, a Skill file may be before it is dropped with a Warning (Spec §8.1 rule 7).</summary>
    private const int MaxFileSizeBytes = 65_536;

    /// <summary>The maximum length, in characters, a <c>description</c> may be before it is an Error (Spec §7.2).</summary>
    private const int MaxDescriptionLength = 500;

    /// <summary>The length, in characters, above which a <c>description</c> is a Warning (Spec §7.2).</summary>
    private const int DescriptionWarningLength = 300;

    private const string SkillMdFileName = "SKILL.md";
    private const string NameKey = "name";
    private const string DescriptionKey = "description";
    private const string ToolsKey = "tools";

    /// <summary>Tools that exist only for Personas holding a Skill that lists them (Spec §6.5).</summary>
    internal static readonly FrozenSet<string> Grantable =
        FrozenSet.ToFrozenSet(["validate_teammate", "propose_teammates"], StringComparer.Ordinal);

    /// <summary>Validates one candidate Skill's files against Spec §8.1 rules 1–7.</summary>
    /// <param name="folderName">The Skill's folder name; a valid <c>name</c> field must equal this, ordinally.</param>
    /// <param name="files">Every file found in the Skill's folder, keyed by file name.</param>
    /// <returns>What parsed, what survived, and every problem found.</returns>
    internal static SkillValidation Validate(string folderName, IReadOnlyDictionary<string, string> files)
    {
        List<SkillIssue> issues = [];

        SkillIssue Info(string message)
        {
            return new SkillIssue(folderName, message, SkillIssueSeverity.Info);
        }

        SkillIssue Warning(string message)
        {
            return new SkillIssue(folderName, message, SkillIssueSeverity.Warning);
        }

        SkillIssue Error(string message)
        {
            return new SkillIssue(folderName, message, SkillIssueSeverity.Error);
        }

        // Rule 1: SKILL.md present?
        if (!files.TryGetValue(SkillMdFileName, out string? skillMdText))
        {
            issues.Add(Error($"'{folderName}' has no SKILL.md."));
            return new SkillValidation(null, null, [], [], issues);
        }

        // Rule 2 ("frontmatter parses?") has no distinct outcome to check here: PersonaFrontmatter.Parse
        // never throws and never reports a parse failure — a SKILL.md with no (or unclosed) frontmatter
        // block simply comes back with an empty Fields list, which rule 3 below already turns into a
        // missing-'name' Error. There is no separate signal this method could observe for "did not parse"
        // that isn't already "has no fields", so rule 2 is not given its own Error here.
        (IReadOnlyList<PersonaFrontmatterField> fields, _) = PersonaFrontmatter.Parse(skillMdText);

        // Rule 3: name present, matches the regex, equals the folder name?
        string? name = GetFieldValue(fields, NameKey);
        if (string.IsNullOrWhiteSpace(name))
        {
            issues.Add(Error("SKILL.md is missing required field 'name'."));
            name = null;
        }
        else if (!NameRule().IsMatch(name))
        {
            issues.Add(Error($"'{name}' is not a valid Skill name: lowercase letters, digits and single hyphens only."));
        }
        else if (!string.Equals(name, folderName, StringComparison.Ordinal))
        {
            issues.Add(Error($"SKILL.md's name '{name}' does not match its folder name '{folderName}'."));
        }

        // Rule 4: description present, single line, <= 500 chars (Warning above 300)? A block-scalar
        // description's embedded line breaks are already collapsed to spaces by PersonaFrontmatter.Parse,
        // so every value this method ever sees is already one line — there is no separate "not single
        // line" case for this method to detect.
        string? description = GetFieldValue(fields, DescriptionKey);
        if (string.IsNullOrWhiteSpace(description))
        {
            issues.Add(Error("SKILL.md is missing required field 'description'."));
            description = null;
        }
        else if (description.Length > MaxDescriptionLength)
        {
            issues.Add(Error($"SKILL.md's description is {description.Length} characters; the limit is {MaxDescriptionLength}."));
        }
        else if (description.Length > DescriptionWarningLength)
        {
            issues.Add(Warning($"SKILL.md's description is {description.Length} characters; over {DescriptionWarningLength} is unusually long."));
        }

        // Rule 5: each tools entry in Grantable?
        List<string> tools = [];
        string? rawTools = GetFieldValue(fields, ToolsKey);
        if (!string.IsNullOrWhiteSpace(rawTools))
        {
            foreach (var tool in rawTools.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Grantable.Contains(tool))
                {
                    tools.Add(tool);
                }
                else
                {
                    issues.Add(Warning($"'{tool}' is not a tool a Skill can grant; it is offered to everyone already."));
                }
            }
        }

        // Rule 6: unknown keys are an Info.
        HashSet<string> knownKeys = new(StringComparer.OrdinalIgnoreCase) { NameKey, DescriptionKey, ToolsKey };
        foreach (var field in fields)
        {
            if (!knownKeys.Contains(field.Key))
            {
                issues.Add(Info($"SKILL.md has an unknown key '{field.Key}'; it is ignored."));
            }
        }

        // Rule 7: files are flat *.md <= 65,536 UTF-8 bytes. SKILL.md first, then ordinal order.
        List<string> orderedFileNames =
        [
            SkillMdFileName,
            .. files.Keys
                .Where(key => !string.Equals(key, SkillMdFileName, StringComparison.Ordinal))
                .Order(StringComparer.Ordinal),
        ];

        List<string> keptFiles = [];
        foreach (var fileName in orderedFileNames)
        {
            string text = files[fileName];
            int byteCount = Encoding.UTF8.GetByteCount(text);
            if (byteCount > MaxFileSizeBytes)
            {
                issues.Add(Warning($"'{fileName}' is {byteCount} bytes; the limit is {MaxFileSizeBytes} (skipped)."));
                continue;
            }

            keptFiles.Add(fileName);
        }

        return new SkillValidation(name, description, tools, keptFiles, issues);
    }

    /// <summary>Looks up the first frontmatter field matching <paramref name="key"/>, case-insensitively.</summary>
    /// <param name="fields">The parsed frontmatter fields, in file order.</param>
    /// <param name="key">The field key to find.</param>
    /// <returns>The matching field's value, or <see langword="null"/> when no field has this key.</returns>
    private static string? GetFieldValue(IReadOnlyList<PersonaFrontmatterField> fields, string key)
    {
        foreach (var field in fields)
        {
            if (string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return field.Value;
            }
        }

        return null;
    }

    [GeneratedRegex(@"\A[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex NameRule();
}
