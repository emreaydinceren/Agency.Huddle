using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Acp.Tools;

/// <summary>
/// Gives the Retro agent a Team's evidence (RT-3): the compact records bundle, one Task's Change log and Evidence, or the tail of
/// a Room transcript. The caller must hold the <c>retrospective</c> Skill and be the Team's manager or its Retro agent, and
/// every read is scoped to the named Team: a Task of another Team, or a Room in which no member of the Team has posted, is
/// refused. Output is capped at <see cref="MaxCharacters"/> characters per call. Offered only to a Persona holding a Skill that
/// lists it.
/// </summary>
internal sealed class ReadRecordsTool(
    ITeamCatalog teams,
    TaskStore store,
    PersonaStore personas,
    TeamTypeCatalog types,
    RecordsBundleBuilder builder,
    IChatStore chat,
    ITeamFolders folders,
    ITeamDirectory directory,
    TimeProvider clock,
    string callerAgentId,
    IPromptSource prompts) : IAppTool
{
    /// <summary>The most characters one call returns.</summary>
    private const int MaxCharacters = 12000;

    /// <summary>The most Room messages one call returns.</summary>
    private const int MaxMessages = 200;

    /// <summary>The most characters of one message that are returned.</summary>
    private const int MaxMessageCharacters = 300;

    /// <summary>The window used when the Team has no report yet.</summary>
    private const int DefaultWindowDays = 30;

    private const string DateFormat = "yyyy-MM-dd";

    private const string MinuteFormat = "yyyy-MM-dd HH:mm";

    private const string TruncatedNote = "\n... truncated";

    /// <inheritdoc />
    public string Name => "read_records";

    /// <summary>The placeholder values a prompt renders with; the tool descriptions take none.</summary>
    private static readonly IReadOnlyDictionary<string, string> NoValues = new Dictionary<string, string>();

    /// <inheritdoc />
    public string Description => prompts.Render("tool.readRecords.description", NoValues);

    /// <inheritdoc />
    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["team"] = new JsonObject { ["type"] = "string" },
            ["since"] = new JsonObject { ["type"] = "string" },
            ["room"] = new JsonObject { ["type"] = "string" },
            ["taskId"] = new JsonObject { ["type"] = "string" },
        },
        ["required"] = new JsonArray { "team" },
    };

    /// <inheritdoc />
    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        (TaskActor? actor, string actorRefusal) = await TaskToolText.ResolveActorAsync(directory, callerAgentId, cancellationToken);
        if (actor is null)
        {
            return actorRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "team", out string? teamText, out string teamRefusal))
        {
            return teamRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "since", out string? sinceText, out string sinceRefusal))
        {
            return sinceRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "room", out string? roomText, out string roomRefusal))
        {
            return roomRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "taskId", out string? taskIdText, out string taskIdRefusal))
        {
            return taskIdRefusal;
        }

        if (string.IsNullOrWhiteSpace(teamText))
        {
            return "team is required.";
        }

        TeamSummary? team = teams.Find(teamText);
        if (team is null)
        {
            return $"There is no Team named {teamText}.";
        }

        string? refusal = this.CheckCaller(team, actor.Name);
        if (refusal is not null)
        {
            return refusal;
        }

        if (!string.IsNullOrWhiteSpace(taskIdText))
        {
            return this.ReadTask(team, taskIdText);
        }

        if (!string.IsNullOrWhiteSpace(roomText))
        {
            return await this.ReadRoomAsync(team, roomText, cancellationToken);
        }

        DateTimeOffset since;
        if (string.IsNullOrWhiteSpace(sinceText))
        {
            since = this.DefaultSince(team.Name);
        }
        else if (DateOnly.TryParseExact(sinceText.Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
        {
            since = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        }
        else
        {
            return "since must be a date like 2026-10-01.";
        }

        IReadOnlyList<RegisterRow> register;
        try
        {
            register = new RetroFileStore(folders).ReadRegister(team.Name);
        }
        catch (IOException)
        {
            return "The retro register could not be read right now; try again.";
        }

        return RecordsBundleBuilder.RenderText(builder.Build(team.Name, since), MaxCharacters, register.Count > 0 ? RetroVerifier.Due(register) : null);
    }

    /// <summary>Keeps the newest lines whose joined length fits <paramref name="limit"/>.</summary>
    /// <param name="lines">The lines, oldest first.</param>
    /// <param name="limit">The length cap.</param>
    /// <returns>The newest lines that fit, oldest first.</returns>
    private static string JoinNewest(List<string> lines, int limit)
    {
        int total = 0;
        int first = lines.Count;
        while (first > 0)
        {
            int next = total + lines[first - 1].Length + (total > 0 ? 1 : 0);
            if (next > limit)
            {
                break;
            }

            total = next;
            first--;
        }

        return string.Join('\n', lines.Skip(first));
    }

    /// <summary>Cuts <paramref name="text"/> to the output cap, marking the cut.</summary>
    /// <param name="text">The text to cap.</param>
    /// <returns>The text, or its start followed by a truncation note.</returns>
    private static string Cap(string text) =>
        text.Length <= MaxCharacters ? text : string.Concat(text.AsSpan(0, MaxCharacters - TruncatedNote.Length), TruncatedNote);

    /// <summary>Checks the caller belongs to the Team, holds the Skill and is its manager or Retro agent.</summary>
    /// <param name="team">The named Team.</param>
    /// <param name="caller">The caller's name.</param>
    /// <returns>The refusal, or <see langword="null"/> when the caller may read.</returns>
    private string? CheckCaller(TeamSummary team, string caller) =>
        RetroCaller.Check(personas, types, team, caller, "Only the Team's manager or its Retro agent may read its records.");

    /// <summary>The start of the window: the date of the newest report in the Team's <c>_retros</c> folder, else thirty days back.</summary>
    /// <param name="team">The Team name.</param>
    /// <returns>The cutoff.</returns>
    private DateTimeOffset DefaultSince(string team)
    {
        DateTimeOffset latest = clock.GetUtcNow().AddDays(-DefaultWindowDays);
        LibraryResult<LibraryPath> folder = folders.FindTeam(team);
        if (folder.Value is not { } path)
        {
            return latest;
        }

        string retros = Path.Combine(path.FullPath, TeamPaths.RetrosDir);
        if (!Directory.Exists(retros))
        {
            return latest;
        }

        DateTimeOffset? newest = null;
        foreach (string file in Directory.GetFiles(retros, "*.md"))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (name.Length >= DateFormat.Length
                && DateOnly.TryParseExact(name[..DateFormat.Length], DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
            {
                DateTimeOffset at = new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                if (newest is null || at > newest)
                {
                    newest = at;
                }
            }
        }

        return newest ?? latest;
    }

    /// <summary>Renders one Task's Change log and Evidence.</summary>
    /// <param name="team">The named Team.</param>
    /// <param name="taskId">The Task id as the caller gave it.</param>
    /// <returns>The text, or a refusal.</returns>
    private string ReadTask(TeamSummary team, string taskId)
    {
        TaskItem? task = store.All.FirstOrDefault(item => string.Equals(item.Id.ToString(), taskId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (task is null)
        {
            return $"There is no Task {taskId.Trim()}.";
        }

        if (!string.Equals(task.Location.Team, team.Name, StringComparison.OrdinalIgnoreCase))
        {
            return $"Task {task.Id} is not in Team {team.Name}.";
        }

        StringBuilder text = new();
        _ = text.Append(CultureInfo.InvariantCulture, $"{task.Id} {task.Title} ({task.Status})").Append("\nchange log:");
        foreach (ChangeLogEntry entry in task.ChangeLog)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"\n{entry.At.UtcDateTime.ToString(MinuteFormat, CultureInfo.InvariantCulture)} {entry.Actor} {entry.Summary}");
        }

        IReadOnlyList<EvidenceLine> evidence = EvidenceLine.ParseSection(task.Description);
        _ = text.Append(evidence.Count == 0 ? "\nevidence: none" : "\nevidence:");
        foreach (EvidenceLine line in evidence)
        {
            _ = text.Append('\n').Append(line.RawText);
        }

        return Cap(text.ToString());
    }

    /// <summary>Renders the tail of one Room's transcript, provided a member of the Team has posted in it.</summary>
    /// <param name="team">The named Team.</param>
    /// <param name="room">The Room id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The text, or a refusal.</returns>
    private async Task<string> ReadRoomAsync(TeamSummary team, string room, CancellationToken cancellationToken)
    {
        string roomId = room.Trim();
        IReadOnlyList<ChatMessage> messages;
        try
        {
            messages = await chat.ReadAllAsync(roomId, cancellationToken);
        }
        catch (ArgumentException)
        {
            return $"Room '{roomId}' is not a valid Room id.";
        }

        if (!messages.Any(message => team.Members.Contains(message.SenderName, StringComparer.OrdinalIgnoreCase)))
        {
            return $"Room {roomId} has no messages from Team {team.Name}.";
        }

        string human = (await directory.GetHumanAsync(cancellationToken)).Name;
        if (messages.Any(message => !team.Members.Contains(message.SenderName, StringComparer.OrdinalIgnoreCase)
            && !string.Equals(message.SenderName, human, StringComparison.OrdinalIgnoreCase)))
        {
            return $"Room {roomId} has participants outside Team {team.Name}.";
        }

        List<string> lines = [.. messages
            .OrderBy(static message => message.Timestamp)
            .TakeLast(MaxMessages)
            .Select(static message => string.Concat(
                message.Timestamp.UtcDateTime.ToString(MinuteFormat, CultureInfo.InvariantCulture),
                " ",
                message.SenderName,
                ": ",
                Compact(message.Text)))];
        return JoinNewest(lines, MaxCharacters);
    }

    /// <summary>Collapses line breaks to spaces and keeps the first characters.</summary>
    /// <param name="text">The message text.</param>
    /// <returns>The compact text.</returns>
    private static string Compact(string text)
    {
        string flat = text.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\n', ' ').Replace('\r', ' ');
        return flat.Length <= MaxMessageCharacters ? flat : flat[..MaxMessageCharacters];
    }
}
