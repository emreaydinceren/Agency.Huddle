using System.Text;
using System.Text.RegularExpressions;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Step-6 rewrite of a Team rename for Task files (Spec R5): another Team's Task whose <c>from_team</c> names the old Team,
/// or whose body holds an attachment path under <c>Teams/&lt;Old&gt;/</c>, is rewritten on disk and gains one change-log
/// entry, so the next start does not take the edit for an outside edit. The Teams are paused while this runs.
/// </summary>
/// <param name="options">The Team options: the data directory and the Teams folder name.</param>
/// <param name="timeProvider">Stamps the change-log entry; the system clock when null.</param>
internal sealed class TaskFileRenameRewrite(IOptions<TeamOptions> options, TimeProvider? timeProvider = null) : ITeamRenameRewrite
{
    private const string Actor = "Huddle";
    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

    private readonly string root = Path.GetFullPath(Path.Combine(options.Value.DataDir, options.Value.Teams.Dir));
    private readonly string teamsSegment = TeamsSegmentOf(options.Value.Teams.Dir);
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    /// <inheritdoc />
    public IReadOnlyList<string> TeamsTouched(TeamRename rename)
    {
        ArgumentNullException.ThrowIfNull(rename);

        List<string> teams = [];
        foreach (string file in this.TaskFiles(rename))
        {
            string text = File.ReadAllText(file);
            if (this.Rewrite(text, rename) is not null)
            {
                string team = this.TeamOf(file);
                if (!teams.Contains(team, StringComparer.OrdinalIgnoreCase))
                {
                    teams.Add(team);
                }
            }
        }

        return teams;
    }

    /// <inheritdoc />
    public async Task RunAsync(TeamRename rename, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rename);

        foreach (string file in this.TaskFiles(rename))
        {
            ct.ThrowIfCancellationRequested();
            string text = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
            string? rewritten = this.Rewrite(text, rename);
            if (rewritten is null)
            {
                continue;
            }

            string logged = TaskFileFormat.AppendEntry(
                rewritten,
                new ChangeLogEntry(this.clock.GetUtcNow(), Actor, $"{(rename.Kind == TeamRenameKind.Project ? "Project" : "Team")} renamed from {rename.OldName} to {rename.NewName}"));
            if (text.Contains("\r\n", StringComparison.Ordinal))
            {
                logged = logged.Replace("\n", "\r\n", StringComparison.Ordinal);
            }

            if (!await AtomicFile.TryWriteAsync(file, Utf8NoBom.GetBytes(logged), ct).ConfigureAwait(false))
            {
                throw new IOException($"The Task file \"{Path.GetFileName(file)}\" is open in another program.");
            }
        }
    }

    /// <summary>The folder name <paramref name="dir"/> ends in.</summary>
    /// <param name="dir">The configured Teams folder.</param>
    private static string TeamsSegmentOf(string dir) => dir.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries) is [.., string last] ? last : "Teams";

    /// <summary>The Task file's index of the change-log heading line, or the text length when there is none.</summary>
    /// <param name="text">The file text.</param>
    private static int ChangeLogStart(string text)
    {
        int index = text.IndexOf("\n" + TaskFileFormat.ChangeLogHeading, StringComparison.Ordinal);
        return index < 0 ? text.Length : index;
    }

    /// <summary>The end index of a leading frontmatter block, or 0 when the text has none.</summary>
    /// <param name="text">The file text.</param>
    private static int FrontmatterEnd(string text)
    {
        if (!text.StartsWith("---", StringComparison.Ordinal))
        {
            return 0;
        }

        int close = text.IndexOf("\n---", 3, StringComparison.Ordinal);
        return close < 0 ? 0 : close;
    }

    /// <summary>The Task files under the Teams folder, except those inside the moved folder (a Team's own, or the renamed Project's): they moved with it.</summary>
    /// <param name="rename">The rename whose new name is the moved folder.</param>
    private List<string> TaskFiles(TeamRename rename) => Directory.Exists(this.root)
        ? [.. Directory.EnumerateFiles(this.root, "*.md", SearchOption.AllDirectories)
            .Where(file => TaskLayout.AffectsTasks(this.root, file) && !this.InMovedFolder(file, rename))]
        : [];

    /// <summary>Whether the file sits in the folder the rename moved: the Team's folder, or the Project's folder inside its Team.</summary>
    /// <param name="file">The file path.</param>
    /// <param name="rename">The rename.</param>
    private bool InMovedFolder(string file, TeamRename rename)
    {
        string[] segments = Path.GetRelativePath(this.root, file).Split(['/', '\\']);
        return rename.Kind == TeamRenameKind.Project
            ? segments.Length > 2
                && string.Equals(segments[0], rename.Team, StringComparison.OrdinalIgnoreCase)
                && string.Equals(segments[1], rename.NewName, StringComparison.OrdinalIgnoreCase)
            : string.Equals(segments[0], rename.NewName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The Team folder a Task file sits in.</summary>
    /// <param name="file">The file path.</param>
    private string TeamOf(string file) => Path.GetRelativePath(this.root, file).Split(['/', '\\'], 2)[0];

    /// <summary>The text with the old Team replaced by the new one, or null when nothing changes.</summary>
    /// <param name="text">The file text.</param>
    /// <param name="rename">The rename.</param>
    private string? Rewrite(string text, TeamRename rename)
    {
        string newName = rename.NewName;
        int frontEnd = FrontmatterEnd(text);
        int bodyEnd = ChangeLogStart(text);
        bool changed = false;

        Regex fromTeam = new(@"^(from_team:[ \t]*)(.*?)([ \t]*)(\r?)$", RegexOptions.Multiline | RegexOptions.CultureInvariant, RegexTimeout);
        string head = text[..frontEnd];
        head = fromTeam.Replace(head, match =>
        {
            string value = match.Groups[2].Value;
            if (rename.Kind == TeamRenameKind.Project || !string.Equals(value, rename.OldName, StringComparison.OrdinalIgnoreCase) || string.Equals(value, newName, StringComparison.Ordinal))
            {
                return match.Value;
            }

            changed = true;
            return match.Groups[1].Value + newName + match.Groups[3].Value + match.Groups[4].Value;
        });

        string teamSegment = rename.Kind == TeamRenameKind.Project ? $@"{Regex.Escape(rename.Team)}[\\/]" : string.Empty;
        Regex path = new(
            $@"(?<=[\\/]{Regex.Escape(this.teamsSegment)}[\\/]{teamSegment}){Regex.Escape(rename.OldName)}(?=[\\/])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            RegexTimeout);
        string body = path.Replace(
            text[frontEnd..Math.Max(frontEnd, bodyEnd)],
            match =>
            {
                if (string.Equals(match.Value, newName, StringComparison.Ordinal))
                {
                    return match.Value;
                }

                changed = true;
                return newName;
            });

        return changed ? head + body + text[Math.Max(frontEnd, bodyEnd)..] : null;
    }
}
