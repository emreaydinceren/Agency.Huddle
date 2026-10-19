using System.Text;

namespace Agency.Huddle.App.SkillSources;

/// <summary>The fixed template the move writes SKILLS.md from (§8.8 "Create", §6.13): the title, the "do not edit by hand"
/// note, a line of description, and the two tables. LF line endings, one newline at the end.</summary>
internal static class SkillsMdTemplate
{
    /// <summary>The whole file.</summary>
    public static string Build(IReadOnlyList<TemplateRow> packs, IReadOnlyList<TemplateRow> catalogs)
    {
        ArgumentNullException.ThrowIfNull(packs);
        ArgumentNullException.ThrowIfNull(catalogs);
        StringBuilder text = new();
        text.Append("# Skills registry\n\n");
        text.Append("> We do not recommend editing this file by hand. Your AI tool adds and updates its rows by\n");
        text.Append("> following InstallSkillPack.md, and a mistake here makes a Pack or Skill show as Broken in\n");
        text.Append("> Huddle.\n\n");
        text.Append("Every Pack and Skill this Huddle knows about. Each row points at a file on this computer.\n\n");
        Table(text, "Packs", packs);
        text.Append('\n');
        Table(text, "Catalogs", catalogs);
        return text.ToString();
    }

    /// <summary>One heading and its table.</summary>
    private static void Table(StringBuilder text, string heading, IReadOnlyList<TemplateRow> rows)
    {
        text.Append("## ").Append(heading).Append("\n\n");
        text.Append("| Local path | Origin URL | Last updated | Notes |\n");
        text.Append("| --- | --- | --- | --- |\n");
        foreach (TemplateRow row in rows)
        {
            string[] cells = [Cell(row.LocalPath), Cell(row.OriginUrl), Cell(row.LastUpdated), Cell(row.Notes)];
            text.Append('|').Append(string.Join('|', cells.Select(static cell => cell.Length == 0 ? " " : $" {cell} "))).Append("|\n");
        }
    }

    /// <summary>A cell: <c>|</c> escaped, line breaks as spaces, trimmed.</summary>
    private static string Cell(string value)
    {
        string flat = value.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ").Trim();
        return flat.Length == 0 ? string.Empty : flat;
    }
}
