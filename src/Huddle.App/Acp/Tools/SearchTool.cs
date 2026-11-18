namespace Agency.Huddle.App.Acp.Tools;

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Search;
using Agency.Huddle.App.Tasks;

/// <summary>
/// Finds Teams, Agents, Library entries, Tasks and Rooms for an Agent by exact phrase, the way Global Search does for
/// the Human. It calls the same providers and the same ranker, once per pass, with no debounce and no event flow. Rooms are
/// the sensitive category: by default only the Rooms the caller is a Member of, plus the Rooms scoped to one of the caller's
/// Teams, are read, and the list is passed to the Rooms provider so no other Transcript is opened. <c>scope: "all"</c>
/// searches every Room, and every such call is logged at Warning. The Chief of Staff searches every Room by default, logged at
/// Information. The Rooms group is not the Rooms provider's: <see cref="RoomGrep"/> reads the Transcripts so that every matching
/// Message is found, with grep-like options. Output is capped at <see cref="MaxCharacters"/> characters.
/// </summary>
/// <param name="providers">The five Global Search providers; the Rooms one is not used.</param>
/// <param name="directory">The Rooms, their Members, and the caller's identity.</param>
/// <param name="chats">Reads each Room's Transcript for <see cref="RoomGrep"/>.</param>
/// <param name="personas">The caller's Persona, for its Teams.</param>
/// <param name="logger">Records every search that reaches beyond the caller's own Rooms.</param>
/// <param name="prompts">The description's text.</param>
/// <param name="callerAgentId">The Agent this tool instance is bound to.</param>
/// <param name="goldfish">Tells whether the caller is a goldfish, which the tool refuses; optional.</param>
internal sealed class SearchTool(
    IEnumerable<ISearchProvider> providers,
    ITeamDirectory directory,
    IChatStore chats,
    PersonaStore personas,
    ILogger<SearchTool> logger,
    IPromptSource prompts,
    string callerAgentId,
    GoldfishGuard? goldfish = null) : IAppTool
{
    /// <summary>The most characters one call returns.</summary>
    private const int MaxCharacters = 12000;

    /// <summary>The default <c>limit</c> per group when searching every category.</summary>
    private const int DefaultAllLimit = SearchLimits.GroupCap;

    /// <summary>The default <c>limit</c> per group when searching one category.</summary>
    private const int DefaultSingleLimit = 20;

    private const string TruncatedNote = "\n... truncated";

    private const string ScopeMine = "mine";

    private const string ScopeAll = "all";

    /// <summary>The most Messages <c>context</c> may ask for on each side of a match.</summary>
    private const int MaxContext = 10;

    private const string RoomOnlyRefusal = "regex, context, count, room, team, sender, since and until apply to Rooms only; use filter \"rooms\".";

    private const string TimeoutRefusal = "The regular expression took too long to run; simplify it, for example by removing nested repeats.";

    private static readonly string[] FilterNames = ["all", "teams", "agents", "library", "tasks", "rooms"];

    /// <inheritdoc />
    public string Name => "search";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.search.description");

    /// <inheritdoc />
    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["query"] = new JsonObject { ["type"] = "string" },
            ["filter"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray([.. FilterNames.Select(name => JsonValue.Create(name))]) },
            ["scope"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray(ScopeMine, ScopeAll) },
            ["limit"] = new JsonObject { ["type"] = "integer" },
            ["regex"] = new JsonObject { ["type"] = "boolean" },
            ["context"] = new JsonObject { ["type"] = "integer" },
            ["count"] = new JsonObject { ["type"] = "boolean" },
            ["room"] = new JsonObject { ["type"] = "string" },
            ["team"] = new JsonObject { ["type"] = "string" },
            ["sender"] = new JsonObject { ["type"] = "string" },
            ["since"] = new JsonObject { ["type"] = "string" },
            ["until"] = new JsonObject { ["type"] = "string" },
        },
    };

    /// <inheritdoc />
    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (goldfish?.Refusal(callerAgentId) is { } goldfishRefusal)
        {
            return goldfishRefusal;
        }

        (TaskActor? actor, string actorRefusal) = await TaskToolText.ResolveActorAsync(directory, callerAgentId, cancellationToken).ConfigureAwait(false);
        if (actor is null)
        {
            return actorRefusal;
        }

        if (!TryReadArguments(arguments, out SearchArguments? read, out string refusal))
        {
            return refusal;
        }

        IReadOnlyList<string> teams = personas.ResolveByNameOrAlias(actor.Name)?.Teams ?? [];
        bool chief = this.IsChiefOfStaff(actor.Name);
        bool everyRoom = read.ScopeAll ?? chief;
        bool searchesRooms = read.Parse.Filter is SearchFilter.All or SearchFilter.Rooms;
        IReadOnlySet<string>? roomIds = null;
        if (searchesRooms && !everyRoom)
        {
            roomIds = await this.RoomsInScopeAsync(teams, cancellationToken).ConfigureAwait(false);
        }
        else if (searchesRooms)
        {
            this.LogEveryRoom(chief, actor.Name, read.Parse.Phrase);
        }

        string? openTeam = teams.Count == 1 ? teams[0] : null;
        IReadOnlyList<GroupResult> groups = await this.RunAsync(read, openTeam, cancellationToken).ConfigureAwait(false);
        StringBuilder text = RenderGroups(searchesRooms, everyRoom, groups, out bool any);
        RoomGrepResult? rooms = null;
        if (searchesRooms)
        {
            try
            {
                rooms = await new RoomGrep(directory, chats).SearchAsync(read.Grep, roomIds, MaxCharacters - text.Length, cancellationToken).ConfigureAwait(false);
            }
            catch (RegexMatchTimeoutException)
            {
                return TimeoutRefusal;
            }

            if (rooms.Refusal is not null)
            {
                return rooms.Refusal;
            }
        }

        return Finish(text, read, any, rooms);
    }

    /// <summary>Whether the caller is the built-in Chief of Staff, who may read every Room by default.</summary>
    private bool IsChiefOfStaff(string name) => personas.Entries.Any(entry =>
        string.Equals(entry.Builtin, BuiltinAgent.ChiefOfStaffMarker, StringComparison.Ordinal)
        && string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase));

    private void LogEveryRoom(bool chief, string agent, string phrase)
    {
        if (chief && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Chief of Staff {Agent} searched every Room for '{Query}'.", agent, phrase);
        }
        else if (!chief && logger.IsEnabled(LogLevel.Warning))
        {
            // A Warning, so the usual level filters keep it: this is the record that an Agent read past its own Rooms (ADR-0066).
            logger.LogWarning("Agent {Agent} searched every Room for '{Query}'.", agent, phrase);
        }
    }

    /// <summary>Reads and validates the arguments; a wrong one is refused with a sentence rather than thrown.</summary>
    private static bool TryReadArguments(JsonObject arguments, [NotNullWhen(true)] out SearchArguments? read, out string refusal)
    {
        read = null;
        if (!TaskToolText.TryGetString(arguments, "query", out string? query, out refusal)
            || !TaskToolText.TryGetString(arguments, "filter", out string? filterText, out refusal)
            || !TaskToolText.TryGetString(arguments, "scope", out string? scopeText, out refusal))
        {
            return false;
        }

        if (!TryParseFilter(filterText, out SearchFilter filter))
        {
            refusal = $"'filter' must be one of: {string.Join(", ", FilterNames)}.";
            return false;
        }

        if (scopeText is not null && !string.Equals(scopeText, ScopeMine, StringComparison.OrdinalIgnoreCase) && !string.Equals(scopeText, ScopeAll, StringComparison.OrdinalIgnoreCase))
        {
            refusal = $"'scope' must be '{ScopeMine}' or '{ScopeAll}'.";
            return false;
        }

        if (!TryReadRoomArguments(arguments, out RoomArguments? room, out refusal)
            || !TryReadQuery(query, filter, room, out SearchParse? parse, out RoomMatcher? matcher, out refusal))
        {
            return false;
        }

        int defaultLimit = parse.Filter == SearchFilter.All ? DefaultAllLimit : DefaultSingleLimit;
        if (!TaskToolText.TryGetInt(arguments, "limit", defaultLimit, out int limit, out refusal))
        {
            return false;
        }

        if (limit is < 1 or > SearchLimits.SingleFilterCap)
        {
            refusal = $"'limit' must be between 1 and {SearchLimits.SingleFilterCap}.";
            return false;
        }

        RoomGrepOptions grep = new(matcher, limit)
        {
            Context = room.Context,
            Count = room.Count,
            Room = room.Room,
            Team = room.Team,
            Sender = room.Sender,
            Since = room.Since,
            Until = room.Until,
        };
        bool? scopeAll = scopeText is null ? null : string.Equals(scopeText, ScopeAll, StringComparison.OrdinalIgnoreCase);
        read = new SearchArguments(parse, scopeAll, limit, grep);
        refusal = string.Empty;
        return true;
    }

    /// <summary>Reads the Room-only arguments; a wrong one is refused with a sentence.</summary>
    private static bool TryReadRoomArguments(JsonObject arguments, [NotNullWhen(true)] out RoomArguments? room, out string refusal)
    {
        room = null;
        if (!TaskToolText.TryGetBool(arguments, "regex", false, out bool regex, out refusal)
            || !TaskToolText.TryGetBool(arguments, "count", false, out bool count, out refusal)
            || !TaskToolText.TryGetString(arguments, "room", out string? roomText, out refusal)
            || !TaskToolText.TryGetString(arguments, "team", out string? team, out refusal)
            || !TaskToolText.TryGetString(arguments, "sender", out string? sender, out refusal)
            || !TaskToolText.TryGetString(arguments, "since", out string? sinceText, out refusal)
            || !TaskToolText.TryGetString(arguments, "until", out string? untilText, out refusal)
            || !TryReadContext(arguments, count, out int? context, out refusal)
            || !TryReadTime(sinceText, "since", false, out DateTimeOffset? since, out refusal)
            || !TryReadTime(untilText, "until", true, out DateTimeOffset? until, out refusal))
        {
            return false;
        }

        room = new RoomArguments(regex, context, count, Blank(roomText), Blank(team), Blank(sender), since, until);
        return true;
    }

    private static bool TryReadContext(JsonObject arguments, bool count, out int? context, out string refusal)
    {
        context = null;
        refusal = string.Empty;
        if (arguments["context"] is null)
        {
            return true;
        }

        if (!TaskToolText.TryGetInt(arguments, "context", 0, out int value, out refusal))
        {
            return false;
        }

        if (value is < 0 or > MaxContext)
        {
            refusal = $"'context' must be between 0 and {MaxContext}.";
            return false;
        }

        if (count)
        {
            refusal = "'count' and 'context' cannot be combined.";
            return false;
        }

        context = value;
        return true;
    }

    private static bool TryReadTime(string? text, string name, bool endOfDay, out DateTimeOffset? value, out string refusal)
    {
        value = null;
        refusal = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!RoomGrep.TryParseTime(text.Trim(), endOfDay, out DateTimeOffset parsed))
        {
            refusal = $"'{name}' must be a date like 2026-10-01 or an ISO 8601 time.";
            return false;
        }

        value = parsed;
        return true;
    }

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>
    /// Reads the query. Without one, a <c>room</c> alone is a tail read; a Room-only argument makes the call a Rooms search; a
    /// regular expression is used as written and a phrase goes through the prefix parser.
    /// </summary>
    private static bool TryReadQuery(string? query, SearchFilter filter, RoomArguments room, [NotNullWhen(true)] out SearchParse? parse, out RoomMatcher? matcher, out string refusal)
    {
        parse = null;
        matcher = null;
        refusal = string.Empty;
        if (room.Room is not null && string.IsNullOrWhiteSpace(query))
        {
            parse = new SearchParse(SearchFilter.Rooms, string.Empty, true);
            return true;
        }

        if (query is null)
        {
            refusal = "'query' is required.";
            return false;
        }

        if (room.Any && filter is not (SearchFilter.All or SearchFilter.Rooms))
        {
            refusal = RoomOnlyRefusal;
            return false;
        }

        if (room.IsRegex)
        {
            return TryReadRegex(query.Trim(), out parse, out matcher, out refusal);
        }

        parse = SearchQueryParser.Parse(query, room.Any ? SearchFilter.Rooms : filter);
        if (room.Any && parse.Filter != SearchFilter.Rooms)
        {
            refusal = RoomOnlyRefusal;
            return false;
        }

        if (parse.Phrase.StartsWith('>'))
        {
            refusal = "A query cannot start with '>'.";
            return false;
        }

        if (!parse.IsSearchable)
        {
            refusal = $"The query needs at least {SearchLimits.MinChars} characters. Only an empty query for teams or agents browses.";
            return false;
        }

        matcher = parse.Filter is SearchFilter.All or SearchFilter.Rooms ? RoomMatcher.ForPhrase(parse.Phrase) : null;
        return true;
    }

    private static bool TryReadRegex(string pattern, out SearchParse? parse, out RoomMatcher? matcher, out string refusal)
    {
        parse = null;
        refusal = string.Empty;
        if (pattern.Length == 0)
        {
            matcher = null;
            refusal = "The query needs at least 1 non-blank character.";
            return false;
        }

        if (!RoomMatcher.TryForRegex(pattern, out matcher, out string error))
        {
            refusal = $"'query' is not a valid regular expression: {error}";
            return false;
        }

        parse = new SearchParse(SearchFilter.Rooms, pattern, true);
        return true;
    }

    /// <summary>Maps the <c>filter</c> argument; an absent one is All.</summary>
    private static bool TryParseFilter(string? text, out SearchFilter filter)
    {
        filter = SearchFilter.All;
        if (text is null)
        {
            return true;
        }

        int index = Array.FindIndex(FilterNames, name => string.Equals(name, text, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return false;
        }

        filter = (SearchFilter)index;
        return true;
    }

    /// <summary>
    /// The Rooms the caller may search by default: every Room scoped to one of the caller's Teams, and every Room the
    /// caller is a Member of (the Team Room is both).
    /// </summary>
    private async Task<IReadOnlySet<string>> RoomsInScopeAsync(IReadOnlyList<string> teams, CancellationToken cancellationToken)
    {
        IReadOnlyList<Room> member = await directory.GetRoomsForUserAsync(callerAgentId, cancellationToken).ConfigureAwait(false);
        HashSet<string> ids = new(member.Select(room => room.Id), StringComparer.Ordinal);
        if (teams.Count == 0)
        {
            return ids;
        }

        IReadOnlyList<Room> rooms = await directory.GetRoomsAsync(cancellationToken).ConfigureAwait(false);
        foreach (Room room in rooms.Where(room => room.Team is not null && teams.Contains(room.Team, StringComparer.OrdinalIgnoreCase)))
        {
            _ = ids.Add(room.Id);
        }

        return ids;
    }

    /// <summary>Runs every selected provider for both passes at once and ranks each batch.</summary>
    private async Task<IReadOnlyList<GroupResult>> RunAsync(SearchArguments read, string? openTeam, CancellationToken cancellationToken)
    {
        List<Task<GroupResult>> runs = [];
        foreach (ISearchProvider provider in providers.Where(provider => provider.Group != SearchFilter.Rooms && (read.Parse.Filter == SearchFilter.All || provider.Group == read.Parse.Filter)))
        {
            foreach (SearchScope scope in new[] { SearchScope.Active, SearchScope.Archived })
            {
                SearchQuery query = new(read.Parse.Phrase, read.Parse.Filter, openTeam, scope, read.Limit, SearchPurpose.Box, null);
                runs.Add(this.RunProviderAsync(provider, query, cancellationToken));
            }
        }

        return await Task.WhenAll(runs).ConfigureAwait(false);
    }

    /// <summary>One provider, one pass; a failure empties this group alone (D-20).</summary>
    private async Task<GroupResult> RunProviderAsync(ISearchProvider provider, SearchQuery query, CancellationToken cancellationToken)
    {
        try
        {
            SearchResult result = await provider.SearchAsync(query, cancellationToken).ConfigureAwait(false);
            return new GroupResult(provider.Group, query.Scope, SearchResultRanker.Rank(result, query), false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // The supervisor that must not die: one provider failing empties only its own group, as in the Human's dropdown.
            // A cancellation the caller did not ask for, such as a provider's own timeout, lands here too.
#pragma warning disable HAA0601 // Boxing a value-type log argument is the cost of ILogger params object[]; accepted together with HAA0101.
            logger.LogWarning(ex, "Search provider {Group} failed for an Agent.", provider.Group);
#pragma warning restore HAA0601
            return new GroupResult(provider.Group, query.Scope, SearchResult.Empty, true);
        }
    }

    /// <summary>Renders the header and the provider groups in the dropdown's fixed order; the Rooms section follows.</summary>
    private static StringBuilder RenderGroups(bool searchesRooms, bool everyRoom, IReadOnlyList<GroupResult> groups, out bool any)
    {
        StringBuilder text = new();
        if (searchesRooms)
        {
            text.Append(everyRoom
                ? "Rooms searched: every Room."
                : "Rooms searched: yours and your Teams' only. Pass scope \"all\" to search every Room.");
        }

        any = false;
        HashSet<string> failed = new(StringComparer.Ordinal);
        foreach (GroupResult group in groups.OrderBy(group => Order(group.Filter, group.Scope)))
        {
            if (group.Failed)
            {
                string heading = Heading(group.Filter, group.Scope);
                if (failed.Add(heading))
                {
                    text.Append("\n\n").Append("Couldn't search ").Append(heading).Append('.');
                }

                any = true;
            }
            else if (group.Result.Hits.Count > 0)
            {
                AppendGroup(text, group);
                any = true;
            }
        }

        return text;
    }

    /// <summary>Adds the Rooms section, or the no-results line, and caps the whole text at <see cref="MaxCharacters"/>.</summary>
    private static string Finish(StringBuilder text, SearchArguments read, bool any, RoomGrepResult? rooms)
    {
        if (rooms is { Unreadable: true })
        {
            text.Append("\n\nCouldn't search Rooms.");
            any = true;
        }
        else if (rooms is { Text.Length: > 0 })
        {
            text.Append("\n\n").Append(rooms.Text);
            any = true;
        }

        if (!any)
        {
            text.Append('\n').Append("No results for '").Append(read.Parse.Phrase).Append("'.");
            if (read.Parse.Phrase.Contains(' ', StringComparison.Ordinal))
            {
                text.Append(" Search matches the whole phrase, in order; it does not look for each word on its own. Don't conclude nothing exists: try one or two words.");
            }
        }

        string rendered = text.ToString().TrimStart('\n');
        if (rendered.Length <= MaxCharacters)
        {
            return rendered;
        }

        // Never cut between the two halves of a surrogate pair: a lone high surrogate is not valid text.
        int cut = char.IsHighSurrogate(rendered[MaxCharacters - 1]) ? MaxCharacters - 1 : MaxCharacters;
        return rendered[..cut] + TruncatedNote;
    }

    private static void AppendGroup(StringBuilder text, GroupResult group)
    {
        text.Append("\n\n").Append(Heading(group.Filter, group.Scope)).Append(':');
        foreach (SearchHit hit in group.Result.Hits)
        {
            text.Append("\n- ").Append(Line(hit));
        }

        if (group.Result.Truncated)
        {
            text.Append("\nResults may be incomplete.");
        }
    }

    /// <summary>One hit as a single line of ids and names, with no UI link.</summary>
    private static string Line(SearchHit hit)
    {
        return hit switch
        {
            TeamHit team => $"{team.Name} | {team.TypeLabel ?? "no type"} | {team.AgentCount} agents",
            AgentHit agent => $"{agent.Name} | {agent.PersonaTitle} | {(agent.Teams.Count == 0 ? "no team" : string.Join(", ", agent.Teams))}",
            LibraryHit library => $"{library.RootId}:{library.RelativePath}{(library.IsFolder ? " (folder)" : string.Empty)}{SnippetOf(library.Snippet)}",
            TaskHit task => $"{task.Id} | {task.Status.ToWire()} | {task.Assignee ?? "unassigned"} | {task.Team} | {task.TaskTitle}",
            _ => hit.Title,
        };
    }

    private static string SnippetOf(string? snippet)
    {
        return string.IsNullOrWhiteSpace(snippet) ? string.Empty : "\n    " + snippet.ReplaceLineEndings(" ");
    }

    private static int Order(SearchFilter filter, SearchScope scope) => ((int)filter * 2) + (int)scope;

    private static string Heading(SearchFilter filter, SearchScope scope)
    {
        return (filter, scope) switch
        {
            (SearchFilter.Tasks, SearchScope.Archived) => "Closed Tasks",
            (SearchFilter.Rooms, SearchScope.Archived) => "Archived Rooms",
            (SearchFilter.Teams, _) => "Teams",
            (SearchFilter.Agents, _) => "Agents",
            (SearchFilter.Library, _) => "Library",
            (SearchFilter.Tasks, _) => "Tasks",
            (SearchFilter.Rooms, _) => "Rooms",
            _ => "Results",
        };
    }

    /// <summary>The read arguments: the parsed query, the explicit <c>scope</c> (null when absent), the limit and the Rooms search.</summary>
    private sealed record SearchArguments(SearchParse Parse, bool? ScopeAll, int Limit, RoomGrepOptions Grep);

    /// <summary>The Room-only arguments; <see cref="Any"/> says whether the caller asked for any of them.</summary>
    private sealed record RoomArguments(bool IsRegex, int? Context, bool Count, string? Room, string? Team, string? Sender, DateTimeOffset? Since, DateTimeOffset? Until)
    {
        public bool Any => this.IsRegex || this.Context is not null || this.Count || this.Room is not null || this.Team is not null
            || this.Sender is not null || this.Since is not null || this.Until is not null;
    }

    private sealed record GroupResult(SearchFilter Filter, SearchScope Scope, SearchResult Result, bool Failed);
}
