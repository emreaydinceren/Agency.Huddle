using Agency.Huddle.App.Data;

namespace Agency.Huddle.App.Services;

/// <summary>
/// Finds the Members Mentioned in a Message.
/// </summary>
/// <remarks>
/// This used to be one regex that read a Mention's shape straight out of the text. That stopped
/// working when a Name gained the right to contain spaces, because "@Emily Lee" and "@Emily"
/// followed by the word "Lee" are the same characters: no pattern can tell them apart without
/// knowing who is actually in the Room. So the member list — which this method has always been
/// handed — decides where a Name ends, and the text is only asked whether it starts with one.
/// </remarks>
public static class MentionParser
{
    public static IReadOnlyList<User> Parse(string text, IReadOnlyList<User> members)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(members);

        // Longest Name first, so "@Emily Lee" resolves to "Emily Lee" and not to "Emily" in a Room
        // that holds both. The longer reading is the safer default: a writer who meant Emily had no
        // reason to type her surname after her Name.
        var candidates = members
            .Where(member => member.Name.Length > 0)
            .OrderByDescending(member => member.Name.Length)
            .ToList();

        var result = new List<User>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '@' || (i > 0 && IsBlockedBeforeAt(text[i - 1])))
            {
                continue;
            }

            foreach (var member in candidates)
            {
                var start = i + 1;
                var end = start + member.Name.Length;

                if (end > text.Length ||
                    string.Compare(text, start, member.Name, 0, member.Name.Length, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    continue;
                }

                // A Mention ends at a word boundary, so "@echo" never Mentions a Member called
                // "ech". Note that a space is NOT a boundary blocker: whether the space after
                // "Emily" continues her Name is precisely what the member list just answered.
                if (end < text.Length && IsNameCharacter(text[end]))
                {
                    continue;
                }

                if (seenIds.Add(member.Id))
                {
                    result.Add(member);
                }

                // Resume past the Name just consumed (the loop's i++ steps onto `end`), so the
                // words inside a multi-word Name cannot be re-read as a Mention of someone else.
                // This is a deliberate scan-advance, not an accidental mutation of the loop's stop
                // condition: without it, a multi-word Name like "Emily Lee" would let "Lee" be
                // re-scanned as a possible Mention of a different Member.
#pragma warning disable S127 // Deliberate scan-advance (see comment above), not the accidental stop-condition mutation the rule guards against.
                i = end - 1;
#pragma warning restore S127
                break;
            }
        }

        return result;
    }

    // '@' itself is here so "you@@echo" is not a Mention, and '_' and the alphanumerics so an
    // email address ("me@example.com") is not one either — the character before the '@' shows that
    // the '@' is joining two things rather than introducing a Name. '-' is deliberately absent:
    // "see-@echo" is a Mention.
    private static bool IsBlockedBeforeAt(char c)
    {
        return char.IsAsciiLetterOrDigit(c) || c == '_' || c == '@';
    }

    private static bool IsNameCharacter(char c)
    {
        return char.IsAsciiLetterOrDigit(c) || c == '_' || c == '-';
    }
}