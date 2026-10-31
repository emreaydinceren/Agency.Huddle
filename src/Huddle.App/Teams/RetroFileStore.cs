using Agency.Huddle.App.Library;
using System.Globalization;
using System.Text;

namespace Agency.Huddle.App.Teams;

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
                AtomicFile.WriteText(path, "# Retro register\n\n" + RetroRegister.Render(rows));
                return;
            }

            string text = File.ReadAllText(path);
            string newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            List<string> lines = [.. text.Split(newline)];
            int last = lines.FindLastIndex(static line => RetroRegister.IsTableLine(line));
            if (last < 0)
            {
                string body = RetroRegister.Render(rows).Replace("\n", newline, StringComparison.Ordinal);
                AtomicFile.WriteText(path, text.TrimEnd('\r', '\n') + newline + newline + body);
                return;
            }

            lines.InsertRange(last + 1, rows.Select(static row => RetroRegister.RenderRow(row)));
            AtomicFile.WriteText(path, string.Join(newline, lines));
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
                    AtomicFile.WriteText(path, string.Join(newline, lines));
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
