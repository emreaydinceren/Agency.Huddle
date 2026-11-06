namespace Agency.Huddle.App.Acp.Tools;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Search;
using Agency.Huddle.App.Tasks;

/// <summary>
/// Finds Teams, Agents, Library entries, Tasks and Rooms for an Agent by exact phrase, the way Global Search does for
/// the Human. It calls the same providers and the same ranker, once per pass, with no debounce and no event flow. Rooms are
/// the sensitive category: by default only the Rooms the caller is a Member of, plus the Rooms scoped to one of the caller's
/// Teams, are read, and the list is passed to the Rooms provider so no other Transcript is opened. <c>scope: "all"</c>
/// searches every Room, and every such call is logged. Output is capped at <see cref="MaxCharacters"/> characters.
/// </summary>
/// <param name="providers">The five Global Search providers.</param>
/// <param name="directory">The Rooms, their Members, and the caller's identity.</param>
/// <param name="personas">The caller's Persona, for its Teams.</param>
/// <param name="logger">Records every search that reaches beyond the caller's own Rooms.</param>
/// <param name="prompts">The description's text.</param>
/// <param name="callerAgentId">The Agent this tool instance is bound to.</param>
internal sealed class SearchTool(
    IEnumerable<ISearchProvider> providers,
    ITeamDirectory directory,
    PersonaStore personas,
    ILogger<SearchTool> logger,
    IPromptSource prompts,
    string callerAgentId) : IAppTool
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
        },
        ["required"] = new JsonArray("query"),
    };

    /// <inheritdoc />
    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

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
        bool searchesRooms = read.Parse.Filter is SearchFilter.All or SearchFilter.Rooms;
        IReadOnlySet<string>? roomIds = null;
        if (searchesRooms && !read.AllRooms)
        {
            roomIds = await this.RoomsInScopeAsync(teams, cancellationToken).ConfigureAwait(false);
        }
        else if (searchesRooms && logger.IsEnabled(LogLevel.Warning))
        {
            // A Warning, so the usual level filters keep it: this is the record that an Agent read past its own Rooms (ADR-0066).
            logger.LogWarning("Agent {Agent} searched every Room for '{Query}'.", actor.Name, read.Parse.Phrase);
        }

        string? openTeam = teams.Count == 1 ? teams[0] : null;
        IReadOnlyList<GroupResult> groups = await this.RunAsync(read, openTeam, roomIds, cancellationToken).ConfigureAwait(false);
        return Render(read, searchesRooms, groups);
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

        if (query is null)
        {
            refusal = "'query' is required.";
            return false;
        }

        SearchParse parse = SearchQueryParser.Parse(query, filter);
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

        read = new SearchArguments(parse, string.Equals(scopeText, ScopeAll, StringComparison.OrdinalIgnoreCase), limit);
        refusal = string.Empty;
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
    private async Task<IReadOnlyList<GroupResult>> RunAsync(SearchArguments read, string? openTeam, IReadOnlySet<string>? roomIds, CancellationToken cancellationToken)
    {
        List<Task<GroupResult>> runs = [];
        foreach (ISearchProvider provider in providers.Where(provider => read.Parse.Filter == SearchFilter.All || provider.Group == read.Parse.Filter))
        {
            foreach (SearchScope scope in new[] { SearchScope.Active, SearchScope.Archived })
            {
                SearchQuery query = new(read.Parse.Phrase, read.Parse.Filter, openTeam, scope, read.Limit, SearchPurpose.Box, roomIds);
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

    /// <summary>Renders the groups in the dropdown's fixed order, capped at <see cref="MaxCharacters"/>.</summary>
    private static string Render(SearchArguments read, bool searchesRooms, IReadOnlyList<GroupResult> groups)
    {
        StringBuilder text = new();
        if (searchesRooms)
        {
            text.Append(read.AllRooms
                ? "Rooms searched: every Room."
                : "Rooms searched: yours and your Teams' only. Pass scope \"all\" to search every Room.");
        }

        bool any = false;
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
            RoomHit room => RoomLine(room),
            _ => hit.Title,
        };
    }

    private static string RoomLine(RoomHit room)
    {
        string matches = string.Empty;
        if (room.MatchCount > 0)
        {
            string noun = room.MatchCount == 1 ? "match" : "matches";
            string when = room.Time?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture) ?? string.Empty;
            matches = string.Create(CultureInfo.InvariantCulture, $" | {room.MatchCount} {noun} | message {room.MessageId} | {room.SenderName} | {when}");
        }

        return $"{room.RoomId} | {room.RoomName}{matches}{SnippetOf(room.Snippet)}";
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

    private sealed record SearchArguments(SearchParse Parse, bool AllRooms, int Limit);

    private sealed record GroupResult(SearchFilter Filter, SearchScope Scope, SearchResult Result, bool Failed);
}
