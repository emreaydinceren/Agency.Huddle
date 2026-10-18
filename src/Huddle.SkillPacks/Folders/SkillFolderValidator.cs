using System.Text;
using System.Text.RegularExpressions;

namespace Agency.Huddle.SkillPacks.Folders;

/// <summary>How serious a problem found while validating a Skill folder is.</summary>
public enum FolderIssueSeverity
{
    /// <summary>Worth surfacing, but not a defect: an unknown frontmatter key, for example.</summary>
    Info,

    /// <summary>Something was ignored or dropped, but the Skill still loads.</summary>
    Warning,

    /// <summary>The Skill failed to validate.</summary>
    Error,
}

/// <summary>Which rule a folder issue comes from, so the checks can map it to a code (§6.3).</summary>
public enum FolderRule
{
    /// <summary>Rule 1: no <c>SKILL.md</c>.</summary>
    NoSkillMd,

    /// <summary>Rule 3: <c>name</c> missing, invalid or not the folder name.</summary>
    Name,

    /// <summary>Rule 4: <c>description</c> missing or over 500 characters.</summary>
    Description,

    /// <summary>Rule 4: <c>description</c> over 300 characters (a warning).</summary>
    LongDescription,

    /// <summary>Rule 5: a <c>tools</c> entry that is not grantable.</summary>
    UngrantableTool,

    /// <summary>Rule 6: an unknown frontmatter key.</summary>
    UnknownKey,

    /// <summary>More than 200 files.</summary>
    TooManyFiles,

    /// <summary>Rule 7: a file over 65,536 UTF-8 bytes.</summary>
    LargeFile,
}

/// <summary>One problem found in a Skill folder.</summary>
/// <param name="Skill">The Skill's folder name.</param>
/// <param name="Message">A human-readable description of the problem.</param>
/// <param name="Severity">How serious the problem is.</param>
/// <param name="Rule">Which rule found it.</param>
public sealed record FolderIssue(string Skill, string Message, FolderIssueSeverity Severity, FolderRule Rule);

/// <summary>What validating one Skill folder found.</summary>
/// <param name="Name">The parsed <c>name</c>, or null when missing or blank.</param>
/// <param name="Description">The parsed <c>description</c>, or null when missing or blank.</param>
/// <param name="Tools">The <c>tools</c> entries that are in the grantable set.</param>
/// <param name="AllTools">Every <c>tools</c> entry as written, split on <c>,</c> and <c>;</c> (S011 reads these).</param>
/// <param name="Files">The files that passed rule 7, <c>SKILL.md</c> first, then ordinal order.</param>
/// <param name="Fields">The frontmatter fields.</param>
/// <param name="Issues">Every problem found.</param>
public sealed record SkillFolderValidation(
    string? Name,
    string? Description,
    IReadOnlyList<string> Tools,
    IReadOnlyList<string> AllTools,
    IReadOnlyList<string> Files,
    IReadOnlyList<SkillFrontmatterField> Fields,
    IReadOnlyList<FolderIssue> Issues)
{
    /// <summary>Whether any issue is an error.</summary>
    public bool HasErrors => this.Issues.Any(static issue => issue.Severity == FolderIssueSeverity.Error);
}

/// <summary>
/// Validates one Skill's files against the Skills spec §8.1, reading <c>SKILL.md</c>'s frontmatter with
/// <see cref="SkillFrontmatter"/>. Moved from the app (§6.3); the app passes its grantable tool names.
/// </summary>
public static partial class SkillFolderValidator
{
    /// <summary>The maximum size, in UTF-8 bytes, a Skill file may be before it is dropped with a warning.</summary>
    public const int MaxFileSizeBytes = 65_536;

    /// <summary>The maximum length of a <c>description</c> before it is an error.</summary>
    public const int MaxDescriptionLength = 500;

    /// <summary>The length above which a <c>description</c> is a warning.</summary>
    public const int DescriptionWarningLength = 300;

    /// <summary>The most files a Skill folder with supporting files may hold.</summary>
    public const int MaxImportedFiles = 200;

    private const string SkillMdFileName = "SKILL.md";
    private const string NameKey = "name";
    private const string DescriptionKey = "description";
    private const string ToolsKey = "tools";

    /// <summary>Validates one Skill's files.</summary>
    /// <param name="folderName">The Skill's folder name; a valid <c>name</c> must equal it, ordinally.</param>
    /// <param name="files">Every file in the folder, keyed by relative path with <c>/</c> separators.</param>
    /// <param name="grantable">The tool names a Skill may grant; any other <c>tools</c> entry is a warning.</param>
    /// <param name="allowSupportingFiles">True when sub-folder and non-<c>.md</c> files are accepted, up to 200 files.</param>
    /// <returns>What parsed, what survived, and every problem found.</returns>
    public static SkillFolderValidation Validate(string folderName, IReadOnlyDictionary<string, string> files, IReadOnlySet<string> grantable, bool allowSupportingFiles = false)
    {
        ArgumentNullException.ThrowIfNull(folderName);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(grantable);
        List<FolderIssue> issues = [];

        FolderIssue Make(string message, FolderIssueSeverity severity, FolderRule rule)
        {
            return new FolderIssue(folderName, message, severity, rule);
        }

        if (!files.TryGetValue(SkillMdFileName, out string? skillMdText))
        {
            issues.Add(Make($"'{folderName}' has no SKILL.md.", FolderIssueSeverity.Error, FolderRule.NoSkillMd));
            return new SkillFolderValidation(null, null, [], [], [], [], issues);
        }

        IReadOnlyList<SkillFrontmatterField> fields = SkillFrontmatter.Parse(skillMdText);

        string? name = SkillFrontmatter.Get(fields, NameKey);
        if (string.IsNullOrWhiteSpace(name))
        {
            issues.Add(Make("SKILL.md is missing required field 'name'.", FolderIssueSeverity.Error, FolderRule.Name));
            name = null;
        }
        else if (!NameRule().IsMatch(name))
        {
            issues.Add(Make($"'{name}' is not a valid Skill name: lowercase letters, digits and single hyphens only.", FolderIssueSeverity.Error, FolderRule.Name));
        }
        else if (!string.Equals(name, folderName, StringComparison.Ordinal))
        {
            issues.Add(Make($"SKILL.md's name '{name}' does not match its folder name '{folderName}'.", FolderIssueSeverity.Error, FolderRule.Name));
        }

        string? description = SkillFrontmatter.Get(fields, DescriptionKey);
        if (string.IsNullOrWhiteSpace(description))
        {
            issues.Add(Make("SKILL.md is missing required field 'description'.", FolderIssueSeverity.Error, FolderRule.Description));
            description = null;
        }
        else if (description.Length > MaxDescriptionLength)
        {
            issues.Add(Make($"SKILL.md's description is {description.Length} characters; the limit is {MaxDescriptionLength}.", FolderIssueSeverity.Error, FolderRule.Description));
        }
        else if (description.Length > DescriptionWarningLength)
        {
            issues.Add(Make($"SKILL.md's description is {description.Length} characters; over {DescriptionWarningLength} is unusually long.", FolderIssueSeverity.Warning, FolderRule.LongDescription));
        }

        List<string> tools = [];
        List<string> allTools = [];
        string? rawTools = SkillFrontmatter.Get(fields, ToolsKey);
        if (!string.IsNullOrWhiteSpace(rawTools))
        {
            foreach (string tool in rawTools.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                allTools.Add(tool);
                if (grantable.Contains(tool))
                {
                    tools.Add(tool);
                }
                else
                {
                    issues.Add(Make($"'{tool}' is not a tool a Skill can grant; it is offered to everyone already.", FolderIssueSeverity.Warning, FolderRule.UngrantableTool));
                }
            }
        }

        HashSet<string> knownKeys = new(StringComparer.OrdinalIgnoreCase) { NameKey, DescriptionKey, ToolsKey };
        foreach (SkillFrontmatterField field in fields)
        {
            if (!knownKeys.Contains(field.Key))
            {
                issues.Add(Make($"SKILL.md has an unknown key '{field.Key}'; it is ignored.", FolderIssueSeverity.Info, FolderRule.UnknownKey));
            }
        }

        if (allowSupportingFiles && files.Count > MaxImportedFiles)
        {
            issues.Add(Make($"'{folderName}' has {files.Count} files; an imported Skill may have no more than {MaxImportedFiles} files.", FolderIssueSeverity.Error, FolderRule.TooManyFiles));
        }

        List<string> ordered =
        [
            SkillMdFileName,
            .. files.Keys.Where(static key => !string.Equals(key, SkillMdFileName, StringComparison.Ordinal)).Order(StringComparer.Ordinal),
        ];

        List<string> kept = [];
        foreach (string fileName in ordered)
        {
            int byteCount = Encoding.UTF8.GetByteCount(files[fileName]);
            if (byteCount > MaxFileSizeBytes)
            {
                issues.Add(Make($"'{fileName}' is {byteCount} bytes; the limit is {MaxFileSizeBytes} (skipped).", FolderIssueSeverity.Warning, FolderRule.LargeFile));
                continue;
            }

            kept.Add(fileName);
        }

        return new SkillFolderValidation(name, description, tools, allTools, kept, fields, issues);
    }

    [GeneratedRegex(@"\A[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex NameRule();
}
