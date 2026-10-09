using System.Globalization;
using System.Text;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Teams;

/// <summary>The status of a retro register row (RT-6, Spec §Retro).</summary>
internal enum RetroStatus
{
    /// <summary>Found, nothing changed yet.</summary>
    Open,

    /// <summary>The change was made; not yet checked against later evidence.</summary>
    Done,

    /// <summary>The change only partly worked.</summary>
    Partial,

    /// <summary>Later evidence shows the change worked.</summary>
    Verified,

    /// <summary>The change was replaced by another one.</summary>
    Changed,
}

/// <summary>One row of <c>Retro register.md</c>.</summary>
/// <param name="Id">The row id, <c>R-001</c>, <c>R-002</c>, and so on.</param>
/// <param name="Finding">What the retro found.</param>
/// <param name="Action">What was decided to do about it.</param>
/// <param name="Owner">Who owns the action.</param>
/// <param name="Status">Where the row stands.</param>
/// <param name="EvidenceNextCheck">The evidence so far, or what to check at the next retro.</param>
internal sealed record RegisterRow(string Id, string Finding, string Action, string Owner, RetroStatus Status, string EvidenceNextCheck);

/// <summary>Reads and writes the markdown table of <c>Retro register.md</c>.</summary>
internal static class RetroRegister
{
    private const string HeaderLine = "| ID | Finding | Action | Owner | Status | Evidence / next check |";

    private const string SeparatorLine = "| --- | --- | --- | --- | --- | --- |";

    private const string IdPrefix = "R-";

    private const int ColumnCount = 6;

    /// <summary>Reads the rows of a register. Text outside the table, the header and rows with a status outside the five words are ignored.</summary>
    /// <param name="markdown">The register file's text.</param>
    /// <returns>The rows, in file order.</returns>
    public static IReadOnlyList<RegisterRow> Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        List<RegisterRow> rows = [];
        foreach (string line in markdown.Split('\n'))
        {
            if (TryParseRow(line, out RegisterRow? row))
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    /// <summary>Writes the header, the separator and one line per row.</summary>
    /// <param name="rows">The rows to write.</param>
    /// <returns>The table text, every line ending in a line feed.</returns>
    public static string Render(IReadOnlyList<RegisterRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        StringBuilder text = new();
        text.Append(HeaderLine).Append('\n').Append(SeparatorLine).Append('\n');
        foreach (RegisterRow row in rows)
        {
            text.Append(RenderRow(row)).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>Gives the next free id: one more than the highest <c>R-nnn</c> number.</summary>
    /// <param name="rows">The existing rows.</param>
    /// <returns>The new id, <c>R-001</c> when there are no numbered rows.</returns>
    public static string NextId(IReadOnlyList<RegisterRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        int highest = 0;
        foreach (RegisterRow row in rows)
        {
            if (row.Id.StartsWith(IdPrefix, StringComparison.Ordinal)
                && int.TryParse(row.Id.AsSpan(IdPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int number)
                && number > highest)
            {
                highest = number;
            }
        }

        return IdPrefix + (highest + 1).ToString("D3", CultureInfo.InvariantCulture);
    }

    /// <summary>Writes one row as a table line.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The line, without a line ending.</returns>
    internal static string RenderRow(RegisterRow row)
    {
        return string.Join(
            ' ',
            "|",
            Escape(row.Id),
            "|",
            Escape(row.Finding),
            "|",
            Escape(row.Action),
            "|",
            Escape(row.Owner),
            "|",
            row.Status.ToString(),
            "|",
            Escape(row.EvidenceNextCheck),
            "|");
    }

    /// <summary>Whether a line belongs to the table (it starts with a pipe).</summary>
    /// <param name="line">One line of the file.</param>
    /// <returns><see langword="true"/> for the header, separator and row lines.</returns>
    internal static bool IsTableLine(string line) => line.TrimStart().StartsWith('|');

    /// <summary>Reads one table line as a row.</summary>
    /// <param name="line">One line of the file.</param>
    /// <param name="row">The row, when the line is one.</param>
    /// <returns><see langword="false"/> for text, the header, the separator and a status outside the five words.</returns>
    internal static bool TryParseRow(string line, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out RegisterRow? row)
    {
        row = null;
        string trimmed = line.Trim();
        if (!trimmed.StartsWith('|') || trimmed.Length < 2)
        {
            return false;
        }

        List<string> cells = SplitCells(trimmed);
        if (cells.Count != ColumnCount || !TryParseStatus(cells[4], out RetroStatus status))
        {
            return false;
        }

        row = new RegisterRow(cells[0], cells[1], cells[2], cells[3], status, cells[5]);
        return true;
    }

    private static bool TryParseStatus(string word, out RetroStatus status)
    {
        foreach (RetroStatus candidate in Enum.GetValues<RetroStatus>())
        {
            if (string.Equals(candidate.ToString(), word, StringComparison.OrdinalIgnoreCase))
            {
                status = candidate;
                return true;
            }
        }

        status = RetroStatus.Open;
        return false;
    }

    private static string Escape(string cell)
    {
        return cell.Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\n', ' ')
            .Replace('\r', ' ')
            .Replace("|", "\\|", StringComparison.Ordinal);
    }

    private static List<string> SplitCells(string trimmedLine)
    {
        List<string> cells = [];
        StringBuilder cell = new();
        int end = trimmedLine.Length;
        if (trimmedLine[end - 1] == '|' && (end < 2 || trimmedLine[end - 2] != '\\'))
        {
            end--;
        }

        bool escaped = false;
        foreach (char c in trimmedLine.AsSpan(1, end - 1))
        {
            if (escaped)
            {
                cell.Append(c == '|' ? "|" : "\\" + c);
                escaped = false;
            }
            else if (c == '\\')
            {
                escaped = true;
            }
            else if (c == '|')
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
            }
            else
            {
                cell.Append(c);
            }
        }

        if (escaped)
        {
            cell.Append('\\');
        }

        cells.Add(cell.ToString().Trim());
        return cells;
    }
}

/// <summary>One of the top issues of a retro report.</summary>
/// <param name="Title">A short name for the issue.</param>
/// <param name="Detail">What went wrong, in a sentence or two.</param>
/// <param name="EvidenceLinks">Task ids, log references or report files that show it.</param>
internal sealed record RetroIssue(string Title, string Detail, IReadOnlyList<string> EvidenceLinks);

/// <summary>One proposed change of a retro report.</summary>
/// <param name="Id">The proposal's id within the report.</param>
/// <param name="Title">A short name for the change.</param>
/// <param name="TargetFile">The file the change edits.</param>
/// <param name="Diff">The change as unified diff text.</param>
internal sealed record RetroProposal(string Id, string Title, string TargetFile, string Diff);

/// <summary>A retro report: the top issues and the proposed changes (RT-5).</summary>
/// <param name="Date">The day of the retro.</param>
/// <param name="TopIssues">At most three issues.</param>
/// <param name="Proposals">At most three proposed changes.</param>
internal sealed record RetroReport(DateOnly Date, IReadOnlyList<RetroIssue> TopIssues, IReadOnlyList<RetroProposal> Proposals);

/// <summary>Reads and writes a Team's <c>_retros/&lt;date&gt;.md</c> reports and its <c>Retro register.md</c>.</summary>
/// <param name="folders">Finds the Team folder.</param>
internal sealed class RetroFileStore(ITeamFolders folders)
{
    private const int MaxListed = 3;

    /// <summary>Serialises every register write in the process, whichever store instance makes it.</summary>
    private static readonly Lock RegisterGate = new();

    /// <summary>Writes a report to <c>_retros/&lt;date&gt;.md</c>, or <c>&lt;date&gt;-2.md</c> and so on when that name is taken. An existing report is never overwritten.</summary>
    /// <param name="team">The Team's name.</param>
    /// <param name="report">The report.</param>
    /// <returns>The full path of the file written.</returns>
    /// <exception cref="InvalidOperationException">The report lists more than three issues or proposals, or the Team has no folder.</exception>
    public string WriteReport(string team, RetroReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.TopIssues.Count > MaxListed)
        {
            throw new InvalidOperationException("A retro report has at most 3 top issues.");
        }

        if (report.Proposals.Count > MaxListed)
        {
            throw new InvalidOperationException("A retro report has at most 3 proposed changes.");
        }

        string directory = Path.Combine(this.TeamFolder(team), TeamPaths.RetrosDir);
        Directory.CreateDirectory(directory);
        string stem = report.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string text = RenderReport(report);
        for (int attempt = 1; ; attempt++)
        {
            string name = attempt == 1 ? stem + ".md" : stem + "-" + attempt.ToString(CultureInfo.InvariantCulture) + ".md";
            string path = Path.Combine(directory, name);
            if (File.Exists(path))
            {
                continue;
            }

            using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write);
            using StreamWriter writer = new(stream, new UTF8Encoding(false));
            writer.Write(text);
            return path;
        }
    }

    /// <summary>Reads the Team's register.</summary>
    /// <param name="team">The Team's name.</param>
    /// <returns>The rows, empty when the Team has no register yet.</returns>
    public IReadOnlyList<RegisterRow> ReadRegister(string team)
    {
        string path = this.RegisterPath(team);
        return File.Exists(path) ? RetroRegister.Parse(File.ReadAllText(path)) : [];
    }

    /// <summary>Adds rows under the register table, creating the register when the Team has none. Every other line of the file is kept.</summary>
    /// <param name="team">The Team's name.</param>
    /// <param name="rows">The rows to add.</param>
    public void AppendRows(string team, IReadOnlyList<RegisterRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        string path = this.RegisterPath(team);
        lock (RegisterGate)
        {
            if (!File.Exists(path))
            {
                WriteAtomic(path, "# Retro register\n\n" + RetroRegister.Render(rows));
                return;
            }

            string text = File.ReadAllText(path);
            string newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            List<string> lines = [.. text.Split(newline)];
            int last = lines.FindLastIndex(static line => RetroRegister.IsTableLine(line));
            if (last < 0)
            {
                string body = RetroRegister.Render(rows).Replace("\n", newline, StringComparison.Ordinal);
                WriteAtomic(path, text.TrimEnd('\r', '\n') + newline + newline + body);
                return;
            }

            lines.InsertRange(last + 1, rows.Select(static row => RetroRegister.RenderRow(row)));
            WriteAtomic(path, string.Join(newline, lines));
        }
    }

    /// <summary>Sets one row's status and evidence; every other line of the file stays as it was.</summary>
    /// <param name="team">The Team's name.</param>
    /// <param name="rowId">The row to change.</param>
    /// <param name="status">The new status.</param>
    /// <param name="evidence">The new text for the Evidence / next check column.</param>
    /// <exception cref="InvalidOperationException">The register has no such row.</exception>
    public void SetStatus(string team, string rowId, RetroStatus status, string evidence)
    {
        ArgumentNullException.ThrowIfNull(rowId);
        ArgumentNullException.ThrowIfNull(evidence);
        string path = this.RegisterPath(team);
        lock (RegisterGate)
        {
            string text = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            string newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            string[] lines = text.Split(newline);
            for (int i = 0; i < lines.Length; i++)
            {
                if (RetroRegister.TryParseRow(lines[i], out RegisterRow? row) && string.Equals(row.Id, rowId, StringComparison.Ordinal))
                {
                    lines[i] = RetroRegister.RenderRow(row with { Status = status, EvidenceNextCheck = evidence });
                    WriteAtomic(path, string.Join(newline, lines));
                    return;
                }
            }
        }

        throw new InvalidOperationException($"The retro register has no row {rowId}.");
    }

    private static string RenderReport(RetroReport report)
    {
        StringBuilder text = new();
        text.Append("# Retro ").Append(report.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append("\n\n");
        text.Append("## Top issues\n\n");
        int number = 0;
        foreach (RetroIssue issue in report.TopIssues)
        {
            number++;
            text.Append("### ").Append(number.ToString(CultureInfo.InvariantCulture)).Append(". ").Append(issue.Title).Append("\n\n");
            text.Append(issue.Detail).Append("\n\n");
            foreach (string link in issue.EvidenceLinks)
            {
                text.Append("- ").Append(link).Append('\n');
            }

            if (issue.EvidenceLinks.Count > 0)
            {
                text.Append('\n');
            }
        }

        text.Append("## Proposed changes\n\n");
        foreach (RetroProposal proposal in report.Proposals)
        {
            string fence = proposal.Diff.Contains("```", StringComparison.Ordinal) ? "````" : "```";
            text.Append("### ").Append(proposal.Id).Append(". ").Append(proposal.Title).Append("\n\n");
            text.Append("Target: `").Append(proposal.TargetFile).Append("`\n\n");
            text.Append(fence).Append("diff\n").Append(proposal.Diff.TrimEnd('\r', '\n')).Append('\n').Append(fence).Append("\n\n");
        }

        return text.ToString().TrimEnd('\n') + "\n";
    }

    private static void WriteAtomic(string path, string text)
    {
        string temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temp, text, new UTF8Encoding(false));
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    private string TeamFolder(string team)
    {
        LibraryResult<LibraryPath> folder = folders.FindTeam(team);
        if (folder.Value is null)
        {
            throw new InvalidOperationException(folder.Error);
        }

        return folder.Value.FullPath;
    }

    private string RegisterPath(string team) => Path.Combine(this.TeamFolder(team), TeamPaths.RetroRegisterFileName);
}
