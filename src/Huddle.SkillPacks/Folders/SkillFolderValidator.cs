using System.Text;
using System.Text.RegularExpressions;

namespace Agency.Huddle.SkillPacks.Folders;

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
        if (!files.TryGetValue(SkillMdFileName, out string? skillMdText))
        {
            issues.Add(MakeIssue(folderName, $"'{folderName}' has no SKILL.md.", FolderIssueSeverity.Error, FolderRule.NoSkillMd));
            return new SkillFolderValidation(null, null, [], [], [], [], issues);
        }

        IReadOnlyList<SkillFrontmatterField> fields = SkillFrontmatter.Parse(skillMdText);
        string? name = CheckName(SkillFrontmatter.Get(fields, NameKey), folderName, issues);
        string? description = CheckDescription(SkillFrontmatter.Get(fields, DescriptionKey), folderName, issues);
        (List<string> tools, List<string> allTools) = CheckTools(SkillFrontmatter.Get(fields, ToolsKey), grantable, folderName, issues);
        HashSet<string> knownKeys = new(StringComparer.OrdinalIgnoreCase) { NameKey, DescriptionKey, ToolsKey };
        foreach (SkillFrontmatterField field in fields)
        {
            if (!knownKeys.Contains(field.Key))
            {
                issues.Add(MakeIssue(folderName, $"SKILL.md has an unknown key '{field.Key}'; it is ignored.", FolderIssueSeverity.Info, FolderRule.UnknownKey));
            }
        }

        if (allowSupportingFiles && files.Count > MaxImportedFiles)
        {
            issues.Add(MakeIssue(folderName, $"'{folderName}' has {files.Count} files; an imported Skill may have no more than {MaxImportedFiles} files.", FolderIssueSeverity.Error, FolderRule.TooManyFiles));
        }

        List<string> kept = KeptFiles(folderName, files, issues);
        return new SkillFolderValidation(name, description, tools, allTools, kept, fields, issues);
    }

    /// <summary>One issue against the Skill folder <paramref name="folderName"/>.</summary>
    private static FolderIssue MakeIssue(string folderName, string message, FolderIssueSeverity severity, FolderRule rule)
    {
        return new FolderIssue(folderName, message, severity, rule);
    }

    /// <summary>Checks the <c>name</c> field; returns it, or null when it is missing.</summary>
    private static string? CheckName(string? name, string folderName, List<FolderIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            issues.Add(MakeIssue(folderName, "SKILL.md is missing required field 'name'.", FolderIssueSeverity.Error, FolderRule.Name));
            return null;
        }

        if (!NameRule().IsMatch(name))
        {
            issues.Add(MakeIssue(folderName, $"'{name}' is not a valid Skill name: lowercase letters, digits and single hyphens only.", FolderIssueSeverity.Error, FolderRule.Name));
        }
        else if (!string.Equals(name, folderName, StringComparison.Ordinal))
        {
            issues.Add(MakeIssue(folderName, $"SKILL.md's name '{name}' does not match its folder name '{folderName}'.", FolderIssueSeverity.Error, FolderRule.Name));
        }

        return name;
    }

    /// <summary>Checks the <c>description</c> field; returns it, or null when it is missing.</summary>
    private static string? CheckDescription(string? description, string folderName, List<FolderIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            issues.Add(MakeIssue(folderName, "SKILL.md is missing required field 'description'.", FolderIssueSeverity.Error, FolderRule.Description));
            return null;
        }

        if (description.Length > MaxDescriptionLength)
        {
            issues.Add(MakeIssue(folderName, $"SKILL.md's description is {description.Length} characters; the limit is {MaxDescriptionLength}.", FolderIssueSeverity.Error, FolderRule.Description));
        }
        else if (description.Length > DescriptionWarningLength)
        {
            issues.Add(MakeIssue(folderName, $"SKILL.md's description is {description.Length} characters; over {DescriptionWarningLength} is unusually long.", FolderIssueSeverity.Warning, FolderRule.LongDescription));
        }

        return description;
    }

    /// <summary>Splits the <c>tools</c> field into the grantable tools and all tools, warning about the ungrantable ones.</summary>
    private static (List<string> Tools, List<string> AllTools) CheckTools(string? rawTools, IReadOnlySet<string> grantable, string folderName, List<FolderIssue> issues)
    {
        List<string> tools = [];
        List<string> allTools = [];
        if (string.IsNullOrWhiteSpace(rawTools))
        {
            return (tools, allTools);
        }

        foreach (string tool in rawTools.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            allTools.Add(tool);
            if (grantable.Contains(tool))
            {
                tools.Add(tool);
            }
            else
            {
                issues.Add(MakeIssue(folderName, $"'{tool}' is not a tool a Skill can grant; it is offered to everyone already.", FolderIssueSeverity.Warning, FolderRule.UngrantableTool));
            }
        }

        return (tools, allTools);
    }

    /// <summary>The files under the size limit, <c>SKILL.md</c> first then the rest in ordinal order; the large ones are warned about.</summary>
    private static List<string> KeptFiles(string folderName, IReadOnlyDictionary<string, string> files, List<FolderIssue> issues)
    {
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
                issues.Add(MakeIssue(folderName, $"'{fileName}' is {byteCount} bytes; the limit is {MaxFileSizeBytes} (skipped).", FolderIssueSeverity.Warning, FolderRule.LargeFile));
                continue;
            }

            kept.Add(fileName);
        }

        return kept;
    }

    [GeneratedRegex(@"\A[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex NameRule();
}
