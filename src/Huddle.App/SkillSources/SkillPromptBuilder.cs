using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.SkillPacks.Checking;

namespace Agency.Huddle.App.SkillSources;

/// <summary>Where <c>InstallSkillPack.md</c> is; lets <see cref="SkillPromptBuilder"/> avoid the sealed writer.</summary>
internal interface IInstallFileLocation
{
    /// <summary>The full path of <c>{DataDir}/InstallSkillPack.md</c>.</summary>
    string InstallFilePath { get; }
}

/// <summary>Builds every prompt the Human copies into their AI tool (§6.8), through <see cref="IPromptSource"/> so Settings ›
/// Prompts can reword each one. Typed text becomes one capped line; findings become an indented block. It returns text and
/// talks to nothing.</summary>
internal sealed partial class SkillPromptBuilder(IPromptSource prompts, IInstallFileLocation installFile)
{
    private const int MaxWhere = 500;
    private const int MaxPurpose = 300;
    private const int MaxProblems = 20;
    private const int MaxProblemLine = 300;
    private const string Indent = "    ";

    /// <summary>The install file's path in the chosen format, unquoted; what <b>Copy path</b> copies.</summary>
    public string InstallFilePath(PathFormat format)
    {
        return PathFormats.Convert(installFile.InstallFilePath, format);
    }

    /// <summary>Task <b>Add from a link or folder</b>.</summary>
    public string Add(string where, string? purpose, PathFormat format = PathFormat.Windows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(where);
        return this.Render("skillPrompt.add", format, new()
        {
            ["{{where}}"] = OneLine(where, MaxWhere),
            ["{{purposeLine}}"] = PurposeLine("What they are for: ", purpose),
        });
    }

    /// <summary>Task <b>Find Packs for a role</b>.</summary>
    public string FindForRole(string roleId, string roleTitle, string? purpose, PathFormat format = PathFormat.Windows)
    {
        ArgumentNullException.ThrowIfNull(roleId);
        ArgumentNullException.ThrowIfNull(roleTitle);
        return this.Render("skillPrompt.findForRole", format, new()
        {
            ["{{roleId}}"] = OneLine(roleId, MaxPurpose),
            ["{{roleTitle}}"] = OneLine(roleTitle, MaxPurpose),
            ["{{purposeLine}}"] = PurposeLine("It should get better at: ", purpose),
        });
    }

    /// <summary>Task <b>Check for updates</b>; no Source ids means every Source.</summary>
    public string Update(IReadOnlyList<string> sourceIds, PathFormat format = PathFormat.Windows)
    {
        ArgumentNullException.ThrowIfNull(sourceIds);
        string sources = sourceIds.Count == 0 ? "all the Sources in SKILLS.md" : string.Join('\n', sourceIds.Select(static id => $"- {OneLine(id, MaxPurpose)}"));
        return this.Render("skillPrompt.update", format, new() { ["{{sources}}"] = sources });
    }

    /// <summary>Task <b>Fix a problem</b>, and <b>Copy fix prompt</b> on a Broken or Not downloaded row.</summary>
    public string Fix(string table, string localPath, string state, IReadOnlyList<Finding> findings, PathFormat format = PathFormat.Windows)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(localPath);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(findings);
        return this.Render("skillPrompt.fix", format, new()
        {
            ["{{table}}"] = OneLine(table, MaxPurpose),
            ["{{localPath}}"] = OneLine(localPath, MaxWhere),
            ["{{state}}"] = OneLine(state, MaxPurpose),
            ["{{problems}}"] = Problems(findings),
        });
    }

    /// <summary><b>Copy prompt</b> beside a Not downloaded Skill.</summary>
    public string FetchSkill(string skillName, string catalogId, string? catalogPath, string? origin, PathFormat format = PathFormat.Windows)
    {
        ArgumentNullException.ThrowIfNull(skillName);
        ArgumentNullException.ThrowIfNull(catalogId);
        return this.Render("skillPrompt.fetchSkill", format, new()
        {
            ["{{skillName}}"] = OneLine(skillName, MaxPurpose),
            ["{{catalogId}}"] = OneLine(catalogId, MaxPurpose),
            ["{{catalogPath}}"] = string.IsNullOrWhiteSpace(catalogPath) ? "(no path on this computer)" : OneLine(catalogPath, MaxWhere),
            ["{{origin}}"] = string.IsNullOrWhiteSpace(origin) ? "(unknown)" : OneLine(origin, MaxWhere),
        });
    }

    /// <summary><b>Copy prompt</b> on a Needs setup draft in Build a team.</summary>
    public string NeedsSetup(string roleId, string roleTitle, IReadOnlyList<MissingItem> missing, PathFormat format = PathFormat.Windows)
    {
        ArgumentNullException.ThrowIfNull(roleId);
        ArgumentNullException.ThrowIfNull(roleTitle);
        ArgumentNullException.ThrowIfNull(missing);
        string lines = string.Join('\n', missing.Select(MissingLine));
        return this.Render("skillPrompt.needsSetup", format, new()
        {
            ["{{roleId}}"] = OneLine(roleId, MaxPurpose),
            ["{{roleTitle}}"] = OneLine(roleTitle, MaxPurpose),
            ["{{missing}}"] = lines,
        });
    }

    /// <summary>One <c>{{missing}}</c> line: <c>Pack "Title" (id)</c>, <c>Skill "name" (catalog id)</c> or <c>Skill "name" (not in any catalog)</c>.</summary>
    private static string MissingLine(MissingItem item)
    {
        if (item.IsPack)
        {
            return $"Pack \"{OneLine(item.Title, MaxPurpose)}\" ({item.Name})";
        }

        return item.CatalogId is { } catalog ? $"Skill \"{item.Name}\" (catalog {catalog})" : $"Skill \"{item.Name}\" (not in any catalog)";
    }

    /// <summary>Text the Human typed as one line: control characters dropped, white space runs one space, trimmed, cut at
    /// <paramref name="max"/> with an ellipsis.</summary>
    internal static string OneLine(string text, int max)
    {
        string flat = WhiteSpace().Replace(new string([.. text.Where(static c => !char.IsControl(c) || char.IsWhiteSpace(c))]), " ").Trim();
        return flat.Length <= max ? flat : flat[..(max - 1)].TrimEnd() + "…";
    }

    /// <summary>The <c>{{purposeLine}}</c>: the prefix and the purpose with one final full stop; empty when blank.</summary>
    private static string PurposeLine(string prefix, string? purpose)
    {
        if (string.IsNullOrWhiteSpace(purpose))
        {
            return string.Empty;
        }

        string text = OneLine(purpose, MaxPurpose).TrimEnd('.');
        return $"{prefix}{text}.";
    }

    /// <summary>The <c>{{problems}}</c> block: errors first, at most 20, each on one indented line.</summary>
    private static string Problems(IReadOnlyList<Finding> findings)
    {
        if (findings.Count == 0)
        {
            return Indent + "(none recorded)";
        }

        List<Finding> ordered = [.. findings.OrderByDescending(static finding => finding.Severity)];
        StringBuilder text = new();
        foreach (Finding finding in ordered.Take(MaxProblems))
        {
            string severity = finding.Severity == Severity.Error ? "ERROR" : "WARN";
            string line = OneLine($"{severity} {finding.Code} {finding.File}: {finding.Message}", MaxProblemLine);
            text.Append(Indent).Append(line).Append('\n');
        }

        if (ordered.Count > MaxProblems)
        {
            text.Append(Indent).Append(string.Create(CultureInfo.InvariantCulture, $"… and {ordered.Count - MaxProblems} more; run validate on the row's path."));
        }

        return text.ToString().TrimEnd('\n');
    }

    /// <summary>Renders a key with the install file filled in, then tidies the result.</summary>
    private string Render(string key, PathFormat format, Dictionary<string, string> values)
    {
        string path = this.InstallFilePath(format);
        values["{{installFile}}"] = path.Any(char.IsWhiteSpace) ? $"\"{path}\"" : path;
        return Tidy(prompts.Render(key, values));
    }

    /// <summary>Trailing spaces trimmed from each line, three or more line breaks made two, the end trimmed.</summary>
    internal static string Tidy(string text)
    {
        string lines = string.Join('\n', text.ReplaceLineEndings("\n").Split('\n').Select(static line => line.TrimEnd()));
        return ManyBreaks().Replace(lines, "\n\n").TrimEnd();
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex WhiteSpace();

    [GeneratedRegex(@"\n{3,}", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex ManyBreaks();
}
