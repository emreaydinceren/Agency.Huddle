using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.References;

/// <summary>
/// Builds the Referenced items header an Agent's Turn carries (spec §9.2). Every value is a JSON
/// string literal inside one dynamic fence under a line saying the lines are data (D-17).
/// </summary>
internal static class ReferencedItemsFormatter
{
    private static readonly JsonSerializerOptions QuoteOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Formats the header for the resolved references of one Message. Library files are skipped: the
    /// Library documents block lists them. Lines stop at the first one that would pass
    /// <paramref name="maxChars"/>, and the items left out are counted in the more line.
    /// </summary>
    /// <param name="items">The references after the resolver looked each one up.</param>
    /// <param name="totalCandidates">How many Task, Room, Team and gone references resolved before the cap; files are not counted.</param>
    /// <param name="prompts">Supplies the header, line, hint and more texts.</param>
    /// <param name="maxChars">The character cap on the item lines together.</param>
    /// <param name="listAgentsTool">The <c>list_agents</c> tool name as this session names it.</param>
    /// <param name="getTaskTool">The <c>get_task</c> tool name, or null when Tasks are not offered.</param>
    /// <returns>The header text, or null when there is no Task, Room, Team or Gone item.</returns>
    public static string? Format(
        IReadOnlyList<ResolvedReference> items,
        int totalCandidates,
        IPromptSource prompts,
        int maxChars,
        string listAgentsTool,
        string? getTaskTool)
    {
        List<ResolvedReference> listed = items.Where(static item => !ReferencedItemsFormatter.IsFile(item)).ToList();
        if (listed.Count == 0)
        {
            return null;
        }

        List<string> lines = [];
        int notWritten = 0;
        int running = 0;
        bool taskWritten = false;
        foreach (ResolvedReference item in listed)
        {

            string line = ReferencedItemsFormatter.LineFor(item, prompts, out bool isTask);
            if (notWritten == 0 && running + line.Length <= maxChars)
            {
                lines.Add(line);
                running += line.Length;
                taskWritten |= isTask;
            }
            else
            {
                notWritten++;
            }
        }

        int more = notWritten + Math.Max(0, totalCandidates - listed.Count);
        if (more > 0)
        {
            lines.Add(prompts.Render("turn.referencesMore", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["{{count}}"] = more.ToString(CultureInfo.InvariantCulture),
            }));
        }

        string fence = ReferencedItemsFormatter.ChooseFence(string.Join('\n', lines));
        string hint = prompts.Render("turn.referencesHint", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{listAgents}}"] = listAgentsTool,
        });
        if (taskWritten && getTaskTool is not null)
        {
            string taskHint = prompts.Render("turn.referencesTaskHint", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["{{getTask}}"] = getTaskTool,
            });
            hint = string.Concat(hint, " ", taskHint);
        }

        List<string> block = [prompts.Render("turn.referencesHeader", new Dictionary<string, string>(StringComparer.Ordinal)), fence];
        block.AddRange(lines);
        block.Add(fence);
        block.Add(hint);
        return string.Join('\n', block);
    }

    /// <summary>Whether an item is a Library file, resolved or gone: the Library documents block lists those, never this header (D-16).</summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True for a resolved or gone Library file.</returns>
    private static bool IsFile(ResolvedReference item) =>
        item is ResolvedLibraryFile or GoneReference { Kind: ReferenceKind.LibraryFile };

    /// <summary>Renders one item's line from its prompt, with every untrusted value quoted.</summary>
    /// <param name="item">The reference to describe; never a Library file.</param>
    /// <param name="prompts">Supplies the per-kind line prompt.</param>
    /// <param name="isTask">Set to true when the line is a Task line.</param>
    /// <returns>The line, without its newline.</returns>
    private static string LineFor(ResolvedReference item, IPromptSource prompts, out bool isTask)
    {
        isTask = false;
        switch (item)
        {
            case ResolvedTask task:
                isTask = true;
                return prompts.Render("turn.referenceTask", new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["{{id}}"] = task.Id.ToString(),
                    ["{{title}}"] = ReferencedItemsFormatter.Quote(task.Title),
                    ["{{status}}"] = ReferencedItemsFormatter.Quote(task.Status.ToWire()),
                    ["{{assignee}}"] = task.Assignee is null ? "none" : ReferencedItemsFormatter.Quote(task.Assignee),
                    ["{{team}}"] = ReferencedItemsFormatter.Quote(task.Team),
                });

            case ResolvedRoom room:
                return prompts.Render("turn.referenceRoom", new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["{{name}}"] = ReferencedItemsFormatter.Quote(room.Name),
                    ["{{id}}"] = room.Id,
                    ["{{scope}}"] = ReferencedItemsFormatter.RoomScope(room),
                    ["{{members}}"] = room.MemberCount.ToString(CultureInfo.InvariantCulture),
                });

            case ResolvedTeam team:
                return prompts.Render("turn.referenceTeam", new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["{{name}}"] = ReferencedItemsFormatter.Quote(team.Name),
                    ["{{type}}"] = team.TypeLabel is null ? "none" : ReferencedItemsFormatter.Quote(team.TypeLabel),
                    ["{{members}}"] = ReferencedItemsFormatter.Teammates(team.Members),
                    ["{{teamRoom}}"] = team.TeamRoomId ?? "none",
                });

            case GoneReference gone:
                return prompts.Render("turn.referenceGone", new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["{{kind}}"] = gone.Kind.ToString(),
                    ["{{key}}"] = ReferencedItemsFormatter.Quote(gone.Key),
                });

            default:
                throw new ArgumentException("Unexpected reference kind for a header line.", nameof(item));
        }
    }

    /// <summary>The scope of a Room as the header words it: <c>Team "X"</c>, <c>Team Room of "X"</c> or <c>root</c> (D-28).</summary>
    /// <param name="room">The Room to scope.</param>
    /// <returns>The scope text.</returns>
    private static string RoomScope(ResolvedRoom room) => room.Team switch
    {
        null => "root",
        string team when room.IsTeamRoom => string.Concat("Team Room of ", ReferencedItemsFormatter.Quote(team)),
        string team => string.Concat("Team ", ReferencedItemsFormatter.Quote(team)),
    };

    /// <summary>The Teammates of a Team: the first eight Names quoted and joined, then how many more there are.</summary>
    /// <param name="members">Every Teammate's Name.</param>
    /// <returns>The members text, or <c>none</c> when there are none.</returns>
    private static string Teammates(IReadOnlyList<string> members)
    {
        if (members.Count == 0)
        {
            return "none";
        }

        List<string> shown = members.Take(ReferenceLimits.MaxTeamMemberNames).ToList();
        string text = ReferencedItemsFormatter.Quote(string.Join(", ", shown));
        int more = members.Count - shown.Count;
        return more > 0
            ? string.Concat(text, " and ", more.ToString(CultureInfo.InvariantCulture), " more")
            : text;
    }

    /// <summary>Cuts a value to 120 text elements and writes it as a JSON string literal.</summary>
    /// <param name="value">The untrusted text.</param>
    /// <returns>The quoted, escaped text.</returns>
    private static string Quote(string value) =>
        JsonSerializer.Serialize(ReferencedItemsFormatter.Cut(value), ReferencedItemsFormatter.QuoteOptions);

    /// <summary>Cuts a value at a text-element boundary once it passes <see cref="ReferenceLimits.MaxValueChars"/>, ending in an ellipsis.</summary>
    /// <param name="value">The text to cut.</param>
    /// <returns>The value, or its first 120 text elements and an ellipsis.</returns>
    private static string Cut(string value)
    {
        StringInfo info = new(value);
        if (info.LengthInTextElements <= ReferenceLimits.MaxValueChars)
        {
            return value;
        }

        return string.Concat(info.SubstringByTextElements(0, ReferenceLimits.MaxValueChars), "…");
    }

    /// <summary>
    /// The fence for the item lines: three backticks, or one more than the longest backtick run in
    /// them, so no line can close it. The rule <c>RoomSession.ChooseFence</c> uses.
    /// </summary>
    /// <param name="text">The item lines, joined.</param>
    /// <returns>The fence.</returns>
    private static string ChooseFence(string text)
    {
        int longestRun = 0;
        int currentRun = 0;
        foreach (char c in text)
        {
            if (c == '`')
            {
                currentRun++;
                longestRun = Math.Max(longestRun, currentRun);
            }
            else
            {
                currentRun = 0;
            }
        }

        return new string('`', Math.Max(3, longestRun + 1));
    }
}
