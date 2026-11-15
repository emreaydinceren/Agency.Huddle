using System.Globalization;
using System.Text;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Search;

/// <summary>
/// Reads Room Transcripts the way <c>grep</c> reads files, for an Agent: every matching Message rather than one hit per
/// Room, optional context lines around each match, per-Room counts, a Room's tail, and filters on Room, Team, sender and
/// time. It never reads a Transcript outside the Rooms it is given, and it stops adding hits before the output budget is
/// passed so a cut lands between hits. The Human's Global Search does not use it.
/// </summary>
/// <param name="directory">The Rooms, and the Human's current Name.</param>
/// <param name="chats">Reads each Room's Transcript.</param>
internal sealed class RoomGrep(ITeamDirectory directory, IChatStore chats)
{
    private const string RoomsHeading = "Rooms:";

    /// <summary>Characters kept free for the notes that follow the hits.</summary>
    private const int Reserve = 260;

    /// <summary>The most characters of one Message shown in a line.</summary>
    private const int LineWidth = 300;

    private const string MoreHint = "Narrow with room, sender, since or until";

    /// <summary>Parses a <c>since</c> or <c>until</c> value: a date, or a full ISO 8601 time.</summary>
    /// <param name="text">The argument's text.</param>
    /// <param name="endOfDay">Whether a bare date means the end of that day, which makes it an exclusive bound.</param>
    /// <param name="value">The instant, or the default when this returns false.</param>
    public static bool TryParseTime(string text, bool endOfDay, out DateTimeOffset value)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
        {
            DateTimeOffset start = new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            value = endOfDay ? start.AddDays(1) : start;
            return true;
        }

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out value);
    }

    /// <summary>
    /// The indexes of the windows of Messages to show for sorted match indexes: <paramref name="context"/> Messages either
    /// side of each, with windows that overlap or touch merged into one.
    /// </summary>
    /// <param name="indexes">The matching Messages' indexes, ascending.</param>
    /// <param name="context">How many Messages to take on each side.</param>
    /// <param name="count">How many Messages the Room has.</param>
    public static IReadOnlyList<(int Start, int End)> Windows(IReadOnlyList<int> indexes, int context, int count)
    {
        ArgumentNullException.ThrowIfNull(indexes);
        List<(int Start, int End)> windows = [];
        foreach (int index in indexes)
        {
            int start = Math.Max(0, index - context);
            int end = Math.Min(count - 1, index + context);
            if (windows.Count > 0 && start <= windows[^1].End + 1)
            {
                windows[^1] = (windows[^1].Start, Math.Max(windows[^1].End, end));
            }
            else
            {
                windows.Add((start, end));
            }
        }

        return windows;
    }

    /// <summary>One Message as a line: its time, sender and text with line breaks collapsed and cut to a fixed width.</summary>
    /// <param name="message">The Message.</param>
    /// <param name="human">The Human's current Name.</param>
    /// <param name="prefix">What starts the line: <c>"&gt; "</c> for a match, two spaces for context.</param>
    public static string MessageLine(ChatMessage message, string? human, string prefix)
    {
        ArgumentNullException.ThrowIfNull(message);
        string text = message.Text.ReplaceLineEndings(" ").Trim();
        if (text.Length > LineWidth)
        {
            int cut = char.IsHighSurrogate(text[LineWidth - 1]) ? LineWidth - 1 : LineWidth;
            text = text[..cut] + "…";
        }

        string when = message.Timestamp.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        string sender = SenderDisplay.NameOf(message, human);
        return $"{prefix}{when} {sender}: {text}";
    }

    /// <summary>
    /// The indexes of the Messages that pass the sender and time filters and match the query (every Message when there is no
    /// query), ascending.
    /// </summary>
    /// <param name="messages">The Room's Messages, oldest first.</param>
    /// <param name="options">The search.</param>
    /// <param name="human">The Human's current Name.</param>
    public static List<int> MatchIndexes(IReadOnlyList<ChatMessage> messages, RoomGrepOptions options, string? human)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(options);
        List<int> indexes = [];
        for (int index = 0; index < messages.Count; index++)
        {
            ChatMessage message = messages[index];
            bool passes = (options.Since is null || message.Timestamp >= options.Since)
                && (options.Until is null || message.Timestamp < options.Until)
                && (options.Sender is null || string.Equals(SenderDisplay.NameOf(message, human), options.Sender, StringComparison.OrdinalIgnoreCase));
            if (passes && (options.Matcher is null || options.Matcher.IsMatch(message.Text)))
            {
                indexes.Add(index);
            }
        }

        return indexes;
    }

    /// <summary>Searches the Rooms in scope.</summary>
    /// <param name="options">The search.</param>
    /// <param name="roomIds">The Rooms the caller may read, or null for every Room. No other Transcript is opened.</param>
    /// <param name="budget">The most characters the Rooms section should take.</param>
    /// <param name="ct">Cancels the search, checked between Rooms.</param>
    /// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">The query's regular expression ran past its timeout.</exception>
    public async Task<RoomGrepResult> SearchAsync(RoomGrepOptions options, IReadOnlySet<string>? roomIds, int budget, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);
        IReadOnlyList<Room> all = await directory.GetRoomsAsync(ct).ConfigureAwait(false);
        string? refusal = SelectRooms(all, options, roomIds, out List<Room> rooms);
        if (refusal is not null)
        {
            return new RoomGrepResult(string.Empty, refusal);
        }

        string? human = (await directory.GetUserAsync(KnownIds.Human, ct).ConfigureAwait(false))?.Name;
        bool tail = options.Matcher is null && !options.Count;
        bool nameLines = options.Matcher is not null && !options.Count && options.Sender is null && options.Since is null && options.Until is null;
        List<RoomMatches> found = [];
        List<Room> byName = [];
        int failures = 0;
        foreach (Room room in rooms)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                IReadOnlyList<ChatMessage> messages = await chats.ReadAllAsync(room.Id, ct).ConfigureAwait(false);
                List<ChatMessage> ordered = [.. messages.OrderBy(message => message.Timestamp)];
                List<int> indexes = MatchIndexes(ordered, options, human);
                if (indexes.Count > 0 || tail)
                {
                    found.Add(new RoomMatches(room, ordered, indexes));
                }
                else if (nameLines && options.Matcher is { } matcher && matcher.IsMatch(room.Name))
                {
                    byName.Add(room);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failures++;
            }
        }

        if (rooms.Count > 0 && failures == rooms.Count)
        {
            return new RoomGrepResult(string.Empty, Unreadable: true);
        }

        string text = tail ? RenderTail(found, options, human, budget) : RenderMatches(found, byName, options, human, budget);
        if (failures > 0)
        {
            text = (text.Length == 0 ? RoomsHeading : text) + $"\nCouldn't read {failures} Rooms.";
        }

        return new RoomGrepResult(text);
    }

    private static string? SelectRooms(IReadOnlyList<Room> all, RoomGrepOptions options, IReadOnlySet<string>? roomIds, out List<Room> rooms)
    {
        rooms = [];
        IEnumerable<Room> candidates = all;
        if (options.Room is { } wanted)
        {
            List<Room> named = [.. all.Where(room => string.Equals(room.Id, wanted, StringComparison.Ordinal))];
            if (named.Count == 0)
            {
                named = [.. all.Where(room => string.Equals(room.Name, wanted, StringComparison.OrdinalIgnoreCase))];
            }

            if (named.Count == 0)
            {
                return $"There is no Room '{wanted}'.";
            }

            if (named.Count > 1)
            {
                string ids = string.Join(", ", named.Select(room => room.Id));
                return $"More than one Room is named '{wanted}': {ids}. Pass one of these ids as room.";
            }

            if (roomIds is not null && !roomIds.Contains(named[0].Id))
            {
                return $"Room '{wanted}' is outside the Rooms you searched; pass scope \"all\".";
            }

            candidates = named;
        }

        rooms = [.. candidates.Where(room => (roomIds is null || roomIds.Contains(room.Id))
            && (options.Team is null || (room.Team is not null && string.Equals(room.Team, options.Team, StringComparison.OrdinalIgnoreCase))))];
        return null;
    }

    private static string RenderTail(List<RoomMatches> found, RoomGrepOptions options, string? human, int budget)
    {
        if (found.Count == 0)
        {
            return string.Empty;
        }

        RoomMatches room = found[0];
        int take = Math.Min(options.Limit, room.Indexes.Count);
        List<string> lines = [.. room.Indexes.Skip(room.Indexes.Count - take).Select(index => MessageLine(room.Messages[index], human, "  "))];
        int space = Math.Max(0, budget - Reserve - Heading(room.Room).Length);
        while (lines.Count > 1 && lines.Sum(line => line.Length + 1) > space)
        {
            lines.RemoveAt(0);
        }

        int earlier = room.Indexes.Count - lines.Count;
        StringBuilder text = new StringBuilder(RoomsHeading).Append('\n').Append(Heading(room.Room));
        if (lines.Count == 0)
        {
            return text.Append("\n  No Messages match.").ToString();
        }

        if (earlier > 0)
        {
            DateTimeOffset oldest = room.Messages[room.Indexes[earlier]].Timestamp.ToUniversalTime();
            text.Append('\n').Append(CultureInfo.InvariantCulture, $"{earlier} earlier Messages; pass until {oldest.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture)} to read further back.");
        }

        foreach (string line in lines)
        {
            text.Append('\n').Append(line);
        }

        return text.ToString();
    }

    private static string RenderMatches(List<RoomMatches> found, List<Room> byName, RoomGrepOptions options, string? human, int budget)
    {
        if (found.Count == 0 && byName.Count == 0)
        {
            return string.Empty;
        }

        int total = found.Sum(room => room.Indexes.Count);
        List<RoomMatches> shown = options.Count || total <= options.Limit ? found : KeepNewest(found, options.Limit);
        List<Block> blocks = [.. shown
            .OrderByDescending(room => room.Messages[room.Indexes[^1]].Timestamp)
            .Select(room => options.Count ? CountBlock(room) : MatchBlock(room, options, human))];
        List<string> names = [.. byName.OrderByDescending(room => room.Created).Select(room => $"{room.Id} | {room.Name} | name matches")];
        StringBuilder text = new(RoomsHeading);
        (int skippedMessages, int skippedNames) = Emit(text, blocks, names, options.Limit, Math.Max(0, budget - Reserve));
        if (!options.Count && total > options.Limit)
        {
            text.Append('\n').Append(CultureInfo.InvariantCulture, $"{total - options.Limit} more matching Messages not shown. {MoreHint}, or raise limit (at most {SearchLimits.SingleFilterCap}).");
        }

        if (skippedMessages > 0)
        {
            text.Append('\n').Append(CultureInfo.InvariantCulture, $"{skippedMessages} more matching Messages not shown (output cap). {MoreHint}.");
        }

        if (skippedNames > 0)
        {
            text.Append('\n').Append(CultureInfo.InvariantCulture, $"{skippedNames} more Rooms matching by name not shown.");
        }

        if (options.Count)
        {
            text.Append('\n').Append(CultureInfo.InvariantCulture, $"Total: {total} {Noun(total)} in {found.Count} Rooms.");
        }

        return text.ToString();
    }

    /// <summary>Appends blocks and name lines while they fit; returns how many matches and name lines were left out.</summary>
    private static (int Messages, int Names) Emit(StringBuilder text, List<Block> blocks, List<string> names, int nameLimit, int space)
    {
        int skippedMessages = 0;
        bool full = false;
        foreach (Block block in blocks)
        {
            string? heading = block.Heading;
            bool headed = heading is null;
            foreach (Unit unit in block.Units)
            {
                int needed = unit.Text.Length + 1 + (heading is null || headed ? 0 : heading.Length + 1);
                if (full || text.Length + needed > space)
                {
                    full = true;
                    skippedMessages += unit.Matches;
                    continue;
                }

                if (!headed)
                {
                    text.Append('\n').Append(heading);
                    headed = true;
                }

                text.Append('\n').Append(unit.Text);
            }
        }

        int shownNames = 0;
        foreach (string line in names.Take(nameLimit))
        {
            if (full || text.Length + line.Length + 1 > space)
            {
                break;
            }

            text.Append('\n').Append(line);
            shownNames++;
        }

        return (skippedMessages, names.Count - shownNames);
    }

    private static List<RoomMatches> KeepNewest(List<RoomMatches> found, int limit)
    {
        HashSet<(int Room, int Index)> keep = [.. found
            .SelectMany((room, roomIndex) => room.Indexes.Select(index => (Room: roomIndex, Index: index, room.Messages[index].Timestamp)))
            .OrderByDescending(entry => entry.Timestamp)
            .ThenBy(entry => entry.Room)
            .ThenByDescending(entry => entry.Index)
            .Take(limit)
            .Select(entry => (entry.Room, entry.Index))];
        return [.. found
            .Select((room, roomIndex) => room with { Indexes = [.. room.Indexes.Where(index => keep.Contains((roomIndex, index)))] })
            .Where(room => room.Indexes.Count > 0)];
    }

    private static Block CountBlock(RoomMatches room)
    {
        int count = room.Indexes.Count;
        return new Block(null, [new Unit($"{room.Room.Id} | {room.Room.Name} | {count.ToString(CultureInfo.InvariantCulture)} {Noun(count)}", count)]);
    }

    private static Block MatchBlock(RoomMatches room, RoomGrepOptions options, string? human)
    {
        return new Block(Heading(room.Room), options.Context is { } context ? ContextUnits(room, context, human) : SnippetUnits(room, options, human));
    }

    private static List<Unit> SnippetUnits(RoomMatches room, RoomGrepOptions options, string? human)
    {
        List<Unit> units = [];
        foreach (int index in room.Indexes)
        {
            ChatMessage message = room.Messages[index];
            string when = message.Timestamp.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
            string snippet = options.Matcher?.Snippet(message.Text, SearchLimits.SnippetWidth) ?? string.Empty;
            string body = snippet.Length == 0 ? string.Empty : "\n    " + snippet;
            units.Add(new Unit($"  message {message.Id} | {SenderDisplay.NameOf(message, human)} | {when}{body}", 1));
        }

        return units;
    }

    private static List<Unit> ContextUnits(RoomMatches room, int context, string? human)
    {
        HashSet<int> matches = [.. room.Indexes];
        List<Unit> units = [];
        foreach ((int start, int end) in Windows(room.Indexes, context, room.Messages.Count))
        {
            List<string> lines = [.. Enumerable.Range(start, end - start + 1).Select(index => MessageLine(room.Messages[index], human, matches.Contains(index) ? "> " : "  "))];
            string separator = units.Count == 0 ? string.Empty : "--\n";
            units.Add(new Unit(separator + string.Join('\n', lines), Enumerable.Range(start, end - start + 1).Count(matches.Contains)));
        }

        return units;
    }

    private static string Heading(Room room)
    {
        string mark = room.Archived ? " (archived)" : string.Empty;
        return $"{room.Id} | {room.Name}{mark}";
    }

    private static string Noun(int count) => count == 1 ? "match" : "matches";

    private sealed record RoomMatches(Room Room, List<ChatMessage> Messages, List<int> Indexes);

    private sealed record Block(string? Heading, List<Unit> Units);

    private sealed record Unit(string Text, int Matches);
}
