using System.Globalization;
using System.Text;
using Agency.Huddle.SkillPacks.Checking;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Agency.Huddle.SkillPacks.Reading;

/// <summary>Reads SKILLS.md into a <see cref="Registry"/> (§8.1). Pure, and never throws for bad content.</summary>
public static class RegistryReader
{
    /// <summary>The registry file's name.</summary>
    public const string FileName = "SKILLS.md";

    /// <summary>The largest SKILLS.md that is parsed (R011).</summary>
    public const long MaxBytes = 1024 * 1024;

    private const string LocalPathColumn = "Local path";
    private const string OriginUrlColumn = "Origin URL";
    private const string LastUpdatedColumn = "Last updated";
    private const string NotesColumn = "Notes";

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UsePipeTables().Build();

    /// <summary>Loads SKILLS.md from <paramref name="registryDir"/>: a missing file is R001; one over 1 MB is R011 and
    /// its text is never read.</summary>
    public static Registry Load(IFileReader files, string registryDir, string computer)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(registryDir);
        ArgumentNullException.ThrowIfNull(computer);
        string path = Path.Combine(registryDir, FileName);
        try
        {
            if (!files.FileExists(path))
            {
                return Only(registryDir, Finding.Error("R001", path, $"{FileName} does not exist in {registryDir}."));
            }

            long length = files.Length(path);
            if (length > MaxBytes)
            {
                return Only(registryDir, Finding.Error("R011", path, string.Create(CultureInfo.InvariantCulture, $"{FileName} is {length:N0} bytes; the limit is {MaxBytes:N0}.")));
            }

            return Read(files.ReadAllText(path), registryDir, computer);
        }
        catch (IOException exception)
        {
            return Only(registryDir, Finding.Error("R001", path, $"cannot read: {exception.Message}"));
        }
        catch (UnauthorizedAccessException exception)
        {
            return Only(registryDir, Finding.Error("R001", path, $"cannot read: {exception.Message}"));
        }
    }

    /// <summary>Reads the text of SKILLS.md.</summary>
    /// <param name="text">The file's text.</param>
    /// <param name="registryDir">The folder SKILLS.md is in.</param>
    /// <param name="computer">This computer's name; kept for the signature's symmetry with <see cref="Load"/>, rows keep every entry.</param>
    public static Registry Read(string text, string registryDir, string computer)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(registryDir);
        ArgumentNullException.ThrowIfNull(computer);
        string file = Path.Combine(registryDir, FileName);
        if (Encoding.UTF8.GetByteCount(text) > MaxBytes)
        {
            return Only(registryDir, Finding.Error("R011", file, $"{FileName} is over 1 MB."));
        }

        if (text.Length > 0 && text[0] == '﻿')
        {
            text = text[1..];
        }

        List<Finding> findings = [];
        List<RowFinding> rowFindings = [];
        List<RegistryRow> packs = [];
        List<RegistryRow> catalogs = [];
        bool sawPacks = false;
        bool sawCatalogs = false;
        bool anyTable = false;

        MarkdownDocument document;
        try
        {
            document = Markdown.Parse(text, Pipeline);
        }
        catch (ArgumentException)
        {
            return Only(registryDir, Finding.Error("R001", file, $"{FileName} could not be read as Markdown."));
        }

        for (int i = 0; i < document.Count; i++)
        {
            if (document[i] is not HeadingBlock { Level: 2 } heading)
            {
                continue;
            }

            string title = Flatten(heading.Inline, keepUrl: false).Trim();
            RowKind? kind = KindOf(title);
            if (kind is null)
            {
                continue;
            }

            bool seen = kind == RowKind.Pack ? sawPacks : sawCatalogs;
            if (seen)
            {
                findings.Add(Finding.Warning("R012", file, $"A second {title} heading at line {heading.Line + 1}; its table is ignored."));
                continue;
            }

            if (kind == RowKind.Pack)
            {
                sawPacks = true;
            }
            else
            {
                sawCatalogs = true;
            }

            Table? table = FindTable(document, i + 1);
            if (table is null)
            {
                continue;
            }

            anyTable = true;
            List<RegistryRow> target = kind == RowKind.Pack ? packs : catalogs;
            ReadTable(table, kind.Value, file, target, findings, rowFindings);
        }

        if (!anyTable)
        {
            findings.Insert(0, Finding.Error("R001", file, $"{FileName} has no Packs or Catalogs table under a level-2 heading."));
        }

        return new Registry(registryDir, packs, catalogs, findings, rowFindings);
    }

    /// <summary>The table a level-2 heading introduces, or null for any other heading.</summary>
    private static RowKind? KindOf(string title)
    {
        if (title.Equals("Packs", StringComparison.OrdinalIgnoreCase))
        {
            return RowKind.Pack;
        }

        return title.Equals("Catalogs", StringComparison.OrdinalIgnoreCase) ? RowKind.Catalog : null;
    }

    /// <summary>A registry with no rows and one finding.</summary>
    private static Registry Only(string registryDir, Finding finding)
    {
        return new Registry(registryDir, [], [], [finding]);
    }

    /// <summary>The first table after the heading at <paramref name="start"/> and before the next level-1 or 2 heading.</summary>
    private static Table? FindTable(MarkdownDocument document, int start)
    {
        for (int j = start; j < document.Count; j++)
        {
            switch (document[j])
            {
                case HeadingBlock { Level: <= 2 }:
                    return null;
                case Table table:
                    return table;
            }
        }

        return null;
    }

    /// <summary>Reads one table's rows into <paramref name="rows"/>.</summary>
    private static void ReadTable(Table table, RowKind kind, string file, List<RegistryRow> rows, List<Finding> findings, List<RowFinding> rowFindings)
    {
        string tableName = kind == RowKind.Pack ? "Packs" : "Catalogs";
        TableRow? header = table.OfType<TableRow>().FirstOrDefault(static row => row.IsHeader);
        if (header is null)
        {
            findings.Add(Finding.Error("R002", file, $"The {tableName} table has no header row with a Local path column."));
            return;
        }

        int localPath = -1;
        int origin = -1;
        int lastUpdated = -1;
        int notes = -1;
        for (int c = 0; c < header.Count; c++)
        {
            string name = CellText(header[c], keepUrl: false).Trim();
            if (name.Equals(LocalPathColumn, StringComparison.OrdinalIgnoreCase))
            {
                localPath = localPath < 0 ? c : localPath;
            }
            else if (name.Equals(OriginUrlColumn, StringComparison.OrdinalIgnoreCase))
            {
                origin = origin < 0 ? c : origin;
            }
            else if (name.Equals(LastUpdatedColumn, StringComparison.OrdinalIgnoreCase))
            {
                lastUpdated = lastUpdated < 0 ? c : lastUpdated;
            }
            else if (name.Equals(NotesColumn, StringComparison.OrdinalIgnoreCase))
            {
                notes = notes < 0 ? c : notes;
            }
        }

        if (localPath < 0)
        {
            findings.Add(Finding.Error("R002", file, $"The {tableName} table has no Local path column."));
            return;
        }

        foreach (TableRow row in table.OfType<TableRow>().Where(static row => !row.IsHeader))
        {
            int index = rows.Count;
            int line = row.Line + 1;
            string pathCell = Cell(row, localPath, keepUrl: false) ?? string.Empty;
            PathEntries entries = PathEntries.Parse(pathCell);
            string? originUrl = Blank(Cell(row, origin, keepUrl: true));
            string? updatedText = Blank(Cell(row, lastUpdated, keepUrl: false));
            string? notesText = Blank(Cell(row, notes, keepUrl: false)?.Replace('\n', ' '));
            DateOnly? updated = null;
            string where = string.Create(CultureInfo.InvariantCulture, $"{tableName} row {index + 1}");
            int before = findings.Count;

            if (entries.IsEmpty)
            {
                findings.Add(Finding.Error("R003", file, $"{where}: the Local path is empty."));
            }

            if (entries.HasDuplicateComputer)
            {
                findings.Add(Finding.Error("R010", file, $"{where}: two entries name the same computer."));
            }

            if (entries.HasTwoPlainEntries)
            {
                findings.Add(Finding.Error("R013", file, $"{where}: two entries apply to every computer."));
            }

            if (updatedText is not null)
            {
                if (DateOnly.TryParseExact(updatedText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
                {
                    updated = date;
                }
                else
                {
                    findings.Add(Finding.Warning("R006", file, $"{where}: Last updated '{updatedText}' is not a yyyy-MM-dd date."));
                }
            }

            if (originUrl is not null && !IsOriginUrl(originUrl))
            {
                findings.Add(Finding.Warning("R007", file, $"{where}: Origin URL '{originUrl}' is not an absolute https, ssh or git URL."));
            }

            rowFindings.AddRange(findings.Skip(before).Select(finding => new RowFinding(kind, index, finding)));
            rows.Add(new RegistryRow(kind, index, entries, originUrl, updatedText, updated, notesText, new LineSpan(line, line)));
        }
    }

    /// <summary>Whether <paramref name="value"/> is an absolute https, ssh or git URL, or an scp-style git address.</summary>
    private static bool IsOriginUrl(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            return uri.Scheme is "https" or "ssh" or "git";
        }

        return value.StartsWith("git@", StringComparison.Ordinal) && value.Contains(':', StringComparison.Ordinal);
    }

    /// <summary>Null for null or white space; otherwise the trimmed text.</summary>
    private static string? Blank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>The flattened text of cell <paramref name="column"/>, or null when the row is shorter.</summary>
    private static string? Cell(TableRow row, int column, bool keepUrl)
    {
        if (column < 0 || column >= row.Count)
        {
            return null;
        }

        return CellText(row[column], keepUrl);
    }

    /// <summary>The plain text of one table cell, each line trimmed.</summary>
    private static string CellText(Block cell, bool keepUrl)
    {
        StringBuilder builder = new();
        if (cell is TableCell tableCell)
        {
            foreach (Block block in tableCell)
            {
                if (block is LeafBlock { Inline: not null } leaf)
                {
                    builder.Append(Flatten(leaf.Inline, keepUrl));
                }
            }
        }

        return string.Join('\n', builder.ToString().Split('\n').Select(static line => line.Trim()));
    }

    /// <summary>Flattens inlines to text: code spans give their literal text, links their URL when <paramref name="keepUrl"/>,
    /// and each <c>&lt;br&gt;</c> is a line break.</summary>
    private static string Flatten(ContainerInline? container, bool keepUrl)
    {
        if (container is null)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        foreach (Inline inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    builder.Append(literal.Content.ToString());
                    break;
                case CodeInline code:
                    builder.Append(code.Content);
                    break;
                case AutolinkInline autolink:
                    builder.Append(autolink.Url);
                    break;
                case LinkInline link when keepUrl && !string.IsNullOrEmpty(link.Url):
                    builder.Append(link.Url);
                    break;
                case HtmlInline html when IsBreak(html.Tag):
                    builder.Append('\n');
                    break;
                case HtmlInline html:
                    builder.Append(html.Tag);
                    break;
                case LineBreakInline:
                    builder.Append('\n');
                    break;
                case HtmlEntityInline entity:
                    builder.Append(entity.Transcoded.ToString());
                    break;
                case ContainerInline nested:
                    builder.Append(Flatten(nested, keepUrl));
                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>Whether an HTML tag is <c>&lt;br&gt;</c>, <c>&lt;br/&gt;</c> or <c>&lt;br /&gt;</c>.</summary>
    private static bool IsBreak(string? tag)
    {
        if (tag is null)
        {
            return false;
        }

        string compact = tag.Replace(" ", string.Empty, StringComparison.Ordinal);
        return compact.Equals("<br>", StringComparison.OrdinalIgnoreCase) || compact.Equals("<br/>", StringComparison.OrdinalIgnoreCase);
    }
}
