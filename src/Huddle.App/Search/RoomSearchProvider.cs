using Agency.Huddle.App.Data;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Search;

/// <summary>
/// Searches the Rooms (spec §8.5): the Room name, and the text of each Message in its Transcript.
/// Reads one Transcript at a time, and gives each Room at most one hit, carrying its match count and
/// its newest matching Message.
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
    /// Searches the Rooms of the pass that <paramref name="query"/> asks for. A blank phrase gives no hits,
    /// because Rooms have no browse. A Room whose Transcript cannot be read is skipped; when every Room in
    /// the pass fails, the search throws so the group is marked Failed.
    /// </summary>
    /// <param name="query">The search as the orchestrator received it.</param>
    /// <param name="ct">Cancels the search, checked between Rooms.</param>
    /// <returns>One hit per matching Room, not truncated.</returns>
    /// <exception cref="InvalidOperationException">Every Room in the pass had an unreadable Transcript.</exception>
    public async Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query.Text))
        {
            return SearchResult.Empty;
        }

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

            RoomHit? hit = Hit(room, messages, query.Text);
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
    /// The Room's hit for one phrase, or null when neither its name nor any Message matches. The tier is the
    /// name's tier when the name matches, and ContentMatch otherwise.
    /// </summary>
    /// <param name="room">The Room being judged.</param>
    /// <param name="messages">Its whole Transcript, in file order.</param>
    /// <param name="phrase">The phrase, ignoring case.</param>
    /// <returns>The hit, or null.</returns>
    private static RoomHit? Hit(Room room, IReadOnlyList<ChatMessage> messages, string phrase)
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
            newest?.SenderName,
            newest is null ? null : SearchSnippet.Around(newest.Text, phrase, SearchLimits.SnippetWidth),
            newest?.Timestamp,
            room.Archived,
            nameTier ?? MatchTier.ContentMatch);
    }
}
