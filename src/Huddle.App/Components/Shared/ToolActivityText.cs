using System.Text.RegularExpressions;

namespace Agency.Huddle.App.Components.Shared;

/// <summary>
/// The words a Turn detail row shows for a tool call. An Adapter reports whatever it likes as the call's
/// title: Huddle's own tool under its model-facing id (<c>mcp__team__ask_human</c>), a path from the data
/// folder, a pattern in Markdown backticks. The Human should read none of that raw, so this turns the
/// title into plain words and keeps the original for the row's tooltip.
/// </summary>
internal static partial class ToolActivityText
{
    private const string TeamToolPrefix = "mcp__team__";

    /// <summary>What each of Huddle's own tools says when the Agent calls it, keyed by the name after the prefix.</summary>
    private static readonly Dictionary<string, string> TeamToolWords = new(StringComparer.Ordinal)
    {
        ["ask_human"] = "Asking you",
        ["create_task"] = "Creating a Task",
        ["update_task"] = "Updating a Task",
        ["get_task"] = "Reading a Task",
        ["list_tasks"] = "Listing Tasks",
        ["next_task"] = "Finding the next Task",
        ["close_task"] = "Closing a Task",
        ["reopen_task"] = "Reopening a Task",
        ["post_message"] = "Posting a Message",
        ["list_agents"] = "Looking up Agents",
        ["list_teams"] = "Looking up Teams",
        ["invite_agent"] = "Inviting an Agent",
        ["create_room"] = "Creating a Room",
        ["follow_room"] = "Following a Room",
        ["unfollow_room"] = "Unfollowing a Room",
        ["get_help"] = "Reading the help",
        ["read_skill"] = "Reading a Skill",
        ["memory"] = "Using its memory",
        ["search"] = "Searching",
        ["jobs"] = "Working with Scheduled jobs",
        ["file_request"] = "Filing a Request",
        ["triage_request"] = "Triaging a Request",
        ["confirm_request"] = "Confirming a Request",
        ["watch_folder"] = "Watching a folder",
        ["unwatch_folder"] = "Unwatching a folder",
        ["read_records"] = "Reading records",
        ["log_change"] = "Logging a change",
        ["add_evidence"] = "Adding evidence",
        ["add_comment"] = "Adding a comment",
        ["validate_agent"] = "Checking an Agent",
        ["propose_agents"] = "Proposing Agents",
        ["propose_sprint"] = "Proposing a Sprint",
        ["propose_team_memory"] = "Proposing a Team memory",
        ["propose_retro_changes"] = "Proposing retro changes",
        ["mark_retro_row"] = "Marking a retro row",
    };

    /// <summary>An absolute path inside a sentence: a drive path, or a POSIX path with at least two segments.</summary>
    [GeneratedRegex(@"[A-Za-z]:[\\/][^\s`""']*|(?<![\w.:/\\])/(?:[\w.\-]+/)+[\w.\-]*", RegexOptions.CultureInvariant)]
    private static partial Regex AbsolutePath();

    /// <summary>What a row says: the Adapter's title in plain words, else the file name of its path, else a plain fallback.</summary>
    /// <param name="title">The Adapter's title for the call, or <see langword="null"/> if it never gave one.</param>
    /// <param name="path">The file the call touches, or <see langword="null"/> when not known.</param>
    /// <returns>The row text.</returns>
    internal static string Row(string? title, string? path)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return MessageDisplay.FileName(path) ?? "Tool call";
        }

        string trimmed = title.Trim();
        if (trimmed.StartsWith(TeamToolPrefix, StringComparison.Ordinal))
        {
            return ToolActivityText.TeamTool(trimmed[TeamToolPrefix.Length..]);
        }

        string plain = AbsolutePath().Replace(trimmed.Replace("`", string.Empty, StringComparison.Ordinal), match => ToolActivityText.PathName(match.Value));
        return plain.Length > 0 ? plain : "Tool call";
    }

    /// <summary>The tooltip of a row: the full path when the call has one, else the raw title when the row shows it differently.</summary>
    /// <param name="title">The Adapter's title for the call, or <see langword="null"/>.</param>
    /// <param name="path">The file the call touches, or <see langword="null"/>.</param>
    /// <returns>The tooltip, or <see langword="null"/> when the row already says everything.</returns>
    internal static string? Tooltip(string? title, string? path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            return path;
        }

        return !string.IsNullOrWhiteSpace(title) && !string.Equals(ToolActivityText.Row(title, null), title.Trim(), StringComparison.Ordinal)
            ? title.Trim()
            : null;
    }

    /// <summary>The words for one of Huddle's own tools; a tool with no entry gets its name spaced out.</summary>
    /// <param name="name">The tool name after the <c>mcp__team__</c> prefix.</param>
    private static string TeamTool(string name)
    {
        if (TeamToolWords.TryGetValue(name, out string? words))
        {
            return words;
        }

        string spaced = name.Replace('_', ' ');
        return spaced.Length == 0 ? "Tool call" : char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }

    /// <summary>The last segment of a matched path, ignoring a trailing separator.</summary>
    /// <param name="path">The matched path.</param>
    private static string PathName(string path) => MessageDisplay.FileName(path.TrimEnd('\\', '/')) ?? path;
}
