using Agency.Huddle.App.Data;
using Agency.Huddle.Contracts;

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
    /// <summary>
    /// The handle that Mentions every Agent in the Room at once, so each takes a Turn. It never Mentions the
    /// Human: it is a request for the Agents' Turns, and a Mention of the Human raises a Needs You card.
    /// </summary>
    internal const string AllHandle = "all";

    /// <summary>
    /// Parses <paramref name="text"/> for Mentions of <paramref name="members"/> by Name only. Delegates
    /// to <see cref="Parse(string, IReadOnlyList{User}, IReadOnlyList{MentionAlias})"/> with an empty
    /// Alias list, so the ~20 existing call sites and tests that only ever dealt with Names keep working
    /// exactly as before.
    /// </summary>
    /// <param name="text">The Message text to scan.</param>
    /// <param name="members">The Room's Members. A Mention can only ever resolve to one of these.</param>
    public static IReadOnlyList<User> Parse(string text, IReadOnlyList<User> members) => Parse(text, members, []);

    /// <summary>
    /// Parses <paramref name="text"/> for Mentions of <paramref name="members"/>, matched either by a
    /// Member's Name or by a Persona's Alias.
    /// </summary>
    /// <param name="text">The Message text to scan.</param>
    /// <param name="members">The Room's Members. A Mention can only ever resolve to one of these.</param>
    /// <param name="aliases">
    /// Every Alias currently known, library-wide - typically <see cref="Agency.Huddle.App.Acp.PersonaStore"/>'s
    /// full set via <see cref="IMentionAliasSource"/>, unfiltered by this Room's membership. An Alias
    /// whose owning Name is not one of <paramref name="members"/> contributes no candidate: Aliases are
    /// unique across the whole Team, but a Mention can only ever resolve to someone actually in this Room.
    /// </param>
    public static IReadOnlyList<User> Parse(string text, IReadOnlyList<User> members, IReadOnlyList<MentionAlias> aliases)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(aliases);

        var candidates = BuildCandidates(members, aliases);

        var result = new List<User>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < text.Length; i++)
        {
            if (!CanStartMention(text, i))
            {
                continue;
            }

            foreach (var candidate in candidates)
            {
                var start = i + 1;
                var end = start + candidate.Handle.Length;

                if (!MatchesHandleAt(text, start, candidate.Handle))
                {
                    continue;
                }

                foreach (var member in candidate.Members)
                {
                    if (seenIds.Add(member.Id))
                    {
                        result.Add(member);
                    }
                }

                // Resume past the handle just consumed (the loop's i++ steps onto `end`), so the
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

    /// <summary>
    /// Builds every handle a Mention can resolve to, longest first, from the Members' Names and the
    /// Aliases of those Members.
    /// </summary>
    /// <param name="members">The Room's Members.</param>
    /// <param name="aliases">Every Alias currently known, library-wide.</param>
    /// <returns>The candidates, ordered by descending handle length.</returns>
    private static List<Candidate> BuildCandidates(IReadOnlyList<User> members, IReadOnlyList<MentionAlias> aliases)
    {
        // Longest handle first, so "@Emily Lee" resolves to "Emily Lee" and not to "Emily" in a Room
        // that holds both. The longer reading is the safer default: a writer who meant Emily had no
        // reason to type her surname after her Name. Names and Aliases are sorted together in ONE
        // list, not scanned in two separate passes: a two-pass approach (all Names first, Aliases only
        // as a fallback) would let a short Name win here whenever it happens to sit at a real word
        // boundary - exactly the "Emily"/"Emily Lee" trap, just triggered by an Alias instead of a
        // second Name.
        //
        // Names are concatenated BEFORE Aliases, deliberately: LINQ's OrderByDescending is a stable
        // sort, so when a Name and an Alias tie in length, whichever one appears first in the
        // un-sorted sequence wins that tie. Swap the two Concat operands below and an equal-length
        // Alias would start silently beating a Name, with no compiler or analyser signal -
        // Parse_NameBeatsAnEqualLengthAlias in MentionParserTests pins this ordering so a future edit
        // cannot make that swap unnoticed. "@all" comes last for the same reason: a Member actually
        // called "All" is named, not swept up with everyone else.
        return members
            .Where(member => member.Name.Length > 0)
            .Select(member => new Candidate(member.Name, [member]))
            .Concat(ResolveAliasCandidates(members, aliases))
            .Concat(ResolveAllCandidate(members))
            .OrderByDescending(candidate => candidate.Handle.Length)
            .ToList();
    }

    /// <summary>
    /// Yields the <see cref="AllHandle"/> candidate, resolving to every Agent in <paramref name="members"/>, or
    /// nothing when the Room holds no Agent.
    /// </summary>
    /// <param name="members">The Room's Members.</param>
    private static IEnumerable<Candidate> ResolveAllCandidate(IReadOnlyList<User> members)
    {
        List<User> agents = [.. members.Where(static member => member.Kind == UserKind.Agent)];
        if (agents.Count > 0)
        {
            yield return new Candidate(AllHandle, agents);
        }
    }

    /// <summary>
    /// Whether the character at <paramref name="index"/> is an <c>@</c> that can introduce a Mention: an
    /// <c>@</c> not joined to the character before it (see <see cref="IsBlockedBeforeAt"/>). Shared with
    /// <see cref="MarkdownRenderer"/>, so a Mention pill is drawn exactly where a Mention is counted.
    /// </summary>
    /// <param name="text">The text being scanned.</param>
    /// <param name="index">The position to test; must lie inside <paramref name="text"/>.</param>
    internal static bool CanStartMention(string text, int index)
    {
        return text[index] == '@' && (index == 0 || !IsBlockedBeforeAt(text[index - 1]));
    }

    /// <summary>
    /// Whether <paramref name="text"/> holds <paramref name="handle"/> at <paramref name="start"/>, compared
    /// <see cref="StringComparison.OrdinalIgnoreCase"/>, and ending at a word boundary - so "@echo" never
    /// Mentions a Member called "ech". A space is NOT a boundary blocker: whether the space after "Emily"
    /// continues her Name is precisely what the longest-first member list answers. Shared with
    /// <see cref="MarkdownRenderer"/>.
    /// </summary>
    /// <param name="text">The text being scanned.</param>
    /// <param name="start">The position just after the <c>@</c>.</param>
    /// <param name="handle">The Name or Alias to look for.</param>
    internal static bool MatchesHandleAt(string text, int start, string handle)
    {
        int end = start + handle.Length;
        if (end > text.Length ||
            string.Compare(text, start, handle, 0, handle.Length, StringComparison.OrdinalIgnoreCase) != 0)
        {
            return false;
        }

        return end == text.Length || !IsNameCharacter(text[end]);
    }

    // '@' itself is here so "you@@echo" is not a Mention, and '_' and the alphanumerics so an
    // email address ("me@example.com") is not one either — the character before the '@' shows that
    // the '@' is joining two things rather than introducing a Name. '-' is deliberately absent:
    // "see-@echo" is a Mention.
    private static bool IsBlockedBeforeAt(char c)
    {
        return char.IsAsciiLetterOrDigit(c) || c == '_' || c == '@';
    }

    /// <summary>Whether <paramref name="c"/> can continue a Name, so a handle ends at the first character that cannot. Shared with <see cref="Agency.Huddle.App.Acp.AdapterCommandInvocation"/>.</summary>
    internal static bool IsNameCharacter(char c)
    {
        return char.IsAsciiLetterOrDigit(c) || c == '_' || c == '-';
    }

    /// <summary>
    /// Projects every Alias whose owning Name matches one of <paramref name="members"/> (compared
    /// <see cref="StringComparison.OrdinalIgnoreCase"/>) into a <see cref="Candidate"/> pointing at that
    /// Member. An Alias belonging to nobody in this Room yields nothing - <c>PersonaIndex</c>'s
    /// uniqueness guarantee holds library-wide, not Room-wide, so an Alias can easily name someone who
    /// simply is not here.
    /// </summary>
    private static IEnumerable<Candidate> ResolveAliasCandidates(IReadOnlyList<User> members, IReadOnlyList<MentionAlias> aliases)
    {
        foreach (var alias in aliases)
        {
            if (alias.Alias.Length == 0)
            {
                continue;
            }

            var owner = members.FirstOrDefault(member => string.Equals(member.Name, alias.Name, StringComparison.OrdinalIgnoreCase));
            if (owner is not null)
            {
                yield return new Candidate(alias.Alias, [owner]);
            }
        }
    }

    /// <summary>One handle - a Member's Name, a Persona's Alias or <see cref="AllHandle"/> - paired with the Members it resolves to, for the candidate scan.</summary>
    private sealed record Candidate(string Handle, IReadOnlyList<User> Members);
}