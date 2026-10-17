using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Search;

/// <summary>
/// Searches the Rooms (spec §8.5). Under the Box purpose: the Room name, and the text of each Message in its
/// Transcript, one hit per Room with its match count and newest matching Message. Under the Reference purpose
/// (spec §5.2, D-29): the Rooms themselves, matched on name then Team name, with no Transcript read.
/// </summary>
/// <param name="directory">The Rooms, and each one's Archived flag.</param>
/// <param name="chats">Reads each Room's Transcript.</param>
/// <param name="logger">Records each Transcript that had to be skipped.</param>
internal sealed class RoomSearchProvider(ITeamDirectory directory, IChatStore chats, ILogger<RoomSearchProvider> logger) : ISearchProvider
{
    /// <summary>The Rooms group.</summary>
    public SearchFilter Group => SearchFilter.Rooms;

    /// <summary>Rooms are read from disk, so this provider waits for the debounce.</summary>
    public bool IsExpensive => true;

    /// <summary>
    /// Searches the Rooms of the pass that <paramref name="query"/> asks for. Under the Box purpose a blank phrase gives
    /// no hits and an unreadable Transcript is skipped; under the Reference purpose no Transcript is read at all.
    /// </summary>
    /// <param name="query">The search as the orchestrator received it.</param>
    /// <param name="ct">Cancels the search, checked between Rooms.</param>
    /// <returns>One hit per matching Room, not truncated.</returns>
    /// <exception cref="InvalidOperationException">Every Room in the pass had an unreadable Transcript (Box purpose only).</exception>
    public async Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken ct)
    {
        if (query.Purpose == SearchPurpose.Reference)
        {
            return await this.NamesOnlyAsync(query, ct);
        }

        if (string.IsNullOrWhiteSpace(query.Text))
        {
            return SearchResult.Empty;
        }

        string? human = (await directory.GetUserAsync(KnownIds.Human, ct))?.Name;
        IReadOnlyList<Room> rooms = await directory.GetRoomsAsync(ct);
        List<Room> inPass = rooms.Where(room => room.Archived == (query.Scope == SearchScope.Archived)).ToList();
        List<SearchHit> hits = [];
        int failures = 0;
        foreach (Room room in inPass)
        {
            ct.ThrowIfCancellationRequested();
            IReadOnlyList<ChatMessage> messages;
            try
            {
                messages = await chats.ReadAllAsync(room.Id, ct);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning("Search skipped room {RoomId}: {Reason}", room.Id, ex.Message);
                failures++;
                continue;
            }

            RoomHit? hit = Hit(room, messages, query.Text, human);
            if (hit is not null)
            {
                hits.Add(hit);
            }
        }

        if (inPass.Count > 0 && failures == inPass.Count)
        {
            throw new InvalidOperationException("No Transcript could be read.");
        }

        return new SearchResult(hits, false);
    }

    /// <summary>
    /// The Reference purpose (D-29): every Room of the pass whose name, or whose Team's name, matches the phrase, with
    /// no match count, snippet or Message. An empty phrase lists every Room of the pass, newest first.
    /// </summary>
    /// <param name="query">The picker's query.</param>
    /// <param name="ct">Checked between Rooms.</param>
    /// <returns>One row per Room, in the provider's order.</returns>
    private async Task<SearchResult> NamesOnlyAsync(SearchQuery query, CancellationToken ct)
    {
        IReadOnlyList<Room> rooms = await directory.GetRoomsAsync(ct);
        bool archived = query.Scope == SearchScope.Archived;
        List<SearchHit> hits = [];
        foreach (Room room in rooms.Where(room => room.Archived == archived).OrderByDescending(room => room.Created))
        {
            ct.ThrowIfCancellationRequested();
            MatchTier? tier = query.Text.Length == 0 ? MatchTier.ExactName : SearchMatch.Tier(room.Name, query.Text);
            if (tier is null && room.Team is { } team && team.Contains(query.Text, StringComparison.OrdinalIgnoreCase))
            {
                tier = MatchTier.NameContains;
            }

            if (tier is { } matched)
            {
                hits.Add(new RoomHit(room.Id, room.Name, 0, null, null, null, null, room.Archived, matched, room.Team, room.IsTeamRoom));
            }
        }

        return new SearchResult(hits, false);
    }

    /// <summary>
    /// The Room's hit for one phrase, or null when neither its name nor any Message matches. The tier is the
    /// name's tier when the name matches, and ContentMatch otherwise.
    /// </summary>
    /// <param name="room">The Room being judged.</param>
    /// <param name="messages">Its whole Transcript, in file order.</param>
    /// <param name="phrase">The phrase, ignoring case.</param>
    /// <param name="human">The Human's current Name, shown for the Human's own Messages whatever Name they were posted under.</param>
    /// <returns>The hit, or null.</returns>
    private static RoomHit? Hit(Room room, IReadOnlyList<ChatMessage> messages, string phrase, string? human)
    {
        int count = 0;
        ChatMessage? newest = null;
        foreach (ChatMessage message in messages)
        {
            if (!message.Text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            count++;
            if (newest is null || message.Timestamp >= newest.Timestamp)
            {
                newest = message;
            }
        }

        MatchTier? nameTier = SearchMatch.Tier(room.Name, phrase);
        if (nameTier is null && count == 0)
        {
            return null;
        }

        return new RoomHit(
            room.Id,
            room.Name,
            count,
            newest?.Id,
            newest is null ? null : SenderDisplay.NameOf(newest, human),
            newest is null ? null : SearchSnippet.Around(newest.Text, phrase, SearchLimits.SnippetWidth),
            newest?.Timestamp,
            room.Archived,
            nameTier ?? MatchTier.ContentMatch,
            room.Team,
            room.IsTeamRoom);
    }
}
