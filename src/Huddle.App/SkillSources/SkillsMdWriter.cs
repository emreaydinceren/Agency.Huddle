using System.Text;
using Agency.Huddle.App.Library;
using Agency.Huddle.SkillPacks.Reading;

namespace Agency.Huddle.App.SkillSources;

/// <summary>The outcome of removing a row from SKILLS.md.</summary>
internal enum RemoveRowResult
{
    /// <summary>The row's lines were deleted.</summary>
    Removed,

    /// <summary>No row points at that file.</summary>
    NotFound,

    /// <summary>The file kept changing between the read and the write, three times.</summary>
    KeptChanging,
}

/// <summary>Deletes a row's lines from SKILLS.md (§8.8): every other byte is written back as it was, through a temporary
/// file and a move. If the file changed between the read and the write (by length or write time), it starts again, at
/// most three times. Huddle never reformats SKILLS.md.</summary>
internal sealed class SkillsMdWriter(SkillSourcesSettings settings)
{
    private const int Attempts = 3;

    /// <summary>The full path of SKILLS.md.</summary>
    public string FilePath => Path.Combine(settings.Dir, RegistryReader.FileName);

    /// <summary>Removes the first row of <paramref name="kind"/> whose Local path resolves, for this computer, to
    /// <paramref name="resolvedFile"/>.</summary>
    public RemoveRowResult RemoveRow(RowKind kind, string resolvedFile)
    {
        ArgumentNullException.ThrowIfNull(resolvedFile);
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            if (!File.Exists(this.FilePath))
            {
                return RemoveRowResult.NotFound;
            }

            FileInfo before = new(this.FilePath);
            long length = before.Length;
            DateTime written = before.LastWriteTimeUtc;
            byte[] bytes = File.ReadAllBytes(this.FilePath);
            byte[]? edited = Remove(bytes, kind, resolvedFile, settings.Dir, settings.ComputerName);
            if (edited is null)
            {
                return RemoveRowResult.NotFound;
            }

            FileInfo now = new(this.FilePath);
            if (now.Length != length || now.LastWriteTimeUtc != written)
            {
                continue;
            }

            AtomicFile.WriteBytes(this.FilePath, edited);
            return RemoveRowResult.Removed;
        }

        return RemoveRowResult.KeptChanging;
    }

    /// <summary>The file's bytes without the matching row's lines, or null when no row matches. Pure, for tests.</summary>
    public static byte[]? Remove(byte[] bytes, RowKind kind, string resolvedFile, string registryDir, string computer)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        string text = Encoding.UTF8.GetString(bytes);
        Registry registry = RegistryReader.Read(text, registryDir, computer);
        IReadOnlyList<RegistryRow> rows = kind == RowKind.Pack ? registry.Packs : registry.Catalogs;
        RegistryRow? row = rows.FirstOrDefault(row => row.LocalPath.Entries.Any(entry =>
            string.Equals(PathEntries.MakeFull(entry.Path, registryDir), Path.GetFullPath(resolvedFile), StringComparison.OrdinalIgnoreCase)));
        if (row is null)
        {
            return null;
        }

        List<(int Start, int Length)> lines = SplitLines(bytes);
        int first = row.Span.FirstLine - 1;
        int last = row.Span.LastLine - 1;
        using MemoryStream output = new(bytes.Length);
        for (int i = 0; i < lines.Count; i++)
        {
            if (i < first || i > last)
            {
                output.Write(bytes, lines[i].Start, lines[i].Length);
            }
        }

        return output.ToArray();
    }

    /// <summary>The byte ranges of each line, each including its line ending (LF, CRLF or CR).</summary>
    private static List<(int Start, int Length)> SplitLines(byte[] bytes)
    {
        List<(int, int)> lines = [];
        int start = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == (byte)'\n' || (bytes[i] == (byte)'\r' && (i + 1 >= bytes.Length || bytes[i + 1] != (byte)'\n')))
            {
                lines.Add((start, i - start + 1));
                start = i + 1;
            }
        }

        if (start < bytes.Length)
        {
            lines.Add((start, bytes.Length - start));
        }

        return lines;
    }
}

/// <summary>One row the move writes.</summary>
/// <param name="LocalPath">The Local path cell: a plain path or <c>NAME: path</c>.</param>
/// <param name="OriginUrl">The Origin URL cell, or empty.</param>
/// <param name="LastUpdated">The Last updated cell (<c>yyyy-MM-dd</c>).</param>
/// <param name="Notes">The Notes cell.</param>
internal sealed record TemplateRow(string LocalPath, string OriginUrl, string LastUpdated, string Notes);

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
