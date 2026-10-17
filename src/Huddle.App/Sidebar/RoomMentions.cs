using Agency.Huddle.App.References;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Sidebar;

/// <summary>One Task or Library file a Room's Messages mention.</summary>
/// <param name="Key">The Task id or the file path, as written.</param>
/// <param name="Label">What to show: the snapshot's Label, or the key (Task) or file name (file) when scanned.</param>
/// <param name="LatestMessageId">The id of the newest Message that mentions the key.</param>
/// <param name="Count">The number of Messages (not occurrences) that mention the key.</param>
internal sealed record RoomMention(string Key, string Label, string LatestMessageId, int Count);

/// <summary>Collects what a Room's Messages mention; the one function both Room panels read.</summary>
internal static class RoomMentions
{
    /// <summary>The path separators of both OSes.</summary>
    private static readonly char[] Separators = ['/', '\\'];

    /// <summary>
    /// Collects the distinct mentions of one kind, the most recently mentioned first. A Message with a
    /// <c>References</c> list uses it (an empty list means none); a Message with null falls back to the scanner.
    /// Files are keyed case-insensitively, as <see cref="ReferenceScanner"/> does; Task ids ordinally.
    /// </summary>
    /// <param name="messages">The Room's Messages, oldest first.</param>
    /// <param name="kind">The kind to collect.</param>
    /// <returns>The mentions, newest mention first.</returns>
    internal static IReadOnlyList<RoomMention> Collect(IReadOnlyList<ChatMessage> messages, ReferenceKind kind)
    {
        ArgumentNullException.ThrowIfNull(messages);

        StringComparer comparer = kind == ReferenceKind.LibraryFile ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        Dictionary<string, RoomMention> byKey = new(comparer);
        List<string> order = [];

        for (int index = messages.Count - 1; index >= 0; index--)
        {
            ChatMessage message = messages[index];
            HashSet<string> seenInMessage = new(comparer);

            foreach ((string key, string label) in RoomMentions.Mentions(message, kind))
            {
                if (!seenInMessage.Add(key))
                {
                    continue;
                }

                if (byKey.TryGetValue(key, out RoomMention? existing))
                {
                    byKey[key] = existing with { Count = existing.Count + 1 };
                }
                else
                {
                    byKey[key] = new RoomMention(key, label, message.Id, 1);
                    order.Add(key);
                }
            }
        }

        return order.Select(key => byKey[key]).ToList();
    }

    /// <summary>The key and label pairs one Message mentions for a kind.</summary>
    private static IEnumerable<(string Key, string Label)> Mentions(ChatMessage message, ReferenceKind kind)
    {
        if (message.References is { } references)
        {
            return references.Where(reference => reference.Kind == kind).Select(static reference => (reference.Key, reference.Label));
        }

        bool isFile = kind == ReferenceKind.LibraryFile;
        return ReferenceScanner.Scan(message.Text)
            .Where(candidate => candidate.Kind == kind)
            .Select(candidate => (candidate.Key, isFile ? RoomMentions.FileName(candidate.Key) : candidate.Key));
    }

    /// <summary>The text after the last '/' or '\', so a path written for either OS gives the same name on any OS.</summary>
    private static string FileName(string key)
    {
        return key[(key.LastIndexOfAny(RoomMentions.Separators) + 1)..];
    }
}
