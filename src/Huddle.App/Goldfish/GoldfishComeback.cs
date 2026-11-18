using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Goldfish;

/// <summary>Recognises the exact "@Ruth comeback" Message (spec 6.7) that calls a possessed Agent back.</summary>
internal static class GoldfishComeback
{
    private const string Word = " comeback";

    /// <summary>
    /// Whether <paramref name="text"/> is, as a whole, a Mention of <paramref name="possessed"/> followed by the word
    /// comeback. Case is ignored and any run of white space counts as one space; nothing else may appear.
    /// </summary>
    /// <param name="text">The Message text.</param>
    /// <param name="possessed">The Agent in goldfish mode.</param>
    /// <param name="members">The Room's Members, which decide where a Name ends.</param>
    /// <param name="aliases">Every Alias currently known.</param>
    /// <param name="postedByGoldfish">Whether the goldfish session itself sent the Message; its own comeback never matches.</param>
    internal static bool Matches(string text, User possessed, IReadOnlyList<User> members, IReadOnlyList<MentionAlias> aliases, bool postedByGoldfish)
    {
        if (postedByGoldfish)
        {
            return false;
        }

        string collapsed = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (!collapsed.EndsWith(Word, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string head = collapsed[..^Word.Length];
        return head.StartsWith('@')
            && NamesPossessed(head[1..], possessed, aliases)
            && IsOnly(MentionParser.Parse(head, members, aliases), possessed)
            && IsOnly(MentionParser.Parse(collapsed, members, aliases), possessed);
    }

    /// <summary>Whether the handle is the Agent's Name or one of its Aliases.</summary>
    private static bool NamesPossessed(string handle, User possessed, IReadOnlyList<MentionAlias> aliases)
    {
        return string.Equals(handle, possessed.Name, StringComparison.OrdinalIgnoreCase)
            || aliases.Any(alias => string.Equals(alias.Alias, handle, StringComparison.OrdinalIgnoreCase)
                && string.Equals(alias.Name, possessed.Name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Whether the Mentions are exactly the possessed Agent and nobody else.</summary>
    private static bool IsOnly(IReadOnlyList<User> mentioned, User possessed)
    {
        return mentioned is [var only] && string.Equals(only.Id, possessed.Id, StringComparison.Ordinal);
    }
}
