using System.Globalization;
using System.Text;

namespace Agency.Huddle.App.Teams;

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
#pragma warning disable HAA0102 // Enum.ToString returns the runtime's cached name, so a lookup table would save nothing.
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
#pragma warning restore HAA0102
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
#pragma warning disable HAA0102 // Enum.ToString returns the runtime's cached name, so a lookup table would save nothing.
            if (string.Equals(candidate.ToString(), word, StringComparison.OrdinalIgnoreCase))
            {
                status = candidate;
                return true;
            }
#pragma warning restore HAA0102
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
